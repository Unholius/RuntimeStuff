using RuntimeStuff;
using RuntimeStuff.Builders;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using RuntimeStuff.Extensions;
using RuntimeStuff.Helpers;

namespace WinFormsExtensions
{
    public partial class ColumnFilterView : BorderlessResizableForm, INotifyPropertyChanged
    {
        public ColumnFilterView() : base()
        {
            InitializeComponent();
            FilterValuesGridView.BindKey(Keys.Space, () => FilterValuesGridView.BeginUpdate(() => SetSelection(null)));
            FilterValuesGridView.AutoCommitCheckCells();
            this.BindCloseFormKey();
            this.AutoCloseOnDeactivate(() => CanClose);
            EventHelper.BindClickToAction(btnLeftAlignment, () => ChangeAlignmentButtonsCheckedState(true, false, false));
            EventHelper.BindClickToAction(btnJustifyAlignment, () => ChangeAlignmentButtonsCheckedState(false, true, false));
            EventHelper.BindClickToAction(btnRightAlignment, () => ChangeAlignmentButtonsCheckedState(false, false, true));
            this.BindPropertyChangeToAction(x => x.ColumnCellLeftAligned, () =>
            {
                if (ColumnCellLeftAligned)
                {
                    SourceColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                    btnLeftAlignment.Checked = true;
                }
            });

            this.BindPropertyChangeToAction(x => x.ColumnCellRightAligned, () =>
            {
                if (ColumnCellRightAligned)
                {
                    SourceColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    btnRightAlignment.Checked = true;
                }
            });

            this.BindPropertyChangeToAction(x => x.ColumnCellJustifyAligned, () =>
            {
                if (ColumnCellJustifyAligned)
                {
                    SourceColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    btnJustifyAlignment.Checked = true;
                }
            });
        }

        private bool CanClose { get; set; } = true;

        private void ChangeAlignmentButtonsCheckedState(bool left, bool justify, bool right)
        {
            ColumnCellLeftAligned = btnLeftAlignment.Checked = left;
            ColumnCellJustifyAligned = btnJustifyAlignment.Checked = justify;
            ColumnCellRightAligned = btnRightAlignment.Checked = right;
        }

        private static StringFilterBuilder StringFilterBuilder = new StringFilterBuilder();

        public event PropertyChangedEventHandler PropertyChanged;

        public DataGridViewColumn SourceColumn { get; private set; }

        public DataGridView SourceDataGridView { get; private set; }

        private BindingListView<FilterRow> Values { get; set; } = new BindingListView<FilterRow>();

        public bool ColumnCellLeftAligned
        {
            get;
            set;
        }

        public bool ColumnCellRightAligned
        {
            get;
            set;
        }

        public bool ColumnCellJustifyAligned
        {
            get;
            set;
        }

        public static void ShowForColumn(DataGridViewColumn column)
        {
            var f = GetInstance();
            f.valueFilterTemplate1.FieldName = column.DataPropertyName;
            f.valueFilterTemplate1.ValueType = column.ValueType;
            f.SourceColumn = column;
            f.SourceDataGridView = column.DataGridView;
            f.FilterValuesGridView.SetRowColors(column.DataGridView.AlternatingRowsDefaultCellStyle.BackColor);
            f.valueFilterTemplate1.Grid.SetRowColors(column.DataGridView.AlternatingRowsDefaultCellStyle.BackColor);
            f.FilterValuesGridView.DataSource = null;
            f.Text = $"{column.HeaderText ?? column.Name} ({column.ValueType.Name})";
            f.StartPosition = FormStartPosition.Manual;

            // прямоугольник заголовка колонки (в координатах DataGridView)
            var rect = f.SourceDataGridView.GetCellDisplayRectangle(column.Index, -1, true);

            // перевод в экранные координаты
            var screenRect = f.SourceDataGridView.RectangleToScreen(rect);

            // позиция курсора на экране
            var cursor = Cursor.Position;

            // смещение курсора внутри header
            int offsetX = cursor.X - screenRect.Left;

            // итоговая позиция (сохраняем offset)
            var point = new Point(screenRect.Left + offsetX, screenRect.Bottom);

            f.Location = point;
            f.ColumnCaption.Text = column.HeaderText ?? column.Name;
            f.ColumnCaption.Items.AddRange(new string[] { column.DataPropertyName, column.Name, column.HeaderText }.Distinct().Where(x => !string.IsNullOrEmpty(x)).ToArray());
            f.ColumnFormat.Text = column.DefaultCellStyle.Format;
            f.ClearFormatTextButton.Click += (s, e) => f.ColumnFormat.Text = string.Empty;
            f.ClearColumnCaptionButton.Click += (s, e) => f.ColumnCaption.Text = f.SourceColumn.DataPropertyName;
            f.ColumnCaption.TextChanged += (s, e) => f.SourceColumn.HeaderText = f.ColumnCaption.Text;
            switch (column.DefaultCellStyle.Alignment)
            {
                case DataGridViewContentAlignment.NotSet:
                case DataGridViewContentAlignment.MiddleLeft:
                    f.ColumnCellLeftAligned = true;
                    break;
                case DataGridViewContentAlignment.MiddleCenter:
                    f.ColumnCellJustifyAligned = true;
                    break;
                case DataGridViewContentAlignment.MiddleRight:
                    f.ColumnCellRightAligned = true;
                    break;
            }
            switch (column.ValueType.Name)
            {
                case "Boolean":
                    f.ColumnFormat.Items.Add("Да;Нет");
                    f.ColumnFormat.Items.Add("True;False");
                    f.ColumnFormat.Items.Add("1;0");
                    f.ColumnFormat.Items.Add("✔;✘");
                    break;

                case "DateTime":
                    f.ColumnFormat.Items.Add("dd.MM.yyyy");
                    f.ColumnFormat.Items.Add("dd.MM.yy");
                    f.ColumnFormat.Items.Add("dd MMMM yyyy");
                    f.ColumnFormat.Items.Add("dd MMMM yyyy (ddd)");
                    f.ColumnFormat.Items.Add("dd.MM.yyyy HH:mm:ss");
                    f.ColumnFormat.Items.Add("dd.MM.yyyy HH:mm");
                    f.ColumnFormat.Items.Add("HH:mm:ss");
                    f.ColumnFormat.Items.Add("yyyy-MM-dd");              // ISO
                    f.ColumnFormat.Items.Add("yyyy-MM-dd HH:mm:ss");     // ISO datetime
                    f.ColumnFormat.Items.Add("yyyyMMdd");
                    f.ColumnFormat.Items.Add("O");                       // round-trip (ISO 8601)
                    f.ColumnFormat.Items.Add("s");                       // sortable
                    break;

                case "Int16":
                case "Int32":
                case "Int64":
                    f.ColumnFormat.Items.Add("N0");   // 1 234
                    f.ColumnFormat.Items.Add("D");    // 1234
                    f.ColumnFormat.Items.Add("D8");   // 00001234
                    f.ColumnFormat.Items.Add("#,0");
                    f.ColumnFormat.Items.Add("#,0;(#,0)"); // отрицательные в скобках
                    break;

                case "Decimal":
                case "Double":
                case "Single":
                    f.ColumnFormat.Items.Add("N2");   // 1 234.56
                    f.ColumnFormat.Items.Add("N3");
                    f.ColumnFormat.Items.Add("F2");   // фиксированное
                    f.ColumnFormat.Items.Add("F4");
                    f.ColumnFormat.Items.Add("G");    // general
                    f.ColumnFormat.Items.Add("#,0.00");
                    f.ColumnFormat.Items.Add("#,0.###");
                    f.ColumnFormat.Items.Add("0.00%");
                    f.ColumnFormat.Items.Add("C2");   // валюта (локаль)
                    break;

                case "String":
                    f.ColumnFormat.Items.Add("");     // без формата
                    f.ColumnFormat.Items.Add("U");    // upper (если будешь обрабатывать)
                    f.ColumnFormat.Items.Add("L");    // lower
                    f.ColumnFormat.Items.Add("Trim");
                    break;

                case "Guid":
                    f.ColumnFormat.Items.Add("D"); // 32 digits separated by hyphens
                    f.ColumnFormat.Items.Add("N"); // 32 digits
                    f.ColumnFormat.Items.Add("B"); // {guid}
                    f.ColumnFormat.Items.Add("P"); // (guid)
                    break;
            }

            f.Show();
            f.RefreshFilterValues();
        }

        private async void RefreshFilterValues()
        {
            try
            {
                await Task.Run(() =>
                {

                    var set = new HashSet<object>();

                    foreach (DataGridViewRow row in SourceDataGridView.Rows)
                    {
                        var value = row.Cells[SourceColumn.Index].Value;
                        if (value is DateTime dt)
                        {
                            FilterValuesGridView.Columns[1].DefaultCellStyle.Format = "dd.MM.yyyy"; // для отображения дат без учета времени
                            value = dt.Date; // для фильтрации по датам без учета времени
                        }

                        if (value != null)
                            set.Add(value);
                    }

                    Values = new(set.ToArray().OrderBy(x => $"{x}").Select(x => new FilterRow(x)).ToArray());
                    if (FilterValuesGridView.InvokeRequired)
                    {
                        FilterValuesGridView.BeginInvoke(() => FilterValuesGridView.DataSource = Values);
                    }
                    else
                    {
                        FilterValuesGridView.DataSource = Values;
                    }
                });
                ValuesTotalCount = Values.Count;
                UpdateUI();
            }
            catch (Exception e)
            {
                throw; // TODO handle exception
            }
        }

        public void SetSelection(bool? @checked)
        {
            var grid = FilterValuesGridView;
            if (grid.SelectedCells.Count > 0)
            {
                var rows = grid.SelectedCells
                    .Cast<DataGridViewCell>()
                    .Select(x => x.RowIndex)
                    .Distinct()
                    .ToArray();

                foreach (var r in rows)
                {
                    var rowFilter = grid.Rows[r].DataBoundItem as FilterRow;
                    rowFilter.Checked = @checked == null ? !rowFilter.Checked : @checked.Value;
                }
            }
            else if (grid.CurrentRow != null)
            {
                var cell = grid.CurrentRow.DataBoundItem as FilterRow;
                bool current = cell.Checked is bool b && b;
                cell.Checked = @checked == null ? !current : @checked.Value;
            }
            UpdateUI();
        }

        private static ColumnFilterView GetInstance() => new ColumnFilterView();

        private void ButtonCancel_Click(object sender, System.EventArgs e)
        {
            ApplyFilters(true);
            RefreshFilterValues();
        }

        private void ButtonOk_Click(object sender, System.EventArgs e)
        {
            ApplyFilters();
            this.Close();
        }

        private void ApplyFilters(bool clear = false)
        {
            StringFilterBuilder.Clear();
            SourceDataGridView.SuspendLayout();
            var checkedValues = new HashSet<object>(Values.Where(x => x.Checked).Select(x => x.Value).ToArray());
            if (clear)
            {
                SourceDataGridView.ClearFilters();
                return;
            }

            SourceDataGridView.CurrentCell = null;
            var filterText = string.Empty;
            switch (tabControl1.SelectedIndex)
            {
                case 0:
                    if (checkedValues.Count > 0)
                        filterText = StringFilterBuilder.Property(SourceColumn.DataPropertyName)
                            .In(checkedValues.ToArray())
                            .ToString();
                    break;

                case 1:
                        filterText = valueFilterTemplate1.SelectedFilterFunc?.Invoke() ?? string.Empty;
                    break;
            }

            switch (SourceDataGridView.DataSource)
            {
                case IBindingListView bindingListView:
                    bindingListView.Filter = filterText;
                    break;
                case DataTable dt:
                    dt.DefaultView.RowFilter = filterText;
                    break;
                default:
                    foreach (DataGridViewRow r in SourceDataGridView.Rows)
                    {
                        if (r.IsNewRow)
                            continue;
                        var c = r.Cells[SourceColumn.Index];
                        try
                        {
                            r.Visible = checkedValues.Contains(c.Value);
                        }
                        catch (Exception ex)
                        {

                        }
                    }

                    break;
            }

            SourceDataGridView.ResumeLayout();
        }

        private void ChangeCheckedItesms(bool? checkValue)
        {
            foreach (FilterRow item in Values)
            {
                item.Checked = checkValue ?? !item.Checked;
            }

            UpdateUI();
        }

        private void ClearFilterTextButton_Click(object sender, System.EventArgs e)
        {
            FindValueText.Text = string.Empty;
            UpdateUI();
        }

        private void ColumnFilterView_Leave(object sender, System.EventArgs e)
        {
        }

        private void ColumnFormat_TextChanged(object sender, EventArgs e)
        {
            var prevFormat = SourceColumn.DefaultCellStyle.Format;
            try
            {
                SourceColumn.DefaultCellStyle.Format = ColumnFormat.Text;
            }
            catch
            {
                SourceColumn.DefaultCellStyle.Format = prevFormat;
            }
        }

        private void FilterTextBox_TextChanged(object sender, System.EventArgs e)
        {
            FilterValuesGridView.DataSource = Values;
            if (string.IsNullOrEmpty(FindValueText.Text))
            {
                Values.RemoveFilter();
                return;
            }
            var filterValues = FindValueText.Text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            Values.SetFilter((x, i) => filterValues.Any(f => x.Value.ToString().Trim().Contains(f, StringComparison.OrdinalIgnoreCase)));
            UpdateUI();
        }

        //private void FilterValuesGridView_CellClick(object sender, DataGridViewCellEventArgs e)
        //{
        //    if (e.ColumnIndex != 0 || e.RowIndex < 0)
        //        return;

        //    var grid = (DataGridView)sender;

        //    // получаем прямоугольник ячейки
        //    var cellRect = grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);

        //    // получаем позицию курсора относительно грида
        //    var mousePos = grid.PointToClient(Cursor.Position);

        //    // вычисляем область чекбокса (примерно по центру)
        //    var checkBoxSize = 16; // стандартный размер
        //    var checkBoxRect = new Rectangle(
        //        cellRect.X + (cellRect.Width - checkBoxSize) / 2,
        //        cellRect.Y + (cellRect.Height - checkBoxSize) / 2,
        //        checkBoxSize,
        //        checkBoxSize);
        //    UpdateUI();
        //    // если клик внутри чекбокса — ничего не делаем (он сам обработается)
        //    if (checkBoxRect.Contains(mousePos))
        //        return;

        //    // иначе — переключаем вручную
        //    var cell = grid.Rows[e.RowIndex].DataBoundItem as FilterRow;

        //    bool current = cell.Value is bool b && b;
        //    cell.Checked = !current;
        //    UpdateUI();
        //    grid.RefreshEdit();
        //}

        private void FilterValuesGridView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
        }

        private void toolStripMenuItemSelectAll_Click(object sender, System.EventArgs e)
        {
            ChangeCheckedItesms(true);
        }

        private void toolStripMenuItemSelectInverse_Click(object sender, System.EventArgs e)
        {
            ChangeCheckedItesms(null);
        }

        private void toolStripMenuItemSelectNone_Click(object sender, System.EventArgs e)
        {
            ChangeCheckedItesms(false);
        }

        private void UpdateUI()
        {
            toolStripStatusLabel1.Text = $"{Values.Count(x => x.Checked)} / {Values.Count} / {ValuesTotalCount}";
            //SourceDataGridView.Refresh();
        }

        public int ValuesTotalCount { get; private set; }

        private void выбратьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetSelection(true);
        }

        private void поменятьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetSelection(null);
        }

        private void убратьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetSelection(false);
        }

        internal class FilterRow : ObservableObjectEx
        {
            private bool @checked;
            private object @value;

            public FilterRow()
            { }

            public FilterRow(object value, bool @checked = false)
            {
                this.@value = value;
                this.@checked = @checked;
            }

            public bool Checked { get => @checked; set => Set(ref this.@checked, value); }
            public object Value { get => value; set => Set(ref this.@value, value); }
        }

        private void FilterValuesGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            SetSelection(null);
        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            ColumnCellLeftAligned = true;
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            ColumnCellJustifyAligned = true;
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            ColumnCellRightAligned = true;
        }

        private void btnFont_Click(object sender, EventArgs e)
        {
            CanClose = false;
            if (fontDialog1.ShowDialog() == DialogResult.OK)
            {
                SourceColumn.DefaultCellStyle.Font = fontDialog1.Font;
            }
            CanClose = true;
        }

        private void btnDecreaseSize_Click(object sender, EventArgs e)
        {
            SourceDataGridView.DecreaseFontSize();
        }

        private void btnIncreaseFont_Click(object sender, EventArgs e)
        {
            SourceDataGridView.IncreaseFontSize();
        }

        private void btnAutoResizeColumn_Click(object sender, EventArgs e)
        {
            SourceDataGridView.AutoResizeColumn(SourceColumn.Index);
        }

        private void FilterValuesGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            //if (FilterValuesGridView == null || e.RowIndex < 0 || e.RowIndex >= FilterValuesGridView.RowCount)
            //    return;
            //var row = FilterValuesGridView.Rows[e.RowIndex].DataBoundItem as FilterRow;
            //if (row == null)
            //    return;

            //row.Checked = (bool)FilterValuesGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
        }

        private void DragForm(object sender, MouseEventArgs e)
        {
            base.DragForm(sender, e);
        }

        private void FilterValuesGridView_MouseUp(object sender, MouseEventArgs e)
        {
            UpdateUI();
        }
    }
}