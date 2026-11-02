using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;

namespace GreenWoodWorkAging
{
    /// <summary>
    /// The sheets customers leave at the shop, grouped the way they actually arrive: one entry per
    /// visit, holding every material handed over in it — "KMI 10, Super Glass 5" — with the total
    /// sheets in, how many of each material have since been cut, and what is left on the shelf.
    ///
    /// The list on the left is the deliveries; the panel on the right is the selected one.
    /// Nothing here feeds the bill or the measurement maths — it is a separate ledger.
    /// </summary>
    public sealed class SheetStockForm : Form
    {
        private static readonly Color CBg      = Color.FromArgb(242, 245, 249);
        private static readonly Color CCard    = Color.White;
        private static readonly Color CBorder  = Color.FromArgb(218, 226, 234);
        private static readonly Color CText    = Color.FromArgb(24, 30, 42);
        private static readonly Color CSub     = Color.FromArgb(130, 142, 158);
        private static readonly Color CGreen   = Color.FromArgb(34, 139, 87);
        private static readonly Color CBlue    = Color.FromArgb(37, 99, 180);
        private static readonly Color CRed     = Color.FromArgb(203, 40, 40);
        private static readonly Color CInputBg = Color.FromArgb(250, 252, 254);

        private static readonly Font FTitle    = new("Segoe UI", 15f, FontStyle.Bold);
        private static readonly Font FCaption  = new("Segoe UI", 8.5f, FontStyle.Bold);
        private static readonly Font FHint     = new("Segoe UI", 8.5f, FontStyle.Italic);
        private static readonly Font FInput    = new("Segoe UI", 10.5f);
        private static readonly Font FBody     = new("Segoe UI", 9.5f);
        private static readonly Font FBtn      = new("Segoe UI", 9.5f, FontStyle.Bold);
        private static readonly Font FGridHead = new("Segoe UI", 9f, FontStyle.Bold);

        private readonly DatabaseService _db;
        private readonly string _initialCustomer;
        private readonly string _initialPhone;
        private readonly int _billId;

        private TextBox        txtSearch    = null!;
        private DataGridView   dgvVisits    = null!;
        private DataGridView   dgvLines     = null!;
        private TextBox        txtName      = null!;
        private TextBox        txtPhone     = null!;
        private DateTimePicker dtpDate      = null!;
        private TextBox        txtMaterial  = null!;
        private NumericUpDown  nudReceived  = null!;
        private Label          lblSummary   = null!;
        private Label          lblStatus    = null!;

        private List<SheetStockVisit> _visits = new();

        /// <summary>The delivery the right-hand panel is showing — saved, or being typed.</summary>
        private SheetStockVisit _draft = null!;

        private int  _selectedId;
        private bool _loading;

        public SheetStockForm(DatabaseService db, string customerName, string phone, int billId)
        {
            _db              = db;
            _initialCustomer = customerName ?? "";
            _initialPhone    = phone ?? "";
            _billId          = billId;

            Text          = "Customers sheets — sheet stock";
            BackColor     = CBg;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize    = new Size(1240, 680);
            MinimumSize   = new Size(980, 560);
            Font          = new Font("Segoe UI", 9f);
            AppIcon.Apply(this);

            var root = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                BackColor   = CBg,
                Padding     = new Padding(18, 14, 18, 12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderHeight()));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,  100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));

            var body = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1,
                BackColor   = CBg
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            body.Controls.Add(BuildVisitListCard(), 0, 0);
            body.Controls.Add(BuildDetailCard(),    1, 0);

            root.Controls.Add(BuildHeaderCard(), 0, 0);
            root.Controls.Add(body,              0, 1);
            root.Controls.Add(BuildStatusLabel(), 0, 2);

            Controls.Add(root);

            RefreshList();
            if (_visits.Count == 0) NewDelivery();
        }

        // ── Sizing ────────────────────────────────────────────────────────────

        private static int Measure(string text, Font font) =>
            TextRenderer.MeasureText(text, font).Width;

        private static int MeasureH(string text, Font font) =>
            TextRenderer.MeasureText(text, font).Height;

        private static int HeaderHeight() => Math.Max(64, MeasureH("0", FTitle) + 34);

        /// <summary>An input box tall enough for its own text — a 10.5 pt box needs more than it looks like it does.</summary>
        private static int InputH() => Math.Max(30, MeasureH("0", FInput) + 12);

        /// <summary>"Ag" so a caption row carries the descenders as well as the tall letters.</summary>
        private static int CaptionH() => Math.Max(20, MeasureH("Ag", FCaption) + 6);

        /// <summary>Caption, box and the card's own padding, so nothing in a row is clipped.</summary>
        private static int EntryHeight() => CaptionH() + InputH() + 24;

        /// <summary>The line the running totals get across the top of the detail card.</summary>
        private static int SummaryHeight() => Math.Max(26, MeasureH("Ag", new Font("Segoe UI", 9.5f, FontStyle.Bold)) + 10);

        /// <summary>
        /// A field column wide enough for both its caption and what is typed into it. A box
        /// sized by eye at 100% scaling hides the tail of a phone number on any other setting.
        /// </summary>
        private static int FieldW(string caption, int boxWidth) =>
            Math.Max(Measure(caption, FCaption) + 24, boxWidth + 12);

        /// <summary>How wide a box has to be to show this much text — a name, a phone, a material.</summary>
        private static int BoxW(string sample, int minWidth) =>
            Math.Max(minWidth, Measure(sample, FInput) + 24);

        // ── Header: title, search, new delivery, close ────────────────────────

        private Control BuildHeaderCard()
        {
            int searchW = Math.Max(260, Measure("Search name, phone, date or material", FInput) + 40);
            int newW    = Math.Max(120, Measure("＋  New", FBtn) + 30);
            int closeW  = Math.Max(96,  Measure("←  Close", FBtn) + 30);

            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Margin = new Padding(0, 0, 0, 10) };
            card.Paint += (_, e) => PaintBorder(e.Graphics, card);

            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 4,
                RowCount    = 1,
                BackColor   = CCard,
                Padding     = new Padding(16, 10, 16, 10)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, searchW + 12f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, newW + 12f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, closeW + 12f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var title = new Label
            {
                Text      = "▦  Customers sheets",
                Font      = FTitle,
                ForeColor = CText,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtSearch = new TextBox
            {
                Font            = FInput,
                BorderStyle     = BorderStyle.FixedSingle,
                BackColor       = CInputBg,
                Anchor          = AnchorStyles.None,
                Width           = searchW,
                Height          = InputH(),
                PlaceholderText = "Search name, phone, date or material…"
            };
            txtSearch.TextChanged += (_, _) => RefreshList();
            txtSearch.Enter += (_, _) => txtSearch.BackColor = Color.FromArgb(235, 250, 242);
            txtSearch.Leave += (_, _) => txtSearch.BackColor = CInputBg;

            var btnNew = BuildButton("＋  New", CGreen);
            btnNew.Anchor = AnchorStyles.None;
            btnNew.Click += (_, _) => NewDelivery();

            var btnClose = BuildButton("←  Close", CBlue);
            btnClose.Anchor = AnchorStyles.None;
            btnClose.Click += (_, _) => Close();

            tbl.Controls.Add(title,     0, 0);
            tbl.Controls.Add(txtSearch, 1, 0);
            tbl.Controls.Add(btnNew,    2, 0);
            tbl.Controls.Add(btnClose,  3, 0);
            card.Controls.Add(tbl);
            return card;
        }

        // ── Left: one row per customer delivery ───────────────────────────────

        private Control BuildVisitListCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Margin = new Padding(0) };
            card.Paint += (_, e) => PaintBorder(e.Graphics, card);

            dgvVisits = NewGrid();
            dgvVisits.CellClick += OnVisitCellClick;
            dgvVisits.SelectionChanged += (_, _) =>
            {
                if (_loading) return;
                if (dgvVisits.CurrentRow?.Tag is SheetStockVisit v) ShowVisit(v);
            };

            AddFillColumn(dgvVisits, "colVCustomer", "Customer", 26f);
            AddFillColumn(dgvVisits, "colVPhone",    "Phone",    18f);
            AddFillColumn(dgvVisits, "colVDate",     "Date",     17f);
            AddFillColumn(dgvVisits, "colVGiven",    "Sheets",   11f);
            AddFillColumn(dgvVisits, "colVUsed",     "Used",     10f);
            AddFillColumn(dgvVisits, "colVLeft",     "Left",     10f);
            AddDeleteButton(dgvVisits);

            card.Controls.Add(dgvVisits);
            return card;
        }

        // ── Right: the selected delivery ──────────────────────────────────────

        private Control BuildDetailCard()
        {
            int captionH = CaptionH();

            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Margin = new Padding(9, 0, 0, 0) };
            card.Paint += (_, e) => PaintBorder(e.Graphics, card);

            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 4,
                BackColor   = CCard,
                Padding     = new Padding(14, 10, 14, 10)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, EntryHeight()));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, SummaryHeight()));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent,  100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, EntryHeight()));

            // — whose delivery, and what it adds up to
            var head = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 4,
                RowCount    = 1,
                BackColor   = CCard
            };
            // Each box is as wide as the longest thing a shop types into it, and each column
            // as wide as that box or its caption — whichever is bigger.
            int nameW  = BoxW("Muhammad Ali Raza", 200);
            int phoneW = BoxW("0317 7777777",      170);
            int dateW  = BoxW("31 Dec 2026",       140);

            head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, FieldW("Customer name", nameW)));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, FieldW("Phone",         phoneW)));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, FieldW("Date",          dateW) + 26));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100f));
            head.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            txtName  = PlainBox(nameW,  InputH(), 60);
            txtPhone = PlainBox(phoneW, InputH(), 20);
            dtpDate  = new DateTimePicker
            {
                Format       = DateTimePickerFormat.Custom,
                CustomFormat = "dd MMM yyyy",
                Font         = FInput,
                Width        = dateW + 22,
                Height       = InputH(),
                Anchor       = AnchorStyles.Left,
                Value        = DateTime.Today
            };

            // The totals get their own line across the card: squeezed between the three fields
            // they wrapped one word to a row, which is not what a count of sheets is for.
            lblSummary = new Label
            {
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = CGreen,
                TextAlign = ContentAlignment.MiddleLeft
            };

            head.Controls.Add(Group("Customer name", txtName,  captionH), 0, 0);
            head.Controls.Add(Group("Phone",         txtPhone, captionH), 1, 0);
            head.Controls.Add(Group("Date",          dtpDate,  captionH), 2, 0);

            // — the materials in this delivery
            dgvLines = NewGrid();
            dgvLines.CellEndEdit  += OnLineEdited;
            dgvLines.CellClick    += OnLineCellClick;

            AddFillColumn(dgvLines, "colMaterial", "Sheet / material", 40f);
            AddFillColumn(dgvLines, "colGiven",    "Given",            14f);
            var used = AddFillColumn(dgvLines, "colUsed", "Used", 14f);
            used.ReadOnly = false;
            AddFillColumn(dgvLines, "colLeft", "Left", 14f);
            AddDeleteButton(dgvLines);

            // — the next material to add to this delivery
            var entry = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 4,
                RowCount    = 1,
                BackColor   = CCard
            };
            int materialW = BoxW("Super Glass board", 200);
            int givenW    = BoxW("1234", 100) + 22;   // the spinner's arrows sit inside its width
            const string hintText = "One line per material.";

            entry.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, FieldW("Sheet / material", materialW)));
            entry.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, FieldW("Sheets given",     givenW)));
            entry.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Math.Max(150, Measure("＋  Add sheet", FBtn) + 40)));
            entry.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Measure(hintText, FHint) + 20));
            entry.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            txtMaterial = PlainBox(materialW, InputH(), 40);
            nudReceived = new NumericUpDown
            {
                Minimum     = 1,
                Maximum     = 9999,
                Value       = 1,
                Font        = FInput,
                Width       = givenW,
                Height      = InputH(),
                TextAlign   = HorizontalAlignment.Center,
                BorderStyle = BorderStyle.FixedSingle
            };

            entry.Controls.Add(Group("Sheet / material", txtMaterial, captionH), 0, 0);
            entry.Controls.Add(Group("Sheets given",     nudReceived, captionH), 1, 0);

            var btnAdd = BuildButton("＋  Add sheet", CGreen);
            btnAdd.AutoSize = false;
            btnAdd.Size     = new Size(Math.Max(150, Measure("＋  Add sheet", FBtn) + 40) - 12, InputH());
            btnAdd.Anchor   = AnchorStyles.Left;
            btnAdd.Margin   = new Padding(0, captionH, 0, 0);
            btnAdd.Click += (_, _) => AddLine();
            entry.Controls.Add(btnAdd, 2, 0);

            var hint = new Label
            {
                Text      = hintText,
                Font      = FHint,
                ForeColor = CSub,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin    = new Padding(6, captionH, 0, 0)
            };
            entry.Controls.Add(hint, 3, 0);

            tbl.Controls.Add(head,     0, 0);
            tbl.Controls.Add(lblSummary, 0, 1);
            tbl.Controls.Add(dgvLines, 0, 2);
            tbl.Controls.Add(entry,    0, 3);
            card.Controls.Add(tbl);

            txtName.KeyDown      += OnHeaderEnter;
            txtPhone.KeyDown     += OnHeaderEnter;
            txtMaterial.KeyDown  += OnEntryEnter;
            nudReceived.KeyDown  += OnEntryEnter;
            dtpDate.KeyDown      += OnEntryEnter;
            txtName.Leave        += (_, _) => CommitHeader();
            txtPhone.Leave       += (_, _) => CommitHeader();
            dtpDate.CloseUp      += (_, _) => CommitHeader();
            return card;
        }

        private void OnHeaderEnter(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            CommitHeader();
        }

        private void OnEntryEnter(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            AddLine();
        }

        private static Control Group(string caption, Control input, int captionH)
        {
            var cell = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 2,
                BackColor   = CCard,
                Margin      = new Padding(0, 0, 10, 0)
            };
            cell.RowStyles.Add(new RowStyle(SizeType.Absolute, captionH));
            cell.RowStyles.Add(new RowStyle(SizeType.Percent,  100f));
            cell.Controls.Add(new Label
            {
                Text      = caption,
                Font      = FCaption,
                ForeColor = CSub,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            }, 0, 0);
            input.Anchor = AnchorStyles.Left;
            cell.Controls.Add(input, 0, 1);
            return cell;
        }

        private static TextBox PlainBox(int width, int height, int maxLength) => new()
        {
            Font        = FInput,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor   = CInputBg,
            Width       = width,
            Height      = height,
            MaxLength   = maxLength
        };

        private static DataGridView NewGrid()
        {
            var grid = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                BackgroundColor       = CCard,
                BorderStyle           = BorderStyle.None,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible     = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                Font                  = FBody,
                ColumnHeadersHeight   = 34,
                EnableHeadersVisualStyles = false,
                GridColor             = CBorder
            };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(237, 245, 240);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = CText;
            grid.ColumnHeadersDefaultCellStyle.Font      = FGridHead;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(237, 245, 240);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = CText;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 236, 224);
            grid.DefaultCellStyle.SelectionForeColor = CText;
            return grid;
        }

        private static DataGridViewTextBoxColumn AddFillColumn(DataGridView grid, string name, string header,
                                                              float weight)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name       = name,
                HeaderText = header,
                ReadOnly   = true,
                SortMode   = DataGridViewColumnSortMode.NotSortable,
                FillWeight = weight
            };
            grid.Columns.Add(col);
            return col;
        }

        /// <summary>The ✕ column takes a small share of the width, enough for the mark.</summary>
        private static void AddDeleteButton(DataGridView grid)
        {
            var del = new DataGridViewButtonColumn
            {
                Name                        = "colDelete",
                HeaderText                  = "",
                Text                        = "✕",
                UseColumnTextForButtonValue = true,
                FlatStyle                   = FlatStyle.Flat,
                FillWeight                  = 8f
            };
            del.Resizable = DataGridViewTriState.False;
            grid.Columns.Add(del);
        }

        private Label BuildStatusLabel()
        {
            lblStatus = new Label
            {
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                Font      = new Font("Segoe UI", 9f),
                ForeColor = CSub,
                TextAlign = ContentAlignment.MiddleLeft,
                Text      = ""
            };
            return lblStatus;
        }

        // ── Loading ───────────────────────────────────────────────────────────

        /// <summary>Re-read the deliveries, keep the selected one in view, and refresh both panels.</summary>
        private void RefreshList()
        {
            _loading = true;
            try
            {
                _visits = _db.SearchSheetStockVisits(txtSearch.Text);

                dgvVisits.Rows.Clear();
                foreach (var v in _visits)
                {
                    dgvVisits.Rows.Add(v.CustomerName, v.PhoneNumber, v.Date.ToString("dd MMM yyyy"),
                                       v.TotalReceived, v.TotalUsed, v.TotalLeft, "✕");
                    dgvVisits.Rows[^1].Tag = v;
                }

                var leftCol = dgvVisits.Columns["colVLeft"];
                if (leftCol != null)
                    foreach (DataGridViewRow r in dgvVisits.Rows)
                        r.Cells[leftCol.Index].Style.ForeColor =
                            Convert.ToInt32(r.Cells[leftCol.Index].Value ?? 0) < 0 ? CRed : CGreen;

                int row = RowOf(_selectedId);
                if (row >= 0)
                {
                    dgvVisits.CurrentCell = dgvVisits.Rows[row].Cells[0];
                    ShowVisit(_visits[row]);
                }
                else if (_visits.Count > 0)
                {
                    _selectedId = 0;
                    dgvVisits.CurrentCell = dgvVisits.Rows[0].Cells[0];
                    ShowVisit(_visits[0]);
                }
                else
                {
                    ShowVisit(NewDraft());
                }

                int given = _visits.Sum(v => v.TotalReceived);
                int used  = _visits.Sum(v => v.TotalUsed);
                string filter = txtSearch.Text.Trim();
                lblStatus.Text = $"{_visits.Count} customer entry(ies)   ·   {given} sheets given   ·   " +
                                 $"{used} used   ·   {given - used} left" +
                                 (filter.Length > 0 ? $"   ·   filtered by “{filter}”" : "");
            }
            finally
            {
                _loading = false;
            }
        }

        private int RowOf(int visitId)
        {
            if (visitId == 0) return -1;
            for (int i = 0; i < dgvVisits.Rows.Count; i++)
                if (dgvVisits.Rows[i].Tag is SheetStockVisit v && v.Id == visitId) return i;
            return -1;
        }

        /// <summary>Put one delivery into the right-hand panel.</summary>
        private void ShowVisit(SheetStockVisit visit)
        {
            _loading = true;
            try
            {
                _draft = visit;
                _selectedId = visit.Id;

                txtName.Text   = visit.CustomerName;
                txtPhone.Text  = visit.PhoneNumber;
                dtpDate.Value  = visit.Date;

                dgvLines.Rows.Clear();
                foreach (var line in visit.Lines)
                {
                    dgvLines.Rows.Add(line.Material, line.Received, line.Used, line.Remaining, "✕");
                    dgvLines.Rows[^1].Tag = line;
                }
                dgvLines.CurrentCell = null;

                var left = dgvLines.Columns["colLeft"];
                if (left != null)
                    foreach (DataGridViewRow r in dgvLines.Rows)
                        r.Cells[left.Index].Style.ForeColor =
                            Convert.ToInt32(r.Cells[left.Index].Value ?? 0) < 0 ? CRed : CGreen;

                lblSummary.Text = visit.Lines.Count == 0
                    ? "No sheets recorded."
                    : $"{visit.TotalReceived} sheets in   ·   {visit.TotalUsed} used   ·   {visit.TotalLeft} left";
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>An unsaved delivery for the customer the window was opened for.</summary>
        private SheetStockVisit NewDraft() => new()
        {
            BillId       = _billId,
            CustomerName = _initialCustomer,
            PhoneNumber  = _initialPhone,
            Date         = DateTime.Today
        };

        private void NewDelivery()
        {
            if (txtSearch.Text.Length > 0) txtSearch.Text = "";

            _selectedId = 0;
            dgvVisits.ClearSelection();
            ShowVisit(NewDraft());

            lblStatus.ForeColor = CSub;
            lblStatus.Text = "Type the customer's name, then add every sheet handed over.";
            (txtName.Text.Length > 0 ? txtMaterial : txtName).Focus();
        }

        // ── Saving ────────────────────────────────────────────────────────────

        /// <summary>Write the header and all its lines together — the list is what was typed.</summary>
        private void SaveDraft()
        {
            try
            {
                _db.SaveSheetStockVisit(_draft);
                _selectedId      = _draft.Id;
                lblStatus.ForeColor = CGreen;
                RefreshList();
            }
            catch (Exception ex)
            {
                lblStatus.Text    = "Could not save: " + ex.Message;
                lblStatus.ForeColor = CRed;
            }
        }

        /// <summary>Keep the delivery in step with the name, phone and date in the panel.</summary>
        private void CommitHeader()
        {
            if (_loading || _draft.Id == 0) return;

            string name  = txtName.Text.Trim();
            string phone = txtPhone.Text.Trim();

            if (name.Length == 0)
            {
                txtName.Text   = _draft.CustomerName;
                lblStatus.Text = "The customer's name cannot be empty.";
                lblStatus.ForeColor = CRed;
                return;
            }
            if (name == _draft.CustomerName && phone == _draft.PhoneNumber &&
                dtpDate.Value.Date == _draft.Date)
                return;

            _draft.CustomerName = name;
            _draft.PhoneNumber  = phone;
            _draft.Date         = dtpDate.Value.Date;
            SaveDraft();
        }

        private void AddLine()
        {
            string name = txtName.Text.Trim();
            string mat  = txtMaterial.Text.Trim();

            if (name.Length == 0)
            {
                lblStatus.Text = "Enter the customer's name first.";
                lblStatus.ForeColor = CRed;
                txtName.Focus();
                return;
            }
            if (mat.Length == 0)
            {
                lblStatus.Text = "Enter what the sheets are — KMI, Super Glass, PVC, textile…";
                lblStatus.ForeColor = CRed;
                txtMaterial.Focus();
                return;
            }

            _draft.CustomerName = name;
            _draft.PhoneNumber  = txtPhone.Text.Trim();
            _draft.Date         = dtpDate.Value.Date;
            _draft.Lines.Add(new SheetStockEntry { Material = mat, Received = (int)nudReceived.Value, Used = 0 });

            txtMaterial.Text  = "";
            nudReceived.Value = 1;
            SaveDraft();
            txtMaterial.Focus();
        }

        /// <summary>Only Used is editable — typing how many of a material have been cut is the whole job here.</summary>
        private void OnLineEdited(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvLines.Rows.Count) return;
            if (dgvLines.Columns[e.ColumnIndex].Name != "colUsed") return;
            if (dgvLines.Rows[e.RowIndex].Tag is not SheetStockEntry line) return;

            string raw = Convert.ToString(dgvLines.Rows[e.RowIndex].Cells[e.ColumnIndex].Value) ?? "";
            if (!int.TryParse(raw.Trim(), out int used) || used < 0)
            {
                dgvLines.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = line.Used;
                lblStatus.Text = "Used must be a whole number.";
                lblStatus.ForeColor = CRed;
                return;
            }
            if (used == line.Used) return;

            line.Used = used;
            try
            {
                _db.SetSheetStockUsed(line.Id, used);
                lblStatus.ForeColor = CGreen;
                RefreshList();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Could not save: " + ex.Message;
                lblStatus.ForeColor = CRed;
            }
        }

        private void OnLineCellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != dgvLines.Columns["colDelete"]?.Index) return;
            if (dgvLines.Rows[e.RowIndex].Tag is not SheetStockEntry line) return;

            if (MessageBox.Show($"Remove the {line.Received} × \"{line.Material}\" sheets from this delivery?",
                    "Remove sheet", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            if (line.Id != 0) _db.DeleteSheetStock(line.Id);
            _draft.Lines.Remove(line);
            RefreshList();
        }

        private void OnVisitCellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != dgvVisits.Columns["colDelete"]?.Index) return;
            if (dgvVisits.Rows[e.RowIndex].Tag is not SheetStockVisit visit) return;

            if (MessageBox.Show(
                    $"Delete the whole delivery for \"{visit.CustomerName}\" — " +
                    $"{visit.TotalReceived} sheets across {visit.Lines.Count} material(s)?",
                    "Delete delivery", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _db.DeleteSheetStockVisit(visit.Id);
            if (_selectedId == visit.Id) _selectedId = 0;
            RefreshList();
        }

        private Button BuildButton(string text, Color color)
        {
            var btn = new Button
            {
                Text      = text,
                AutoSize  = true,
                Padding   = new Padding(10, 5, 10, 5),
                Font      = FBtn,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (_, _) => btn.BackColor = ControlDark(color);
            btn.MouseLeave += (_, _) => btn.BackColor = color;
            return btn;
        }

        private static Color ControlDark(Color c) =>
            Color.FromArgb(Math.Max(0, c.R - 30), Math.Max(0, c.G - 30), Math.Max(0, c.B - 30));

        private static void PaintBorder(Graphics g, Control c)
        {
            using var pen = new Pen(CBorder, 1f);
            g.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
        }
    }
}
