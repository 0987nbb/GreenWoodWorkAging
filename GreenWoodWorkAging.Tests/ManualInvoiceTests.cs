using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;
using Xunit;

namespace GreenWoodWorkAging.Tests
{
    public class ManualInvoiceTests
    {
        [Fact]
        public void ManualInvoice_Calculations_AreAccurate()
        {
            var inv = new ManualInvoice
            {
                CustomerName    = "Rashid Ahmed",
                PhoneNumber     = "03001234567",
                InvoiceDate     = new DateTime(2026, 11, 2),
                TotalTapesFeet  = 120.5m,
                TapeRate        = 15.0m,
                SheetCuts       = 8,
                RatePerSheetCut = 125.0m,
                CarriageCharges = 500.0m
            };

            // Tape Amount = 120.5 * 15 = 1807.50
            Assert.Equal(1807.50m, inv.TapeAmount);

            // Cutting Amount = 8 * 125 = 1000.00
            Assert.Equal(1000.00m, inv.CuttingAmount);

            // Grand Total = 1807.50 + 1000.00 + 500.00 = 3307.50
            Assert.Equal(3307.50m, inv.GrandTotal);
        }

        [Fact]
        public void ManualInvoice_ZeroCarriage_GrandTotalIncludesTapesAndCutting()
        {
            var inv = new ManualInvoice
            {
                TotalTapesFeet  = 50m,
                TapeRate        = 20m,
                SheetCuts       = 5,
                RatePerSheetCut = 100m,
                CarriageCharges = 0m
            };

            Assert.Equal(1000m, inv.TapeAmount);
            Assert.Equal(500m, inv.CuttingAmount);
            Assert.Equal(1500m, inv.GrandTotal);
        }

        [Fact]
        public void Database_SaveSearchAndGetManualInvoice_WorksAsExpected()
        {
            using var testDb = new DatabaseService();

            var inv = new ManualInvoice
            {
                CustomerName    = "Test Manual Customer " + Guid.NewGuid().ToString("N")[..6],
                PhoneNumber     = "03219988776",
                InvoiceDate     = DateTime.Today,
                TotalTapesFeet  = 80m,
                TapeRate        = 12.5m,
                SheetCuts       = 4,
                RatePerSheetCut = 200m,
                CarriageCharges = 350m,
                Notes           = "Urgent cutting needed"
            };

            var saved = testDb.SaveManualInvoice(inv);
            Assert.True(saved.Id > 0);

            // Search by name
            var resultsName = testDb.SearchManualInvoices(inv.CustomerName);
            Assert.Contains(resultsName, x => x.Id == saved.Id);

            // Search by phone
            var resultsPhone = testDb.SearchManualInvoices("03219988776");
            Assert.Contains(resultsPhone, x => x.Id == saved.Id);

            // Get by ID
            var fetched = testDb.GetManualInvoice(saved.Id);
            Assert.NotNull(fetched);
            Assert.Equal(inv.CustomerName, fetched!.CustomerName);
            Assert.Equal(80m, fetched.TotalTapesFeet);
            Assert.Equal(12.5m, fetched.TapeRate);
            Assert.Equal(4, fetched.SheetCuts);
            Assert.Equal(200m, fetched.RatePerSheetCut);
            Assert.Equal(350m, fetched.CarriageCharges);
            Assert.Equal(1000m + 800m + 350m, fetched.GrandTotal);

            // Clean up
            testDb.DeleteManualInvoice(saved.Id);
            Assert.Null(testDb.GetManualInvoice(saved.Id));
        }

        [Fact]
        public void ManualInvoicePdfService_BuildDocument_Succeeds()
        {
            var inv = new ManualInvoice
            {
                Id              = 101,
                CustomerName    = "Malik Furniture",
                PhoneNumber     = "0333 4455667",
                InvoiceDate     = new DateTime(2026, 11, 2),
                TotalTapesFeet  = 150m,
                TapeRate        = 18m,
                SheetCuts       = 12,
                RatePerSheetCut = 150m,
                CarriageCharges = 800m,
                Notes           = "Delivered to shop"
            };

            var doc = ManualInvoicePdfService.Build(inv, null);
            Assert.NotNull(doc);
        }
    }
}
