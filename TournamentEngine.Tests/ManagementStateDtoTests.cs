using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common.Dashboard;

namespace TournamentEngine.Tests;

[TestClass]
public class ManagementStateDtoTests
{
    [TestMethod]
    public void Defaults_AreInitialized()
    {
        var dto = new ManagementStateDto();

        Assert.AreEqual(ManagementRunState.NotStarted, dto.Status);
        Assert.AreEqual(string.Empty, dto.Message);
        Assert.IsFalse(dto.BotsReady);
        Assert.AreEqual(0, dto.BotCount);
        Assert.IsNull(dto.LastAction);
        Assert.IsNull(dto.LastActionAt);
        Assert.AreNotEqual(default, dto.LastUpdated);
    }

    [TestMethod]
    public void Properties_CanBeSet()
    {
        var timestamp = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);
        var dto = new ManagementStateDto
        {
            Status = ManagementRunState.Running,
            Message = "Tournament running",
            BotsReady = true,
            BotCount = 3,
            LastAction = "Start",
            LastActionAt = timestamp,
            LastUpdated = timestamp
        };

        Assert.AreEqual(ManagementRunState.Running, dto.Status);
        Assert.AreEqual("Tournament running", dto.Message);
        Assert.IsTrue(dto.BotsReady);
        Assert.AreEqual(3, dto.BotCount);
        Assert.AreEqual("Start", dto.LastAction);
        Assert.AreEqual(timestamp, dto.LastActionAt);
        Assert.AreEqual(timestamp, dto.LastUpdated);
    }
}
