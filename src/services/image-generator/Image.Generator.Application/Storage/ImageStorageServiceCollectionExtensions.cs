// <copyright file="ImageStorageServiceCollectionExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentStorage;
using FluentStorage.Blobs;
using Image.Generator.Application.Storage;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering FluentStorage-based image storage.
/// </summary>
public static class ImageStorageServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="IImageStorage"/> backed by FluentStorage (S3/MinIO).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFluentImageStorage(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection storageSection = configuration.GetSection("ImageStorage");

        string provider = storageSection["Provider"] ?? "minio";
        string baseUri = storageSection["BaseUri"] ?? string.Empty;
        string bucket = storageSection["Bucket"] ?? "teck-images";
        string localPath = storageSection["LocalPath"] ?? Path.Combine(Path.GetTempPath(), "teck-images");

#pragma warning disable CA2000
        IBlobStorage blobStorage = provider.ToLowerInvariant() switch
        {
            "minio" or "s3" => CreateMinIoBlobStorage(storageSection, bucket),
            _ => StorageFactory.Blobs.DirectoryFiles(localPath),
        };
#pragma warning restore CA2000

        string publicBaseUri = string.IsNullOrWhiteSpace(baseUri)
            ? $"file://{localPath}"
            : baseUri.TrimEnd('/');

        services.AddSingleton(blobStorage);
        services.AddSingleton<IImageStorage, FluentImageStorage>(_ =>
            new FluentImageStorage(
                _.GetRequiredService<IBlobStorage>(),
                $"{publicBaseUri}/{bucket}"));

        return services;
    }

    private static IBlobStorage CreateMinIoBlobStorage(IConfigurationSection section, string bucket)
    {
        string endpoint = section["Endpoint"] ?? "localhost:9000";
        string accessKey = section["AccessKey"] ?? string.Empty;
        string secretKey = section["SecretKey"] ?? string.Empty;
        string region = section["Region"] ?? "us-east-1";
        bool useSsl = section.GetValue<bool>("UseSsl");

        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "MinIO storage requires AccessKey and SecretKey. Configure ImageStorage:AccessKey and ImageStorage:SecretKey.");
        }

        string scheme = useSsl ? "https" : "http";
        string url = $"{scheme}://{endpoint}";

        return StorageFactory.Blobs.MinIO(
            accessKeyId: accessKey,
            secretAccessKey: secretKey,
            bucketName: bucket,
            awsRegion: region,
            minioServerUrl: url);
    }
}
