#!/bin/zsh
# Tek seferlik kurulum: sanal ortam + bağımlılıklar.
DIR="${0:A:h}"
cd "$DIR"
if [ ! -d venv ]; then
  /usr/bin/python3 -m venv venv
fi
venv/bin/pip install --upgrade pip
venv/bin/pip install -r requirements.txt
echo "Kurulum tamam. Çalıştırmak için: ./calistir.sh"
