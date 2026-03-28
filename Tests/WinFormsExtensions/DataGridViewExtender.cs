using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using WinFormsExtensions;

namespace System.Windows.Forms.Extensions
{
    public class DataGridViewExtender : INotifyPropertyChanged
    {
        private DataGridView grid;
        private HashSet<(int Row, int Col)> selectedCells = new HashSet<(int, int)>();

        public DataGridViewExtender(DataGridView grid)
        {
            this.grid = grid;
            this.GridId = grid.GetFullName();
            grid.SetParam("Id", this.GridId);
            grid.RowPostPaint += Grid_RowPostPaint;
            grid.CurrentCellChanged += (s, e) => grid.Invalidate();
            grid.RowEnter += Grid_RowEnter;
            grid.RowLeave += Grid_RowLeave;
            grid.SelectionChanged += Grid_SelectionChanged;
            KeepSelectionOnRowChange = true;
        }

        private void SaveSelection()
        {
            if (!KeepSelectionOnRowChange)
                return;

            foreach (DataGridViewCell cell in grid.SelectedCells)
                selectedCells.Add((cell.RowIndex, cell.ColumnIndex));
        }

        private void Grid_RowLeave(object sender, DataGridViewCellEventArgs e)
        {
            SaveSelection();
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            RestoreSelection();
        }

        private void RestoreSelection()
        {
            if (!KeepSelectionOnRowChange)
                return;
            if (selectedCells != null && grid.CurrentCell != null && selectedCells.Any(x => x.Row == grid.CurrentCell.RowIndex && x.Col == grid.CurrentCell.ColumnIndex))
            {
                foreach (var (row, col) in selectedCells)
                {
                    if (row < grid.RowCount && col < grid.ColumnCount)
                        grid[col, row].Selected = true;
                }
            }
            else
            {
                selectedCells.Clear();
            }
        }

        private void Grid_RowEnter(object sender, DataGridViewCellEventArgs e)
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Color FocusedRowBackground { get; set; } = Color.FromArgb(125, Color.DeepSkyBlue);

        public Color FocusedRowBorderColor { get; set; } = Color.FromArgb(125, Color.Black);

        public int FocusedRowBorderThickness { get; set; } = 2;

        public bool FocusedRowBorderVisible { get; set; } = true;

        public string GridId { get; }

        public bool RowNumberVisible { get; set; } = true;

        public bool KeepSelectionOnRowChange { get; set; }

        private void OnKeepSelectionOnRowChangeChanged()
        {
            //grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            //grid.MultiSelect = true;
        }

        private void Grid_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (RowNumberVisible)
            {
                string rowNumber = (e.RowIndex + 1).ToString();
                var rowBounds = e.RowBounds;
                using (var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                })
                {
                    var bounds = new Rectangle(
                        rowBounds.Left,
                        rowBounds.Top,
                        grid.RowHeadersWidth,
                        rowBounds.Height);
                    e.Graphics.DrawString(rowNumber, grid.Font, SystemBrushes.ControlText, bounds, format);
                }
            }

            if (FocusedRowBorderVisible && grid.CurrentCell != null)
            {
                if (e.RowIndex == grid.CurrentCell.RowIndex)
                {
                    var rowBounds = new Rectangle(
                        grid.RowHeadersWidth,
                        e.RowBounds.Top,
                        grid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible) - grid.HorizontalScrollingOffset,
                        e.RowBounds.Height
                    );

                    using (Brush backBrush = new SolidBrush(FocusedRowBackground))
                    {
                        e.Graphics.FillRectangle(backBrush, rowBounds);
                    }

                    using (Pen pen = new Pen(FocusedRowBorderColor, FocusedRowBorderThickness))
                    {
                        // Немного уменьшаем, чтобы рамка не обрезалась
                        var borderRect = new Rectangle(
                            rowBounds.X,
                            rowBounds.Y,
                            rowBounds.Width - 1,
                            rowBounds.Height - 1
                        );

                        e.Graphics.DrawRectangle(pen, borderRect);
                    }
                }
            }
        }

        private void OnFocusedRowBorderVisibleChanged()
        {
        }

        private void OnRowNumberVisibleChanged()
        {
            if (RowNumberVisible)
            {
                if (grid.RowHeadersWidth < 50)
                    grid.RowHeadersWidth = 50;
            }
        }
    }
}