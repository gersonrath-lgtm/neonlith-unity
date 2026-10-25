# Neonlith für Unity: Grundgerüst

Unity-Port von Neonlith (Unity 6, URP, OpenXR, Zielgerät Meta Quest 3/3S). **Führend bleibt das Godot-Projekt**
`gersonrath-lgtm/neonlith`: Spielinhalt, Balance, Missionen, Modelle, Klänge und Texturen entstehen dort; dieses
Repository wird daraus mit den Werkzeugen unter `Tools/` befüllt und enthält nur, was Unity braucht. Es enthält
noch **kein portiertes Spiel**, sondern:

- alle Assets des Godot-Projekts, die Unity direkt lesen kann (`ASSET_MANIFEST.md`: Modelle als GLB, Texturen,
  Audio, Schriften, Icons, Vorschaubilder, Leveldaten als JSON, Lizenzen);
- die Godot-Konfigurationsressourcen (`data/*.tres`) als JSON unter `Assets/Neonlith/Resources/Data/config`;
- ein C#-Grundgerüst (`Assets/Neonlith/Scripts`): Weltfarben (`WorldTheme`), globale Shaderwerte
  (`ShaderGlobals`), Datenzugriff (`GameData`), Einstieg (`Bootstrap`), Projekteinrichtung im Editor
  (Menü „Neonlith → Projekt einrichten (Quest 3)“) und erste Tests;
- die Paketliste (`Packages/manifest.json`).

Der Plan für den eigentlichen Port, die Abbildung der Godot-Konzepte und die Reihenfolge der Etappen stehen in
`Docs/UNITY_PORT.md`.

## Spielstand des Ports

Mission 1 bis 20 aus `campaign.json` laufen als Ego-Shooter-Tower-Defense auf der Quest 3, mit den Regeln des
Godot-Projekts (Balance, Kampf, Gegner, Türme 1:1 übernommen):

- **Welt**: Brücke der Mission aus den Kartendaten (Läufe, Dämme, Leuchtlinien, Knotenplatten, Kernplatte,
  Standdeck, Wände), Himmel und Farben der Welt, Nebel.
- **Spieler**: steht in Lebensgröße auf dem Standdeck, Gehen mit dem linken Stick, Drehen in 30°-Stufen, Begrenzung
  auf das Deck; Foveation Stufe 2, 90 Hz (Meta-OpenXR), Auflösung 1,2, AutoRes.
- **Waffenarsenal**: Pistole (ein Schuss je Abzug), Sturmgewehr (Dauerfeuer, Ladeschuss mit dem Griff, zweite Hand
  am Vordergriff), Sniper (breiter Strahl); Magazin, Nachladen mit der Waffe nach unten, Vibration; Freischaltung
  nach Welt I und II, Wahl im Menü.
- **Türme und Bauen**: Bauplätze aus den Läufen (Modulraster, pad_r), Sperrplätze auf der Fahrbahn; die freie Hand
  trägt einen Strahl: auf einem Bauplatz öffnet der Abzug das Baumenü mit den Türmen der Mission (Gatling,
  Kryo-Blaster, Plasmawerfer, Tesla-Spule, Railgun, Raketenwerfer, Generator; Barrikade, Laserzaun), auf einem Turm
  die Turm-Tafel (Ausbau, Verkauf, Zielwahl). Zielwahl, Geschosse mit Bogen, Kettenblitz, Railgun-Treffer,
  Flächenschaden, Verlangsamung, Brand mit Rüstungsbruch, Thermoschock, Spröde, Barrikaden mit Dornen, Laserzaun,
  Generator-Einkommen, Verkauf (70 %, in derselben Bauphase 100 %), früher Start mit Bonus.
- **Gegner**: alle Arten mit Fähigkeiten (Hetzer-Sprint, Schilder-Abschirmung, Nebelweberin-Heilung, Spieher legt
  Türme lahm, Packträger platzt, Flieger), Eliten (Schildträger, Phantom, Unbeugsam, Signalschatten), Bosse
  (Rufe, Wut, Regeneration, Immunitäten), Lebensfaktor je Welle nach `run.gd`.
- **Ablauf**: Funk des Protokolls (Wellenansagen mit Zusammensetzung, Zwischenfunk, Durchbruch, Anomalie), Countdown,
  Wellen, Leben am Kern, Sieg und Niederlage, Pausenmenü (Weiter, neu starten, Missionen mit Freischaltung und
  Sternen, Waffe, Waffenhand), Spielstand in `progress.json`.
- **Segenskarten**: nach jeder dritten Welle drei Karten im Halbkreis vor der Brust (gewöhnlich, selten, episch),
  Wahl per Strahl; alle 25 Karten mit ihren Wirkungen auf die Türme.
- **Gizmo und Tutorial**: die Begleitdrohne schwebt links vorn, reagiert auf Wellen, Treffer, Bosse, Sieg und
  Niederlage; in Mission 1 führt sie einmal durch Bauen, Wellenstart, Gewehr und Turm-Tafel.
- **Basis**: Kernsplitter je Mission (Wellen, erster Sieg, Sterne) und alle acht Fertigkeitsbäume (Kern, Gewehr,
  sechs Türme: Stamm, dann Pfad A oder B, Umskillen) im Pausenmenü unter „Basis“; Wirkungen auf Turmwerte,
  Kosten, Startessenz, Leben, Einkommen, zweites Herz, Sonderfähigkeiten (Splitterbolzen, Schockfrost, Flächenbrand,
  Blitzschlag, Hinrichtung, Nachbeben, Permafrost).
- **Karte**: Tempozonen (schnell, langsam) und Engstellen der Missionen 4, 5, 9, 10, 14, 15, 19, 20; Wände als
  Aufstiege. Wetter je Mission (Wind, Niesel, Nebel, Regen, Gewitter, Sturm): Partikel um den Spieler, Sichtweite,
  Blitze mit Donner, Klangkulisse.
- **Hände**: Roboterhände über den Controllern (Finger beugen sich mit Abzug und Griff) und Unterarme.

| Eingabe | Wirkung |
|---|---|
| Linker Stick | Gehen (2,2 m/s, kopfrelativ) |
| Rechter Stick | Drehen in Stufen von 30° |
| Abzug (Waffenhand) | Feuer |
| Griff (Waffenhand) | Sturmgewehr: Ladeschuss laden, loslassen feuert |
| Griff (freie Hand) | zweite Hand am Vordergriff |
| Abzug (freie Hand) | Strahl: Bauplatz → Baumenü, Turm → Turm-Tafel, Knopf wählen |
| Waffe nach unten | Nachladen |
| A | Bau-Modus: alle Bauplätze zeigen |
| B | Menü schließen |
| X | nächste Welle sofort (Bonus-Essenz) |
| Menü-Taste (links) | Pause und Menü |

Noch nicht dabei (Reihenfolge in `Docs/UNITY_PORT.md`): Rook und Hände, Hub und Halle der Tore, Fertigkeitsbäume mit Kernsplittern, Zonen und Wandläufe der Gegner, Nachschub und
Raider, Wetter, die eigenen Shader (Fernberuhigung, Holo), Ladebild als OpenXR-Ebene, die reichen Modelle der
Türme, Gegner und Waffen (vorerst Formen aus Kästen und Prismen).

## Öffnen und bauen

1. Unity Hub → „Add project from disk“ → die Wurzel dieses Repositories wählen. Unity 6000.0 LTS (Version in
   `ProjectSettings/ProjectVersion.txt`; eine andere 6000.x-Version geht ebenso, der Hub fragt nach). Android Build
   Support mit OpenJDK, SDK und NDK muss im Hub installiert sein.
2. Beim ersten Öffnen lädt der Package Manager die Pakete aus `Packages/manifest.json`. Passt eine Version nicht
   zur Editorversion, im Package Manager die nächste freigegebene Version wählen.
3. Menü **Neonlith → Alles einrichten (Quest 3)**. Das setzt der Reihe nach:
   1. Player-Einstellungen: Paket `com.example.neonlith`, Version 4.99.65 / Code 5055, IL2CPP, nur ARM64,
      Android-API 32, Vulkan, linearer Farbraum, ASTC, kein Splash; Build Target Android.
   2. Materialien in `Resources/Materials` (URP Lit und Unlit als Grundlage, Himmel je Welt aus `space_sky_w1..4`).
   3. URP-Asset `Settings/NeonlithURP.asset`: MSAA 4x, kein HDR, keine Schatten, keine Depth- und Opaque-Texture
      (sonst entfällt das Subsampling der Foveation), als Pipeline in Graphics und allen Quality-Stufen.
   4. XR Plug-in Management: OpenXR für Android; OpenXR: Multiview, kein Depth-Submit, Oculus Touch Controller
      Profile, Hand Interaction Profile, Meta-Quest-Features, Foveated Rendering.
   5. Szene `Scenes/Bootstrap.unity` mit dem Objekt `Bootstrap` (Mission, Waffe, Linkshänder einstellbar), als
      einzige Szene in den Build Settings.
   Jeder Schritt steht auch einzeln im Menü. Was ein Schritt nicht per Skript schafft, meldet er im Log mit dem Ort
   in den Project Settings; vor dem ersten Build einmal *Project Settings → XR Plug-in Management* (Android: OpenXR
   angekreuzt) und *OpenXR* (Profile und Meta-Features) nachsehen.
4. Menü **Neonlith → APK bauen (Quest 3)** schreibt `Builds/Neonlith.apk`. Installieren mit
   `adb install -r Builds/Neonlith.apk`, in der Brille unter „Unbekannte Quellen“ starten.
5. Tests: *Window → General → Test Runner*, `Neonlith.Tests` (Daten, Missionen, Weltzuordnung).

Die Pakete:

   | Paket | Zweck | Godot-Gegenstück |
   |---|---|---|
   | Universal Render Pipeline | Renderer (Forward, Mobile) | Renderer „Mobile“ (Vulkan) |
   | OpenXR Plugin + XR Management | Brille, Controller, Handtracking | `[xr] openxr/*` in project.godot |
   | Unity OpenXR: Meta | Quest-Features (Foveation, Refresh-Rate) | `xr_features/enable_meta_plugin` |
   | XR Interaction Toolkit | Strahl auf Tafeln (ab Etappe 5) | Strahl in `XRMain` |
   | XR Hands | Handtracking | `openxr/extensions/hand_tracking` |
   | XR Composition Layers | Ladebild und Tafeln als OpenXR-Ebenen (ab Etappe 3) | `OpenXRCompositionLayerQuad/Cylinder` |
   | Input System | Aktionen der Controller (`XRInput`, im Code, keine .inputactions nötig) | `openxr_action_map.tres` |
   | glTFast | GLB-Modelle importieren (Unity liest GLB nicht von Haus aus) | Godot-GLTF-Import |
   | Newtonsoft Json | `campaign.json`, Karten mit gemischten Listen | `JSON.parse_string` |
   | uGUI (mit TextMeshPro) | Tafeln, Schrift mit Abstandsfeld (ab Etappe 5) | `Control` in SubViewports, `Label3D` MSDF |
   | Test Framework | Tests | `scripts/tests/*_runner.gd` |

## Cloud-Build ohne Unity auf dem Mac

Der Workflow `.github/workflows/quest-apk.yml` baut die APK bei jedem Push auf `main` auf GitHub (GameCI,
Unity 6000.0.58f1, IL2CPP, ARM64). Einmalig einzurichten:

1. **Unity-Konto und Lizenzdatei.** Unity Hub installieren (nur der Hub, kein Editor), anmelden, dann
   *Preferences → Licenses → Add → „Get a free personal license“*. Das schreibt die Datei
   `/Library/Application Support/Unity/Unity_lic.ulf` (im Finder: Gehe zu → Ordner, Pfad eingeben; der Ordner
   `Library` liegt auf der obersten Ebene der Platte, nicht im Benutzerordner). Erscheint die Lizenz im Hub, aber
   die Datei fehlt, den Schritt wiederholen.
2. **Drei Secrets** im Repository `neonlith-unity` unter *Settings → Secrets and variables → Actions → New
   repository secret*:
   - `UNITY_LICENSE`: der komplette Inhalt der Datei `Unity_lic.ulf` (mit TextEdit öffnen, alles kopieren),
   - `UNITY_EMAIL`: die E-Mail des Unity-Kontos,
   - `UNITY_PASSWORD`: das Passwort des Unity-Kontos.
3. Unter *Actions* den Workflow **Quest-APK** einmal von Hand starten („Run workflow“) oder einfach pushen.
   Der erste Lauf dauert 30 bis 60 Minuten (Docker-Image, IL2CPP), spätere mit Cache kürzer. Ohne Secrets endet der
   Lauf mit einem Hinweis, nicht mit einem Fehler.
4. **Ergebnis**: unter *Releases* das Vorab-Release **quest-latest** mit `Neonlith.apk`, außerdem als Artefakt am
   Workflow-Lauf. Die Lizenz bleibt an deinem Konto; Personal reicht für den Build.

**Auf die Quest bringen** (ohne Unity): entweder die App **SideQuest** auf dem Mac (APK hineinziehen) oder die
Android-„Platform-Tools“ von Google (klein, nur `adb`):

```
~/platform-tools/adb install -r ~/Downloads/Neonlith.apk
```

Die Quest muss im Entwicklermodus sein und die Verbindung zum Mac einmal in der Brille erlaubt werden. Die App
erscheint dann unter „Unbekannte Quellen“. Logmeldungen für die Fehlersuche: `adb logcat -s Unity`.

## Ordner

```
neonlith-unity/
  Assets/Neonlith/
    Art/Models/        GLB aus assets/models (Türme, Straße, Hangar, Gewehr, Schiff) und Quellen/ (Gegner-Rohmodelle)
    Art/Models/AusGodot/  (nach Tools/unity_export.gd) Godot-.mesh und Generator-Modelle als GLB
    Art/Textures, Art/Worlds, Art/Fonts
    Resources/Audio/   Musik und Klänge (als Resources ladbar: Resources.Load<AudioClip>("Audio/SFX/..."))
    UI/Icons, UI/Thumbs, UI/missions, UI/comms, UI/loading
    Resources/Data/    campaign.json, hangar.json, levels/**, config/*.json (als TextAsset)
    Scripts/Runtime    Neonlith.Runtime: Core (Bootstrap, WorldTheme, ShaderGlobals), Data (Balance, MissionData,
                       GameData), XR (XRInput, XRRig), Game (GameSession, Enemy, PlayerWeapon), View (MeshKit,
                       BridgeBuilder, SkyBuilder, NeonMaterials), UI (Hud)
    Scripts/Editor     Neonlith.Editor (Projekteinrichtung, Szene, APK-Build, CI-Einstieg CiBuild)
    Settings/          URP-Asset und Renderer (vom Menü angelegt)
    Scenes/            Bootstrap.unity (vom Menü angelegt)
    Shaders/           leer: hier entstehen die URP-Shader (Tabelle in Docs/UNITY_PORT.md)
    Tests/Runtime      Neonlith.Tests
    Texte/             CREDITS.txt, Lizenzen, Encyclopedia
  Docs/UNITY_PORT.md   Plan, Abbildung Godot→Unity, Etappen
  Tools/               Befüllung aus dem Godot-Projekt
  Packages/manifest.json
  ProjectSettings/ProjectVersion.txt
```

## Aus dem Godot-Projekt befüllen

Missionen, Karten, Konfigurationen, Modelle, Texturen und Klänge entstehen weiter im Godot-Projekt
(`tools/art/levels/generate_missions.py`, `data/*.tres`, `assets/`). Nichts davon hier von Hand ändern; nach
Änderungen dort beide Repositories nebeneinander auschecken und ausführen:

```
# in neonlith-unity: Assets, Daten, .tres→JSON, ASSET_MANIFEST.md
python3 -I Tools/unity_copy_assets.py ../neonlith

# in neonlith (Godot-Projekt): Godot-.mesh und Generator-Modelle als GLB nach Assets/Neonlith/Art/Models/AusGodot
godot --headless --path . -s ../neonlith-unity/Tools/unity_export.gd -- --out=../neonlith-unity
```

Die Unity-Metadateien (`*.meta`) legt der Editor beim ersten Öffnen an; sie gehören danach mit ins Repository.

## Was fehlt

Der Spielkern ist ein erster Durchstich; der größte Teil der 73 000 Zeilen GDScript (Türme, Bauen, Eliten, Bosse,
Brückengenerator in voller Form, Hub, Menüs, Fertigkeiten, Spielstände), die 88 Godot-Shader und die prozeduralen
Modelle sind noch nicht portiert. Reihenfolge, Aufwand und Abbildung je System: `Docs/UNITY_PORT.md`.
