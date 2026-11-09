using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;

namespace GreenWoodWorkAging
{
    /// <summary>
    /// The checking step of an image import. The picture stays on screen while the sizes are
    /// confirmed: whatever the text reader picked up arrives already typed in — a printed list
    /// therefore needs no typing at all — and the rest is filled in by eye. That is quicker than
    /// reading a phone and typing into the main grid, and it never adds a size nobody checked.
    /// </summary>
    public sealed class ImageImportForm : Form
    {
        private static readonly Color CBg      = Color.FromArgb(242, 245, 249);
        private static readonly Color CCard    = Color.White;
        private static readonly Color CBorder  = Color.FromArgb(218, 226, 234);
        private static readonly Color CText    = Color.FromArgb(24, 30, 42);
        private static readonly Color CSub     = Color.FromArgb(130, 142, 158);
        private static readonly Color CGreen   = Color.FromArgb(34, 139, 87);
        private static readonly Color CBlue    = Color.FromArgb(37, 99, 180);
        private static readonly Color CRed     = Color.FromArgb(203, 40, 40);
        private static readonly Color CRedLite = Color.FromArgb(255, 243, 243);

        private static readonly Font FTitle = new("Segoe UI", 15f, FontStyle.Bold);
        private static readonly Font FLine  = new("Segoe UI", 10f);
        private static readonly Font FMeta  = new("Segoe UI", 8.5f);
        private static readonly Font FBtn   = new("Segoe UI", 10f, FontStyle.Bold);
        private static readonly Font FGrid  = new("Segoe UI", 11.5f);

        private readonly List<ImageReadResult> _results;
        private readonly MeasurementInputValidator _validator = new();

        private PictureBox _pic    = null!;
        private Label _lblFile     = null!;
        private Label _lblPosition = null!;
        private DataGridView _grid = null!;
        private int _shown;

        /// <summary>Filled when the user confirms; empty if the window was cancelled.</summary>
        public List<ImportedSize> Sizes { get; } = new();

        public ImageImportForm(IReadOnlyList<ImageReadResult> results)
        {
            _results = results.ToList();

            Text          = "Import sizes from image";
            BackColor     = CBg;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize    = new Size(1120, 660);
            MinimumSize   = new Size(860, 520);
            Font          = new Font("Segoe UI", 9f);
            AppIcon.Apply(this);
            KeyPreview    = true;

            var root = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                BackColor   = CBg,
                Padding     = new Padding(16, 12, 16, 12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildBody(),   0, 1);
            root.Controls.Add(BuildFooter(), 0, 2);

            Controls.Add(root);

            ShowImage(0);
            KeyDown += OnKeyDown;
        }

        // ── Cards ────────────────────────────────────────────────────────────

        private Control BuildHeader()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Padding = new Padding(16, 8, 16, 8) };
            card.Paint += (_, e) => PaintBorder(e.Graphics, card);

            card.Controls.Add(new Label
            {
                Text      = "Import sizes from image",
                Font      = FTitle,
                ForeColor = CText,
                AutoSize  = true,
                Location  = new Point(14, 8)
            });

            int images = _results.Count;
            int found  = _results.Sum(r => r.Sizes.Count);
            int failed = _results.Count(r => r.Error != null);

            string line = found switch
            {
                > 0 when failed == 0 =>
                    $"The reader found {found} size(s) in {images} image(s) — check them against the photo, then add.",
                > 0 =>
                    $"The reader found {found} size(s); {failed} image(s) could not be read — type the rest, then add.",
                _ when images == 0 =>
                    "No image was readable — type the sizes here and add them.",
                _ =>
                    "The reader could not pick these sizes out of the photo(s) — type them here while looking at the image."
            };

            card.Controls.Add(new Label
            {
                Text      = line,
                Font      = FLine,
                ForeColor = found > 0 ? CGreen : CSub,
                AutoSize  = false,
                Dock      = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleLeft
            });

            return card;
        }

        private Control BuildBody()
        {
            var body = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 3,
                RowCount    = 1,
                BackColor   = CBg
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44f));

            body.Controls.Add(BuildImageCard(), 0, 0);
            body.Controls.Add(BuildSizesCard(), 2, 0);
            return body;
        }

        private Control BuildImageCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Margin = new Padding(0) };
            card.Paint += (_, e) => PaintBorder(e.Graphics, card);

            var strip = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                Height      = 36,
                ColumnCount = 4,
                RowCount    = 1,
                BackColor   = CCard,
                Padding     = new Padding(10, 4, 10, 2)
            };
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40f));
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40f));
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62f));
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100f));

            var previous = BuildButton("◀", 32, 26, CBlue);
            var next     = BuildButton("▶", 32, 26, CBlue);
            previous.Anchor = AnchorStyles.None;
            next.Anchor     = AnchorStyles.None;
            previous.Click += (_, _) => ShowImage(_shown - 1);
            next.Click     += (_, _) => ShowImage(_shown + 1);

            _lblPosition = new Label
            {
                Font      = FMeta,
                ForeColor = CSub,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock      = DockStyle.Fill
            };

            _lblFile = new Label
            {
                Font      = FMeta,
                ForeColor = CText,
                TextAlign = ContentAlignment.MiddleRight,
                Dock      = DockStyle.Fill,
                AutoEllipsis = true
            };

            strip.Controls.Add(previous, 0, 0);
            strip.Controls.Add(next,     1, 0);
            strip.Controls.Add(_lblPosition, 2, 0);
            strip.Controls.Add(_lblFile,     3, 0);

            _pic = new PictureBox
            {
                Dock          = DockStyle.Fill,
                SizeMode      = PictureBoxSizeMode.Zoom,
                BackColor     = Color.FromArgb(250, 251, 253),
                Margin        = new Padding(10, 2, 10, 10)
            };

            card.Controls.Add(_pic);
            card.Controls.Add(strip);
            return card;
        }

        private Control BuildSizesCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Margin = new Padding(0) };
            card.Paint += (_, e) => PaintBorder(e.Graphics, card);

            _grid = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                Margin                = new Padding(10, 8, 10, 10),
                BackgroundColor       = Color.White,
                BorderStyle           = BorderStyle.FixedSingle,
                AllowUserToAddRows    = true,
                AllowUserToDeleteRows = true,
                RowHeadersVisible     = false,
                ScrollBars            = ScrollBars.Vertical,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight   = 34,
                Font                  = FGrid,
                EnableHeadersVisualStyles = false
            };
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 250, 247);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = CText;
            _grid.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 252, 249);
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(215, 245, 228);
            _grid.DefaultCellStyle.SelectionForeColor = CText;

            _grid.Columns.Add(Column("Height (in.sooter)", 150));
            _grid.Columns.Add(Column("Width (in.sooter)",  150));
            _grid.Columns.Add(Column("Qty",                 62));

            // Where each number came from is only a note, so it takes whatever width is left.
            var source = Column("read from", 120, readOnly: true);
            source.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            source.MinimumWidth   = 110;
            _grid.Columns.Add(source);

            _grid.DefaultValuesNeeded += (_, e) => e.Row.Cells[2].Value = "1";

            foreach (var result in _results)
                foreach (var size in result.Sizes)
                    _grid.Rows.Add(Compact(size.HeightInches, size.HeightSooter, size.HeightHalf),
                                   Compact(size.WidthInches,  size.WidthSooter,  size.WidthHalf),
                                   size.Quantity.ToString(),
                                   size.SourceLine);

            if (_grid.Rows.Count == 0)
                _grid.Rows.Add();

            card.Controls.Add(_grid);
            return card;
        }

        private static DataGridViewTextBoxColumn Column(string header, int width, bool readOnly = false)
            => new()
            {
                HeaderText     = header,
                Width          = width,
                ReadOnly       = readOnly,
                Resizable      = DataGridViewTriState.False,
                SortMode       = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = readOnly ? DataGridViewContentAlignment.MiddleLeft
                                         : DataGridViewContentAlignment.MiddleCenter,
                    ForeColor = readOnly ? CSub : CText
                }
            };

        private Control BuildFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 3,
                RowCount    = 1,
                BackColor   = CBg
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190f));

            var cancel = BuildButton("✕  Cancel", 138, 36, CRed);
            var add    = BuildButton("＋  Add all to bill", 178, 36, CGreen);
            cancel.Anchor = add.Anchor = AnchorStyles.None;
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            add.Click    += (_, _) => ConfirmAndClose();

            footer.Controls.Add(cancel, 1, 0);
            footer.Controls.Add(add,    2, 0);
            return footer;
        }

        private static Button BuildButton(string text, int width, int height, Color color)
        {
            var button = new Button
            {
                Text      = text,
                Size      = new Size(width, height),
                Font      = FBtn,
                BackColor = Color.White,
                ForeColor = color,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = color;
            button.FlatAppearance.BorderSize  = 1;
            button.MouseEnter += (_, _) => { button.BackColor = color; button.ForeColor = Color.White; };
            button.MouseLeave += (_, _) => { button.BackColor = Color.White; button.ForeColor = color; };
            return button;
        }

        // ── Behaviour ────────────────────────────────────────────────────────

        /// <summary>Escape walks out without adding anything.</summary>
        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        }

        private void ShowImage(int index)
        {
            if (_results.Count == 0)
            {
                _lblFile.Text = _lblPosition.Text = "";
                return;
            }

            _shown = Math.Clamp(index, 0, _results.Count - 1);
            var result = _results[_shown];

            _lblPosition.Text = $"{_shown + 1} / {_results.Count}";
            _lblFile.Text     = result.Error != null ? $"⚠  {result.FileName} — {result.Error}" : result.FileName;

            var old = _pic.Image;
            _pic.Image = LoadPreview(result.FilePath);
            old?.Dispose();
        }

        /// <summary>A copy at screen size: a phone photo kept whole would eat tens of MB per image.</summary>
        private static Image? LoadPreview(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            using var full = new Bitmap(path);
            const int maxSide = 1600;
            float factor = Math.Min(1f, (float)maxSide / Math.Max(full.Width, full.Height));
            if (factor >= 1f) return new Bitmap(full);

            var small = new Bitmap((int)(full.Width * factor), (int)(full.Height * factor));
            using var g = Graphics.FromImage(small);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(full, 0, 0, small.Width, small.Height);
            return small;
        }

        private void ConfirmAndClose()
        {
            _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            _grid.EndEdit();

            var collected = new List<ImportedSize>();
            var problems  = new List<string>();

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;

                string height = CellText(row, 0), width = CellText(row, 1), quantity = CellText(row, 2);
                if (height.Length == 0 && width.Length == 0 && quantity.Length == 0) continue;

                int number = row.Index + 1;
                var (heightCheck, heightParsed) = _validator.ParseAndValidateMeasurement(OrZero(height), "Height");
                var (widthCheck,  widthParsed)  = _validator.ParseAndValidateMeasurement(OrZero(width),  "Width");
                var quantityCheck = _validator.ValidateQuantity(OrOne(quantity));

                bool bothZero = heightParsed != null && widthParsed != null &&
                                heightParsed.Inches == 0 && heightParsed.Sooter == 0 && heightParsed.Half == 0 &&
                                widthParsed.Inches  == 0 && widthParsed.Sooter  == 0 && widthParsed.Half  == 0;

                if (!heightCheck.IsValid || !widthCheck.IsValid || !quantityCheck.IsValid || bothZero)
                {
                    problems.Add($"Row {number}: {FirstProblem(heightCheck, widthCheck, quantityCheck)}");
                    row.Cells[0].Style.BackColor = heightCheck.IsValid ? row.Cells[0].Style.BackColor : CRedLite;
                    row.Cells[1].Style.BackColor = widthCheck.IsValid  ? row.Cells[1].Style.BackColor : CRedLite;
                    row.Cells[2].Style.BackColor = quantityCheck.IsValid ? row.Cells[2].Style.BackColor : CRedLite;
                    continue;
                }

                collected.Add(new ImportedSize
                {
                    HeightInches = heightParsed!.Inches, HeightSooter = heightParsed.Sooter,
                    HeightHalf   = heightParsed.Half,
                    WidthInches  = widthParsed!.Inches,  WidthSooter  = widthParsed.Sooter,
                    WidthHalf    = widthParsed.Half,
                    Quantity     = int.Parse(OrOne(quantity)),
                    SourceLine   = CellText(row, 3)
                });
            }

            if (problems.Count > 0)
            {
                MessageBox.Show(
                    "Fix these before adding:\n\n" + string.Join("\n", problems.Take(6)) +
                    (problems.Count > 6 ? $"\n… and {problems.Count - 6} more" : ""),
                    "Check the sizes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (collected.Count == 0)
            {
                MessageBox.Show("There is nothing typed in the list yet — add the sizes first.",
                    "Nothing to add", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Sizes.AddRange(collected);
            DialogResult = DialogResult.OK;
            Close();
        }

        private static string FirstProblem(MeasurementInputValidator.ValidationResult height,
                                           MeasurementInputValidator.ValidationResult width,
                                           MeasurementInputValidator.ValidationResult quantity)
            => !height.IsValid   ? height.ErrorMessage
             : !width.IsValid    ? width.ErrorMessage
             : !quantity.IsValid ? quantity.ErrorMessage
             : "height and width are both zero";

        private static string CellText(DataGridViewRow row, int column)
            => row.Cells[column].Value?.ToString()?.Trim() ?? "";

        private static string OrZero(string text) => text.Length == 0 ? "0" : text;
        private static string OrOne(string text)  => text.Length == 0 ? "1" : text;

        // The same reading the bill window gives: the two digits the size was typed with.
        private static readonly MeasurementDisplayFormatter Fmt = new();
        private static string Compact(int inches, int sooter, int half = 0)
            => Fmt.FormatCompact(inches, sooter, half);

        private static void PaintBorder(Graphics g, Control c)
        {
            using var pen = new Pen(CBorder, 1f);
            g.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
        }
    }
}
