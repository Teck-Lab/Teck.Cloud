// <copyright file="CreateLocationNode.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Location.Application.Service.Abstractions;
using SharedKernel.Core.CQRS;

namespace Location.Application.Service.Features.CreateLocationNode.V1;

/// <summary>
/// Command to create a new location node.
/// </summary>
public sealed record CreateLocationNodeCommand(
    string TenantId,
    string Name,
    string? ParentLocationNodeId)
    : ICommand<ErrorOr<CreateLocationNodeResponse>>;

/// <summary>
/// Response returned after creating a location node.
/// </summary>
public sealed record CreateLocationNodeResponse
{
    /// <summary>
    /// Gets the identifier of the created location node.
    /// </summary>
    public string LocationNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the human-readable name of the created location node.
    /// </summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Handler for <see cref="CreateLocationNodeCommand"/>.
/// </summary>
public sealed class CreateLocationNodeCommandHandler(ILocationNodeWriteRepository writeRepository)
    : ICommandHandler<CreateLocationNodeCommand, ErrorOr<CreateLocationNodeResponse>>
{
    private readonly ILocationNodeWriteRepository writeRepository = writeRepository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<CreateLocationNodeResponse>> Handle(
        CreateLocationNodeCommand request,
        CancellationToken cancellationToken)
    {
        bool nameExists = await this.writeRepository.NameExistsAsync(
            request.TenantId,
            request.Name,
            cancellationToken).ConfigureAwait(false);

        if (nameExists)
        {
            return Error.Conflict(
                "LocationNode.NameExists",
                $"A location node with the name '{request.Name}' already exists.");
        }

        string locationNodeId = Guid.NewGuid().ToString("N");

        var snapshot = new LocationNodeSnapshot(
            LocationNodeId: locationNodeId,
            ParentLocationNodeId: request.ParentLocationNodeId,
            TemplateId: null,
            Name: request.Name,
            LocationGroupId: null,
            Aisle: null,
            Shelf: null);

        await this.writeRepository.CreateAsync(snapshot, cancellationToken).ConfigureAwait(false);

        return new CreateLocationNodeResponse
        {
            LocationNodeId = locationNodeId,
            Name = request.Name,
        };
    }
}
