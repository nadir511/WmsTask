using System;
using System.Collections.Generic;
using System.Text;
using Wms.Outbound.Inventory.Domain;
using Wms.Outbound.Order.Domain;

namespace Wms.Outbound.Tests
{
    public class TestDataFactory
    {
        public static CustomerOrder CreateDefaultOrder(string orderId = "ORD-001", int quantity = 30, OrderLine? orderLine = null, OrderPriority orderPriority = OrderPriority.High, bool completeDeliveryRequired = false)
        {
            return new CustomerOrder(
                orderId,
                "CUST-001",
                new List<OrderLine> { orderLine ?? CreateDefaultOrderLine(1, "1", quantity) },
                orderPriority,
                DateTime.Now,
                completeDeliveryRequired);
        }

        public static WarehouseLocation CreateDefaultLocation(string locationId = "LOC-001", bool isLocked = false)
        {
            return new WarehouseLocation
            {
                Id = locationId,
                IsLocked = isLocked
            };
        }
        public static Product CreateDefaultProduct(string productId = "1", string productName = "Barf")
        {
            return new Product
            {
                ProductId = productId,
                ProductName = productName
            };
        }
        public static OrderLine CreateDefaultOrderLine(int lineId = 1, string productId = "1", int quantity = 30)
        {
            return new OrderLine(lineId, productId, quantity);
        }
        
        public static Sku CreateDefaultSku(string skuId = "SKU-001", int quantity = 100, Product? product = null, WarehouseLocation? location = null)
        {
            return new Sku(
                skuId,
                quantity,
                product ?? new Product { ProductId = "1", ProductName = "Barf" },
                location ?? new WarehouseLocation { Id = "LOC-001", IsLocked = false });
        }
        public static List<Sku> GetSkuList()
        {
            return
            [
                CreateDefaultSku("SKU-001", 100, new Product { ProductId = "1", ProductName = "Barf" }, new WarehouseLocation { Id = "LOC-001", IsLocked = false }),
                CreateDefaultSku("SKU-002", 20, new Product { ProductId = "2", ProductName = "Puch" }, new WarehouseLocation { Id = "LOC-002", IsLocked = false }),
                CreateDefaultSku("SKU-003", 40, new Product { ProductId = "3", ProductName = "A-Sass" }, new WarehouseLocation { Id = "LOC-003", IsLocked = false }),
                CreateDefaultSku("SKU-004", 30, new Product { ProductId = "4", ProductName = "Shito" }, new WarehouseLocation { Id = "LOC-004", IsLocked = false }),
                CreateDefaultSku("SKU-005", 10, new Product { ProductId = "5", ProductName = "Bimbo" }, new WarehouseLocation { Id = "LOC-005", IsLocked = false }),
            ];
        }
    }
}
