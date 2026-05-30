using Location.Infrastructure.Persistence;
using Location.Infrastructure.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

#pragma warning disable CA2012

namespace Location.UnitTests.Infrastructure.Service;

public sealed class TemplateFontAssetServiceTests
{
    [Fact]
    public async Task UploadListDelete_ShouldPersistMetadataAndManageBlob()
    {
        (TemplateFontAssetService service, string localDirectory) = CreateService();

        try
        {
            byte[] payload = [1, 2, 3, 4, 5, 6, 7, 8];

            await using MemoryStream uploadContent = new(payload);

            var uploadResult = await service.UploadAsync(
                tenantId: "tenant-a",
                templateId: "template-1",
                fontKey: "fonts/Inter-Regular.ttf",
                fileName: "Inter-Regular.ttf",
                contentType: "font/ttf",
                content: uploadContent,
                cancellationToken: TestContext.Current.CancellationToken);

            uploadResult.IsError.ShouldBeFalse();
            uploadResult.Value.FontKey.ShouldBe("fonts/Inter-Regular.ttf");
            uploadResult.Value.FontFamilyToken.ShouldBe("tenant-font:fonts/Inter-Regular.ttf");
            uploadResult.Value.SizeBytes.ShouldBe(payload.Length);

            string objectPath = BuildLocalObjectPath(localDirectory, uploadResult.Value.ObjectKey);
            File.Exists(objectPath).ShouldBeTrue();

            var listResult = await service.ListAsync(
                tenantId: "tenant-a",
                templateId: "template-1",
                cancellationToken: TestContext.Current.CancellationToken);

            listResult.IsError.ShouldBeFalse();
            listResult.Value.Fonts.Count.ShouldBe(1);
            listResult.Value.Fonts[0].FontKey.ShouldBe("fonts/Inter-Regular.ttf");
            listResult.Value.Fonts[0].ObjectKey.ShouldBe(uploadResult.Value.ObjectKey);

            var deleteResult = await service.DeleteAsync(
                tenantId: "tenant-a",
                templateId: "template-1",
                fontKey: "fonts/Inter-Regular.ttf",
                cancellationToken: TestContext.Current.CancellationToken);

            deleteResult.IsError.ShouldBeFalse();

            var listAfterDelete = await service.ListAsync(
                tenantId: "tenant-a",
                templateId: "template-1",
                cancellationToken: TestContext.Current.CancellationToken);

            listAfterDelete.IsError.ShouldBeFalse();
            listAfterDelete.Value.Fonts.ShouldBeEmpty();
            File.Exists(objectPath).ShouldBeFalse();
        }
        finally
        {
            CleanupDirectory(localDirectory);
        }
    }

    [Fact]
    public async Task Delete_ShouldKeepBlobUntilLastTemplateReferenceIsRemoved()
    {
        (TemplateFontAssetService service, string localDirectory) = CreateService();

        try
        {
            await using MemoryStream firstUploadContent = new([10, 20, 30]);
            var firstUpload = await service.UploadAsync(
                tenantId: "tenant-a",
                templateId: "template-one",
                fontKey: "shared/brand.ttf",
                fileName: "brand.ttf",
                contentType: "font/ttf",
                content: firstUploadContent,
                cancellationToken: TestContext.Current.CancellationToken);

            firstUpload.IsError.ShouldBeFalse();

            await using MemoryStream secondUploadContent = new([40, 50, 60]);
            var secondUpload = await service.UploadAsync(
                tenantId: "tenant-a",
                templateId: "template-two",
                fontKey: "shared/brand.ttf",
                fileName: "brand.ttf",
                contentType: "font/ttf",
                content: secondUploadContent,
                cancellationToken: TestContext.Current.CancellationToken);

            secondUpload.IsError.ShouldBeFalse();

            string objectPath = BuildLocalObjectPath(localDirectory, firstUpload.Value.ObjectKey);
            File.Exists(objectPath).ShouldBeTrue();

            var deleteFirst = await service.DeleteAsync(
                tenantId: "tenant-a",
                templateId: "template-one",
                fontKey: "shared/brand.ttf",
                cancellationToken: TestContext.Current.CancellationToken);

            deleteFirst.IsError.ShouldBeFalse();
            File.Exists(objectPath).ShouldBeTrue();

            var deleteSecond = await service.DeleteAsync(
                tenantId: "tenant-a",
                templateId: "template-two",
                fontKey: "shared/brand.ttf",
                cancellationToken: TestContext.Current.CancellationToken);

            deleteSecond.IsError.ShouldBeFalse();
            File.Exists(objectPath).ShouldBeFalse();
        }
        finally
        {
            CleanupDirectory(localDirectory);
        }
    }

    [Fact]
    public async Task List_ShouldReturnValidationError_WhenTenantIdIsMissing()
    {
        (TemplateFontAssetService service, string localDirectory) = CreateService();

        try
        {
            var result = await service.ListAsync(
                tenantId: string.Empty,
                templateId: "template-1",
                cancellationToken: TestContext.Current.CancellationToken);

            result.IsError.ShouldBeTrue();
            result.FirstError.Code.ShouldBe("Location.TemplateFonts.TenantIdRequired");
        }
        finally
        {
            CleanupDirectory(localDirectory);
        }
    }

    private static (TemplateFontAssetService Service, string LocalDirectory) CreateService()
    {
        string localDirectory = Path.Combine(Path.GetTempPath(), "teck-cloud", "location-font-tests", Guid.NewGuid().ToString("N"));

        var dbOptions = new DbContextOptionsBuilder<TemplateFontMetadataDbContext>()
            .UseInMemoryDatabase($"template-fonts-{Guid.NewGuid():N}")
            .Options;

        IDbContextFactory<TemplateFontMetadataDbContext> dbContextFactory = new TestDbContextFactory(dbOptions);

        TemplateFontStorageOptions storageOptions = new()
        {
            LocalDirectory = localDirectory,
            ObjectKeyTemplate = "tenant-fonts/{tenantId}/{fontKey}",
            MaxFontBytes = 1024 * 1024,
        };

        TemplateFontAssetService service = new(
            dbContextFactory,
            Options.Create(storageOptions),
            NullLogger<TemplateFontAssetService>.Instance);

        return (service, localDirectory);
    }

    private static string BuildLocalObjectPath(string localDirectory, string objectKey)
    {
        string normalizedObjectKey = objectKey.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(localDirectory, normalizedObjectKey);
    }

    private static void CleanupDirectory(string localDirectory)
    {
        if (Directory.Exists(localDirectory))
        {
            Directory.Delete(localDirectory, recursive: true);
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<TemplateFontMetadataDbContext> options)
        : IDbContextFactory<TemplateFontMetadataDbContext>
    {
        private readonly DbContextOptions<TemplateFontMetadataDbContext> options = options;

        public TemplateFontMetadataDbContext CreateDbContext()
        {
            return new TemplateFontMetadataDbContext(this.options);
        }

        public Task<TemplateFontMetadataDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(this.CreateDbContext());
        }
    }
}

#pragma warning restore CA2012
