import json
from uuid import uuid4
import httpx
import pytest
from fastapi.testclient import TestClient
from app.config import Settings
from app.main import create_app
from app.models import FeedbackRequest
from app.provider import OpenAIProvider,mock_answer
from app.knowledge import Knowledge

@pytest.fixture
def env(tmp_path):
    settings=Settings(database=str(tmp_path/'test.sqlite3'),rpm=100,daily=200)
    app=create_app(settings)
    token=app.state.store.provision('alice')
    return app,TestClient(app),{'Authorization':'Bearer '+token},settings

def payload(question='Explain FIFO and FEFO',**kwargs):
    return dict(requestId=str(uuid4()),question=question,language='en',**kwargs)

def post(env,question='Explain FIFO and FEFO',**kwargs):
    return env[1].post('/api/feedback',headers=env[2],json=payload(question,**kwargs))

SCENARIOS=[
 ('Five sunscreen boxes requested but three present','SOP-SHORT-PICK','EXC-001','shortage of 2'),
 ('Perfume leaking with broken cap','SOP-DAMAGED','EXC-002','Quality Hold'),
 ('Faded T-shirt barcode: manual input?','SOP-RELABEL','EXC-005','Never guess'),
 ('A01 has 65-inch TV instead of 55-inch','SOP-WRONG-BIN','EXC-003','Do not substitute'),
 ('Broken pallet obstructs the aisle','SOP-BLOCKED-PATH','EXC-004','clearance'),
]

@pytest.mark.parametrize('question,document,code,phrase',SCENARIOS)
def test_five_scenarios(env,question,document,code,phrase):
    response=post(env,question)
    assert response.status_code==200,response.text
    data=response.json()
    assert data['sources'][0]['documentId']==document
    assert data['action']['exceptionCode']==code
    assert phrase in data['message']
    assert data['operationalMutation'] is False
    assert data['actionExecution']=='proposal_only'
    assert data['mode']=='mock'

@pytest.mark.parametrize('language,question,expected',[
 ('vi','Kem chống nắng thiếu hai hộp','SOP-SHORT-PICK'),
 ('vi','Nước hoa rò rỉ','SOP-DAMAGED'),
 ('vi','Mã vạch áo bị mờ','SOP-RELABEL'),
 ('vi','Tivi 65 thay vì 55 ở A01','SOP-WRONG-BIN'),
 ('vi','Pallet chặn lối đi','SOP-BLOCKED-PATH'),
 ('zh','防晒产品缺货','SOP-SHORT-PICK'),
 ('zh','香水泄漏','SOP-DAMAGED'),
 ('zh','条码褪色可以手动输入吗','SOP-RELABEL'),
 ('zh','A01电视65不是55','SOP-WRONG-BIN'),
 ('zh','托盘堵塞通道','SOP-BLOCKED-PATH'),
])
def test_multilingual(env,language,question,expected):
    data=payload(question);data['language']=language
    result=env[1].post('/api/feedback',headers=env[2],json=data)
    assert result.status_code==200,result.text
    assert result.json()['sources'][0]['documentId']==expected
    assert result.json()['language']==language

def test_auth_and_schema(env):
    client=env[1]
    assert client.get('/api/health').status_code==200
    assert client.post('/api/feedback',json=payload()).status_code==401
    assert post(env,role='supervisor').status_code==422
    assert post(env,observation={'expectedQuantity':99}).status_code==422
    assert post(env,taskId='TEST-PICK-999').status_code==403
    assert post(env,locale='zh-Hant').status_code==422
    assert post(env,' '*10).status_code==422
    assert post(env,'a'*2001).status_code==422
    assert client.post('/api/feedback',headers=env[2],content=b'x'*17000).status_code==413

def test_continuity_isolation_and_restart(env):
    first=post(env,'Perfume leaking').json()
    sid=first['sessionId']
    second=post(env,'Why is that?',sessionId=sid).json()
    assert second['sources'][0]['documentId']=='SOP-DAMAGED'
    assert second['contextTurns']==1
    other=env[0].state.store.provision('bob')
    assert env[1].post('/api/feedback',headers={'Authorization':'Bearer '+other},json=payload(sessionId=sid)).status_code==404
    assert post(env,sessionId=sid,taskId='TEST-PUT-001').status_code==409
    restarted=TestClient(create_app(env[3]))
    assert restarted.post('/api/feedback',headers=env[2],json=payload('Why?',sessionId=sid)).json()['contextTurns']==2

def test_idempotent_retry_and_inflight(env):
    data=payload()
    a=env[1].post('/api/feedback',headers=env[2],json=data)
    b=env[1].post('/api/feedback',headers=env[2],json=data)
    assert a.json()==b.json()
    data['question']='Something else'
    assert env[1].post('/api/feedback',headers=env[2],json=data).status_code==409
    store=env[0].state.store
    user=store.authenticate(env[2]['Authorization'][7:])
    req=FeedbackRequest(**payload())
    session,_,_=store.begin(user,req,env[3])
    assert env[1].post('/api/feedback',headers=env[2],json=req.model_dump(mode='json')).status_code==409
    store.release(session['id'],user['id'],req.requestId)

def test_missing_coverage_and_numeric_safety(env):
    data=post(env,'What is the capital of France?').json()
    assert not data['sources'] and data['needsClarification']
    safety=post(env,'What forklift speed is allowed?').json()
    assert 'unverified' in safety['message']
    assert '10 km' not in safety['message']
    assert safety['sources'][0]['documentId']=='SOP-SAFETY'

def test_occupied_location_no_fake_route(env):
    data=post(env,'Where to put SKU-2048? B-12-03 is occupied',taskId='TEST-PUT-001',observation={'scanFailureCount':2}).json()
    assert data['sources'][0]['documentId']=='SOP-PUT-AWAY'
    assert 'no alternative' in data['message']
    assert data['action']['targetId'] is None

def test_rate_limit_persistent(env):
    env[3].rpm=1
    assert post(env).status_code==200
    assert post(env).status_code==429
    client=TestClient(create_app(env[3]))
    assert client.post('/api/feedback',headers=env[2],json=payload()).status_code==429

def provider_env(tmp_path,handler):
    settings=Settings(database=str(tmp_path/'live.sqlite3'),mode='openai',api_key='unit-test-secret',model='test-model')
    provider=OpenAIProvider(settings,httpx.MockTransport(handler))
    app=create_app(settings,provider)
    token=app.state.store.provision('test','icqa')
    return TestClient(app),{'Authorization':'Bearer '+token}

def valid_answer():
    req=FeedbackRequest(**payload())
    chunks=Knowledge().retrieve(req.question)
    return mock_answer(req,chunks).model_dump()

def response_for(answer):
    return httpx.Response(200,json={'status':'completed','output':[{'type':'message','content':[{'type':'output_text','text':json.dumps(answer)}]}]})

def test_responses_contract_and_blind_count(tmp_path):
    captured=[]
    def handler(request):
        body=json.loads(request.content);captured.append(body)
        assert request.url.path=='/v1/responses'
        assert body['store'] is False
        assert body['text']['format']['type']=='json_schema'
        assert body['text']['format']['strict'] is True
        citation_schema=body['text']['format']['schema']['properties']['sources']['items']['anyOf']
        assert any(c['properties']['documentId']['enum']==['SOP-LOT'] and
                   c['properties']['sectionId']['enum']==['fifo-fefo'] for c in citation_schema)
        assert 'expectedQuantity' not in json.dumps(body)
        return response_for(valid_answer())
    client,headers=provider_env(tmp_path,handler)
    first=client.post('/api/feedback',headers=headers,json=payload(taskId='TEST-COUNT-001'))
    assert first.status_code==200,first.text
    second=client.post('/api/feedback',headers=headers,json=payload('Why is that?',taskId='TEST-COUNT-001',sessionId=first.json()['sessionId']))
    assert second.status_code==200
    context=json.loads(captured[1]['input'][0]['content'])
    assert len(context['recentConversation'])==2
    assert context['trustedFacts']['role']=='icqa'
    assert context['trustedFacts']['inventoryTransactions']==[]

@pytest.mark.parametrize('status,expected',[(401,502),(403,502),(429,503),(400,502),(500,502)])
def test_provider_errors_redacted(tmp_path,status,expected):
    client,headers=provider_env(tmp_path,lambda r:httpx.Response(status,text='unit-test-secret sensitive-camera-content'))
    response=client.post('/api/feedback',headers=headers,json=payload())
    assert response.status_code==expected
    assert 'unit-test-secret' not in response.text and 'sensitive-camera' not in response.text

@pytest.mark.parametrize('failure',['bad-json','refusal','incomplete','citation','unknown-action','target','exception','no-sources','timeout','cooldown'])
def test_invalid_outputs_fail_closed(tmp_path,failure):
    def handler(request):
        answer=valid_answer()
        if failure=='bad-json':return httpx.Response(200,text='not json')
        if failure=='refusal':return httpx.Response(200,json={'status':'completed','output':[]})
        if failure=='incomplete':return httpx.Response(200,json={'status':'incomplete'})
        if failure=='timeout':raise httpx.ReadTimeout('secret',request=request)
        if failure=='citation':answer['sources'][0]['documentId']='FORGED'
        if failure=='unknown-action':answer['action']['type']='adjust_stock'
        if failure=='target':answer['action'].update(type='show_navigation_route',targetId='invented')
        if failure=='exception':answer['action'].update(type='open_exception_report',exceptionCode='EXC-002')
        if failure=='no-sources':answer['sources']=[]
        if failure=='cooldown':answer['cooldownSeconds']=999
        return response_for(answer)
    client,headers=provider_env(tmp_path,handler)
    result=client.post('/api/feedback',headers=headers,json=payload())
    assert result.status_code==(504 if failure=='timeout' else 502),result.text

def test_static_ui_and_source_endpoint(env):
    assert env[1].get('/').status_code==200
    assert env[1].get('/static/app.js').status_code==200
    path='/api/knowledge/SOP-LOT/fifo-fefo'
    assert env[1].get(path).status_code==401
    source=env[1].get(path,headers=env[2]).json()
    assert source['site_approved'] is False
    assert source['source_status']=='spec_confirmed_project_policy'
    assert env[1].post('/api/picks',headers=env[2],json={'quantity':3}).status_code==404

def test_mock_never_calls_provider(env):
    async def fail(*args):raise AssertionError('mock attempted provider call')
    env[0].state.provider.answer=fail
    assert post(env).status_code==200

def test_reports_are_retained_as_unverified_not_completed(env):
    first=post(env,'Barcode scan failed',eventType='scan_failure',observation={'scanFailureCount':2}).json()
    with env[0].state.store.connect() as db:
        history=json.loads(db.execute('SELECT history FROM sessions WHERE id=?',(first['sessionId'],)).fetchone()[0])
    report=json.loads(history[0]['content'])
    assert report['observation']['scanFailureCount']==2
    assert report['evidenceStatus']=='worker_report_not_verified_transaction'

def test_safety_metadata_and_provenance():
    from app.config import ROOT
    unverified=json.loads((ROOT/'knowledge'/'unverified.json').read_text(encoding='utf-8'))
    assert not unverified['site_approved'] and not unverified['runtime_approved']
    assert unverified['settings']['loaded_speed_kmh']==10
    for chunk in Knowledge().chunks:
        assert chunk['version'] and chunk['source'] and not chunk['site_approved']
    assert Knowledge().retrieve('quantum mechanics')==[]

@pytest.mark.parametrize('question,missing',[
    ('cần 5 hộp kem nhưng chỉ có 3 tôi phải làm gì ?',2),
    ('5 hộp kem nhưng chỉ có 3 tôi cần làm gì ?',2),
    ('5 hop kem nhung chi co 3 toi can lam gi?',2),
    ('Nhiệm vụ yêu cầu 10 hộp kem nhưng chỉ có 4 hộp.',6),
])
def test_vietnamese_numeric_shortage(env,question,missing):
    data=payload(question)
    data['language']='vi'
    response=env[1].post('/api/feedback',headers=env[2],json=data)
    assert response.status_code==200
    assert response.json()['sources'][0]['documentId']=='SOP-SHORT-PICK'
    assert response.json()['action']['exceptionCode']=='EXC-001'
    assert f'thiếu {missing} hộp' in response.json()['message']
    assert response.json()['needsClarification'] is False
    assert response.json()['clarifyingQuestion'] is None
    assert 'Bạn đang nói đến nhiệm vụ' not in response.json()['message']
    assert Knowledge().retrieve('cần 3 hộp kem nhưng chỉ có 5')==[]

def test_live_paraphrase_not_blocked_by_keywords(tmp_path):
    calls=[]
    def handler(request):
        context=json.loads(json.loads(request.content)['input'][0]['content'])
        calls.append(context)
        assert len(context['retrievedDocuments'])==12
        assert all(isinstance(c['text'],str) for c in context['retrievedDocuments'])
        assert all(c['runtime_approved'] for c in context['retrievedDocuments'])
        return response_for(valid_answer())
    client,headers=provider_env(tmp_path,handler)
    result=client.post('/api/feedback',headers=headers,json=payload('How should I choose which goods to take first?'))
    assert result.status_code==200,result.text
    assert len(calls)==1

def test_live_out_of_scope_clarification_without_fake_citation(tmp_path):
    answer=valid_answer()
    answer.update(message='I help with warehouse procedures. What warehouse question can I help with?',
                  sources=[],needsClarification=True,clarifyingQuestion='What warehouse question can I help with?')
    answer['action']={'type':'none','targetId':None,'exceptionCode':None}
    client,headers=provider_env(tmp_path,lambda r:response_for(answer))
    result=client.post('/api/feedback',headers=headers,json=payload('Hello'))
    assert result.status_code==200,result.text
    assert result.json()['sources']==[]
    assert result.json()['sourceDetails']==[]

def test_unused_exception_parameter_is_removed(tmp_path):
    answer=valid_answer()
    answer['action']['exceptionCode']='EXC-001'
    client,headers=provider_env(tmp_path,lambda r:response_for(answer))
    result=client.post('/api/feedback',headers=headers,json=payload())
    assert result.status_code==200,result.text
    assert result.json()['action']['type']=='explain_sop_step'
    assert result.json()['action']['exceptionCode'] is None

def test_render_demo_bootstrap_and_restart(tmp_path):
    import hashlib
    token='synthetic-test-token-not-for-deployment'
    digest=hashlib.sha256(token.encode()).hexdigest()
    for database in ['first.sqlite3','fresh-filesystem.sqlite3']:
        client=TestClient(create_app(Settings(database=str(tmp_path/database),prototype_token_sha256=digest)))
        result=client.get('/api/me',headers={'Authorization':'Bearer '+token})
        assert result.status_code==200
        assert result.json()['role']=='staff'
        assert result.json()['id']=='render-demo'
        assert client.get('/api/me',headers={'Authorization':'Bearer wrong'}).status_code==401

def test_render_demo_hash_validation(tmp_path):
    with pytest.raises(ValueError,match='SHA-256'):
        create_app(Settings(database=str(tmp_path/'bad.sqlite3'),prototype_token_sha256='raw-token'))
