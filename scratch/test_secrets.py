import os, re

pattern = re.compile(r'(Password=[^;"\'\s]{4,}|User Id=[^;"\'\s]{4,}|AccountKey=[^;"\'\s]{4,}|api_key[ =\'"]+[A-Za-z0-9_\-]{16,})', re.IGNORECASE)

matches = []
for root, dirs, files in os.walk('.'):
    dirs[:] = [d for d in dirs if d not in ('bin', 'obj', '.git', '.github', 'scratch')]
    for file in files:
        filepath = os.path.join(root, file)
        try:
            with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
                for line_no, line in enumerate(f, 1):
                    m = pattern.search(line)
                    if m:
                        matches.append((filepath, line_no, line.strip(), m.group(0)))
        except Exception as e:
            pass

print(f"Total matches: {len(matches)}")
for m in matches:
    print(m)
