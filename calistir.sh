#!/bin/zsh
# Geliştirme başlatıcısı (venv'den çalıştırır).
# Argümansız: menü çubuğu modu. --sec: tek seferlik bölge seçimi.
DIR="${0:A:h}"
exec "$DIR/venv/bin/python3" "$DIR/ekran_ceviri.py" "$@"
