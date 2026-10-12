using NSubstitute;
using Wms.Outbound.Inventory.Services;

namespace Wms.Outbound.Tests.Inventory
{
    public class InventoryServiceTests
    {
        [Fact]
        public async Task GetSkuByIdAsync_ReturnsSkuFromRepository()
        {
            var repository = Substitute.For<IInventoryRepository>();
            var sku = TestDataFactory.CreateDefaultSku("SKU-001");
            repository.GetSkuByIdAsync("SKU-001").Returns(sku);
            var inventoryService = new InventoryService(repository);

            var result = await inventoryService.GetSkuByIdAsync("SKU-001");

            Assert.Same(sku, result);
        }

        [Fact]
        public async Task GetSkuListAsync_ReturnsSkusFromRepository()
        {
            var repository = Substitute.For<IInventoryRepository>();
            var skus = TestDataFactory.GetSkuList();
            repository.GetSkuListAsync().Returns(skus);
            var inventoryService = new InventoryService(repository);

            var result = await inventoryService.GetSkuListAsync();

            Assert.Same(skus, result);
        }

        [Fact]
        public async Task UpdateSkuAsync_PassesSkuToRepositoryAndReturnsResult()
        {
            var repository = Substitute.For<IInventoryRepository>();
            var sku = TestDataFactory.CreateDefaultSku("SKU-001");
            repository.UpdateSkuAsync(sku).Returns(true);
            var inventoryService = new InventoryService(repository);

            var result = await inventoryService.UpdateSkuAsync(sku);

            Assert.True(result);
            await repository.Received(1).UpdateSkuAsync(sku);
        }

        [Fact]
        public async Task UpdateSkuAsync_WhenRepositoryReturnsFalse_ReturnsFalse()
        {
            var repository = Substitute.For<IInventoryRepository>();
            var sku = TestDataFactory.CreateDefaultSku("SKU-001");
            repository.UpdateSkuAsync(sku).Returns(false);
            var inventoryService = new InventoryService(repository);

            var result = await inventoryService.UpdateSkuAsync(sku);

            Assert.False(result);
        }
    }
}
