import hashlib
import json
import re
import secrets
import sqlite3
import time
from pathlib import Path
from contextlib import contextmanager
from uuid import uuid4
from fastapi import HTTPException

class Store:
    def __init__(self,path):
        self.path = path
        Path(path).parent.mkdir(parents=True,exist_ok=True)
        with self.connect() as db:
            db.executescript('''
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS users(id TEXT PRIMARY KEY,token_hash TEXT UNIQUE,role TEXT,tasks TEXT);
            CREATE TABLE IF NOT EXISTS sessions(id TEXT PRIMARY KEY,user_id TEXT,task TEXT,history TEXT,topic TEXT,busy_until REAL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS replies(user_id TEXT,request_id TEXT,fingerprint TEXT,response TEXT,PRIMARY KEY(user_id,request_id));
            CREATE TABLE IF NOT EXISTS usage(user_id TEXT,at REAL);
            CREATE TABLE IF NOT EXISTS inflight(user_id TEXT,request_id TEXT,until REAL,PRIMARY KEY(user_id,request_id));
            CREATE INDEX IF NOT EXISTS usage_user_time ON usage(user_id,at);
            ''')

    @contextmanager
    def connect(self):
        connection=sqlite3.connect(self.path,timeout=10)
        try:
            with connection:
                yield connection
        finally:
            connection.close()

    def provision(self,user,role='staff'):
        token = secrets.token_urlsafe(32)
        tasks = ['TEST-PICK-001','TEST-PUT-001','TEST-COUNT-001']
        with self.connect() as db:
            db.execute('INSERT INTO users VALUES(?,?,?,?) ON CONFLICT(id) DO UPDATE SET token_hash=excluded.token_hash,role=excluded.role,tasks=excluded.tasks',
                       (user,hashlib.sha256(token.encode()).hexdigest(),role,json.dumps(tasks)))
        return token

    def authenticate(self,token):
        with self.connect() as db:
            row = db.execute('SELECT id,role,tasks FROM users WHERE token_hash=?',(hashlib.sha256(token.encode()).hexdigest(),)).fetchone()
        if not row:
            raise HTTPException(401,'Invalid prototype user token')
        return {'id':row[0],'role':row[1],'tasks':json.loads(row[2])}

    def bootstrap_demo(self,token_hash):
        """Restore low-privilege demo login on ephemeral hosts; no raw token stored."""
        if not re.fullmatch(r'[0-9a-f]{64}',token_hash):
            raise ValueError('PROTOTYPE_TOKEN_SHA256 must be a lowercase SHA-256 hex digest')
        with self.connect() as db:
            db.execute('INSERT INTO users VALUES(?,?,?,?) ON CONFLICT(id) DO UPDATE SET token_hash=excluded.token_hash,role=excluded.role,tasks=excluded.tasks',
                       ('render-demo',token_hash,'staff',json.dumps(['TEST-PICK-001','TEST-PUT-001','TEST-COUNT-001'])))

    def begin(self,user,req,settings):
        fingerprint = hashlib.sha256(req.model_dump_json().encode()).hexdigest()
        now = time.time()
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            prior = db.execute('SELECT fingerprint,response FROM replies WHERE user_id=? AND request_id=?',(user['id'],str(req.requestId))).fetchone()
            if prior:
                if prior[0]!=fingerprint:
                    raise HTTPException(409,'requestId already used for another payload')
                return None,json.loads(prior[1]),fingerprint
            if req.taskId not in user['tasks']:
                raise HTTPException(403,'Task is not assigned to this prototype user')
            sid = str(req.sessionId) if req.sessionId else str(uuid4())
            row = db.execute('SELECT user_id,task,history,topic,busy_until FROM sessions WHERE id=?',(sid,)).fetchone()
            if req.sessionId and (not row or row[0]!=user['id']):
                raise HTTPException(404,'Session not found')
            if row and row[1]!=req.taskId:
                raise HTTPException(409,'Start a new session when changing task')
            if row and row[4]>now:
                raise HTTPException(409,'Session is processing; retry later')
            db.execute('DELETE FROM inflight WHERE until<?',(now,))
            pending=db.execute('SELECT 1 FROM inflight WHERE user_id=? AND request_id=?',(user['id'],str(req.requestId))).fetchone()
            if pending:
                raise HTTPException(409,'Request is processing; retry the same request later')
            db.execute('DELETE FROM usage WHERE at<?',(now-86400,))
            minute,day = db.execute('SELECT COALESCE(SUM(at>?),0),COUNT(*) FROM usage WHERE user_id=?',(now-60,user['id'])).fetchone()
            if minute>=settings.rpm or day>=settings.daily:
                raise HTTPException(429,'Prototype request budget reached; retry later')
            db.execute('INSERT INTO usage VALUES(?,?)',(user['id'],now))
            db.execute('INSERT INTO inflight VALUES(?,?,?)',(user['id'],str(req.requestId),now+90))
            if not row:
                db.execute('INSERT INTO sessions VALUES(?,?,?,?,?,?)',(sid,user['id'],req.taskId,'[]','',now+90))
            else:
                db.execute('UPDATE sessions SET busy_until=? WHERE id=?',(now+90,sid))
        return {'id':sid,'history':json.loads(row[2]) if row else [],'topic':row[3] if row else ''},None,fingerprint

    def finish(self,user,req,session,fingerprint,response,topic):
        report={'question':req.question,'eventType':req.eventType,
                'occurredAtUtc':req.occurredAtUtc.isoformat() if req.occurredAtUtc else None,
                'observation':req.observation.model_dump(exclude_none=True),
                'evidenceStatus':'worker_report_not_verified_transaction'}
        history = session['history'] + [{'role':'user','content':json.dumps(report,ensure_ascii=False)},
                                        {'role':'assistant','content':response['message']}]
        with self.connect() as db:
            db.execute('UPDATE sessions SET history=?,topic=?,busy_until=0 WHERE id=?',(json.dumps(history[-20:],ensure_ascii=False),topic,session['id']))
            db.execute('INSERT INTO replies VALUES(?,?,?,?)',(user['id'],str(req.requestId),fingerprint,json.dumps(response,ensure_ascii=False)))

    def release(self,sid,user,request_id):
        with self.connect() as db:
            db.execute('UPDATE sessions SET busy_until=0 WHERE id=?',(sid,))
            db.execute('DELETE FROM inflight WHERE user_id=? AND request_id=?',(user,str(request_id)))
