extern alias CatalogMs;

using CatalogMs::CatalogMicroservice.applicationCore.Entities;
using CatalogMs::CatalogMicroservice.applicationCore.Services;
using CatalogMs::Microsoft.eShopWeb.CatalogMicroservice.Interfaces;
using NSubstitute;
using Xunit;

namespace ExamTests;

public class DeleteCatalogItemHandlerTests
{
    [Fact]
    public async Task HandleAsync_DeletesItem_WhenItExists()
    {
        // Arrange
        var item = new CatalogItem(1, 1, "desc", "Mug", 9.99m, "mug.png");
        var repository = Substitute.For<IRepository<CatalogItem>>();
        repository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(item);

        var sut = new DeleteCatalogItemHandler(repository);

        // Act
        var result = await sut.HandleAsync(new DeleteCatalogItemRequest { CatalogItemId = 1 }, CancellationToken.None);

        // Assert
        Assert.True(result);
        await repository.Received(1).DeleteAsync(item, Arg.Any<CancellationToken>());
    }
}
