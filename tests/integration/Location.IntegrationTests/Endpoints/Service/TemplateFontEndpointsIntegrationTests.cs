using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Location.Application.Service.Abstractions;
using Location.IntegrationTests.TestSupport;
using Shouldly;

#pragma warning disable CA2012
#pragma warning disable CA2000

namespace Location.IntegrationTests.Endpoints.Service;

[Collection("LocationIntegrationTests")]
public sealed class TemplateFontEndpointsIntegrationTests
{
    [Fact]
    public async Task UploadListDelete_ShouldPersistAndRemoveTemplateFont()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        string tenantId = $"tenant-{Guid.NewGuid():N}";
        string templateId = $"template-{Guid.NewGuid():N}";
        const string fontKey = "fonts/Inter-Regular.ttf";

        using MultipartFormDataContent formData = CreateFontUpload("Inter-Regular.ttf", "font/ttf", [1, 2, 3, 4, 5]);

        using HttpRequestMessage uploadRequest = new(HttpMethod.Post, $"/location/v1/Service/Templates/{templateId}/Fonts/{fontKey}")
        {
            Content = formData,
        };
        uploadRequest.Headers.Add("X-TenantId", tenantId);

        HttpResponseMessage uploadResponse = await host.Client.SendAsync(uploadRequest, TestContext.Current.CancellationToken);
        string uploadBody = await uploadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        uploadResponse.StatusCode.ShouldBe(HttpStatusCode.Created, uploadBody);

        TemplateFontUploadResponse? uploaded = await uploadResponse.Content
            .ReadFromJsonAsync<TemplateFontUploadResponse>(cancellationToken: TestContext.Current.CancellationToken);

        uploaded.ShouldNotBeNull();
        uploaded.TenantId.ShouldBe(tenantId);
        uploaded.TemplateId.ShouldBe(templateId);
        uploaded.FontKey.ShouldBe(fontKey);

        string objectPath = BuildObjectPath(host.LocalStorageDirectory, uploaded.ObjectKey);
        File.Exists(objectPath).ShouldBeTrue();

        using HttpRequestMessage listRequest = new(HttpMethod.Get, $"/location/v1/Service/Templates/{templateId}/Fonts");
        listRequest.Headers.Add("X-TenantId", tenantId);

        HttpResponseMessage listResponse = await host.Client.SendAsync(listRequest, TestContext.Current.CancellationToken);
        string listBody = await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, listBody);

        TemplateFontListResponse? listed = await listResponse.Content
            .ReadFromJsonAsync<TemplateFontListResponse>(cancellationToken: TestContext.Current.CancellationToken);

        listed.ShouldNotBeNull();
        listed.TemplateId.ShouldBe(templateId);
        listed.Fonts.Count.ShouldBe(1);
        listed.Fonts[0].FontKey.ShouldBe(fontKey);

        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/location/v1/Service/Templates/{templateId}/Fonts/{fontKey}");
        deleteRequest.Headers.Add("X-TenantId", tenantId);

        HttpResponseMessage deleteResponse = await host.Client.SendAsync(deleteRequest, TestContext.Current.CancellationToken);
        string deleteBody = await deleteResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent, deleteBody);

        using HttpRequestMessage listAfterDeleteRequest = new(HttpMethod.Get, $"/location/v1/Service/Templates/{templateId}/Fonts");
        listAfterDeleteRequest.Headers.Add("X-TenantId", tenantId);

        HttpResponseMessage listAfterDeleteResponse = await host.Client.SendAsync(listAfterDeleteRequest, TestContext.Current.CancellationToken);
        string listAfterDeleteBody = await listAfterDeleteResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        listAfterDeleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK, listAfterDeleteBody);

        TemplateFontListResponse? listAfterDelete = await listAfterDeleteResponse.Content
            .ReadFromJsonAsync<TemplateFontListResponse>(cancellationToken: TestContext.Current.CancellationToken);

        listAfterDelete.ShouldNotBeNull();
        listAfterDelete.Fonts.ShouldBeEmpty();
        File.Exists(objectPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_ShouldKeepBlobUntilLastTemplateReferenceIsRemoved()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        string tenantId = $"tenant-{Guid.NewGuid():N}";
        string templateOne = $"template-a-{Guid.NewGuid():N}";
        string templateTwo = $"template-b-{Guid.NewGuid():N}";
        const string fontKey = "shared/Brand.ttf";

        TemplateFontUploadResponse firstUpload = await UploadAsync(
            host,
            tenantId,
            templateOne,
            fontKey,
            "brand-a.ttf",
            [10, 20, 30]);

        await UploadAsync(
            host,
            tenantId,
            templateTwo,
            fontKey,
            "brand-b.ttf",
            [40, 50, 60]);

        string objectPath = BuildObjectPath(host.LocalStorageDirectory, firstUpload.ObjectKey);
        File.Exists(objectPath).ShouldBeTrue();

        HttpResponseMessage deleteTemplateOneResponse = await DeleteAsync(host, tenantId, templateOne, fontKey);
        deleteTemplateOneResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        File.Exists(objectPath).ShouldBeTrue();

        HttpResponseMessage deleteTemplateTwoResponse = await DeleteAsync(host, tenantId, templateTwo, fontKey);
        deleteTemplateTwoResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        File.Exists(objectPath).ShouldBeFalse();
    }

    [Fact]
    public async Task List_ShouldReturnBadRequest_WhenTenantHeaderIsMissing()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri("/location/v1/Service/Templates/template-1/Fonts", UriKind.Relative),
            TestContext.Current.CancellationToken);

        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, responseBody);
        ShouldContainErrorCode(responseBody, "Location.TemplateFonts.TenantIdRequired");
    }

    [Fact]
    public async Task Upload_ShouldReturnBadRequest_WhenTenantHeaderIsMissing()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        using MultipartFormDataContent formData = CreateFontUpload("Inter-Regular.ttf", "font/ttf", [1, 2, 3]);
        using HttpRequestMessage request = new(HttpMethod.Post, "/location/v1/Service/Templates/template-1/Fonts/fonts/Inter-Regular.ttf")
        {
            Content = formData,
        };

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, responseBody);
        ShouldContainErrorCode(responseBody, "Location.TemplateFonts.TenantIdRequired");
    }

    [Fact]
    public async Task Upload_ShouldReturnBadRequest_WhenFileIsMissing()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        using MultipartFormDataContent formData = [];
        formData.Add(new StringContent("placeholder"), "Metadata");
        using HttpRequestMessage request = new(HttpMethod.Post, "/location/v1/Service/Templates/template-1/Fonts/fonts/Inter-Regular.ttf")
        {
            Content = formData,
        };
        request.Headers.Add("X-TenantId", "tenant-1");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, responseBody);
        ShouldContainErrorCode(responseBody, "Location.TemplateFonts.FileRequired");
    }

    [Fact]
    public async Task Upload_ShouldReturnBadRequest_WhenBodyIsNotMultipart()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        using HttpRequestMessage request = new(HttpMethod.Post, "/location/v1/Service/Templates/template-1/Fonts/fonts/Inter-Regular.ttf")
        {
            Content = JsonContent.Create(new { fileName = "Inter-Regular.ttf", payload = "AQID" }),
        };
        request.Headers.Add("X-TenantId", "tenant-1");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType, responseBody);
    }

    [Fact]
    public async Task Upload_ShouldReturnBadRequest_WhenFileExceedsConfiguredMaxBytes()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        byte[] largePayload = new byte[(1024 * 1024) + 1];
        using MultipartFormDataContent formData = CreateFontUpload("too-large.ttf", "font/ttf", largePayload);
        using HttpRequestMessage request = new(HttpMethod.Post, "/location/v1/Service/Templates/template-1/Fonts/fonts/TooLarge.ttf")
        {
            Content = formData,
        };
        request.Headers.Add("X-TenantId", "tenant-1");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, responseBody);
        ShouldContainErrorCode(responseBody, "Location.TemplateFonts.FileTooLarge");
    }

    [Fact]
    public async Task Upload_ShouldSucceed_WhenFileSizeEqualsConfiguredMaxBytes()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        const int maxBytes = 1024 * 1024;
        byte[] payload = new byte[maxBytes];
        for (int index = 0; index < payload.Length; index++)
        {
            payload[index] = (byte)(index % 251);
        }

        string tenantId = $"tenant-{Guid.NewGuid():N}";
        string templateId = $"template-{Guid.NewGuid():N}";
        const string fontKey = "fonts/Exact-Limit.ttf";

        TemplateFontUploadResponse upload = await UploadAsync(
            host,
            tenantId,
            templateId,
            fontKey,
            "exact-limit.ttf",
            payload);

        upload.SizeBytes.ShouldBe(maxBytes);

        string objectPath = BuildObjectPath(host.LocalStorageDirectory, upload.ObjectKey);
        File.Exists(objectPath).ShouldBeTrue();

        FileInfo persistedFile = new(objectPath);
        persistedFile.Length.ShouldBe(maxBytes);
    }

    [Fact]
    public async Task Upload_ShouldDefaultContentType_WhenMultipartFileContentTypeMissing()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        string tenantId = $"tenant-{Guid.NewGuid():N}";
        string templateId = $"template-{Guid.NewGuid():N}";

        using MultipartFormDataContent formData = CreateFontUploadWithoutContentType("no-content-type.ttf", [1, 2, 3, 4]);
        using HttpRequestMessage request = new(HttpMethod.Post, $"/location/v1/Service/Templates/{templateId}/Fonts/fonts/No-Content-Type.ttf")
        {
            Content = formData,
        };
        request.Headers.Add("X-TenantId", tenantId);

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, responseBody);

        TemplateFontUploadResponse? uploaded = await response.Content
            .ReadFromJsonAsync<TemplateFontUploadResponse>(cancellationToken: TestContext.Current.CancellationToken);

        uploaded.ShouldNotBeNull();
        uploaded.ContentType.ShouldBe("font/ttf");
    }

    [Fact]
    public async Task Upload_ShouldDecodeEncodedSpacesInFontKey()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        string tenantId = $"tenant-{Guid.NewGuid():N}";
        string templateId = $"template-{Guid.NewGuid():N}";
        const string encodedFontKey = "fonts/My%20Brand.ttf";

        using MultipartFormDataContent formData = CreateFontUpload("my-brand.ttf", "font/ttf", [1, 3, 5, 7]);
        using HttpRequestMessage request = new(HttpMethod.Post, $"/location/v1/Service/Templates/{templateId}/Fonts/{encodedFontKey}")
        {
            Content = formData,
        };
        request.Headers.Add("X-TenantId", tenantId);

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, responseBody);

        TemplateFontUploadResponse? uploaded = await response.Content
            .ReadFromJsonAsync<TemplateFontUploadResponse>(cancellationToken: TestContext.Current.CancellationToken);

        uploaded.ShouldNotBeNull();
        uploaded.FontKey.ShouldBe("fonts/My Brand.ttf");
    }

    [Fact]
    public async Task Upload_ShouldNormalizeBackslashesInFontKey_FromUrlEncodedPath()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        string tenantId = $"tenant-{Guid.NewGuid():N}";
        string templateId = $"template-{Guid.NewGuid():N}";
        const string encodedFontKey = "fonts%5Csubdir%5CBackslash.ttf";

        using MultipartFormDataContent formData = CreateFontUpload("backslash.ttf", "font/ttf", [2, 4, 6, 8]);
        using HttpRequestMessage request = new(HttpMethod.Post, $"/location/v1/Service/Templates/{templateId}/Fonts/{encodedFontKey}")
        {
            Content = formData,
        };
        request.Headers.Add("X-TenantId", tenantId);

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, responseBody);

        TemplateFontUploadResponse? uploaded = await response.Content
            .ReadFromJsonAsync<TemplateFontUploadResponse>(cancellationToken: TestContext.Current.CancellationToken);

        uploaded.ShouldNotBeNull();
        uploaded.FontKey.ShouldBe("fonts/subdir/Backslash.ttf");
    }

    [Fact]
    public async Task Upload_ShouldPreserveEncodedForwardSlashesInFontKey()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        string tenantId = $"tenant-{Guid.NewGuid():N}";
        string templateId = $"template-{Guid.NewGuid():N}";
        const string encodedFontKey = "fonts%2Fnested%2FEncodedSlash.ttf";

        using MultipartFormDataContent formData = CreateFontUpload("encoded-slash.ttf", "font/ttf", [11, 22, 33, 44]);
        using HttpRequestMessage request = new(HttpMethod.Post, $"/location/v1/Service/Templates/{templateId}/Fonts/{encodedFontKey}")
        {
            Content = formData,
        };
        request.Headers.Add("X-TenantId", tenantId);

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, responseBody);

        TemplateFontUploadResponse? uploaded = await response.Content
            .ReadFromJsonAsync<TemplateFontUploadResponse>(cancellationToken: TestContext.Current.CancellationToken);

        uploaded.ShouldNotBeNull();
        uploaded.FontKey.ShouldBe("fonts%2Fnested%2FEncodedSlash.ttf");
    }

    [Fact]
    public async Task Upload_ShouldReturnBadRequest_WhenMultipartBoundaryIsMissing()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        using HttpRequestMessage request = new(HttpMethod.Post, "/location/v1/Service/Templates/template-1/Fonts/fonts/CorruptBoundary.ttf")
        {
            Content = new StringContent("invalid multipart payload"),
        };
        request.Headers.Add("X-TenantId", "tenant-1");
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, responseBody);
        ShouldContainErrorCode(responseBody, "request.malformedMultipart");
    }

    [Fact]
    public async Task Upload_ShouldReturnBadRequest_WhenFilePartIsEmpty()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        using MultipartFormDataContent formData = CreateFontUpload("empty.ttf", "font/ttf", []);
        using HttpRequestMessage request = new(HttpMethod.Post, "/location/v1/Service/Templates/template-1/Fonts/fonts/Empty.ttf")
        {
            Content = formData,
        };
        request.Headers.Add("X-TenantId", "tenant-1");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, responseBody);
        ShouldContainErrorCode(responseBody, "Location.TemplateFonts.FileRequired");
    }

    [Fact]
    public async Task Delete_ShouldReturnNotFound_WhenTemplateFontDoesNotExist()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        HttpResponseMessage response = await DeleteAsync(host, "tenant-1", "template-1", "fonts/unknown.ttf");
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, responseBody);
        ShouldContainErrorCode(responseBody, "Location.TemplateFonts.NotFound");
    }

    [Fact]
    public async Task Delete_ShouldReturnBadRequest_WhenTenantHeaderIsMissing()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        using HttpRequestMessage request = new(HttpMethod.Delete, "/location/v1/Service/Templates/template-1/Fonts/fonts/Inter-Regular.ttf");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, responseBody);
        ShouldContainErrorCode(responseBody, "Location.TemplateFonts.TenantIdRequired");
    }

    [Fact]
    public async Task List_ShouldReturnEmpty_WhenTemplateHasNoFonts()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        using HttpRequestMessage request = new(HttpMethod.Get, "/location/v1/Service/Templates/template-empty/Fonts");
        request.Headers.Add("X-TenantId", "tenant-1");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, responseBody);

        TemplateFontListResponse? listResponse = await response.Content
            .ReadFromJsonAsync<TemplateFontListResponse>(cancellationToken: TestContext.Current.CancellationToken);

        listResponse.ShouldNotBeNull();
        listResponse.Fonts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Upload_ShouldOverwriteExistingFont_WhenSameTemplateAndFontKeyAreUploadedAgain()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        string tenantId = $"tenant-{Guid.NewGuid():N}";
        string templateId = $"template-{Guid.NewGuid():N}";
        const string fontKey = "fonts/Brand-Regular.ttf";

        TemplateFontUploadResponse firstUpload = await UploadAsync(
            host,
            tenantId,
            templateId,
            fontKey,
            "brand-v1.ttf",
            [1, 2, 3, 4]);

        byte[] secondPayload = [9, 8, 7, 6, 5, 4];
        TemplateFontUploadResponse secondUpload = await UploadAsync(
            host,
            tenantId,
            templateId,
            fontKey,
            "brand-v2.ttf",
            secondPayload);

        using HttpRequestMessage listRequest = new(HttpMethod.Get, $"/location/v1/Service/Templates/{templateId}/Fonts");
        listRequest.Headers.Add("X-TenantId", tenantId);

        HttpResponseMessage listResponse = await host.Client.SendAsync(listRequest, TestContext.Current.CancellationToken);
        string listBody = await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, listBody);

        TemplateFontListResponse? listed = await listResponse.Content
            .ReadFromJsonAsync<TemplateFontListResponse>(cancellationToken: TestContext.Current.CancellationToken);

        listed.ShouldNotBeNull();
        listed.Fonts.Count.ShouldBe(1);
        listed.Fonts[0].OriginalFileName.ShouldBe("brand-v2.ttf");
        listed.Fonts[0].SizeBytes.ShouldBe(secondPayload.Length);
        listed.Fonts[0].ChecksumSha256.ShouldBe(secondUpload.ChecksumSha256);
        listed.Fonts[0].ObjectKey.ShouldBe(secondUpload.ObjectKey);

        string objectPath = BuildObjectPath(host.LocalStorageDirectory, secondUpload.ObjectKey);
        File.Exists(objectPath).ShouldBeTrue();
        byte[] persistedBytes = await File.ReadAllBytesAsync(objectPath, TestContext.Current.CancellationToken);
        persistedBytes.ShouldBe(secondPayload);

        firstUpload.ChecksumSha256.ShouldNotBe(secondUpload.ChecksumSha256);
    }

    [Fact]
    public async Task List_ShouldBeTenantIsolated_WhenTemplateIdMatchesAcrossTenants()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        string tenantA = $"tenant-a-{Guid.NewGuid():N}";
        string tenantB = $"tenant-b-{Guid.NewGuid():N}";
        string templateId = $"template-{Guid.NewGuid():N}";

        await UploadAsync(
            host,
            tenantA,
            templateId,
            "fonts/TenantA.ttf",
            "tenant-a.ttf",
            [1, 2, 3]);

        using HttpRequestMessage listTenantARequest = new(HttpMethod.Get, $"/location/v1/Service/Templates/{templateId}/Fonts");
        listTenantARequest.Headers.Add("X-TenantId", tenantA);
        HttpResponseMessage listTenantAResponse = await host.Client.SendAsync(listTenantARequest, TestContext.Current.CancellationToken);
        listTenantAResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        TemplateFontListResponse? tenantAList = await listTenantAResponse.Content
            .ReadFromJsonAsync<TemplateFontListResponse>(cancellationToken: TestContext.Current.CancellationToken);

        tenantAList.ShouldNotBeNull();
        tenantAList.Fonts.Count.ShouldBe(1);

        using HttpRequestMessage listTenantBRequest = new(HttpMethod.Get, $"/location/v1/Service/Templates/{templateId}/Fonts");
        listTenantBRequest.Headers.Add("X-TenantId", tenantB);
        HttpResponseMessage listTenantBResponse = await host.Client.SendAsync(listTenantBRequest, TestContext.Current.CancellationToken);
        string listTenantBBody = await listTenantBResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        listTenantBResponse.StatusCode.ShouldBe(HttpStatusCode.OK, listTenantBBody);

        TemplateFontListResponse? tenantBList = await listTenantBResponse.Content
            .ReadFromJsonAsync<TemplateFontListResponse>(cancellationToken: TestContext.Current.CancellationToken);

        tenantBList.ShouldNotBeNull();
        tenantBList.Fonts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_ShouldNotAffectOtherTenant_WhenFontKeysMatch()
    {
        await using TestLocationApiHost host = await TestLocationApiHost.StartAsync();

        string tenantA = $"tenant-a-{Guid.NewGuid():N}";
        string tenantB = $"tenant-b-{Guid.NewGuid():N}";
        string templateId = $"template-{Guid.NewGuid():N}";
        const string fontKey = "shared/Brand.ttf";

        TemplateFontUploadResponse tenantAUpload = await UploadAsync(
            host,
            tenantA,
            templateId,
            fontKey,
            "brand-a.ttf",
            [1, 2, 3, 4]);

        TemplateFontUploadResponse tenantBUpload = await UploadAsync(
            host,
            tenantB,
            templateId,
            fontKey,
            "brand-b.ttf",
            [5, 6, 7, 8]);

        HttpResponseMessage deleteTenantAResponse = await DeleteAsync(host, tenantA, templateId, fontKey);
        string deleteTenantABody = await deleteTenantAResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        deleteTenantAResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent, deleteTenantABody);

        using HttpRequestMessage listTenantBRequest = new(HttpMethod.Get, $"/location/v1/Service/Templates/{templateId}/Fonts");
        listTenantBRequest.Headers.Add("X-TenantId", tenantB);

        HttpResponseMessage listTenantBResponse = await host.Client.SendAsync(listTenantBRequest, TestContext.Current.CancellationToken);
        string listTenantBBody = await listTenantBResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        listTenantBResponse.StatusCode.ShouldBe(HttpStatusCode.OK, listTenantBBody);

        TemplateFontListResponse? tenantBList = await listTenantBResponse.Content
            .ReadFromJsonAsync<TemplateFontListResponse>(cancellationToken: TestContext.Current.CancellationToken);

        tenantBList.ShouldNotBeNull();
        tenantBList.Fonts.Count.ShouldBe(1);
        tenantBList.Fonts[0].FontKey.ShouldBe(fontKey);

        string tenantAObjectPath = BuildObjectPath(host.LocalStorageDirectory, tenantAUpload.ObjectKey);
        string tenantBObjectPath = BuildObjectPath(host.LocalStorageDirectory, tenantBUpload.ObjectKey);

        File.Exists(tenantAObjectPath).ShouldBeFalse();
        File.Exists(tenantBObjectPath).ShouldBeTrue();
    }

    private static async Task<TemplateFontUploadResponse> UploadAsync(
        TestLocationApiHost host,
        string tenantId,
        string templateId,
        string fontKey,
        string fileName,
        byte[] payload)
    {
        using MultipartFormDataContent formData = CreateFontUpload(fileName, "font/ttf", payload);
        using HttpRequestMessage uploadRequest = new(HttpMethod.Post, $"/location/v1/Service/Templates/{templateId}/Fonts/{fontKey}")
        {
            Content = formData,
        };
        uploadRequest.Headers.Add("X-TenantId", tenantId);

        HttpResponseMessage uploadResponse = await host.Client.SendAsync(uploadRequest, TestContext.Current.CancellationToken);
        string uploadBody = await uploadResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        uploadResponse.StatusCode.ShouldBe(HttpStatusCode.Created, uploadBody);

        TemplateFontUploadResponse? result = await uploadResponse.Content
            .ReadFromJsonAsync<TemplateFontUploadResponse>(cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        return result;
    }

    private static async Task<HttpResponseMessage> DeleteAsync(
        TestLocationApiHost host,
        string tenantId,
        string templateId,
        string fontKey)
    {
        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/location/v1/Service/Templates/{templateId}/Fonts/{fontKey}");
        deleteRequest.Headers.Add("X-TenantId", tenantId);
        return await host.Client.SendAsync(deleteRequest, TestContext.Current.CancellationToken);
    }

    private static MultipartFormDataContent CreateFontUpload(string fileName, string contentType, byte[] payload)
    {
        var formData = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(payload);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        formData.Add(fileContent, "File", fileName);
        return formData;
    }

    private static MultipartFormDataContent CreateFontUploadWithoutContentType(string fileName, byte[] payload)
    {
        var formData = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(payload);
        formData.Add(fileContent, "File", fileName);
        return formData;
    }

    private static string BuildObjectPath(string localStorageDirectory, string objectKey)
    {
        return Path.Combine(localStorageDirectory, objectKey.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void ShouldContainErrorCode(string responseBody, string errorCode)
    {
        responseBody.ShouldNotBeNullOrWhiteSpace();

        using JsonDocument document = JsonDocument.Parse(responseBody);

        if (!document.RootElement.TryGetProperty("errors", out JsonElement errorsElement))
        {
            responseBody.ShouldContain(errorCode);
            return;
        }

        if (errorsElement.ValueKind == JsonValueKind.Object)
        {
            errorsElement.TryGetProperty(errorCode, out _).ShouldBeTrue(responseBody);
            return;
        }

        if (errorsElement.ValueKind == JsonValueKind.Array)
        {
            bool hasErrorCode = errorsElement
                .EnumerateArray()
                .Any(entry =>
                    entry.TryGetProperty("name", out JsonElement nameElement)
                    && string.Equals(nameElement.GetString(), errorCode, StringComparison.Ordinal));

            hasErrorCode.ShouldBeTrue(responseBody);
            return;
        }

        responseBody.ShouldContain(errorCode);
    }
}

#pragma warning restore CA2012
#pragma warning restore CA2000
