#pragma warning disable IDE0005
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Shouldly;
using ZiggyCreatures.Caching.Fusion;
using Microsoft.Extensions.DependencyInjection;
using Catalog.Infrastructure.Caching;
using Catalog.Application.Products.Repositories;
using Catalog.Application.Products.ReadModels;
using NSubstitute;
#pragma warning restore IDE0005

namespace Catalog.IntegrationTests.Infrastructure.Caches;

public class ProductCacheIntegrationTests
{
    [Fact]
    public async Task ProductCache_GetSetRemove_Works()
    {
        var services = new ServiceCollection();
        services.AddFusionCache();
        var provider = services.BuildServiceProvider();

        var fusion = provider.GetRequiredService<IFusionCache>();
        var repo = NSubstitute.Substitute.For<IProductReadRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<ProductReadModel?>(null));
        var cache = new ProductCache(fusion, repo);

        var key = Guid.NewGuid();
        var value = new ProductReadModel { Id = key, Name = "name1" };

        await cache.SetAsync(key, value, TestContext.Current.CancellationToken);
        var got = await cache.TryGetByIdAsync(key, TestContext.Current.CancellationToken);
        got.ShouldNotBeNull();
        got!.Name.ShouldBe("name1");

        await cache.RemoveAsync(key, TestContext.Current.CancellationToken);
        var after = await cache.TryGetByIdAsync(key, TestContext.Current.CancellationToken);
        after.ShouldBeNull();
    }
}
