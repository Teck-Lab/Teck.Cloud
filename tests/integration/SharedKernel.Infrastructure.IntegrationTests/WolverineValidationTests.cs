using ErrorOr;
using FluentValidation;
using JasperFx.CodeGeneration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Infrastructure.Messaging;
using Shouldly;
using Wolverine;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Proves the shared Wolverine FluentValidation middleware at its handler boundary.</summary>
public sealed class WolverineValidationTests
{
    /// <summary>Invalid messages stop before handler invocation while valid messages reach the handler.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task UseFoundationValidationRejectsInvalidMessagesBeforeHandlers()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var builder = Host.CreateApplicationBuilder();
        _ = builder.Services.AddSingleton<WolverineValidationRecorder>();
        _ = builder.Services.AddSingleton<WolverineResultProbeState>();
        _ = builder.UseWolverine(options =>
        {
            options.ApplicationAssembly = typeof(WolverineValidationTests).Assembly;
            _ = options.Discovery.IncludeAssembly(typeof(WolverineValidationTests).Assembly);
            _ = options.UseFoundationValidation();

            var output = Environment.GetEnvironmentVariable("TECK_CODEGEN_OUTPUT");
            if (!string.IsNullOrEmpty(output))
            {
                options.CodeGeneration.GeneratedCodeOutputPath = output;
                options.CodeGeneration.SourceCodeWritingEnabled = true;
            }

#if VERIFY_STATIC_HANDLERS
            options.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;
#else
            _ = options.UseRuntimeCompilation();
            options.CodeGeneration.TypeLoadMode = TypeLoadMode.Auto;
#endif
        });

        using var host = builder.Build();
        await host.StartAsync(cancellation).ConfigureAwait(true);
        try
        {
            var bus = host.Services.GetRequiredService<IMessageBus>();
            var recorder = host.Services.GetRequiredService<WolverineValidationRecorder>();
            var error = await Should.ThrowAsync<ValidationException>(() => bus.InvokeAsync(new WolverineValidationMessage(string.Empty), cancellation)).ConfigureAwait(true);
            error.Message.ShouldContain("Validation failure", Case.Sensitive);
            recorder.Calls.ShouldBe(0);

            await bus.InvokeAsync(new WolverineValidationMessage("Teck"), cancellation).ConfigureAwait(true);
            recorder.Calls.ShouldBe(1);

            var resultState = host.Services.GetRequiredService<WolverineResultProbeState>();
            var id = Guid.NewGuid();
            var success = await bus.InvokeAsync<ErrorOr<Guid>>(new WolverineResultProbe(id, ReturnError: false), cancellation).ConfigureAwait(true);
            success.IsError.ShouldBeFalse();
            success.Value.ShouldBe(id);
            resultState.SuccessfulCount.ShouldBe(1);

            var failure = await bus.InvokeAsync<ErrorOr<Guid>>(new WolverineResultProbe(Guid.NewGuid(), ReturnError: true), cancellation).ConfigureAwait(true);
            failure.IsError.ShouldBeTrue();
            failure.FirstError.Description.ShouldBe("Requested result failure.");
            resultState.SuccessfulCount.ShouldBe(1);
        }
        finally
        {
            await host.StopAsync(cancellation).ConfigureAwait(true);
        }
    }
}
