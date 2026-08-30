import configparser, json, os, sqlite3, sys, tarfile, time, zipfile

def parse(item):
    path, fmt = item['path'], item.get('format','')
    if fmt == 'sqlite':
        con=sqlite3.connect('file:'+os.path.abspath(path)+'?mode=ro', uri=True); names=[r[0] for r in con.execute("select name from sqlite_master where type='table' order by name")]; con.close()
        return {'status':'passed','path':path,'parserId':'sqlite.batch.v1','summary':{'tableCount':len(names)},'entries':names[:200]}
    if fmt in ('zip','unitypackage'):
        with zipfile.ZipFile(path) as z: names=z.namelist()
        return {'status':'passed','path':path,'parserId':'zip.batch.v1','summary':{'memberCount':len(names)},'entries':names[:200]}
    if fmt in ('tar','gz','archive'):
        with tarfile.open(path,'r:*') as t: names=[m.name for m in t.getmembers()]
        return {'status':'passed','path':path,'parserId':'archive.batch.v1','summary':{'memberCount':len(names)},'entries':names[:200]}
    with open(path,encoding='utf-8-sig') as f: lines=[x for x in f if x.strip() and not x.lstrip().startswith(('#',';'))]
    parser=fmt+'.batch.v1'; return {'status':'passed','path':path,'parserId':parser,'summary':{'nonCommentLineCount':len(lines)},'entries':[]}

def main(manifest):
    start=time.perf_counter(); loaded=json.load(open(manifest,encoding='utf-8-sig')); loaded=loaded if isinstance(loaded,list) else [loaded]; items=[]
    for x in loaded:
        try: items.append(parse(x))
        except Exception as e: items.append({'status':'failed','path':x.get('path',''),'error':str(e)})
    print(json.dumps({'status':'passed','parserId':'structured.batch.v1','elapsedMilliseconds':round((time.perf_counter()-start)*1000,3),'items':items},ensure_ascii=False))
if __name__=='__main__':
    try: main(sys.argv[1])
    except Exception as e: print(json.dumps({'status':'failed','error':str(e)})); sys.exit(1)
