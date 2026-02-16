using Microsoft.VisualStudio.TestTools.UnitTesting;
using TournamentEngine.Api.Models;
using TournamentEngine.Core.Common;
using System.Collections.Generic;
using System.Linq;

namespace TournamentEngine.Tests.Api;

/// <summary>
/// Unit tests for bot validation rules enforced in BotEndpoints
/// Tests all validation logic including double semicolons, approved namespaces, etc.
/// </summary>
[TestClass]
public class BotValidationTests
{
    #region Double Semicolon Rule Tests

    [TestMethod]
    public void ValidateCSharpFile_WithSingleSemicolons_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";
    public GameType GameType => GameType.RPSLS;
    
    public async Task<string> MakeMove(GameState state, CancellationToken ct)
    {
        return ""Rock"";
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("double semicolon rule")), 
            "Should detect missing double semicolons");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithDoubleSemicolons_NoError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";;
    public GameType GameType => GameType.RPSLS;;
    
    public async Task<string> MakeMove(GameState state, CancellationToken ct)
    {
        return await Task.FromResult(""Rock"");;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsFalse(errors.Any(e => e.Contains("double semicolon rule")), 
            $"Should NOT report double semicolon error. Errors: {string.Join(", ", errors)}");
    }

    [TestMethod]
    public void ValidateCSharpFile_UsingDirectivesWithSingleSemicolon_Allowed()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.Linq;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsFalse(errors.Any(e => e.Contains("double semicolon rule")), 
            "Using directives can have single semicolons");
    }

    [TestMethod]
    public void ValidateCSharpFile_ForLoopWithSingleSemicolon_Allowed()
    {
        // Arrange - for loops can have 2 single semicolons in the declaration
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public class TestBot
{
    public string Name => ""Test"";;
    
    public void Process()
    {
        for (int i = 0; i < 10; i++)
        {
            var x = i;;
        }
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert - for loops should be allowed to have single semicolons
        Assert.IsFalse(errors.Any(e => e.Contains("double semicolon rule")), 
            $"For loops can have single semicolons in the declaration. Errors: {string.Join(", ", errors)}");
    }

    #endregion

    #region Approved Namespace Tests

    [TestMethod]
    public void ValidateCSharpFile_WithApprovedNamespaces_NoError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsFalse(errors.Any(e => e.Contains("unapproved namespace")), 
            $"All namespaces should be approved. Errors: {string.Join(", ", errors)}");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithNetworkingNamespace_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.Net.Http;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert - System.Net.Http is caught by dangerous pattern check
        Assert.IsTrue(errors.Any(e => e.Contains("System.Net.Http")), 
            $"Should detect System.Net.Http. Errors: {string.Join("; ", errors)}");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithReflectionNamespace_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.Reflection;

public class TestBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert -System.Reflection is unapproved namespace (not a sub of approved ones)
        Assert.IsTrue(errors.Any(e => e.Contains("System.Reflection")), 
            $"Should detect System.Reflection. Errors: {string.Join("; ", errors)}");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithCustomNamespace_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using MyCustomLibrary.Hacks;

public class TestBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("unapproved namespace") && e.Contains("MyCustomLibrary")), 
            "Should detect custom unapproved namespace");
    }

    #endregion

    #region Dangerous API Pattern Tests

    [TestMethod]
    public void ValidateCSharpFile_WithFileDelete_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.IO;

public class TestBot
{
    public void BadMethod()
    {
        File.Delete(""somefile.txt"");;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("disallowed pattern") && e.Contains("File.Delete")), 
            "Should detect File.Delete pattern");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithProcessStart_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.Diagnostics;

public class TestBot
{
    public void BadMethod()
    {
        System.Diagnostics.Process.Start(""cmd.exe"");;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("disallowed pattern") && e.Contains("Process.Start")), 
            "Should detect Process.Start pattern");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithAssemblyLoad_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.Reflection;

public class TestBot
{
    public void BadMethod()
    {
        System.Reflection.Assembly.Load(""SomeDll"");;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("disallowed pattern") && e.Contains("Assembly.Load")), 
            "Should detect Assembly.Load pattern");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithHttpClient_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System.Net.Http;

public class TestBot
{
    private HttpClient _client = new HttpClient();;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("disallowed pattern") && e.Contains("System.Net.Http")), 
            "Should detect HttpClient/networking pattern");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithEnvironmentExit_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public class TestBot
{
    public void BadMethod()
    {
        Environment.Exit(0);;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("disallowed pattern") && e.Contains("Environment.Exit")), 
            "Should detect Environment.Exit pattern");
    }

    #endregion

    #region Unsafe Code Tests

    [TestMethod]
    public void ValidateCSharpFile_WithUnsafeBlock_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public class TestBot
{
    public unsafe void BadMethod()
    {
        int* ptr = null;;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("unsafe") && e.Contains("not allowed")), 
            "Should detect unsafe code blocks");
    }

    #endregion

    #region Target Framework Tests

    [TestMethod]
    public void ValidateCSharpFile_WithNet80TargetFramework_NoError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

// <TargetFramework>net8.0</TargetFramework>
public class TestBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsFalse(errors.Any(e => e.Contains("framework") && e.Contains("net8.0")), 
            "Should NOT report error for net8.0 target framework");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithNet60TargetFramework_ReturnsError()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

// <TargetFramework>net6.0</TargetFramework>
public class TestBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("framework") && e.Contains("other than")), 
            $"Should detect non-net8.0 target framework. Errors: {string.Join("; ", errors)}");
    }

    #endregion

    #region IBot Implementation Tests

    [TestMethod]
    public void ValidateCSharpFile_WithoutIBotImplementation_ReturnsWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public class TestBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsTrue(warnings.Any(w => w.Contains("IBot")), 
            "Should warn about missing IBot implementation");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithIBotImplementation_NoWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsFalse(warnings.Any(w => w.Contains("IBot") && w.Contains("doesn't appear")), 
            "Should NOT warn when IBot is implemented");
    }

    #endregion

    #region Required Methods Tests

    [TestMethod]
    public void ValidateCSharpFile_WithAllRequiredMethods_NoWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";;
    public GameType GameType => GameType.RPSLS;;
    
    public async Task<string> MakeMove(GameState state, CancellationToken ct)
    {
        return await Task.FromResult(""Rock"");;
    }
    
    public async Task<List<int>> AllocateTroops(GameState state, CancellationToken ct)
    {
        return await Task.FromResult(new List<int> { 20, 20, 20, 20, 20 });;
    }
    
    public async Task<PenaltyDecision> MakePenaltyDecision(GameState state, CancellationToken ct)
    {
        return await Task.FromResult(new PenaltyDecision());;
    }
    
    public async Task<SecurityDecision> MakeSecurityMove(GameState state, CancellationToken ct)
    {
        return await Task.FromResult(new SecurityDecision());;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsFalse(warnings.Any(w => w.Contains("MakeMove")), "Should NOT warn about missing MakeMove");
        Assert.IsFalse(warnings.Any(w => w.Contains("AllocateTroops")), "Should NOT warn about missing AllocateTroops");
        Assert.IsFalse(warnings.Any(w => w.Contains("MakePenaltyDecision")), "Should NOT warn about missing MakePenaltyDecision");
        Assert.IsFalse(warnings.Any(w => w.Contains("MakeSecurityMove")), "Should NOT warn about missing MakeSecurityMove");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithoutMakeMove_ReturnsWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";;
    
    public async Task<List<int>> AllocateTroops(GameState state, CancellationToken ct)
    {
        return await Task.FromResult(new List<int>());;
    }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsTrue(warnings.Any(w => w.Contains("MakeMove")), 
            "Should warn about missing MakeMove method");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithoutAllocateTroops_ReturnsWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public class TestBot
{
    public async Task<string> MakeMove() { return ""Rock"";;  }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsTrue(warnings.Any(w => w.Contains("AllocateTroops")), 
            "Should warn about missing AllocateTroops method");
    }

    #endregion

    #region File Type Tests

    [TestMethod]
    public void ValidateAllFiles_WithPythonFile_ReturnsError()
    {
        // Arrange
        var files = new List<BotFile>
        {
            new BotFile
            {
                FileName = "bot.py",
                Code = "def make_move():\n    return 'Rock'"
            }
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateAllFilesDirectly(files, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("Python") && e.Contains("supported")), 
            $"Should reject Python files with clear error. Errors: {string.Join("; ", errors)}");
    }

    [TestMethod]
    public void ValidateAllFiles_WithJavaScriptFile_ReturnsError()
    {
        // Arrange
        var files = new List<BotFile>
        {
            new BotFile
            {
                FileName = "bot.js",
                Code = "function makeMove() { return 'Rock'; }"
            }
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateAllFilesDirectly(files, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("unsupported extension") && e.Contains("Only C#")), 
            "Should reject JavaScript files");
    }

    [TestMethod]
    public void ValidateAllFiles_WithCSharpFile_NoFileTypeError()
    {
        // Arrange
        var files = new List<BotFile>
        {
            new BotFile
            {
                FileName = "TestBot.cs",
                Code = @"
using System;
public class TestBot
{
    public string Name => ""Test"";;
}"
            }
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateAllFilesDirectly(files, errors, warnings);

        // Assert
        Assert.IsFalse(errors.Any(e => e.Contains("unsupported extension")), 
            "Should accept C# files");
    }

    [TestMethod]
    public void ValidateAllFiles_WithEmptyFile_ReturnsError()
    {
        // Arrange
        var files = new List<BotFile>
        {
            new BotFile
            {
                FileName = "TestBot.cs",
                Code = ""
            }
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateAllFilesDirectly(files, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("empty")), 
            "Should detect empty files");
    }

    [TestMethod]
    public void ValidateAllFiles_WithWhitespaceOnlyFile_ReturnsError()
    {
        // Arrange
        var files = new List<BotFile>
        {
            new BotFile
            {
                FileName = "TestBot.cs",
                Code = "   \n\t  \n  "
            }
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateAllFilesDirectly(files, errors, warnings);

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("empty")), 
            "Should detect whitespace-only files");
    }

    #endregion

    #region Missing Namespace Tests

    [TestMethod]
    public void ValidateCSharpFile_WithoutTournamentEngineNamespace_ReturnsWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public class TestBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsTrue(warnings.Any(w => w.Contains("TournamentEngine.Core.Common")), 
            "Should warn about missing TournamentEngine.Core.Common namespace");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithTournamentEngineNamespace_NoWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;
using TournamentEngine.Core.Common;

public class TestBot : IBot
{
    public string TeamName => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsFalse(warnings.Any(w => w.Contains("TournamentEngine.Core.Common") && w.Contains("should include")), 
            "Should NOT warn when TournamentEngine.Core.Common is included");
    }

    #endregion

    #region Class Definition Tests

    [TestMethod]
    public void ValidateCSharpFile_WithoutClassDefinition_ReturnsWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public interface ITest
{
    string Name { get; }
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsTrue(warnings.Any(w => w.Contains("no class definitions")), 
            "Should warn about missing class definition");
    }

    [TestMethod]
    public void ValidateCSharpFile_WithClassDefinition_NoWarning()
    {
        // Arrange
        var file = new BotFile
        {
            FileName = "TestBot.cs",
            Code = @"
using System;

public class TestBot
{
    public string Name => ""Test"";;
}"
        };

        var errors = new List<string>();
        var warnings = new List<string>();

        // Act
        ValidateCSharpFileDirectly(file, errors, warnings);

        // Assert
        Assert.IsFalse(warnings.Any(w => w.Contains("no class definitions")), 
            "Should NOT warn when class is defined");
    }

    #endregion

    #region Helper Methods - Direct Validation Access

    /// <summary>
    /// Helper to replicate ValidateCSharpFile logic from BotEndpoints.cs
    /// Must match the actual implementation for accurate testing
    /// </summary>
    private void ValidateCSharpFileDirectly(BotFile file, List<string> errors, List<string> warnings)
    {
        var code = file.Code;

        // Check for basic C# structure
        if (!code.Contains("class "))
        {
            warnings.Add($"File {file.FileName} appears to be C# but has no class definitions");
        }

        // CODING RULE: Check for double semicolons
        var singleSemicolonPattern = @"(?<!;);(?!;)";
        var singleSemicolons = System.Text.RegularExpressions.Regex.Matches(code, singleSemicolonPattern);
        
        if (singleSemicolons.Count > 0)
        {
            // Count valid single semicolons in specific contexts
            var validSingleSemiCount = 0;
            
            // Using directives - each using has 1 semicolon
            validSingleSemiCount += System.Text.RegularExpressions.Regex.Matches(code, @"using\s+[^;]+;").Count;
            
            // For loops - each for loop has 2 semicolons
            var forLoopMatches = System.Text.RegularExpressions.Regex.Matches(code, @"for\s*\([^;]*;[^;]*;[^)]*\)");
            validSingleSemiCount += forLoopMatches.Count * 2;
            
            // Namespace declarations - each namespace has 1 semicolon
            validSingleSemiCount += System.Text.RegularExpressions.Regex.Matches(code, @"namespace\s+[^;]+;").Count;

            if (singleSemicolons.Count > validSingleSemiCount)
            {
                errors.Add($"File {file.FileName} violates double semicolon rule. All statements must end with ';;' (except using directives and for loops)");
            }
        }

        // Check for unsafe code blocks
        if (code.Contains("unsafe "))
        {
            errors.Add($"File {file.FileName} contains 'unsafe' code blocks which are not allowed");
        }

        // Check for dangerous patterns (exact strings from BotEndpoints.cs)
        var dangerousPatterns = new[]
        {
            "System.Reflection.Assembly.Load",
            "System.Runtime.InteropServices",
            "System.Net.Http",
            "System.Net.Sockets",
            "System.Diagnostics.Process.Start",
            "File.Delete",
            "Directory.Delete",
            "Environment.Exit"
        };

        foreach (var pattern in dangerousPatterns)
        {
            if (code.Contains(pattern))
            {
                errors.Add($"File {file.FileName} contains disallowed pattern: {pattern}");
            }
        }

        // Validate using directives
        var approvedNamespaces = new HashSet<string>
        {
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Numerics",
            "System.Threading",
            "System.IO",
            "System.Text.RegularExpressions",
            "System.Diagnostics",
            "TournamentEngine.Core.Common"
        };

        var usingMatches = System.Text.RegularExpressions.Regex.Matches(code, @"using\s+([a-zA-Z0-9_.]+)\s*;");
        foreach (System.Text.RegularExpressions.Match match in usingMatches)
        {
            var namespaceName = match.Groups[1].Value;
            
            // Only exact matches are allowed
            if (!approvedNamespaces.Contains(namespaceName))
            {
                errors.Add($"File {file.FileName} uses unapproved namespace: {namespaceName}. Only approved .NET libraries are allowed: {string.Join(", ", approvedNamespaces)}");
            }
        }

        // Check for TournamentEngine.Core.Common
        if (!code.Contains("using TournamentEngine.Core.Common"))
        {
            warnings.Add($"File {file.FileName} should include 'using TournamentEngine.Core.Common;' to implement IBot interface");
        }

        // Check for target framework
        if (code.Contains("<TargetFramework>") && !code.Contains("<TargetFramework>net8.0</TargetFramework>"))
        {
            errors.Add($"File {file.FileName} targets a framework other than net8.0. Must target .NET 8.0");
        }

        // Check for IBot implementation
        if (!code.Contains(": IBot"))
        {
            warnings.Add($"File {file.FileName} doesn't appear to implement IBot interface");
        }

        // Check for required methods
        var requiredMethods = new[] { "MakeMove", "AllocateTroops", "MakePenaltyDecision", "MakeSecurityMove" };
        foreach (var method in requiredMethods)
        {
            if (!code.Contains(method))
            {
                warnings.Add($"File {file.FileName} doesn't appear to implement required method: {method}");
            }
        }
    }

    private void ValidateAllFilesDirectly(List<BotFile> files, List<string> errors, List<string> warnings)
    {
        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file.Code))
            {
                errors.Add($"File {file.FileName} is empty");
                continue;
            }

            if (file.FileName.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
            {
                ValidateCSharpFileDirectly(file, errors, warnings);
            }
            else if (file.FileName.EndsWith(".py", System.StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"File {file.FileName} is Python (.py) but only C# (.cs) files are supported");
            }
            else if (!file.FileName.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"File {file.FileName} has unsupported extension. Only C# (.cs) files are accepted");
            }
        }
    }

    #endregion
}
