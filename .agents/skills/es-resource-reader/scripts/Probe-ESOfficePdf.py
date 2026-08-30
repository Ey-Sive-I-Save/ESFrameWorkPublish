import json, os, re, sys, zipfile

def main(path):
    ext=os.path.splitext(path)[1].lower()
    if ext=='.pdf':
        with open(path,'rb') as f: data=f.read(8*1024*1024)
        if not data.startswith(b'%PDF-'): raise ValueError('invalid PDF signature')
        pages=len(re.findall(rb'/Type\s*/Page(?!s)',data))
        text=re.findall(rb'\(([^()]{2,120})\)',data)
        sample=' '.join(x.decode('latin1','replace') for x in text)[:4096]
        info={'container':'pdf','pageObjectCount':pages,'version':data[5:8].decode('ascii','replace'),'textSample':sample}
    else:
        with zipfile.ZipFile(path,'r') as z:
            names=sorted(z.namelist())
            if '[Content_Types].xml' not in names: raise ValueError('invalid XLSX container')
            sheets=[n for n in names if n.startswith('xl/worksheets/') and n.endswith('.xml')]
            sheet_names=[]
            if 'xl/workbook.xml' in names:
                xml=z.read('xl/workbook.xml')[:2*1024*1024].decode('utf-8','replace')
                sheet_names=re.findall(r'<sheet[^>]+name="([^"]+)"',xml)[:64]
            info={'container':'xlsx','entryCount':len(names),'worksheetCount':len(sheets),'worksheets':sheets[:64],'worksheetNames':sheet_names}
    print(json.dumps({'status':'passed','parserId':('pdf' if ext=='.pdf' else 'xlsx')+'.probe.v1','summary':info},ensure_ascii=True))
if __name__=='__main__':
    try: main(sys.argv[1])
    except Exception as e: print(json.dumps({'status':'failed','error':str(e)})); sys.exit(1)
