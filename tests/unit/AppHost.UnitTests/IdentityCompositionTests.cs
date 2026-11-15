using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Shouldly;
using Teck.Cloud.AppHost;
using Xunit;

namespace AppHost.UnitTests;

/// <summary>Verifies shared identity inputs and generated development credentials without starting containers.</summary>
public sealed class IdentityCompositionTests
{
    /// <summary>Aspire uses the exact integration-test image and secret parameter references.</summary>
    [Fact]
    public void SharedPinAndGeneratedGatewayCredentialsAreUsed()
    {
        var repository = FindRepository();
        var project = Path.Combine(repository, "src", "aspire", "Teck.Cloud.AppHost");
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            ProjectDirectory = project,
            DisableDashboard = true,
        });
        builder.AddFoundationServices();
        var resource = builder.Resources.Single(item => string.Equals(item.Name, "keycloak", StringComparison.Ordinal));
        var image = resource.Annotations.OfType<ContainerImageAnnotation>().ShouldHaveSingleItem();
        using var runtime = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "Identity", "runtime.json")));
        image.Image.ShouldBe(runtime.RootElement.GetProperty("image").GetString());
        runtime.RootElement.GetProperty("baseImage").GetString().ShouldNotBeNull().ShouldContain("@sha256:", Case.Sensitive);
        runtime.RootElement.GetProperty("optimized").GetBoolean().ShouldBeTrue();
        _ = resource.Annotations.OfType<DockerfileBuildAnnotation>().ShouldHaveSingleItem();
        var database = builder.Resources.OfType<PostgresDatabaseResource>().Single(item => string.Equals(item.Name, "keycloak-db", StringComparison.Ordinal));
        database.DatabaseName.ShouldBe("keycloak");
        image.Registry.ShouldBeNull();
        image.Tag.ShouldBeNull();
        foreach (var name in new[] { "keycloak-customer-gateway-secret", "keycloak-admin-gateway-secret" })
        {
            var parameter = builder.Resources.OfType<ParameterResource>().Single(item => string.Equals(item.Name, name, StringComparison.Ordinal));
            parameter.Secret.ShouldBeTrue();
            var generated = parameter.Default.ShouldBeOfType<GenerateParameterDefault>();
            generated.MinLength.ShouldBe(48);
        }
    }

    /// <summary>Administrative tokens remain short-lived after the BFF's MFA boundary.</summary>
    /// <param name="clientId">The public login or confidential exchange client.</param>
    [Theory]
    [InlineData("teck-admin")]
    [InlineData("admin-gateway")]
    public void AdministrativeAccessTokensHaveBoundedLifetime(string clientId)
    {
        using var realm = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRepository(), "src", "aspire", "Teck.Cloud.AppHost", "Identity", "Realms", "teck-local-realm.json")));
        var client = realm.RootElement.GetProperty("clients").EnumerateArray().Single(item => string.Equals(item.GetProperty("clientId").GetString(), clientId, StringComparison.Ordinal));
        client.GetProperty("attributes").GetProperty("access.token.lifespan").GetString().ShouldBe("60");
    }

    private static string FindRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
