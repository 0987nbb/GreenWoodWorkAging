using GreenWoodWorkAging.Models;
using Microsoft.Data.Sqlite;

namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Manages all SQLite persistence for customers, bills, and measurement entries.
    /// Database is stored in %AppData%\GreenWoodWorkAging\ so it survives app restarts.
    /// </summary>
    public class DatabaseService : IDisposable
    {
        private readonly string _connectionString;

        // ── Construction / Schema ─────────────────────────────────────────────

        public DatabaseService()
        {
            string dir = AppPaths.DataDirectory;
            Directory.CreateDirectory(dir);

            string dbPath = AppPaths.DatabaseFile;
            _connectionString = $"Data Source={dbPath}";
            InitSchema();
        }

        private void InitSchema()
        {
            using var cn = Open();
            Exec(cn, "PRAGMA journal_mode=WAL;");
            Exec(cn, "PRAGMA foreign_keys=ON;");

            // Check if old schema exists and migrate
            try
            {
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(Measurements);";
                    using var rdr = cmd.ExecuteReader();
                    bool hasBillId = false;
                    while (rdr.Read())
                    {
                        if (rdr.GetString(1) == "BillId")
                            hasBillId = true;
                    }

                    // Old schema has ProjectId, new has BillId — drop old and recreate
                    if (!hasBillId)
                    {
                        try { Exec(cn, "DROP TABLE IF EXISTS Measurements;"); } catch { }
                        try { Exec(cn, "DROP TABLE IF EXISTS Projects;"); } catch { }
                        try { Exec(cn, "DROP TABLE IF EXISTS Customers;"); } catch { }
                    }
                }
            }
            catch { /* table doesn't exist yet, continue */ }

            // Bills table — one record per customer visit / bill
            Exec(cn, @"
                CREATE TABLE IF NOT EXISTS Bills (
                    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerName TEXT NOT NULL,
                    PhoneNumber  TEXT NOT NULL DEFAULT '',
                    BillDate     TEXT NOT NULL,
                    CreatedAt    TEXT NOT NULL,
                    RatePerFoot  REAL    NOT NULL DEFAULT 0,
                    CarriageFare REAL    NOT NULL DEFAULT 0,
                    SheetCuts    INTEGER NOT NULL DEFAULT 0,
                    RatePerSheet REAL    NOT NULL DEFAULT 0
                );");

            // Bills written before the charges existed have none of these columns.
            try
            {
                var billColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(Bills);";
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read()) billColumns.Add(rdr.GetString(1));
                }
                if (!billColumns.Contains("RatePerFoot"))  Exec(cn, "ALTER TABLE Bills ADD COLUMN RatePerFoot  REAL    NOT NULL DEFAULT 0;");
                if (!billColumns.Contains("CarriageFare")) Exec(cn, "ALTER TABLE Bills ADD COLUMN CarriageFare REAL    NOT NULL DEFAULT 0;");
                if (!billColumns.Contains("SheetCuts"))    Exec(cn, "ALTER TABLE Bills ADD COLUMN SheetCuts    INTEGER NOT NULL DEFAULT 0;");
                if (!billColumns.Contains("RatePerSheet")) Exec(cn, "ALTER TABLE Bills ADD COLUMN RatePerSheet REAL    NOT NULL DEFAULT 0;");
            }
            catch { }

            // Measurement rows — many per bill
            Exec(cn, @"
                CREATE TABLE IF NOT EXISTS Measurements (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    BillId       INTEGER NOT NULL REFERENCES Bills(Id) ON DELETE CASCADE,
                    SerialNumber INTEGER NOT NULL,
                    HeightInches INTEGER NOT NULL,
                    HeightSooter INTEGER NOT NULL,
                    WidthInches  INTEGER NOT NULL,
                    WidthSooter  INTEGER NOT NULL,
                    Quantity     INTEGER NOT NULL,
                    SideMode     INTEGER NOT NULL DEFAULT 0,
                    SheetCode    TEXT    NOT NULL DEFAULT '',
                    HeightHalf   INTEGER NOT NULL DEFAULT 0,
                    WidthHalf    INTEGER NOT NULL DEFAULT 0
                );");

            // Rows written before the side option existed have no SideMode column yet.
            try
            {
                bool hasSideMode = false;
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(Measurements);";
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                        if (rdr.GetString(1) == "SideMode") hasSideMode = true;
                }
                if (!hasSideMode)
                    Exec(cn, "ALTER TABLE Measurements ADD COLUMN SideMode INTEGER NOT NULL DEFAULT 0;");
            }
            catch { }

            // …and rows written before sizes were grouped by sheet have no SheetCode column.
            try
            {
                bool hasSheetCode = false;
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(Measurements);";
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                        if (rdr.GetString(1) == "SheetCode") hasSheetCode = true;
                }
                if (!hasSheetCode)
                    Exec(cn, "ALTER TABLE Measurements ADD COLUMN SheetCode TEXT NOT NULL DEFAULT '';");
            }
            catch { }

            // …and rows written before a size could carry a sady have no half-sooter column.
            // Both default to 0, which is what every older measurement means: no half sooter.
            try
            {
                var measureColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(Measurements);";
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read()) measureColumns.Add(rdr.GetString(1));
                }
                if (!measureColumns.Contains("HeightHalf"))
                    Exec(cn, "ALTER TABLE Measurements ADD COLUMN HeightHalf INTEGER NOT NULL DEFAULT 0;");
                if (!measureColumns.Contains("WidthHalf"))
                    Exec(cn, "ALTER TABLE Measurements ADD COLUMN WidthHalf  INTEGER NOT NULL DEFAULT 0;");
            }
            catch { }

            // One row per customer visit, and one SheetStock line per material inside it. These
            // tables are separate from bills on purpose: the ledger of sheets a customer leaves
            // here never feeds the measurement maths, the totals or the printed bill.
            Exec(cn, @"
                CREATE TABLE IF NOT EXISTS SheetStockVisits (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    BillId       INTEGER NOT NULL DEFAULT 0,
                    CustomerName TEXT    NOT NULL DEFAULT '',
                    PhoneNumber  TEXT    NOT NULL DEFAULT '',
                    StockDate    TEXT    NOT NULL,
                    CreatedAt    TEXT    NOT NULL DEFAULT ''
                );");

            Exec(cn, @"
                CREATE TABLE IF NOT EXISTS SheetStock (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    VisitId      INTEGER NOT NULL DEFAULT 0,
                    BillId       INTEGER NOT NULL DEFAULT 0,
                    CustomerName TEXT    NOT NULL DEFAULT '',
                    PhoneNumber  TEXT    NOT NULL DEFAULT '',
                    StockDate    TEXT    NOT NULL,
                    Material     TEXT    NOT NULL DEFAULT '',
                    Received     INTEGER NOT NULL DEFAULT 0,
                    Used         INTEGER NOT NULL DEFAULT 0
                );");

            // Lines saved before a visit grouped them belong to one now: each distinct customer,
            // phone and day becomes a visit holding exactly the lines it already had.
            try
            {
                bool hasVisitId = false;
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(SheetStock);";
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                        if (rdr.GetString(1) == "VisitId") hasVisitId = true;
                }
                if (!hasVisitId)
                    Exec(cn, "ALTER TABLE SheetStock ADD COLUMN VisitId INTEGER NOT NULL DEFAULT 0;");

                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT DISTINCT BillId, CustomerName, PhoneNumber, StockDate
                        FROM SheetStock WHERE VisitId = 0;";
                    var orphans = new List<(int bill, string name, string phone, string date)>();
                    using (var rdr = cmd.ExecuteReader())
                        while (rdr.Read())
                            orphans.Add((rdr.GetInt32(0), rdr.GetString(1), rdr.GetString(2), rdr.GetString(3)));

                    foreach (var (bill, name, phone, date) in orphans)
                    {
                        using var newVisit = cn.CreateCommand();
                        newVisit.CommandText = @"
                            INSERT INTO SheetStockVisits (BillId, CustomerName, PhoneNumber, StockDate, CreatedAt)
                            VALUES ($bill, $name, $phone, $date, $created);
                            SELECT last_insert_rowid();";
                        newVisit.Parameters.AddWithValue("$bill",    bill);
                        newVisit.Parameters.AddWithValue("$name",    name);
                        newVisit.Parameters.AddWithValue("$phone",   phone);
                        newVisit.Parameters.AddWithValue("$date",    date);
                        newVisit.Parameters.AddWithValue("$created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        int visitId = Convert.ToInt32(newVisit.ExecuteScalar());

                        using var link = cn.CreateCommand();
                        link.CommandText = @"
                            UPDATE SheetStock SET VisitId=$visit
                             WHERE VisitId=0 AND BillId=$bill AND CustomerName=$name
                               AND PhoneNumber=$phone AND StockDate=$date;";
                        link.Parameters.AddWithValue("$visit", visitId);
                        link.Parameters.AddWithValue("$bill",  bill);
                        link.Parameters.AddWithValue("$name",  name);
                        link.Parameters.AddWithValue("$phone", phone);
                        link.Parameters.AddWithValue("$date",  date);
                        link.ExecuteNonQuery();
                    }
                }
            }
            catch { }

            Exec(cn, "CREATE INDEX IF NOT EXISTS idx_bills_name  ON Bills(CustomerName COLLATE NOCASE);");
            Exec(cn, "CREATE INDEX IF NOT EXISTS idx_bills_phone ON Bills(PhoneNumber);");
            Exec(cn, "CREATE INDEX IF NOT EXISTS idx_meas_bill   ON Measurements(BillId);");
            Exec(cn, "CREATE INDEX IF NOT EXISTS idx_stock_name  ON SheetStock(CustomerName COLLATE NOCASE);");
            Exec(cn, "CREATE INDEX IF NOT EXISTS idx_stock_bill  ON SheetStock(BillId);");

            // Manual Invoices table — standalone customer bills created manually
            Exec(cn, @"
                CREATE TABLE IF NOT EXISTS ManualInvoices (
                    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerName    TEXT    NOT NULL,
                    PhoneNumber     TEXT    NOT NULL DEFAULT '',
                    InvoiceDate     TEXT    NOT NULL,
                    CreatedAt       TEXT    NOT NULL,
                    TotalTapesFeet  REAL    NOT NULL DEFAULT 0,
                    TapeRate        REAL    NOT NULL DEFAULT 0,
                    TapeAmount      REAL    NOT NULL DEFAULT 0,
                    SheetCuts       INTEGER NOT NULL DEFAULT 0,
                    RatePerSheetCut REAL    NOT NULL DEFAULT 0,
                    CuttingAmount   REAL    NOT NULL DEFAULT 0,
                    CarriageCharges REAL    NOT NULL DEFAULT 0,
                    GrandTotal      REAL    NOT NULL DEFAULT 0,
                    Notes           TEXT    NOT NULL DEFAULT ''
                );");
            Exec(cn, "CREATE INDEX IF NOT EXISTS idx_manual_inv_name  ON ManualInvoices(CustomerName COLLATE NOCASE);");
            Exec(cn, "CREATE INDEX IF NOT EXISTS idx_manual_inv_phone ON ManualInvoices(PhoneNumber);");
            Exec(cn, "CREATE INDEX IF NOT EXISTS idx_manual_inv_date  ON ManualInvoices(InvoiceDate);");
            Exec(cn, "CREATE INDEX IF NOT EXISTS idx_stock_visit ON SheetStock(VisitId);");
        }

        // ── Bill CRUD ─────────────────────────────────────────────────────────

        /// <summary>Insert a new bill and return its Id.</summary>
        public int CreateBill(string customerName, string phone, DateTime billDate)
        {
            using var cn  = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Bills (CustomerName, PhoneNumber, BillDate, CreatedAt)
                VALUES ($name, $phone, $date, $created);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$name",    customerName);
            cmd.Parameters.AddWithValue("$phone",   phone);
            cmd.Parameters.AddWithValue("$date",    billDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$created", DateTime.Now.ToString("o"));
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        /// <summary>Update customer details on an existing bill (used when user edits the header fields).</summary>
        public void UpdateBillHeader(int billId, string customerName, string phone, DateTime billDate)
        {
            using var cn  = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Bills
                SET CustomerName=$name, PhoneNumber=$phone, BillDate=$date
                WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$id",    billId);
            cmd.Parameters.AddWithValue("$name",  customerName);
            cmd.Parameters.AddWithValue("$phone", phone);
            cmd.Parameters.AddWithValue("$date",  billDate.ToString("yyyy-MM-dd"));
            cmd.ExecuteNonQuery();
        }

        /// <summary>The money charges (rates, carriage, sheet cuts) saved on a bill.</summary>
        public BillCharges GetBillCharges(int billId)
        {
            using var cn  = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                SELECT RatePerFoot, CarriageFare, SheetCuts, RatePerSheet
                FROM Bills WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$id", billId);
            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read()) return new BillCharges();
            return new BillCharges
            {
                RatePerFoot  = (decimal) rdr.GetDouble(0),
                CarriageFare = (decimal) rdr.GetDouble(1),
                SheetCuts    = rdr.GetInt32(2),
                RatePerSheet = (decimal) rdr.GetDouble(3)
            };
        }

        /// <summary>Overwrite the money charges on an existing bill.</summary>
        public void UpdateBillCharges(int billId, BillCharges charges)
        {
            using var cn  = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Bills
                SET RatePerFoot=$rate, CarriageFare=$carriage, SheetCuts=$cuts, RatePerSheet=$sheetRate
                WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$id",       billId);
            cmd.Parameters.AddWithValue("$rate",     (double) charges.RatePerFoot);
            cmd.Parameters.AddWithValue("$carriage", (double) charges.CarriageFare);
            cmd.Parameters.AddWithValue("$cuts",     charges.SheetCuts);
            cmd.Parameters.AddWithValue("$sheetRate",(double) charges.RatePerSheet);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Search bills by customer name OR phone OR date (partial match from first character).</summary>
        public List<BillSummary> SearchBills(string nameFilter = "", string phoneFilter = "", string dateFilter = "")
        {
            using var cn  = Open();
            using var cmd = cn.CreateCommand();

            var where = new List<string>();
            if (!string.IsNullOrWhiteSpace(nameFilter))
            {
                where.Add("b.CustomerName LIKE $name COLLATE NOCASE");
                cmd.Parameters.AddWithValue("$name", $"%{nameFilter.Trim()}%");
            }
            if (!string.IsNullOrWhiteSpace(phoneFilter))
            {
                where.Add("b.PhoneNumber LIKE $phone");
                cmd.Parameters.AddWithValue("$phone", $"%{phoneFilter.Trim()}%");
            }
            if (!string.IsNullOrWhiteSpace(dateFilter))
            {
                where.Add("b.BillDate LIKE $date");
                cmd.Parameters.AddWithValue("$date", $"%{dateFilter.Trim()}%");
            }

            // Use OR so single search box can match name, phone, or date
            // If all filters are empty, return all bills
            string w = where.Count > 0 ? "WHERE " + string.Join(" OR ", where) : "";
            cmd.CommandText = $@"
                SELECT b.Id, b.CustomerName, b.PhoneNumber, b.BillDate,
                       COUNT(m.Id) AS MeasCount
                FROM Bills b
                LEFT JOIN Measurements m ON m.BillId = b.Id
                {w}
                GROUP BY b.Id
                ORDER BY b.BillDate DESC, b.Id DESC;";

            var list = new List<BillSummary>();
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                list.Add(new BillSummary
                {
                    Id           = rdr.GetInt32(0),
                    CustomerName = rdr.GetString(1),
                    PhoneNumber  = rdr.GetString(2),
                    BillDate     = DateTime.Parse(rdr.GetString(3)),
                    MeasCount    = rdr.GetInt32(4)
                });
            }
            return list;
        }

        /// <summary>Get all bills without any filtering</summary>
        public List<BillSummary> GetAllBills()
        {
            return SearchBills("", "", "");
        }

        public BillSummary? GetBill(int billId)
        {
            var list = SearchBillById(billId);
            return list.Count > 0 ? list[0] : null;
        }

        private List<BillSummary> SearchBillById(int billId)
        {
            using var cn  = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                SELECT b.Id, b.CustomerName, b.PhoneNumber, b.BillDate,
                       COUNT(m.Id)
                FROM Bills b
                LEFT JOIN Measurements m ON m.BillId = b.Id
                WHERE b.Id = $id
                GROUP BY b.Id;";
            cmd.Parameters.AddWithValue("$id", billId);
            var list = new List<BillSummary>();
            using var rdr = cmd.ExecuteReader();
            if (rdr.Read())
                list.Add(new BillSummary
                {
                    Id           = rdr.GetInt32(0),
                    CustomerName = rdr.GetString(1),
                    PhoneNumber  = rdr.GetString(2),
                    BillDate     = DateTime.Parse(rdr.GetString(3)),
                    MeasCount    = rdr.GetInt32(4)
                });
            return list;
        }

        public void DeleteBill(int billId)
        {
            using var cn  = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM Bills WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$id", billId);
            cmd.ExecuteNonQuery();
        }

        // ── Measurement CRUD ──────────────────────────────────────────────────

        /// <summary>Insert or update a measurement. Sets m.Id on insert.</summary>
        public void SaveMeasurement(MeasurementEntry m)
        {
            using var cn = Open();
            if (m.Id == 0)
            {
                using var cmd = cn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Measurements
                        (BillId, SerialNumber, HeightInches, HeightSooter, WidthInches, WidthSooter, Quantity, SideMode, SheetCode, HeightHalf, WidthHalf)
                    VALUES ($bid, $sn, $hi, $hs, $wi, $ws, $qty, $side, $sheet, $hh, $wh);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$bid",  m.BillId);
                cmd.Parameters.AddWithValue("$sn",   m.SerialNumber);
                cmd.Parameters.AddWithValue("$hi",   m.HeightInches);
                cmd.Parameters.AddWithValue("$hs",   m.HeightSooter);
                cmd.Parameters.AddWithValue("$wi",   m.WidthInches);
                cmd.Parameters.AddWithValue("$ws",   m.WidthSooter);
                cmd.Parameters.AddWithValue("$qty",  m.Quantity);
                cmd.Parameters.AddWithValue("$side", (int)m.Side);
                cmd.Parameters.AddWithValue("$sheet", m.SheetCode ?? "");
                cmd.Parameters.AddWithValue("$hh",   m.HeightHalf);
                cmd.Parameters.AddWithValue("$wh",   m.WidthHalf);
                m.Id = Convert.ToInt32(cmd.ExecuteScalar());
            }
            else
            {
                using var cmd = cn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Measurements
                    SET HeightInches=$hi, HeightSooter=$hs,
                        WidthInches=$wi,  WidthSooter=$ws, Quantity=$qty, SideMode=$side,
                        SheetCode=$sheet, HeightHalf=$hh, WidthHalf=$wh
                    WHERE Id=$id;";
                cmd.Parameters.AddWithValue("$id",    m.Id);
                cmd.Parameters.AddWithValue("$hi",    m.HeightInches);
                cmd.Parameters.AddWithValue("$hs",    m.HeightSooter);
                cmd.Parameters.AddWithValue("$wi",    m.WidthInches);
                cmd.Parameters.AddWithValue("$ws",    m.WidthSooter);
                cmd.Parameters.AddWithValue("$qty",   m.Quantity);
                cmd.Parameters.AddWithValue("$side",  (int)m.Side);
                cmd.Parameters.AddWithValue("$sheet", m.SheetCode ?? "");
                cmd.Parameters.AddWithValue("$hh",    m.HeightHalf);
                cmd.Parameters.AddWithValue("$wh",    m.WidthHalf);
                cmd.ExecuteNonQuery();
            }
        }

        public List<MeasurementEntry> GetMeasurements(int billId)
        {
            using var cn  = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, BillId, SerialNumber,
                       HeightInches, HeightSooter, WidthInches, WidthSooter, Quantity,
                       IFNULL(SideMode, 0), IFNULL(SheetCode, ''),
                       IFNULL(HeightHalf, 0), IFNULL(WidthHalf, 0)
                FROM Measurements
                WHERE BillId=$bid
                ORDER BY SerialNumber;";
            cmd.Parameters.AddWithValue("$bid", billId);

            var list = new List<MeasurementEntry>();
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                list.Add(new MeasurementEntry
                {
                    Id           = rdr.GetInt32(0),
                    BillId       = rdr.GetInt32(1),
                    SerialNumber = rdr.GetInt32(2),
                    HeightInches = rdr.GetInt32(3),
                    HeightSooter = rdr.GetInt32(4),
                    WidthInches  = rdr.GetInt32(5),
                    WidthSooter  = rdr.GetInt32(6),
                    Quantity     = rdr.GetInt32(7),
                    Side         = (MeasurementSide)rdr.GetInt32(8),
                    SheetCode    = rdr.GetString(9),
                    HeightHalf   = rdr.GetInt32(10),
                    WidthHalf    = rdr.GetInt32(11)
                });
            }
            return list;
        }

        public void DeleteMeasurement(int measId)
        {
            using var cn  = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM Measurements WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$id", measId);
            cmd.ExecuteNonQuery();
        }

        public void DeleteAllMeasurements(int billId)
        {
            using var cn  = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM Measurements WHERE BillId=$bid;";
            cmd.Parameters.AddWithValue("$bid", billId);
            cmd.ExecuteNonQuery();
        }

        // ── Sheet stock ───────────────────────────────────────────────────────

        /// <summary>
        /// Save a customer's whole delivery in one go: the visit header plus every material line
        /// under it. Lines that disappeared from the window are deleted, new ones inserted, and
        /// the rest updated — so the sheet the shop sees is the sheet that was typed.
        /// </summary>
        public SheetStockVisit SaveSheetStockVisit(SheetStockVisit visit)
        {
            using var cn = Open();
            string  date = visit.Date.ToString("yyyy-MM-dd");

            if (visit.Id == 0)
            {
                using var ins = cn.CreateCommand();
                ins.CommandText = @"
                    INSERT INTO SheetStockVisits (BillId, CustomerName, PhoneNumber, StockDate, CreatedAt)
                    VALUES ($bill, $name, $phone, $date, $created);
                    SELECT last_insert_rowid();";
                ins.Parameters.AddWithValue("$bill",    visit.BillId);
                ins.Parameters.AddWithValue("$name",    visit.CustomerName);
                ins.Parameters.AddWithValue("$phone",   visit.PhoneNumber);
                ins.Parameters.AddWithValue("$date",    date);
                ins.Parameters.AddWithValue("$created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                visit.Id = Convert.ToInt32(ins.ExecuteScalar());
            }
            else
            {
                using var upd = cn.CreateCommand();
                upd.CommandText = @"
                    UPDATE SheetStockVisits
                       SET BillId=$bill, CustomerName=$name, PhoneNumber=$phone, StockDate=$date
                     WHERE Id=$id;";
                upd.Parameters.AddWithValue("$bill",  visit.BillId);
                upd.Parameters.AddWithValue("$name",  visit.CustomerName);
                upd.Parameters.AddWithValue("$phone", visit.PhoneNumber);
                upd.Parameters.AddWithValue("$date",  date);
                upd.Parameters.AddWithValue("$id",    visit.Id);
                upd.ExecuteNonQuery();
            }

            // A line removed from the window is removed from the shelf too.
            using (var gone = cn.CreateCommand())
            {
                gone.CommandText = "DELETE FROM SheetStock WHERE VisitId=$visit;";
                gone.Parameters.AddWithValue("$visit", visit.Id);
                gone.ExecuteNonQuery();
            }

            foreach (var line in visit.Lines)
            {
                line.Id       = 0;
                line.VisitId  = visit.Id;
                line.BillId   = visit.BillId;
                line.CustomerName = visit.CustomerName;
                line.PhoneNumber  = visit.PhoneNumber;
                line.Date         = visit.Date;
                WriteLine(cn, line);
            }

            return visit;
        }

        /// <summary>Save one material line on its own — used when only the count cut changes.</summary>
        public SheetStockEntry SaveSheetStock(SheetStockEntry e)
        {
            using var cn = Open();
            WriteLine(cn, e);
            return e;
        }

        private static void WriteLine(SqliteConnection cn, SheetStockEntry e)
        {
            if (e.Id == 0)
            {
                using var ins = cn.CreateCommand();
                ins.CommandText = @"
                    INSERT INTO SheetStock (VisitId, BillId, CustomerName, PhoneNumber, StockDate, Material, Received, Used)
                    VALUES ($visit, $bill, $name, $phone, $date, $material, $received, $used);
                    SELECT last_insert_rowid();";
                AddStockParams(ins, e);
                e.Id = Convert.ToInt32(ins.ExecuteScalar());
            }
            else
            {
                using var upd = cn.CreateCommand();
                upd.CommandText = @"
                    UPDATE SheetStock
                       SET VisitId=$visit, BillId=$bill, CustomerName=$name, PhoneNumber=$phone,
                           StockDate=$date, Material=$material, Received=$received, Used=$used
                     WHERE Id=$id;";
                AddStockParams(upd, e);
                upd.Parameters.AddWithValue("$id", e.Id);
                upd.ExecuteNonQuery();
            }

            static void AddStockParams(SqliteCommand cmd, SheetStockEntry e)
            {
                cmd.Parameters.AddWithValue("$visit",    e.VisitId);
                cmd.Parameters.AddWithValue("$bill",     e.BillId);
                cmd.Parameters.AddWithValue("$name",     e.CustomerName ?? "");
                cmd.Parameters.AddWithValue("$phone",    e.PhoneNumber ?? "");
                cmd.Parameters.AddWithValue("$date",     e.Date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("$material", e.Material ?? "");
                cmd.Parameters.AddWithValue("$received", e.Received);
                cmd.Parameters.AddWithValue("$used",     e.Used);
            }
        }

        /// <summary>
        /// Deliveries, newest first, each carrying its material lines. The one search box matches
        /// the customer's name, phone, the day, or any of the materials in the visit.
        /// </summary>
        public List<SheetStockVisit> SearchSheetStockVisits(string filter = "")
        {
            using var cn  = Open();
            using var cmd = cn.CreateCommand();

            string f = filter.Trim();
            string where = "";
            if (f.Length > 0)
            {
                where = @"WHERE v.CustomerName LIKE $f COLLATE NOCASE
                            OR v.PhoneNumber  LIKE $f
                            OR v.StockDate    LIKE $f
                            OR EXISTS (SELECT 1 FROM SheetStock s
                                        WHERE s.VisitId = v.Id AND s.Material LIKE $f COLLATE NOCASE)";
                cmd.Parameters.AddWithValue("$f", $"%{f}%");
            }

            cmd.CommandText = $@"
                SELECT v.Id, v.BillId, v.CustomerName, v.PhoneNumber, v.StockDate
                FROM SheetStockVisits v
                {where}
                ORDER BY v.StockDate DESC, v.Id DESC;";

            var visits = new List<SheetStockVisit>();
            var byId   = new Dictionary<int, SheetStockVisit>();
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    var visit = new SheetStockVisit
                    {
                        Id           = rdr.GetInt32(0),
                        BillId       = rdr.GetInt32(1),
                        CustomerName = rdr.GetString(2),
                        PhoneNumber  = rdr.GetString(3),
                        Date         = DateTime.TryParse(rdr.GetString(4), out var d) ? d : DateTime.Today
                    };
                    visits.Add(visit);
                    byId[visit.Id] = visit;
                }
            }

            using (var lines = cn.CreateCommand())
            {
                lines.CommandText = @"
                    SELECT Id, VisitId, BillId, CustomerName, PhoneNumber, StockDate, Material, Received, Used
                    FROM SheetStock ORDER BY Id;";
                using var rdr = lines.ExecuteReader();
                while (rdr.Read())
                {
                    var line = new SheetStockEntry
                    {
                        Id           = rdr.GetInt32(0),
                        VisitId      = rdr.GetInt32(1),
                        BillId       = rdr.GetInt32(2),
                        CustomerName = rdr.GetString(3),
                        PhoneNumber  = rdr.GetString(4),
                        Date         = DateTime.TryParse(rdr.GetString(5), out var d) ? d : DateTime.Today,
                        Material     = rdr.GetString(6),
                        Received     = rdr.GetInt32(7),
                        Used         = rdr.GetInt32(8)
                    };
                    if (byId.TryGetValue(line.VisitId, out var visit))
                        visit.Lines.Add(line);
                }
            }

            return visits;
        }

        /// <summary>One delivery, lines included, or null when it no longer exists.</summary>
        public SheetStockVisit? GetSheetStockVisit(int visitId) =>
            SearchSheetStockVisits("").FirstOrDefault(v => v.Id == visitId);

        /// <summary>Erase a whole delivery — every material line that came in with it goes too.</summary>
        public void DeleteSheetStockVisit(int visitId)
        {
            using var cn = Open();
            using (var lines = cn.CreateCommand())
            {
                lines.CommandText = "DELETE FROM SheetStock WHERE VisitId=$id;";
                lines.Parameters.AddWithValue("$id", visitId);
                lines.ExecuteNonQuery();
            }
            using var head = cn.CreateCommand();
            head.CommandText = "DELETE FROM SheetStockVisits WHERE Id=$id;";
            head.Parameters.AddWithValue("$id", visitId);
            head.ExecuteNonQuery();
        }

        /// <summary>Record how many of one material line have now been cut.</summary>
        public void SetSheetStockUsed(int id, int used)
        {
            using var cn  = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE SheetStock SET Used=$used WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$used", Math.Max(0, used));
            cmd.Parameters.AddWithValue("$id",   id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteSheetStock(int id)
        {
            using var cn  = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM SheetStock WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Manual Invoices
        // ════════════════════════════════════════════════════════════════════════

        public ManualInvoice SaveManualInvoice(ManualInvoice inv)
        {
            using var cn = Open();
            string date = inv.InvoiceDate.ToString("yyyy-MM-dd");

            if (inv.Id == 0)
            {
                using var ins = cn.CreateCommand();
                ins.CommandText = @"
                    INSERT INTO ManualInvoices (CustomerName, PhoneNumber, InvoiceDate, CreatedAt,
                                               TotalTapesFeet, TapeRate, TapeAmount,
                                               SheetCuts, RatePerSheetCut, CuttingAmount,
                                               CarriageCharges, GrandTotal, Notes)
                    VALUES ($name, $phone, $date, $created,
                            $tapes, $tapeRate, $tapeAmt,
                            $cuts, $cutRate, $cutAmt,
                            $carriage, $grandTotal, $notes);
                    SELECT last_insert_rowid();";
                ins.Parameters.AddWithValue("$name",       inv.CustomerName);
                ins.Parameters.AddWithValue("$phone",      inv.PhoneNumber);
                ins.Parameters.AddWithValue("$date",       date);
                ins.Parameters.AddWithValue("$created",    inv.CreatedAt.ToString("o"));
                ins.Parameters.AddWithValue("$tapes",      (double)inv.TotalTapesFeet);
                ins.Parameters.AddWithValue("$tapeRate",   (double)inv.TapeRate);
                ins.Parameters.AddWithValue("$tapeAmt",    (double)inv.TapeAmount);
                ins.Parameters.AddWithValue("$cuts",       inv.SheetCuts);
                ins.Parameters.AddWithValue("$cutRate",    (double)inv.RatePerSheetCut);
                ins.Parameters.AddWithValue("$cutAmt",     (double)inv.CuttingAmount);
                ins.Parameters.AddWithValue("$carriage",   (double)inv.CarriageCharges);
                ins.Parameters.AddWithValue("$grandTotal", (double)inv.GrandTotal);
                ins.Parameters.AddWithValue("$notes",      inv.Notes ?? "");
                inv.Id = Convert.ToInt32(ins.ExecuteScalar());
            }
            else
            {
                using var upd = cn.CreateCommand();
                upd.CommandText = @"
                    UPDATE ManualInvoices
                       SET CustomerName    = $name,
                           PhoneNumber     = $phone,
                           InvoiceDate     = $date,
                           TotalTapesFeet  = $tapes,
                           TapeRate        = $tapeRate,
                           TapeAmount      = $tapeAmt,
                           SheetCuts       = $cuts,
                           RatePerSheetCut = $cutRate,
                           CuttingAmount   = $cutAmt,
                           CarriageCharges = $carriage,
                           GrandTotal      = $grandTotal,
                           Notes           = $notes
                     WHERE Id = $id;";
                upd.Parameters.AddWithValue("$name",       inv.CustomerName);
                upd.Parameters.AddWithValue("$phone",      inv.PhoneNumber);
                upd.Parameters.AddWithValue("$date",       date);
                upd.Parameters.AddWithValue("$tapes",      (double)inv.TotalTapesFeet);
                upd.Parameters.AddWithValue("$tapeRate",   (double)inv.TapeRate);
                upd.Parameters.AddWithValue("$tapeAmt",    (double)inv.TapeAmount);
                upd.Parameters.AddWithValue("$cuts",       inv.SheetCuts);
                upd.Parameters.AddWithValue("$cutRate",    (double)inv.RatePerSheetCut);
                upd.Parameters.AddWithValue("$cutAmt",     (double)inv.CuttingAmount);
                upd.Parameters.AddWithValue("$carriage",   (double)inv.CarriageCharges);
                upd.Parameters.AddWithValue("$grandTotal", (double)inv.GrandTotal);
                upd.Parameters.AddWithValue("$notes",      inv.Notes ?? "");
                upd.Parameters.AddWithValue("$id",         inv.Id);
                upd.ExecuteNonQuery();
            }

            return inv;
        }

        public List<ManualInvoice> SearchManualInvoices(string filter = "")
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();

            string f = filter.Trim();
            string where = "";
            if (!string.IsNullOrEmpty(f))
            {
                where = @"WHERE CustomerName LIKE $f COLLATE NOCASE
                             OR PhoneNumber LIKE $f
                             OR InvoiceDate LIKE $f";
                cmd.Parameters.AddWithValue("$f", $"%{f}%");
            }

            cmd.CommandText = $@"
                SELECT Id, CustomerName, PhoneNumber, InvoiceDate, CreatedAt,
                       TotalTapesFeet, TapeRate, TapeAmount,
                       SheetCuts, RatePerSheetCut, CuttingAmount,
                       CarriageCharges, GrandTotal, Notes
                  FROM ManualInvoices
                  {where}
                 ORDER BY InvoiceDate DESC, Id DESC;";

            var list = new List<ManualInvoice>();
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                list.Add(new ManualInvoice
                {
                    Id              = rdr.GetInt32(0),
                    CustomerName    = rdr.GetString(1),
                    PhoneNumber     = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                    InvoiceDate     = DateTime.TryParse(rdr.GetString(3), out var d) ? d : DateTime.Today,
                    CreatedAt       = DateTime.TryParse(rdr.GetString(4), out var c) ? c : DateTime.Now,
                    TotalTapesFeet  = Convert.ToDecimal(rdr.GetDouble(5)),
                    TapeRate        = Convert.ToDecimal(rdr.GetDouble(6)),
                    SheetCuts       = rdr.GetInt32(8),
                    RatePerSheetCut = Convert.ToDecimal(rdr.GetDouble(9)),
                    CarriageCharges = Convert.ToDecimal(rdr.GetDouble(11)),
                    Notes           = rdr.IsDBNull(13) ? "" : rdr.GetString(13)
                });
            }

            return list;
        }

        public ManualInvoice? GetManualInvoice(int id)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, CustomerName, PhoneNumber, InvoiceDate, CreatedAt,
                       TotalTapesFeet, TapeRate, TapeAmount,
                       SheetCuts, RatePerSheetCut, CuttingAmount,
                       CarriageCharges, GrandTotal, Notes
                  FROM ManualInvoices
                 WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read()) return null;

            return new ManualInvoice
            {
                Id              = rdr.GetInt32(0),
                CustomerName    = rdr.GetString(1),
                PhoneNumber     = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                InvoiceDate     = DateTime.TryParse(rdr.GetString(3), out var d) ? d : DateTime.Today,
                CreatedAt       = DateTime.TryParse(rdr.GetString(4), out var c) ? c : DateTime.Now,
                TotalTapesFeet  = Convert.ToDecimal(rdr.GetDouble(5)),
                TapeRate        = Convert.ToDecimal(rdr.GetDouble(6)),
                SheetCuts       = rdr.GetInt32(8),
                RatePerSheetCut = Convert.ToDecimal(rdr.GetDouble(9)),
                CarriageCharges = Convert.ToDecimal(rdr.GetDouble(11)),
                Notes           = rdr.IsDBNull(13) ? "" : rdr.GetString(13)
            };
        }

        public void DeleteManualInvoice(int id)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM ManualInvoices WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private SqliteConnection Open()
        {
            var cn = new SqliteConnection(_connectionString);
            cn.Open();
            return cn;
        }

        private static void Exec(SqliteConnection cn, string sql)
        {
            using var cmd = cn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        public void Dispose() { }
    }

    // ── Lightweight read-model ────────────────────────────────────────────────

    public class BillSummary
    {
        public int      Id           { get; set; }
        public string   CustomerName { get; set; } = "";
        public string   PhoneNumber  { get; set; } = "";
        public DateTime BillDate     { get; set; }
        public int      MeasCount    { get; set; }
    }
}
