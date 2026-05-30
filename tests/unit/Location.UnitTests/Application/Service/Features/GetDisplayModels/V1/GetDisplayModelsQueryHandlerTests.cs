using Location.Application.Service.Abstractions;
using Location.Application.Service.Features.GetDisplayModels.V1;
using NSubstitute;
using Shouldly;

namespace Location.UnitTests.Application.Service.Features.GetDisplayModels.V1;

public sealed class GetDisplayModelsQueryHandlerTests
{
    private readonly IDisplayModelReadRepository repository = Substitute.For<IDisplayModelReadRepository>();

    [Fact]
    public async Task Handle_WhenTenantIdHasWhitespace_ShouldTrimAndMapResponse()
    {
        GetDisplayModelsQuery request = new("  tenant-1  ");
        this.repository.ListAsync("tenant-1", Arg.Any<CancellationToken>())
            .Returns(
            [
                new DisplayModelSnapshot("disp-1", "Display 1", 1920, 1080),
                new DisplayModelSnapshot("disp-2", "Display 2", 1080, 1920),
            ]);

        GetDisplayModelsQueryHandler sut = new(this.repository);

        var result = await sut.Handle(request, TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        result.Value.DisplayModels.Count.ShouldBe(2);
        result.Value.DisplayModels[0].DisplayModelId.ShouldBe("disp-1");
        result.Value.DisplayModels[0].Name.ShouldBe("Display 1");
        result.Value.DisplayModels[0].Width.ShouldBe(1920);
        result.Value.DisplayModels[0].Height.ShouldBe(1080);
    }
}
