# Portraets fuer den Funk (ab 3.36)

Funksprecher stehen in `scripts/game/comms.gd` (`SPEAKERS`, Feld `portrait` = Dateiname ohne Endung).
Das Portraet wird quadratisch gezeigt (230 x 230 Pixel im Funkfenster). Reihenfolge der Suche:

1. `reyes.ogv` – Video (Ogg Theora), laeuft stumm in Schleife. Aus GIF oder MP4 umwandeln:
   `ffmpeg -i gesicht.gif -vf "scale=256:256" -c:v libtheora -q:v 7 -an reyes.ogv`
   (auf der Quest wird Theora auf der CPU dekodiert – klein halten, 256 x 256 reicht)
2. `reyes_sheet.png` – Bildfolge: quadratische Einzelbilder nebeneinander in einer Zeile
   (z. B. 8 Bilder a 256 x 256 = 2048 x 256), 12 Bilder je Sekunde. Auf der Quest am billigsten.
   Aus einem GIF: `ffmpeg -i gesicht.gif -vf "scale=256:256,tile=8x1" -frames:v 1 reyes_sheet.png`
3. `reyes.png` oder `reyes.jpg` – Standbild
4. nichts davon: Platzhalter (Silhouette mit Scanlinien)

GIF direkt geht nicht (Godot 4 kann es nicht abspielen) – bitte in 1 oder 2 umwandeln.
