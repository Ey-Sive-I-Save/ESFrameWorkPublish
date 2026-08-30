import json, os, sys, time, zipfile
MAGIC={'.png':b'\x89PNG','.jpg':b'\xff\xd8','.jpeg':b'\xff\xd8','.gif':b'GIF8','.pdf':b'%PDF','.zip':b'PK\x03\x04','.xlsx':b'PK\x03\x04','.ogg':b'OggS','.wav':b'RIFF'}
def is_mp3(head):
 if head.startswith(b'ID3'): return True
 # MPEG audio frame sync (11 set bits), allowing common layer/bitrate variants.
 return len(head) >= 2 and head[0] == 0xFF and (head[1] & 0xE0) == 0xE0
def parse(x):
 p=x['path']; ext=os.path.splitext(p)[1].lower(); head=open(p,'rb').read(16); ok=(is_mp3(head) if ext == '.mp3' else (ext not in MAGIC or head.startswith(MAGIC[ext]))); summary={'byteLength':os.path.getsize(p),'magicMatched':ok,'extension':ext}; entries=[]
 if ext in ('.xlsx',) and ok:
  with zipfile.ZipFile(p) as z: entries=z.namelist()[:200]; summary['memberCount']=len(z.namelist())
 return {'status':'passed' if ok else 'failed','path':p,'parserId':'binary.batch.v1','summary':summary,'entries':entries} if ok else {'status':'failed','path':p,'error':'magic signature mismatch'}
def main(m):
 a=json.load(open(m,encoding='utf-8-sig')); a=a if isinstance(a,list) else [a]; t=time.perf_counter(); out=[]
 for x in a:
  try: out.append(parse(x))
  except Exception as e: out.append({'status':'failed','path':x.get('path',''),'error':str(e)})
 print(json.dumps({'status':'passed','parserId':'binary.batch.v1','elapsedMilliseconds':round((time.perf_counter()-t)*1000,3),'items':out},ensure_ascii=False))
if __name__=='__main__': main(sys.argv[1])
