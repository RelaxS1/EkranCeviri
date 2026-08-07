#!/bin/bash
# GitHub'da herkese açık yayınla. Tek seferlik; sonraki güncellemeler için
# sadece: git add -A && git commit -m "..." && git push
set -euo pipefail
cd "$(dirname "$0")"

DEPO="${1:-EkranCeviri}"

echo "▸ Gizli tarama (anahtar sızıntısı var mı?)"
if git grep -I -n -E 'xai-[A-Za-z0-9]{20,}' -- . >/dev/null 2>&1; then
    echo "✗ DURDURULDU: depoda API anahtarı görünüyor. Yayınlanmadı."
    git grep -I -n -E 'xai-[A-Za-z0-9]{20,}' -- . | head
    exit 1
fi
if git log --all -p 2>/dev/null | grep -q -E 'xai-[A-Za-z0-9]{20,}'; then
    echo "✗ DURDURULDU: git GEÇMİŞİNDE API anahtarı var. Yayınlanmadı."
    exit 1
fi
if [ -n "$(python3 -c "
import json,sys
try: print(json.load(open('config.json')).get('grok_api_key',''))
except Exception: print('')
")" ]; then
    echo "✗ DURDURULDU: config.json içinde anahtar dolu. Boşalt, sonra tekrar dene."
    exit 1
fi
echo "  temiz ✓"

echo "▸ GitHub girişi"
if ! gh auth status >/dev/null 2>&1; then
    echo "  Giriş yapılmamış. Tarayıcı açılacak, GitHub hesabınla giriş yap."
    echo "  (Sorulara: GitHub.com → HTTPS → Y → Login with a web browser)"
    gh auth login
fi
KULLANICI=$(gh api user --jq .login)
echo "  giriş: $KULLANICI ✓"

echo "▸ README'deki bağlantılar düzeltiliyor"
sed -i '' "s|github.com/KULLANICI/EkranCeviri|github.com/$KULLANICI/$DEPO|g" \
    README.md CONTRIBUTING.md
if ! git diff --quiet; then
    git add README.md CONTRIBUTING.md
    git commit -q -m "README: depo bağlantısı güncellendi"
fi

echo "▸ Depo oluşturuluyor ve gönderiliyor"
if gh repo view "$KULLANICI/$DEPO" >/dev/null 2>&1; then
    echo "  depo zaten var, sadece gönderiliyor"
    git remote get-url origin >/dev/null 2>&1 \
        || git remote add origin "https://github.com/$KULLANICI/$DEPO.git"
    git push -u origin HEAD
else
    gh repo create "$DEPO" --public --source=. --remote=origin --push \
        --description "Ekranda seçtiğin sohbeti balonların üstüne Türkçe yazan macOS uygulaması — Almanca lehçeleri (İsviçre, Bavyera, Avusturya) dahil"
fi

echo "▸ Windows .exe'si için sürüm etiketi atılıyor"
# Etiket atılınca GitHub Actions gerçek bir Windows makinesinde derliyor,
# testleri koşuyor ve .exe'yi Releases sayfasına koyuyor.
if git rev-parse v1.0.0 >/dev/null 2>&1; then
    echo "  v1.0.0 etiketi zaten var, atlanıyor"
else
    git tag -a v1.0.0 -m "İlk yayın: macOS + Windows"
    git push origin v1.0.0
fi

echo
echo "✅ YAYINDA — arkadaşlarına bu bağlantıyı gönder:"
echo "   https://github.com/$KULLANICI/$DEPO"
echo
echo "Windows kullanan arkadaşların için (.exe birkaç dakikada hazır olur):"
echo "   https://github.com/$KULLANICI/$DEPO/releases/latest"
echo "   → EkranCeviri.exe indir, çift tıkla. Kurulum yok."
