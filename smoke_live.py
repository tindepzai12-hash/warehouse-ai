"""Manual opt-in: three paid requests using server .env; never print secrets."""
from pathlib import Path
import argparse
from uuid import uuid4
from fastapi.testclient import TestClient
from app.config import Settings
from app.main import create_app

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--skip-greeting',action='store_true',help='Run only the question and follow-up (two paid calls).')
    parser.add_argument('--single',action='store_true',help='Run only the initial question (one paid call).')
    args=parser.parse_args()
    settings=Settings.from_env()
    if settings.mode!='openai':
        raise SystemExit('Set AI_MODE=openai in .env first')
    client=TestClient(create_app(settings))
    token=Path('.local-token').read_text(encoding='utf-8').strip()
    session=None
    questions=['Đơn cần năm hộp kem mà trên kệ còn ba thôi, giờ em làm sao?',
                     'Nếu không tìm được thêm hai hộp còn lại thì sao?',
                     'Chào bạn, bạn giúp được những gì?']
    selected=questions[:1] if args.single else (questions[:2] if args.skip_greeting else questions)
    for question in selected:
        response=client.post('/api/feedback',headers={'Authorization':'Bearer '+token},json={
            'requestId':str(uuid4()),'sessionId':session,'language':'vi','question':question})
        data=response.json()
        print('HTTP',response.status_code)
        if response.status_code!=200:
            print(data)
            raise SystemExit(1)
        session=data['sessionId']
        print(data['message'].encode('ascii','backslashreplace').decode())
        print({'sources':data['sources'],'needsClarification':data['needsClarification'],
               'contextTurns':data['contextTurns'],'mode':data['mode']})

if __name__=='__main__':
    main()
