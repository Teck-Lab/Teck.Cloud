// <copyright file="FluentImageStorageTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentStorage.Blobs;
using Image.Generator.Application.Storage;
using NSubstitute;
using Shouldly;

namespace Image.Generator.UnitTests.Storage;

public sealed class FluentImageStorageTests
{
    private readonly IBlobStorage _blobStorage;
    private readonly FluentImageStorage _sut;

    public FluentImageStorageTests()
    {
        _blobStorage = Substitute.For<IBlobStorage>();
        _sut = new FluentImageStorage(_blobStorage, "http://storage.local:9000/teck-images");
    }

    [Fact]
    public async Task SaveAsync_WithValidParams_ShouldWriteToBlobStorage()
    {
        // Arrange
        using var stream = new MemoryStream([0x01, 0x02, 0x03]);

        // Act
        Uri result = await _sut.SaveAsync("test/image.png", stream, "image/png", CancellationToken.None);

        // Assert
        await _blobStorage.Received(1)
            .WriteAsync("images/test/image.png", Arg.Any<Stream>(), false, Arg.Any<CancellationToken>());
        result.ToString().ShouldBe("http://storage.local:9000/teck-images/images/test/image.png");
    }

    [Fact]
    public async Task SaveAsync_WithNullPath_ShouldThrowArgumentException()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act + Assert
        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await _sut.SaveAsync(string.Empty, stream, "image/png", CancellationToken.None));
        exception.ParamName.ShouldBe("path");
    }

    [Fact]
    public async Task SaveAsync_WithNullStream_ShouldThrowArgumentNullException()
    {
        // Act + Assert
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _sut.SaveAsync("test.png", null!, "image/png", CancellationToken.None));
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public async Task SaveAsync_WithNullContentType_ShouldThrowArgumentException()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act + Assert
        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await _sut.SaveAsync("test.png", stream, string.Empty, CancellationToken.None));
        exception.ParamName.ShouldBe("contentType");
    }

    [Fact]
    public async Task SaveAsync_WithTrailingSlashBaseUri_ShouldNotDoubleSlash()
    {
        // Arrange
        var storage = new FluentImageStorage(_blobStorage, "http://storage.local:9000/teck-images/");
        using var stream = new MemoryStream();

        // Act
        Uri result = await storage.SaveAsync("test.png", stream, "image/png", CancellationToken.None);

        // Assert
        result.ToString().ShouldBe("http://storage.local:9000/teck-images/images/test.png");
    }

    [Fact]
    public async Task GetAsync_WithValidUri_ShouldOpenRead()
    {
        // Arrange
        var uri = new Uri("http://storage.local:9000/teck-images/images/test.png");
        var expectedStream = new MemoryStream([0x01, 0x02]);
        _blobStorage.OpenReadAsync("images/test.png", Arg.Any<CancellationToken>())
            .Returns(expectedStream);

        // Act
        Stream result = await _sut.GetAsync(uri, CancellationToken.None);

        // Assert
        result.ShouldBe(expectedStream);
    }

    [Fact]
    public async Task GetAsync_WithNullUri_ShouldThrowArgumentNullException()
    {
        // Act + Assert
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _sut.GetAsync(null!, CancellationToken.None));
        exception.ParamName.ShouldBe("imageUri");
    }

    [Fact]
    public async Task DeleteAsync_WithValidUri_ShouldDeleteBlob()
    {
        // Arrange
        var uri = new Uri("http://storage.local:9000/teck-images/images/test.png");

        // Act
        await _sut.DeleteAsync(uri, CancellationToken.None);

        // Assert
        await _blobStorage.Received(1)
            .DeleteAsync(Arg.Is<string[]>(arr => arr.Length == 1 && arr[0] == "images/test.png"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WithNullUri_ShouldThrowArgumentNullException()
    {
        // Act + Assert
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _sut.DeleteAsync(null!, CancellationToken.None));
        exception.ParamName.ShouldBe("imageUri");
    }

    [Fact]
    public async Task GetBlobPath_WithNonMatchingUri_ShouldFallbackToLastSegment()
    {
        // Arrange
        var uri = new Uri("http://other.host/some/path/image.png");
        var expectedStream = new MemoryStream();
        _blobStorage.OpenReadAsync("images/image.png", Arg.Any<CancellationToken>())
            .Returns(expectedStream);

        // Act
        Stream result = await _sut.GetAsync(uri, CancellationToken.None);

        // Assert
        result.ShouldBe(expectedStream);
    }
}
