using Neonlith.Data;
using UnityEngine;

namespace Neonlith.View
{
    /// <summary>
    /// Erster Brückengenerator des Ports (stark vereinfachtes Gegenstück zu KlarBridge, FogBridge, BridgeKit):
    /// Läufe als Platten mit Dämmen und Leuchtlinien, Knotenplatten an inneren Wegpunkten, Kernplatte mit Kernsäule
    /// und Kern, Standdeck als Achteck auf Pylonen über dem Kern, Zulauf mit rotem Ring am Wegbeginn.
    /// Alles aus <see cref="MeshKit"/>, vier Zeichenaufrufe je Welt (Metall, Dämme, Neon, Kern).
    /// </summary>
    public static class BridgeBuilder
    {
        const float F = Balance.FieldM;
        const float DeckDepth = 0.75f * F;             // FogBridge.DECK_DEPTH
        const float BermH = MissionData.BermTop * F;   // Dammkrone über der Fahrbahn
        const float LineW = 0.12f * F;                 // KlarBridge.LINE_W
        const float CoreW = 0.045f * F;                // KlarBridge.CORE_W (heller Kern der Linie)
        const float PlateHalf = 2.5f * F;              // Knotenplatte 5 × 5 Felder

        public static GameObject Build(MissionData m, WorldTheme theme)
        {
            var root = new GameObject("Bruecke");
            var metal = new MeshKit();
            var berm = new MeshKit();
            var neon = new MeshKit();
            var neonCore = new MeshKit();
            var core = new MeshKit();

            foreach (var cells in m.pathCells)
            {
                // Zulauf: der erste Lauf beginnt vor dem Eingang im Nebel
                var first = MissionData.ToM(cells[0]);
                var d0 = MissionData.Dir(cells[0], cells[1]);
                var lead = MissionData.ToM(cells[0] - d0 * MissionData.EntryLead);
                Run(metal, berm, neon, neonCore, lead, first);
                for (var i = 0; i + 1 < cells.Length; i++)
                {
                    var a = MissionData.ToM(cells[i]);
                    var b = MissionData.ToM(cells[i + 1]);
                    if (MissionData.Dir(cells[i], cells[i + 1]) == Vector3.zero)
                    {
                        // Wand (senkrechter Abschnitt, 4.93): Block zwischen den Höhen, die Gegner laufen daran hoch
                        var lo = Mathf.Min(a.y, b.y); var hi = Mathf.Max(a.y, b.y);
                        var dir = i + 2 < cells.Length ? MissionData.Dir(cells[i + 1], cells[i + 2]) : MissionData.Dir(cells[i - 1], cells[i]);
                        var c = new Vector3(a.x, (lo + hi) * 0.5f, a.z) + dir * (0.5f * F);
                        metal.Box(c, new Vector3(MissionData.DeckWidth * F, hi - lo + DeckDepth, 1f * F));
                        continue;
                    }
                    Run(metal, berm, neon, neonCore, a, b);
                }
                // Knotenplatten an inneren Punkten
                for (var i = 1; i + 1 < cells.Length; i++)
                {
                    var c = MissionData.ToM(cells[i]);
                    if (MissionData.Dir(cells[i - 1], cells[i]) == Vector3.zero || MissionData.Dir(cells[i], cells[i + 1]) == Vector3.zero) continue;
                    metal.Box(c + Vector3.down * (DeckDepth * 0.5f) + Vector3.up * 0.01f, new Vector3(PlateHalf * 2f, DeckDepth, PlateHalf * 2f));
                    Outline(neon, c + Vector3.up * 0.01f, PlateHalf, LineW);
                }
                // Roter Ring am Wegbeginn (SpawnPortal)
                neonCore.Ring(lead + Vector3.up * 0.02f, 1.0f * F, 1.25f * F, 24);
            }

            // Zonen (KlarBridge: Beschleunigungsstreifen energierot, Verlangsamungsfelder gedämpft, Engstelle mit Seitenblöcken)
            var zoneFast = new MeshKit(); var zoneSlow = new MeshKit();
            foreach (var z in m.zones)
            {
                var along = z.b - z.a; along.y = 0f;
                if (along.sqrMagnitude < 1e-4f) continue;
                var n = Mathf.Max(1, Mathf.RoundToInt(along.magnitude / (0.5f * F)));
                for (var i = 0; i < n; i++)
                {
                    var p0 = Vector3.Lerp(z.a, z.b, i / (float)n);
                    var p1 = Vector3.Lerp(z.a, z.b, (i + 0.5f) / n);
                    if (z.kind == "narrow")
                    {
                        metal.Slab(p0, p1, 0.6f * F, 0.9f * F, MissionData.RoadWidth * F * 0.5f - 0.3f * F, 0.9f * F);
                        metal.Slab(p0, p1, 0.6f * F, 0.9f * F, -(MissionData.RoadWidth * F * 0.5f - 0.3f * F), 0.9f * F);
                    }
                    else (z.kind == "fast" ? zoneFast : zoneSlow).Strip(p0, p1, 0.14f * F * MissionData.RoadWidth, 0f, 0.008f);
                }
            }
            zoneFast.Spawn("ZoneSchnell", NeonMaterials.Neon("zone_fast", new Color(0.75f, 0.02f, 0.012f).gamma), root.transform, false);
            zoneSlow.Spawn("ZoneLangsam", NeonMaterials.Neon("zone_slow", new Color(0.22f, 0.25f, 0.3f).gamma), root.transform, false);

            // Kernplatte (BridgeLevel.BASTION_HALF: 5 × 5 Felder) und Kernsäule darunter
            var k = m.core;
            metal.Box(k + Vector3.down * (DeckDepth * 0.5f) + Vector3.up * 0.015f, new Vector3(PlateHalf * 2f, DeckDepth, PlateHalf * 2f));
            Outline(neon, k + Vector3.up * 0.02f, PlateHalf, LineW);
            metal.Box(k + Vector3.down * (DeckDepth + 12f), new Vector3(4.8f * 0.5f * F, 24f, 4.8f * 0.5f * F));
            core.Prism(k + Vector3.down * (DeckDepth + 0.3f * F), 0.9f * F, 0.3f * F, 8);

            // Standdeck (GunTower): Sockel, vier Pylone, Achteckdeck, Leuchtband am Rand
            var top = m.deckTop;
            var deckT = 0.18f * F;
            metal.Box(k + Vector3.up * (0.35f * F * 0.5f), new Vector3(1.15f * 2f * F, 0.35f * F, 1.15f * 2f * F));
            for (var i = 0; i < 4; i++)
            {
                var a = Mathf.PI * 0.25f + Mathf.PI * 0.5f * i;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (1.55f * F);
                var h = top.y - deckT - k.y;
                metal.Box(k + p + Vector3.up * (h * 0.5f), new Vector3(0.22f * F, h, 0.22f * F));
            }
            metal.Prism(top + Vector3.down * deckT, MissionData.PlatformR * F, deckT, 8, Mathf.PI / 8f);
            var rOut = MissionData.PlatformR * F * Mathf.Cos(Mathf.PI / 8f);
            neon.Ring(top + Vector3.up * 0.004f, rOut - 0.3f * F, rOut - 0.3f * F + 0.1f * F, 8, Mathf.PI / 8f);

            metal.Spawn("Metall", NeonMaterials.Lit("ground", theme.ground), root.transform, true);
            berm.Spawn("Daemme", NeonMaterials.Lit("ground2", theme.ground2, 0.5f, 0.35f), root.transform, true);
            neon.Spawn("Neon", NeonMaterials.Neon("neon", theme.neon), root.transform, false);
            neonCore.Spawn("NeonKern", NeonMaterials.Neon("hi", theme.hi), root.transform, false);
            core.Spawn("Kern", NeonMaterials.Glow("core", WorldTheme.Threat, WorldTheme.Threat, 2.5f), root.transform, false);
            return root;
        }

        /// <summary>Ein Lauf: Fahrbahnplatte, beidseitig Damm mit Außenlinie, Linien an den Fahrbahnkanten.</summary>
        static void Run(MeshKit metal, MeshKit berm, MeshKit neon, MeshKit neonCore, Vector3 a, Vector3 b)
        {
            var w = MissionData.DeckWidth * F;
            var road = MissionData.RoadWidth * F;
            metal.Slab(a, b, road, DeckDepth);
            var bermW = (w - road) * 0.5f;
            var off = road * 0.5f + bermW * 0.5f;
            berm.Slab(a, b, bermW, DeckDepth + BermH, off, BermH);
            berm.Slab(a, b, bermW, DeckDepth + BermH, -off, BermH);
            // Leuchtlinien: Außenkante der Dämme (gedämpft) mit hellem Kern, Fahrbahnkanten
            var outer = w * 0.5f - LineW * 0.5f - 0.02f;
            neon.Strip(a, b, LineW, outer, BermH + 0.004f);
            neon.Strip(a, b, LineW, -outer, BermH + 0.004f);
            neonCore.Strip(a, b, CoreW, outer, BermH + 0.006f);
            neonCore.Strip(a, b, CoreW, -outer, BermH + 0.006f);
            neon.Strip(a, b, LineW * 0.7f, road * 0.5f - LineW * 0.35f);
            neon.Strip(a, b, LineW * 0.7f, -(road * 0.5f - LineW * 0.35f));
        }

        static void Outline(MeshKit neon, Vector3 c, float half, float w)
        {
            var o = half - w * 0.5f;
            neon.Strip(c + new Vector3(-o, 0, -half), c + new Vector3(-o, 0, half), w);
            neon.Strip(c + new Vector3(o, 0, -half), c + new Vector3(o, 0, half), w);
            neon.Strip(c + new Vector3(-half, 0, -o), c + new Vector3(half, 0, -o), w);
            neon.Strip(c + new Vector3(-half, 0, o), c + new Vector3(half, 0, o), w);
        }
    }
}
