# Unity-Port: Plan und Abbildung Godot → Unity

Stand 4.99.65 (Godot 4.7, Renderer Mobile/Vulkan, Meta Quest 3). Ziel des Ports ist dasselbe Spiel in Unity 6 mit
URP und OpenXR auf der Quest 3/3S. **Das Godot-Projekt `neonlith` bleibt führend und unverändert**; der Port lebt in
diesem eigenen Repository `neonlith-unity` und wird mit den Werkzeugen unter `Tools/` aus Godot befüllt. Dieses
Dokument beschreibt, was schon übernommen ist, wie sich die Godot-Konzepte in Unity abbilden, in welcher Reihenfolge portiert wird und wie jede Etappe geprüft wird.
Die Creative Direction (`SILENCE_PROTOCOL.md`), die Regeln für die Brille (`KLARBILD.md`, `HOLO_NACHT.md`,
`PERFORMANCE.md`, `LADEN_UND_LEISTUNG.md`) und die Balance (`KAMPAGNE.md`) gelten für den Port unverändert.

## 1. Stand: was dieses Repository enthält

| Teil | Stand | Ort |
|---|---|---|
| Unity-Projekt (Unity 6000.0, URP, OpenXR, XRI, glTFast, Newtonsoft) | angelegt, noch nicht im Editor geöffnet | `Packages/manifest.json`, `ProjectSettings` |
| Assets, die Unity direkt liest: 54 GLB, Texturen, 102 Klänge, 4 Musiktitel, Schriften, Icons, Vorschaubilder, Android-Icons, Lizenzen | kopiert (417 Dateien, 194 MB) | `Assets/Neonlith/Art`, `Audio`, `UI`, `Texte`, Liste in `ASSET_MANIFEST.md` |
| Rohmodelle der Gegner (Tripo-GLB aus `tools/art/src`, `tools/art/rig`) | kopiert | `Art/Models/Quellen` |
| Spieldaten: `campaign.json`, 20 Missionskarten, Brückenbausätze, `hangar.json` | kopiert, unverändert | `Resources/Data` |
| Konfigurationen `data/*.tres` (WeaponConfig, EssenceConfig, HoloTheme, NightSkyConfig, …) | nach JSON gewandelt | `Resources/Data/config` |
| C#: `WorldTheme` (vier Welten), `ShaderGlobals` (Palette wie `Palette.apply`), `GameData` (JSON-Zugriff), `Bootstrap`, Projekteinrichtung, Tests | geschrieben, **nicht kompiliert** (hier läuft kein Unity) | `Scripts`, `Tests` |
| Werkzeuge: Assets kopieren, Godot-Netze als GLB exportieren | geschrieben; der GLB-Export ist ungetestet (kein Godot hier) | `Tools/unity_copy_assets.py`, `Tools/unity_export.gd` |
| Spielcode, Shader, prozedurale Modelle, Szenen, Menüs | **nicht portiert** | Etappen unten |

Die Unity-`.meta`-Dateien entstehen beim ersten Öffnen im Editor. Die vier Himmelstexturen `space_sky_w1..w4.png`
sind je 19 bis 21 MB (80 MB zusammen) und machen den größten Teil der Kopie aus.

## 2. Umfang des Godot-Projekts

| Bereich | Dateien | Zeilen | Port |
|---|---:|---:|---|
| `scripts/autoload` (Balance, Settings, SaveGame, Meta, Audio, Assets, CrashLog) | 7 | 2 707 | C#-Dienste |
| `scripts/game` (Spiellogik: Game, Run, Missions, Enemy, Tower, Weapon, Combat, Boons, …) | 39 | 11 035 | C#-Klassen, weitgehend 1:1 (reine Logik ohne Szenenbaum) |
| `scripts/view3d` (Darstellung: Brücken, Türme, Gegner, Himmel, Wetter, Hangar, Tore) | 61 | 22 125 | MonoBehaviours + Mesh-Generatoren, größter Block |
| `scripts/ui` (VR-Menüs, Holo-Bausteine, HUD, Tutorial) | 45 | 8 626 | uGUI/TextMeshPro auf RenderTextures, Holo-Shader |
| `scripts/xr` (XRMain, Standmodus, Gewehr, Körper, Teleport, Tafeln, Drohne) | 33 | 13 277 | XR Origin, XRI, Input System, Composition Layers |
| `scripts/tests` (Test- und Simulationsläufer, Sim-Bot) | 4 | 13 291 | Unity Test Framework; Simulation als Edit-Mode-Test |
| Shader (`assets/shaders`, 88 + 12 Includes) | 100 | 6 886 | URP Shader Graph oder HLSL, neu |
| Netze aus Code (`ArrayMesh`/`SurfaceTool`) | 36 Skripte | – | `Mesh`-API in C# (`MeshKit`, `WeaponMesh`, `StripMesh` als Grundlage) |

Zusammen rund 73 000 Zeilen GDScript. Ein 1:1-Port ist Neuentwicklung in der Größenordnung von mehreren
Personenmonaten; die Etappen in Abschnitt 7 machen daraus einzeln spielbare, prüfbare Schritte.

## 3. Zielplattform und Projekteinstellungen

| Godot (`project.godot`, `export_presets.cfg`) | Unity |
|---|---|
| Renderer `mobile`, Vulkan, `vrs/mode=2` (Foveation über VRS) | URP Forward, Vulkan allein; Foveation über OpenXR-Feature „Meta Quest: Foveated Rendering“ (Stufe 2, fest) |
| `etc2_astc`, Anisotropie 2 | Android-Textur-Subtarget ASTC; Anisotropie im URP-Asset „Per Texture“, Standard 2 |
| MSAA (Settings: 4x), keine Schatten, kein Glow, keine Nachbearbeitung | URP-Asset: MSAA 4x, Schatten aus, Post-Processing aus, Depth/Opaque Texture aus |
| `openxr/view_configuration=1` (Stereo), `reference_space=2` (Stage) | OpenXR Render Mode „Multi-view“; XR Origin Tracking Origin „Floor“ |
| `foveation_level=2`, `foveation_dynamic=false`, `submit_depth_buffer=false` | Meta Foveated Rendering Stufe 2, nicht dynamisch; „Submit Depth“ aus |
| `hand_tracking`, `hand_interaction_profile` | XR Hands + „Hand Interaction Profile“ |
| `meta/passthrough`, `meta/render_model` | nicht nötig (seit 4.99.44 ohne Mixed Reality; Controller-Modelle sind in `XRStand` unsichtbar) |
| Paket `com.example.neonlith`, Version 4.99.65, Code 5055, nur arm64-v8a, Gradle | `NeonlithProjectSetup.Apply()` setzt Paket, Version, IL2CPP, ARM64, API 32, Vulkan, Linear, ASTC |
| Standard 1,2-fache Auflösung, 90 Hz (`Settings`, `AutoRes`) | `XRSettings.eyeTextureResolutionScale` bzw. `renderViewportScale` (AutoRes), Refresh-Rate über das Meta-OpenXR-Feature |
| Hauptszene `scenes/main_xr.tscn` (ein Node3D mit `xr_main.gd`, alles aus Code) | Szene `Bootstrap.unity`: `Bootstrap` + XR Origin; die Welt entsteht weiter aus Code |

## 4. Ordnerabbildung

| Godot | Unity | Hinweis |
|---|---|---|
| `assets/models/**/*.glb` | `Assets/Neonlith/Art/Models/**` | glTFast importiert GLB; Texturen daneben wie in Godot |
| `assets/models/*.mesh` (Gegner, Laub) | `Art/Models/AusGodot/*.glb` nach `Tools/unity_export.gd` | Rohmodelle liegen zusätzlich in `Art/Models/Quellen` |
| `assets/textures`, `assets/worlds`, `assets/fonts` | `Art/Textures`, `Art/Worlds`, `Art/Fonts` | Himmel `space_sky_w1..w4` je Welt; Mipmaps beim Import an |
| `assets/audio/music`, `assets/audio/sfx` | `Audio/Music`, `Audio/SFX` | OGG und WAV lesbar; Busse als AudioMixer-Gruppen |
| `assets/icons`, `assets/thumbs`, `assets/ui` | `UI/Icons`, `UI/Thumbs`, `UI/*` | Import als Sprite (2D/UI) |
| `data/campaign.json`, `data/levels/**`, `data/hangar.json` | `Resources/Data/**` (TextAsset) | nie von Hand ändern, Quelle bleibt `generate_missions.py` |
| `data/*.tres` | `Resources/Data/config/*.json` | Werte 1:1; `Color`/`Vector3` als Objekte, `PackedVector3Array` als Liste |
| `assets/shaders/*.gdshader` | `Assets/Neonlith/Shaders` (neu) | Abschnitt 6 |
| `assets/materials/*.tres` | Materialien im Editor | hängen an den neuen Shadern |
| `scripts/**` | `Assets/Neonlith/Scripts/Runtime/**` | Abschnitt 5 |
| `user://` (Spielstände, Einstellungen, Absturzspur, Logs) | `Application.persistentDataPath` | gleiche Dateinamen beibehalten (`progress.json`, `progress_2.json`, `settings.cfg` als JSON) |

## 5. Abbildung der Godot-Konzepte

### 5.1 Allgemein

| Godot | Unity |
|---|---|
| Autoload (`Balance`, `Settings`, `SaveGame`, `Meta`, `Audio`, `Assets`, `CrashLog`) | Komponenten unter dem `Bootstrap`-Objekt (`DontDestroyOnLoad`), Zugriff über statische `Instance`; reine Daten (`Balance`) als `static class` |
| `Node3D` mit Skript | `GameObject` + `MonoBehaviour` |
| `RefCounted`-Klassen (Logik: `Run`, `MapData`, `Combat`, `WaveManager`, `Boons`, `SkillTrees`, `Missions`) | gewöhnliche C#-Klassen, keine Unity-Abhängigkeit; so bleiben sie im Edit-Mode-Test simulierbar |
| `Node2D` als Logikträger (`Enemy`, `Tower`, `Weapon` nutzen nur `position`) | C#-Klassen mit `Vector2`; Darstellung getrennt (wie heute `EnemyVisual`, `TowerVisual`) |
| `signal` | C#-`event`/`Action<T>`; `Game` behält seine Ereignisliste (`wave_started`, `game_over`, `boon_offer`, …) |
| `_process(delta)` + fester Simulationstakt (`Game.sim_step`, `set_step_hz`) | `Update` mit eigenem Akkumulator (nicht `FixedUpdate`: die Spielzeit läuft mit Pause und 2x-Tempo, nicht mit der Physik) |
| `create_tween` (2 Stellen) | Coroutinen oder `DOTween` |
| `MultiMeshInstance3D` (21 Stellen: Schwärme, Gras, Laub, Turm-Batches) | `Graphics.RenderMeshInstanced` / `RenderMeshIndirect` mit `MaterialPropertyBlock`; Instanzdaten (Farbe, Phase) als per-instance Buffer |
| `SubViewport` als Tafelbild (19 Stellen) | `RenderTexture` + World-Space-Canvas (uGUI) oder direkt World-Space-Canvas; Strahl über XRI `XRRayInteractor` + `TrackedDeviceGraphicRaycaster` |
| `Label3D` mit MSDF-Schrift (11 Stellen) | TextMeshPro (SDF) mit Oxanium/Bruno Ace als TMP-Font-Assets |
| `OpenXRCompositionLayerQuad/Cylinder` (Ladebild, Kommandotafel, Armband) | Paket XR Composition Layers: `CompositionLayer` Quad/Cylinder mit `TexturesExtension` |
| `RenderingServer.global_shader_parameter_set` (40 Shader lesen `global uniform`) | `Shader.SetGlobalColor/Float/Vector` (`ShaderGlobals.cs`, Namen `_PalMain` statt `pal_main`) |
| `Environment`/`Sky` (`WorldTheme.apply_env`, Nachthimmel) | Skybox-Material mit eigenem Himmelsshader, `RenderSettings.fog` linear, Umgebungslicht als Farbe |
| `Decal` (2 Stellen) | URP Decal Renderer Feature (teuer auf der Quest; prüfen, ob eine Fläche mit Shader reicht) |
| `CPUParticles3D` (1 Stelle), eigene Partikel in `WeatherFx` | Shuriken mit Obergrenze oder weiter als Instanzen |
| `WorkerThreadPool` (Brücke im Arbeitsthread, 4.98.2) | `Task.Run` für die Geometrie, `Mesh.SetVertexBufferData` im Hauptthread |
| `ConfigFile`, `FileAccess`, `JSON` | `System.IO` + Newtonsoft; Beschädigte Dateien wie heute beiseitelegen (`.corrupt`) |
| `AudioServer`-Busse Music/SFX/UI/Ambience | `AudioMixer` mit vier Gruppen; Stimmenpool als `AudioSource`-Pool (16 Effekt-, eigene UI- und Baustimmen wie in `audio.gd`) |
| `XRServer`, `XRInterface` (Headset erkannt, Sitzung läuft) | `XRGeneralSettings`/`OpenXRRuntime`, `XRDisplaySubsystem.running` |
| `trigger_haptic_pulse` | `XRController` (XRI) `SendHapticImpulse` oder Input-System-Haptik |
| `XRHandTracker`/`XRHandModifier` | XR Hands `XRHandSubsystem` |
| Desktop-Simulator ohne Brille (`NEONLITH_XR_SIM`, Maus = rechter Controller) | XR Interaction Simulator aus dem XRI-Paket (Samples) |
| Kommandozeilen-Werkzeuge (`--sim-mission`, `--xr-perf-missions`, `--xr-shot-flimmer`) | Edit-Mode-Tests und Batchmode-Läufe (`-batchmode -executeMethod`) |

### 5.2 Eingabe (`openxr_action_map.tres` → Input System)

Eine Input-Actions-Datei `Neonlith.inputactions` mit zwei Maps (Links, Rechts), Bindings über die
OpenXR-Controller-Layouts des Input Systems:

| Aktion (Godot) | Pfad | Unity-Binding |
|---|---|---|
| `trigger` (Wert) | `input/trigger/value` | `<XRController>{RightHand}/trigger` |
| `grip` (Wert) | `input/squeeze/value` | `…/grip` |
| `primary` (Stick) | `input/thumbstick` | `…/thumbstick` |
| `ax_button`, `by_button` | `input/a/click`, `input/b/click` (rechts), `x`, `y` (links) | `…/primaryButton`, `…/secondaryButton` |
| `menu_button` | `input/menu/click` (links) | `<XRController>{LeftHand}/menu` |
| `aim_pose`, `grip_pose` | `input/aim/pose`, `input/grip/pose` | `…/pointerPosition/Rotation`, `…/devicePosition/Rotation` |
| `haptic` | `output/haptic` | `SendHapticImpulse` |
| Handtracking (`pinch_ext`, `grasp_ext`) | `/interaction_profiles/ext/hand_interaction_ext` | XR Hands, „Hand Interaction Profile“ |

Belegung im Standmodus (aus `docs/STAND.md`): linker Stick gehen (2,2 m/s, kopfrelativ), rechter Stick drehen
(30°-Stufen oder stufenlos), Abzug der Waffenhand feuern, Griff der anderen Hand am Vordergriff, Waffe nach unten
= nachladen, A Bau-Modus, B zurück, X Welle starten, Menü-Taste Pause, Y Tafel vor mich. Linkshänder spiegeln.

### 5.3 Systeme im Einzelnen

**Autoload → Dienste (Etappe 1).** `Balance` wird eine statische Klasse mit denselben Tabellen (Türme, Gegner,
Wellen, Sperren, Eliten, Bosse); sie ist die größte Konstantensammlung und muss zahlengleich bleiben, sonst
stimmt die Simulation aus `KAMPAGNE.md` nicht mehr. `Settings` (JSON statt `settings.cfg`), `SaveGame` (drei
Stände, `validate_run`, `.corrupt`), `Meta` (Kernsplitter, Fertigkeiten, `changed`-Ereignis), `Audio` (Busse,
Pools, Überblendung, Kulisse je Welt), `CrashLog` (`absturz_spur.txt`, erste Zeile LAEUFT/SAUBER; die Fehlerzeilen
kommen aus `Application.logMessageReceived` statt aus Godots Log).

**Spiellogik (Etappe 2).** `Game`, `GameState`, `GameSession`, `Run`, `Campaign`, `Missions`, `MapData`,
`WaveManager`, `Enemy`, `EnemyTraffic`, `Tower`, `GunTower`/`Weapon`, `Projectile`, `Combat`, `Boons`,
`SkillTrees`, `Unlocks`, `Bestiary`, `Arsenal`, `SupplyDrop`, `GunRaider`, `Intros`, `Comms`: alles reine Logik
auf einem Raster (Felder, Felder/s, Spielzeit). 1:1 übersetzen, Felder bleiben `Vector2`/`Vector3` in
Spieleinheiten; die Umrechnung in Meter passiert erst in der Darstellung (heute `ws`, Einheiten je Meter).
`SimBot` und die Missionssimulation (`--sim-mission=1-20`) als Edit-Mode-Test: so lässt sich die Balance
Zahl für Zahl gegen `docs/kampagne_simulation.txt` prüfen, bevor es ein Bild gibt.

**Darstellung (Etappen 3 bis 5).** Grundlage sind die Netzbaukästen `MeshKit`, `WeaponMesh`, `StripMesh`,
`AtlasKit` (Profile extrudieren, Fasen, Facetten, Atlas-UVs); sie werden zuerst nach C# gebracht
(`Mesh.SetVertices/SetIndices`, 16-Bit-Indizes wo möglich). Darauf bauen `KlarBridge` (Deck aus Formen),
`FogBridge` (Unterbau, Band, Umgebung, 1 867 Zeilen), `BridgeKit`/`BridgeKitBuild` (Bausatz aus
`data/levels/strips`), `Terrain3D`, `HorizonRing`, `NightSky`, `WorldGates`/`Hangar` (Halle der Tore),
`TowerModels`/`TowerVisual`/`TowerBatch`, `EnemyShapes`/`EnemyVisual`/`EnemyCrowd`/`EnemySwarm`,
`WeaponModels`/`WeaponVisual`, `RobotModels` (Rook), `Weather`/`WeatherFx`, `HoloField`/`HoloMark`/`TowerHolos`,
`EssenceView`, `DamageNumbers`, `LineFX`, `NeonPulse`, `Look` (Erkennungsfarben). Jede Welt nimmt ihre Farben
nur aus `WorldTheme` (`ShaderGlobals.ApplyWorld`).

**XR und Standmodus (Etappe 4).** `XRMain` (5 666 Zeilen) ist in Godot zugleich Szene, Zustandsautomat,
Tafelverwaltung und Eingabe; in Unity wird sie aufgeteilt: `XRRig` (XR Origin, Kamera, Hände), `XRStand`
(Gehen, Drehen, Plattformgrenze, Blende), `XRRifle` (Waffe an der Zielpose, Vordergriff, Kimme/Korn, Rückstoß,
Magazin), `XRBody`/`XRHandView`/`XRTorso` (Hände, Unterarme, Rumpf), `XRTeleport`, `PlayerLaser`
(Bauen per Strahl), `BuddyDrone` (Gizmo, nur Begleiterin), `XRLoadVeil` (Ladebild als Composition Layer),
`AutoRes`, `ComfortVignette`, `SunGlare`, `BoonCeremony`, `MenuCinematic`.
Tafeln (`XRFloatPanel`, `XRHud`, `XRWristHud`, `XRStatusBoard`, `XRSpawnHud`, `XRTowerPanel`, `XREnemyPanel`,
`XRComms`, `XRTutorialHint`, `XRHoverFrame`): World-Space-Canvas mit TextMeshPro; die Kommandotafel als
Zylinder-Composition-Layer wie heute (Schrift bleibt scharf, Godot 4.9.5).

**Menüs (Etappe 5).** `VRStyle` (Graustufen, Maße in Tafelpixeln, 1 050 px je Meter) wird eine statische
Klasse; die Bausteine `VRButton`, `VRPanel`, `VRSlider`, `VRToggle`, `VRTabBar`, `VRTowerCard`, `VRWorldCard`,
`VRMissionButton`, `VRUpgradeNode`, … werden uGUI-Prefabs mit einem Skript je Baustein; die Screens
(`VRMainMenu`, `VRCampaignMap`, `VRWorldPicker`, `VRSkillWall`, `VRUpgradeHub`, `VRTowerScreen`, `VRBuildMenu`,
`VRLoading`, `VRMenus` als Verteiler) bleiben in der Struktur gleich. Holo-Bausteine (`Holo*`) brauchen die
Holo-Shader aus Etappe 3.

## 6. Shader

Godots Shadersprache lässt sich nicht übersetzen, nur nachbauen. Reihenfolge nach Nutzen: zuerst die wenigen
Shader, die fast alles zeichnen, dann Effekte. Für jeden gilt `KLARBILD.md` (unter `_BtKlar` keine feinen Muster,
keine laufenden Lichter, keine Normalenkarten; Linien nie dünner als gut ein Bildpunkt, in der Ferne zur Tönung
ausblenden: `far_calm.gdshaderinc` wird eine gemeinsame HLSL-Include `FarCalm.hlsl`).

| Gruppe | Godot-Shader | Unity | Etappe |
|---|---|---|---|
| Flächen der Welt | `lowpoly`, `lowpoly_body.inc`, `lowpoly_holo`, `lowpoly_ghost`, `strip`, `strip_body.inc`, `strip_flat*`, `strip_holo`, `world_ground`, `moon_ground`, `horizon_ground`, `slate`, `hull`, `weapon_surface` | ein HLSL-Shader „Neonlith/Surface“ mit Varianten (Facetten, Metalltextur gebürstet, Leuchtkanten aus `_Pal*`, Fernberuhigung) | 3 |
| Himmel | `space_sky`, `night_sky`, `menu_sky`, `menu_stars`, `void_sky`, `fog_sky`, `sky_life`, `cloud_sea`, `cloud_body.inc` | Skybox-Shader mit Sterne/Galaxie-Textur `space_sky_w*`, Tönung und Entsättigung aus `WorldTheme` | 3 |
| Holo-System | `holo`, `holo_core`, `holo_core_glow`, `holo_field`, `holo_floor(.inc)`, `holo_grid.inc`, `holo_hub`, `holo_kit`, `holo_model`, `holo_net`, `holo_preview`, `holo_projection`, `holo_ends.inc`, `holo_red.inc`, `holo_sim.inc` | „Neonlith/Holo“ mit Include `Holo.hlsl` (Raster, Kanten, Puls aus `_Holo*`) | 3 |
| Gegner | `wraith(.inc)`, `wraith_crowd`, `swarm_body`, `obsidian_beast`, `neon_voxel`, `mist`, `blood_puddle` | instanzierte Shader mit per-instance Phase (Laufanimation im Vertex-Shader) | 4 |
| Waffe und Spieler | `gun_sight_beam`, `gun_sight_dot`, `gun_gauge`, `gun_hp_bar`, `kanzel_*`, `scope_lens`, `robot_face`, `charge_glow`, `aim_ring`, `impact_flash`, `impact_mark` | kleine unlit Shader; Rotpunkt als offenes Rohr mit Leuchtpunkt (4.99.65, kein Kamerabild) | 4 |
| Wetter und Umgebung | `weather_rain`, `weather_mist`, `weather_leaves`, `weather_bolt`, `storm_wall`, `frost_patch`, `grass_blades`, `meadow`, `leaf_litter`, `fog_wall`, `fog_tree`, `cryo_chamber`, `gate_glow`, `light_beam`, `lamp`-Leuchten | instanzierte unlit Shader, Partikel begrenzt | 5 |
| Portale, Teleport | `portal_common.inc`, `portal_rim`, `portal_void`, `spawn_portal`, `teleport_arc`, `teleport_marker` | unlit mit Additiv/Transparent, wenig Fläche | 4 |
| Tafeln und UI | `glow`, `glow_card`, `glow_solid`, `boon_card`, `boon_sigil`, `boon_spark`, `bar_icon`, `hp_bar`, `damage_number`, `ground_mark`, `ship_empower` | UI-Shader (uGUI) oder TextMeshPro-Material | 5 |
| Vollbild | `screen_fade`, `comfort_vignette`, `pause_blur`, `sun_glare` | **keine** Nachbearbeitung: Blende und Vignette als Quad vor der Kamera (wie heute), Pausen-Unschärfe entfällt oder wird ein abgedunkeltes Quad | 4 |
| Werkzeuge | `moon_ground_bake` | Editor-Skript oder weglassen (gebackene Textur ist kopiert) | – |

Prüfen mit `tools/dev/shader_check.gd`-Entsprechung: in Unity der Shader-Compiler-Bericht je Variante
(Vulkan, Android) und die Pipeline-Vorwärmung über eine `ShaderVariantCollection`, die beim Ladebild
`WarmUp()` aufruft (Gegenstück zu `XRMain._queue_tower_warm`).

## 7. Etappen

Jede Etappe ist für sich prüfbar; die Reihenfolge folgt dem Risiko: erst Daten und Logik (prüfbar ohne Brille),
dann Bild und Brille. Aufwände sind grobe Schätzungen für eine erfahrene Unity-Entwicklerin oder einen
erfahrenen Unity-Entwickler in Personenwochen (PW); sie dienen der Reihenfolge, nicht als Zusage.

| # | Etappe | Inhalt | Prüfung | Aufwand |
|---|---|---|---|---|
| 0 | Projekt öffnen | Repository im Hub öffnen, Pakete lösen, Projekteinrichtung ausführen, XR/URP von Hand (README.md), Bootstrap-Szene, leere APK auf der Quest starten | APK läuft, Log zeigt „Welt Kernstation, 20 Missionen“, Tests `Neonlith.Tests` grün | 0,5 |
| 1 | Dienste | `Balance`, `Settings`, `SaveGame`, `Meta`, `Audio`, `CrashLog` nach C# | Spielstand laden/speichern, drei Stände, `.corrupt`; Audio-Busse hörbar | 1,5 |
| 2 | Spiellogik und Simulation | `scripts/game` nach C#; `SimBot` + Missionssimulation als Edit-Mode-Test | Simulation 1 bis 20 liefert dieselben Werte wie `docs/kampagne_simulation.txt` (Toleranz festlegen) | 4 |
| 3 | Welt sichtbar | `MeshKit`/`WeaponMesh`/`StripMesh`, `KlarBridge`, `FogBridge`, `BridgeKit`, `Terrain`, Himmel, Surface- und Holo-Shader, `WorldTheme`-Farben, Ladebild als Composition Layer | Mission 1 steht in der Brille, Flimmertest wie `--xr-shot-flimmer`, Zeichenaufrufe im Rahmen von `PERFORMANCE.md` | 5 |
| 4 | Standmodus und Kampf | XR Rig, `XRStand`, `XRRifle` (Pistole/Sturmgewehr/Sniper laut `STAND.md`), Hände, Türme und Gegner sichtbar (Instanzen), Treffer, Essenz, HUD, Funk, Buddy-Drohne, Tutorial-Hinweise | Mission 1 bis 5 spielbar, 72/90 Hz ohne Ruckler nach Vorwärmung (`LADEN_UND_LEISTUNG.md`) | 6 |
| 5 | Hub und Menüs | Halle der Tore, Weltkarte, Missionskacheln mit Startabfrage, Fertigkeitswand, Waffe wählen, Einstellungen, Pause, Segenskarten-Zeremonie, Wetter | Durchlauf aller 20 Missionen (`--xr-perf-missions`-Entsprechung), Spielanleitung stimmt | 5 |
| 6 | Feinschliff Quest | AutoRes, Foveation, Pipeline-Vorwärmung je Mission, Haptik, Komfort, Absturzbericht, Store-Build (AAB) | Brillentest nach `PRUEFBERICHT.md`, Batterie und Wärme über eine Mission | 2 |

Zusammen grob 24 Personenwochen. Etappe 0 bis 2 brauchen keine Brille.

## 7a. Stand der Parität (Ende der ersten Sitzung)

Portiert (Logik und Ablauf, Darstellung aus einfachen Formen): Balance, Kampf, Gegner mit allen Fähigkeiten, Eliten
und Bossen, Türme, Sperren, Bauen per Strahl, Arsenal mit Ladeschuss und Vordergriff, Nachschub und Flugaliens,
Segenskarten, Fertigkeitsbäume mit Kernsplittern, Spielstand und Sterne, Tutorial, Funk, Drohne Gizmo, Wetter,
Zonen, Halle der Tore mit Missionswahl, Pausenmenü und Einstellungen, Ladeschleier, Schadenszahlen, Gegner-Info,
Foveation, 90 Hz, AutoRes, ein eigener Neon-Shader mit Fernberuhigung.

Noch offen: die reichen Modelle aus Code (TowerModels, WeaponModels, RobotModels, EnemyShapes, Rook als Figur),
die übrigen Shader (Surface mit Facetten und Metall, Holo-System, Himmel, Gegner-Laufanimation), die volle Brücke
(BridgeKit-Varianten, Unterbau, Band), Statustafel und Armband, Kombo und Zeitlupe, Endlosmodus und Feldzug,
Absturzbericht, Composition Layers, Vorwärmen der Shader. Alles ist ohne Unity-Editor geschrieben und muss im
Cloud-Build (GameCI) und in der Brille geprüft werden.

## 8. Was nicht kopierbar ist (und was stattdessen)

- **Netze aus Code (36 Skripte).** Türme, Waffen, Rook, Brücken, Terrain, Schwärme entstehen zur Laufzeit. Das
  bleibt so: die Baukästen werden nach C# übersetzt, die Generatoren darauf. Für Zwischenstände exportiert
  `Tools/unity_export.gd` fertige Netze als GLB (Gegner-`.mesh`, Pistole und Sniper); diese GLB sind
  Platzhalter ohne die Shader-Animationen.
- **Godot-Ressourcen** (`.tres`, `.res`, `.mesh`, `.import`, `.uid`): `data/*.tres` sind als JSON übernommen,
  `assets/materials/*.tres` entstehen neu an den neuen Shadern, `.mesh` über den Export.
- **Shader** (Abschnitt 6).
- **MSDF-Schriften** (`Assets._oxanium`): TextMeshPro erzeugt eigene SDF-Font-Assets aus den TTFs (Oxanium
  variabel in 400/500/600/700, Bruno Ace, Inter als Rückfall für Pfeile und Sterne).
- **OpenXR-Ebenen** über das Paket XR Composition Layers; Verhalten wie in `XRMain` (Zylinder für die
  Kommandotafel, Quad für Ladebild und Armband, Ebene erst nach laufender Sitzung anhängen, 10 Versuche).
- **Foveation über VRS** ist in Unity ein OpenXR-Feature des Meta-Pakets, kein Projektflag.
- **Godot-Testläufer** (`test_runner.gd`, `xr_test_runner.gd`, 13 000 Zeilen): nicht übersetzen, sondern die
  Prüfungen als Unity-Tests neu aufsetzen, zuerst die Simulation (Etappe 2), dann je Etappe die passenden.
- **Werkzeuge in `tools/`** (Python für Texturen, Audio, Missionen; Godot-Skripte für Vorschau, Flimmern,
  Shader-Prüfung) bleiben beim Godot-Projekt; der Unity-Ordner wird daraus befüllt (Abschnitt 9).

## 9. Werkzeuge

Beide Repositories liegen nebeneinander (`neonlith`, `neonlith-unity`):

```
# in neonlith-unity
python3 -I Tools/unity_copy_assets.py ../neonlith                       # Assets, Daten, .tres→JSON, ASSET_MANIFEST.md
# in neonlith (Godot)
godot --headless --path . -s ../neonlith-unity/Tools/unity_export.gd -- --out=../neonlith-unity   # .mesh und Generator-Modelle als GLB
```

Beide laufen idempotent und überschreiben nur ihre Zielordner. Unity-eigene Dateien (Skripte, Szenen, Shader,
`.meta`) bleiben unberührt. Richtung ist immer Godot → Unity: eine Änderung an Daten oder Assets wird in Godot
gemacht und hierher kopiert, nie umgekehrt. Die Kopie macht rund 190 MB aus; wer das Repository schlank halten
will, legt `Assets/Neonlith/Art`, `Audio` und `UI` in Git LFS.

## 10. Prüffragen vor jeder Etappe (aus `SILENCE_PROTOCOL.md`, Abschnitt 10)

Sie gelten in Unity unverändert: Farben nur aus `WorldTheme`, Himmel nur Sterne/Galaxie/Nebel, gleiche Funktion =
gleiche Farbe, Interface in Graustufen (`VRStyle`), Rot nur für Gegner und Gefahr, nichts Feines in der Ferne,
nichts, was in der Brille flimmert oder läuft, wenige Zeichenaufrufe, keine Volumeneffekte, keine Nachbearbeitung.

## 11. Risiken

- **Zahlengleiche Balance.** Die Tabellen in `balance.gd` (577 Zeilen) und die Wellenableitung in `Run`,
  `Campaign`, `Missions` müssen exakt übernommen werden; die Simulation in Etappe 2 ist die Absicherung.
- **Leistung.** Godot erreicht 90 Hz mit wenigen Zeichenaufrufen und Instanzen; URP bringt eigene Kosten (SRP
  Batcher nutzen, keine Depth/Opaque Texture, Foveation früh einschalten und messen, nicht erst in Etappe 6).
- **Flimmern.** Alle Fernberuhigungen stecken in den Shadern; ohne `FarCalm`-Include von Anfang an werden die
  Flimmertests in Etappe 3 nicht bestehen.
- **Composition Layers** hängen am Meta-OpenXR-Runtime-Verhalten; früh auf der Quest prüfen (Etappe 3).
- **GLB-Import.** glTFast importiert Materialien als URP-Lit; die Godot-Modelle (Türme, Straße) brauchen danach die
  eigenen Surface-Materialien, sonst sehen sie nach Standard-PBR aus.
- **Ungetestete Teile dieses Pakets.** Die C#-Dateien sind ohne Unity-Editor geschrieben und nicht kompiliert;
  `unity_export.gd` ist ohne Godot nicht gelaufen. Etappe 0 fängt beides auf.
