import csv, json, sys

def main(path, delimiter, limit=200):
    rows=[]; row_count=0
    with open(path, 'r', encoding='utf-8-sig', newline='') as f:
        reader=csv.reader(f, delimiter=delimiter)
        headers=next(reader, [])
        for row in reader:
            row_count += 1
            if len(rows) < limit: rows.append(row)
    print(json.dumps({'status':'passed','parserId':('tsv' if delimiter=='\t' else 'csv')+'.rfc4180.v1',
                      'summary':{'rowCount':row_count,'columnCount':len(headers),'headers':headers},
                      'entries':rows}, ensure_ascii=False))

if __name__=='__main__':
    try: main(sys.argv[1], '\t' if sys.argv[2]=='tsv' else ',')
    except Exception as e:
        print(json.dumps({'status':'failed','error':str(e)})); sys.exit(1)
