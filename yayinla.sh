#!/bin/bash
# Yeni sürüm yayınla: etiketi atar, GitHub Actions gerçek bir Windows
# makinesinde derler, testleri koşar ve .exe'yi Releases sayfasına koyar.
#
#   ./yayinla.sh 1.1.0
set -euo pipefail
cd "$(dirname "$0")"

SURUM="${1:-}"
if [ -z "$SURUM" ]; then
    echo "Kullanım: ./yayinla.sh <sürüm>     örnek: ./yayinla.sh 1.1.0"
    exit 1
fi
ETIKET="v$SURUM"

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
echo "  temiz ✓"

echo "▸ Çalışma ağacı denetimi"
if [ -n "$(git status --porcelain)" ]; then
    echo "✗ Commit edilmemiş değişiklik var. Önce commit et."
    git status --short
    exit 1
fi
echo "  temiz ✓"

if git rev-parse "$ETIKET" >/dev/null 2>&1; then
    echo "✗ $ETIKET etiketi zaten var. Farklı bir sürüm numarası ver."
    exit 1
fi

echo "▸ Sürüm numarası csproj'a yazılıyor"
sed -i '' "s|<Version>.*</Version>|<Version>$SURUM.0</Version>|" EkranCeviri.csproj
if ! git diff --quiet; then
    git add EkranCeviri.csproj
    git commit -q -m "Sürüm $SURUM"
fi

echo "▸ Etiket atılıyor ve gönderiliyor"
git tag -a "$ETIKET" -m "Sürüm $SURUM"
git push origin main
git push origin "$ETIKET"

KULLANICI=$(gh api user --jq .login 2>/dev/null || echo "KULLANICI")
DEPO=$(basename "$(git rev-parse --show-toplevel)")

echo
echo "✅ Etiket gönderildi. GitHub Actions şimdi gerçek bir Windows"
echo "   makinesinde derliyor ve testleri koşuyor (~2 dakika)."
echo
echo "   Durumu izle:  gh run watch"
echo "   Hazır olunca: https://github.com/$KULLANICI/$DEPO/releases/latest"
echo
echo "   Arkadaşlarına gönderilecek bağlantı bu — EkranCeviri.exe'yi"
echo "   indirip çift tıklayacaklar, kurulum yok."
