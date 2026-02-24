$code = @"
using System;
using TournamentEngine.Core.BotLoader;

var loader = new BotLoader();
var botFolder = @"$PWD\bots\Alpha_Team_001_v1";
try {
    var result = await loader.LoadBotFromFolderAsync(botFolder);
    Console.WriteLine("IsValid: " + result.IsValid);
    Console.WriteLine("TeamName: " + result.TeamName);
    Console.WriteLine("ValidationErrors: " + string.Join(", ", result.ValidationErrors ?? new List<string>()));
    if (result.BotInstance != null) {
        Console.WriteLine("BotInstance: " + result.BotInstance.GetType().FullName);
    }
} catch (Exception ex) {
    Console.WriteLine("Exception: " + ex.Message);
}
"@

$code | dotnet-script -
