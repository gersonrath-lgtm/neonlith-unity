# Ladebilder der Stufen (ab 3.34)

Hier liegen die Bilder des Ladebildschirms (`scripts/ui/vr/screens/vr_loading.gd`).

- Dateiname: `level_XX.png` (oder `.jpg`), XX = Stufe zweistellig (`level_01.png` … `level_20.png`, `level_00.png` = klassische Karte)
- Format: etwa 2,7 : 1 (z. B. 2160 × 800 px); andere Formate werden mittig zugeschnitten
- Fehlt ein Bild, zeigt der Ladebildschirm einen Platzhalter: das abgedunkelte Weltbild aus `assets/worlds/` mit Raster und Dateiname
- Import: Standard (VRAM-komprimiert, Mipmaps an), keine weiteren Einstellungen nötig
