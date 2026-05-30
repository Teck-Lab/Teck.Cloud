using Location.Application.Service.Abstractions;
using Location.Application.Service.Features.ListLocationGroups.V1;
using NSubstitute;
using Shouldly;

namespace Location.UnitTests.Application.Service.Features.ListLocationGroups.V1;

public sealed class ListLocationGroupsQueryHandlerTests
{
    private readonly ILocationGroupReadRepository repository = Substitute.For<ILocationGroupReadRepository>();

    [Fact]
    public async Task Handle_WhenGroupsExist_ShouldReturnMappedGroupList()
    {
        this.repository.ListByTenantAsync("_current", Arg.Any<CancellationToken>())
            .Returns(
            [
                new LocationGroupSnapshot("tenant-1", "grp-1", "North"),
                new LocationGroupSnapshot("tenant-1", "grp-2", "South"),
            ]);

        ListLocationGroupsQueryHandler sut = new(this.repository);

        var result = await sut.Handle(new ListLocationGroupsQuery(), TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        result.Value.Groups.Count.ShouldBe(2);
        result.Value.Groups[0].LocationGroupId.ShouldBe("grp-1");
        result.Value.Groups[0].Name.ShouldBe("North");
    }
}
