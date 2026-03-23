namespace WinFormsExtensions
{
    partial class ValueFilterTemplate
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            Presets = new System.Windows.Forms.DataGridView();
            PresetName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            FilterFunc = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)Presets).BeginInit();
            SuspendLayout();
            // 
            // Presets
            // 
            Presets.AllowUserToAddRows = false;
            Presets.AllowUserToDeleteRows = false;
            Presets.AllowUserToResizeColumns = false;
            Presets.AllowUserToResizeRows = false;
            Presets.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            Presets.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            Presets.ColumnHeadersVisible = false;
            Presets.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { PresetName, FilterFunc });
            Presets.Dock = System.Windows.Forms.DockStyle.Fill;
            Presets.Location = new System.Drawing.Point(0, 0);
            Presets.MultiSelect = false;
            Presets.Name = "Presets";
            Presets.ReadOnly = true;
            Presets.RowHeadersVisible = false;
            Presets.RowHeadersWidth = 10;
            Presets.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            Presets.RowTemplate.Height = 20;
            Presets.RowTemplate.ReadOnly = true;
            Presets.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            Presets.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            Presets.ShowCellErrors = false;
            Presets.ShowEditingIcon = false;
            Presets.ShowRowErrors = false;
            Presets.Size = new System.Drawing.Size(200, 232);
            Presets.TabIndex = 0;
            // 
            // PresetName
            // 
            PresetName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            PresetName.DataPropertyName = "PresetName";
            PresetName.HeaderText = "Preset Name";
            PresetName.Name = "PresetName";
            PresetName.ReadOnly = true;
            PresetName.Visible = false;
            // 
            // FilterFunc
            // 
            FilterFunc.DataPropertyName = "FilterText";
            FilterFunc.HeaderText = "Filter Func";
            FilterFunc.Name = "FilterFunc";
            FilterFunc.ReadOnly = true;
            FilterFunc.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            FilterFunc.Visible = false;
            FilterFunc.Width = 5;
            // 
            // ValueFilterTemplate
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(Presets);
            Name = "ValueFilterTemplate";
            Size = new System.Drawing.Size(200, 232);
            ((System.ComponentModel.ISupportInitialize)Presets).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.DataGridView Presets;
        private System.Windows.Forms.DataGridViewTextBoxColumn PresetName;
        private System.Windows.Forms.DataGridViewTextBoxColumn FilterFunc;
    }
}
