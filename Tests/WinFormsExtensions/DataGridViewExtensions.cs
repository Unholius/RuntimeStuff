namespace WinFormsExtensions
{
    using RuntimeStuff;
    using RuntimeStuff.Helpers;
    using System;
    using System.ComponentModel;
    using System.Data;
    using System.Drawing;
    using System.Reflection;
    using System.Windows.Forms;

    public static class DataGridViewExtensions
    {
        private static bool isInited = false;
        private static MemberCache DataGridViewType;
        private static MemberCache DataGridViewRowPostPaintEventArgsType;
        private static MemberCache RowIndexProperty;
        private static MemberCache GraphicsProperty;
        private static MemberCache RowBoundsProperty;
        private static MemberCache RowHeadersWidthProperty;
        private static MemberCache FontProperty;
        private static MethodInfo DrawStringMethod;

        static DataGridViewExtensions()
        {
            if (isInited)
            {
                return;
            }

            DataGridViewType = MemberCache.Create(typeof(DataGridView));
            DataGridViewRowPostPaintEventArgsType = MemberCache.Create(typeof(DataGridViewRowPostPaintEventArgs));
            RowIndexProperty = DataGridViewRowPostPaintEventArgsType["RowIndex"];
            GraphicsProperty = DataGridViewRowPostPaintEventArgsType["Graphics"];
            RowBoundsProperty = DataGridViewRowPostPaintEventArgsType["RowBounds"];
            RowHeadersWidthProperty = DataGridViewType["RowHeadersWidth"];
            FontProperty = DataGridViewType["Font"];
            isInited = true;
        }

        public static DataGridView AddColumn<T>(this DataGridView grid, string fieldName, string headerText = null, string format = null)
        {
            return AddColumn(grid, fieldName, headerText ?? fieldName, typeof(T), format);
        }

        public static DataGridView AddColumn(this DataGridView grid, string fieldName, string headerText, Type valueType, string format)
        {
            var col = new DataGridViewColumn
            {
                Name = "col_"+fieldName,
                DataPropertyName = fieldName,
                HeaderText = headerText,
                CellTemplate = new DataGridViewTextBoxCell { ValueType = valueType,  },
                DefaultCellStyle = new DataGridViewCellStyle { Format = format }
            };
            grid.Columns.Add(col);
            return grid;
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

        public static DataGridView AutoCommitCheckCells(this DataGridView grid)
        {
            grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (grid.CurrentCell is DataGridViewCheckBoxCell)
                {
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            return grid;
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

        public static DataGridView ShowRowNumbers(this DataGridView grid)
        {
            EventHelper.BindEventToAction(grid, "RowPostPaint", Grid_RowPostPaint);
            //grid.RowPostPaint -= Grid_RowPostPaint;
            //grid.RowPostPaint += Grid_RowPostPaint;

            if (grid.RowHeadersWidth < 50)
                grid.RowHeadersWidth = 50;

            return grid;
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

            for (int i=0; i<cols.Length; i++)
            {
                grid.Columns[i].AutoSizeMode = cols[i];
            }
            grid.AutoSizeColumnsMode = autoSizeColumns;
            grid.AutoSizeRowsMode = autoSizeRows;
            grid.ResumeLayout();
            return grid;
        }

        private static void Grid_RowPostPaint(object sender, EventArgs e)
        {
            string rowNumber = ((int)RowIndexProperty.Getter(e) + 1).ToString();
            var graphics = (Graphics)GraphicsProperty.Getter(e);
            var rowBounds = (Rectangle)RowBoundsProperty.Getter(e);
            using (var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            })
            {
                var bounds = new Rectangle(
                    rowBounds.Left,
                    rowBounds.Top,
                    (int)RowHeadersWidthProperty.Getter(sender),
                    rowBounds.Height);
                try
                {
                    graphics.DrawString(rowNumber, FontProperty.GetValue<Font>(sender), SystemBrushes.ControlText, bounds, format);
                } catch (Exception ex)
                {

                }
            }
        }
    }
}
