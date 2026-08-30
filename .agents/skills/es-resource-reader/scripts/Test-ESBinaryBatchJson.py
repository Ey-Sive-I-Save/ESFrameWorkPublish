import json,sys
try:
 d=json.load(open(sys.argv[1],encoding='utf-8-sig')); assert d['status']=='passed' and d['parserId']=='binary.batch.v1' and d['items']; paths=[x['path'] for x in d['items']]; assert len(paths)==len(set(paths)); print(json.dumps({'valid':True,'count':len(paths)}))
except Exception as e: print(json.dumps({'valid':False,'error':str(e)})); sys.exit(1)
