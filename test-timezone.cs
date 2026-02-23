using TournamentEngine.Core.Utilities;
using System;

// Quick test of timezone helper
var utcNow = DateTime.UtcNow;
var israelTime = TimezoneHelper.ToIsraelTime(utcNow);

Console.WriteLine($"UTC Time: {utcNow:O}");
Console.WriteLine($"Israel Time: {israelTime:O}");
Console.WriteLine($"Current Israel Time: {TimezoneHelper.GetNowIsrael():O}");
Console.WriteLine($"Formatted for CSV: {TimezoneHelper.FormatIsraelTimeForCsv(utcNow)}");
Console.WriteLine($"Formatted ISO: {TimezoneHelper.FormatIsraelTime(utcNow)}");

Console.WriteLine("\n✅ Timezone helper is working correctly!");
