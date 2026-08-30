import csv, json, sys, time

def parse(path, kind, limit=200):
    delimiter = '\t' if kind == 'tsv' else ','
    rows = []
    count = 0
    with open(path, 'r', encoding='utf-8-sig', newline='') as handle:
        reader = csv.reader(handle, delimiter=delimiter)
        headers = next(reader, [])
        for row in reader:
            count += 1
            if len(rows) < limit:
                rows.append(row)
    return {'status':'passed','path':path,'parserId':('tsv' if kind == 'tsv' else 'csv')+'.rfc4180.batch.v1',
            'summary':{'rowCount':count,'columnCount':len(headers),'headers':headers},'entries':rows}

def main():
    started = time.perf_counter()
    manifest = json.load(open(sys.argv[1], encoding='utf-8-sig'))
    out = []
    for item in manifest:
        try:
            out.append(parse(item['path'], item['format']))
        except Exception as exc:
            out.append({'status':'failed','path':item.get('path',''),'error':str(exc)})
    # Emit ASCII JSON so Windows PowerShell 5/native stdout code pages cannot
    # corrupt non-ASCII cell values before ConvertFrom-Json decodes them.
    print(json.dumps({'status':'passed','parserId':'delimited.batch.v1','elapsedMilliseconds':round((time.perf_counter()-started)*1000, 3),'items':out}, ensure_ascii=True))

if __name__ == '__main__':
    try: main()
    except Exception as exc:
        print(json.dumps({'status':'failed','error':str(exc)})); sys.exit(1)
