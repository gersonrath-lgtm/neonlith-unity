using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Neonlith.Data
{
    /// <summary>
    /// Dauerhafter Fortschritt (SaveGame + Meta in Godot, erster Stand): Missionsrekorde mit Sieg, Sternen
    /// (Missions.stars: nach verlorenen Leben) und Kernsplittern, Freischaltung der nächsten Mission, gewählte
    /// Waffe, Linkshänder. Datei <c>progress.json</c> in <see cref="Application.persistentDataPath"/> (Godot: user://).
    /// Beschädigte Dateien werden beiseitegelegt (.corrupt) und durch Standardwerte ersetzt.
    /// </summary>
    public static class Progress
    {
        static JObject _data;
        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, "progress.json");

        public static JObject Data
        {
            get
            {
                if (_data != null) return _data;
                try
                {
                    if (File.Exists(Path)) _data = JObject.Parse(File.ReadAllText(Path));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Neonlith: Spielstand beschädigt, beiseitegelegt: " + ex.Message);
                    try { File.Move(Path, Path + ".corrupt"); } catch { }
                }
                if (_data == null) _data = Defaults();
                return _data;
            }
        }

        static JObject Defaults() => new JObject
        {
            ["version"] = 1,
            ["missions"] = new JObject(),
            ["shards"] = 0,
            ["weapon"] = "pistol",
            ["left_handed"] = false,
        };

        public static void Save()
        {
            try { File.WriteAllText(Path, Data.ToString()); }
            catch (Exception ex) { Debug.LogError("Neonlith: Speichern fehlgeschlagen: " + ex.Message); }
        }

        /// <summary>Sterne nach verlorenen Leben (Missions.stars): 3 bei höchstens 10 %, 2 bis 40 %, sonst 1.</summary>
        public static int Stars(int livesLost, int startLives)
        {
            var k = startLives > 0 ? livesLost / (float)startLives : 1f;
            return k <= 0.10f ? 3 : (k <= 0.40f ? 2 : 1);
        }

        public static void RecordMission(int n, bool won, int livesLost, int startLives)
        {
            var m = Data["missions"] as JObject ?? (JObject)(Data["missions"] = new JObject());
            var rec = m[n.ToString()] as JObject ?? new JObject { ["won"] = false, ["stars"] = 0, ["tries"] = 0 };
            rec["tries"] = (int)rec["tries"] + 1;
            if (won)
            {
                var first = !(bool)rec["won"];
                rec["won"] = true;
                rec["stars"] = Math.Max((int)rec["stars"], Stars(livesLost, startLives));
                Data["shards"] = (int)Data["shards"] + (first ? 30 : 10);
            }
            m[n.ToString()] = rec;
            Save();
        }

        public static bool Won(int n) => (Data["missions"] as JObject)?[n.ToString()]?["won"]?.Value<bool>() ?? false;
        public static int StarsOf(int n) => (Data["missions"] as JObject)?[n.ToString()]?["stars"]?.Value<int>() ?? 0;
        /// <summary>Mission 1 ist immer offen, jede weitere nach dem Sieg der vorigen (Missions.unlocked).</summary>
        public static bool Unlocked(int n) => n <= 1 || Won(n - 1);
        public static int NextOpen() { for (var n = 1; n <= 20; n++) if (!Won(n)) return n; return 20; }
        /// <summary>Welten, deren letzte Mission gewonnen ist (für die Waffenfreischaltung: Mission 5, 10, 15).</summary>
        public static int WorldsDone() { var w = 0; for (var n = 5; n <= 20; n += 5) if (Won(n)) w++; return w; }

        public static string Weapon { get => (string)Data["weapon"] ?? "pistol"; set { Data["weapon"] = value; Save(); } }
        public static bool LeftHanded { get => Data["left_handed"]?.Value<bool>() ?? false; set { Data["left_handed"] = value; Save(); } }
        public static int Shards => Data["shards"]?.Value<int>() ?? 0;
    }
}
