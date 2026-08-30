import json, sys, time

def parse(path, fmt='json'):
    if fmt == 'jsonl':
        with open(path, encoding='utf-8-sig') as f:
            rows=[json.loads(line) for line in f if line.strip()]
        return {'status':'passed','path':path,'parserId':'jsonl.batch.v1','summary':{'rootType':'jsonl','count':len(rows),'validLineCount':len(rows)},'entries':rows[:200]}
    with open(path, encoding='utf-8-sig') as f:
        obj = json.load(f)
    if isinstance(obj, list):
        count = len(obj); root = 'list'; entries = obj[:200]
    elif isinstance(obj, dict):
        count = len(obj); root = 'object'; entries = list(obj.items())[:200]
    else:
        count = 1; root = type(obj).__name__; entries = [obj]
    return {'status':'passed','path':path,'parserId':'json.batch.v1','summary':{'rootType':root,'count':count},'entries':entries}

def main(manifest):
    started=time.perf_counter(); items=[]
    loaded=json.load(open(manifest, encoding='utf-8-sig'))
    if isinstance(loaded, dict): loaded=[loaded]
    for item in loaded:
        try: items.append(parse(item['path'], item.get('format','json')))
        except Exception as exc: items.append({'status':'failed','path':item.get('path',''),'error':str(exc)})
    print(json.dumps({'status':'passed','parserId':'json.batch.v1','elapsedMilliseconds':round((time.perf_counter()-started)*1000,3),'items':items}, ensure_ascii=False))

if __name__=='__main__':
    try: main(sys.argv[1])
    except Exception as exc: print(json.dumps({'status':'failed','error':str(exc)})); sys.exit(1)
