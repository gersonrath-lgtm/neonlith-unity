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

## Öffnen

1. Unity Hub → „Add project from disk“ → die Wurzel dieses Repositories wählen. Unity 6000.0 LTS (Version in
   `ProjectSettings/ProjectVersion.txt`; eine andere 6000.x-Version geht ebenso, der Hub fragt nach).
2. Beim ersten Öffnen lädt der Package Manager die Pakete aus `Packages/manifest.json`. Passt eine Version nicht
   zur Editorversion, im Package Manager die nächste freigegebene Version wählen. Die Pakete:

   | Paket | Zweck | Godot-Gegenstück |
   |---|---|---|
   | Universal Render Pipeline | Renderer (Forward, Mobile) | Renderer „Mobile“ (Vulkan) |
   | OpenXR Plugin + XR Management | Brille, Controller, Handtracking | `[xr] openxr/*` in project.godot |
   | Unity OpenXR: Meta | Quest-Features (Foveation, Refresh-Rate, Render-Model) | `xr_features/enable_meta_plugin` |
   | XR Interaction Toolkit | XR Origin, Controller, Strahl auf Tafeln | `XROrigin3D`, `XRController3D`, Strahl in `XRMain` |
   | XR Hands | Handtracking | `openxr/extensions/hand_tracking` |
   | XR Composition Layers | Ladebild und Tafeln als OpenXR-Ebenen | `OpenXRCompositionLayerQuad/Cylinder` |
   | Input System | Aktionen der Controller | `openxr_action_map.tres` |
   | glTFast | GLB-Modelle importieren (Unity liest GLB nicht von Haus aus) | Godot-GLTF-Import |
   | Newtonsoft Json | `campaign.json`, Karten mit gemischten Listen | `JSON.parse_string` |
   | uGUI (mit TextMeshPro) | Tafeln, Schrift mit Abstandsfeld | `Control` in SubViewports, `Label3D` MSDF |
   | Test Framework | Tests | `scripts/tests/*_runner.gd` |

3. Menü **Neonlith → Projekt einrichten (Quest 3)**: setzt Produktname, Paketname `com.example.neonlith`,
   Version 4.99.65 / Code 5055, IL2CPP, nur ARM64, Android-API 32, Vulkan, linearen Farbraum, ASTC, kein Splash.
4. Von Hand (lässt sich nicht sicher per Skript setzen):
   - *Project Settings → XR Plug-in Management → Android*: OpenXR ankreuzen.
   - *OpenXR → Android*: Interaction Profiles „Oculus Touch Controller Profile“ und „Hand Interaction Profile“;
     Features „Meta Quest Support“, „Meta Quest: Foveated Rendering“ (Stufe 2, nicht dynamisch, wie Godot
     `foveation_level=2`, `foveation_dynamic=false`), „Hand Tracking“ (XR Hands). Render Mode „Multi-view“
     (Godot `view_configuration=1`, Stereo). Kein Passthrough (seit 4.99.44 entfernt).
   - URP-Asset für die Quest anlegen und unter *Graphics* und *Quality (Android)* eintragen: Forward, **kein**
     Post-Processing, keine HDR-Zwischenbilder, MSAA 4x, keine Schatten (Godot: `docs/PERFORMANCE.md`,
     `docs/KLARBILD.md`), keine Depth- oder Opaque-Texture (sonst entfällt das Subsampling der Foveation).
   - Szene `Assets/Neonlith/Scenes/Bootstrap.unity` anlegen: leeres GameObject mit `Bootstrap`, dazu ein
     „XR Origin (XR Rig)“ aus dem XR Interaction Toolkit; in *Build Settings* als erste Szene eintragen.
5. Tests: *Window → General → Test Runner*, `Neonlith.Tests` (prüfen Daten und Weltzuordnung).

## Ordner

```
neonlith-unity/
  Assets/Neonlith/
    Art/Models/        GLB aus assets/models (Türme, Straße, Hangar, Gewehr, Schiff) und Quellen/ (Gegner-Rohmodelle)
    Art/Models/AusGodot/  (nach Tools/unity_export.gd) Godot-.mesh und Generator-Modelle als GLB
    Art/Textures, Art/Worlds, Art/Fonts
    Audio/Music, Audio/SFX
    UI/Icons, UI/Thumbs, UI/missions, UI/comms, UI/loading
    Resources/Data/    campaign.json, hangar.json, levels/**, config/*.json (als TextAsset)
    Scripts/Runtime    Neonlith.Runtime (Core, Data)
    Scripts/Editor     Neonlith.Editor (Projekteinrichtung)
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

Alles, was in Godot aus Code entsteht: 73 000 Zeilen GDScript (Spiel, Türme, Gegner, Brückengenerator, Hub,
Menüs, Standmodus mit Gewehr), 88 Godot-Shader und 36 Skripte, die Netze prozedural bauen. Reihenfolge, Aufwand
und Abbildung je System: `Docs/UNITY_PORT.md`.
