import json,sys
def main(p):
 d=json.load(open(p,encoding='utf-8-sig')); assert d.get('status')=='passed' and d.get('parserId')=='structured.batch.v1'; seen=set()
 for x in d.get('items',[]):
  assert x.get('path') and x['path'] not in seen; seen.add(x['path']); assert x.get('status') in ('passed','failed')
  if x['status']=='passed': assert x.get('parserId','').endswith('.batch.v1') and len(x.get('entries',[]))<=200
  else: assert x.get('error')
 print(json.dumps({'valid':True,'count':len(seen)}))
if __name__=='__main__':
 try: main(sys.argv[1])
 except Exception as e: print(json.dumps({'valid':False,'error':str(e)})); sys.exit(1)
