using Catalog.Application.Categories.Response;

namespace Catalog.UnitTests.Application.Categories
{
    public class ResponseTypesTests
    {
        [Fact]
        public void CategoryResponse_Defaults_Are_Correct()
        {
            var resp = new CategoryResponse();
            Assert.Null(resp.Name);
            Assert.Null(resp.Description);
        }

        [Fact]
        public void CategoryResponse_CanSet_All_Properties()
        {
            var resp = new CategoryResponse
            {
                Name = "Cat",
                Description = "desc"
            };
            Assert.Equal("Cat", resp.Name);
            Assert.Equal("desc", resp.Description);
        }

        [Fact]
        public void CategoryResponse_Allows_Nulls()
        {
            var resp = new CategoryResponse
            {
                Name = null,
                Description = null
            };
            Assert.Null(resp.Name);
            Assert.Null(resp.Description);
        }
    }
}
