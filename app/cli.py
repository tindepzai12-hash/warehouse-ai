import argparse
from pathlib import Path
from .config import Settings
from .store import Store

def main():
    parser=argparse.ArgumentParser(description='Provision/rotate a low-privilege prototype user token; not an OpenAI key.')
    parser.add_argument('command',choices=['create-user'])
    parser.add_argument('--user',required=True)
    parser.add_argument('--role',choices=['staff','icqa','supervisor'],default='staff')
    parser.add_argument('--token-file',default='.local-token')
    args=parser.parse_args()
    token=Store(Settings.from_env().database).provision(args.user,args.role)
    Path(args.token_file).write_text(token,encoding='utf-8')
    print('Prototype token written to '+args.token_file+'. Keep it private. Existing token for this user is revoked.')

if __name__=='__main__':
    main()
