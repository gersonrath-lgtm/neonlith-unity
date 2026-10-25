# Shader

Hier entstehen die URP-Shader des Ports. Die Godot-Shader (`assets/shaders/*.gdshader`, 88 Stück, 12 Includes)
lassen sich nicht kopieren: Godots Shadersprache ist eine eigene; in Unity werden sie als Shader Graph oder
HLSL (`.shader`) neu geschrieben. Reihenfolge und Zuordnung je Shader: `Docs/UNITY_PORT.md`, Abschnitt „Shader“.

Regeln aus `CLAUDE.md` und `docs/KLARBILD.md` gelten weiter:

- Linien und Muster nie dünner als gut einen Bildpunkt, in der Ferne zur Tönung ausblenden.
- Unter Klarbild (`_BtKlar` = 1, Standard) keine feinen Muster, keine laufenden oder blinkenden Lichter,
  keine Normalenkarten. Erlaubt: das gemeinsame Aufleuchten `_NeonPulse`.
- Keine Shader, die Bild oder Tiefe vom Schirm lesen (Opaque/Depth Texture): sonst fällt das Subsampling der
  Foveation weg. Keine Nachbearbeitung.
- Globale Werte (`_Pal*`, `_Bt*`, `_Wx*`, `_Holo*`, `_NeonPulse`, `_SceneDim`) setzt `ShaderGlobals.cs`; im
  Shader Graph als Property mit genau diesem Reference-Namen und „Exposed“ aus.
- 3D-Texturen immer mit Mipmaps.
