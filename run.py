import os
import uvicorn
from app.config import Settings

if __name__=='__main__':
    Settings.from_env()
    uvicorn.run('app.main:create_app',factory=True,host='0.0.0.0',port=int(os.getenv('PORT','8000')),workers=1)
