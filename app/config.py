import os
from dataclasses import dataclass
from pathlib import Path
from dotenv import load_dotenv

ROOT = Path(__file__).resolve().parent.parent

@dataclass
class Settings:
    database: str = 'data/warehouse.sqlite3'
    mode: str = 'mock'
    api_key: str = ''
    model: str = ''
    rpm: int = 20
    daily: int = 100
    max_tokens: int = 1200
    timeout: float = 30
    chinese_locale: str = ''
    prototype_token_sha256: str = ''

    @classmethod
    def from_env(cls):
        load_dotenv(ROOT / '.env')
        value = cls(database=os.getenv('DATABASE_PATH','data/warehouse.sqlite3'),
                    mode=os.getenv('AI_MODE','mock'), api_key=os.getenv('OPENAI_API_KEY',''),
                    model=os.getenv('OPENAI_MODEL',''), rpm=int(os.getenv('MAX_REQUESTS_PER_MINUTE','20')),
                    daily=int(os.getenv('MAX_REQUESTS_PER_DAY','100')),
                    max_tokens=int(os.getenv('MAX_OUTPUT_TOKENS','1200')),
                    timeout=float(os.getenv('REQUEST_TIMEOUT_SECONDS','30')),
                    chinese_locale=os.getenv('CHINESE_LOCALE',''),
                    prototype_token_sha256=os.getenv('PROTOTYPE_TOKEN_SHA256','').strip())
        if value.mode not in ('mock','openai'):
            raise ValueError('AI_MODE must be mock or openai')
        if value.mode == 'openai' and (not value.api_key or not value.model):
            raise ValueError('Set OPENAI_API_KEY and OPENAI_MODEL in server environment')
        if min(value.rpm,value.daily,value.max_tokens,value.timeout) <= 0 or value.timeout > 60:
            raise ValueError('Positive limits required; timeout must be <=60 seconds')
        return value
