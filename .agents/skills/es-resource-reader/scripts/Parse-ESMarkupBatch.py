import json, re, sys, time
def parse(item):
    path=item['path']; kind=item['format']; text=open(path,encoding='utf-8-sig').read()
    if kind == 'unityyaml':
        blocks=re.findall(r'^--- !u!(\d+) &(-?\d+)',text,re.M); guids=re.findall(r'guid:\s*([0-9a-fA-F]{32})',text)
        summary={'objectCount':len(blocks),'guidCount':len(guids),'uniqueGuidCount':len(set(g.lower() for g in guids))}
        parser='unity-yaml.batch.v1'
    elif kind in ('yaml','yml'):
        summary={'keyLineCount':len(re.findall(r'^\s*[A-Za-z0-9_.-]+\s*:',text,re.M))}; parser='yaml.batch.v1'
    elif kind in ('html','htm'):
        summary={'title':(re.search(r'(?is)<title[^>]*>(.*?)</title>',text) or ['',''])[1].strip(),'linkCount':len(re.findall(r'(?is)<a\b',text)),'scriptCount':len(re.findall(r'(?is)<script\b',text))}; parser='html.batch.v1'
    elif kind in ('md','markdown'):
        summary={'headingCount':len(re.findall(r'^\s{0,3}#{1,6}\s+',text,re.M)),'linkCount':len(re.findall(r'!?\[[^\]]*\]\([^)]*\)',text))}; parser='markdown.batch.v1'
    elif kind == 'xml':
        summary={'elementCount':len(re.findall(r'<[A-Za-z_][^!?/>\s]*\b',text)),'attributeCount':len(re.findall(r'\s[A-Za-z_:][\w:.-]*\s*=\s*["\']',text))}; parser='xml.batch.v1'
    else:
        raise ValueError('unsupported markup format: '+kind)
    return {'status':'passed','path':path,'parserId':parser,'summary':summary,'entries':[]}
def main(manifest):
    start=time.perf_counter(); items=[]
    loaded=json.load(open(manifest,encoding='utf-8-sig'))
    if isinstance(loaded, dict): loaded=[loaded]
    for item in loaded:
        try: items.append(parse(item))
        except Exception as exc: items.append({'status':'failed','path':item.get('path',''),'error':str(exc)})
    print(json.dumps({'status':'passed','parserId':'markup.batch.v1','elapsedMilliseconds':round((time.perf_counter()-start)*1000,3),'items':items},ensure_ascii=False))
if __name__=='__main__':
    try: main(sys.argv[1])
    except Exception as exc: print(json.dumps({'status':'failed','error':str(exc)}));sys.exit(1)
