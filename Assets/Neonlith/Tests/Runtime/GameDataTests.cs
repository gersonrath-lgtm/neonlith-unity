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
        public void ConfigsConverted()
        {
            Assert.IsNotNull(GameData.Config("weapon"));
            Assert.IsNotNull(GameData.Config("essence"));
            Assert.AreEqual("WeaponConfig", (string)GameData.Config("weapon")["_class"]);
        }
    }
}
