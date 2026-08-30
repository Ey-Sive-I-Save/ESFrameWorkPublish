import json, sys

def main(path):
    rows = json.load(open(path, encoding='utf-8-sig'))
    assert isinstance(rows, list) and rows
    assert all(x.get('status') == 'passed' and x.get('path') for x in rows)
    print(json.dumps({'valid': True, 'count': len(rows), 'parserId': 'delimited.batch.v1'}))

if __name__ == '__main__':
    try: main(sys.argv[1])
    except Exception as exc:
        print(json.dumps({'valid': False, 'error': str(exc)})); sys.exit(1)
