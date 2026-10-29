using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;
using System.Drawing.Drawing2D;

namespace GreenWoodWorkAging
{
    /// <summary>
    /// Carpenter Measurement Calculator — single form.
    ///
    /// New features added on top of the original design:
    ///   • Customer strip at the top (Name, Date, Phone)
    ///   • Automatic SQLite save on every add / edit / delete
    ///   • Bill History panel (replaces inactive nav items)
    ///   • Search by name or phone, open old bills, print
    ///   • Single "X Inch Y Sooter" label in grid + bill
    ///   • Sooter NUD hard-limited to 0–7
    /// </summary>
    public class CarpenterMeasurementCalculatorForm : Form
    {
        // ── Services ──────────────────────────────────────────────────────────
        private readonly MeasurementCalculatorService _calc      = new();
        private readonly MeasurementInputValidator    _validator = new();
        private readonly MeasurementDisplayFormatter  _fmt       = new();
        private readonly DatabaseService              _db        = new();

        // ── Current Bill State ────────────────────────────────────────────────
        private int  _currentBillId  = 0;   // 0 = not saved yet
        private readonly List<MeasurementEntry> _measurements = new();
        private int  _nextSerial   = 1;
        private int? _editingIndex = null;

        // ── Bill History State ────────────────────────────────────────────────
        private List<BillSummary> _historyList  = new();
        private bool              _historyVisible = false;

        // ── Controls — customer strip ─────────────────────────────────────────
        private TextBox       txtCustName   = null!;
        private DateTimePicker dtpBillDate  = null!;
        private TextBox       txtPhone      = null!;
        private Label         lblSaveStatus = null!;

        // ── Controls — measurement inputs (single combined TextBox per dimension) ────
        private TextBox        txtHeight = null!;  // "79.7" format (79 inches + 7 sooter)
        private TextBox        txtWidth  = null!;  // "48.3" format (48 inches + 3 sooter)
        private NumericUpDown  nudQuantity     = null!;
        private ComboBox       _cmbSides      = null!;  // 1/2/3/4 tape-edge selection
        private Label          lblError        = null!;
        private Button         btnAdd          = null!;
        private Button         btnCancel       = null!;
        private DataGridView   dgv             = null!;
        private Label          lblTotalRows    = null!;
        private Label          lblTotalPieces  = null!;
        private Label          lblGrandTotal   = null!;
        private Label          lblGrandSub     = null!;
        private Label          lblFeet         = null!;
        private Label          lblFeetSub      = null!;

        // ── Controls — bill charges (money) ───────────────────────────────────
        private NumericUpDown nudRateFoot   = null!;
        private NumericUpDown nudCarriage   = null!;
        private NumericUpDown nudSheetsCut  = null!;
        private NumericUpDown nudRateSheet  = null!;
        private Label         lblTapeAmount = null!;
        private Label         lblSheetAmount= null!;
        private Label         lblTotalAmount= null!;
        private System.Windows.Forms.Timer? _chargeSaveTimer;
        private bool          _loadingCharges;   // stops a bill load from re-saving itself
        private Label          lblClock        = null!;
        private System.Windows.Forms.Timer _clockTimer = null!;

        // ── Logo — sidebar tile and the watermark behind the measurement grid ──
        private readonly Image? _logo;
        private readonly Image? _watermark;

        // ── Controls — sidebar history panel (kept for reference if needed later) ─────
        private Panel?        pnlHistory      = null;
        private TextBox?      txtSearchName   = null;
        private ListBox?      lstHistory      = null;

        // ── Controls — dashboard search (now in header) ───────────────────────
        private Panel?         _mainArea       = null;
        private TextBox?       txtDashSearch   = null!;
        private ListBox?       lstDashResults  = null!;
        private List<BillSummary> _dashList   = new();

        // ── Design Palette ────────────────────────────────────────────────────
        private static readonly Color SidebarBg     = Color.FromArgb(30, 48, 37);
        private static readonly Color SidebarHover  = Color.FromArgb(44, 72, 55);
        private static readonly Color SidebarActive = Color.FromArgb(52, 133, 90);
        private static readonly Color SidebarText   = Color.FromArgb(170, 208, 185);
        private static readonly Color CBg           = Color.FromArgb(242, 245, 249);
        private static readonly Color CCard         = Color.White;
        private static readonly Color CBorder       = Color.FromArgb(218, 226, 234);
        private static readonly Color CGreen        = Color.FromArgb(34, 139, 87);
        private static readonly Color CGreenDark    = Color.FromArgb(22, 108, 65);
        private static readonly Color CGreenLight   = Color.FromArgb(236, 252, 243);
        private static readonly Color CBrown        = Color.FromArgb(181, 114, 46);
        private static readonly Color CBrownLight   = Color.FromArgb(255, 246, 235);
        private static readonly Color CBlue         = Color.FromArgb(37, 99, 180);
        private static readonly Color CBlueLight    = Color.FromArgb(235, 243, 255);
        private static readonly Color CRed          = Color.FromArgb(203, 40, 40);
        private static readonly Color CRedLight     = Color.FromArgb(255, 243, 243);
        private static readonly Color CWarn         = Color.FromArgb(180, 108, 18);
        private static readonly Color CGridHdr      = Color.FromArgb(245, 250, 247);
        private static readonly Color CGridAlt      = Color.FromArgb(248, 252, 249);
        private static readonly Color CSelected     = Color.FromArgb(215, 245, 228);
        private static readonly Color CText         = Color.FromArgb(24, 30, 42);
        private static readonly Color CSub          = Color.FromArgb(130, 142, 158);
        private static readonly Color CWatermark    = Color.FromArgb(52, 133, 90);

        // ── Fonts ─────────────────────────────────────────────────────────────
        private static readonly Font FSideName   = new("Segoe UI", 11f, FontStyle.Bold);
        private static readonly Font FSideTag    = new("Segoe UI", 8.5f);
        private static readonly Font FNavActive  = new("Segoe UI", 9.5f, FontStyle.Bold);
        private static readonly Font FNavNormal  = new("Segoe UI", 9.5f);
        private static readonly Font FHTitle     = new("Segoe UI", 16f, FontStyle.Bold);
        private static readonly Font FHSub       = new("Segoe UI", 9f);
        private static readonly Font FClock      = new("Segoe UI", 9f);
        private static readonly Font FSecTitle   = new("Segoe UI", 11.5f, FontStyle.Bold);
        private static readonly Font FFieldLabel = new("Segoe UI", 9.5f, FontStyle.Bold);
        private static readonly Font FFieldSub   = new("Segoe UI", 8.5f);
        private static readonly Font FInput      = new("Segoe UI", 12f);
        private static readonly Font FMoney      = new("Segoe UI", 11f, FontStyle.Bold);
        private static readonly Font FBtn        = new("Segoe UI", 10f, FontStyle.Bold);
        private static readonly Font FGridCell   = new("Segoe UI", 10f);
        private static readonly Font FGridSub    = new("Segoe UI", 8f);
        private static readonly Font FGridHdr    = new("Segoe UI", 9.5f, FontStyle.Bold);
        private static readonly Font FSumCap     = new("Segoe UI", 8.5f);
        private static readonly Font FSumVal     = new("Segoe UI", 15f, FontStyle.Bold);
        private static readonly Font FSumSub     = new("Segoe UI", 8.5f);
        private static readonly Font FCustLabel  = new("Segoe UI", 9f, FontStyle.Bold);
        private static readonly Font FCustInput  = new("Segoe UI", 10.5f);

        // ══════════════════════════════════════════════════════════════════════
        public CarpenterMeasurementCalculatorForm()
        {
            _logo = LoadLogo();
            _watermark = _logo == null ? null : BuildWatermark(_logo);
            SuspendLayout();
            BuildUI();
            ResumeLayout(false);
            StartClock();
            ResetInputs();
            RefreshTotals();
            HookDropdownTracking();
        }

        /// <summary>Keeps the search dropdown glued under its textbox while the window moves or resizes.</summary>
        private void HookDropdownTracking()
        {
            void Sync(object? _, EventArgs __)
            {
                if (lstDashResults == null || !lstDashResults.Visible) return;
                PositionDashDropdown(_dashList.Count == 0 ? 1 : Math.Min(_dashList.Count, 8));
            }
            Resize += Sync;
            Move   += Sync;
        }

        // ════════════════════════════════════════════════════════════════════
        //  UI Construction
        // ════════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            Text          = "Carpenter Measurement Calculator";
            Size          = new Size(1400, 900);
            MinimumSize   = new Size(1280, 780);
            BackColor     = CBg;
            Font          = new Font("Segoe UI", 9.5f);
            StartPosition = FormStartPosition.CenterScreen;

            var main = BuildMainArea();
            Controls.Add(main);
        }

        // ── Sidebar ───────────────────────────────────────────────────────────

        private Panel BuildSidebar()
        {
            // Use a plain Panel with absolute positions so rows NEVER overlap.
            var sidebar = new Panel
            {
                Width     = 232,
                Dock      = DockStyle.Left,
                BackColor = SidebarBg
            };

            // Logo (top 130 px)
            var logo = BuildLogoArea();
            logo.SetBounds(0, 0, 232, 130);
            logo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            sidebar.Controls.Add(logo);

            // Company name strip (130–184)
            var company = BuildCompanyArea();
            company.SetBounds(0, 130, 232, 54);
            company.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            sidebar.Controls.Add(company);

            // Nav items 3 × 42 px = 126 px  (184–324)
            var nav = BuildNavItems();
            nav.SetBounds(0, 184, 232, 140);
            nav.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            sidebar.Controls.Add(nav);

            // History panel — fills everything between nav and footer
            pnlHistory = BuildHistoryPanel();
            pnlHistory.SetBounds(0, 324, 232, 300);
            pnlHistory.Anchor  = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnlHistory.Visible = false;
            sidebar.Controls.Add(pnlHistory);

            // Footer pinned to bottom
            var footer = BuildSideFooter();
            footer.Dock   = DockStyle.Bottom;
            footer.Height = 54;
            sidebar.Controls.Add(footer);

            return sidebar;
        }

        private static Image? LoadLogo()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", "logo.jpg");
            if (!File.Exists(path)) return null;
            try { return Image.FromFile(path); } catch { return null; }
        }

        private Panel BuildLogoArea()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12) };
            if (_logo != null)
            {
                p.Controls.Add(new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = _logo, BackColor = Color.White });
                return p;
            }
            p.BackColor = SidebarBg;
            p.Controls.Add(new Label { Text = "GW", Font = new Font("Segoe UI", 36f, FontStyle.Bold), ForeColor = CGreen, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
            return p;
        }

        private Panel BuildCompanyArea()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(24, 40, 30), Padding = new Padding(16, 8, 16, 4) };
            var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Transparent };
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tbl.Controls.Add(new Label { Text = "GreenWoodWorkAging", Font = FSideName, ForeColor = Color.White, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft }, 0, 0);
            tbl.Controls.Add(new Label { Text = "Crafting Better Spaces", Font = FSideTag, ForeColor = SidebarText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft }, 0, 1);
            p.Controls.Add(tbl);
            return p;
        }

        private Panel BuildNavItems()
        {
            var p = new Panel { BackColor = SidebarBg, Padding = new Padding(0, 6, 0, 0) };
            var sep = new Panel { Height = 1, Dock = DockStyle.Top, BackColor = Color.FromArgb(48, 72, 56) };
            p.Controls.Add(sep);

            // Calculator (always active)
            var calc = BuildNavItem("🧮", "Measurement Calc", true,  null);
            calc.SetBounds(0, 7, 232, 42);
            p.Controls.Add(calc);

            // Bill History (toggle)
            var hist = BuildNavItem("📋", "Bill History",     false, ToggleHistory);
            hist.SetBounds(0, 51, 232, 42);
            p.Controls.Add(hist);

            // New Bill
            var newBill = BuildNavItem("＋", "New Bill",       false, StartNewBill);
            newBill.SetBounds(0, 95, 232, 42);
            p.Controls.Add(newBill);

            return p;
        }

        private Panel BuildNavItem(string icon, string text, bool active, Action? onClick)
        {
            var back = active ? SidebarActive : SidebarBg;
            var p = new Panel { BackColor = back, Cursor = Cursors.Hand };

            if (active)
                p.Controls.Add(new Panel { Width = 4, Dock = DockStyle.Left, BackColor = Color.FromArgb(150, 230, 175) });

            var lbl = new Label
            {
                Text      = $"   {icon}  {text}",
                Font      = active ? FNavActive : FNavNormal,
                ForeColor = active ? Color.White : SidebarText,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(active ? 8 : 12, 0, 0, 0)
            };
            p.Controls.Add(lbl);

            if (!active && onClick != null)
            {
                p.MouseEnter   += (_, _) => { p.BackColor = SidebarHover; lbl.ForeColor = Color.White; };
                p.MouseLeave   += (_, _) => { p.BackColor = back; lbl.ForeColor = SidebarText; };
                lbl.MouseEnter += (_, _) => { p.BackColor = SidebarHover; lbl.ForeColor = Color.White; };
                lbl.MouseLeave += (_, _) => { p.BackColor = back; lbl.ForeColor = SidebarText; };
                p.Click        += (_, _) => onClick();
                lbl.Click      += (_, _) => onClick();
            }
            return p;
        }

        // ── History Panel (inside sidebar) ────────────────────────────────────

        private Panel BuildHistoryPanel()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(22, 38, 28), Visible = false };

            // Title bar
            var title = new Label
            {
                Text      = "  📋  Bill History",
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.White,
                Dock      = DockStyle.Top,
                Height    = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(18, 32, 22)
            };
            p.Controls.Add(title);

            // Single combined search box (name OR phone, partial match from 1st char)
            var searchPnl = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(22, 38, 28), Padding = new Padding(6, 6, 6, 0) };
            txtSearchName = new TextBox
            {
                PlaceholderText = "Search name or phone…",
                Dock      = DockStyle.Fill,
                Font      = new Font("Segoe UI", 9f),
                BackColor = Color.FromArgb(38, 60, 44),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtSearchName.TextChanged += (_, _) => RefreshHistory();
            searchPnl.Controls.Add(txtSearchName);
            p.Controls.Add(searchPnl);

            // Bill list
            lstHistory = new ListBox
            {
                Dock          = DockStyle.Fill,
                BackColor     = Color.FromArgb(28, 46, 32),
                ForeColor     = Color.FromArgb(200, 230, 210),
                Font          = new Font("Segoe UI", 8.5f),
                BorderStyle   = BorderStyle.None,
                DrawMode      = DrawMode.OwnerDrawFixed,
                ItemHeight    = 46
            };
            lstHistory.DrawItem    += DrawHistoryItem;
            lstHistory.DoubleClick += OnHistoryDoubleClick;
            p.Controls.Add(lstHistory);

            // Action buttons
            var btnRow = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = Color.FromArgb(18, 32, 22) };

            var btnOpen = MakeSideBtn("📂 Open", 74);
            btnOpen.Dock  = DockStyle.Left;
            btnOpen.Click += (_, _) => OpenSelectedBill();
            btnRow.Controls.Add(btnOpen);

            var btnPrint = MakeSideBtn("🖨 Print", 74);
            btnPrint.Dock  = DockStyle.Left;
            btnPrint.Click += (_, _) => PrintSelectedBill();
            btnRow.Controls.Add(btnPrint);

            var btnDel = MakeSideBtn("🗑 Del", 74);
            btnDel.Dock  = DockStyle.Right;
            btnDel.Click += (_, _) => DeleteSelectedBill();
            btnRow.Controls.Add(btnDel);

            p.Controls.Add(btnRow);

            return p;
        }

        private void DrawHistoryItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _historyList.Count) return;
            e.DrawBackground();

            var bill = _historyList[e.Index];
            bool sel = (e.State & DrawItemState.Selected) != 0;
            Color bg  = sel ? Color.FromArgb(52, 133, 90) : (e.Index % 2 == 0 ? Color.FromArgb(28, 46, 32) : Color.FromArgb(32, 52, 36));
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, e.Bounds);

            string line1 = $"  {bill.CustomerName}";
            string line2 = $"  {bill.BillDate:dd MMM yyyy}";
            if (!string.IsNullOrWhiteSpace(bill.PhoneNumber))
                line2 += $"   📞 {bill.PhoneNumber}";
            string line3 = $"  {bill.MeasCount} measurement(s)";

            var fBold = new Font("Segoe UI", 9f, FontStyle.Bold);
            var fNorm = new Font("Segoe UI", 8f);
            TextRenderer.DrawText(e.Graphics, line1, fBold, new Rectangle(e.Bounds.X, e.Bounds.Y + 2,  e.Bounds.Width, 16), sel ? Color.White : Color.FromArgb(210, 240, 220), TextFormatFlags.Left);
            TextRenderer.DrawText(e.Graphics, line2, fNorm, new Rectangle(e.Bounds.X, e.Bounds.Y + 16, e.Bounds.Width, 15), sel ? Color.White : Color.FromArgb(170, 208, 185), TextFormatFlags.Left);
            TextRenderer.DrawText(e.Graphics, line3, fNorm, new Rectangle(e.Bounds.X, e.Bounds.Y + 30, e.Bounds.Width, 14), sel ? Color.White : Color.FromArgb(140, 180, 160), TextFormatFlags.Left);
            fBold.Dispose(); fNorm.Dispose();
            e.DrawFocusRectangle();
        }

        private Panel BuildSideFooter()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 32, 24), Padding = new Padding(16, 8, 16, 8) };
            p.Controls.Add(new Label { Text = "🌿  Quality Woodwork\n      Lasts a Lifetime", Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), ForeColor = SidebarText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft });
            return p;
        }

        // ── Main Area ─────────────────────────────────────────────────────────

        private Panel BuildMainArea()
        {
            var main = new Panel { Dock = DockStyle.Fill, BackColor = CBg };
            _mainArea = main;

            // WinForms docks in reverse z-order: the LAST control added sits closest to the
            // edge, so the Fill body must be added before the Top-docked strips.
            main.Controls.Add(BuildBody());
            main.Controls.Add(BuildCustomerStrip());
            main.Controls.Add(BuildHeader());
            return main;
        }

        private Panel BuildHeader()
        {
            var h = new Panel { Height = 84, Dock = DockStyle.Top, BackColor = CCard };
            h.Paint += (_, e) => { using var pen = new Pen(CBorder, 1); e.Graphics.DrawLine(pen, 0, h.Height - 1, h.Width, h.Height - 1); };

            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 6,
                RowCount    = 1,
                BackColor   = CCard,
                Padding     = new Padding(20, 10, 20, 10)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52f));   // icon
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100f));  // title (flexes)
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 286f));  // search
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96f));   // New
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96f));   // All
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170f));  // clock
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            h.Controls.Add(tbl);

            tbl.Controls.Add(new Label
            {
                Text      = "🧮",
                Font      = new Font("Segoe UI", 22f),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = CCard
            }, 0, 0);

            // Title + subtitle stack
            var titleCell = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                BackColor   = CCard,
                Padding     = new Padding(0, 4, 8, 0)
            };
            titleCell.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            titleCell.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            titleCell.RowStyles.Add(new RowStyle(SizeType.Percent,  100f));
            titleCell.Controls.Add(new Label
            {
                Text      = "Carpenter Measurement",
                Font      = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = CText,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = CCard
            }, 0, 0);
            titleCell.Controls.Add(new Label
            {
                Text        = "Wardrobe & Furniture Management",
                Font        = new Font("Segoe UI", 9f),
                ForeColor   = CSub,
                AutoSize    = false,
                Dock        = DockStyle.Fill,
                UseMnemonic = false,
                TextAlign   = ContentAlignment.MiddleLeft,
                BackColor   = CCard
            }, 0, 1);
            tbl.Controls.Add(titleCell, 1, 0);

            // Search box
            txtDashSearch = new TextBox
            {
                Size            = new Size(268, 30),
                Anchor          = AnchorStyles.None,
                Font            = new Font("Segoe UI", 10.5f),
                BorderStyle     = BorderStyle.FixedSingle,
                BackColor       = Color.FromArgb(250, 252, 254),
                PlaceholderText = "🔍 Search name, phone or date..."
            };
            txtDashSearch.Enter += (_, _) => txtDashSearch.BackColor = Color.FromArgb(235, 250, 242);
            txtDashSearch.Leave += (_, _) =>
            {
                txtDashSearch.BackColor = Color.FromArgb(250, 252, 254);
                var timer = new System.Windows.Forms.Timer { Interval = 200 };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    if (lstDashResults != null && !lstDashResults.Focused)
                        lstDashResults.Visible = false;
                };
                timer.Start();
            };
            txtDashSearch.TextChanged += OnDashSearchChanged;
            txtDashSearch.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Down && lstDashResults != null && lstDashResults.Visible && lstDashResults.Items.Count > 0)
                {
                    lstDashResults.Focus();
                    lstDashResults.SelectedIndex = 0;
                }
                else if (e.KeyCode == Keys.Escape && lstDashResults != null)
                {
                    lstDashResults.Visible = false;
                }
            };
            tbl.Controls.Add(txtDashSearch, 2, 0);

            // New Bill
            var btnNew = new Button
            {
                Text      = "+ New",
                Size      = new Size(84, 38),
                Anchor    = AnchorStyles.None,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = CGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            btnNew.FlatAppearance.BorderSize = 0;
            btnNew.MouseEnter += (_, _) => btnNew.BackColor = CGreenDark;
            btnNew.MouseLeave += (_, _) => btnNew.BackColor = CGreen;
            btnNew.Click += (_, _) => StartNewBill();
            tbl.Controls.Add(btnNew, 3, 0);

            // All Customers
            var btnAll = new Button
            {
                Text      = "📋 All",
                Size      = new Size(84, 38),
                Anchor    = AnchorStyles.None,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = CBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            btnAll.FlatAppearance.BorderSize = 0;
            btnAll.MouseEnter += (_, _) => btnAll.BackColor = Color.FromArgb(30, 80, 150);
            btnAll.MouseLeave += (_, _) => btnAll.BackColor = CBlue;
            btnAll.Click += (_, _) => ShowAllCustomers();
            tbl.Controls.Add(btnAll, 4, 0);

            // Clock
            lblClock = new Label
            {
                Size      = new Size(160, 22),
                Anchor    = AnchorStyles.None,
                Font      = new Font("Segoe UI", 9f),
                ForeColor = CSub,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = CCard
            };
            UpdateClock();
            tbl.Controls.Add(lblClock, 5, 0);

            // Search results dropdown — parented to the form so it is never clipped
            // by the 78 px header panel it visually hangs out of.
            lstDashResults = new ListBox
            {
                Font           = new Font("Segoe UI", 9.5f),
                BackColor      = Color.White,
                ForeColor      = CText,
                BorderStyle    = BorderStyle.FixedSingle,
                DrawMode       = DrawMode.OwnerDrawFixed,
                ItemHeight     = 42,
                Visible        = false,
                IntegralHeight = false,
                Width          = 268
            };
            lstDashResults.DrawItem  += DrawDashItem;
            lstDashResults.DoubleClick += OnDashDoubleClick;
            lstDashResults.MouseDown += OnDashMouseDown;
            lstDashResults.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                    OpenDashSelected();
                else if (e.KeyCode == Keys.Escape)
                    lstDashResults.Visible = false;
            };
            Controls.Add(lstDashResults);
            lstDashResults.BringToFront();

            return h;
        }

        /// <summary>Customer details strip — sits between header and body.</summary>
        private Panel BuildCustomerStrip()
        {
            var strip = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 86,
                BackColor = CCard,
                Padding   = new Padding(24, 10, 24, 10)
            };
            strip.Paint += (_, e) =>
            {
                using var pen = new Pen(CBorder, 1);
                e.Graphics.DrawLine(pen, 0, strip.Height - 1, strip.Width, strip.Height - 1);
                // subtle green top border
                using var gpen = new Pen(Color.FromArgb(100, CGreen), 2);
                e.Graphics.DrawLine(gpen, 0, 0, strip.Width, 0);
            };

            // Layout: Name | Date | Phone | Save-Status
            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 4,
                RowCount    = 1,
                BackColor   = CCard
            };
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100f));  // name
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210f)); // date
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300f)); // phone
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240f)); // status

            // Name
            tbl.Controls.Add(BuildCustField("👤 Customer Name *", out txtCustName), 0, 0);

            // Date
            var datePnl = BuildFieldShell();
            datePnl.Controls.Add(BuildFieldLabel("📅 Date *"), 0, 0);
            dtpBillDate = new DateTimePicker
            {
                Dock     = DockStyle.Bottom,
                Height   = 30,
                Font     = FCustInput,
                Format   = DateTimePickerFormat.Short,
                Value    = DateTime.Today
            };
            dtpBillDate.ValueChanged += OnCustomerFieldChanged;
            datePnl.Controls.Add(dtpBillDate, 0, 1);
            tbl.Controls.Add(datePnl, 1, 0);

            // Phone
            tbl.Controls.Add(BuildCustField("📞 Phone (optional)", out txtPhone), 2, 0);

            // Save Status
            var statusPnl = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Padding = new Padding(12, 18, 8, 6) };
            lblSaveStatus = new Label
            {
                Dock      = DockStyle.Fill,
                Font      = new Font("Segoe UI", 9f, FontStyle.Italic),
                ForeColor = CSub,
                TextAlign = ContentAlignment.MiddleLeft,
                Text      = "Enter customer details to start a new bill"
            };
            statusPnl.Controls.Add(lblSaveStatus);
            tbl.Controls.Add(statusPnl, 3, 0);

            strip.Controls.Add(tbl);

            // Wire name+phone change to auto-save header
            txtCustName.TextChanged += OnCustomerFieldChanged;
            txtPhone.TextChanged    += OnCustomerFieldChanged;

            return strip;
        }

        /// <summary>Two-row shell (caption on top, control filling the rest) used by the customer strip.</summary>
        private static TableLayoutPanel BuildFieldShell()
        {
            var p = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 2,
                BackColor   = CCard,
                Padding     = new Padding(0, 4, 14, 4)
            };
            p.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            p.RowStyles.Add(new RowStyle(SizeType.Percent,  100f));
            return p;
        }

        private static Label BuildFieldLabel(string text) => new()
        {
            Text      = text,
            Font      = FCustLabel,
            ForeColor = CSub,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize  = false
        };

        private Panel BuildCustField(string label, out TextBox tb, string placeholder = "")
        {
            var p = BuildFieldShell();
            p.Controls.Add(BuildFieldLabel(label), 0, 0);
            var box = new TextBox
            {
                Dock            = DockStyle.Bottom,
                Height          = 30,
                Font            = FCustInput,
                BorderStyle     = BorderStyle.FixedSingle,
                BackColor       = Color.FromArgb(250, 252, 254),
                PlaceholderText = placeholder
            };
            box.Enter += (_, _) => box.BackColor = Color.FromArgb(235, 250, 242);
            box.Leave += (_, _) => box.BackColor = Color.FromArgb(250, 252, 254);
            p.Controls.Add(box, 0, 1);
            tb = box;
            return p;
        }

        private Panel BuildBody()
        {
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 12, 18, 12), BackColor = CBg };

            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                RowCount    = 4,
                ColumnCount = 1,
                BackColor   = CBg,
                Margin      = new Padding(0)
            };
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 144f));  // input card
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // grid
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute,  90f));  // summary
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 116f));  // charges

            tbl.Controls.Add(BuildInputCard(),   0, 0);
            tbl.Controls.Add(BuildGridCard(),    0, 1);
            tbl.Controls.Add(BuildSummaryRow(),  0, 2);
            tbl.Controls.Add(BuildChargesCard(), 0, 3);

            body.Controls.Add(tbl);
            return body;
        }

        // ── Dashboard Live Search ─────────────────────────────────────────────

        /// <summary>
        /// Single source of truth for bill look-ups. The dashboard dropdown, the sidebar
        /// history and the All Customer Records dialog all go through this, so the same
        /// query always returns the same rows on every screen.
        /// </summary>
        private List<BillSummary> QueryBills(string query)
        {
            string q = (query ?? "").Trim();
            return string.IsNullOrEmpty(q) ? _db.GetAllBills() : _db.SearchBills(q, q, q);
        }

        private void OnDashSearchChanged(object? sender, EventArgs e)
        {
            if (txtDashSearch == null || lstDashResults == null) return;

            string q = txtDashSearch.Text.Trim();
            if (q.Length == 0)
            {
                lstDashResults.Visible = false;
                return;
            }

            try
            {
                _dashList = QueryBills(q);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
                return;
            }

            lstDashResults.BeginUpdate();
            lstDashResults.Items.Clear();
            if (_dashList.Count == 0)
                lstDashResults.Items.Add(NoResultsTag);
            else
                foreach (var b in _dashList) lstDashResults.Items.Add(b.CustomerName);
            lstDashResults.EndUpdate();

            PositionDashDropdown(_dashList.Count == 0 ? 1 : Math.Min(_dashList.Count, 8));
            lstDashResults.Visible = true;
            lstDashResults.BringToFront();
        }

        private const string NoResultsTag = "──  No matching records  ──";

        private void PositionDashDropdown(int rows)
        {
            if (lstDashResults == null || txtDashSearch == null) return;

            lstDashResults.Height = rows * lstDashResults.ItemHeight + 4;
            var onScreen  = txtDashSearch.PointToScreen(Point.Empty);
            var inForm    = PointToClient(onScreen);
            lstDashResults.Location = new Point(inForm.X, inForm.Y + txtDashSearch.Height + 4);
        }

        private void DrawDashItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();

            if (e.Index >= _dashList.Count)
            {
                TextRenderer.DrawText(e.Graphics, NoResultsTag, new Font("Segoe UI", 9f, FontStyle.Italic),
                    e.Bounds, CSub, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            var bill = _dashList[e.Index];
            bool sel = (e.State & DrawItemState.Selected) != 0;
            Color bg = sel ? CGreenLight : (e.Index % 2 == 0 ? Color.White : Color.FromArgb(248, 252, 249));
            using (var br = new SolidBrush(bg)) e.Graphics.FillRectangle(br, e.Bounds);
            using (var br = new SolidBrush(sel ? CGreen : Color.FromArgb(180, CGreen)))
                e.Graphics.FillRectangle(br, e.Bounds.X, e.Bounds.Y + 4, 4, e.Bounds.Height - 8);
            var fBold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            var fNorm = new Font("Segoe UI", 8.5f);
            string name = $"  {bill.CustomerName}";
            string meta = $"  📅 {bill.BillDate:dd MMM yyyy}";
            if (!string.IsNullOrWhiteSpace(bill.PhoneNumber)) meta += $"   📞 {bill.PhoneNumber}";
            meta += $"   ({bill.MeasCount} meas.)";
            TextRenderer.DrawText(e.Graphics, name, fBold, new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 3,  e.Bounds.Width, 18), sel ? CGreenDark : CText, TextFormatFlags.Left);
            TextRenderer.DrawText(e.Graphics, meta, fNorm, new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 20, e.Bounds.Width, 16), sel ? CGreenDark : CSub,  TextFormatFlags.Left);
            fBold.Dispose(); fNorm.Dispose();
            e.DrawFocusRectangle();
        }

        private void OnDashDoubleClick(object? sender, EventArgs e) => OpenDashSelected();

        private void OnDashMouseDown(object? sender, MouseEventArgs e)
        {
            if (lstDashResults == null) return;
            int idx = lstDashResults.IndexFromPoint(e.Location);
            if (idx < 0 || idx >= _dashList.Count) return;   // ignore clicks on the "no results" row
            lstDashResults.SelectedIndex = idx;
            OpenDashSelected();
        }

        private void OpenDashSelected()
        {
            if (lstDashResults == null) return;
            int idx = lstDashResults.SelectedIndex;
            if (idx < 0 || idx >= _dashList.Count) return;
            var bill = _dashList[idx];
            if (_measurements.Count > 0 && _currentBillId != bill.Id)
            {
                if (MessageBox.Show("Opening another bill will leave the current measurements. Continue?",
                        "Open Bill", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }
            LoadBill(bill);
            if (txtDashSearch != null) txtDashSearch.Text = "";
            lstDashResults.Visible = false;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  All Customers View
        // ══════════════════════════════════════════════════════════════════════

        private void ShowAllCustomers()
        {
            var allCustomersForm = new Form
            {
                Text = "All Customer Records",
                Size = new Size(1100, 700),
                MinimumSize = new Size(900, 600),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = CBg,
                Font = new Font("Segoe UI", 9.5f),
                Padding = new Padding(0)
            };

            var header = new Panel 
            { 
                Dock = DockStyle.Top, 
                Height = 96,
                BackColor = CCard,
                Padding = new Padding(16, 12, 16, 12)
            };
            header.Paint += (_, e) => 
            { 
                using var pen = new Pen(CBorder); 
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1); 
            };

            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                BackColor = CCard,
                Padding = new Padding(0)
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115f));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320f));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent,  100f));
            header.Controls.Add(headerLayout);
            
            // Back button - vertically centered in the left column
            var btnBack = new Button
            {
                Text = "← Back",
                Size = new Size(95, 40),
                Anchor = AnchorStyles.None,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = CText,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnBack.FlatAppearance.BorderSize = 1;
            btnBack.FlatAppearance.BorderColor = CBorder;
            btnBack.MouseEnter += (_, _) => btnBack.BackColor = Color.FromArgb(220, 220, 220);
            btnBack.MouseLeave += (_, _) => btnBack.BackColor = Color.FromArgb(240, 240, 240);
            btnBack.Click += (_, _) => allCustomersForm.Close();
            headerLayout.Controls.Add(btnBack, 0, 0);
            headerLayout.SetRowSpan(btnBack, 3);
            
            var lblTitle = new Label 
            { 
                Text = "Customer Bills & Records", 
                Font = new Font("Segoe UI", 18f, FontStyle.Bold), 
                ForeColor = CText, 
                AutoSize = false,
                Dock = DockStyle.Fill,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = CCard
            };
            headerLayout.Controls.Add(lblTitle, 1, 0);
            
            var lblSubtitle = new Label 
            { 
                Text = "View, search and manage saved customer bills", 
                Font = new Font("Segoe UI", 10.5f), 
                ForeColor = CSub, 
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = CCard
            };
            headerLayout.Controls.Add(lblSubtitle, 1, 1);

            // Search box - right column, centered; TableLayoutPanel keeps it clear of the title
            var txtSearch = new TextBox
            {
                Width = 300,
                Height = 38,
                Anchor = AnchorStyles.None,
                Font = new Font("Segoe UI", 10f),
                PlaceholderText = "🔍 Search by name, phone or date...",
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(250, 252, 254)
            };
            txtSearch.Enter += (_, _) => txtSearch.BackColor = Color.FromArgb(235, 250, 242);
            txtSearch.Leave += (_, _) => txtSearch.BackColor = Color.FromArgb(250, 252, 254);
            headerLayout.Controls.Add(txtSearch, 2, 0);
            headerLayout.SetRowSpan(txtSearch, 3);
            
            // DataGridView for all customers
            var dgvCustomers = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.None,
                BackgroundColor = CCard,
                GridColor = CBorder,
                Font = new Font("Segoe UI", 10f),
                ColumnHeadersHeight = 45,
                RowTemplate = { Height = 45 },
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Margin = new Padding(20)
            };

            dgvCustomers.ColumnHeadersDefaultCellStyle.BackColor = CGridHdr;
            dgvCustomers.ColumnHeadersDefaultCellStyle.ForeColor = CText;
            dgvCustomers.ColumnHeadersDefaultCellStyle.SelectionBackColor = CGridHdr;
            dgvCustomers.ColumnHeadersDefaultCellStyle.SelectionForeColor = CText;
            dgvCustomers.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            dgvCustomers.ColumnHeadersDefaultCellStyle.Padding = new Padding(12, 0, 0, 0);
            dgvCustomers.EnableHeadersVisualStyles = false;
            dgvCustomers.DefaultCellStyle.Padding = new Padding(12, 8, 12, 8);
            dgvCustomers.DefaultCellStyle.SelectionBackColor = CSelected;
            dgvCustomers.DefaultCellStyle.SelectionForeColor = CText;
            dgvCustomers.AlternatingRowsDefaultCellStyle.BackColor = CGridAlt;

            // Define columns
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "Bill #", FillWeight = 60, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { Name = "CustomerName", HeaderText = "Customer Name", FillWeight = 200 });
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { Name = "PhoneNumber", HeaderText = "Phone", FillWeight = 130 });
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { Name = "BillDate", HeaderText = "Date", FillWeight = 110 });
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { Name = "MeasCount", HeaderText = "Items", FillWeight = 55, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            
            var btnColOpen = new DataGridViewButtonColumn { Name = "Open", HeaderText = "", Text = "Open", UseColumnTextForButtonValue = true, Width = 110, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, MinimumWidth = 110 };
            btnColOpen.DefaultCellStyle.BackColor = CGreenLight;
            btnColOpen.DefaultCellStyle.ForeColor = CGreen;
            btnColOpen.DefaultCellStyle.SelectionBackColor = CGreenLight;
            btnColOpen.DefaultCellStyle.SelectionForeColor = CGreen;
            dgvCustomers.Columns.Add(btnColOpen);

            var btnColDelete = new DataGridViewButtonColumn { Name = "Delete", HeaderText = "", Text = "Delete", UseColumnTextForButtonValue = true, Width = 110, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, MinimumWidth = 110 };
            btnColDelete.DefaultCellStyle.BackColor = CRedLight;
            btnColDelete.DefaultCellStyle.ForeColor = CRed;
            btnColDelete.DefaultCellStyle.SelectionBackColor = CRedLight;
            btnColDelete.DefaultCellStyle.SelectionForeColor = CRed;
            dgvCustomers.Columns.Add(btnColDelete);

            // Container for grid with padding
            var gridContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20), BackColor = CBg };
            gridContainer.Controls.Add(dgvCustomers);
            
            // ADD IN CORRECT ORDER: Status bar first, then grid container, then header last
            // This ensures header is on top due to Dock.Top

            // Load all bills — same query path as the dashboard search
            var allBills = QueryBills("");

            // Status bar (built before LoadBills so the counter can be driven from it)
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = CCard, Padding = new Padding(20, 10, 20, 10) };
            statusBar.Paint += (_, e) => { using var pen = new Pen(CBorder); e.Graphics.DrawLine(pen, 0, 0, statusBar.Width, 0); };
            var lblStatus = new Label
            {
                Text      = $"Total Records: {allBills.Count}",
                Font      = new Font("Segoe UI", 9f),
                ForeColor = CSub,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusBar.Controls.Add(lblStatus);

            void LoadBills(List<BillSummary> bills)
            {
                dgvCustomers.Rows.Clear();
                foreach (var bill in bills)
                {
                    dgvCustomers.Rows.Add(
                        bill.Id,
                        bill.CustomerName,
                        string.IsNullOrWhiteSpace(bill.PhoneNumber) ? "—" : bill.PhoneNumber,
                        bill.BillDate.ToString("dd MMM yyyy"),
                        bill.MeasCount
                    );
                }
                dgvCustomers.CurrentCell = null;
                dgvCustomers.ClearSelection();
                lblStatus.Text = bills.Count == allBills.Count
                    ? $"Total Records: {allBills.Count}"
                    : $"Showing: {bills.Count} of {allBills.Count} records";
            }
            LoadBills(allBills);

            // Search functionality — identical semantics to the dashboard search
            txtSearch.TextChanged += (_, _) =>
            {
                LoadBills(QueryBills(txtSearch.Text));
            };

            // Handle button clicks
            dgvCustomers.CellClick += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                
                if (e.ColumnIndex == dgvCustomers.Columns["Open"].Index)
                {
                    int billId = Convert.ToInt32(dgvCustomers.Rows[e.RowIndex].Cells["Id"].Value);
                    var bill = QueryBills("").FirstOrDefault(b => b.Id == billId);
                    if (bill != null)
                    {
                        if (_measurements.Count > 0 && _currentBillId != bill.Id)
                        {
                            if (MessageBox.Show("Opening another bill will leave current measurements. Continue?",
                                "Open Bill", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                            {
                                LoadBill(bill);
                                allCustomersForm.Close();
                            }
                        }
                        else
                        {
                            LoadBill(bill);
                            allCustomersForm.Close();
                        }
                    }
                }
                else if (e.ColumnIndex == dgvCustomers.Columns["Delete"].Index)
                {
                    int billId = Convert.ToInt32(dgvCustomers.Rows[e.RowIndex].Cells["Id"].Value);
                    string customerName = dgvCustomers.Rows[e.RowIndex].Cells["CustomerName"].Value.ToString() ?? "";
                    
                    if (MessageBox.Show($"Delete bill for \"{customerName}\"?\n\nThis will permanently delete all measurements.", 
                        "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        try
                        {
                            _db.DeleteBill(billId);
                            if (_currentBillId == billId)
                            {
                                StartNewBill();
                            }
                            // Refresh the list
                            allBills = QueryBills("");
                            LoadBills(QueryBills(txtSearch.Text));
                            MessageBox.Show("Bill deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error deleting bill: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            };
            
            // ADD CONTROLS IN CORRECT ORDER: Bottom, Fill, Top
            // This ensures proper docking and no overlap
            allCustomersForm.Controls.Add(statusBar);      // Bottom bar first
            allCustomersForm.Controls.Add(gridContainer);  // Fill area second  
            allCustomersForm.Controls.Add(header);         // Top header last (this makes it appear on top)

            allCustomersForm.ShowDialog(this);
        }

        // ── Input Card — combined "inches.sooter" TextBox ──────────────────────

        private Panel BuildInputCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Margin = new Padding(0, 0, 0, 8) };
            card.Paint += (_, e) => PaintCardBorder(e.Graphics, card);

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                BackColor   = CCard,
                Padding     = new Padding(24, 14, 24, 8)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,  100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            card.Controls.Add(layout);

            layout.Controls.Add(new Label { Text = "＋  Add Measurement", Font = FSecTitle, ForeColor = CText, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);

            var fieldsRow = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 7,
                RowCount    = 1,
                BackColor   = CCard,
                Margin      = new Padding(0)
            };
            fieldsRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            // Fixed field widths keep Height → Width → Qty → Tape sides → Add in a short,
            // left-packed run; the spare card width goes to the filler on the right so the
            // Add button never drifts away from the inputs on a wide window.
            fieldsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210f));  // Height
            fieldsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210f));  // Width
            fieldsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));  // Qty
            fieldsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300f));  // Tape sides
            fieldsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));  // Add
            fieldsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130f));  // Cancel
            fieldsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100f));  // filler
            layout.Controls.Add(fieldsRow, 0, 1);

            txtHeight = BuildTextBox(Point.Empty, 190, 38);
            txtWidth  = BuildTextBox(Point.Empty, 190, 38);
            fieldsRow.Controls.Add(BuildFieldGroup("↕  Height (in.sooter)", txtHeight, 12), 0, 0);
            fieldsRow.Controls.Add(BuildFieldGroup("↔  Width (in.sooter)",  txtWidth,  12), 1, 0);

            nudQuantity = BuildNud(Point.Empty, 110, 38, 1, 9999);
            nudQuantity.Value = 1;
            fieldsRow.Controls.Add(BuildFieldGroup("📦  Qty", nudQuantity, 12), 2, 0);
            fieldsRow.Controls.Add(BuildFieldGroup("📐  Tape sides", BuildSideSelector(), 0), 3, 0);

            btnAdd = new Button
            {
                Text      = "＋  Add",
                Font      = FBtn,
                BackColor = CGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.MouseEnter += (_, _) => btnAdd.BackColor = CGreenDark;
            btnAdd.MouseLeave += (_, _) => btnAdd.BackColor = CGreen;
            btnAdd.Click += OnAddClicked;
            fieldsRow.Controls.Add(BuildFieldGroup("", btnAdd, 10), 4, 0);

            btnCancel = new Button
            {
                Text      = "✕  Cancel",
                Font      = FBtn,
                BackColor = Color.White,
                ForeColor = CWarn,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Visible   = false
            };
            btnCancel.FlatAppearance.BorderColor = CWarn;
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.MouseEnter += (_, _) => { btnCancel.BackColor = CRedLight; };
            btnCancel.MouseLeave += (_, _) => { btnCancel.BackColor = Color.White; };
            btnCancel.Click += OnCancelClicked;
            fieldsRow.Controls.Add(BuildFieldGroup("", btnCancel, 0), 5, 0);

            lblError = new Label { AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), ForeColor = CRed, Text = "", TextAlign = ContentAlignment.MiddleLeft };
            layout.Controls.Add(lblError, 0, 2);

            void Wire(Control c) => c.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; OnAddClicked(this, EventArgs.Empty); } };
            Wire(txtHeight);
            Wire(txtWidth);
            Wire(nudQuantity);

            return card;
        }

        /// <summary>Caption above an input, all groups sharing the same baseline so the row stays aligned.</summary>
        private static TableLayoutPanel BuildFieldGroup(string caption, Control input, int rightGap)
        {
            var p = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 2,
                BackColor   = CCard,
                Padding     = new Padding(0, 0, rightGap, 0)
            };
            p.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            p.RowStyles.Add(new RowStyle(SizeType.Percent,  100f));
            p.Controls.Add(new Label
            {
                Text        = caption,
                Font        = FFieldLabel,
                ForeColor   = CText,
                AutoSize    = false,
                Dock        = DockStyle.Fill,
                UseMnemonic = false,
                TextAlign   = ContentAlignment.BottomLeft
            }, 0, 0);
            input.Dock   = DockStyle.Top;
            input.Height = 38;
            p.Controls.Add(input, 0, 1);
            return p;
        }

        /// <summary>Dropdown choices in display order: how many edges the tape runs along.</summary>
        private static readonly (MeasurementSide Side, string Text)[] SideChoices =
        {
            (MeasurementSide.Height,            "1 Side — Height"),
            (MeasurementSide.Width,             "1 Side — Width"),
            (MeasurementSide.HeightHeight,      "2 Sides — H+H"),
            (MeasurementSide.HeightWidth,       "2 Sides — H+W"),
            (MeasurementSide.WidthWidth,        "2 Sides — W+W"),
            (MeasurementSide.HeightHeightWidth, "3 Sides — H+H+W"),
            (MeasurementSide.WidthWidthHeight,  "3 Sides — W+W+H"),
            (MeasurementSide.Both,              "4 Sides — H+W+H+W")
        };

        private static int SideChoiceIndex(MeasurementSide side)
        {
            int i = Array.FindIndex(SideChoices, c => c.Side == side);
            return i >= 0 ? i : Array.FindIndex(SideChoices, c => c.Side == MeasurementSide.Both);
        }

        /// <summary>Which edges of one piece the tape is measured along.</summary>
        private Control BuildSideSelector()
        {
            _cmbSides = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle     = FlatStyle.Flat,
                Font          = FInput,
                BackColor     = Color.FromArgb(250, 252, 254),
                Cursor        = Cursors.Hand
            };
            foreach (var (_, text) in SideChoices) _cmbSides.Items.Add(text);
            _cmbSides.SelectedIndex = SideChoiceIndex(MeasurementSide.Both);
            return _cmbSides;
        }

        private MeasurementSide CurrentSide() => SideChoices[_cmbSides.SelectedIndex].Side;

        private void SelectSide(MeasurementSide side) => _cmbSides.SelectedIndex = SideChoiceIndex(side);

        private static string SideLabel(MeasurementSide side) => MeasurementDisplayFormatter.FormatSides(side);

        // ── Grid Card ─────────────────────────────────────────────────────────

        private Panel BuildGridCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Padding = new Padding(20, 8, 20, 8), Margin = new Padding(0, 0, 0, 8) };
            card.Paint += (_, e) => PaintCardBorder(e.Graphics, card);

            var clearAllRows = BuildOutlineButton("🗑  Clear All", Point.Empty, 168, 30, CRed);
            var btnPdf       = BuildOutlineButton("📄  Save PDF",  Point.Empty, 152, 30, CGreen);
            var btnCsv       = BuildOutlineButton("⬇  Export CSV", Point.Empty, 150, 30, CBlue);
            clearAllRows.Click += OnClearAllClicked;
            btnPdf.Click       += (_, _) => ExportCurrentBillPdf();
            btnCsv.Click       += (_, _) => ExportSizesCsv();

            // Header bar of the grid card. It lives in its own row of the table layout below
            // rather than docking beside the grid: docked siblings share z-order rules that are
            // easy to get wrong, and a mis-ordered pair lets the grid paint over the buttons.
            const int gap = 8;

            var hdrBar = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1,
                BackColor   = CCard
            };
            hdrBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            // Sized from the buttons themselves, so display scaling cannot clip them.
            hdrBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,
                clearAllRows.Width + btnPdf.Width + btnCsv.Width + gap * 2 + 12));
            hdrBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var titles = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                BackColor     = CCard,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                Margin        = new Padding(0),
                Padding       = new Padding(0, 4, 0, 0)
            };
            titles.Controls.Add(new Label { Text = "≡  Measurements", Font = FSecTitle, ForeColor = CText, AutoSize = true, Margin = new Padding(0, 2, 10, 0) });
            titles.Controls.Add(new Label { Text = "·  1 inch = 8 sooter", Font = new Font("Segoe UI", 8.5f), ForeColor = CSub, AutoSize = true, Margin = new Padding(0, 6, 0, 0) });
            hdrBar.Controls.Add(titles, 0, 0);

            var clearCell = new Panel { Dock = DockStyle.Fill, BackColor = CCard };
            // Repositioned from Resize only — mixing Anchor with manual Location makes the
            // buttons drift past the card edge when the window is shrunk.
            clearCell.Resize += (_, _) =>
            {
                int top = Math.Max(0, (clearCell.ClientSize.Height - clearAllRows.Height) / 2);
                clearAllRows.Location = new Point(Math.Max(0, clearCell.ClientSize.Width - clearAllRows.Width), top);
                btnPdf.Location       = new Point(Math.Max(0, clearAllRows.Left - gap - btnPdf.Width), top);
                btnCsv.Location       = new Point(Math.Max(0, btnPdf.Left - gap - btnCsv.Width), top);
            };
            clearCell.Controls.Add(clearAllRows);
            clearCell.Controls.Add(btnPdf);
            clearCell.Controls.Add(btnCsv);
            hdrBar.Controls.Add(clearCell, 1, 0);

            dgv = new DataGridView
            {
                Dock                            = DockStyle.Fill,
                AllowUserToAddRows              = false,
                AllowUserToDeleteRows           = false,
                AllowUserToResizeRows           = false,
                ReadOnly                        = true,
                SelectionMode                   = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect                     = false,
                RowHeadersVisible               = false,
                BorderStyle                     = BorderStyle.None,
                BackgroundColor                 = CCard,
                GridColor                       = CBorder,
                Font                            = FGridCell,
                ColumnHeadersHeightSizeMode     = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight             = 42,
                CellBorderStyle                 = DataGridViewCellBorderStyle.SingleHorizontal,
                // No header dividers: the header reads as one continuous bar, so the
                // unlabeled action columns and the trailing spacer show no empty boxes.
                ColumnHeadersBorderStyle        = DataGridViewHeaderBorderStyle.None,
                ScrollBars                      = ScrollBars.Vertical,
                AutoSizeColumnsMode             = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToResizeColumns        = false
            };
            dgv.RowTemplate.Height                        = 40;
            dgv.ColumnHeadersDefaultCellStyle.BackColor  = CGridHdr;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor  = CSub;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = CGridHdr;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = CSub;
            dgv.ColumnHeadersDefaultCellStyle.Font       = FGridHdr;
            dgv.ColumnHeadersDefaultCellStyle.Padding    = new Padding(2, 0, 2, 0);
            dgv.ColumnHeadersDefaultCellStyle.Alignment  = DataGridViewContentAlignment.MiddleCenter;
            dgv.EnableHeadersVisualStyles                = false;
            dgv.DefaultCellStyle.Padding                 = new Padding(6, 4, 6, 4);
            dgv.DefaultCellStyle.SelectionBackColor      = CSelected;
            dgv.DefaultCellStyle.SelectionForeColor      = CText;
            dgv.AlternatingRowsDefaultCellStyle.BackColor= CGridAlt;
            dgv.CellClick += OnGridCellClicked;
            dgv.Paint     += PaintGridWatermark;

            BuildGridColumns();

            // One column, two rows: the header strip keeps its 46 pt no matter how tall the
            // grid is, and the grid can never be laid over the buttons.
            var body = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 2,
                BackColor   = CCard,
                Margin      = new Padding(0),
                Padding     = new Padding(0)
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            body.Controls.Add(hdrBar, 0, 0);
            body.Controls.Add(dgv,    0, 1);

            card.Controls.Add(body);
            return card;
        }

        /// <summary>
        /// Fades the company logo into the blank band the grid leaves under its last row, so a
        /// short or empty bill still reads as a finished dashboard. dgv.Paint runs after the
        /// cells, so the band is measured first and the mark is skipped whenever the rows
        /// already fill the grid.
        /// </summary>
        private void PaintGridWatermark(object? sender, PaintEventArgs e)
        {
            if (_watermark == null) return;

            int used = dgv.ColumnHeadersHeight;
            foreach (DataGridViewRow r in dgv.Rows) used += r.Height;
            if (used > dgv.ClientSize.Height) return;   // no free space: rows fill the grid

            var area = new Rectangle(0, used, dgv.ClientSize.Width, dgv.ClientSize.Height - used);
            if (area.Width < 80 || area.Height < 60) return;

            float scale = Math.Min(Math.Min(420, area.Width - 40) / (float)_watermark.Width,
                                   (area.Height - 20)            / (float)_watermark.Height);
            int w = Math.Max(40, (int)(_watermark.Width  * scale));
            int h = Math.Max(20, (int)(_watermark.Height * scale));
            var dest = new Rectangle((area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);

            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(_watermark, dest);
        }

        /// <summary>
        /// The logo is light artwork on a dark sheet, so it cannot be laid on the white grid as-is:
        /// the sheet would read as a grey card. Brightness is turned into coverage instead — the
        /// dark tones become transparent paper and the mark is redrawn as a soft brand-green
        /// silhouette behind the rows.
        /// </summary>
        private static Image? BuildWatermark(Image logo)
        {
            try
            {
                using var src = new Bitmap(logo);
                int w = Math.Min(src.Width, 512);
                int h = Math.Max(1, (int)(src.Height * (w / (float)src.Width)));
                var art = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(art))
                    g.DrawImage(src, new Rectangle(0, 0, w, h));

                var rect = new Rectangle(0, 0, w, h);
                var data = art.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    var px = new byte[Math.Abs(data.Stride) * h];
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0, px, 0, px.Length);

                    int min = 255, max = 0;
                    for (int i = 0; i < px.Length; i += 4)
                    {
                        int lum = (px[i] + px[i + 1] + px[i + 2]) / 3;
                        if (lum < min) min = lum;
                        if (lum > max) max = lum;
                    }

                    // Everything below the mid-tone of the sheet is background, not artwork.
                    float floorLum = min + (max - min) * 0.46f;
                    float ramp     = Math.Max(1f, max - floorLum);
                    for (int i = 0; i < px.Length; i += 4)
                    {
                        int lum  = (px[i] + px[i + 1] + px[i + 2]) / 3;
                        // Gamma < 1 lifts the mid-tones so the mark is readable, not barely there.
                        float on = (float)Math.Pow(Math.Clamp((lum - floorLum) / ramp, 0f, 1f), 0.55);
                        px[i]     = CWatermark.B;
                        px[i + 1] = CWatermark.G;
                        px[i + 2] = CWatermark.R;
                        px[i + 3] = (byte)(on * 255f);
                    }

                    System.Runtime.InteropServices.Marshal.Copy(px, 0, data.Scan0, px.Length);
                }
                finally { art.UnlockBits(data); }
                return art;
            }
            catch { return null; }
        }

        private void BuildGridColumns()
        {
            dgv.Columns.Clear();
            AddTextCol("colNo",   "#",             60);
            AddTextCol("colH",    "Height",       115);
            AddTextCol("colW",    "Width",        115);
            AddTextCol("colQty",  "Qty",           70);
            AddTextCol("colSides","Sides",        132);
            AddTextCol("colOne",  "One Piece",    110).Visible = false;   // kept for calculations only
            AddTextCol("colRow",  "Total Inches", 165);
            AddBtnCol ("colEdit", "Actions", "✏  Edit",   CGreenLight, CGreen, 140);
            AddBtnCol ("colDel",  "Delete",  "🗑  Delete", CRedLight,   CRed,   150);
        }

        // ── Summary Row ───────────────────────────────────────────────────────

        private Panel BuildSummaryRow()
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 4, BackColor = CBg, Margin = new Padding(0) };
            for (int i = 0; i < 4; i++)
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            row.Controls.Add(BuildSummaryCard("≡", "Total Rows",    CGreen, CGreenLight, out lblTotalRows,   out _),          0, 0);
            row.Controls.Add(BuildSummaryCard("⊞", "Total Pieces",  CGreen, CGreenLight, out lblTotalPieces, out _),          1, 0);
            row.Controls.Add(BuildSummaryCard("Σ", "Grand Total",   CBrown, CBrownLight, out lblGrandTotal,  out lblGrandSub),2, 0);
            var feetCard = BuildSummaryCard("↔", "Feet & Inches", CBlue, CBlueLight, out lblFeet, out lblFeetSub);
            feetCard.Margin = new Padding(0);
            row.Controls.Add(feetCard, 3, 0);

            return row;
        }

        private Panel BuildSummaryCard(string iconChar, string caption, Color iconColor, Color iconBg,
                                        out Label valueLabel, out Label subLabel)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Margin = new Padding(0, 0, 12, 0) };
            card.Paint += (_, e) => PaintCardBorder(e.Graphics, card);

            var iconCol = new Panel { Width = 62, Dock = DockStyle.Left, BackColor = iconBg };
            iconCol.Paint += (_, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                int s = 38, x = (iconCol.Width - s) / 2, y = (iconCol.Height - s) / 2;
                using var br = new SolidBrush(iconColor); g.FillEllipse(br, x, y, s, s);
                TextRenderer.DrawText(g, iconChar, new Font("Segoe UI Symbol", 14f, FontStyle.Bold), new Rectangle(x, y, s, s), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };

            var textCol = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Padding = new Padding(12, 8, 8, 8) };
            var capLbl  = new Label { Text = caption, Font = FSumCap, ForeColor = CSub, AutoSize = false, Dock = DockStyle.Top, Height = 18, AutoEllipsis = true, UseMnemonic = false };
            var valLbl  = new Label { Text = "—",     Font = FSumVal, ForeColor = CText, AutoSize = false, Dock = DockStyle.Top, Height = 38, AutoEllipsis = true };
            var subLbl  = new Label { Text = "",      Font = FSumSub, ForeColor = CSub, AutoSize = false, Dock = DockStyle.Top, Height = 18, AutoEllipsis = true };

            valueLabel = valLbl;
            subLabel   = subLbl;

            textCol.Controls.Add(subLbl);
            textCol.Controls.Add(valLbl);
            textCol.Controls.Add(capLbl);
            card.Controls.Add(textCol);
            card.Controls.Add(iconCol);
            return card;
        }

        // ── Bill Charges (money) ──────────────────────────────────────────────

        /// <summary>
        /// Rates the customer is charged on top of the tape: rate per foot of tape,
        /// one gari (vehicle) carriage fare and the sheet-cuts done at a per-sheet rate.
        /// Tape amount follows the grid totals, so it is always billed on the current feet.
        /// </summary>
        private Panel BuildChargesCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Margin = new Padding(0, 8, 0, 0) };
            card.Paint += (_, e) => PaintCardBorder(e.Graphics, card);

            // The title and the fields are both fixed height and a filler row soaks up the spare
            // space, so a short window trims the gap instead of cutting the amounts in half.
            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                BackColor   = CCard,
                Padding     = new Padding(24, 6, 24, 0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,  100f));
            card.Controls.Add(layout);

            var titles = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                BackColor     = CCard,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                Margin        = new Padding(0),
                Padding       = new Padding(0)
            };
            titles.Controls.Add(new Label { Text = "💵  Bill Charges", Font = FSecTitle, ForeColor = CText, AutoSize = true, Margin = new Padding(0, 1, 10, 0) });
            titles.Controls.Add(new Label { Text = "·  tape amount = total feet × rate", Font = new Font("Segoe UI", 8.5f), ForeColor = CSub, AutoSize = true, Margin = new Padding(0, 5, 0, 0) });
            layout.Controls.Add(titles, 0, 0);

            var row = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 7,
                RowCount    = 1,
                BackColor   = CCard,
                Margin      = new Padding(0)
            };
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 6; i++)
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210f));  // Total Amount
            layout.Controls.Add(row, 0, 1);

            nudRateFoot  = BuildMoneyNud(0, 10_000_000);
            nudCarriage  = BuildMoneyNud(0, 10_000_000);
            nudSheetsCut = BuildMoneyNud(0, 100_000, decimals: 0);
            nudRateSheet = BuildMoneyNud(0, 10_000_000);
            foreach (var nud in new[] { nudRateFoot, nudCarriage, nudSheetsCut, nudRateSheet })
                nud.ValueChanged += OnChargeFieldChanged;

            row.Controls.Add(BuildFieldGroup("Rate / Foot",  nudRateFoot,  12), 0, 0);
            row.Controls.Add(BuildFieldGroup("Tape Amount",  BuildAmountBox(out lblTapeAmount,  CText, CCard),        12), 1, 0);
            row.Controls.Add(BuildFieldGroup("Gari Fare",    nudCarriage,  12), 2, 0);
            row.Controls.Add(BuildFieldGroup("Sheets Cut",   nudSheetsCut, 12), 3, 0);
            row.Controls.Add(BuildFieldGroup("Rate / Sheet", nudRateSheet, 12), 4, 0);
            row.Controls.Add(BuildFieldGroup("Sheet Amount", BuildAmountBox(out lblSheetAmount, CText, CCard),        12), 5, 0);
            row.Controls.Add(BuildFieldGroup("Total Amount", BuildAmountBox(out lblTotalAmount, Color.White, CGreen),  0), 6, 0);
            lblTotalAmount.Font = new Font("Segoe UI", 13f, FontStyle.Bold);

            return card;
        }

        /// <summary>NumericUpDown for rupee amounts — pre-filled and clamped, so no bad text can reach the total.</summary>
        private static NumericUpDown BuildMoneyNud(int min, int max, int decimals = 2)
        {
            var nud = BuildNud(Point.Empty, 120, 38, min, max);
            nud.DecimalPlaces = decimals;
            return nud;
        }

        /// <summary>Read-only amount field that lines up with the editable boxes in the same row.</summary>
        private static Panel BuildAmountBox(out Label value, Color fore, Color back)
        {
            var box = new Panel { BackColor = back };
            if (back == CCard)
                box.Paint += (_, e) =>
                {
                    using var pen = new Pen(CBorder);
                    e.Graphics.DrawRectangle(pen, 0, 0, box.Width - 1, box.Height - 1);
                };
            value = new Label
            {
                Text      = Money(0m),
                Font      = FMoney,
                ForeColor = fore,
                BackColor = back,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            box.Controls.Add(value);
            return box;
        }

        private BillCharges ReadCharges() => new()
        {
            RatePerFoot  = nudRateFoot.Value,
            CarriageFare = nudCarriage.Value,
            SheetCuts    = (int)nudSheetsCut.Value,
            RatePerSheet = nudRateSheet.Value
        };

        /// <summary>Amounts track the grid live, so a late-added measurement never leaves a stale total.</summary>
        private void RefreshAmounts()
        {
            var c = ReadCharges();
            int eighths = _calc.CalculateGrandTotal(_measurements).GrandTotalEighths;
            lblTapeAmount.Text  = Money(c.TapeAmount(eighths));
            lblSheetAmount.Text = Money(c.SheetAmount);
            lblTotalAmount.Text = Money(c.TotalAmount(eighths));
        }

        /// <summary>Loads a bill's saved charges into the fields without triggering another save.</summary>
        private void SetCharges(BillCharges charges)
        {
            _chargeSaveTimer?.Stop();
            _loadingCharges = true;
            nudRateFoot.Value  = charges.RatePerFoot;
            nudCarriage.Value  = charges.CarriageFare;
            nudSheetsCut.Value = charges.SheetCuts;
            nudRateSheet.Value = charges.RatePerSheet;
            _loadingCharges = false;
        }

        private void OnChargeFieldChanged(object? sender, EventArgs e)
        {
            RefreshAmounts();
            if (_loadingCharges) return;

            _chargeSaveTimer?.Stop();
            _chargeSaveTimer = new System.Windows.Forms.Timer { Interval = 600 };
            _chargeSaveTimer.Tick += (_, _) =>
            {
                _chargeSaveTimer!.Stop();
                SaveCharges();
            };
            _chargeSaveTimer.Start();
        }

        private void SaveCharges()
        {
            // The bill row only exists once a customer name has been saved.
            if (_currentBillId == 0) AutoSaveBillHeader();
            if (_currentBillId == 0) return;

            try
            {
                _db.UpdateBillCharges(_currentBillId, ReadCharges());
                lblSaveStatus.Text      = $"✔  Saved  [{DateTime.Now:hh:mm tt}]";
                lblSaveStatus.ForeColor = CGreen;
            }
            catch (Exception ex)
            {
                lblSaveStatus.Text      = "Save error: " + ex.Message;
                lblSaveStatus.ForeColor = CRed;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Customer Strip Handlers
        // ════════════════════════════════════════════════════════════════════

        private System.Windows.Forms.Timer? _custSaveTimer;

        /// <summary>
        /// Called whenever customer name, date, or phone changes.
        /// Debounces 600 ms then auto-saves the bill header.
        /// </summary>
        private void OnCustomerFieldChanged(object? sender, EventArgs e)
        {
            _custSaveTimer?.Stop();
            _custSaveTimer = new System.Windows.Forms.Timer { Interval = 600 };
            _custSaveTimer.Tick += (_, _) =>
            {
                _custSaveTimer.Stop();
                AutoSaveBillHeader();
            };
            _custSaveTimer.Start();
        }

        private void AutoSaveBillHeader()
        {
            string name = txtCustName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                lblSaveStatus.Text      = "Customer name is required to save.";
                lblSaveStatus.ForeColor = CWarn;
                return;
            }

            try
            {
                if (_currentBillId == 0)
                {
                    _currentBillId = _db.CreateBill(name, txtPhone.Text.Trim(), dtpBillDate.Value.Date);
                    // Re-assign billId to any measurements already added
                    foreach (var m in _measurements)
                        m.BillId = _currentBillId;
                }
                else
                {
                    _db.UpdateBillHeader(_currentBillId, name, txtPhone.Text.Trim(), dtpBillDate.Value.Date);
                }

                lblSaveStatus.Text      = $"✔  Saved  [{DateTime.Now:hh:mm tt}]";
                lblSaveStatus.ForeColor = CGreen;
            }
            catch (Exception ex)
            {
                lblSaveStatus.Text      = "Save error: " + ex.Message;
                lblSaveStatus.ForeColor = CRed;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Sidebar History Actions
        // ════════════════════════════════════════════════════════════════════

        private void ToggleHistory()
        {
            _historyVisible = !_historyVisible;
            pnlHistory.Visible = _historyVisible;
            if (_historyVisible)
                RefreshHistory();
        }

        private void RefreshHistory()
        {
            try
            {
                _historyList = QueryBills(txtSearchName?.Text ?? "");
                lstHistory.BeginUpdate();
                lstHistory.Items.Clear();
                foreach (var b in _historyList)
                    lstHistory.Items.Add(b.CustomerName); // display text; data is in _historyList[idx]
                lstHistory.EndUpdate();
            }
            catch { }
        }

        private void OnHistoryDoubleClick(object? sender, EventArgs e) => OpenSelectedBill();

        private void OpenSelectedBill()
        {
            int idx = lstHistory.SelectedIndex;
            if (idx < 0 || idx >= _historyList.Count) return;

            var bill = _historyList[idx];

            // Ask if current unsaved work should be discarded
            if (_measurements.Count > 0 && _currentBillId != bill.Id)
            {
                if (MessageBox.Show("Opening another bill will leave the current measurements. Continue?",
                        "Open Bill", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            LoadBill(bill);
        }

        private void LoadBill(BillSummary bill)
        {
            _currentBillId    = bill.Id;
            _editingIndex     = null;
            btnAdd.Text       = "＋  Add";
            btnCancel.Visible = false;

            txtCustName.Text    = bill.CustomerName;
            dtpBillDate.Value   = bill.BillDate;
            txtPhone.Text       = bill.PhoneNumber;

            _measurements.Clear();
            var rows = _db.GetMeasurements(bill.Id);
            _measurements.AddRange(rows);
            _nextSerial = _measurements.Count > 0 ? _measurements.Max(m => m.SerialNumber) + 1 : 1;

            ResetInputs();
            SetCharges(_db.GetBillCharges(bill.Id));
            RefreshGrid();
            RefreshTotals();
            ClearError();

            lblSaveStatus.Text      = $"📂  Loaded bill from {bill.BillDate:dd MMM yyyy}";
            lblSaveStatus.ForeColor = CBlue;
        }

        private void DeleteSelectedBill()
        {
            int idx = lstHistory.SelectedIndex;
            if (idx < 0 || idx >= _historyList.Count) return;
            var bill = _historyList[idx];

            if (MessageBox.Show($"Delete bill for \"{bill.CustomerName}\" ({bill.BillDate:dd MMM yyyy})?\nThis cannot be undone.",
                    "Delete Bill", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                _db.DeleteBill(bill.Id);
                if (_currentBillId == bill.Id)
                {
                    _currentBillId = 0;
                    _measurements.Clear();
                    _nextSerial = 1;
                    txtCustName.Text  = "";
                    txtPhone.Text     = "";
                    dtpBillDate.Value = DateTime.Today;
                    ResetInputs();
                    SetCharges(new BillCharges());
                    RefreshGrid();
                    RefreshTotals();
                    lblSaveStatus.Text      = "Bill deleted.";
                    lblSaveStatus.ForeColor = CSub;
                }
                RefreshHistory();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Delete error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PrintSelectedBill()
        {
            int idx = lstHistory.SelectedIndex;
            if (idx < 0 || idx >= _historyList.Count) return;
            var bill = _historyList[idx];
            var rows = _db.GetMeasurements(bill.Id);
            PrintBill(bill.CustomerName, bill.PhoneNumber, bill.BillDate, rows, _db.GetBillCharges(bill.Id));
        }

        // ════════════════════════════════════════════════════════════════════
        //  New Bill
        // ════════════════════════════════════════════════════════════════════

        private void StartNewBill()
        {
            if (_measurements.Count > 0)
            {
                if (MessageBox.Show("Start a new bill? Current measurements will remain saved if a bill was already created.",
                        "New Bill", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            _currentBillId    = 0;
            _measurements.Clear();
            _nextSerial       = 1;
            _editingIndex     = null;
            btnAdd.Text       = "＋  Add";
            btnCancel.Visible = false;
            txtCustName.Text  = "";
            txtPhone.Text     = "";
            dtpBillDate.Value = DateTime.Today;
            ResetInputs();
            SetCharges(new BillCharges());
            RefreshGrid();
            RefreshTotals();
            ClearError();
            lblSaveStatus.Text      = "Enter customer details to start a new bill.";
            lblSaveStatus.ForeColor = CSub;
            txtCustName.Focus();
        }

        // ════════════════════════════════════════════════════════════════════
        //  Measurement Event Handlers
        // ════════════════════════════════════════════════════════════════════

        private void OnAddClicked(object? sender, EventArgs e)
        {
            ClearError();

            // Parse Height (format: "79.7" = 79 inches + 7 sooter)
            if (!ParseMeasurement(txtHeight.Text, "Height", out int hi, out int hs)) return;

            // Parse Width (format: "48.3" = 48 inches + 3 sooter)
            if (!ParseMeasurement(txtWidth.Text, "Width", out int wi, out int ws)) return;

            int qty = (int)nudQuantity.Value;
            var side = CurrentSide();

            if (hi == 0 && hs == 0 && wi == 0 && ws == 0)
            {
                ShowError("At least one dimension (Height or Width) must be greater than zero.");
                return;
            }

            // Ensure bill exists before saving measurement
            EnsureBillExists();
            if (_currentBillId == 0)
            {
                ShowError("Please enter a customer name before adding measurements.");
                txtCustName.Focus();
                return;
            }

            try
            {
                if (_editingIndex.HasValue)
                {
                    var m = _measurements[_editingIndex.Value];
                    m.HeightInches = hi; m.HeightSooter = hs;
                    m.WidthInches  = wi; m.WidthSooter  = ws;
                    m.Quantity     = qty;
                    m.Side         = side;
                    _db.SaveMeasurement(m);
                    _editingIndex     = null;
                    btnAdd.Text       = "＋  Add";
                    btnCancel.Visible = false;
                }
                else
                {
                    var m = new MeasurementEntry
                    {
                        BillId       = _currentBillId,
                        SerialNumber = _nextSerial++,
                        HeightInches = hi, HeightSooter = hs,
                        WidthInches  = wi, WidthSooter  = ws,
                        Quantity     = qty,
                        Side         = side
                    };
                    _db.SaveMeasurement(m);
                    _measurements.Add(m);
                }

                lblSaveStatus.Text      = $"✔  Saved  [{DateTime.Now:hh:mm tt}]";
                lblSaveStatus.ForeColor = CGreen;
            }
            catch (Exception ex)
            {
                ShowError("Save error: " + ex.Message);
                return;
            }

            ResetInputs();
            RefreshGrid();
            RefreshTotals();
            txtHeight.Focus();
        }

        /// <summary>
        /// Parses combined "inches.sooter" format (e.g., "80.1" → 80 inches + 1 sooter).
        /// Sooter must be 0-7. Empty input returns 0,0.
        /// </summary>
        private bool ParseMeasurement(string input, string fieldName, out int inches, out int sooter)
        {
            inches = 0;
            sooter = 0;

            string txt = input.Trim();
            if (string.IsNullOrEmpty(txt)) return true; // Empty is valid (0,0)

            if (!txt.Contains('.'))
            {
                // Just inches, no sooter
                if (!int.TryParse(txt, out inches) || inches < 0)
                {
                    ShowError($"{fieldName}: Invalid number. Enter like 79 or 79.7 (79 inch 7 sooter)");
                    return false;
                }
                return true;
            }

            string[] parts = txt.Split('.');
            if (parts.Length != 2)
            {
                ShowError($"{fieldName}: Invalid format. Enter like 79 or 79.7 (79 inch 7 sooter)");
                return false;
            }

            if (!int.TryParse(parts[0], out inches) || inches < 0)
            {
                ShowError($"{fieldName}: Invalid inches. Enter like 79 or 79.7 (79 inch 7 sooter)");
                return false;
            }

            if (!int.TryParse(parts[1], out sooter) || sooter < 0 || sooter > 7)
            {
                ShowError($"{fieldName}: Sooter must be 0-7 (1 inch = 8 sooter). Example: 79.7 = 79 inch 7 sooter");
                return false;
            }

            return true;
        }

        /// <summary>Creates the bill record if it doesn't exist yet (lazy creation).</summary>
        private void EnsureBillExists()
        {
            if (_currentBillId != 0) return;
            string name = txtCustName.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;

            try
            {
                _currentBillId = _db.CreateBill(name, txtPhone.Text.Trim(), dtpBillDate.Value.Date);
                lblSaveStatus.Text      = $"✔  Bill created  [{DateTime.Now:hh:mm tt}]";
                lblSaveStatus.ForeColor = CGreen;
            }
            catch (Exception ex)
            {
                lblSaveStatus.Text      = "Save error: " + ex.Message;
                lblSaveStatus.ForeColor = CRed;
            }
        }

        private void OnCancelClicked(object? sender, EventArgs e)
        {
            _editingIndex     = null;
            btnAdd.Text       = "＋  Add";
            btnCancel.Visible = false;
            ResetInputs();
            ClearError();
        }

        private void OnClearAllClicked(object? sender, EventArgs e)
        {
            if (_measurements.Count == 0) return;
            if (MessageBox.Show($"Delete all {_measurements.Count} measurement(s)?",
                    "Clear All", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                if (_currentBillId != 0)
                    _db.DeleteAllMeasurements(_currentBillId);

                _measurements.Clear();
                _nextSerial       = 1;
                _editingIndex     = null;
                btnAdd.Text       = "＋  Add";
                btnCancel.Visible = false;
                ResetInputs();
                RefreshGrid();
                RefreshTotals();
                ClearError();
            }
            catch (Exception ex) { ShowError("Delete error: " + ex.Message); }
        }

        private void OnGridCellClicked(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _measurements.Count) return;
            if (dgv.Columns[e.ColumnIndex].Name == "colEdit")
                LoadForEdit(e.RowIndex);
            else if (dgv.Columns[e.ColumnIndex].Name == "colDel")
                DeleteRow(e.RowIndex);
        }

        // ════════════════════════════════════════════════════════════════════
        //  Data Operations
        // ════════════════════════════════════════════════════════════════════

        private void LoadForEdit(int idx)
        {
            var m = _measurements[idx];
            txtHeight.Text = FormatMeasurement(m.HeightInches, m.HeightSooter);
            txtWidth.Text  = FormatMeasurement(m.WidthInches, m.WidthSooter);
            nudQuantity.Value     = m.Quantity;
            SelectSide(m.Side);
            _editingIndex     = idx;
            btnAdd.Text       = "✔  Save Changes";
            btnCancel.Visible = true;
            ClearError();
            txtHeight.Focus();
        }

        /// <summary>Formats inches + sooter as "79.7".</summary>
        private string FormatMeasurement(int inches, int sooter)
        {
            if (inches == 0 && sooter == 0) return "";
            if (sooter == 0) return inches.ToString();
            return $"{inches}.{sooter}";
        }

        private void DeleteRow(int idx)
        {
            var m = _measurements[idx];
            if (MessageBox.Show($"Delete measurement #{m.SerialNumber}?",
                    "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                if (m.Id != 0) _db.DeleteMeasurement(m.Id);
                _measurements.RemoveAt(idx);
                if (_editingIndex == idx)
                {
                    _editingIndex     = null;
                    btnAdd.Text       = "＋  Add";
                    btnCancel.Visible = false;
                    ResetInputs();
                }
                RefreshGrid();
                RefreshTotals();
            }
            catch (Exception ex) { ShowError("Delete error: " + ex.Message); }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Print
        // ════════════════════════════════════════════════════════════════════

        private void PrintCurrentBill()
        {
            string name = txtCustName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Please enter a customer name before printing.", "Print", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_measurements.Count == 0)
            {
                MessageBox.Show("No measurements to print.", "Print", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            PrintBill(name, txtPhone.Text.Trim(), dtpBillDate.Value.Date, _measurements, ReadCharges());
        }

        /// <summary>
        /// Writes the bill on screen as a customer-ready PDF and opens it, so the shop can
        /// hand the file over or send it on WhatsApp straight away.
        /// </summary>
        private void ExportCurrentBillPdf()
        {
            string name = txtCustName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Please enter a customer name before saving a PDF bill.", "PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_measurements.Count == 0)
            {
                MessageBox.Show("No measurements to save.", "PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GreenWood Bills");
            Directory.CreateDirectory(folder);

            using var dlg = new SaveFileDialog
            {
                Title            = "Save bill as PDF",
                Filter           = "PDF file (*.pdf)|*.pdf",
                FileName         = SafeFileName($"Bill-{name}-{dtpBillDate.Value.Date:yyyyMMdd}"),
                InitialDirectory = folder
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                BillPdfService.Generate(dlg.FileName, new BillPdfData
                {
                    CustomerName = name,
                    Phone        = txtPhone.Text.Trim(),
                    Date         = dtpBillDate.Value.Date,
                    BillNumber   = _currentBillId,
                    Rows         = _measurements.ToList(),
                    Charges      = ReadCharges(),
                    Logo         = LogoBytes()
                });

                lblSaveStatus.Text      = $"✔  PDF saved  [{DateTime.Now:hh:mm tt}]";
                lblSaveStatus.ForeColor = CGreen;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not create the PDF: " + ex.Message, "PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// The three numbers a shop actually re-uses: length, width and quantity. Nothing else
        /// goes in the file, so it opens as a clean three-column sheet in Excel.
        /// </summary>
        private void ExportSizesCsv()
        {
            if (_measurements.Count == 0)
            {
                MessageBox.Show("No measurements to export.", "CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GreenWood Bills");
            Directory.CreateDirectory(folder);

            string name = txtCustName.Text.Trim();
            using var dlg = new SaveFileDialog
            {
                Title            = "Export sizes as CSV",
                Filter           = "CSV file (*.csv)|*.csv",
                FileName         = SafeFileName(name.Length == 0 ? $"Sizes-{dtpBillDate.Value.Date:yyyyMMdd}"
                                                                : $"Sizes-{name}-{dtpBillDate.Value.Date:yyyyMMdd}"),
                InitialDirectory = folder
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Length,Width,Qty");
                foreach (var m in _measurements)
                    sb.AppendLine($"{_fmt.FormatCompact(m.HeightInches, m.HeightSooter)}," +
                                  $"{_fmt.FormatCompact(m.WidthInches, m.WidthSooter)},{m.Quantity}");

                // The BOM is what tells Excel the file is UTF-8 rather than guessing at a code page.
                File.WriteAllText(dlg.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));

                lblSaveStatus.Text      = $"✔  CSV saved  [{DateTime.Now:hh:mm tt}]";
                lblSaveStatus.ForeColor = CBlue;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not create the CSV: " + ex.Message, "CSV", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>A file name the customer can read, with nothing Windows would reject.</summary>
        private static string SafeFileName(string name)
        {
            foreach (char bad in Path.GetInvalidFileNameChars())
                name = name.Replace(bad, '-');
            return name.Length <= 80 ? name : name[..80];
        }

        private static byte[]? LogoBytes()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", "logo.jpg");
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        private void PrintBill(string customerName, string phone, DateTime date, IList<MeasurementEntry> rows, BillCharges charges)
        {
            string billText = BuildBillText(customerName, phone, date, rows, charges);

            using var pd = new System.Drawing.Printing.PrintDocument();
            pd.PrintPage += (_, e) =>
            {
                if (e.Graphics == null) return;
                var font  = new Font("Courier New", 9f);
                var brush = new SolidBrush(Color.Black);
                float y   = e.MarginBounds.Top;
                foreach (var line in billText.Split('\n'))
                {
                    e.Graphics.DrawString(line.TrimEnd('\r'), font, brush, e.MarginBounds.Left, y);
                    y += font.GetHeight(e.Graphics);
                    if (y > e.MarginBounds.Bottom) break;
                }
                font.Dispose(); brush.Dispose();
            };

            using var dlg = new PrintDialog { Document = pd };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try { pd.Print(); }
                catch (Exception ex) { MessageBox.Show("Print error: " + ex.Message, "Print", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private string BuildBillText(string customerName, string phone, DateTime date, IList<MeasurementEntry> rows, BillCharges charges)
        {
            var sb  = new System.Text.StringBuilder();
            string line    = new string('─', 68);
            string dblLine = new string('═', 68);

            sb.AppendLine(dblLine);
            sb.AppendLine("  GreenWoodWorkAging — Measurement Bill");
            sb.AppendLine(dblLine);
            sb.AppendLine($"  Customer  : {customerName}");
            if (!string.IsNullOrWhiteSpace(phone))
                sb.AppendLine($"  Phone     : {phone}");
            sb.AppendLine($"  Date      : {date:dd MMMM yyyy}");
            sb.AppendLine($"  Printed   : {DateTime.Now:dd MMMM yyyy   hh:mm tt}");
            sb.AppendLine(line);
            sb.AppendLine($"  {"#",-4}  {"Height",-10}  {"Width",-10}  {"Qty",-5}  {"Sides",-10}  {"Total Inches",-12}");
            sb.AppendLine(line);

            foreach (var m in rows)
            {
                string h = _fmt.FormatCompact(m.HeightInches, m.HeightSooter);
                string w = _fmt.FormatCompact(m.WidthInches,  m.WidthSooter);
                string r = _fmt.FormatCompactFromEighths(m.RowTotalEighths);
                sb.AppendLine($"  {m.SerialNumber,-4}  {h,-10}  {w,-10}  {m.Quantity,-5}  {SideLabel(m.Side),-10}  {r,-12}");
            }

            sb.AppendLine(line);
            var t = _calc.CalculateGrandTotal(rows);
            sb.AppendLine($"  Total Rows   : {t.TotalRows}");
            sb.AppendLine($"  Total Pieces : {t.TotalPieces}");
            sb.AppendLine($"  Grand Total  : {_fmt.FormatCompactFromEighths(t.GrandTotalEighths)} in");
            sb.AppendLine($"               : {_fmt.FormatGrandTotalFeetDot(t)}");

            if (!charges.IsEmpty)
            {
                int    eighths = t.GrandTotalEighths;
                decimal feet   = BillCharges.Feet(eighths);
                sb.AppendLine(line);
                sb.AppendLine(ChargeLine($"Tape  {feet:0.00} ft × Rs {charges.RatePerFoot:0.##}/ft", charges.TapeAmount(eighths)));
                sb.AppendLine(ChargeLine("Gari carriage", charges.CarriageFare));
                sb.AppendLine(ChargeLine($"Sheet cuts  {charges.SheetCuts} × Rs {charges.RatePerSheet:0.##}", charges.SheetAmount));
                sb.AppendLine(dblLine);
                sb.AppendLine(ChargeLine("TOTAL AMOUNT", charges.TotalAmount(eighths)));
            }

            sb.AppendLine(dblLine);
            return sb.ToString();
        }

        /// <summary>A bill line with the rupee amount pushed to the right edge.</summary>
        private static string ChargeLine(string label, decimal amount) => $"  {label,-40}  {Money(amount),14}";

        private static string Money(decimal amount) => BillCharges.Rupees(amount);

        // ════════════════════════════════════════════════════════════════════
        //  UI Refresh
        // ════════════════════════════════════════════════════════════════════

        private void RefreshGrid()
        {
            dgv.Rows.Clear();
            foreach (var m in _measurements)
            {
                // Compact dot notation: 80 → "80", 80 in + 4 sooter → "80.4"
                string hVal = _fmt.FormatCompact(m.HeightInches, m.HeightSooter);
                string wVal = _fmt.FormatCompact(m.WidthInches,  m.WidthSooter);
                string oVal = _fmt.FormatCompactFromEighths(m.OnePieceEighths);
                string rVal = _fmt.FormatCompactFromEighths(m.RowTotalEighths);
                dgv.Rows.Add(m.SerialNumber.ToString(), hVal, wVal, m.Quantity.ToString(),
                             SideLabel(m.Side), oVal, rVal, "✏  Edit", "🗑  Delete");
            }
            // Clear the current cell and row selection so the rebuilt grid does not
            // leave row 1 painted with the selection colours.
            dgv.CurrentCell = null;
            dgv.ClearSelection();
        }

        private void RefreshTotals()
        {
            var t = _calc.CalculateGrandTotal(_measurements);
            lblTotalRows.Text   = t.TotalRows.ToString("N0");
            lblTotalPieces.Text = t.TotalPieces.ToString("N0");
            lblGrandTotal.Text  = _fmt.FormatCompactFromEighths(t.GrandTotalEighths) + " in";
            lblGrandSub.Text    = _fmt.FormatGrandTotalDecimalSub(t);
            lblFeet.Text        = _fmt.FormatGrandTotalFeetDot(t);
            lblFeetSub.Text     = _fmt.FormatGrandTotalFeetDecimalSub(t);
            RefreshAmounts();
        }

        private void ResetInputs()
        {
            txtHeight.Text = "";
            txtWidth.Text  = "";
            nudQuantity.Value = 1;
            SelectSide(MeasurementSide.Both);
        }

        // ════════════════════════════════════════════════════════════════════
        //  Clock
        // ════════════════════════════════════════════════════════════════════

        private void StartClock()
        {
            _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _clockTimer.Tick += (_, _) => UpdateClock();
            _clockTimer.Start();
        }

        private void UpdateClock()
        {
            if (lblClock != null)
                lblClock.Text = DateTime.Now.ToString("MMM dd, yyyy   hh:mm tt");
        }

        // ════════════════════════════════════════════════════════════════════
        //  Error helpers
        // ════════════════════════════════════════════════════════════════════

        private void ShowError(string msg)  => lblError.Text = "⚠  " + msg;
        private void ClearError()           => lblError.Text = "";

        // ════════════════════════════════════════════════════════════════════
        //  UI Factory Helpers
        // ════════════════════════════════════════════════════════════════════

        private static TextBox BuildTextBox(Point loc, int width, int height)
        {
            var tb = new TextBox
            {
                Location    = loc,
                Size        = new Size(width, height),
                Font        = FInput,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor   = Color.FromArgb(250, 252, 254),
                MaxLength   = 6,
                TextAlign   = HorizontalAlignment.Center
            };
            tb.KeyPress += (_, e) =>
            {
                char c = e.KeyChar == ',' ? '.' : e.KeyChar;   // accept both decimal separators
                if (char.IsControl(c)) { e.KeyChar = c; return; }
                if (!char.IsDigit(c) && c != '.') { e.Handled = true; return; }

                // Validate the text the key would produce, so an invalid sooter can never
                // appear no matter where the caret sits. After the dot only one digit is
                // allowed and it must be 0–7, because 8 sooter make a whole inch.
                string next = tb.Text
                    .Remove(tb.SelectionStart, tb.SelectionLength)
                    .Insert(tb.SelectionStart, c.ToString());
                int dot = next.IndexOf('.');
                bool ok = dot < 0
                    ? next.All(char.IsDigit)
                    : next.Remove(dot).All(char.IsDigit)
                      && next.Length - dot <= 2
                      && next.Substring(dot + 1).All(ch => ch >= '0' && ch <= '7');
                e.Handled = !ok;
                e.KeyChar = c;
            };
            tb.Enter += (_, _) => tb.BackColor = Color.FromArgb(235, 250, 242);
            tb.Leave += (_, _) => tb.BackColor = Color.FromArgb(250, 252, 254);
            return tb;
        }

        private static NumericUpDown BuildNud(Point loc, int width, int height, int min, int max)
        {
            var nud = new NumericUpDown
            {
                Location    = loc,
                Size        = new Size(width, height),
                Font        = FInput,
                Minimum     = min,
                Maximum     = max,
                Value       = min,
                TextAlign   = HorizontalAlignment.Center,
                BorderStyle = BorderStyle.FixedSingle
            };
            nud.Enter += (_, _) => nud.BackColor = Color.FromArgb(235, 250, 242);
            nud.Leave += (_, _) => nud.BackColor = SystemColors.Window;
            return nud;
        }

        private static Button BuildButton(string text, Point loc, int w, int h, Color back, Color hover, Color fore)
        {
            var btn = new Button { Text = text, Location = loc, Size = new Size(w, h), Font = FBtn, BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (_, _) => btn.BackColor = hover;
            btn.MouseLeave += (_, _) => btn.BackColor = back;
            return btn;
        }

        private static Button BuildOutlineButton(string text, Point loc, int w, int h, Color color)
        {
            var btn = new Button { Text = text, Location = loc, Size = new Size(w, h), Font = FBtn, BackColor = Color.White, ForeColor = color, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderColor = color;
            btn.FlatAppearance.BorderSize  = 1;
            btn.MouseEnter += (_, _) => { btn.BackColor = color; btn.ForeColor = Color.White; };
            btn.MouseLeave += (_, _) => { btn.BackColor = Color.White; btn.ForeColor = color; };
            return btn;
        }

        private static Button MakeSideBtn(string text, int width)
        {
            var btn = new Button
            {
                Text      = text,
                Width     = width,
                Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
                BackColor = Color.FromArgb(44, 72, 55),
                ForeColor = Color.FromArgb(200, 230, 210),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (_, _) => btn.BackColor = Color.FromArgb(52, 133, 90);
            btn.MouseLeave += (_, _) => btn.BackColor = Color.FromArgb(44, 72, 55);
            return btn;
        }

        private DataGridViewColumn AddTextCol(string name, string header, int width)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name, HeaderText = header, Width = width, ReadOnly = true,
                SortMode      = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode  = DataGridViewAutoSizeColumnMode.None,
                MinimumWidth  = width,
                Resizable     = DataGridViewTriState.False,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Padding = new Padding(4, 0, 4, 0) }
            };
            dgv.Columns.Add(col);
            return col;
        }

        private void AddBtnCol(string name, string header, string text, Color bg, Color fg, int width)
        {
            var col = new DataGridViewButtonColumn
            {
                Name = name, HeaderText = header,
                Text = text, UseColumnTextForButtonValue = true,
                Width = width, FlatStyle = FlatStyle.Flat,
                MinimumWidth = width,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Resizable = DataGridViewTriState.False
            };
            col.DefaultCellStyle.BackColor = bg;
            col.DefaultCellStyle.ForeColor = fg;
            col.DefaultCellStyle.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            col.DefaultCellStyle.Padding   = new Padding(2, 0, 2, 0);
            // A selected row must not repaint its action buttons with the saturated
            // row-selection colour — they would look permanently hovered.
            col.DefaultCellStyle.SelectionBackColor = bg;
            col.DefaultCellStyle.SelectionForeColor = fg;
            dgv.Columns.Add(col);
        }

        private static void PaintCardBorder(Graphics g, Control c)
        {
            using var pen = new Pen(CBorder, 1);
            g.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
        }

        // ── Dispose ───────────────────────────────────────────────────────────

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _clockTimer?.Dispose();
                _custSaveTimer?.Dispose();
                _chargeSaveTimer?.Dispose();
                _logo?.Dispose();
                _watermark?.Dispose();
                _db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
