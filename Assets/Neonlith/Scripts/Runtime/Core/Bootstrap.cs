using Neonlith.Data;
using Neonlith.Game;
using Neonlith.UI;
using Neonlith.View;
using Neonlith.XR;
using UnityEngine;

namespace Neonlith
{
    /// <summary>
    /// Einstieg des Unity-Ports: Gegenstück zu <c>scenes/main_xr.tscn</c> + <c>XRMain</c> in Godot, auf den ersten
    /// spielbaren Stand reduziert. Baut aus einer leeren Szene alles im Code auf: Palette und Weltfarben, Himmel und
    /// Licht, Brücke der Mission, Spieler in der Brille auf dem Standdeck, Waffe in der Hand, Wellen und HUD.
    /// Auf ein leeres GameObject in der Szene <c>Bootstrap</c> legen (Menü „Neonlith → Szene erzeugen“).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class Bootstrap : MonoBehaviour
    {
        [Tooltip("0 = Halle der Tore, 1..20 = Mission (campaign.json)")]
        [Range(0, 20)] public int mission = 0;

        [Tooltip("Waffe: pistol, rifle, sniper (Arsenal); auto = gespeicherte Wahl")]
        public string weapon = "auto";

        [Tooltip("Linkshänder: Waffe links, Gehen mit dem rechten Stick")]
        public bool leftHanded = false;

        public static Bootstrap Instance { get; private set; }
        /// <summary>Ziel des nächsten Szenenstarts: -1 = Feld der Szene, 0 = Halle, 1..20 = Mission.</summary>
        public static int nextMission = -1;
        public MissionData Mission { get; private set; }
        public GameSession Session { get; private set; }
        public XRRig Rig { get; private set; }

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = -1;
            NeonMaterials.Clear();
            GameData.ClearCache();
            ShaderGlobals.ApplyDefaults();

            if (nextMission >= 0) { mission = nextMission; nextMission = -1; }
            XRInput.Init();
            XRInput.LeftHanded = leftHanded || Progress.LeftHanded;
            if (mission <= 0) { StartHub(); return; }
            Mission = MissionData.Load(mission);
            if (Mission == null) return;
            WorldTheme.Index = WorldTheme.WorldOfMission(mission);
            var theme = WorldTheme.Current;
            ShaderGlobals.ApplyWorld(theme);
            QuestRuntime.Create();

            var world = new GameObject("Welt").transform;
            var sun = SkyBuilder.Apply(theme, world);
            BridgeBuilder.Build(Mission, theme).transform.SetParent(world, false);

            Session = GameSession.Create(Mission);
            var lookAt = Mission.paths.Count > 0 ? Mission.paths[0][0] : Mission.core + Vector3.forward * 20f;
            Rig = XRRig.Create(Mission.deckTop, Mission.StandRadiusM, lookAt);
            SkyBuilder.SetupCamera(Rig.head, theme);
            var armId = string.IsNullOrEmpty(weapon) || weapon == "auto" ? Progress.Weapon : weapon;
            var arm = Balance.Arms.TryGetValue(armId, out var a) ? a : Balance.Arms["pistol"];
            if (arm.unlockWorld > Progress.WorldsDone()) arm = Balance.Arms["pistol"];     // gesperrte Wahl fällt auf die Pistole zurück
            var pw = PlayerWeapon.Attach(Rig.WeaponHand, Rig.OtherHand, arm, Session, theme);
            BuildController.Create(Session, Mission, Rig.OtherHand, Rig.head.transform, theme);
            PauseMenu.Create(Session, Rig.head.transform);
            BoonCeremony.Create(Session, Rig.head.transform);
            BuddyDrone.Create(Rig.head.transform, Session, theme);
            WeatherFx.Create(Mission.weather, Rig.head.transform, sun, theme);
            XRHandVisual.Create(Rig.leftHand, Rig.head.transform, true, XRInput.LeftHanded, theme);
            XRHandVisual.Create(Rig.rightHand, Rig.head.transform, false, !XRInput.LeftHanded, theme);
            Hud.Create(Session, pw, Rig.head.transform);
            LoadVeil.Create(Rig.head.transform);

            Debug.Log($"Neonlith: Mission {mission} „{Mission.name}“, Welt {theme.name}, {Mission.waves.Count} Wellen, {Mission.paths.Count} Wege, {Mission.pads.Count} Bauplätze, Deck bei {Mission.deckTop}");
        }

        /// <summary>Halle der Tore: Nabe mit vier Toren, Lauftafel und Turmwand (HubSession).</summary>
        void StartHub()
        {
            QuestRuntime.Create();
            WorldTheme.Index = 1;
            var theme = WorldTheme.Current;
            ShaderGlobals.ApplyWorld(theme);
            var world = new GameObject("Welt").transform;
            SkyBuilder.Apply(theme, world);
            RenderSettings.fog = false;
            Rig = XRRig.Create(Vector3.zero, 4f * 1.4f - 0.3f, Vector3.back);
            SkyBuilder.SetupCamera(Rig.head, theme);
            var hub = HubSession.Create(Rig);
            hub.pauseMenu = PauseMenu.Create(null, Rig.head.transform);
            XRHandVisual.Create(Rig.leftHand, Rig.head.transform, true, XRInput.LeftHanded, theme);
            XRHandVisual.Create(Rig.rightHand, Rig.head.transform, false, !XRInput.LeftHanded, theme);
            LoadVeil.Create(Rig.head.transform);
            Debug.Log("Neonlith: Halle der Tore");
        }
    }
}
