using Wms.Outbound.StockAllocation.Domain;
using Wms.Outbound.StockAllocation.Services;

namespace Wms.Outbound.Tests.StockAllocation
{
    public class AllocationLedgerTests
    {
        [Fact]
        public void GetAllocatedQuantity_WithNoAllocations_ReturnsZero()
        {
            var ledger = new AllocationLedger();

            var quantity = ledger.GetAllocatedQuantity("ORD-001", 1);

            Assert.Equal(0, quantity);
        }

        [Fact]
        public void Record_WithSingleAllocation_IsReflectedInGetAllocatedQuantity()
        {
            var ledger = new AllocationLedger();

            ledger.Record(new OrderLineAllocation("ORD-001", 1, "SKU-001", 20));

            Assert.Equal(20, ledger.GetAllocatedQuantity("ORD-001", 1));
        }

        [Fact]
        public void Record_WithMultipleAllocationsForSameLine_SumsQuantities()
        {
            var ledger = new AllocationLedger();

            ledger.Record(new OrderLineAllocation("ORD-001", 1, "SKU-001", 20));
            ledger.Record(new OrderLineAllocation("ORD-001", 1, "SKU-002", 10));

            Assert.Equal(30, ledger.GetAllocatedQuantity("ORD-001", 1));
        }

        [Fact]
        public void Record_WithAllocationsForDifferentLines_KeepsQuantitiesSeparate()
        {
            var ledger = new AllocationLedger();

            ledger.Record(new OrderLineAllocation("ORD-001", 1, "SKU-001", 20));
            ledger.Record(new OrderLineAllocation("ORD-001", 2, "SKU-001", 15));

            Assert.Equal(20, ledger.GetAllocatedQuantity("ORD-001", 1));
            Assert.Equal(15, ledger.GetAllocatedQuantity("ORD-001", 2));
        }

        [Fact]
        public void Record_WithAllocationsForDifferentOrders_KeepsQuantitiesSeparate()
        {
            var ledger = new AllocationLedger();

            ledger.Record(new OrderLineAllocation("ORD-001", 1, "SKU-001", 20));
            ledger.Record(new OrderLineAllocation("ORD-002", 1, "SKU-001", 5));

            Assert.Equal(20, ledger.GetAllocatedQuantity("ORD-001", 1));
            Assert.Equal(5, ledger.GetAllocatedQuantity("ORD-002", 1));
        }

        [Fact]
        public void ReleaseOrder_WithUnknownOrder_ReturnsEmpty()
        {
            var ledger = new AllocationLedger();

            var released = ledger.ReleaseOrder("ORD-001");
            Assert.Empty(released);
        }

        [Fact]
        public void ReleaseOrder_RemovesAllAllocationsAcrossAllLines()
        {
            var ledger = new AllocationLedger();
            ledger.Record(new OrderLineAllocation("ORD-001", 1, "SKU-001", 20));
            ledger.Record(new OrderLineAllocation("ORD-001", 2, "SKU-002", 10));

            var released = ledger.ReleaseOrder("ORD-001");

            Assert.Equal(30, released.Sum(allocation => allocation.Quantity));
            Assert.Equal(0, ledger.GetAllocatedQuantity("ORD-001", 1));
            Assert.Equal(0, ledger.GetAllocatedQuantity("ORD-001", 2));
        }

        [Fact]
        public void ReleaseOrder_DoesNotAffectOtherOrders()
        {
            var ledger = new AllocationLedger();
            ledger.Record(new OrderLineAllocation("ORD-001", 1, "SKU-001", 20));
            ledger.Record(new OrderLineAllocation("ORD-002", 1, "SKU-001", 5));

            ledger.ReleaseOrder("ORD-001");

            Assert.Equal(0, ledger.GetAllocatedQuantity("ORD-001", 1));
            Assert.Equal(5, ledger.GetAllocatedQuantity("ORD-002", 1));
        }

        [Fact]
        public void ReleaseOrder_CalledTwice_SecondCallReturnsEmpty()
        {
            var ledger = new AllocationLedger();
            ledger.Record(new OrderLineAllocation("ORD-001", 1, "SKU-001", 20));

            ledger.ReleaseOrder("ORD-001");
            var secondRelease = ledger.ReleaseOrder("ORD-001");

            Assert.Empty(secondRelease);
        }

        [Fact]
        public void ReleaseLine_WithUnknownLine_ReturnsEmpty()
        {
            var ledger = new AllocationLedger();
            ledger.Record(new OrderLineAllocation("ORD-001", 1, "SKU-001", 20));

            var released = ledger.ReleaseLine("ORD-001", 2);

            Assert.Empty(released);
        }

        [Fact]
        public void ReleaseLine_RemovesOnlyThatLineAndLeavesOtherLinesIntact()
        {
            var ledger = new AllocationLedger();
            ledger.Record(new OrderLineAllocation("ORD-001", 1, "SKU-001", 20));
            ledger.Record(new OrderLineAllocation("ORD-001", 2, "SKU-002", 10));

            var released = ledger.ReleaseLine("ORD-001", 1);

            Assert.Equal(20, released.Single().Quantity);
            Assert.Equal(0, ledger.GetAllocatedQuantity("ORD-001", 1));
            Assert.Equal(10, ledger.GetAllocatedQuantity("ORD-001", 2));
        }

        [Fact]
        public void Record_AfterReleaseLine_CanBeAllocatedAgain()
        {
            var ledger = new AllocationLedger();
            ledger.Record(new OrderLineAllocation("ORD-001", 1, "SKU-001", 20));
            ledger.ReleaseLine("ORD-001", 1);

            ledger.Record(new OrderLineAllocation("ORD-001", 1, "SKU-001", 15));

            Assert.Equal(15, ledger.GetAllocatedQuantity("ORD-001", 1));
        }
    }
}
