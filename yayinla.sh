#!/bin/bash
# Yeni sürüm yayınla: etiketi atar, GitHub Actions gerçek bir Windows
# makinesinde derler, testleri koşar ve .exe'yi Releases sayfasına koyar.
#
#   ./yayinla.sh 1.2.0
set -euo pipefail
cd "$(dirname "$0")"

SURUM="${1:-}"
if [ -z "$SURUM" ]; then
    echo "Kullanım: ./yayinla.sh <sürüm>     örnek: ./yayinla.sh 1.2.0"
    exit 1
fi
ETIKET="v$SURUM"

# DAL KAPISI: yalnız main yayınlanır. Çalışma dalından (windows-evrim gibi)
# etiket atılırsa CI o etiketi derler ve yarım işi Releases'a koyar; "git push
# origin main" ise yanlış daldaki commit'i göndermez ama etiket gider. Kapı
# en başta: hiçbir şey değişmeden durur.
DAL="$(git branch --show-current)"
if [ "$DAL" != "main" ]; then
    echo "✗ DURDURULDU: yayın yalnız 'main' dalından yapılır; şu an '$DAL' dalındasın."
    echo "  Önce dalı main'e birleştir (CI yeşilse), sonra tekrar dene."
    exit 1
fi

# Bulguyu ekrana basarken anahtarın kendisini MASKELE: kapının çıktısı
# terminal geçmişine ve ekran paylaşımına düşer; "xai-abcdef…(GIZLENDI)"
# sızıntının yerini gösterir, değerini göstermez.
maskele() {
    sed -E 's/(xai-.{6})[A-Za-z0-9]*/\1…(GIZLENDI)/g'
}

echo "▸ Gizli tarama (anahtar sızıntısı var mı?)"
CALISMA_AGACI="$(git grep -I -n -E 'xai-[A-Za-z0-9]{20,}' -- . 2>/dev/null || true)"
if [ -n "$CALISMA_AGACI" ]; then
    echo "✗ DURDURULDU: depoda API anahtarı görünüyor. Yayınlanmadı."
    printf '%s\n' "$CALISMA_AGACI" | maskele | head
    exit 1
fi
# BORU YOK. Eski kalıp `git log --all -p | grep -q` idi: grep ilk eşleşmede
# çıkar, üretici SIGPIPE (141) alır ve `set -o pipefail` altında `if` bunu
# "eşleşme yok" sayar — kapı sır VARKEN "temiz" diyordu (Mac KARARLAR #1).
# Artık çıkış KODUNA değil, ÇIKTININ VARLIĞINA bakılıyor: sonuç değişkene
# alınır, `|| true` yalnız "hiç eşleşme yok" (grep 1) durumunu yutar.
GECMIS="$(git rev-list --all 2>/dev/null \
    | xargs git grep -I -l -E 'xai-[A-Za-z0-9]{20,}' 2>/dev/null || true)"
if [ -n "$GECMIS" ]; then
    echo "✗ DURDURULDU: git GEÇMİŞİNDE API anahtarı var. Yayınlanmadı."
    echo "  (commit:dosya — anahtarın kendisi basılmaz)"
    printf '%s\n' "$GECMIS" | head
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
echo "   makinesinde derliyor, Windows testlerini ve saf test koşucusunu"
echo "   koşuyor (~2 dakika)."
echo
echo "   Durumu izle:  gh run watch"
echo "   Hazır olunca: https://github.com/$KULLANICI/$DEPO/releases/latest"
echo
echo "   Arkadaşlarına gönderilecek bağlantı bu — EkranCeviri.exe'yi"
echo "   indirip çift tıklayacaklar, kurulum yok."
