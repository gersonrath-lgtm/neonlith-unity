using Neonlith.Data;
using UnityEngine;

namespace Neonlith
{
    /// <summary>
    /// Einstieg des Unity-Ports: Gegenstück zu <c>scenes/main_xr.tscn</c> + <c>XRMain._ready</c> in Godot,
    /// hier vorerst nur Palette, Weltfarben und ein Datencheck. Auf ein leeres GameObject in der Szene
    /// <c>Bootstrap</c> legen. Die Autoloads aus Godot (CrashLog, Balance, Assets, Settings, SaveGame, Meta,
    /// Audio) werden beim Port als eigene Komponenten unter diesem Objekt angelegt (Docs/UNITY_PORT.md, Etappe 1).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class Bootstrap : MonoBehaviour
    {
        [Tooltip("Welt 0..3 beim Start (Standard 1 = Kernstation, wie in Godot WorldTheme.index)")]
        [Range(0, 3)] public int world = 1;

        [Tooltip("Mission 1..20, mit der die Weltfarbe bestimmt wird (0 = Feld 'world' benutzen)")]
        [Range(0, 20)] public int mission = 0;

        static Bootstrap _instance;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            ShaderGlobals.ApplyDefaults();
            WorldTheme.Index = mission > 0 ? WorldTheme.WorldOfMission(mission) : world;
            ShaderGlobals.ApplyWorld(WorldTheme.Current);

            var count = GameData.MissionCount;
            Debug.Log($"Neonlith: Welt {WorldTheme.Current.name} ({WorldTheme.Current.id}), {count} Missionen in campaign.json");
        }

        /// <summary>Welt wechseln (Hub, Missionsstart). Setzt Palette und Nebel neu.</summary>
        public static void SetWorld(int index)
        {
            WorldTheme.Index = index;
            ShaderGlobals.ApplyWorld(WorldTheme.Current);
        }
    }
}
