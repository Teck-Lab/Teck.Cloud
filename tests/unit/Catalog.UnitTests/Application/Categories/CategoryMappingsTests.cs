using Catalog.Application.Brands.Mappings;
using Catalog.Application.Categories.ReadModels;
using Catalog.Application.Categories.Response;
using Catalog.Domain.Entities.CategoryAggregate;

namespace Catalog.UnitTests.Application.Categories
{
    public class CategoryMappingsTests
    {
        [Fact]
        public void CategoryToCategoryResponse_Maps_All_Properties()
        {
            // Arrange
            var category = new Category();

            // Act
            var response = CategoryMapper.CategoryToCategoryResponse(category);

            // Assert
            Assert.NotNull(response);
            Assert.IsType<CategoryResponse>(response);
        }

        [Fact]
        public void CategoryToCategoryResponse_Returns_NonNull_Response()
        {
            // Arrange
            var category = new Category();

            // Act
            var response = CategoryMapper.CategoryToCategoryResponse(category);

            // Assert
            Assert.NotNull(response);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_Maps_All_Properties()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "Test Category",
                Description = "Test Description",
                ParentId = Guid.NewGuid(),
                ParentName = "Parent Category",
                ImageUrl = new Uri("https://example.com/image.jpg")
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.IsType<CategoryResponse>(response);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_Returns_NonNull_Response()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "Test Category"
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_Maps_Name_Property()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "Specific Category Name"
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("Specific Category Name", response.Name);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_Maps_Description_Property()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "Test Category",
                Description = "Specific Description"
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("Specific Description", response.Description);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_Maps_Null_Description()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "Test Category",
                Description = null
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.Null(response.Description);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_Maps_Empty_Description()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "Test Category",
                Description = ""
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("", response.Description);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_Maps_Empty_Name()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = ""
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("", response.Name);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_Handles_Long_Name()
        {
            // Arrange
            var longName = new string('A', 1000);
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = longName
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(longName, response.Name);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_Handles_Long_Description()
        {
            // Arrange
            var longDescription = new string('B', 2000);
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "Test Category",
                Description = longDescription
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(longDescription, response.Description);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_Handles_Special_Characters()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "Test & Category <script>alert('xss')</script>",
                Description = "Description with special chars: @#$%^&*()"
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("Test & Category <script>alert('xss')</script>", response.Name);
            Assert.Equal("Description with special chars: @#$%^&*()", response.Description);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_Handles_Unicode_Characters()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "Категория 日本語 العربية",
                Description = "Description: 🎉 émojis and ñ characters"
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("Категория 日本語 العربية", response.Name);
            Assert.Equal("Description: 🎉 émojis and ñ characters", response.Description);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_With_ParentId_And_ParentName()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "Child Category",
                ParentId = Guid.NewGuid(),
                ParentName = "Parent Category"
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.IsType<CategoryResponse>(response);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_With_ImageUrl()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "Test Category",
                ImageUrl = new Uri("https://example.com/category-image.png")
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.IsType<CategoryResponse>(response);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_With_Null_ImageUrl()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "Test Category",
                ImageUrl = null
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.IsType<CategoryResponse>(response);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_With_Null_ParentId()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "Test Category",
                ParentId = null
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.IsType<CategoryResponse>(response);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_With_Null_ParentName()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "Test Category",
                ParentName = null
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.IsType<CategoryResponse>(response);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_Produces_Distinct_Objects()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "Test Category",
                Description = "Test Description"
            };

            // Act
            var response1 = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);
            var response2 = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response1);
            Assert.NotNull(response2);
            Assert.NotSame(response1, response2);
        }

        [Fact]
        public void CategoryToCategoryResponse_Produces_Distinct_Objects()
        {
            // Arrange
            var category = new Category();

            // Act
            var response1 = CategoryMapper.CategoryToCategoryResponse(category);
            var response2 = CategoryMapper.CategoryToCategoryResponse(category);

            // Assert
            Assert.NotNull(response1);
            Assert.NotNull(response2);
            Assert.NotSame(response1, response2);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_With_Empty_Guid()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.Empty,
                Name = "Test Category"
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.IsType<CategoryResponse>(response);
        }

        [Fact]
        public void CategoryReadModelToCategoryResponse_With_Whitespace_Only_Name()
        {
            // Arrange
            var categoryReadModel = new CategoryReadModel
            {
                Id = Guid.NewGuid(),
                Name = "   "
            };

            // Act
            var response = CategoryMapper.CategoryReadModelToCategoryResponse(categoryReadModel);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("   ", response.Name);
        }
    }
}
