// <copyright file="InMemoryLabelRenderJobRunner.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using FastEndpoints;
using Grpc.Core;
using SharedKernel.Grpc.Contracts.Remote.V1.Labels;

namespace Device.Infrastructure.Assignments;

internal sealed class InMemoryLabelRenderJobRunner : ILabelRenderJobRunner
{
    public async ValueTask<LabelRenderJobResult> EnqueueAsync(
        Guid jobId,
        Guid displayId,
        string layoutName,
        IReadOnlyCollection<LabelRenderJobZoneItem> zones,
        ResolvedTemplateDesignSnapshot? templateDesign,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        EnqueueRenderJobCommand command = new()
        {
            JobId = jobId,
            DisplayId = displayId,
            LayoutName = layoutName,
            Template = BuildTemplate(displayId, layoutName, zones, templateDesign),
        };

        foreach (LabelRenderJobZoneItem zone in zones)
        {
            command.Zones.Add(new EnqueueRenderJobZoneRpcItem
            {
                ZoneIndex = zone.ZoneIndex,
                ProductId = zone.ProductId,
            });
        }

        try
        {
            EnqueueRenderJobRpcResult rpcResult = await command
                .RemoteExecuteAsync(new CallOptions(cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (rpcResult is not null)
            {
                return new LabelRenderJobResult(
                    JobId: rpcResult.JobId,
                    Status: rpcResult.Status);
            }
        }
        catch (RpcException)
        {
            // Fall through to local behavior when transport is unavailable.
        }
        catch (InvalidOperationException)
        {
            // Fall through when FE remote registration is not configured.
        }

        return new LabelRenderJobResult(
            JobId: jobId != Guid.Empty ? jobId : Guid.NewGuid(),
            Status: "queued");
    }

    private static EnqueueRenderJobTemplateRpcItem BuildTemplate(
        Guid displayId,
        string layoutName,
        IReadOnlyCollection<LabelRenderJobZoneItem> zones,
        ResolvedTemplateDesignSnapshot? templateDesign)
    {
        if (templateDesign is not null)
        {
            return BuildTemplateFromDesign(templateDesign, displayId, layoutName, zones);
        }

        const float cardWidth = 360;
        const float cardHeight = 250;
        const float padding = 24;
        const float gap = 24;
        const float headerHeight = 110;

        List<EnqueueRenderJobTemplateElementRpcItem> elements =
        [
            new EnqueueRenderJobTemplateElementRpcItem
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
            new EnqueueRenderJobTemplateElementRpcItem
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
            new EnqueueRenderJobTemplateElementRpcItem
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
        foreach (LabelRenderJobZoneItem zone in zones)
        {
            int row = index / 3;
            int column = index % 3;

            float cardLeft = padding + (column * (cardWidth + gap));
            float cardTop = headerHeight + padding + (row * (cardHeight + gap));

            elements.Add(new EnqueueRenderJobTemplateElementRpcItem
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

            elements.Add(new EnqueueRenderJobTemplateElementRpcItem
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

            elements.Add(new EnqueueRenderJobTemplateElementRpcItem
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

        return new EnqueueRenderJobTemplateRpcItem
        {
            Width = width,
            Height = height,
            BackgroundColor = "#FFFFFF",
            Elements = elements,
        };
    }

    private static EnqueueRenderJobTemplateRpcItem BuildTemplateFromDesign(
        ResolvedTemplateDesignSnapshot design,
        Guid displayId,
        string layoutName,
        IReadOnlyCollection<LabelRenderJobZoneItem> zones)
    {
        _ = layoutName;

        List<EnqueueRenderJobTemplateElementRpcItem> elements = [];

        // Header elements are hardcoded defaults until ElementsJson parsing is implemented.
        const float padding = 24;

        elements.Add(new EnqueueRenderJobTemplateElementRpcItem
        {
            Type = "text",
            Left = padding,
            Top = 24,
            Width = 1000,
            Height = 42,
            Value = design.Name,
            FontFamily = "Arial",
            FontSize = 32,
            FontWeight = "bold",
            ForegroundColor = "#101010",
        });

        elements.Add(new EnqueueRenderJobTemplateElementRpcItem
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
        });

        int index = 0;
        foreach (LabelRenderJobZoneItem zone in zones)
        {
            elements.Add(new EnqueueRenderJobTemplateElementRpcItem
            {
                Type = "text",
                Left = padding,
                Top = 110 + (index * 40),
                Width = 1000,
                Height = 30,
                Value = $"Zone {zone.ZoneIndex}: {zone.ProductId:N}",
                FontFamily = "Arial",
                FontSize = 16,
                ForegroundColor = "#303030",
            });
            index++;
        }

        return new EnqueueRenderJobTemplateRpcItem
        {
            Width = design.Width,
            Height = design.Height,
            BackgroundColor = design.BackgroundColor,
            Elements = elements,
        };
    }
}
