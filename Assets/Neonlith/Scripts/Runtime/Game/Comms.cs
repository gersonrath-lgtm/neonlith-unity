using System.Collections.Generic;
using Neonlith.Data;
using UnityEngine;

namespace Neonlith.Game
{
    /// <summary>
    /// Funkmeldungen (scripts/game/comms.gd): in der Kampagne spricht das Protokoll selbst (PROTOCOL_EVENTS, knapp
    /// und sachlich). Erste Welle, Ansage jeder weiteren Welle mit Zusammensetzung, letzte Welle, Welle geschafft,
    /// Zwischenfunk in der Bauphase, Zwischenstand in der Welle, Durchbruch, kritischer Zustand, Anomalie, Ende.
    /// </summary>
    public static class Comms
    {
        public const string Speaker = "SILENCE PROTOCOL";
        static readonly Dictionary<string, string[]> Protocol = new Dictionary<string, string[]>
        {
            ["first_wave"] = new[] { "Kontakt. Feindsignaturen auf der Brücke. Feuer frei.", "Erste Welle in Bewegung. Die Verteidigung ist aktiv." },
            ["wave_cleared"] = new[] { "Welle abgewehrt. Zeit für den Ausbau.", "Keine Signaturen mehr auf der Brücke. Die nächste Welle formiert sich.",
                "Sektor ruhig. Das Protokoll empfiehlt Ausbau.", "Welle neutralisiert. Integrität der Anlage unverändert.", "Brücke frei. Essenz gutgeschrieben.",
                "Keine Bedrohung mehr erfasst. Das Protokoll notiert: effizient.", "Welle abgewehrt. Nächste Formation im Nebel erkannt.",
                "Sektor gehalten. Das Protokoll dankt.", "Alle Ziele erloschen. Ausbau empfohlen." },
            ["wave_start"] = new[] { "Welle {0} von {1} erfasst. Verteidigung aktiv.", "Kontakt. Welle {0} von {1} auf der Brücke.",
                "Welle {0} von {1}. Das Protokoll überwacht.", "Signaturen am Rand. Welle {0} von {1}." },
            ["last_wave"] = new[] { "Letzte Welle erfasst. Alle Systeme auf Abwehr.", "Finale Formation. Danach ist der Sektor gesichert." },
            ["idle"] = new[] { "Protokoll: Kern stabil. Energie nominal.", "Protokoll: Türme bereit. Keine Bedrohung in Reichweite.",
                "Diagnose läuft. Alle Leuchtlinien antworten.", "Protokoll: Bewegung im Nebel, Entfernung groß.",
                "Hinweis: Ausbau an Kurven erhöht die Wirkung.", "Protokoll: Standplatz gesichert. Gewehr bereit.",
                "Systemzeit synchronisiert. Das Protokoll wacht.", "Protokoll: Essenz verfügbar. Ausbau möglich.",
                "Keine Störung. Die Anlage hält.", "Protokoll: Verbindung stabil. Du bist nicht allein." },
            ["wave_mid"] = new[] { "Noch {0} Ziele auf der Brücke.", "{0} Signaturen aktiv. Abwehr wirkt.", "Protokoll zählt {0}. Die Hälfte ist neutralisiert." },
            ["first_leak"] = new[] { "Durchbruch. Die Anlage hat Schaden genommen." },
            ["critical"] = new[] { "Kritischer Zustand. Integrität unter dreißig Prozent." },
            ["boss"] = new[] { "Anomalie erkannt. Alle Systeme auf dieses Ziel." },
            ["defeat"] = new[] { "Verbindung zur Anlage verloren. Das Protokoll startet neu." },
        };

        public static string Line(string ev)
        {
            if (!Protocol.TryGetValue(ev, out var o) || o.Length == 0) return "";
            return o[Random.Range(0, o.Length)];
        }

        /// <summary>„Nächste Welle: 30× Schwärmer, 5× Lauerer.“ Bosswellen nennen den Wächter.</summary>
        public static string WaveSummary(MissionData m, int idx)
        {
            if (idx < 0 || idx >= m.waves.Count) return "";
            var w = m.waves[idx];
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            var bossName = "";
            foreach (var g in w.groups)
            {
                var t = Balance.Enemy(g.type);
                if (t.boss) { bossName = t.name; continue; }
                var label = t.name;
                if (!string.IsNullOrEmpty(g.elite) && Balance.Elites.TryGetValue(g.elite, out var el)) label = el.name;
                if (!counts.ContainsKey(label)) { counts[label] = 0; order.Add(label); }
                counts[label] += g.count;
            }
            var parts = new List<string>();
            foreach (var l in order) parts.Add($"{counts[l]}× {l}");
            if (bossName.Length > 0) parts.Insert(0, "Wächter " + bossName);
            if (parts.Count == 0) return "";
            return (w.boss ? "Endkampf: {0}." : "Nächste Welle: {0}.").Replace("{0}", string.Join(", ", parts));
        }

        public static string ClearedText(MissionData m, int idx)
        {
            var s = Line("wave_cleared");
            var nxt = WaveSummary(m, idx + 1);
            return nxt.Length == 0 ? s : s + " " + nxt;
        }

        public static string StartText(MissionData m, int idx)
        {
            var total = m.waves.Count;
            if (idx <= 0) return Line("first_wave");
            if (m.waves[idx].boss) return "";
            if (idx >= total - 1) return Line("last_wave");
            return string.Format(Line("wave_start"), idx + 1, total);
        }

        public static string MidText(int enemies) => string.Format(Line("wave_mid"), enemies);
    }
}
