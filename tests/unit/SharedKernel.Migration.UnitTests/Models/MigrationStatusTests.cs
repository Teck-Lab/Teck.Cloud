using SharedKernel.Migration.Models;
using Shouldly;

namespace SharedKernel.Migration.UnitTests.Models;

public class MigrationStatusTests
{
    [Fact]
    public void Pending_ShouldHaveValue0()
    {
        // Act & Assert
        MigrationStatus.Pending.ShouldBe((MigrationStatus)0);
        ((int)MigrationStatus.Pending).ShouldBe(0);
    }

    [Fact]
    public void InProgress_ShouldHaveValue1()
    {
        // Act & Assert
        MigrationStatus.InProgress.ShouldBe((MigrationStatus)1);
        ((int)MigrationStatus.InProgress).ShouldBe(1);
    }

    [Fact]
    public void Completed_ShouldHaveValue2()
    {
        // Act & Assert
        MigrationStatus.Completed.ShouldBe((MigrationStatus)2);
        ((int)MigrationStatus.Completed).ShouldBe(2);
    }

    [Fact]
    public void Failed_ShouldHaveValue3()
    {
        // Act & Assert
        MigrationStatus.Failed.ShouldBe((MigrationStatus)3);
        ((int)MigrationStatus.Failed).ShouldBe(3);
    }

    [Fact]
    public void PartiallyProvisioned_ShouldHaveValue4()
    {
        // Act & Assert
        MigrationStatus.PartiallyProvisioned.ShouldBe((MigrationStatus)4);
        ((int)MigrationStatus.PartiallyProvisioned).ShouldBe(4);
    }

    [Fact]
    public void AllValues_ShouldBeDefined()
    {
        // Act
        var allValues = Enum.GetValues<MigrationStatus>();

        // Assert
        allValues.ShouldContain(MigrationStatus.Pending);
        allValues.ShouldContain(MigrationStatus.InProgress);
        allValues.ShouldContain(MigrationStatus.Completed);
        allValues.ShouldContain(MigrationStatus.Failed);
        allValues.ShouldContain(MigrationStatus.PartiallyProvisioned);
        allValues.Length.ShouldBe(5);
    }

    [Fact]
    public void ToString_ShouldReturnEnumName()
    {
        // Act & Assert
        MigrationStatus.Pending.ToString().ShouldBe("Pending");
        MigrationStatus.InProgress.ToString().ShouldBe("InProgress");
        MigrationStatus.Completed.ToString().ShouldBe("Completed");
        MigrationStatus.Failed.ToString().ShouldBe("Failed");
        MigrationStatus.PartiallyProvisioned.ToString().ShouldBe("PartiallyProvisioned");
    }

    [Fact]
    public void Parse_ShouldConvertStringToEnum()
    {
        // Act
        var pending = Enum.Parse<MigrationStatus>("Pending");
        var inProgress = Enum.Parse<MigrationStatus>("InProgress");
        var completed = Enum.Parse<MigrationStatus>("Completed");
        var failed = Enum.Parse<MigrationStatus>("Failed");
        var partial = Enum.Parse<MigrationStatus>("PartiallyProvisioned");

        // Assert
        pending.ShouldBe(MigrationStatus.Pending);
        inProgress.ShouldBe(MigrationStatus.InProgress);
        completed.ShouldBe(MigrationStatus.Completed);
        failed.ShouldBe(MigrationStatus.Failed);
        partial.ShouldBe(MigrationStatus.PartiallyProvisioned);
    }
}
