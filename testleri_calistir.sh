#!/bin/zsh
# Ağsız, belirlenimci temel testler (yayın öncesi zorunlu kontrol).
set -e
DIR="${0:A:h}"
cd "$DIR"
python3 - <<'PY'
k = open('EkranCeviri.swift').read()
i = k.find('// MARK: - Giriş')
open('/tmp/ec_kutuphane.swift', 'w').write(k[:i])
PY
swiftc -swift-version 5 /tmp/ec_kutuphane.swift Sinama.swift Testler/main.swift \
  -o /tmp/ekranceviri_testler
exec /tmp/ekranceviri_testler
