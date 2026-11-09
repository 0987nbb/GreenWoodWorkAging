using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;

namespace GreenWoodWorkAging
{
    /// <summary>
    /// The question asked before a cutting plan is made: what sheets are actually on the floor.
    /// The shop writes the material, its width and length and how many it has; the optimizer then
    /// cuts the bill out of exactly those sheets. Leaving the count blank is allowed — that sheet
    /// is simply treated as plenty, which is how every plan was cut before this window existed.
    /// </summary>
    public sealed class SheetStockPickForm : Form
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

        private static readonly Font FTitle  = new("Segoe UI", 12.5f, FontStyle.Bold);
        private static readonly Font FLine   = new("Segoe UI", 9.5f);
        private static readonly Font FSub    = new("Segoe UI", 9f);
        private static readonly Font FHead   = new("Segoe UI", 9f, FontStyle.Bold);
        private static readonly Font FBtn    = new("Segoe UI", 9.5f, FontStyle.Bold);

        private readonly DataGridView _grid = new();

        /// <summary>What the shop wrote, once it pressed Optimize. Empty means "cut as before".</summary>
        public List<SheetCuttingOptimizer.StockSheet> Stock { get; } = new();

        public SheetStockPickForm(IEnumerable<string> sheetCodes)
        {
            Text            = "Sheets in stock";
            BackColor       = CBg;
            StartPosition   = FormStartPosition.CenterParent;
            ClientSize      = new Size(760, 460);
            MinimumSize     = new Size(620, 360);
            Font            = new Font("Segoe UI", 9f);
            MinimizeBox     = false;
            AppIcon.Apply(this);

            var root = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                BackColor   = CBg,
                Padding     = new Padding(16, 12, 16, 12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(BuildHeading(),   0, 0);
            root.Controls.Add(BuildGrid(),      0, 1);
            root.Controls.Add(BuildButtons(),   0, 2);

            Controls.Add(root);

            foreach (string code in sheetCodes.Distinct(StringComparer.OrdinalIgnoreCase))
                AddRow(code);
            if (_grid.Rows.Count == 0) AddRow("");
        }

        private Control BuildHeading()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Padding = new Padding(16, 12, 16, 12) };
            card.Paint += (_, e) => PaintBorder(e.Graphics, card);

            var title = new Label
            {
                Text      = "▦  Which sheets are on the floor?",
                Font      = FTitle,
                ForeColor = CText,
                AutoSize  = true,
                Location  = new Point(14, 10)
            };
            var hint = new Label
            {
                Text      = "Width and length in inches — 96 × 48 is the standard 8 ft × 4 ft sheet. " +
                            "Leave the count empty for a sheet that is not being counted. " +
                            "A sheet named like a sheet code on the bill is cut from that code's sizes.",
                Font      = FSub,
                ForeColor = CSub,
                AutoSize  = false,
                Width     = 660,
                Height    = 40,
                Location  = new Point(16, 40)
            };

            card.Resize += (_, _) =>
            {
                hint.Width = Math.Max(320, card.ClientSize.Width - 32);
                card.Height = title.Bottom + hint.Height + 22;
            };
            card.Height = title.Bottom + hint.Height + 22;

            card.Controls.Add(title);
            card.Controls.Add(hint);
            return card;
        }

        private Control BuildGrid()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Padding = new Padding(12) };
            card.Paint += (_, e) => PaintBorder(e.Graphics, card);

            _grid.Dock               = DockStyle.Fill;
            _grid.AllowUserToAddRows = true;
            _grid.RowHeadersVisible  = false;
            _grid.BackgroundColor    = CCard;
            _grid.GridColor          = CBorder;
            _grid.BorderStyle        = BorderStyle.None;
            _grid.Font               = FLine;
            _grid.RowTemplate.Height = 32;
            _grid.ColumnHeadersDefaultCellStyle.BackColor  = CBg;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor  = CSub;
            _grid.ColumnHeadersDefaultCellStyle.Font       = FHead;
            _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            _grid.EnableHeadersVisualStyles = false;
            _grid.DefaultCellStyle.BackColor    = CInputBg;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(226, 240, 232);
            _grid.DefaultCellStyle.SelectionForeColor = CText;

            _grid.Columns.Add(Text("colName",  "Sheet name / type", 240));
            _grid.Columns.Add(Text("colW",     "Width (in)",        110));
            _grid.Columns.Add(Text("colH",     "Height / length (in)", 130));
            _grid.Columns.Add(Text("colQty",   "Quantity",          100));

            card.Controls.Add(_grid);
            return card;

            DataGridViewTextBoxColumn Text(string name, string header, int width) => new()
            {
                Name         = name,
                HeaderText   = header,
                Width        = width,
                MinimumWidth = 70,
                SortMode     = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = name == "colName" ? DataGridViewContentAlignment.MiddleLeft
                                                   : DataGridViewContentAlignment.MiddleCenter
                }
            };
        }

        private Control BuildButtons()
        {
            var bar = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 3,
                RowCount    = 1,
                BackColor   = CBg,
                Margin      = new Padding(0, 10, 0, 0)
            };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var note = new Label
            {
                Text   = "",
                Font   = FSub,
                ForeColor = CRed,
                AutoSize = true,
                Margin = new Padding(4, 10, 0, 0)
            };

            var optimize = BuildButton("⚙  Optimize", CGreen);
            var back     = BuildButton("←  Cancel",   CBlue);
            optimize.Click += (_, _) => TryAccept(note);
            back.Click     += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            bar.Controls.Add(note,     0, 0);
            bar.Controls.Add(optimize, 1, 0);
            bar.Controls.Add(back,     2, 0);
            AcceptButton = optimize;
            CancelButton = back;
            return bar;
        }

        private static Button BuildButton(string text, Color color)
        {
            var b = new Button
            {
                Text      = text,
                AutoSize  = true,
                Padding   = new Padding(10, 5, 10, 5),
                Font      = FBtn,
                BackColor = Color.White,
                ForeColor = color,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Margin    = new Padding(6, 0, 0, 0)
            };
            b.FlatAppearance.BorderColor = color;
            b.FlatAppearance.BorderSize  = 1;
            b.MouseEnter += (_, _) => { b.BackColor = color; b.ForeColor = Color.White; };
            b.MouseLeave += (_, _) => { b.BackColor = Color.White; b.ForeColor = color; };
            return b;
        }

        private void AddRow(string name)
            => _grid.Rows.Add(name,
                              SheetCuttingOptimizer.DefaultSheetWidthInches.ToString(),
                              SheetCuttingOptimizer.DefaultSheetHeightInches.ToString(),
                              "");

        /// <summary>
        /// Reads the sheet the shop wrote. A blank or unusable line is skipped with a note;
        /// a window where every line was cleared means "no stock entered", and the plan is cut
        /// the way it always was.
        /// </summary>
        private void TryAccept(Label note)
        {
            _grid.EndEdit();
            var read = new List<SheetCuttingOptimizer.StockSheet>();
            var bad  = new List<string>();

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;

                string name = Cell(row, "colName");
                int? width  = Number(Cell(row, "colW"));
                int? height = Number(Cell(row, "colH"));
                int? qty    = Number(Cell(row, "colQty"));

                bool empty = name.Length == 0 && width == null && height == null && qty == null;
                if (empty) continue;

                if (width is not > 0 || height is not > 0)
                {
                    bad.Add($"row {row.Index + 1}");
                    continue;
                }

                read.Add(new SheetCuttingOptimizer.StockSheet(name, width.Value, height.Value, qty ?? 0));
            }

            if (bad.Count > 0)
            {
                note.Text   = $"Width and length are needed on {string.Join(", ", bad)}.";
                note.ForeColor = CRed;
                return;
            }

            Stock.Clear();
            Stock.AddRange(read);
            DialogResult = DialogResult.OK;
            Close();
        }

        private static string Cell(DataGridViewRow row, string column)
            => row.Cells[column].Value?.ToString()?.Trim() ?? "";

        /// <summary>A typed number, or null when the box is empty or holds something else.</summary>
        private static int? Number(string text)
            => int.TryParse(text, out int value) ? value : null;

        private static void PaintBorder(Graphics g, Control c)
        {
            using var pen = new Pen(CBorder, 1f);
            g.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
        }
    }
}
