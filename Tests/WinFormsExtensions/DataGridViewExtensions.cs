namespace WinFormsExtensions
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Drawing;
    using System.Helpers;
    using System.Linq;
    using System.Windows.Forms;
    using System.Windows.Forms.Extensions;

    public static class DataGridViewExtensions
    {
        private static bool isInited = false;
        private static ConcurrentDictionary<int, DataGridViewExtender> gridExtenders = new ConcurrentDictionary<int, DataGridViewExtender>();

        static DataGridViewExtensions()
        {
            if (isInited)
            {
                return;
            }

            isInited = true;
        }

        public static DataGridViewColumn AddColumn<T>(this DataGridView grid, string fieldName, string headerText = null, string format = null)
        {
            return AddColumn(grid, fieldName, headerText ?? fieldName, typeof(T), format);
        }

        public static DataGridViewColumn AddColumn(this DataGridView grid, string fieldName, string headerText, Type valueType, string format)
        {
            var col = new DataGridViewColumn
            {
                Name = "col_" + fieldName,
                DataPropertyName = fieldName,
                HeaderText = headerText,
                CellTemplate = new DataGridViewTextBoxCell { ValueType = valueType, },
                DefaultCellStyle = new DataGridViewCellStyle { Format = format },
                ValueType = valueType,
            };
            grid.Columns.Add(col);
            return col;
        }

        public static DataGridView AddGantt(this DataGridView grid, DateTime dateFrom, DateTime dateTo, string fieldNameFrom, string fieldNameTo, string headerFormat = "dd/MM", string dataMember = null)
        {
            grid.CellFormatting -= Grid_CellFormatting;
            var days = DateTimeHelper.EachDay(dateFrom, dateTo).ToArray();

            foreach (var day in days)
            {
                var col = AddColumn<bool>(grid, $"GanttCol_{fieldNameFrom}_{fieldNameTo}_{dataMember}_{day:yyyyMMdd}", string.Format("{0:" + headerFormat + "}", day));
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader;
                col.Resizable = DataGridViewTriState.False;
                col.ReadOnly = true;
            }
            grid.CellFormatting += Grid_CellFormatting;
            return grid;
        }

        private static void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null)
            {
                return;
            }
            if (grid.Columns[e.ColumnIndex].DataPropertyName?.StartsWith("GanttCol_") == true)
            {
                var row = grid.Rows[e.RowIndex];
                if (row.IsNewRow)
                    return;
                try
                {
                    var fields = grid.Columns[e.ColumnIndex].DataPropertyName.Split('_').Skip(1).Take(4).ToArray();
                    var cellDate = Obj.ChangeType<DateTime?>(fields[3]);
                    var fromDate = row.Cells[fields[0]].Value as DateTime?;
                    var toDate = row.Cells[fields[1]].Value as DateTime?;
                    var ranges = new List<DateTimeHelper.DateRange>();
                    if (fromDate != null && toDate != null)
                    {
                        ranges.Add(new DateTimeHelper.DateRange(fromDate.Value, toDate.Value));
                    }
                    var cellValue = false;
                    var dataMember = fields[2];
                    if (!string.IsNullOrEmpty(dataMember))
                    {
                        var item = row.DataBoundItem;
                        var details = Obj.Get<IEnumerable<object>>(item, dataMember);
                        if (details != null && details.Count() > 0)
                        {
                            foreach (var d in details)
                            {
                                ranges.Add(new DateTimeHelper.DateRange(Obj.Get<DateTime>(d, fields[0]), Obj.Get<DateTime>(d, fields[1])));
                            }
                        }
                    }

                    if (cellDate != null && fromDate != null && toDate != null)
                    {
                        cellValue = cellDate.Value.Date >= fromDate.Value.Date && cellDate.Value.Date <= toDate.Value.Date;
                    }

                    if (!cellValue && ranges.Count > 1)
                    {
                        cellValue = ranges.Any(x => x.Contains(cellDate.Value));
                    }

                    if (cellValue)
                    {
                        e.CellStyle.BackColor = cellValue ? Color.Yellow : Color.White;
                        grid.InvalidateCell(e.ColumnIndex, e.RowIndex);
                        e.FormattingApplied = true;
                    }
                }
                catch
                {
                }
            }
        }

        public static DataGridView ClearFilters(this DataGridView grid)
        {
            switch (grid.DataSource)
            {
                case IBindingListView blv when blv.SupportsFiltering:
                    blv.RemoveFilter();
                    break;

                case BindingSource bs:
                    bs.RemoveFilter();
                    break;

                case DataView dv:
                    dv.RowFilter = string.Empty;
                    break;

                case DataTable dt:
                    dt.DefaultView.RowFilter = string.Empty;
                    break;

                default:
                    foreach (DataGridViewRow r in grid.Rows)
                    {
                        r.Visible = true;
                    }

                    break;
            }

            return grid;
        }

        public static DataGridView SetAutoCommitCheckCells(this DataGridView grid, bool autoCommitEnabled)
        {
            grid.CurrentCellDirtyStateChanged -= Grid_CurrentCellDirtyStateChanged;
            if (!autoCommitEnabled)
                return grid;
            grid.CurrentCellDirtyStateChanged += Grid_CurrentCellDirtyStateChanged;
            return grid;
        }

        private static void Grid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null) return;
            if (grid.CurrentCell is DataGridViewCheckBoxCell)
            {
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        public static DataGridView IncreaseFontSize(this DataGridView grid)
        {
            return ChangeFontSize(grid, 1f);
        }

        public static DataGridView DecreaseFontSize(this DataGridView grid)
        {
            return ChangeFontSize(grid, -1f);
        }

        public static DataGridView ChangeFontSize(this DataGridView grid, float delta)
        {
            foreach (DataGridViewColumn c in grid.Columns)
            {
                var f = c.DefaultCellStyle.Font ?? c.DataGridView?.Font;
                if (f == null)
                    return grid;
                c.DefaultCellStyle.Font = new Font(f.FontFamily, f.Size + delta, f.Style);
            }
            grid.AutoResizeRows();
            return grid;
        }

        public static DataGridView SetDoubleBuffered(this DataGridView grid, bool enabled)
        {
            Obj.Set(grid, "DoubleBuffered", enabled);
            return grid;
        }

        public static DataGridView SetRowsHeight(this DataGridView grid, int height)
        {
            grid.RowTemplate.Height = height;
            return grid;
        }

        public static DataGridView SetRowColors(this DataGridView grid, Color odd, Color? even = null)
        {
            grid.AlternatingRowsDefaultCellStyle.BackColor = odd;
            if (even != null)
                grid.RowsDefaultCellStyle.BackColor = even.Value;
            return grid;
        }

        public static DataGridView SetColumnMenu(this DataGridView grid, bool columnMenuEnabled)
        {
            grid.ColumnHeaderMouseClick -= Grid_ColumnHeaderMouseClick;
            if (!columnMenuEnabled)
            {
                return grid;
            }
            grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;
            return grid;
        }

        private static void Grid_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            ColumnFilterView.ShowForColumn((sender as DataGridView)?.Columns[e.ColumnIndex]);
        }

        public static DataGridView ShowRowNumbers(this DataGridView grid, bool rowNumbersVisible)
        {
            var ge = GetExtender(grid);
            ge.RowNumberVisible = rowNumbersVisible;
            return grid;
        }

        public static DataGridViewExtender GetExtender(this DataGridView grid)
        {
            return gridExtenders.GetOrAdd(grid.GetHashCode(), () => new DataGridViewExtender(grid));
        }

        private static Color bg = Color.FromArgb(100, Color.DimGray);

        private static void Grid_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null || grid.Rows[e.RowIndex].IsNewRow)
            {
                return;
            }
        }

        public static DataGridView BeginUpdate(this DataGridView grid, Action update)
        {
            var autoSizeColumns = grid.AutoSizeColumnsMode;
            var autoSizeRows = grid.AutoSizeRowsMode;

            grid.SuspendLayout();
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            var cols = new DataGridViewAutoSizeColumnMode[grid.Columns.Count];
            foreach (DataGridViewColumn c in grid.Columns)
            {
                cols[c.Index] = c.AutoSizeMode;
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.NotSet;
            }

            update();

            for (int i = 0; i < cols.Length; i++)
            {
                grid.Columns[i].AutoSizeMode = cols[i];
            }
            grid.AutoSizeColumnsMode = autoSizeColumns;
            grid.AutoSizeRowsMode = autoSizeRows;
            grid.ResumeLayout();
            return grid;
        }
    }
}