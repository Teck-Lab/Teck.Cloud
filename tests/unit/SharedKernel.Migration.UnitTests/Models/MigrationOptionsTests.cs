using SharedKernel.Migration.Models;
using Shouldly;

namespace SharedKernel.Migration.UnitTests.Models;

public class MigrationOptionsTests
{
    [Fact]
    public void Constructor_ShouldSetDefaultValues()
    {
        // Act
        var options = new MigrationOptions();

        // Assert
        options.ScriptsPath.ShouldBe("Scripts");
        options.Provider.ShouldBe("PostgreSQL");
        options.JournalSchema.ShouldBeNull();
        options.JournalTable.ShouldBe("SchemaVersions");
        options.UseTransactions.ShouldBeTrue();
        options.CommandTimeoutSeconds.ShouldBe(300);
        options.LogScriptOutput.ShouldBeTrue();
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var options = new MigrationOptions();

        // Act
        options.ScriptsPath = "/custom/scripts";
        options.Provider = "MySQL";
        options.JournalSchema = "migrations";
        options.JournalTable = "VersionHistory";
        options.UseTransactions = false;
        options.CommandTimeoutSeconds = 600;
        options.LogScriptOutput = false;

        // Assert
        options.ScriptsPath.ShouldBe("/custom/scripts");
        options.Provider.ShouldBe("MySQL");
        options.JournalSchema.ShouldBe("migrations");
        options.JournalTable.ShouldBe("VersionHistory");
        options.UseTransactions.ShouldBeFalse();
        options.CommandTimeoutSeconds.ShouldBe(600);
        options.LogScriptOutput.ShouldBeFalse();
    }

    [Fact]
    public void ObjectInitializer_ShouldWork()
    {
        // Act
        var options = new MigrationOptions
        {
            ScriptsPath = "DatabaseMigrations",
            Provider = "SqlServer",
            JournalSchema = "dbo",
            JournalTable = "MigrationLog",
            UseTransactions = true,
            CommandTimeoutSeconds = 120,
            LogScriptOutput = true,
        };

        // Assert
        options.ScriptsPath.ShouldBe("DatabaseMigrations");
        options.Provider.ShouldBe("SqlServer");
        options.JournalSchema.ShouldBe("dbo");
        options.JournalTable.ShouldBe("MigrationLog");
        options.UseTransactions.ShouldBeTrue();
        options.CommandTimeoutSeconds.ShouldBe(120);
        options.LogScriptOutput.ShouldBeTrue();
    }

    [Fact]
    public void CommandTimeoutSeconds_DefaultValue_ShouldBe5Minutes()
    {
        // Arrange
        var options = new MigrationOptions();

        // Act & Assert
        options.CommandTimeoutSeconds.ShouldBe(300); // 5 minutes * 60 seconds
        TimeSpan.FromSeconds(options.CommandTimeoutSeconds).ShouldBe(TimeSpan.FromMinutes(5));
    }
}
