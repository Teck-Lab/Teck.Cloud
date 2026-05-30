// <copyright file="EnqueueRenderJobCommandHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Finbuckle.MultiTenant.Abstractions;
using Image.Generator.Application.Render.Features.SubmitRenderJob.V1;
using Mediator;
using SharedKernel.Grpc.Contracts.Remote.V1.Labels;
using SharedKernel.Infrastructure.MultiTenant;

namespace Image.Generator.Api.Grpc.V1;

/// <summary>
/// Handles internal image render enqueue RPC requests.
/// </summary>
internal sealed class EnqueueRenderJobCommandHandler(
    ISender sender,
    IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor = null)
    : FastEndpoints.ICommandHandler<EnqueueRenderJobCommand, EnqueueRenderJobRpcResult>
{
    private readonly ISender sender = sender;
    private readonly IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor = tenantContextAccessor;

    /// <inheritdoc/>
    public async Task<EnqueueRenderJobRpcResult> ExecuteAsync(EnqueueRenderJobCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.DisplayId == Guid.Empty)
        {
            return new EnqueueRenderJobRpcResult
            {
                JobId = Guid.Empty,
                Status = "failed",
            };
        }

        string tenantId = this.tenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id ?? string.Empty;

        SubmitRenderJobCommand submitCommand = new(
            command.JobId,
            command.DisplayId,
            tenantId,
            command.OutputType,
            command.PaletteColors.ToArray(),
            MapTemplate(command),
            new Dictionary<string, string>(command.Data, StringComparer.OrdinalIgnoreCase));

        ErrorOr<SubmitRenderJobResponse> submitResponse = await this.sender.Send(submitCommand, ct).ConfigureAwait(false);
        if (submitResponse.IsError)
        {
            return new EnqueueRenderJobRpcResult
            {
                JobId = Guid.Empty,
                Status = "failed",
            };
        }

        return new EnqueueRenderJobRpcResult
        {
            JobId = submitResponse.Value.JobId,
            Status = submitResponse.Value.Status,
        };
    }

    private static SubmitRenderJobTemplateRequest MapTemplate(EnqueueRenderJobCommand command)
    {
        if (command.Template is null)
        {
            return BuildLegacyTemplate(command.DisplayId, command.LayoutName, command.Zones);
        }

        return new SubmitRenderJobTemplateRequest
        {
            Width = command.Template.Width,
            Height = command.Template.Height,
            BackgroundColor = command.Template.BackgroundColor,
            Elements = command.Template.Elements
                .Select(element => new SubmitRenderJobTemplateElementRequest
                {
                    Type = element.Type,
                    Left = element.Left,
                    Top = element.Top,
                    Width = element.Width,
                    Height = element.Height,
                    Value = element.Value,
                    Binding = element.Binding,
                    Format = element.Format,
                    FontFamily = element.FontFamily,
                    FontSize = element.FontSize,
                    FontWeight = element.FontWeight,
                    HorizontalAlign = element.HorizontalAlign,
                    ForegroundColor = element.ForegroundColor,
                    BackgroundColor = element.BackgroundColor,
                    StrokeWidth = element.StrokeWidth,
                    CornerRadius = element.CornerRadius,
                    Fill = element.Fill,
                })
                .ToList(),
        };
    }

    private static SubmitRenderJobTemplateRequest BuildLegacyTemplate(
        Guid displayId,
        string layoutName,
        IEnumerable<EnqueueRenderJobZoneRpcItem> zones)
    {
        const float cardWidth = 360;
        const float cardHeight = 250;
        const float padding = 24;
        const float gap = 24;
        const float headerHeight = 110;

        List<SubmitRenderJobTemplateElementRequest> elements =
        [
            new SubmitRenderJobTemplateElementRequest
            {
                Type = "text",
                Left = padding,
                Top = 24,
                Width = 1000,
                Height = 42,
                Value = "Image Generator Render",
                FontFamily = "Arial",
                FontSize = 32,
                FontWeight = "bold",
                ForegroundColor = "#101010",
            },
            new SubmitRenderJobTemplateElementRequest
            {
                Type = "text",
                Left = padding,
                Top = 70,
                Width = 1000,
                Height = 30,
                Value = $"Display: {displayId}",
                FontFamily = "Arial",
                FontSize = 16,
                ForegroundColor = "#303030",
            },
            new SubmitRenderJobTemplateElementRequest
            {
                Type = "text",
                Left = padding,
                Top = 92,
                Width = 1000,
                Height = 30,
                Value = $"Layout: {layoutName}",
                FontFamily = "Arial",
                FontSize = 16,
                ForegroundColor = "#303030",
            },
        ];

        int index = 0;
        foreach (EnqueueRenderJobZoneRpcItem zone in zones)
        {
            int row = index / 3;
            int column = index % 3;

            float cardLeft = padding + (column * (cardWidth + gap));
            float cardTop = headerHeight + padding + (row * (cardHeight + gap));

            elements.Add(new SubmitRenderJobTemplateElementRequest
            {
                Type = "rectangle",
                Left = cardLeft,
                Top = cardTop,
                Width = cardWidth,
                Height = cardHeight,
                Fill = true,
                BackgroundColor = "#FFFFFF",
                ForegroundColor = "#404040",
                StrokeWidth = 2,
                CornerRadius = 18,
            });

            elements.Add(new SubmitRenderJobTemplateElementRequest
            {
                Type = "text",
                Left = cardLeft + 18,
                Top = cardTop + 18,
                Width = 320,
                Height = 28,
                Value = $"Zone {zone.ZoneIndex}",
                FontFamily = "Arial",
                FontSize = 22,
                FontWeight = "bold",
                ForegroundColor = "#101010",
            });

            elements.Add(new SubmitRenderJobTemplateElementRequest
            {
                Type = "qrcode",
                Left = cardLeft + 80,
                Top = cardTop + 52,
                Width = 200,
                Height = 160,
                Value = $"display:{displayId:N}|layout:{layoutName}|zone:{zone.ZoneIndex}|product:{zone.ProductId:N}",
            });

            index++;
        }

        int rows = Math.Max(1, (int)Math.Ceiling(index / 3d));
        int width = (int)((padding * 2) + (3 * cardWidth) + (2 * gap));
        int height = (int)(headerHeight + (padding * 2) + (rows * cardHeight) + ((rows - 1) * gap));

        return new SubmitRenderJobTemplateRequest
        {
            Width = width,
            Height = height,
            BackgroundColor = "#FFFFFF",
            Elements = elements,
        };
    }
}
