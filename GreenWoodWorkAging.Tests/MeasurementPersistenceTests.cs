using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;
using Xunit;

namespace GreenWoodWorkAging.Tests
{
    /// <summary>
    /// What the shop already saved must not move: a bill reopened reads exactly like the day it
    /// was written, and the only new number a row can carry — the sady — has to survive the trip
    /// into SQLite and back.
    /// </summary>
    public class MeasurementPersistenceTests
    {
        private static int NewBill(DatabaseService db, string name)
            => db.CreateBill(name, "03000000000", new DateTime(2026, 11, 8));

        [Fact]
        public void ASadySaved_IsReadBackAsTheSameLength()
        {
            var db = new DatabaseService();
            int billId = NewBill(db, "Ali Raza");

            db.SaveMeasurement(new MeasurementEntry
            {
                BillId       = billId,
                SerialNumber = 1,
                HeightInches = 80, HeightSooter = 1, HeightHalf = 1,   // 80.19
                WidthInches  = 18, WidthSooter  = 0, WidthHalf  = 0,
                Quantity     = 2,
                SheetCode    = "KMI 123"
            });

            var back = db.GetMeasurements(billId).Single();
            Assert.Equal(1, back.HeightHalf);
            Assert.Equal(0, back.WidthHalf);
            Assert.Equal(Sooter.Combine(80, 1, 1), back.HeightSixteenths);
            Assert.Equal("80.19", new MeasurementDisplayFormatter()
                                    .FormatCompact(back.HeightInches, back.HeightSooter, back.HeightHalf));

            // Editing the row keeps it the same row, halves and all.
            back.HeightHalf = 0;
            back.WidthHalf  = 1;
            db.SaveMeasurement(back);

            var edited = db.GetMeasurements(billId).Single();
            Assert.Equal(0, edited.HeightHalf);
            Assert.Equal(1, edited.WidthHalf);
        }

        [Fact]
        public void RowsWithoutASady_ReadBackExactlyAsBefore()
        {
            var db = new DatabaseService();
            int billId = NewBill(db, "Imran Khan");

            db.SaveMeasurement(new MeasurementEntry
            {
                BillId = billId, SerialNumber = 1,
                HeightInches = 80, HeightSooter = 4,
                WidthInches  = 17, WidthSooter  = 7,
                Quantity     = 3, Side = MeasurementSide.HeightWidth
            });

            var m = db.GetMeasurements(billId).Single();
            Assert.Equal(80, m.HeightInches); Assert.Equal(4, m.HeightSooter); Assert.Equal(0, m.HeightHalf);
            Assert.Equal(17, m.WidthInches);  Assert.Equal(7, m.WidthSooter);  Assert.Equal(0, m.WidthHalf);

            // The lengths are the ones billed before sady existed: 80 inch 4 sooter is 80.5 inch,
            // and it now reads with the two digits the tape gives.
            Assert.Equal(80 * Sooter.PerInch + 8, m.HeightSixteenths);
            Assert.Equal("80.50", new MeasurementDisplayFormatter()
                                    .FormatCompact(m.HeightInches, m.HeightSooter, m.HeightHalf));
        }

        [Fact]
        public void MoneyForAnOldRow_IsTheSameRupeesTheEighthInchRuleGave()
        {
            // 80 × 18, quantity 2 → 3136 eighths of an inch under the old counting.
            const int oldEighths = 3136;
            var row = new MeasurementEntry { HeightInches = 80, WidthInches = 18, Quantity = 2 };

            decimal oldFeet = oldEighths / 96m;                       // 96 eighths in a foot
            decimal newFeet = BillCharges.FeetFromSixteenths(row.RowTotalSixteenths);

            Assert.Equal(2 * oldEighths, row.RowTotalSixteenths);     // every old length doubles exactly
            Assert.Equal(oldFeet, newFeet);

            var charges = new BillCharges { RatePerFoot = 45m, CarriageFare = 500m, SheetCuts = 2, RatePerSheet = 30m };
            Assert.Equal(decimal.Round(oldFeet * 45m, 2) + 500m + 60m, charges.TotalAmount(row.RowTotalSixteenths));
        }

        [Fact]
        public void ChargesSavedWithABill_StillComeBack()
        {
            var db = new DatabaseService();
            int billId = NewBill(db, "Shahid Mehmood");

            db.SaveMeasurement(new MeasurementEntry
            {
                BillId = billId, SerialNumber = 1,
                HeightInches = 40, WidthInches = 10, Quantity = 1
            });
            db.UpdateBillCharges(billId, new BillCharges
            {
                RatePerFoot = 60m, CarriageFare = 350m, SheetCuts = 4, RatePerSheet = 25m
            });

            var charges = db.GetBillCharges(billId);
            Assert.Equal(60m,  charges.RatePerFoot);
            Assert.Equal(350m, charges.CarriageFare);
            Assert.Equal(4,    charges.SheetCuts);
            Assert.Equal(25m,  charges.RatePerSheet);
        }
    }
}
