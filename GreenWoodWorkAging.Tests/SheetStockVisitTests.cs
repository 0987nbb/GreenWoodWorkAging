using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;
using Xunit;

namespace GreenWoodWorkAging.Tests
{
    /// <summary>
    /// A customer's sheets are recorded the way they arrive: one entry for the visit, one line for
    /// each material in it. These are the numbers the shop reads off that entry.
    /// </summary>
    [Collection("SheetStock")]
    public class SheetStockVisitTests
    {
        static SheetStockVisitTests()
        {
            Environment.SetEnvironmentVariable("GREENWOOD_DATA_DIR", Scratch);
        }

        private static readonly string Scratch =
            Path.Combine(Path.GetTempPath(), "gwa-sheet", DateTime.Now.ToString("HHmmss"));

        private static SheetStockVisit Delivery(string name, string phone, int billId,
                                                params (string material, int given)[] materials)
        {
            var visit = new SheetStockVisit
            {
                BillId       = billId,
                CustomerName = name,
                PhoneNumber  = phone,
                Date         = new DateTime(2026, 10, 28)
            };
            foreach (var (material, given) in materials)
                visit.Lines.Add(new SheetStockEntry { Material = material, Received = given });
            return visit;
        }

        [Fact]
        public void FourMaterialsFromOneCustomer_StayOneEntry()
        {
            Directory.CreateDirectory(Scratch);
            var db = new DatabaseService();
            int billId = db.CreateBill("Naveed Formely", "03009998888", new DateTime(2026, 10, 28));

            var saved = db.SaveSheetStockVisit(Delivery("Naveed Formely", "03009998888", billId,
                ("KMI 123", 10), ("Super Glass", 5), ("PVC", 3), ("Textile", 2)));
            Assert.NotEqual(0, saved.Id);

            var found = db.SearchSheetStockVisits("Naveed Formely");
            Assert.Single(found);
            Assert.Equal(4, found[0].Lines.Count);
            Assert.Equal(20, found[0].TotalReceived);
        }

        [Fact]
        public void CuttingEightOfOneMaterial_LeavesTheOtherMaterialsUntouched()
        {
            Directory.CreateDirectory(Scratch);
            var db = new DatabaseService();
            int billId = db.CreateBill("Waqas Ali", "03214445566", new DateTime(2026, 10, 28));

            var visit = db.SaveSheetStockVisit(Delivery("Waqas Ali", "03214445566", billId,
                ("KMI 123", 10), ("Super Glass", 6)));

            var kmi = visit.Lines.Single(l => l.Material == "KMI 123");
            db.SetSheetStockUsed(kmi.Id, 8);

            var again = db.GetSheetStockVisit(visit.Id)!;
            Assert.Equal(8, again.Lines.Single(l => l.Material == "KMI 123").Used);
            Assert.Equal(2, again.Lines.Single(l => l.Material == "KMI 123").Remaining);
            Assert.Equal(0, again.Lines.Single(l => l.Material == "Super Glass").Used);
            Assert.Equal(16, again.TotalReceived);
            Assert.Equal(8, again.TotalUsed);
            Assert.Equal(8, again.TotalLeft);
        }

        [Fact]
        public void SavingTheEntryAgain_KeepsTheEntryAndReplacesItsLines()
        {
            Directory.CreateDirectory(Scratch);
            var db = new DatabaseService();
            int billId = db.CreateBill("Shahid Mehmood", "03337776655", new DateTime(2026, 10, 28));

            var visit = db.SaveSheetStockVisit(Delivery("Shahid Mehmood", "03337776655", billId,
                ("KMI 123", 4), ("PVC", 2), ("Textile", 1)));

            // The shop corrects the entry: the textile was never handed over.
            visit.Lines.RemoveAt(2);
            db.SaveSheetStockVisit(visit);

            var entries = db.SearchSheetStockVisits("Shahid Mehmood");
            Assert.Single(entries);
            Assert.Equal(visit.Id, entries[0].Id);
            Assert.Equal(2, entries[0].Lines.Count);
            Assert.Equal(6, entries[0].TotalReceived);
        }

        [Fact]
        public void TheSearchBoxFindsAnEntryByAnyMaterialInTheVisit()
        {
            Directory.CreateDirectory(Scratch);
            var db = new DatabaseService();
            int billId = db.CreateBill("Naveed Formely", "03009998888", new DateTime(2026, 10, 28));
            db.SaveSheetStockVisit(Delivery("Naveed Formely", "03009998888", billId,
                ("KMI 123", 10), ("Super Glass", 5)));

            var byMaterial = db.SearchSheetStockVisits("super");
            Assert.Contains(byMaterial, v => v.CustomerName == "Naveed Formely");

            var byPhone = db.SearchSheetStockVisits("0300999");
            Assert.Contains(byPhone, v => v.CustomerName == "Naveed Formely");
        }

        /// <summary>
        /// The old window saved one row per material, so the same customer appeared several times.
        /// Opening the database once more gathers those rows into a single entry — nothing is lost.
        /// </summary>
        [Fact]
        public void LinesFromTheOldList_GatherIntoOneEntryPerCustomer()
        {
            Directory.CreateDirectory(Scratch);
            var db = new DatabaseService();
            int billId = db.CreateBill("Bilal Ahmed", "03331112222", new DateTime(2026, 10, 27));

            // Rows written the way the old window wrote them: no visit attached.
            db.SaveSheetStock(new SheetStockEntry
            {
                BillId = billId, CustomerName = "Bilal Ahmed", PhoneNumber = "03331112222",
                Date = new DateTime(2026, 10, 27), Material = "KMI 123", Received = 4
            });
            db.SaveSheetStock(new SheetStockEntry
            {
                BillId = billId, CustomerName = "Bilal Ahmed", PhoneNumber = "03331112222",
                Date = new DateTime(2026, 10, 27), Material = "PVC", Received = 2, Used = 1
            });

            using var reopened = new DatabaseService();
            var entries = reopened.SearchSheetStockVisits("Bilal Ahmed");

            Assert.Single(entries);
            Assert.Equal(2, entries[0].Lines.Count);
            Assert.Equal(6, entries[0].TotalReceived);
            Assert.Equal(1, entries[0].TotalUsed);
        }
    }
}
