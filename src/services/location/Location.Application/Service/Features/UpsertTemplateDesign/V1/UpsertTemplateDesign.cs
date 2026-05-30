// <copyright file="UpsertTemplateDesign.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.UpsertTemplateDesign.V1;

public sealed record UpsertTemplateDesignCommand(
    string TemplateId,
    string Name,
    int Width,
    int Height,
    string BackgroundColor,
    string ElementsJson,
    string DefaultsJson)
    : ICommand<ErrorOr<UpsertTemplateDesignResponse>>;

public sealed class UpsertTemplateDesignCommandHandler(ITemplateDesignWriteRepository writeRepository)
    : ICommandHandler<UpsertTemplateDesignCommand, ErrorOr<UpsertTemplateDesignResponse>>
{
    private readonly ITemplateDesignWriteRepository writeRepository = writeRepository;

    public async ValueTask<ErrorOr<UpsertTemplateDesignResponse>> Handle(
        UpsertTemplateDesignCommand request,
        CancellationToken cancellationToken)
    {
        // TenantId will be resolved from context in a real implementation
        var snapshot = new TemplateDesignSnapshot(
            TenantId: "_current",
            request.TemplateId,
            request.Name,
            request.Width,
            request.Height,
            request.BackgroundColor,
            request.ElementsJson,
            request.DefaultsJson);

        await this.writeRepository.UpsertAsync(snapshot, cancellationToken).ConfigureAwait(false);

        return new UpsertTemplateDesignResponse
        {
            TemplateId = request.TemplateId,
            Name = request.Name,
        };
    }
}

public sealed record UpsertTemplateDesignResponse
{
    public string TemplateId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}
