import logging
import time
from uuid import uuid4
from fastapi import FastAPI, HTTPException, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import FileResponse, JSONResponse
from fastapi.staticfiles import StaticFiles
from .config import ROOT, Settings
from .knowledge import Knowledge
from .models import FeedbackRequest
from .provider import OpenAIProvider, mock_answer, validate_answer
from .store import Store
from .warehouse import WarehouseAdapter

log=logging.getLogger('warehouse')

def create_app(settings=None,provider=None):
    settings=settings or Settings.from_env()
    store=Store(settings.database)
    if settings.prototype_token_sha256:
        store.bootstrap_demo(settings.prototype_token_sha256)
    knowledge=Knowledge()
    warehouse=WarehouseAdapter()
    provider=provider or OpenAIProvider(settings)
    app=FastAPI(title='Warehouse AI — Milestone A',version='0.1.0')
    app.state.store=store
    app.state.provider=provider
    app.state.settings=settings
    app.state.live_verified=False

    @app.middleware('http')
    async def boundaries(request:Request,call_next):
        trace=str(uuid4())
        started=time.monotonic()
        size=0
        body=[]
        if request.method in ('POST','PUT','PATCH'):
            async for chunk in request.stream():
                size+=len(chunk)
                if size>16384:
                    return JSONResponse({'detail':'Request body exceeds 16 KiB','traceId':trace},status_code=413)
                body.append(chunk)
            request._body=b''.join(body)
        try:
            response=await call_next(request)
        except Exception:
            # Do not log exception text: provider errors can include request content.
            response=JSONResponse({'detail':'Internal service error; inspect configuration','traceId':trace},status_code=500)
        response.headers['X-Request-ID']=trace
        response.headers['Cache-Control']='no-store'
        response.headers['X-Content-Type-Options']='nosniff'
        response.headers['Content-Security-Policy']="default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'"
        log.info('request=%s status=%s latency_ms=%s',trace,response.status_code,round((time.monotonic()-started)*1000))
        return response

    @app.exception_handler(RequestValidationError)
    async def validation_error(request,exc):
        return JSONResponse({'detail':'Invalid request schema','fields':[{'location':e['loc'],'type':e['type']} for e in exc.errors()]},status_code=422)

    def auth(request):
        header=request.headers.get('Authorization','')
        if not header.startswith('Bearer ') or len(header)>512:
            raise HTTPException(401,'Bearer prototype user token required')
        return store.authenticate(header[7:])

    @app.get('/api/health')
    def health():
        with store.connect() as db:
            db.execute('SELECT 1')
        return {'status':'ok','mode':settings.mode,'milestone':'A','synthetic':True,'liveApiVerified':app.state.live_verified}

    @app.get('/api/me')
    def me(request:Request):
        return auth(request)

    @app.get('/api/knowledge/{document_id}/{section_id}')
    def source(document_id:str,section_id:str,request:Request):
        auth(request)
        for chunk in knowledge.chunks:
            if (chunk['documentId'],chunk['sectionId'])==(document_id,section_id):
                return chunk
        raise HTTPException(404,'Knowledge source not found')

    @app.post('/api/feedback')
    async def feedback(req:FeedbackRequest,request:Request):
        user=auth(request)
        if req.locale and req.locale.split('-')[0]!=req.language:
            raise HTTPException(422,'locale must match language')
        started=time.monotonic()
        session,cached,fingerprint=store.begin(user,req,settings)
        if cached:
            return cached
        try:
            facts=warehouse.facts(user,req.taskId)
            chunks=knowledge.retrieve(req.question,session['topic'])
            if req.eventType=='summary_request' and not chunks and session['topic']:
                chunks=knowledge.retrieve('summary of that',session['topic'])
            if settings.mode=='mock':
                answer=mock_answer(req,chunks)
            else:
                model_chunks=knowledge.for_model(req.question,session['topic'],req.language)
                answer=await provider.answer(req,model_chunks,session['history'],facts)
                chunks=knowledge.chunks
            if chunks:
                answer=validate_answer(answer,chunks,facts)
            if settings.mode=='openai':
                app.state.live_verified=True
            result=answer.model_dump()
            result.update(sessionId=session['id'],requestId=str(req.requestId),
                          language=req.language,locale=req.locale or (settings.chinese_locale or 'zh' if req.language=='zh' else req.language),
                          mode=settings.mode,synthetic=True,operationalMutation=False,
                          latencyMs=round((time.monotonic()-started)*1000),
                          contextTurns=len(session['history'])//2,contextWindowTurns=10,
                          actionExecution='proposal_only',
                          sourceDetails=[c for c in chunks if any(s.documentId==c['documentId'] and s.sectionId==c['sectionId'] for s in answer.sources)])
            store.finish(user,req,session,fingerprint,result,answer.sources[0].documentId if answer.sources else session['topic'])
            log.info('feedback request=%s mode=%s sources=%s latency_ms=%s',req.requestId,settings.mode,','.join(s.documentId for s in answer.sources),result['latencyMs'])
            return result
        finally:
            store.release(session['id'],user['id'],req.requestId)

    @app.get('/')
    def index():
        return FileResponse(ROOT/'static'/'index.html')

    app.mount('/static',StaticFiles(directory=ROOT/'static'),name='static')
    return app
