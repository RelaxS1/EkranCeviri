#!/usr/bin/env python3
"""Uygulama ikonunu (ikon.icns) üretir: mavi-mor degrade zemin üzerinde
konuşma balonu ve 'ç' harfi."""
import os
import subprocess
import sys
import tempfile

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
from PyQt6.QtGui import (QGuiApplication, QImage, QPainter, QColor,
                         QLinearGradient, QFont, QPolygon, QBrush)
from PyQt6.QtCore import Qt, QRect, QPoint

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

app = QGuiApplication(sys.argv)

B = 1024
img = QImage(B, B, QImage.Format.Format_ARGB32)
img.fill(Qt.GlobalColor.transparent)
p = QPainter(img)
p.setRenderHint(QPainter.RenderHint.Antialiasing)

# macOS tarzı yuvarlatılmış kare zemin (kenarlarda ~%10 boşluk)
kenar = 100
degrade = QLinearGradient(0, kenar, 0, B - kenar)
degrade.setColorAt(0.0, QColor(59, 130, 246))
degrade.setColorAt(1.0, QColor(124, 58, 237))
p.setPen(Qt.PenStyle.NoPen)
p.setBrush(QBrush(degrade))
p.drawRoundedRect(kenar, kenar, B - 2 * kenar, B - 2 * kenar, 185, 185)

# Konuşma balonu
p.setBrush(QColor(255, 255, 255))
balon = QRect(255, 285, 514, 340)
p.drawRoundedRect(balon, 90, 90)
p.drawPolygon(QPolygon([QPoint(340, 600), QPoint(480, 600), QPoint(345, 730)]))

# 'ç' harfi
f = QFont("Helvetica")
f.setPixelSize(300)
f.setBold(True)
p.setFont(f)
p.setPen(QColor(79, 94, 240))
p.drawText(balon.adjusted(0, -14, 0, -14),
           int(Qt.AlignmentFlag.AlignCenter), "ç")
p.end()

# iconset boyutları ve icns dönüşümü
with tempfile.TemporaryDirectory() as gecici:
    seti = os.path.join(gecici, "ikon.iconset")
    os.makedirs(seti)
    for boyut in (16, 32, 128, 256, 512):
        for olcek in (1, 2):
            piksel = boyut * olcek
            kucuk = img.scaled(piksel, piksel,
                               Qt.AspectRatioMode.KeepAspectRatio,
                               Qt.TransformationMode.SmoothTransformation)
            ek = "@2x" if olcek == 2 else ""
            kucuk.save(os.path.join(seti, f"icon_{boyut}x{boyut}{ek}.png"))
    subprocess.run(["iconutil", "-c", "icns", seti,
                    "-o", os.path.join(BASE_DIR, "ikon.icns")], check=True)
print("ikon.icns üretildi")
