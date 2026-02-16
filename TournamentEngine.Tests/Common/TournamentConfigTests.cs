using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Core.Common;

namespace TournamentEngine.Tests.Common;

[TestClass]
public class TournamentConfigTests
{
    [TestMethod]
    public void TournamentConfig_DefaultGameTypes_Contains4Games()
    {
        // Arrange & Act
        var config = new TournamentConfig();
        
        // Assert
        Assert.AreEqual(4, config.GameTypes.Count);
        Assert.IsTrue(config.GameTypes.Contains(GameType.RPSLS));
        Assert.IsTrue(config.GameTypes.Contains(GameType.ColonelBlotto));
        Assert.IsTrue(config.GameTypes.Contains(GameType.PenaltyKicks));
        Assert.IsTrue(config.GameTypes.Contains(GameType.SecurityGame));
    }
    
    [TestMethod]
    public void TournamentConfig_DefaultGroupCount_Is10()
    {
        // Arrange & Act
        var config = new TournamentConfig();
        
        // Assert
        Assert.AreEqual(10, config.GroupCount);
    }
    
    [TestMethod]
    public void TournamentConfig_CanCustomizeGameTypes()
    {
        // Arrange & Act
        var config = new TournamentConfig
        {
            GameTypes = new List<GameType> { GameType.RPSLS, GameType.ColonelBlotto }
        };
        
        // Assert
        Assert.AreEqual(2, config.GameTypes.Count);
    }
    
    [TestMethod]
    public void TournamentConfig_CanCustomizeGroupCount()
    {
        // Arrange & Act
        var config = new TournamentConfig { GroupCount = 5 };
        
        // Assert
        Assert.AreEqual(5, config.GroupCount);
    }
    
    [TestMethod]
    public void TournamentConfig_FinalistsPerGroup_DefaultIs1()
    {
        // Arrange & Act
        var config = new TournamentConfig();
        
        // Assert
        Assert.AreEqual(1, config.FinalistsPerGroup);
    }
}
