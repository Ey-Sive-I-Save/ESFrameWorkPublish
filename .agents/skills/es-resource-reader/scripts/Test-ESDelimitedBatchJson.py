import json, sys

def main(path):
    payload = json.load(open(path, encoding='utf-8-sig'))
    if payload.get('status') != 'passed' or payload.get('parserId') != 'delimited.batch.v1':
        raise ValueError('invalid batch envelope')
    rows = payload.get('items', [])
    if not isinstance(rows, list) or not rows:
        raise ValueError('batch output must be a non-empty array')
    seen = set()
    for row in rows:
        if row.get('status') not in ('passed','failed') or not row.get('path'):
            raise ValueError('every item must be passed or failed with a path')
        if row['path'] in seen:
            raise ValueError('duplicate path: ' + row['path'])
        seen.add(row['path'])
        if row.get('status') == 'failed':
            if not row.get('error'): raise ValueError('failed item missing error')
            continue
        if row.get('parserId') not in ('csv.rfc4180.batch.v1','tsv.rfc4180.batch.v1'):
            raise ValueError('invalid parserId')
        summary = row.get('summary', {})
        if summary.get('rowCount', -1) < 0 or summary.get('columnCount', -1) < 0:
            raise ValueError('invalid summary counts')
        if len(row.get('entries', [])) > 200:
            raise ValueError('entries exceed bounded projection')
    print(json.dumps({'validator':'Test-ESDelimitedBatchJson','valid':True,'count':len(rows)}))

if __name__ == '__main__':
    try: main(sys.argv[1])
    except Exception as exc:
        print(json.dumps({'validator':'Test-ESDelimitedBatchJson','valid':False,'error':str(exc)})); sys.exit(1)
