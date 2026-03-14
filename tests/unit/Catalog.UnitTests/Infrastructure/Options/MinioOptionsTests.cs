using System.ComponentModel.DataAnnotations;
using Catalog.Infrastructure.Options;
using Shouldly;

namespace Catalog.UnitTests.Infrastructure.Options;

public sealed class MinioOptionsTests
{
    [Fact]
    public void Properties_ShouldRoundTripAssignedValues()
    {
        // Arrange
        var endpoint = new Uri("https://minio.example.com");

        // Act
        var options = new MinioOptions
        {
            AccessKeyId = "access-key",
            SecretAccessKey = "secret-key",
            AwsRegion = "us-east-1",
            MinioServerUrl = endpoint,
        };

        // Assert
        options.AccessKeyId.ShouldBe("access-key");
        options.SecretAccessKey.ShouldBe("secret-key");
        options.AwsRegion.ShouldBe("us-east-1");
        options.MinioServerUrl.ShouldBe(endpoint);
    }

    [Fact]
    public void RequiredAttributes_ShouldBePresent_OnAllOptionProperties()
    {
        // Act
        var requiredAttributes = typeof(MinioOptions)
            .GetProperties()
            .Select(property => new
            {
                Name = property.Name,
                Required = property.GetCustomAttributes(typeof(RequiredAttribute), inherit: false).Cast<RequiredAttribute>().SingleOrDefault(),
            })
            .ToDictionary(item => item.Name, item => item.Required);

        // Assert
        requiredAttributes[nameof(MinioOptions.AccessKeyId)].ShouldNotBeNull();
        requiredAttributes[nameof(MinioOptions.AccessKeyId)]!.AllowEmptyStrings.ShouldBeFalse();

        requiredAttributes[nameof(MinioOptions.SecretAccessKey)].ShouldNotBeNull();
        requiredAttributes[nameof(MinioOptions.SecretAccessKey)]!.AllowEmptyStrings.ShouldBeFalse();

        requiredAttributes[nameof(MinioOptions.AwsRegion)].ShouldNotBeNull();
        requiredAttributes[nameof(MinioOptions.AwsRegion)]!.AllowEmptyStrings.ShouldBeFalse();

        requiredAttributes[nameof(MinioOptions.MinioServerUrl)].ShouldNotBeNull();
    }
}
