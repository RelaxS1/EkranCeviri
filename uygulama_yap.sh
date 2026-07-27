#!/bin/zsh
# EkranCeviri.app (yerli Swift sürümü) derler ve Uygulamalar'a kurar.
set -e
DIR="${0:A:h}"
cd "$DIR"

# İkon (yoksa üret — python venv gerektirir, ikon zaten üretilmişse dokunmaz)
if [ ! -f ikon.icns ]; then
  QT_QPA_PLATFORM=offscreen venv/bin/python3 ikon_yap.py
fi

APP="$DIR/EkranCeviri.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

echo "Testler koşuluyor..."
./testleri_calistir.sh

echo "Swift derleniyor..."
swiftc -swift-version 5 -O EkranCeviri.swift Sinama.swift main.swift -o "$APP/Contents/MacOS/EkranCeviri"

# GÜVENLİK: gerçek API anahtarı pakete asla gömülmez
if grep -q '"grok_api_key": *"xai-' config.json; then
  echo "HATA: config.json gerçek API anahtarı içeriyor — paketleme durduruldu."
  exit 1
fi

cp Info.plist "$APP/Contents/Info.plist"
cp ikon.icns "$APP/Contents/Resources/ikon.icns"
cp config.json "$APP/Contents/Resources/config.varsayilan.json"

# Sabit kimlikli imza: TCC (Ekran Kaydı) izni yeniden derlemede bozulmasın
codesign --force --sign - --identifier com.sami.ekranceviri \
  -r='designated => identifier "com.sami.ekranceviri"' "$APP"

# Çalışan eski kopyayı (python veya swift) kapat, kur
pkill -f "EkranCeviri.app/Contents" 2>/dev/null || true
sleep 0.5
if [ -w /Applications ]; then
  HEDEF="/Applications/EkranCeviri.app"
else
  mkdir -p "$HOME/Applications"
  HEDEF="$HOME/Applications/EkranCeviri.app"
fi
rm -rf "$HEDEF"
mv "$APP" "$HEDEF"
echo "Kuruldu: $HEDEF"
