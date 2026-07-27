#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Ekran Çeviri — ekrandan bölge seç, yazıyı tanı (Apple Vision OCR), çevir,
orijinalin üstüne aynı konumda/renkte yaz (Google Lens tarzı overlay).

Motorlar:
  - hizli : Google'ın anahtarsız gtx uç noktası (ücretsiz, standart diller)
  - ai    : Grok API (İsviçre Almancası / Zürih lehçesi için şart)
  - auto  : Metinde Zürih lehçesi belirteci varsa ai, yoksa hizli

Kullanım:
  ekran_ceviri.py            bölge seç ve çevir (config'deki motor)
  ekran_ceviri.py --ai       bu seferlik Grok ile çevir
  ekran_ceviri.py --hizli    bu seferlik ücretsiz Google ile çevir
  ekran_ceviri.py --dil en   bu seferlik hedef dili değiştir
  ekran_ceviri.py --test     GUI olmadan OCR+çeviri öz testi
"""

import sys
import os
import json
import re
import subprocess
import tempfile
import threading
import time
import urllib.request
import urllib.parse

from PyQt6.QtCore import Qt, QObject, QRect, QPoint, QTimer, QThread, pyqtSignal
from PyQt6.QtGui import (QGuiApplication, QPainter, QColor, QPen, QFont,
                         QFontMetrics, QImage, QPixmap, QIcon, QAction,
                         QActionGroup, QCursor, QShortcut, QKeySequence)
from PyQt6.QtWidgets import (QApplication, QWidget, QPushButton, QHBoxLayout,
                             QLabel, QSystemTrayIcon, QMenu)
from PyQt6.QtNetwork import QLocalServer, QLocalSocket

import Quartz
import Vision
from Foundation import NSURL

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DESTEK_DIZINI = os.path.expanduser("~/Library/Application Support/EkranCeviri")
CONFIG_PATH = os.path.join(DESTEK_DIZINI, "config.json")
SOKET_ADI = "ekranceviri-tekil"  # tek kopya + "tekrar açınca çevir" sinyali

VARSAYILAN_AYARLAR = {
    "hedef_dil": "tr",
    "motor": "auto",          # auto | ai | hizli
    "grok_api_key": "",
    "grok_model": "grok-3",
    "ocr_dilleri": ["de-DE", "en-US", "tr-TR", "fr-FR", "it-IT"],
}

DIL_ADLARI = {
    "tr": "Türkçe", "en": "İngilizce", "de": "Almanca", "fr": "Fransızca",
    "it": "İtalyanca", "es": "İspanyolca", "ar": "Arapça", "ru": "Rusça",
}

# Zürih lehçesine özgü kelimeler (standart Almancada bu yazımlar yok)
ZURIH_BELIRTECLERI = {
    "isch", "nöd", "nid", "chli", "chlii", "gäll", "gell", "hoi", "grüezi",
    "grüazi", "gsi", "gsii", "chasch", "chunnsch", "chunnt", "chume", "chum",
    "öppis", "öpper", "hüt", "bisch", "hesch", "häsch", "machsch", "tuesch",
    "gahts", "gaht", "gats", "zäme", "bitzli", "würkli", "wüki", "lüt",
    "guet", "guät", "dänn", "wänn", "eus", "üs", "mir sind", "villicht",
    "vilicht", "nöchst", "znacht", "zmittag", "zmorge", "merci vilmal",
    "uf widerluege", "schätzli", "müesst", "müend", "wettsch", "gseh",
    "gsehsch", "verzell", "verzellsch", "es bitzeli", "sötti", "eifach so",
}


def ayarlari_kaydet(ayarlar):
    os.makedirs(DESTEK_DIZINI, exist_ok=True)
    with open(CONFIG_PATH, "w", encoding="utf-8") as f:
        json.dump(ayarlar, f, ensure_ascii=False, indent=2)


def ayarlari_yukle():
    """Ayarlar ~/Library/Application Support/EkranCeviri/config.json'da
    tutulur; ilk çalıştırmada uygulama paketindeki varsayılandan tohumlanır."""
    ayarlar = dict(VARSAYILAN_AYARLAR)
    kaynak = CONFIG_PATH if os.path.exists(CONFIG_PATH) else None
    if kaynak is None:
        for aday in ("config.varsayilan.json", "config.json"):
            yol = os.path.join(BASE_DIR, aday)
            if os.path.exists(yol):
                kaynak = yol
                break
    if kaynak:
        try:
            with open(kaynak, "r", encoding="utf-8") as f:
                ayarlar.update(json.load(f))
        except Exception as e:
            print(f"Ayar dosyası okunamadı ({kaynak}): {e}", file=sys.stderr)
    if not os.path.exists(CONFIG_PATH):
        try:
            ayarlari_kaydet(ayarlar)
        except Exception as e:
            print(f"Ayar dosyası yazılamadı: {e}", file=sys.stderr)
    return ayarlar


# ---------------------------------------------------------------- OCR (Vision)

def ocr_yap(png_yolu, ocr_dilleri):
    """Görseldeki yazı satırlarını Apple Vision ile tanır.
    Dönen liste: [{"text": str, "nx","ny","nw","nh": normalize bbox}, ...]
    Vision'da orijin sol-ALT köşedir."""
    url = NSURL.fileURLWithPath_(png_yolu)
    handler = Vision.VNImageRequestHandler.alloc().initWithURL_options_(url, None)
    istek = Vision.VNRecognizeTextRequest.alloc().init()
    istek.setRecognitionLevel_(Vision.VNRequestTextRecognitionLevelAccurate)
    istek.setUsesLanguageCorrection_(True)
    istek.setRecognitionLanguages_(ocr_dilleri)
    basarili, hata = handler.performRequests_error_([istek], None)
    if not basarili:
        raise RuntimeError(f"Vision OCR hatası: {hata}")
    satirlar = []
    for gozlem in (istek.results() or []):
        adaylar = gozlem.topCandidates_(1)
        if adaylar.count() == 0:
            continue
        metin = str(adaylar.objectAtIndex_(0).string()).strip()
        if not metin:
            continue
        bb = gozlem.boundingBox()
        satirlar.append({
            "text": metin,
            "nx": bb.origin.x, "ny": bb.origin.y,
            "nw": bb.size.width, "nh": bb.size.height,
        })
    return satirlar


# ------------------------------------------------------------- Blok gruplama

class Blok:
    """Aynı konuşma balonuna ait ardışık satırlar."""

    def __init__(self, satir):
        self.satirlar = [satir]
        self.rect = QRect(satir["rect"])
        self.ceviri = None

    def ekle(self, satir):
        self.satirlar.append(satir)
        self.rect = self.rect.united(satir["rect"])

    @property
    def metin(self):
        return " ".join(s["text"] for s in self.satirlar)

    @property
    def satir_yuksekligi(self):
        return sum(s["rect"].height() for s in self.satirlar) / len(self.satirlar)

    @property
    def cevrilebilir(self):
        # En az bir harf içermeyen bloklar (saat, sayı, emoji) çevrilmez
        return bool(re.search(r"[^\W\d_]", self.metin, re.UNICODE))

    @property
    def saga_yasli(self):
        if len(self.satirlar) < 2:
            return False
        sag_kenarlar = [s["rect"].right() for s in self.satirlar]
        sol_kenarlar = [s["rect"].x() for s in self.satirlar]
        return (max(sag_kenarlar) - min(sag_kenarlar)) * 2 < \
               (max(sol_kenarlar) - min(sol_kenarlar))


def bloklara_ayir(satirlar, genislik, yukseklik):
    """Normalize bbox'ları bölge-yerel piksel koordinatına çevirir ve
    dikey olarak yakın + yatay olarak çakışan satırları balonlara gruplar."""
    for s in satirlar:
        x = s["nx"] * genislik
        y = (1.0 - s["ny"] - s["nh"]) * yukseklik   # sol-alt -> sol-üst
        s["rect"] = QRect(round(x), round(y),
                          max(1, round(s["nw"] * genislik)),
                          max(1, round(s["nh"] * yukseklik)))
    satirlar.sort(key=lambda s: (s["rect"].y(), s["rect"].x()))

    bloklar = []
    for s in satirlar:
        eklendi = False
        for blok in bloklar[-3:]:
            son = blok.satirlar[-1]["rect"]
            r = s["rect"]
            dikey_bosluk = r.y() - son.bottom()
            yatay_cakisma = min(son.right(), r.right()) - max(son.x(), r.x())
            if -son.height() * 0.4 < dikey_bosluk < son.height() * 0.6 \
                    and yatay_cakisma > 0:
                blok.ekle(s)
                eklendi = True
                break
        if not eklendi:
            bloklar.append(Blok(s))
    return bloklar


# ------------------------------------------------------------------- Çeviri

def zurih_lehcesi_mi(metin):
    kelimeler = set(re.findall(r"[a-zäöüéàè]+", metin.lower()))
    eslesen = kelimeler & ZURIH_BELIRTECLERI
    # İki kelimelik kalıplar
    dusuk = metin.lower()
    for kalip in ZURIH_BELIRTECLERI:
        if " " in kalip and kalip in dusuk:
            eslesen.add(kalip)
    return len(eslesen) >= 2


def google_hizli_cevir(metin, hedef_dil):
    """Google'ın anahtarsız gtx uç noktası — ücretsiz, resmi olmayan."""
    url = ("https://translate.googleapis.com/translate_a/single"
           f"?client=gtx&sl=auto&tl={hedef_dil}&dt=t&q="
           + urllib.parse.quote(metin))
    istek = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(istek, timeout=10) as yanit:
        veri = json.loads(yanit.read().decode("utf-8"))
    return "".join(parca[0] for parca in veri[0] if parca and parca[0])


def google_toplu_cevir(metinler, hedef_dil):
    """Tüm blokları TEK istekte çevirir (clients5 çoklu-q destekler).
    Paralel istek atmak Google'ın hız sınırına takılıyor (429)."""
    try:
        params = "&".join("q=" + urllib.parse.quote(m) for m in metinler)
        url = ("https://clients5.google.com/translate_a/t"
               f"?client=dict-chrome-ex&sl=auto&tl={hedef_dil}&" + params)
        istek = urllib.request.Request(
            url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(istek, timeout=15) as yanit:
            veri = json.loads(yanit.read().decode("utf-8"))
        sonuc = [oge if isinstance(oge, str) else oge[0] for oge in veri]
        if len(sonuc) != len(metinler):
            raise ValueError("clients5 yanıtı eksik döndü")
        return sonuc
    except Exception:
        # Yedek: sıralı gtx istekleri (aralıklı, hız sınırına takılmasın)
        sonuc = []
        for i, m in enumerate(metinler):
            if i:
                time.sleep(0.2)
            sonuc.append(google_hizli_cevir(m, hedef_dil))
        return sonuc


def grok_toplu_cevir(metinler, hedef_dil, ayarlar):
    """Tüm blokları tek Grok çağrısında çevirir (JSON dizisi protokolü)."""
    dil_adi = DIL_ADLARI.get(hedef_dil, hedef_dil)
    sistem = f"""Sen profesyonel bir çevirmensin. Kaynak metinler bir sohbet \
ekranından alındı; İsviçre Almancası (Zürih lehçesi), standart Almanca veya \
başka bir dil olabilir. Hepsini doğal ve günlük bir {dil_adi} ile çevir.

Kurallar:
- Anlamı yumuşatma, değiştirme; samimi mesajlaşma tonunu koru.
- Sana JSON dizisi vereceğim. Her öğeyi ayrı çevir, sırayı koru.
- SADECE çevirilerden oluşan, aynı uzunlukta bir JSON dizisi döndür.
- Açıklama, not veya kod bloğu işareti ekleme."""
    govde = json.dumps({
        "model": ayarlar["grok_model"],
        "messages": [
            {"role": "system", "content": sistem},
            {"role": "user", "content": json.dumps(metinler, ensure_ascii=False)},
        ],
        "temperature": 0.3,
    }).encode("utf-8")
    istek = urllib.request.Request(
        "https://api.x.ai/v1/chat/completions",
        data=govde,
        headers={
            "Content-Type": "application/json",
            "Authorization": f"Bearer {ayarlar['grok_api_key']}",
        },
        method="POST")
    with urllib.request.urlopen(istek, timeout=90) as yanit:
        veri = json.loads(yanit.read().decode("utf-8"))
    icerik = veri["choices"][0]["message"]["content"].strip()
    icerik = re.sub(r"^```(?:json)?\s*|\s*```$", "", icerik)
    sonuc = json.loads(icerik)
    if not isinstance(sonuc, list) or len(sonuc) != len(metinler):
        raise ValueError("Grok yanıtı beklenen JSON dizisi formatında değil")
    return [str(c) for c in sonuc]


def blocklari_cevir(bloklar, motor, ayarlar):
    """Blokları çevirir; kullanılan motor adını döndürür."""
    hedefler = [b for b in bloklar if b.cevrilebilir]
    if not hedefler:
        return "yok"
    metinler = [b.metin for b in hedefler]

    if motor == "auto":
        motor = "ai" if zurih_lehcesi_mi(" ".join(metinler)) else "hizli"
    if motor == "ai" and not ayarlar.get("grok_api_key"):
        motor = "hizli"

    if motor == "ai":
        try:
            ceviriler = grok_toplu_cevir(metinler, ayarlar["hedef_dil"], ayarlar)
            motor_adi = "Grok AI"
        except Exception as e:
            print(f"Grok hatası, ücretsiz motora düşülüyor: {e}", file=sys.stderr)
            ceviriler = google_toplu_cevir(metinler, ayarlar["hedef_dil"])
            motor_adi = "Google (AI hata verdi)"
    else:
        ceviriler = google_toplu_cevir(metinler, ayarlar["hedef_dil"])
        motor_adi = "Google (ücretsiz)"

    for blok, ceviri in zip(hedefler, ceviriler):
        blok.ceviri = ceviri
    return motor_adi


# ------------------------------------------------------------ Ekran yakalama

def ekran_izni_iste():
    try:
        if not Quartz.CGPreflightScreenCaptureAccess():
            Quartz.CGRequestScreenCaptureAccess()
            return False
    except Exception:
        pass
    return True


def uygulamayi_one_al():
    """Aksesuar uygulamayı geçici olarak öne alır; seçim penceresi ancak
    böyle görünür ve klavye (Esc) alır — Spotlight'ın yaptığı gibi."""
    try:
        from AppKit import NSApplication
        NSApplication.sharedApplication().activateIgnoringOtherApps_(True)
    except Exception:
        pass


def hep_goster(pencere):
    """Tool pencereleri, uygulama aktif değilken macOS tarafından gizlenir
    (aksesuar uygulamada hiç görünmezler!). Bu bayrak her durumda gösterir."""
    pencere.setAttribute(Qt.WidgetAttribute.WA_MacAlwaysShowToolWindow, True)


def dock_ikonunu_gizle():
    """Süreç, CLT Python'un kendi Python.app kimliğiyle çalıştığı için
    Dock'ta 'Python' ikonu belirir. Süreci 'aksesuar' (yalnız menü çubuğu)
    politikasına çekerek Dock ikonunu kaldırıyoruz."""
    try:
        from AppKit import NSApplication
        # 1 = NSApplicationActivationPolicyAccessory
        NSApplication.sharedApplication().setActivationPolicy_(1)
    except Exception as e:
        print(f"Dock ikonu gizlenemedi: {e}", file=sys.stderr)


def bolgeyi_yakala(bolge: QRect):
    yol = os.path.join(tempfile.gettempdir(),
                       f"ekran_ceviri_{os.getpid()}.png")
    subprocess.run(
        ["screencapture", "-x",
         f"-R{bolge.x()},{bolge.y()},{bolge.width()},{bolge.height()}", yol],
        check=True)
    return yol


# ---------------------------------------------------------------- Arka plan işi

class CeviriIsi(QThread):
    """OCR + çeviri boru hattını arayüzü kilitlemeden yürütür."""
    bitti = pyqtSignal(object)   # (bloklar, motor_adi, QImage) | Exception
    # png_yolu verilirse: OCR+çevir. bloklar verilirse: sadece yeniden çevir.

    def __init__(self, ayarlar, motor, bolge=None, png_yolu=None, bloklar=None):
        super().__init__()
        self.ayarlar = ayarlar
        self.motor = motor
        self.bolge = bolge
        self.png_yolu = png_yolu
        self.bloklar = bloklar

    def run(self):
        try:
            if self.bloklar is None:
                goruntu = QImage(self.png_yolu)
                satirlar = ocr_yap(self.png_yolu, self.ayarlar["ocr_dilleri"])
                os.unlink(self.png_yolu)
                bloklar = bloklara_ayir(
                    satirlar, self.bolge.width(), self.bolge.height())
            else:
                goruntu = None
                bloklar = self.bloklar
            motor_adi = blocklari_cevir(bloklar, self.motor, self.ayarlar)
            self.bitti.emit((bloklar, motor_adi, goruntu))
        except Exception as e:
            self.bitti.emit(e)


# ------------------------------------------------------------------ Arayüz

class BolgeSecici(QWidget):
    """Ekran görüntüsü aracı gibi sürükleyerek bölge seçtirir."""
    secildi = pyqtSignal(QRect)
    iptal = pyqtSignal()

    def __init__(self, ekran):
        super().__init__()
        self.setWindowFlags(Qt.WindowType.FramelessWindowHint
                            | Qt.WindowType.WindowStaysOnTopHint)
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)
        self.setCursor(Qt.CursorShape.CrossCursor)
        self.setGeometry(ekran.geometry())
        self.baslangic = None
        self.simdiki = None

    def secim_recti(self):
        if not self.baslangic or not self.simdiki:
            return QRect()
        return QRect(self.baslangic, self.simdiki).normalized()

    def paintEvent(self, olay):
        p = QPainter(self)
        p.fillRect(self.rect(), QColor(0, 0, 0, 70))
        r = self.secim_recti()
        if not r.isEmpty():
            p.setCompositionMode(QPainter.CompositionMode.CompositionMode_Clear)
            p.fillRect(r, Qt.GlobalColor.transparent)
            p.setCompositionMode(
                QPainter.CompositionMode.CompositionMode_SourceOver)
            p.setPen(QPen(QColor(255, 255, 255, 220), 1.5))
            p.drawRect(r)
            p.setPen(QColor(255, 255, 255, 230))
            p.drawText(r.x(), max(14, r.y() - 6),
                       f"{r.width()} × {r.height()}  —  bırakınca çevrilir")
        else:
            p.setPen(QColor(255, 255, 255, 200))
            f = p.font()
            f.setPointSize(15)
            p.setFont(f)
            p.drawText(self.rect(), Qt.AlignmentFlag.AlignCenter,
                       "Çevrilecek bölgeyi sürükleyerek seç  (Esc: vazgeç)")

    def mousePressEvent(self, olay):
        self.baslangic = olay.position().toPoint()
        self.simdiki = self.baslangic
        self.update()

    def mouseMoveEvent(self, olay):
        if self.baslangic:
            self.simdiki = olay.position().toPoint()
            self.update()

    def mouseReleaseEvent(self, olay):
        r = self.secim_recti()
        self.baslangic = self.simdiki = None
        if r.width() > 15 and r.height() > 15:
            genel = QRect(self.geometry().topLeft() + r.topLeft(), r.size())
            self.secildi.emit(genel)
        else:
            self.iptal.emit()

    def keyPressEvent(self, olay):
        if olay.key() == Qt.Key.Key_Escape:
            self.iptal.emit()


class MesajBaloncugu(QWidget):
    """Kısa bilgi mesajı (ör. 'Yazı bulunamadı')."""

    def __init__(self, metin, konum: QPoint, sure_ms=1800):
        super().__init__()
        self.setWindowFlags(Qt.WindowType.FramelessWindowHint
                            | Qt.WindowType.WindowStaysOnTopHint
                            | Qt.WindowType.Tool)
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)
        hep_goster(self)
        etiket = QLabel(metin, self)
        etiket.setStyleSheet(
            "background: rgba(30,30,30,235); color: white; padding: 8px 14px;"
            "border-radius: 8px; font-size: 13px;")
        etiket.adjustSize()
        self.resize(etiket.size())
        self.move(konum)
        if sure_ms:
            QTimer.singleShot(sure_ms, self.close)


class CeviriKatmani(QWidget):
    """Seçilen bölgenin üzerine oturan çeviri katmanı."""
    BAR_YUKSEKLIK = 40

    def __init__(self, bolge: QRect, goruntu: QImage, bloklar, motor_adi,
                 ayarlar):
        super().__init__()
        self.bolge = bolge
        self.goruntu = goruntu
        self.bloklar = bloklar
        self.ayarlar = ayarlar
        self.orijinal_goster = False
        self.is_parcacigi = None

        self.setWindowFlags(Qt.WindowType.FramelessWindowHint
                            | Qt.WindowType.WindowStaysOnTopHint
                            | Qt.WindowType.Tool)
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)
        self.setAttribute(Qt.WidgetAttribute.WA_DeleteOnClose)
        hep_goster(self)

        # Kontrol çubuğu bölgenin altına, yer yoksa üstüne
        ekran = QGuiApplication.screenAt(bolge.center()) \
            or QGuiApplication.primaryScreen()
        ekran_r = ekran.geometry()
        self.bar_ustte = bolge.bottom() + self.BAR_YUKSEKLIK > ekran_r.bottom()
        if self.bar_ustte:
            geo = QRect(bolge.x(), bolge.y() - self.BAR_YUKSEKLIK,
                        bolge.width(), bolge.height() + self.BAR_YUKSEKLIK)
            self.bolge_ofset_y = self.BAR_YUKSEKLIK
        else:
            geo = QRect(bolge.x(), bolge.y(),
                        bolge.width(), bolge.height() + self.BAR_YUKSEKLIK)
            self.bolge_ofset_y = 0
        self.setGeometry(geo)

        self._bar_kur(motor_adi)
        QShortcut(QKeySequence(Qt.Key.Key_Escape), self, self.close)

    # ---- kontrol çubuğu

    def _bar_kur(self, motor_adi):
        bar = QWidget(self)
        bar.setAttribute(Qt.WidgetAttribute.WA_StyledBackground, True)
        bar_y = 0 if self.bar_ustte else self.bolge.height()
        bar.setGeometry(0, bar_y, self.width(), self.BAR_YUKSEKLIK)
        bar.setStyleSheet("""
            QWidget { background: rgba(28,28,30,240); border-radius: 9px; }
            QLabel  { background: transparent; color: #b8b8bd; font-size: 11px; }
            QPushButton {
                background: rgba(255,255,255,26); color: white; border: none;
                border-radius: 6px; padding: 4px 10px; font-size: 12px; }
            QPushButton:hover { background: rgba(255,255,255,55); }
        """)
        yerlesim = QHBoxLayout(bar)
        yerlesim.setContentsMargins(10, 5, 8, 5)
        yerlesim.setSpacing(6)

        self.motor_etiketi = QLabel(motor_adi)
        yerlesim.addWidget(self.motor_etiketi)
        yerlesim.addStretch()

        ai_dgm = QPushButton("AI ile çevir")
        ai_dgm.clicked.connect(self._ai_ile_cevir)
        yerlesim.addWidget(ai_dgm)

        self.orj_dgm = QPushButton("Orijinal")
        self.orj_dgm.clicked.connect(self._orijinal_degistir)
        yerlesim.addWidget(self.orj_dgm)

        kopyala_dgm = QPushButton("Kopyala")
        kopyala_dgm.clicked.connect(self._kopyala)
        yerlesim.addWidget(kopyala_dgm)

        kapat_dgm = QPushButton("✕")
        kapat_dgm.clicked.connect(self.close)
        yerlesim.addWidget(kapat_dgm)

    def _ai_ile_cevir(self):
        if self.is_parcacigi and self.is_parcacigi.isRunning():
            return
        self.motor_etiketi.setText("Grok AI çeviriyor…")
        self.is_parcacigi = CeviriIsi(self.ayarlar, "ai", bloklar=self.bloklar)
        self.is_parcacigi.bitti.connect(self._ai_bitti)
        self.is_parcacigi.start()

    def _ai_bitti(self, sonuc):
        if isinstance(sonuc, Exception):
            self.motor_etiketi.setText(f"AI hatası: {sonuc}")
            return
        _, motor_adi, _ = sonuc
        self.motor_etiketi.setText(motor_adi)
        self.update()

    def _orijinal_degistir(self):
        self.orijinal_goster = not self.orijinal_goster
        self.orj_dgm.setText("Çeviri" if self.orijinal_goster else "Orijinal")
        self.update()

    def _kopyala(self):
        metin = "\n".join(b.ceviri for b in self.bloklar if b.ceviri)
        QGuiApplication.clipboard().setText(metin)
        self.motor_etiketi.setText("Panoya kopyalandı ✓")

    # ---- çizim

    def _arka_plan_rengi(self, rect):
        """Yamanın rengini, ekran görüntüsünde bloğun hemen dışındaki
        piksellerin medyanından alır (balon rengiyle bire bir uyum)."""
        olcek = self.goruntu.width() / max(1, self.bolge.width())
        r = QRect(int(rect.x() * olcek), int(rect.y() * olcek),
                  int(rect.width() * olcek), int(rect.height() * olcek))
        kirmizi, yesil, mavi = [], [], []
        adim = max(2, r.width() // 30)
        noktalar = []
        for x in range(r.left(), r.right() + 1, adim):
            noktalar += [(x, r.top()), (x, r.bottom())]
        for y in range(r.top(), r.bottom() + 1, adim):
            noktalar += [(r.left(), y), (r.right(), y)]
        for x, y in noktalar:
            if 0 <= x < self.goruntu.width() and 0 <= y < self.goruntu.height():
                c = self.goruntu.pixelColor(x, y)
                kirmizi.append(c.red())
                yesil.append(c.green())
                mavi.append(c.blue())
        if not kirmizi:
            return QColor(240, 240, 240)
        orta = len(kirmizi) // 2
        return QColor(sorted(kirmizi)[orta], sorted(yesil)[orta],
                      sorted(mavi)[orta])

    @staticmethod
    def _sigdir(metin, rect, baslangic_px):
        """Metni dikdörtgene sığdıran en büyük yazı tipini bulur."""
        boyut = max(9, baslangic_px)
        while boyut > 8:
            f = QFont()
            f.setPixelSize(boyut)
            fm = QFontMetrics(f)
            gereken = fm.boundingRect(
                QRect(0, 0, rect.width(), 10_000),
                int(Qt.TextFlag.TextWordWrap), metin)
            if gereken.height() <= rect.height() and \
                    gereken.width() <= rect.width():
                return f
            boyut -= 1
        f = QFont()
        f.setPixelSize(9)
        return f

    def paintEvent(self, olay):
        if self.orijinal_goster or self.goruntu is None:
            return
        p = QPainter(self)
        p.setRenderHint(QPainter.RenderHint.Antialiasing)
        for blok in self.bloklar:
            if not blok.ceviri:
                continue
            yama = blok.rect.adjusted(-4, -3, 4, 3)
            yama.translate(0, self.bolge_ofset_y)
            yama = yama.intersected(
                QRect(0, self.bolge_ofset_y,
                      self.bolge.width(), self.bolge.height()))
            renk = self._arka_plan_rengi(blok.rect.adjusted(-4, -3, 4, 3))
            p.setPen(Qt.PenStyle.NoPen)
            p.setBrush(renk)
            p.drawRoundedRect(yama, 5, 5)

            parlaklik = (0.299 * renk.red() + 0.587 * renk.green()
                         + 0.114 * renk.blue()) / 255
            p.setPen(QColor(25, 25, 27) if parlaklik > 0.55
                     else QColor(250, 250, 250))
            ic = yama.adjusted(4, 2, -4, -2)
            p.setFont(self._sigdir(blok.ceviri, ic,
                                   int(blok.satir_yuksekligi * 0.92)))
            hiza = Qt.AlignmentFlag.AlignRight if blok.saga_yasli \
                else Qt.AlignmentFlag.AlignLeft
            bayraklar = (int(Qt.TextFlag.TextWordWrap) | int(hiza)
                         | int(Qt.AlignmentFlag.AlignVCenter))
            p.drawText(ic, bayraklar, blok.ceviri)


class CeviriAkisi:
    """Akış: bölge seç → yakala → OCR → çevir → katmanı göster.
    `bitince` verilirse (menü çubuğu modu) bitince onu çağırır,
    verilmezse (tek seferlik mod) uygulamadan çıkar."""

    def __init__(self, ayarlar, motor, bitince=None):
        self.ayarlar = ayarlar
        self.motor = motor
        self.bitince = bitince
        self.secici = None
        self.bekleme = None
        self.katman = None
        self.is_parcacigi = None
        self.aktif = False

    def _son(self):
        self.aktif = False
        if self.bitince:
            self.bitince()
        else:
            QApplication.quit()

    def kapat(self):
        """Dışarıdan iptal: açık pencereleri kapatır."""
        for pencere in (self.secici, self.bekleme, self.katman):
            if pencere is not None:
                try:
                    pencere.close()
                except RuntimeError:
                    pass  # WA_DeleteOnClose ile zaten silinmiş olabilir

    def basla(self):
        self.aktif = True
        if not ekran_izni_iste():
            # İzin yokken seçim açmak boş sonuç verir: yönlendirme göster ve
            # Sistem Ayarları'nın doğru sayfasını aç
            geo = QGuiApplication.primaryScreen().geometry()
            self._izin_uyarisi = MesajBaloncugu(
                "⚠️ Ekran Kaydı izni gerekli\n"
                "Açılan Sistem Ayarları sayfasında EkranCeviri (veya Python)\n"
                "anahtarını aç, sonra uygulamayı yeniden başlat",
                QPoint(geo.center().x() - 250, geo.top() + 60), 12000)
            self._izin_uyarisi.show()
            subprocess.run(["open", "x-apple.systempreferences:"
                            "com.apple.preference.security?Privacy_ScreenCapture"])
            QTimer.singleShot(12200, self._son)
            return
        ekran = QGuiApplication.screenAt(QCursor.pos()) \
            or QGuiApplication.primaryScreen()
        self.secici = BolgeSecici(ekran)
        self.secici.secildi.connect(self._bolge_secildi)
        self.secici.iptal.connect(self._iptal_edildi)
        # showFullScreen kullanılmaz: aksesuar uygulamada macOS tam ekran
        # Space geçişi yapamaz ve pencere hiç görünmeyebilir. Ekranı kaplayan
        # normal çerçevesiz pencere + uygulamayı öne alma yeterli.
        uygulamayi_one_al()
        self.secici.show()
        self.secici.raise_()
        self.secici.activateWindow()  # Esc çalışsın

    def _iptal_edildi(self):
        self.secici.close()
        self._son()

    def _bolge_secildi(self, bolge: QRect):
        self.secici.hide()
        self.secici.close()
        # Seçim perdesinin ekrandan tamamen kalkması için kısa bekleme;
        # bekleme baloncuğu ancak kare yakalandıktan SONRA gösterilir ki
        # kendisi ekran görüntüsüne girmesin.
        QTimer.singleShot(300, lambda: self._yakala_ve_cevir(bolge))

    def _yakala_ve_cevir(self, bolge):
        try:
            png = bolgeyi_yakala(bolge)
        except Exception as e:
            MesajBaloncugu(f"Ekran yakalanamadı: {e}",
                           bolge.topLeft(), 4000).show()
            QTimer.singleShot(4200, self._son)
            return
        self.bekleme = MesajBaloncugu(
            "⏳ Çevriliyor…", bolge.topLeft() + QPoint(8, 8), sure_ms=0)
        self.bekleme.show()
        self.is_parcacigi = CeviriIsi(self.ayarlar, self.motor,
                                      bolge=bolge, png_yolu=png)
        self.is_parcacigi.bitti.connect(
            lambda sonuc: self._bitti(bolge, sonuc))
        self.is_parcacigi.start()

    def _bitti(self, bolge, sonuc):
        self.bekleme.close()
        if isinstance(sonuc, Exception):
            MesajBaloncugu(f"Hata: {sonuc}", bolge.topLeft(), 4000).show()
            QTimer.singleShot(4200, self._son)
            return
        bloklar, motor_adi, goruntu = sonuc
        if not any(b.ceviri for b in bloklar):
            MesajBaloncugu("Çevrilecek yazı bulunamadı",
                           bolge.topLeft(), 2500).show()
            QTimer.singleShot(2700, self._son)
            return
        self.katman = CeviriKatmani(bolge, goruntu, bloklar, motor_adi,
                                    self.ayarlar)
        self.katman.destroyed.connect(self._son)
        self.katman.show()


# ------------------------------------------------------- Menü çubuğu uygulaması

class KisayolDinleyici(QObject):
    """⌃⌥T global kısayolunu dinler (Quartz event tap, salt-dinleme).
    macOS 'Giriş İzleme' izni yoksa sessizce devre dışı kalır."""
    tetiklendi = pyqtSignal()
    TUS_T = 17  # ANSI 'T' sanal tuş kodu

    def __init__(self):
        super().__init__()
        self.aktif = False

    def basla(self):
        threading.Thread(target=self._dinle, daemon=True).start()

    def _dinle(self):
        try:
            if hasattr(Quartz, "CGRequestListenEventAccess"):
                Quartz.CGRequestListenEventAccess()

            def geri_cagri(vekil, tur, olay, veri):
                if tur == Quartz.kCGEventKeyDown:
                    kod = Quartz.CGEventGetIntegerValueField(
                        olay, Quartz.kCGKeyboardEventKeycode)
                    bayraklar = Quartz.CGEventGetFlags(olay)
                    if kod == self.TUS_T \
                            and bayraklar & Quartz.kCGEventFlagMaskControl \
                            and bayraklar & Quartz.kCGEventFlagMaskAlternate:
                        self.tetiklendi.emit()
                return olay

            tap = Quartz.CGEventTapCreate(
                Quartz.kCGSessionEventTap,
                Quartz.kCGHeadInsertEventTap,
                Quartz.kCGEventTapOptionListenOnly,
                Quartz.CGEventMaskBit(Quartz.kCGEventKeyDown),
                geri_cagri, None)
            if tap is None:
                return  # izin yok; menüden kullanmaya devam
            self.aktif = True
            kaynak = Quartz.CFMachPortCreateRunLoopSource(None, tap, 0)
            Quartz.CFRunLoopAddSource(Quartz.CFRunLoopGetCurrent(), kaynak,
                                      Quartz.kCFRunLoopCommonModes)
            Quartz.CGEventTapEnable(tap, True)
            Quartz.CFRunLoopRun()
        except Exception as e:
            print(f"Kısayol dinleyici başlatılamadı: {e}", file=sys.stderr)


def tepsi_ikonu():
    """Menü çubuğu için konuşma balonu + 'ç' ikonu (şablon/mask modunda,
    açık-koyu menü çubuğuna kendiliğinden uyar)."""
    pm = QPixmap(36, 36)
    pm.setDevicePixelRatio(2.0)
    pm.fill(Qt.GlobalColor.transparent)
    p = QPainter(pm)
    p.setRenderHint(QPainter.RenderHint.Antialiasing)
    p.setPen(Qt.PenStyle.NoPen)
    p.setBrush(QColor(0, 0, 0))
    p.drawRoundedRect(1, 1, 16, 12, 4, 4)
    # balon kuyruğu
    from PyQt6.QtGui import QPolygon
    p.drawPolygon(QPolygon([QPoint(4, 12), QPoint(9, 12), QPoint(4, 17)]))
    f = QFont("Helvetica")
    f.setPixelSize(10)
    f.setBold(True)
    p.setFont(f)
    p.setPen(QColor(255, 255, 255))
    p.drawText(QRect(1, 1, 16, 12), int(Qt.AlignmentFlag.AlignCenter), "ç")
    p.end()
    ikon = QIcon(pm)
    ikon.setIsMask(True)
    return ikon


class MenuBarUygulamasi(QObject):
    """Menü çubuğunda yaşayan kalıcı uygulama."""

    def __init__(self):
        super().__init__()
        self.ayarlar = ayarlari_yukle()
        self.akis = None

        self.tepsi = QSystemTrayIcon(tepsi_ikonu())
        self.tepsi.setToolTip("Ekran Çeviri")
        menu = QMenu()

        cevir = QAction("Bölgeyi Çevir", menu)
        cevir.triggered.connect(self.cevir_baslat)
        menu.addAction(cevir)

        self.kisayol_bilgi = QAction("Kısayol: ⌃⌥T", menu)
        self.kisayol_bilgi.setEnabled(False)
        menu.addAction(self.kisayol_bilgi)
        menu.addSeparator()

        motor_menu = menu.addMenu("Çeviri motoru")
        motor_grubu = QActionGroup(motor_menu)
        for ad, deger in (("Otomatik (lehçe → AI)", "auto"),
                          ("Grok AI", "ai"),
                          ("Hızlı (ücretsiz)", "hizli")):
            a = QAction(ad, motor_menu, checkable=True)
            a.setChecked(self.ayarlar["motor"] == deger)
            a.triggered.connect(
                lambda _, d=deger: self._ayar_degistir("motor", d))
            motor_grubu.addAction(a)
            motor_menu.addAction(a)

        dil_menu = menu.addMenu("Hedef dil")
        dil_grubu = QActionGroup(dil_menu)
        for kod, ad in DIL_ADLARI.items():
            a = QAction(ad, dil_menu, checkable=True)
            a.setChecked(self.ayarlar["hedef_dil"] == kod)
            a.triggered.connect(
                lambda _, k=kod: self._ayar_degistir("hedef_dil", k))
            dil_grubu.addAction(a)
            dil_menu.addAction(a)

        menu.addSeparator()
        cikis = QAction("Ekran Çeviri'den Çık", menu)
        cikis.triggered.connect(QApplication.quit)
        menu.addAction(cikis)

        self.tepsi.setContextMenu(menu)
        self.tepsi.show()

        self.dinleyici = KisayolDinleyici()
        self.dinleyici.tetiklendi.connect(self.cevir_baslat)
        self.dinleyici.basla()
        QTimer.singleShot(2000, self._kisayol_durumu)

        # Tek kopya sunucusu: uygulama tekrar açılırsa (çift tık/Spotlight)
        # çalışan kopyaya "çevir" sinyali gelir → doğrudan bölge seçimi başlar.
        QLocalServer.removeServer(SOKET_ADI)
        self.sunucu = QLocalServer()
        self.sunucu.newConnection.connect(self._yeni_baglanti)
        self.sunucu.listen(SOKET_ADI)

        # Açılışta görünür geri bildirim + doğrudan seçime başla:
        # menü çubuğu ikonu (çentik/kalabalık yüzünden) gizlense bile
        # uygulamanın çalıştığı ekranın kararmasından belli olur.
        ekran_geo = QGuiApplication.primaryScreen().geometry()
        self.karsilama = MesajBaloncugu(
            "Ekran Çeviri açık — bölgeyi seç (Esc: vazgeç)",
            QPoint(ekran_geo.right() - 420, ekran_geo.top() + 44), 4500)
        self.karsilama.show()
        QTimer.singleShot(700, self.cevir_baslat)

    def _yeni_baglanti(self):
        baglanti = self.sunucu.nextPendingConnection()
        if baglanti is not None:
            baglanti.disconnected.connect(baglanti.deleteLater)
            baglanti.close()
        self.cevir_baslat()

    def _kisayol_durumu(self):
        if not self.dinleyici.aktif:
            self.kisayol_bilgi.setText(
                "Kısayol için: Ayarlar → Gizlilik → Giriş İzleme")

    def _ayar_degistir(self, anahtar, deger):
        self.ayarlar[anahtar] = deger
        try:
            ayarlari_kaydet(self.ayarlar)
        except Exception as e:
            print(f"Ayar kaydedilemedi: {e}", file=sys.stderr)

    def cevir_baslat(self):
        if self.akis and self.akis.aktif:
            if self.akis.katman is not None:
                # Overlay açıkken tekrar tetiklendi: kapat, yeni seçim başlat
                self.akis.kapat()
            else:
                return  # seçim/çeviri zaten sürüyor
        self.akis = CeviriAkisi(self.ayarlar, self.ayarlar["motor"],
                                bitince=lambda: None)
        self.akis.basla()


# ---------------------------------------------------------------------- Test

def oz_test(ai_dahil=False):
    """GUI olmadan boru hattını sınar: örnek sohbet görseli çiz → OCR →
    grupla → lehçe algıla → çevir."""
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
    app = QGuiApplication(sys.argv)
    ayarlar = ayarlari_yukle()

    G, Y = 760, 420
    img = QImage(G * 2, Y * 2, QImage.Format.Format_RGB32)
    img.setDevicePixelRatio(2.0)
    img.fill(QColor(255, 255, 255))
    p = QPainter(img)
    p.setRenderHint(QPainter.RenderHint.Antialiasing)
    f = QFont("Helvetica")
    f.setPixelSize(17)
    p.setFont(f)

    def balon(x, y, w, h, renk, yazi_rengi, satirlar):
        p.setPen(Qt.PenStyle.NoPen)
        p.setBrush(QColor(*renk))
        p.drawRoundedRect(QRect(x, y, w, h), 12, 12)
        p.setPen(QColor(*yazi_rengi))
        for i, s in enumerate(satirlar):
            p.drawText(x + 14, y + 26 + i * 24, s)

    balon(20, 20, 330, 44, (233, 233, 235), (20, 20, 20),
          ["Hoi! Wie gahts dir hüt?"])
    balon(20, 80, 390, 44, (233, 233, 235), (20, 20, 20),
          ["Chunnsch du morn au a d Party?"])
    balon(330, 140, 400, 68, (52, 199, 89), (255, 255, 255),
          ["Ja gärn! Ich bring no es bitzli", "Wii mit, gäll das isch ok?"])
    balon(20, 230, 360, 44, (233, 233, 235), (20, 20, 20),
          ["Super, merci vilmal! Bis morn"])
    p.end()

    yol = os.path.join(tempfile.gettempdir(), "ekran_ceviri_test.png")
    img.save(yol)

    satirlar = ocr_yap(yol, ayarlar["ocr_dilleri"])
    print(f"OCR: {len(satirlar)} satır bulundu")
    for s in satirlar:
        print(f"   {s['text']!r}")

    bloklar = bloklara_ayir(satirlar, G, Y)
    print(f"\nGruplama: {len(bloklar)} blok")
    for b in bloklar:
        r = b.rect
        print(f"   [{r.x()},{r.y()} {r.width()}x{r.height()}] "
              f"sağa_yaslı={b.saga_yasli}  {b.metin!r}")

    tum_metin = " ".join(b.metin for b in bloklar)
    print(f"\nZürih lehçesi algılandı mı: {zurih_lehcesi_mi(tum_metin)}")

    motor = "ai" if ai_dahil else "hizli"
    motor_adi = blocklari_cevir(bloklar, motor, ayarlar)
    print(f"\nÇeviri ({motor_adi}):")
    for b in bloklar:
        if b.ceviri:
            print(f"   {b.metin!r}\n   → {b.ceviri!r}\n")
    os.unlink(yol)


# ---------------------------------------------------------------------- Main

def main():
    argv = sys.argv[1:]
    if "--test" in argv:
        oz_test(ai_dahil="--test-ai" in argv)
        return

    app = QApplication(sys.argv)
    app.setQuitOnLastWindowClosed(False)
    dock_ikonunu_gizle()

    if "--sec" in argv:
        # Tek seferlik mod: hemen bölge seç, bitince çık (Alfred vb. için)
        ayarlar = ayarlari_yukle()
        motor = ayarlar["motor"]
        if "--ai" in argv:
            motor = "ai"
        elif "--hizli" in argv:
            motor = "hizli"
        if "--dil" in argv:
            try:
                ayarlar["hedef_dil"] = argv[argv.index("--dil") + 1]
            except IndexError:
                pass
        akis = CeviriAkisi(ayarlar, motor)
        akis.basla()
    else:
        # Zaten çalışan kopya varsa ona "çevir" sinyali gönder ve çık
        deneme = QLocalSocket()
        deneme.connectToServer(SOKET_ADI)
        if deneme.waitForConnected(300):
            deneme.disconnectFromServer()
            return
        # Varsayılan: menü çubuğu uygulaması
        uygulama = MenuBarUygulamasi()  # noqa: F841 — yaşam süresi app'e bağlı

    sys.exit(app.exec())


if __name__ == "__main__":
    main()
