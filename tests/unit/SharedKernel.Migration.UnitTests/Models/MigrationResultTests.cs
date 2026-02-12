using SharedKernel.Migration.Models;
using Shouldly;

namespace SharedKernel.Migration.UnitTests.Models;

public class MigrationResultTests
{
    [Fact]
    public void Successful_ShouldCreateSuccessfulResult()
    {
        // Arrange
        var scriptsApplied = 5;
        var duration = TimeSpan.FromSeconds(10);
        var appliedScripts = new List<string> { "001_Init.sql", "002_AddUsers.sql" };
        var provider = "PostgreSQL";

        // Act
        var result = MigrationResult.Successful(scriptsApplied, duration, appliedScripts, provider);

        // Assert
        result.Success.ShouldBeTrue();
        result.ScriptsApplied.ShouldBe(5);
        result.Duration.ShouldBe(duration);
        result.AppliedScripts.ShouldBe(appliedScripts);
        result.Provider.ShouldBe("PostgreSQL");
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Successful_ShouldCreateSuccessfulResult_WithoutProvider()
    {
        // Arrange
        var scriptsApplied = 3;
        var duration = TimeSpan.FromSeconds(5);
        var appliedScripts = new List<string> { "001_Init.sql" };

        // Act
        var result = MigrationResult.Successful(scriptsApplied, duration, appliedScripts);

        // Assert
        result.Success.ShouldBeTrue();
        result.ScriptsApplied.ShouldBe(3);
        result.Duration.ShouldBe(duration);
        result.AppliedScripts.ShouldBe(appliedScripts);
        result.Provider.ShouldBeNull();
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Successful_ShouldCreateResult_WithEmptyScriptList()
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(1);
        var appliedScripts = new List<string>();

        // Act
        var result = MigrationResult.Successful(0, duration, appliedScripts);

        // Assert
        result.Success.ShouldBeTrue();
        result.ScriptsApplied.ShouldBe(0);
        result.AppliedScripts.ShouldBeEmpty();
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Failed_ShouldCreateFailedResult()
    {
        // Arrange
        var errorMessage = "Connection timeout";
        var duration = TimeSpan.FromSeconds(30);
        var provider = "PostgreSQL";

        // Act
        var result = MigrationResult.Failed(errorMessage, duration, provider);

        // Assert
        result.Success.ShouldBeFalse();
        result.ScriptsApplied.ShouldBe(0);
        result.Duration.ShouldBe(duration);
        result.ErrorMessage.ShouldBe("Connection timeout");
        result.AppliedScripts.ShouldBeEmpty();
        result.Provider.ShouldBe("PostgreSQL");
    }

    [Fact]
    public void Failed_ShouldCreateFailedResult_WithoutProvider()
    {
        // Arrange
        var errorMessage = "Syntax error in script";
        var duration = TimeSpan.FromSeconds(2);

        // Act
        var result = MigrationResult.Failed(errorMessage, duration);

        // Assert
        result.Success.ShouldBeFalse();
        result.ScriptsApplied.ShouldBe(0);
        result.Duration.ShouldBe(duration);
        result.ErrorMessage.ShouldBe("Syntax error in script");
        result.AppliedScripts.ShouldBeEmpty();
        result.Provider.ShouldBeNull();
    }

    [Fact]
    public void MigrationResult_ShouldBeRecord()
    {
        // Arrange
        var scripts = new List<string> { "test.sql" };
        var result1 = MigrationResult.Successful(1, TimeSpan.FromSeconds(1), scripts, "PostgreSQL");
        var result2 = MigrationResult.Successful(1, TimeSpan.FromSeconds(1), scripts, "PostgreSQL");

        // Act & Assert
        result1.ShouldBe(result2); // Records have value equality when same list instance
    }

    [Fact]
    public void MigrationResult_ShouldSupportWith_Expression()
    {
        // Arrange
        var original = MigrationResult.Successful(1, TimeSpan.FromSeconds(1), new List<string> { "test.sql" });

        // Act
        var modified = original with { Provider = "MySQL" };

        // Assert
        modified.Provider.ShouldBe("MySQL");
        modified.ScriptsApplied.ShouldBe(original.ScriptsApplied);
        original.Provider.ShouldBeNull(); // Original unchanged
    }
}
