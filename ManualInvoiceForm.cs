using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;

namespace GreenWoodWorkAging
{
    /// <summary>
    /// Standalone dialog for creating, calculating, saving, searching, and exporting
    /// manual customer invoices / bills without needing row-by-row piece measurements.
    /// </summary>
    public sealed class ManualInvoiceForm : Form
    {
        private static readonly Color CBg        = Color.FromArgb(242, 245, 249);
        private static readonly Color CCard      = Color.White;
        private static readonly Color CBorder    = Color.FromArgb(218, 226, 234);
        private static readonly Color CText      = Color.FromArgb(24, 30, 42);
        private static readonly Color CSub       = Color.FromArgb(130, 142, 158);
        private static readonly Color CGreen     = Color.FromArgb(34, 139, 87);
        private static readonly Color CGreenDark = Color.FromArgb(22, 108, 65);
        private static readonly Color CBlue      = Color.FromArgb(37, 99, 180);
        private static readonly Color CRed       = Color.FromArgb(203, 40, 40);
        private static readonly Color CWarn      = Color.FromArgb(180, 108, 18);
        private static readonly Color CInputBg   = Color.FromArgb(250, 252, 254);
        private static readonly Color CTotalBg   = Color.FromArgb(236, 252, 243);
        private static readonly Color CLabelText = Color.FromArgb(45, 55, 72);

        private static readonly Font FTitle     = new("Segoe UI", 12f, FontStyle.Bold);
        private static readonly Font FSecTitle  = new("Segoe UI", 10.5f, FontStyle.Bold);
        private static readonly Font FCaption   = new("Segoe UI", 9f, FontStyle.Bold);
        private static readonly Font FInput     = new("Segoe UI", 10f);
        private static readonly Font FBtn       = new("Segoe UI", 9.5f, FontStyle.Bold);
        private static readonly Font FGrand     = new("Segoe UI", 16f, FontStyle.Bold);
        private static readonly Font FGridHead  = new("Segoe UI", 9f, FontStyle.Bold);

        private readonly DatabaseService _db;
        private int _selectedId;
        private List<ManualInvoice> _invoices = new();
        private bool _isUpdating;

        // List / Search Controls
        private TextBox      txtSearch    = null!;
        private DataGridView dgvInvoices  = null!;
        private Label        lblListCount = null!;

        // Form Inputs
        private TextBox         txtName       = null!;
        private TextBox         txtPhone      = null!;
        private DateTimePicker  dtpDate       = null!;
        private NumericUpDown   nudTapes      = null!;
        private NumericUpDown   nudTapeRate   = null!;
        private Label           lblTapeAmount = null!;
        private NumericUpDown   nudCuts       = null!;
        private NumericUpDown   nudCutRate    = null!;
        private Label           lblCutAmount  = null!;
        private NumericUpDown   nudCarriage   = null!;
        private Label           lblGrandTotal = null!;
        private Label           lblBreakdown  = null!;
        private TextBox         txtNotes      = null!;

        // Action Buttons
        private Button btnSave   = null!;
        private Button btnPdf    = null!;
        private Button btnNew    = null!;
        private Button btnDelete = null!;

        public ManualInvoiceForm(DatabaseService db, string initialCustomer = "", string initialPhone = "")
        {
            _db = db;

            Text            = "Manual Invoice & Bill — GreenWood Works";
            BackColor       = CBg;
            StartPosition   = FormStartPosition.CenterParent;
            ClientSize      = new Size(1260, 720);
            MinimumSize     = new Size(1040, 600);
            Font            = new Font("Segoe UI", 9f);
            AppIcon.Apply(this);

            var root = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 2,
                BackColor   = CBg,
                Padding     = new Padding(16, 12, 16, 12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,  100f));

            root.Controls.Add(BuildHeaderCard(), 0, 0);

            var body = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1,
                BackColor   = CBg
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53f));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            body.Controls.Add(BuildInvoicesListCard(), 0, 0);
            body.Controls.Add(BuildDetailCard(),       1, 0);

            root.Controls.Add(body, 0, 1);
            Controls.Add(root);

            // Populate initial values
            if (!string.IsNullOrWhiteSpace(initialCustomer))
            {
                txtName.Text  = initialCustomer;
                txtPhone.Text = initialPhone;
            }

            Recalculate();
            RefreshInvoicesList();
        }

        // ── Header Card ───────────────────────────────────────────────────────

        private Control BuildHeaderCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Margin = new Padding(0, 0, 0, 8) };
            card.Paint += (_, e) => PaintBorder(e.Graphics, card);

            // 3 columns: [Back btn 120px] | [Title 100%] | [New Invoice btn 160px]
            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 3,
                RowCount    = 1,
                BackColor   = CCard,
                Padding     = new Padding(12, 0, 12, 0)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));  // Back button
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100f));  // Title
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160f));  // New Invoice button
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // ── Back Button (col 0) ──────────────────────────────────────────
            var btnBack = new Button
            {
                Text        = "◄  Back",
                UseMnemonic = false,
                Font        = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor   = Color.White,
                BackColor   = CBlue,
                FlatStyle   = FlatStyle.Flat,
                Cursor      = Cursors.Hand,
                Size        = new Size(105, 38),
                Anchor      = AnchorStyles.Left
            };
            btnBack.FlatAppearance.BorderSize = 0;
            btnBack.Click      += (_, _) => this.Close();
            btnBack.MouseEnter += (_, _) => btnBack.BackColor = Color.FromArgb(28, 78, 142);
            btnBack.MouseLeave += (_, _) => btnBack.BackColor = CBlue;
            tbl.Controls.Add(btnBack, 0, 0);

            // ── Title (col 1) ────────────────────────────────────────────────
            var title = new Label
            {
                Text        = "Manual Invoice & Bill Creation",
                UseMnemonic = false,
                Font        = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor   = CText,
                Dock        = DockStyle.Fill,
                TextAlign   = ContentAlignment.MiddleLeft,
                Padding     = new Padding(8, 0, 0, 0)
            };
            tbl.Controls.Add(title, 1, 0);

            // ── New Invoice Button (col 2) ────────────────────────────────────
            var btnTopNew = new Button
            {
                Text        = "＋  New Invoice",
                UseMnemonic = false,
                Font        = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor   = Color.White,
                BackColor   = CGreen,
                FlatStyle   = FlatStyle.Flat,
                Cursor      = Cursors.Hand,
                Size        = new Size(150, 38),
                Anchor      = AnchorStyles.Right
            };
            btnTopNew.FlatAppearance.BorderSize = 0;
            btnTopNew.Click      += (_, _) => ResetToNew();
            btnTopNew.MouseEnter += (_, _) => btnTopNew.BackColor = CGreenDark;
            btnTopNew.MouseLeave += (_, _) => btnTopNew.BackColor = CGreen;
            tbl.Controls.Add(btnTopNew, 2, 0);

            card.Controls.Add(tbl);
            return card;
        }

        // ── Left Card: Invoices List & Search ─────────────────────────────────

        private Control BuildInvoicesListCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Margin = new Padding(0, 0, 10, 0) };
            card.Paint += (_, e) => PaintBorder(e.Graphics, card);

            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                BackColor   = CCard,
                Padding     = new Padding(12)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent,  100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));

            // Search row
            txtSearch = new TextBox
            {
                Dock            = DockStyle.Fill,
                Font            = FInput,
                BorderStyle     = BorderStyle.FixedSingle,
                BackColor       = CInputBg,
                PlaceholderText = "🔍 Search customer name, phone, or date..."
            };
            txtSearch.TextChanged += (_, _) => RefreshInvoicesList();
            tbl.Controls.Add(txtSearch, 0, 0);

            // Invoices Grid
            dgvInvoices = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible     = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = false,
                BackgroundColor       = Color.White,
                BorderStyle           = BorderStyle.None,
                CellBorderStyle       = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor             = Color.FromArgb(238, 242, 246),
                Font                  = new Font("Segoe UI", 9.5f)
            };
            dgvInvoices.ColumnHeadersDefaultCellStyle.Font = FGridHead;
            dgvInvoices.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 248, 250);
            dgvInvoices.ColumnHeadersDefaultCellStyle.ForeColor = CText;
            dgvInvoices.ColumnHeadersHeight = 32;
            dgvInvoices.EnableHeadersVisualStyles = false;

            dgvInvoices.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText   = "#",
                Width        = 40,
                MinimumWidth = 35
            });
            dgvInvoices.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText   = "Date",
                Width        = 95,
                MinimumWidth = 90
            });
            dgvInvoices.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText   = "Customer",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 110
            });
            dgvInvoices.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText   = "Phone",
                Width        = 105,
                MinimumWidth = 95
            });
            dgvInvoices.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText   = "Total Amount",
                Width        = 135,
                MinimumWidth = 130,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = CGreenDark
                }
            });

            dgvInvoices.SelectionChanged += (_, _) =>
            {
                if (_isUpdating) return;
                if (dgvInvoices.SelectedRows.Count > 0 && dgvInvoices.SelectedRows[0].Tag is ManualInvoice inv)
                {
                    LoadInvoice(inv);
                }
            };

            tbl.Controls.Add(dgvInvoices, 0, 1);

            lblListCount = new Label
            {
                Dock        = DockStyle.Fill,
                Font        = new Font("Segoe UI", 8.5f),
                ForeColor   = CSub,
                TextAlign   = ContentAlignment.MiddleLeft,
                Text        = "0 invoices",
                UseMnemonic = false
            };
            tbl.Controls.Add(lblListCount, 0, 2);

            card.Controls.Add(tbl);
            return card;
        }

        // ── Right Card: Detail & Editor ───────────────────────────────────────

        private Control BuildDetailCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Margin = new Padding(0) };
            card.Paint += (_, e) => PaintBorder(e.Graphics, card);

            var scroll = new Panel
            {
                Dock       = DockStyle.Fill,
                AutoScroll = true,
                BackColor  = CCard,
                Padding    = new Padding(16, 12, 16, 12)
            };

            var flow = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                AutoSize    = true,
                AutoSizeMode= AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount    = 7,
                BackColor   = CCard
            };
            flow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // Section 1: Customer Information
            flow.Controls.Add(BuildCustomerSection());

            // Section 2: Tape Details
            flow.Controls.Add(BuildTapeSection());

            // Section 3: Sheet Cutting Details
            flow.Controls.Add(BuildCuttingSection());

            // Section 4: Carriage Charges
            flow.Controls.Add(BuildCarriageSection());

            // Section 5: Grand Total Tile
            flow.Controls.Add(BuildGrandTotalTile());

            // Section 6: Notes
            flow.Controls.Add(BuildNotesSection());

            // Section 7: Action Buttons
            flow.Controls.Add(BuildActionBar());

            scroll.Controls.Add(flow);
            card.Controls.Add(scroll);
            return card;
        }

        private Control BuildCustomerSection()
        {
            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                AutoSize    = true,
                AutoSizeMode= AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount    = 3,
                BackColor   = CCard,
                Margin      = new Padding(0, 0, 0, 14)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));  // section header
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));  // label row - tall enough for 9pt bold
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));  // input row

            var header = CreateSectionHeader("1. Customer Information");
            tbl.SetColumnSpan(header, 3);
            tbl.Controls.Add(header, 0, 0);

            tbl.Controls.Add(CreateLabel("Customer Name *"), 0, 1);
            tbl.Controls.Add(CreateLabel("Phone Number"), 1, 1);
            tbl.Controls.Add(CreateLabel("Invoice Date"), 2, 1);

            txtName  = new TextBox { Dock = DockStyle.Fill, Font = FInput, BorderStyle = BorderStyle.FixedSingle, BackColor = CInputBg };
            txtPhone = new TextBox { Dock = DockStyle.Fill, Font = FInput, BorderStyle = BorderStyle.FixedSingle, BackColor = CInputBg };
            dtpDate  = new DateTimePicker { Dock = DockStyle.Fill, Font = FInput, Format = DateTimePickerFormat.Short, Value = DateTime.Today };

            tbl.Controls.Add(txtName,  0, 2);
            tbl.Controls.Add(txtPhone, 1, 2);
            tbl.Controls.Add(dtpDate,  2, 2);

            return tbl;
        }

        private Control BuildTapeSection()
        {
            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                AutoSize    = true,
                AutoSizeMode= AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount    = 3,
                BackColor   = CCard,
                Margin      = new Padding(0, 0, 0, 14)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));  // section header
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));  // label row
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));  // input row

            var header = CreateSectionHeader("2. Tape Details");
            tbl.SetColumnSpan(header, 3);
            tbl.Controls.Add(header, 0, 0);

            tbl.Controls.Add(CreateLabel("Total Tapes (Feet)"), 0, 1);
            tbl.Controls.Add(CreateLabel("Tape Rate (Per Foot)"), 1, 1);
            tbl.Controls.Add(CreateLabel("Tape Amount (Rs)"), 2, 1);

            nudTapes      = CreateNud(0, 1000000, 2);
            nudTapeRate   = CreateNud(0, 100000, 2);
            lblTapeAmount = CreateResultLabel("Rs 0.00");

            nudTapes.ValueChanged    += (_, _) => Recalculate();
            nudTapeRate.ValueChanged += (_, _) => Recalculate();

            tbl.Controls.Add(nudTapes,      0, 2);
            tbl.Controls.Add(nudTapeRate,   1, 2);
            tbl.Controls.Add(lblTapeAmount, 2, 2);

            return tbl;
        }

        private Control BuildCuttingSection()
        {
            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                AutoSize    = true,
                AutoSizeMode= AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount    = 3,
                BackColor   = CCard,
                Margin      = new Padding(0, 0, 0, 14)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));  // section header
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));  // label row
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));  // input row

            var header = CreateSectionHeader("3. Sheet Cutting Details");
            tbl.SetColumnSpan(header, 3);
            tbl.Controls.Add(header, 0, 0);

            tbl.Controls.Add(CreateLabel("Total Sheet Cuts (Qty)"), 0, 1);
            tbl.Controls.Add(CreateLabel("Per Sheet Cut Rate (Rs)"), 1, 1);
            tbl.Controls.Add(CreateLabel("Cutting Amount (Rs)"), 2, 1);

            nudCuts      = CreateNud(0, 100000, 0);
            nudCutRate   = CreateNud(0, 100000, 2);
            lblCutAmount = CreateResultLabel("Rs 0.00");

            nudCuts.ValueChanged    += (_, _) => Recalculate();
            nudCutRate.ValueChanged += (_, _) => Recalculate();

            tbl.Controls.Add(nudCuts,      0, 2);
            tbl.Controls.Add(nudCutRate,   1, 2);
            tbl.Controls.Add(lblCutAmount, 2, 2);

            return tbl;
        }

        private Control BuildCarriageSection()
        {
            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                AutoSize    = true,
                AutoSizeMode= AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount    = 3,
                BackColor   = CCard,
                Margin      = new Padding(0, 0, 0, 14)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));  // section header
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));  // label row
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));  // input row

            var header = CreateSectionHeader("4. Carriage / Transportation Charges");
            tbl.SetColumnSpan(header, 2);
            tbl.Controls.Add(header, 0, 0);

            tbl.Controls.Add(CreateLabel("Carriage Charges (Optional)"), 0, 1);
            tbl.Controls.Add(new Label
            {
                Text        = "(Enter 0 if customer arranged own vehicle / transport)",
                Font        = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor   = CSub,
                Dock        = DockStyle.Fill,
                TextAlign   = ContentAlignment.BottomLeft,
                UseMnemonic = false
            }, 1, 1);

            nudCarriage = CreateNud(0, 1000000, 2);
            nudCarriage.ValueChanged += (_, _) => Recalculate();

            tbl.Controls.Add(nudCarriage, 0, 2);

            return tbl;
        }

        private Control BuildGrandTotalTile()
        {
            var pnl = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 84,
                BackColor = CTotalBg,
                Margin    = new Padding(0, 4, 0, 14),
                Padding   = new Padding(16, 8, 16, 8)
            };
            pnl.Paint += (_, e) =>
            {
                using var pen = new Pen(CGreen, 1.5f);
                e.Graphics.DrawRectangle(pen, 0, 0, pnl.Width - 1, pnl.Height - 1);
            };

            var topRow = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                Height      = 38,
                ColumnCount = 2,
                RowCount    = 1,
                BackColor   = CTotalBg
            };
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65f));

            var lblTag = new Label
            {
                Text        = "GRAND TOTAL",
                UseMnemonic = false,
                Font        = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor   = CGreenDark,
                Dock        = DockStyle.Fill,
                TextAlign   = ContentAlignment.MiddleLeft
            };
            lblGrandTotal = new Label
            {
                Text        = "Rs 0.00",
                UseMnemonic = false,
                Font        = FGrand,
                ForeColor   = CGreenDark,
                Dock        = DockStyle.Fill,
                TextAlign   = ContentAlignment.MiddleRight
            };
            topRow.Controls.Add(lblTag, 0, 0);
            topRow.Controls.Add(lblGrandTotal, 1, 0);

            lblBreakdown = new Label
            {
                Dock        = DockStyle.Bottom,
                Height      = 24,
                Font        = new Font("Segoe UI", 9.5f),
                ForeColor   = CText,
                TextAlign   = ContentAlignment.MiddleRight,
                Text        = "Tape: Rs 0.00  +  Cutting: Rs 0.00  +  Carriage: Rs 0.00",
                UseMnemonic = false
            };

            pnl.Controls.Add(topRow);
            pnl.Controls.Add(lblBreakdown);
            return pnl;
        }

        private Control BuildNotesSection()
        {
            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                AutoSize    = true,
                AutoSizeMode= AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount    = 2,
                BackColor   = CCard,
                Margin      = new Padding(0, 0, 0, 14)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));  // label row
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));  // input row

            tbl.Controls.Add(CreateLabel("Special Notes / Remarks (Optional)"), 0, 0);

            txtNotes = new TextBox
            {
                Dock        = DockStyle.Fill,
                Font        = FInput,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor   = CInputBg
            };
            tbl.Controls.Add(txtNotes, 0, 1);

            return tbl;
        }

        private Control BuildActionBar()
        {
            var pnl = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                Height      = 44,
                ColumnCount = 4,
                RowCount    = 1,
                Margin      = new Padding(0, 4, 0, 10)
            };
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f));
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f));

            btnSave   = BuildButton("▣  Save Invoice", CGreen, () => SaveCurrentInvoice());
            btnPdf    = BuildButton("⎙  Save PDF",     CBlue,  () => ExportPdf());
            btnNew    = BuildButton("✕  Clear",         CWarn,  () => ResetToNew());
            btnDelete = BuildButton("▣  Delete",        CRed,   () => DeleteCurrentInvoice());
            btnDelete.Enabled = false;

            btnSave.Dock   = DockStyle.Fill;
            btnPdf.Dock    = DockStyle.Fill;
            btnNew.Dock    = DockStyle.Fill;
            btnDelete.Dock = DockStyle.Fill;

            btnSave.Margin   = new Padding(2);
            btnPdf.Margin    = new Padding(2);
            btnNew.Margin    = new Padding(2);
            btnDelete.Margin = new Padding(2);

            pnl.Controls.Add(btnSave,   0, 0);
            pnl.Controls.Add(btnPdf,    1, 0);
            pnl.Controls.Add(btnNew,    2, 0);
            pnl.Controls.Add(btnDelete, 3, 0);

            return pnl;
        }

        // ── Helper UI Builders ────────────────────────────────────────────────

        private static Label CreateSectionHeader(string title) =>
            new Label
            {
                Text        = title,
                Font        = FSecTitle,
                ForeColor   = CGreenDark,
                Dock        = DockStyle.Fill,
                TextAlign   = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };

        private static Label CreateLabel(string text) =>
            new Label
            {
                Text        = text,
                Font        = FCaption,
                ForeColor   = CLabelText,
                Dock        = DockStyle.Fill,
                TextAlign   = ContentAlignment.BottomLeft,
                UseMnemonic = false,
                Margin      = new Padding(1, 0, 1, 2)
            };

        private static Label CreateResultLabel(string text) =>
            new Label
            {
                Text            = text,
                Font            = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor       = CGreenDark,
                BackColor       = Color.FromArgb(245, 250, 247),
                BorderStyle     = BorderStyle.FixedSingle,
                Dock            = DockStyle.Fill,
                TextAlign       = ContentAlignment.MiddleRight,
                Padding         = new Padding(0, 0, 6, 0),
                UseMnemonic     = false
            };

        private static NumericUpDown CreateNud(decimal min, decimal max, int decimals) =>
            new NumericUpDown
            {
                Dock          = DockStyle.Fill,
                Font          = FInput,
                BorderStyle   = BorderStyle.FixedSingle,
                BackColor     = CInputBg,
                Minimum       = min,
                Maximum       = max,
                DecimalPlaces = decimals,
                Value         = 0
            };

        private static Button BuildButton(string caption, Color accent, Action action)
        {
            var btn = new Button
            {
                Text        = caption,
                UseMnemonic = false,
                Font        = FBtn,
                ForeColor   = accent,
                BackColor   = Color.White,
                FlatStyle   = FlatStyle.Flat,
                Cursor      = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = accent;
            btn.FlatAppearance.BorderSize  = 1;
            btn.Click      += (_, _) => action();
            btn.MouseEnter += (_, _) => { btn.BackColor = accent; btn.ForeColor = Color.White; };
            btn.MouseLeave += (_, _) => { btn.BackColor = Color.White; btn.ForeColor = accent; };
            return btn;
        }

        private static void PaintBorder(Graphics g, Control c)
        {
            using var pen = new Pen(CBorder, 1);
            g.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
        }

        // ── Business Logic & Actions ──────────────────────────────────────────

        private void Recalculate()
        {
            decimal tapes    = nudTapes.Value;
            decimal tapeRate = nudTapeRate.Value;
            decimal tapeAmt  = Math.Round(tapes * tapeRate, 2);
            lblTapeAmount.Text = $"Rs {tapeAmt:N2}";

            int cuts        = (int)nudCuts.Value;
            decimal cutRate = nudCutRate.Value;
            decimal cutAmt  = Math.Round(cuts * cutRate, 2);
            lblCutAmount.Text = $"Rs {cutAmt:N2}";

            decimal carriage   = nudCarriage.Value;
            decimal grandTotal = tapeAmt + cutAmt + carriage;

            lblGrandTotal.Text = $"Rs {grandTotal:N2}";
            lblBreakdown.Text  = $"Tape: Rs {tapeAmt:N2}  +  Cutting: Rs {cutAmt:N2}  +  Carriage: Rs {carriage:N2}";
        }

        private void RefreshInvoicesList()
        {
            _isUpdating = true;
            try
            {
                string q = (txtSearch?.Text ?? "").Trim();
                _invoices = _db.SearchManualInvoices(q);

                dgvInvoices.Rows.Clear();
                int selectRow = -1;

                for (int i = 0; i < _invoices.Count; i++)
                {
                    var inv = _invoices[i];
                    int r = dgvInvoices.Rows.Add(
                        inv.Id.ToString(),
                        inv.InvoiceDate.ToString("dd-MMM-yyyy"),
                        inv.CustomerName,
                        inv.PhoneNumber,
                        $"Rs {inv.GrandTotal:N2}"
                    );
                    dgvInvoices.Rows[r].Tag = inv;
                    if (inv.Id == _selectedId) selectRow = r;
                }

                lblListCount.Text = $"{_invoices.Count} invoice(s) found";

                if (selectRow >= 0)
                    dgvInvoices.Rows[selectRow].Selected = true;
                else if (dgvInvoices.Rows.Count > 0 && _selectedId > 0)
                    dgvInvoices.ClearSelection();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void LoadInvoice(ManualInvoice inv)
        {
            _selectedId = inv.Id;
            txtName.Text       = inv.CustomerName;
            txtPhone.Text      = inv.PhoneNumber;
            dtpDate.Value      = inv.InvoiceDate;
            nudTapes.Value     = Math.Min(nudTapes.Maximum, inv.TotalTapesFeet);
            nudTapeRate.Value  = Math.Min(nudTapeRate.Maximum, inv.TapeRate);
            nudCuts.Value      = Math.Min(nudCuts.Maximum, inv.SheetCuts);
            nudCutRate.Value   = Math.Min(nudCutRate.Maximum, inv.RatePerSheetCut);
            nudCarriage.Value  = Math.Min(nudCarriage.Maximum, inv.CarriageCharges);
            txtNotes.Text      = inv.Notes;

            btnDelete.Enabled = true;
            Recalculate();
        }

        private void ResetToNew()
        {
            _selectedId = 0;
            txtName.Text      = "";
            txtPhone.Text     = "";
            dtpDate.Value     = DateTime.Today;
            nudTapes.Value    = 0;
            nudTapeRate.Value = 0;
            nudCuts.Value     = 0;
            nudCutRate.Value  = 0;
            nudCarriage.Value = 0;
            txtNotes.Text     = "";

            btnDelete.Enabled = false;
            dgvInvoices.ClearSelection();
            Recalculate();
            txtName.Focus();
        }

        private ManualInvoice CollectCurrentInvoice()
        {
            return new ManualInvoice
            {
                Id              = _selectedId,
                CustomerName    = txtName.Text.Trim(),
                PhoneNumber     = txtPhone.Text.Trim(),
                InvoiceDate     = dtpDate.Value.Date,
                TotalTapesFeet  = nudTapes.Value,
                TapeRate        = nudTapeRate.Value,
                SheetCuts       = (int)nudCuts.Value,
                RatePerSheetCut = nudCutRate.Value,
                CarriageCharges = nudCarriage.Value,
                Notes           = txtNotes.Text.Trim()
            };
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Please enter the customer name.", "Customer Name Required",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
                return false;
            }
            return true;
        }

        private void SaveCurrentInvoice()
        {
            if (!ValidateForm()) return;

            var inv = CollectCurrentInvoice();
            _db.SaveManualInvoice(inv);
            _selectedId = inv.Id;
            btnDelete.Enabled = true;

            RefreshInvoicesList();
            MessageBox.Show($"Invoice #{inv.Id} saved successfully!", "Saved",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExportPdf()
        {
            if (!ValidateForm()) return;

            var inv = CollectCurrentInvoice();
            if (inv.Id == 0)
            {
                _db.SaveManualInvoice(inv);
                _selectedId = inv.Id;
                btnDelete.Enabled = true;
                RefreshInvoicesList();
            }

            string safeCust = string.Concat(inv.CustomerName.Split(Path.GetInvalidFileNameChars())).Trim();
            string fileName = $"Invoice-{safeCust}-{inv.InvoiceDate:yyyyMMdd}.pdf";

            using var sfd = new SaveFileDialog
            {
                Title            = "Save Manual Invoice as PDF",
                Filter           = "PDF Document (*.pdf)|*.pdf",
                FileName         = fileName,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                byte[]? logo = LoadLogo();
                ManualInvoicePdfService.Generate(sfd.FileName, inv, logo);

                var res = MessageBox.Show("PDF invoice created successfully! Would you like to open it now?",
                                          "PDF Generated", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName)
                    {
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not create PDF:\n{ex.Message}", "PDF Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteCurrentInvoice()
        {
            if (_selectedId <= 0) return;

            var res = MessageBox.Show($"Are you sure you want to delete invoice #{_selectedId}?",
                                      "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (res != DialogResult.Yes) return;

            _db.DeleteManualInvoice(_selectedId);
            ResetToNew();
            RefreshInvoicesList();
        }

        private static byte[]? LoadLogo()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", "logo.jpg");
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
    }
}
