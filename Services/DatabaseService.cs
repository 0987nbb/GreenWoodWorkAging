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
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GreenWoodWorkAging");
            Directory.CreateDirectory(dir);

            string dbPath = Path.Combine(dir, "greenwoodwork.db");
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
                    SideMode     INTEGER NOT NULL DEFAULT 0
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

            Exec(cn, "CREATE INDEX IF NOT EXISTS idx_bills_name  ON Bills(CustomerName COLLATE NOCASE);");
            Exec(cn, "CREATE INDEX IF NOT EXISTS idx_bills_phone ON Bills(PhoneNumber);");
            Exec(cn, "CREATE INDEX IF NOT EXISTS idx_meas_bill   ON Measurements(BillId);");
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
                        (BillId, SerialNumber, HeightInches, HeightSooter, WidthInches, WidthSooter, Quantity, SideMode)
                    VALUES ($bid, $sn, $hi, $hs, $wi, $ws, $qty, $side);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$bid",  m.BillId);
                cmd.Parameters.AddWithValue("$sn",   m.SerialNumber);
                cmd.Parameters.AddWithValue("$hi",   m.HeightInches);
                cmd.Parameters.AddWithValue("$hs",   m.HeightSooter);
                cmd.Parameters.AddWithValue("$wi",   m.WidthInches);
                cmd.Parameters.AddWithValue("$ws",   m.WidthSooter);
                cmd.Parameters.AddWithValue("$qty",  m.Quantity);
                cmd.Parameters.AddWithValue("$side", (int)m.Side);
                m.Id = Convert.ToInt32(cmd.ExecuteScalar());
            }
            else
            {
                using var cmd = cn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Measurements
                    SET HeightInches=$hi, HeightSooter=$hs,
                        WidthInches=$wi,  WidthSooter=$ws, Quantity=$qty, SideMode=$side
                    WHERE Id=$id;";
                cmd.Parameters.AddWithValue("$id",    m.Id);
                cmd.Parameters.AddWithValue("$hi",    m.HeightInches);
                cmd.Parameters.AddWithValue("$hs",    m.HeightSooter);
                cmd.Parameters.AddWithValue("$wi",    m.WidthInches);
                cmd.Parameters.AddWithValue("$ws",    m.WidthSooter);
                cmd.Parameters.AddWithValue("$qty",   m.Quantity);
                cmd.Parameters.AddWithValue("$side",  (int)m.Side);
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
                       IFNULL(SideMode, 0)
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
                    Side         = (MeasurementSide)rdr.GetInt32(8)
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
