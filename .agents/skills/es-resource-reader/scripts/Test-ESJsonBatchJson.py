import json, sys
def main(path):
    p=json.load(open(path,encoding='utf-8-sig'))
    if p.get('status')!='passed' or p.get('parserId')!='json.batch.v1': raise ValueError('invalid JSON batch envelope')
    items=p.get('items',[]); seen=set()
    if not isinstance(items,list) or not items: raise ValueError('items must be non-empty')
    for x in items:
        if x.get('status') not in ('passed','failed') or not x.get('path'): raise ValueError('invalid item identity')
        if x['path'] in seen: raise ValueError('duplicate path: '+x['path'])
        seen.add(x['path'])
        if x['status']=='failed':
            if not x.get('error'): raise ValueError('failed item missing error')
        elif x.get('parserId') not in ('json.batch.v1','jsonl.batch.v1') or len(x.get('entries',[]))>200: raise ValueError('invalid successful item')
    print(json.dumps({'validator':'Test-ESJsonBatchJson','valid':True,'count':len(items)}))
if __name__=='__main__':
    try: main(sys.argv[1])
    except Exception as e: print(json.dumps({'validator':'Test-ESJsonBatchJson','valid':False,'error':str(e)}));sys.exit(1)
