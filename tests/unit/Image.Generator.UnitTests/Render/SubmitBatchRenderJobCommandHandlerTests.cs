using ErrorOr;
using Image.Generator.Application.Render.Features.SubmitBatchRenderJob.V1;
using Image.Generator.Application.Render.Features.SubmitRenderJob.V1;
using Image.Generator.Application.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Image.Generator.UnitTests.Render;

public sealed class SubmitBatchRenderJobCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenBatchIsEmpty_ShouldReturnValidationError()
    {
        // Arrange
        SubmitBatchRenderJobCommand command = BuildCommand([]);
        SubmitBatchRenderJobCommandHandler sut = BuildSut();

        // Act
        ErrorOr<SubmitBatchRenderJobResponse> result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("ImageGenerator.Batch.Empty");
    }

    [Fact]
    public async Task Handle_WhenAllItemsRenderSuccessfully_ShouldReturnRenderedResults()
    {
        // Arrange
        IReadOnlyList<BatchRenderItem> items =
        [
            new BatchRenderItem(Guid.Parse("00000000-0000-0000-0000-000000000011"), new Dictionary<string, string> { ["name"] = "A" }),
            new BatchRenderItem(Guid.Parse("00000000-0000-0000-0000-000000000012"), new Dictionary<string, string> { ["name"] = "B" }),
        ];

        SubmitBatchRenderJobCommand command = BuildCommand(items);
        SubmitBatchRenderJobCommandHandler sut = BuildSut();

        // Act
        ErrorOr<SubmitBatchRenderJobResponse> result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Results.Count.ShouldBe(2);
        result.Value.Results.All(batchRenderResult => batchRenderResult.Status == "rendered").ShouldBeTrue();
        result.Value.Results.All(batchRenderResult => batchRenderResult.ImageUri is not null).ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WhenOneItemFails_ShouldReturnPartialFailureResults()
    {
        // Arrange
        IReadOnlyList<BatchRenderItem> items =
        [
            new BatchRenderItem(Guid.Parse("00000000-0000-0000-0000-000000000021"), new Dictionary<string, string> { ["name"] = "ok" }),
            new BatchRenderItem(Guid.Parse("00000000-0000-0000-0000-000000000022"), null!),
        ];

        SubmitBatchRenderJobCommand command = BuildCommand(items);
        SubmitBatchRenderJobCommandHandler sut = BuildSut();

        // Act
        ErrorOr<SubmitBatchRenderJobResponse> result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Results.Count.ShouldBe(2);
        result.Value.Results.Count(batchRenderResult => batchRenderResult.Status == "failed").ShouldBe(1);
        result.Value.Results.Count(batchRenderResult => batchRenderResult.Status == "rendered").ShouldBe(1);
        result.Value.Results.Single(batchRenderResult => batchRenderResult.Status == "failed").ErrorCode.ShouldBe("ImageGenerator.RenderingFailed");
    }

    private static SubmitBatchRenderJobCommand BuildCommand(IReadOnlyList<BatchRenderItem> items)
        => new(
            BatchId: Guid.NewGuid(),
            TenantId: "tenant-1",
            OutputType: "png",
            PaletteColors: Array.Empty<string>(),
            Template: new SubmitRenderJobTemplateRequest
            {
                Width = 120,
                Height = 80,
                BackgroundColor = "#FFFFFF",
                Elements =
                [
                    new SubmitRenderJobTemplateElementRequest
                    {
                        Type = "text",
                        Left = 10,
                        Top = 10,
                        Width = 100,
                        Height = 20,
                        Value = "Hello",
                        FontFamily = "Arial",
                        FontSize = 14,
                        ForegroundColor = "#000000",
                    },
                ],
            },
            Items: items);

    private static SubmitBatchRenderJobCommandHandler BuildSut()
    {
        IOptions<RenderProcessingOptions> renderOptions = Options.Create(new RenderProcessingOptions
        {
            MaxConcurrentRenders = 2,
            QueueTimeoutMilliseconds = 1000,
            EnableRenderCache = false,
            StrictBindings = false,
        });

        ITenantFontAssetStore tenantFontAssetStore = Substitute.For<ITenantFontAssetStore>();
        tenantFontAssetStore
            .EnsureFontsAvailableAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));

        IFusionCache fusionCache = Substitute.For<IFusionCache>();
        IMessageBus messageBus = Substitute.For<IMessageBus>();

        IImageStorage imageStorage = Substitute.For<IImageStorage>();
        imageStorage
            .SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new ValueTask<Uri>(new Uri($"https://cdn.test/{callInfo.ArgAt<string>(0)}")));

        ILogger<SubmitBatchRenderJobCommandHandler> logger = Substitute.For<ILogger<SubmitBatchRenderJobCommandHandler>>();

        return new SubmitBatchRenderJobCommandHandler(
            renderOptions,
            tenantFontAssetStore,
            fusionCache,
            messageBus,
            imageStorage,
            logger);
    }
}
