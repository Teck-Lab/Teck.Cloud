using Customer.Application.Tenants.Responses;
using Shouldly;

namespace Customer.UnitTests.Application.Responses;

public class ServiceDatabaseInfoResponseTests
{
    [Fact]
    public void Properties_ShouldRoundTripAssignedValues()
    {
        // Arrange
        const string writeKey = "ConnectionStrings__Tenants__tenant-1__Write";
        const string readKey = "ConnectionStrings__Tenants__tenant-1__Read";

        // Act
        var response = new ServiceDatabaseInfoResponse
        {
            WriteEnvVarKey = writeKey,
            ReadEnvVarKey = readKey,
            HasSeparateReadDatabase = true,
        };

        // Assert
        response.WriteEnvVarKey.ShouldBe(writeKey);
        response.ReadEnvVarKey.ShouldBe(readKey);
        response.HasSeparateReadDatabase.ShouldBeTrue();
    }
}
