import json
import re
import unicodedata
from .config import ROOT

def normalize(text):
    return ''.join(c for c in unicodedata.normalize('NFD',text.lower().replace('đ','d')) if unicodedata.category(c)!='Mn')

def reported_shortage(question):
    """Recognize a worker's quantity comparison; never treat it as WMS facts."""
    q = normalize(question)
    match = re.search(r'\b(?:can\s+|yeu cau\s+)?(\d+)\s+hop\b[^.!?]*?\bchi\s+co\s+(\d+)\b', q)
    if match and int(match[1]) > int(match[2]):
        return int(match[1]), int(match[2])
    return None

class Knowledge:
    def __init__(self):
        self.chunks = json.loads((ROOT/'knowledge'/'reviewed.json').read_text(encoding='utf-8'))
        assert all(c['source_status']=='spec_confirmed_project_policy' and c['runtime_approved'] for c in self.chunks)

    def for_model(self,question,previous_topic,language):
        # This small reviewed corpus fits in context. Lexical matches rank it,
        # never gate model access. Include other SOPs for paraphrases/new topics.
        ranked=self.retrieve(question,previous_topic)
        ordered=ranked+[c for c in self.chunks if c not in ranked]
        return [{k:v for k,v in c.items() if k!='keywords' and k!='text'} |
                {'text':c['text'][language]} for c in ordered]

    def retrieve(self,question,previous_topic=''):
        q = normalize(question)
        if reported_shortage(question):
            q += ' short pick'
        scored=[]
        for chunk in self.chunks:
            score=0
            for keyword in chunk['keywords']:
                key=normalize(keyword)
                match = key in q if re.search(r'[\u3400-\u9fff]',key) else re.search(r'(?<!\w)'+re.escape(key)+r'(?!\w)',q)
                if match:
                    score+=len(key)+3
            if score:
                scored.append((score,chunk))
        if scored:
            return [c for _,c in sorted(scored,key=lambda x:x[0],reverse=True)[:3]]
        followup = any(phrase in q for phrase in ['why','what next','then','that','it?','tai sao','tiep theo','tom tat','vua roi','为什么','然后','总结','刚才'])
        if followup and previous_topic:
            return [c for c in self.chunks if c['documentId']==previous_topic]
        return []
