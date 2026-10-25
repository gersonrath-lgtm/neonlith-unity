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
        [Tooltip("Mission 1..20 (campaign.json); bestimmt Karte, Wellen und Weltfarbe")]
        [Range(1, 20)] public int mission = 1;

        [Tooltip("Waffe: pistol, rifle, sniper (Arsenal); auto = gespeicherte Wahl")]
        public string weapon = "auto";

        [Tooltip("Linkshänder: Waffe links, Gehen mit dem rechten Stick")]
        public bool leftHanded = false;

        public static Bootstrap Instance { get; private set; }
        /// <summary>Mission für den nächsten Szenenstart (Pausenmenü, Missionswahl); 0 = Feld der Szene.</summary>
        public static int nextMission;
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

            if (nextMission > 0) { mission = nextMission; nextMission = 0; }
            Mission = MissionData.Load(mission);
            if (Mission == null) return;
            WorldTheme.Index = WorldTheme.WorldOfMission(mission);
            var theme = WorldTheme.Current;
            ShaderGlobals.ApplyWorld(theme);
            XRInput.Init();
            XRInput.LeftHanded = leftHanded || Progress.LeftHanded;
            QuestRuntime.Create();

            var world = new GameObject("Welt").transform;
            SkyBuilder.Apply(theme, world);
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
            Hud.Create(Session, pw, Rig.head.transform);

            Debug.Log($"Neonlith: Mission {mission} „{Mission.name}“, Welt {theme.name}, {Mission.waves.Count} Wellen, {Mission.paths.Count} Wege, {Mission.pads.Count} Bauplätze, Deck bei {Mission.deckTop}");
        }
    }
}
