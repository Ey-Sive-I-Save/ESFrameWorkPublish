import json, os, struct, sys

def probe(path):
    ext=os.path.splitext(path)[1].lower().lstrip('.')
    with open(path,'rb') as f: data=f.read(1024*1024)
    s={'extension':ext,'sizeBytes':os.path.getsize(path),'signature':data[:16].hex()}
    if data.startswith(b'\x89PNG\r\n\x1a\n') and len(data)>=24:
        s.update(codec='png',width=struct.unpack('>I',data[16:20])[0],height=struct.unpack('>I',data[20:24])[0])
    elif data.startswith(b'\xff\xd8'): s['codec']='jpeg'
    elif ext=='wav' and data[:4]==b'RIFF' and data[8:12]==b'WAVE' and len(data)>=28:
        ch=struct.unpack('<H',data[22:24])[0]; rate=struct.unpack('<I',data[24:28])[0]; s.update(codec='wav',channels=ch,sampleRate=rate,durationSecondsSampled=round(max(0,len(data)-44)/(rate*ch*2),3))
    elif data.startswith(b'OggS'): s['codec']='ogg'
    elif data.startswith(b'ID3') or data[:2] in (b'\xff\xfb',b'\xff\xf3'): s['codec']='mp3'
    elif len(data)>=12 and data[4:8]==b'ftyp': s['codec']='mp4-family'; s['brand']=data[8:12].decode('ascii','replace')
    elif ext=='gltf' and data.lstrip().startswith(b'{'): s['codec']='gltf-json'
    elif ext=='obj':
        s['codec']='obj'; s['vertexCount']=sum(1 for x in data.splitlines() if x.startswith(b'v ')); s['faceCount']=sum(1 for x in data.splitlines() if x.startswith(b'f '))
    elif ext=='fbx': s['codec']='fbx'
    elif ext in ('ttf','otf','woff','woff2'): s['codec']='font'
    else: s['codec']='unknown'
    return s
if __name__=='__main__':
    try: print(json.dumps({'status':'passed','parserId':'media.probe.v1','summary':probe(sys.argv[1])},ensure_ascii=False))
    except Exception as e: print(json.dumps({'status':'failed','error':str(e)})); sys.exit(1)
