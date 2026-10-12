using System;
using System.Collections.Generic;
using System.Text;
using Wms.Outbound.Inventory.Domain;
using Wms.Outbound.Order.Domain;

namespace Wms.Outbound.Tests
{
    public class TestDataFactory
    {
        public static CustomerOrder CreateDefaultOrder(string orderId = "ORD-001", int quantity = 30)
        {
            return new CustomerOrder
            {
                OrderId = orderId,
                CustomerId = "CUST-001",
                Lines = new List<OrderLine> { CreateDefaultOrderLine() },
                Status = OrderStatus.Released,
                Priority = OrderPriority.High,
                OrderDate = DateTime.Now,
                CompleteDeliveryRequired = false
            };
        }

        public static List<CustomerOrder> GetOrderList()
        {
            return
            [
                CreateDefaultOrder("ORD-001", 30),
                CreateDefaultOrder("ORD-002", 20),
                CreateDefaultOrder("ORD-003", 40),
                CreateDefaultOrder("ORD-004", 30),
                CreateDefaultOrder("ORD-005", 10)
            ];
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
        public static List<Product> GetProductList()
        {
            return
            [
                CreateDefaultProduct("1", "Barf"),
                CreateDefaultProduct("2", "Puch"),
                CreateDefaultProduct("3", "A-Sass"),
                CreateDefaultProduct("4", "Shito"),
                CreateDefaultProduct("5", "Bimbo")
            ];
        }
        public static OrderLine CreateDefaultOrderLine(int lineId = 1, string productId = "1", int quantity = 30)
        {
            return new OrderLine(lineId, productId, quantity);
        }
        public static List<OrderLine> GetOrderLineList()
        {
            return
            [
                CreateDefaultOrderLine(1, "1", 30),
                CreateDefaultOrderLine(2, "2", 20),
                CreateDefaultOrderLine(3, "3", 40),
                CreateDefaultOrderLine(4, "4", 30),
                CreateDefaultOrderLine(5, "5", 10)
            ];
        }
        public static Sku CreateDefaultSku(string skuId = "SKU-001", int quantity = 100, Product product = null, WarehouseLocation location = null)
        {
            return new Sku
            {
                SkuId = skuId,
                ProductQuantity = quantity,
                Product = product ?? new Product { ProductId = "1", ProductName = "Barf" },
                Location = location ?? new WarehouseLocation { Id = "LOC-001", IsLocked = false }
            };
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
