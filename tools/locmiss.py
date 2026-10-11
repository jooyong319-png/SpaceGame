import re, glob, os
os.chdir(r'C:/Make_Game/SalvageRun/game/Assets/_Project')
BS = chr(92)
keys = set()
for line in open('Resources/loc_en.txt', encoding='utf8'):
    line = line.rstrip('\n').rstrip('\r')
    if not line or line.startswith('#') or '\t' not in line:
        continue
    k = line.split('\t', 1)[0].replace(BS + 'n', '\n').replace(BS + 't', '\t')
    keys.add(k)
miss = []
pat = re.compile('Loc' + BS + '.T' + BS + '("((?:[^"' + BS + BS + ']|' + BS + BS + '.)*)"' + BS + ')')
for f in glob.glob('Scripts/Orbit/**/*.cs', recursive=True):
    s = open(f, encoding='utf8').read()
    for m in pat.finditer(s):
        k = m.group(1).replace(BS + 'n', '\n').replace(BS + '"', '"').replace(BS + BS, BS)
        if re.search('[가-힣]', k) and k not in keys:
            miss.append((os.path.basename(f), s[:m.start()].count('\n') + 1, k))
for x in miss:
    print(x)
print(len(miss))
