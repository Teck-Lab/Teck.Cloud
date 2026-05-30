using ErrorOr;
using Finbuckle.MultiTenant.Abstractions;
using Image.Generator.Application.Render.Features.SubmitRenderJob.V1;
using Mediator;
using NSubstitute;
using SharedKernel.Grpc.Contracts.Remote.V1.Labels;
using SharedKernel.Infrastructure.MultiTenant;
using Shouldly;

namespace Image.Generator.UnitTests.Grpc;

public sealed class EnqueueRenderJobCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenSenderReturnsError_ShouldReturnFailedResult()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<SubmitRenderJobCommand>(), Arg.Any<CancellationToken>())
            .Returns(Error.Unexpected(code: "ImageGenerator.RenderingFailed", description: "boom"));

        object sut = CreateHandler(sender, CreateTenantAccessor("tenant-a"));
        EnqueueRenderJobCommand command = BuildCommand();

        // Act
        EnqueueRenderJobRpcResult result = await ExecuteAsync(sut, command);

        // Assert
        result.Status.ShouldBe("failed");
        result.JobId.ShouldBe(Guid.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSenderSucceeds_ShouldReturnQueuedResultAndMapCommand()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        SubmitRenderJobResponse response = new()
        {
            JobId = Guid.Parse("00000000-0000-0000-0000-00000000ABCD"),
            Status = "queued",
            ImageUri = null,
            QueuedAtUtc = DateTimeOffset.UtcNow,
        };

        sender.Send(Arg.Any<SubmitRenderJobCommand>(), Arg.Any<CancellationToken>())
            .Returns(response);

        object sut = CreateHandler(sender, CreateTenantAccessor("tenant-b"));
        EnqueueRenderJobCommand command = BuildCommand();

        // Act
        EnqueueRenderJobRpcResult result = await ExecuteAsync(sut, command);

        // Assert
        result.Status.ShouldBe("queued");
        result.JobId.ShouldBe(response.JobId);

        await sender.Received(1).Send(
            Arg.Is<SubmitRenderJobCommand>(submitCommand =>
                submitCommand.JobId == command.JobId
                && submitCommand.DisplayId == command.DisplayId
                && submitCommand.TenantId == "tenant-b"
                && submitCommand.OutputType == command.OutputType
                && submitCommand.Template.Width == command.Template!.Width
                && submitCommand.Template.Elements.Count == command.Template.Elements.Count
                && submitCommand.Data["sku"] == "1001"),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisplayIdIsEmpty_ShouldReturnFailedWithoutCallingSender()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        object sut = CreateHandler(sender, CreateTenantAccessor("tenant-c"));
        EnqueueRenderJobCommand command = BuildCommand();
        command.DisplayId = Guid.Empty;

        // Act
        EnqueueRenderJobRpcResult result = await ExecuteAsync(sut, command);

        // Assert
        result.Status.ShouldBe("failed");
        result.JobId.ShouldBe(Guid.Empty);
        await sender.DidNotReceive().Send(Arg.Any<SubmitRenderJobCommand>(), Arg.Any<CancellationToken>());
    }

    private static EnqueueRenderJobCommand BuildCommand()
        => new()
        {
            JobId = Guid.Parse("00000000-0000-0000-0000-000000000123"),
            DisplayId = Guid.Parse("00000000-0000-0000-0000-000000000456"),
            OutputType = "png",
            PaletteColors = ["#FFFFFF", "#000000"],
            Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sku"] = "1001",
            },
            Template = new EnqueueRenderJobTemplateRpcItem
            {
                Width = 120,
                Height = 80,
                BackgroundColor = "#FFFFFF",
                Elements =
                [
                    new EnqueueRenderJobTemplateElementRpcItem
                    {
                        Type = "text",
                        Left = 10,
                        Top = 10,
                        Width = 100,
                        Height = 20,
                        Value = "Hello",
                        FontFamily = "Arial",
                        FontSize = 12,
                        ForegroundColor = "#000000",
                    },
                ],
            },
        };

    private static IMultiTenantContextAccessor<TenantDetails> CreateTenantAccessor(string tenantId)
    {
        var accessor = Substitute.For<IMultiTenantContextAccessor<TenantDetails>>();
        accessor.MultiTenantContext.Returns(new MultiTenantContext<TenantDetails>(new TenantDetails { Id = tenantId }));
        return accessor;
    }

    private static object CreateHandler(ISender sender, IMultiTenantContextAccessor<TenantDetails> tenantAccessor)
    {
        Type handlerType = Type.GetType("Image.Generator.Api.Grpc.V1.EnqueueRenderJobCommandHandler, Image.Generator.Api")!;
        return Activator.CreateInstance(handlerType, sender, tenantAccessor)!;
    }

    private static async ValueTask<EnqueueRenderJobRpcResult> ExecuteAsync(object handler, EnqueueRenderJobCommand command)
    {
        var executeMethod = handler.GetType().GetMethod("ExecuteAsync")!;
        var task = (Task<EnqueueRenderJobRpcResult>)executeMethod.Invoke(handler, [command, TestContext.Current.CancellationToken])!;
        return await task;
    }
}
