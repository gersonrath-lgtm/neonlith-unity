using Neonlith;
using Neonlith.Data;
using NUnit.Framework;

namespace Neonlith.Tests
{
    /// <summary>Erste Prüfungen des Grundgerüsts (Unity Test Framework, Play oder Edit Mode).</summary>
    public class GameDataTests
    {
        [Test]
        public void CampaignHasTwentyMissions()
        {
            Assert.AreEqual(20, GameData.MissionCount);
        }

        [Test]
        public void EveryMissionMapLoads()
        {
            for (var n = 1; n <= 20; n++)
                Assert.IsNotNull(GameData.Mission(n), $"mission_{n:00}.json fehlt");
        }

        [Test]
        public void WorldOfMissionFollowsFivePerWorld()
        {
            Assert.AreEqual(0, WorldTheme.WorldOfMission(1));
            Assert.AreEqual(0, WorldTheme.WorldOfMission(5));
            Assert.AreEqual(1, WorldTheme.WorldOfMission(6));
            Assert.AreEqual(3, WorldTheme.WorldOfMission(20));
        }

        [Test]
        public void MissionOneHasPathsWavesAndDeck()
        {
            var m = MissionData.Load(1);
            Assert.IsNotNull(m);
            Assert.AreEqual(1, m.paths.Count, "Mission 1: ein Zugang");
            Assert.Greater(m.waves.Count, 0);
            Assert.AreEqual(20, m.lives);
            Assert.Greater(m.deckTop.y, m.core.y, "Standdeck liegt über der Kernplatte");
            Assert.Greater(m.StandRadiusM, 3f);
            var last = m.paths[0][m.paths[0].Length - 1];
            Assert.Less(UnityEngine.Vector3.Distance(last, m.core), 8f, "Weg endet an der Kernplatte");
        }

        [Test]
        public void EveryMissionBuildsData()
        {
            for (var n = 1; n <= 20; n++)
            {
                var m = MissionData.Load(n);
                Assert.IsNotNull(m, $"Mission {n}");
                Assert.Greater(m.paths.Count, 0, $"Mission {n}: Wege");
                Assert.AreEqual(m.waves.Count, m.hpWave.Length, $"Mission {n}: Lebensfaktoren");
            }
        }

        [Test]
        public void BalanceTablesMatchGodot()
        {
            Assert.AreEqual(6f, Balance.Enemy("block").armor);
            Assert.AreEqual(14f, Balance.Stats("arrow", 0)["damage"]);
            Assert.AreEqual(2f, Balance.Stats("arrow", 2)["targets"]);
            Assert.AreEqual(60, Balance.BuildCost("arrow"));
            Assert.AreEqual(75, Balance.UpgradeCost("arrow", 0));
            Assert.AreEqual(-1, Balance.UpgradeCost("arrow", 2));
            Assert.IsTrue(Balance.IsTrap("barricade"));
            Assert.IsTrue(Balance.Enemy("boss_kael").srcMult["ice"] == 0f);
            Assert.AreEqual(9, Balance.Structures.Count);
        }

        [Test]
        public void MissionOneHasBuildPads()
        {
            var m = MissionData.Load(1);
            Assert.Greater(m.pads.Count, 4, "Bauplätze an den Läufen");
            Assert.IsTrue(m.pads.Exists(p => p.trap), "Sperrplätze auf der Fahrbahn");
            Assert.IsTrue(m.towers.Contains("arrow"));
        }

        [Test]
        public void ConfigsConverted()
        {
            Assert.IsNotNull(GameData.Config("weapon"));
            Assert.IsNotNull(GameData.Config("essence"));
            Assert.AreEqual("WeaponConfig", (string)GameData.Config("weapon")["_class"]);
        }
    }
}
