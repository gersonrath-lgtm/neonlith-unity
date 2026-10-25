# Shader

Erster eigener URP-Shader des Ports:

- `NeonlithNeon.shader` („Neonlith/Neon“): unbeleuchtete Leuchtlinien und Leuchtflächen mit **Fernberuhigung**
  (Gegenstück zu `far_calm.gdshaderinc`): ab `_FarStart` blendet die Farbe zur Nebelfarbe aus, bei `_FarEnd` ist sie
  ganz getönt. `_NeonPulse` (ShaderGlobals) hebt die Helligkeit bei großen Momenten an. Ein Durchgang, keine Texturen,
  kein Lesen von Bild oder Tiefe, Instancing und Stereo (Multiview) vorbereitet.
  `NeonMaterials.Neon` nutzt ihn, sobald `Resources/Materials/Neon.mat` auf diesen Shader zeigt (Menü „Neonlith →
  2 Materialien und Himmel“ setzt ihn, wenn der Shader übersetzt).

Die übrigen Godot-Shader (`assets/shaders/*.gdshader`, 88 Stück, 12 Includes) lassen sich nicht kopieren: Godots
Shadersprache ist eine eigene; in Unity werden sie als Shader Graph oder HLSL neu geschrieben. Reihenfolge und
Zuordnung je Shader: `Docs/UNITY_PORT.md`, Abschnitt „Shader“.

Regeln aus `CLAUDE.md` und `docs/KLARBILD.md` des Godot-Projekts gelten weiter:

- Linien und Muster nie dünner als gut einen Bildpunkt, in der Ferne zur Tönung ausblenden.
- Unter Klarbild (`_BtKlar` = 1, Standard) keine feinen Muster, keine laufenden oder blinkenden Lichter,
  keine Normalenkarten. Erlaubt: das gemeinsame Aufleuchten `_NeonPulse`.
- Keine Shader, die Bild oder Tiefe vom Schirm lesen (Opaque/Depth Texture): sonst fällt das Subsampling der
  Foveation weg. Keine Nachbearbeitung.
- Globale Werte (`_Pal*`, `_Bt*`, `_Wx*`, `_Holo*`, `_NeonPulse`, `_SceneDim`) setzt `ShaderGlobals.cs`; im
  Shader als Variable außerhalb des Material-CBUFFER, im Shader Graph als Property mit genau diesem Reference-Namen
  und „Exposed“ aus.
- 3D-Texturen immer mit Mipmaps.
