namespace WinFormsExtensions
{
    partial class ColumnFilterView
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ColumnFilterView));
            FindValueText = new System.Windows.Forms.TextBox();
            FilterValuesGridView = new System.Windows.Forms.DataGridView();
            Check = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            CheckColumnMenuStrip = new System.Windows.Forms.ContextMenuStrip(components);
            toolStripMenuItemSelectAll = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItemSelectNone = new System.Windows.Forms.ToolStripMenuItem();
            toolStripMenuItemSelectInverse = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            выбратьToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            убратьToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            поменятьToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            Value = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ClearFilterTextButton = new System.Windows.Forms.Button();
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            ColumnCaption = new System.Windows.Forms.ComboBox();
            ClearColumnCaptionButton = new System.Windows.Forms.Button();
            label3 = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            ClearFormatTextButton = new System.Windows.Forms.Button();
            ColumnFormat = new System.Windows.Forms.ComboBox();
            statusStrip1 = new System.Windows.Forms.StatusStrip();
            toolStripSplitButton1 = new System.Windows.Forms.ToolStripSplitButton();
            toolStripSplitButton2 = new System.Windows.Forms.ToolStripSplitButton();
            toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            panel1 = new System.Windows.Forms.Panel();
            tabControl1 = new System.Windows.Forms.TabControl();
            tabColumnValues = new System.Windows.Forms.TabPage();
            progressBar1 = new System.Windows.Forms.ProgressBar();
            panel2 = new System.Windows.Forms.Panel();
            label2 = new System.Windows.Forms.Label();
            tabPresets = new System.Windows.Forms.TabPage();
            valueFilterTemplate1 = new ValueFilterTemplate();
            tabConstructor = new System.Windows.Forms.TabPage();
            toolStrip1 = new System.Windows.Forms.ToolStrip();
            btnDecreaseSize = new System.Windows.Forms.ToolStripButton();
            btnFont = new System.Windows.Forms.ToolStripButton();
            btnIncreaseFont = new System.Windows.Forms.ToolStripButton();
            toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            btnLeftAlignment = new System.Windows.Forms.ToolStripButton();
            btnJustifyAlignment = new System.Windows.Forms.ToolStripButton();
            btnRightAlignment = new System.Windows.Forms.ToolStripButton();
            toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            btnAutosizeColumn = new System.Windows.Forms.ToolStripButton();
            btnFreezeColumn = new System.Windows.Forms.ToolStripButton();
            fontDialog1 = new System.Windows.Forms.FontDialog();
            colorDialog1 = new System.Windows.Forms.ColorDialog();
            panelRoot = new System.Windows.Forms.Panel();
            contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(components);
            ((System.ComponentModel.ISupportInitialize)FilterValuesGridView).BeginInit();
            CheckColumnMenuStrip.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            statusStrip1.SuspendLayout();
            panel1.SuspendLayout();
            tabControl1.SuspendLayout();
            tabColumnValues.SuspendLayout();
            panel2.SuspendLayout();
            tabPresets.SuspendLayout();
            toolStrip1.SuspendLayout();
            panelRoot.SuspendLayout();
            SuspendLayout();
            // 
            // FindValueText
            // 
            FindValueText.Dock = System.Windows.Forms.DockStyle.Fill;
            FindValueText.Location = new System.Drawing.Point(60, 0);
            FindValueText.Margin = new System.Windows.Forms.Padding(0);
            FindValueText.Name = "FindValueText";
            FindValueText.Size = new System.Drawing.Size(136, 23);
            FindValueText.TabIndex = 1;
            FindValueText.TextChanged += FilterTextBox_TextChanged;
            // 
            // FilterValuesGridView
            // 
            FilterValuesGridView.AllowUserToAddRows = false;
            FilterValuesGridView.AllowUserToDeleteRows = false;
            FilterValuesGridView.AllowUserToResizeRows = false;
            FilterValuesGridView.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            FilterValuesGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Segoe UI", 9F);
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            FilterValuesGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            FilterValuesGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            FilterValuesGridView.ColumnHeadersVisible = false;
            FilterValuesGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { Check, Value });
            FilterValuesGridView.Location = new System.Drawing.Point(0, 27);
            FilterValuesGridView.Margin = new System.Windows.Forms.Padding(0, 2, 0, 0);
            FilterValuesGridView.Name = "FilterValuesGridView";
            FilterValuesGridView.RowHeadersVisible = false;
            FilterValuesGridView.RowTemplate.Height = 20;
            FilterValuesGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            FilterValuesGridView.ShowEditingIcon = false;
            FilterValuesGridView.ShowRowErrors = false;
            FilterValuesGridView.Size = new System.Drawing.Size(214, 196);
            FilterValuesGridView.TabIndex = 2;
            FilterValuesGridView.CellDoubleClick += FilterValuesGridView_CellDoubleClick;
            FilterValuesGridView.ColumnHeaderMouseClick += FilterValuesGridView_ColumnHeaderMouseClick;
            FilterValuesGridView.MouseUp += FilterValuesGridView_MouseUp;
            // 
            // Check
            // 
            Check.ContextMenuStrip = CheckColumnMenuStrip;
            Check.DataPropertyName = "Checked";
            Check.FillWeight = 30.456852F;
            Check.FlatStyle = System.Windows.Forms.FlatStyle.System;
            Check.HeaderText = "";
            Check.Name = "Check";
            Check.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            Check.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            Check.Width = 25;
            // 
            // CheckColumnMenuStrip
            // 
            CheckColumnMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { toolStripMenuItemSelectAll, toolStripMenuItemSelectNone, toolStripMenuItemSelectInverse, toolStripSeparator1, выбратьToolStripMenuItem, убратьToolStripMenuItem, поменятьToolStripMenuItem });
            CheckColumnMenuStrip.Name = "CheckColumnMenuStrip";
            CheckColumnMenuStrip.Size = new System.Drawing.Size(151, 142);
            // 
            // toolStripMenuItemSelectAll
            // 
            toolStripMenuItemSelectAll.Name = "toolStripMenuItemSelectAll";
            toolStripMenuItemSelectAll.Size = new System.Drawing.Size(150, 22);
            toolStripMenuItemSelectAll.Text = "Выбрать все";
            toolStripMenuItemSelectAll.Click += toolStripMenuItemSelectAll_Click;
            // 
            // toolStripMenuItemSelectNone
            // 
            toolStripMenuItemSelectNone.Name = "toolStripMenuItemSelectNone";
            toolStripMenuItemSelectNone.Size = new System.Drawing.Size(150, 22);
            toolStripMenuItemSelectNone.Text = "Убрать все";
            toolStripMenuItemSelectNone.Click += toolStripMenuItemSelectNone_Click;
            // 
            // toolStripMenuItemSelectInverse
            // 
            toolStripMenuItemSelectInverse.Name = "toolStripMenuItemSelectInverse";
            toolStripMenuItemSelectInverse.Size = new System.Drawing.Size(150, 22);
            toolStripMenuItemSelectInverse.Text = "Поменять все";
            toolStripMenuItemSelectInverse.Click += toolStripMenuItemSelectInverse_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new System.Drawing.Size(147, 6);
            // 
            // выбратьToolStripMenuItem
            // 
            выбратьToolStripMenuItem.Name = "выбратьToolStripMenuItem";
            выбратьToolStripMenuItem.Size = new System.Drawing.Size(150, 22);
            выбратьToolStripMenuItem.Text = "Выбрать";
            выбратьToolStripMenuItem.Click += выбратьToolStripMenuItem_Click;
            // 
            // убратьToolStripMenuItem
            // 
            убратьToolStripMenuItem.Name = "убратьToolStripMenuItem";
            убратьToolStripMenuItem.Size = new System.Drawing.Size(150, 22);
            убратьToolStripMenuItem.Text = "Убрать";
            убратьToolStripMenuItem.Click += убратьToolStripMenuItem_Click;
            // 
            // поменятьToolStripMenuItem
            // 
            поменятьToolStripMenuItem.Name = "поменятьToolStripMenuItem";
            поменятьToolStripMenuItem.Size = new System.Drawing.Size(150, 22);
            поменятьToolStripMenuItem.Text = "Поменять";
            поменятьToolStripMenuItem.Click += поменятьToolStripMenuItem_Click;
            // 
            // Value
            // 
            Value.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            Value.ContextMenuStrip = CheckColumnMenuStrip;
            Value.DataPropertyName = "Value";
            Value.FillWeight = 169.543152F;
            Value.HeaderText = "Значения";
            Value.Name = "Value";
            Value.ReadOnly = true;
            // 
            // ClearFilterTextButton
            // 
            ClearFilterTextButton.Dock = System.Windows.Forms.DockStyle.Right;
            ClearFilterTextButton.FlatAppearance.BorderSize = 0;
            ClearFilterTextButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            ClearFilterTextButton.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
            ClearFilterTextButton.Location = new System.Drawing.Point(196, 0);
            ClearFilterTextButton.Margin = new System.Windows.Forms.Padding(0);
            ClearFilterTextButton.Name = "ClearFilterTextButton";
            ClearFilterTextButton.Size = new System.Drawing.Size(18, 25);
            ClearFilterTextButton.TabIndex = 3;
            ClearFilterTextButton.Text = "X";
            ClearFilterTextButton.UseVisualStyleBackColor = true;
            ClearFilterTextButton.Click += ClearFilterTextButton_Click;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Inset;
            tableLayoutPanel1.ColumnCount = 3;
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            tableLayoutPanel1.Controls.Add(ColumnCaption, 1, 0);
            tableLayoutPanel1.Controls.Add(ClearColumnCaptionButton, 2, 0);
            tableLayoutPanel1.Controls.Add(label3, 0, 0);
            tableLayoutPanel1.Controls.Add(label1, 0, 2);
            tableLayoutPanel1.Controls.Add(ClearFormatTextButton, 2, 2);
            tableLayoutPanel1.Controls.Add(ColumnFormat, 1, 2);
            tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 4;
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel1.Size = new System.Drawing.Size(222, 54);
            tableLayoutPanel1.TabIndex = 4;
            tableLayoutPanel1.Paint += tableLayoutPanel1_Paint;
            // 
            // ColumnCaption
            // 
            ColumnCaption.Dock = System.Windows.Forms.DockStyle.Fill;
            ColumnCaption.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            ColumnCaption.FormattingEnabled = true;
            ColumnCaption.Location = new System.Drawing.Point(69, 2);
            ColumnCaption.Margin = new System.Windows.Forms.Padding(0);
            ColumnCaption.Name = "ColumnCaption";
            ColumnCaption.Size = new System.Drawing.Size(131, 23);
            ColumnCaption.TabIndex = 12;
            // 
            // ClearColumnCaptionButton
            // 
            ClearColumnCaptionButton.Dock = System.Windows.Forms.DockStyle.Fill;
            ClearColumnCaptionButton.FlatAppearance.BorderSize = 0;
            ClearColumnCaptionButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            ClearColumnCaptionButton.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
            ClearColumnCaptionButton.Location = new System.Drawing.Point(202, 2);
            ClearColumnCaptionButton.Margin = new System.Windows.Forms.Padding(0);
            ClearColumnCaptionButton.Name = "ClearColumnCaptionButton";
            ClearColumnCaptionButton.Size = new System.Drawing.Size(18, 23);
            ClearColumnCaptionButton.TabIndex = 11;
            ClearColumnCaptionButton.Text = "X";
            ClearColumnCaptionButton.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Dock = System.Windows.Forms.DockStyle.Fill;
            label3.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            label3.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            label3.Location = new System.Drawing.Point(2, 2);
            label3.Margin = new System.Windows.Forms.Padding(0);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(65, 23);
            label3.TabIndex = 9;
            label3.Text = "Заголовок";
            label3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            label3.MouseDown += DragForm;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Dock = System.Windows.Forms.DockStyle.Fill;
            label1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            label1.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            label1.Location = new System.Drawing.Point(2, 29);
            label1.Margin = new System.Windows.Forms.Padding(0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(65, 23);
            label1.TabIndex = 6;
            label1.Text = "Формат";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            label1.MouseDown += DragForm;
            // 
            // ClearFormatTextButton
            // 
            ClearFormatTextButton.Dock = System.Windows.Forms.DockStyle.Fill;
            ClearFormatTextButton.FlatAppearance.BorderSize = 0;
            ClearFormatTextButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            ClearFormatTextButton.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 204);
            ClearFormatTextButton.Location = new System.Drawing.Point(202, 29);
            ClearFormatTextButton.Margin = new System.Windows.Forms.Padding(0);
            ClearFormatTextButton.Name = "ClearFormatTextButton";
            ClearFormatTextButton.Size = new System.Drawing.Size(18, 23);
            ClearFormatTextButton.TabIndex = 7;
            ClearFormatTextButton.Text = "X";
            ClearFormatTextButton.UseVisualStyleBackColor = true;
            // 
            // ColumnFormat
            // 
            ColumnFormat.Dock = System.Windows.Forms.DockStyle.Fill;
            ColumnFormat.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            ColumnFormat.FormattingEnabled = true;
            ColumnFormat.Location = new System.Drawing.Point(69, 29);
            ColumnFormat.Margin = new System.Windows.Forms.Padding(0);
            ColumnFormat.Name = "ColumnFormat";
            ColumnFormat.Size = new System.Drawing.Size(131, 23);
            ColumnFormat.TabIndex = 6;
            ColumnFormat.TextChanged += ColumnFormat_TextChanged;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { toolStripSplitButton1, toolStripSplitButton2, toolStripStatusLabel1 });
            statusStrip1.Location = new System.Drawing.Point(0, 330);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new System.Drawing.Size(222, 22);
            statusStrip1.SizingGrip = false;
            statusStrip1.TabIndex = 5;
            statusStrip1.Text = "statusStrip1";
            statusStrip1.MouseDown += DragForm;
            // 
            // toolStripSplitButton1
            // 
            toolStripSplitButton1.DropDownButtonWidth = 0;
            toolStripSplitButton1.Image = (System.Drawing.Image)resources.GetObject("toolStripSplitButton1.Image");
            toolStripSplitButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            toolStripSplitButton1.Name = "toolStripSplitButton1";
            toolStripSplitButton1.Size = new System.Drawing.Size(44, 20);
            toolStripSplitButton1.Text = "OK";
            toolStripSplitButton1.ButtonClick += ButtonOk_Click;
            // 
            // toolStripSplitButton2
            // 
            toolStripSplitButton2.DropDownButtonWidth = 0;
            toolStripSplitButton2.Image = (System.Drawing.Image)resources.GetObject("toolStripSplitButton2.Image");
            toolStripSplitButton2.ImageTransparentColor = System.Drawing.Color.Magenta;
            toolStripSplitButton2.Name = "toolStripSplitButton2";
            toolStripSplitButton2.Size = new System.Drawing.Size(80, 20);
            toolStripSplitButton2.Text = "Очистить";
            toolStripSplitButton2.ButtonClick += ButtonCancel_Click;
            // 
            // toolStripStatusLabel1
            // 
            toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            toolStripStatusLabel1.Size = new System.Drawing.Size(83, 17);
            toolStripStatusLabel1.Spring = true;
            toolStripStatusLabel1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            toolStripStatusLabel1.ToolTipText = "Выбрано / Отфильтровано / Всего";
            toolStripStatusLabel1.MouseDown += DragForm;
            // 
            // panel1
            // 
            panel1.Controls.Add(tableLayoutPanel1);
            panel1.Dock = System.Windows.Forms.DockStyle.Top;
            panel1.Location = new System.Drawing.Point(0, 25);
            panel1.Name = "panel1";
            panel1.Size = new System.Drawing.Size(222, 54);
            panel1.TabIndex = 6;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabColumnValues);
            tabControl1.Controls.Add(tabPresets);
            tabControl1.Controls.Add(tabConstructor);
            tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            tabControl1.Location = new System.Drawing.Point(0, 79);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new System.Drawing.Size(222, 251);
            tabControl1.TabIndex = 7;
            // 
            // tabColumnValues
            // 
            tabColumnValues.Controls.Add(progressBar1);
            tabColumnValues.Controls.Add(FilterValuesGridView);
            tabColumnValues.Controls.Add(panel2);
            tabColumnValues.Location = new System.Drawing.Point(4, 24);
            tabColumnValues.Margin = new System.Windows.Forms.Padding(0);
            tabColumnValues.Name = "tabColumnValues";
            tabColumnValues.Size = new System.Drawing.Size(214, 223);
            tabColumnValues.TabIndex = 1;
            tabColumnValues.Text = "Значения";
            tabColumnValues.UseVisualStyleBackColor = true;
            // 
            // progressBar1
            // 
            progressBar1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            progressBar1.Location = new System.Drawing.Point(57, 114);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new System.Drawing.Size(100, 23);
            progressBar1.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            progressBar1.TabIndex = 5;
            progressBar1.Visible = false;
            // 
            // panel2
            // 
            panel2.Controls.Add(FindValueText);
            panel2.Controls.Add(label2);
            panel2.Controls.Add(ClearFilterTextButton);
            panel2.Dock = System.Windows.Forms.DockStyle.Top;
            panel2.Location = new System.Drawing.Point(0, 0);
            panel2.Margin = new System.Windows.Forms.Padding(0, 0, 0, 2);
            panel2.Name = "panel2";
            panel2.Size = new System.Drawing.Size(214, 25);
            panel2.TabIndex = 4;
            // 
            // label2
            // 
            label2.Dock = System.Windows.Forms.DockStyle.Left;
            label2.Location = new System.Drawing.Point(0, 0);
            label2.Margin = new System.Windows.Forms.Padding(0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(60, 25);
            label2.TabIndex = 9;
            label2.Text = "Поиск";
            label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            label2.MouseDown += DragForm;
            // 
            // tabPresets
            // 
            tabPresets.Controls.Add(valueFilterTemplate1);
            tabPresets.Location = new System.Drawing.Point(4, 24);
            tabPresets.Name = "tabPresets";
            tabPresets.Padding = new System.Windows.Forms.Padding(3);
            tabPresets.Size = new System.Drawing.Size(214, 223);
            tabPresets.TabIndex = 0;
            tabPresets.Text = "Шаблоны";
            tabPresets.UseVisualStyleBackColor = true;
            // 
            // valueFilterTemplate1
            // 
            valueFilterTemplate1.Dock = System.Windows.Forms.DockStyle.Fill;
            valueFilterTemplate1.FieldName = null;
            valueFilterTemplate1.Location = new System.Drawing.Point(3, 3);
            valueFilterTemplate1.Name = "valueFilterTemplate1";
            valueFilterTemplate1.Size = new System.Drawing.Size(208, 217);
            valueFilterTemplate1.TabIndex = 0;
            valueFilterTemplate1.ValueType = null;
            // 
            // tabConstructor
            // 
            tabConstructor.Location = new System.Drawing.Point(4, 24);
            tabConstructor.Name = "tabConstructor";
            tabConstructor.Size = new System.Drawing.Size(214, 223);
            tabConstructor.TabIndex = 2;
            tabConstructor.Text = "Конструктор";
            tabConstructor.UseVisualStyleBackColor = true;
            // 
            // toolStrip1
            // 
            toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { btnDecreaseSize, btnFont, btnIncreaseFont, toolStripSeparator2, btnLeftAlignment, btnJustifyAlignment, btnRightAlignment, toolStripSeparator3, btnAutosizeColumn, btnFreezeColumn });
            toolStrip1.Location = new System.Drawing.Point(0, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new System.Drawing.Size(222, 25);
            toolStrip1.TabIndex = 8;
            toolStrip1.Text = "toolStrip1";
            toolStrip1.MouseDown += DragForm;
            // 
            // btnDecreaseSize
            // 
            btnDecreaseSize.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            btnDecreaseSize.Image = (System.Drawing.Image)resources.GetObject("btnDecreaseSize.Image");
            btnDecreaseSize.ImageTransparentColor = System.Drawing.Color.Magenta;
            btnDecreaseSize.Name = "btnDecreaseSize";
            btnDecreaseSize.Size = new System.Drawing.Size(23, 22);
            btnDecreaseSize.Text = "Уменьшить размер шрифта";
            btnDecreaseSize.ToolTipText = "Уменьшить размер шрифта";
            btnDecreaseSize.Click += btnDecreaseSize_Click;
            // 
            // btnFont
            // 
            btnFont.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            btnFont.Image = (System.Drawing.Image)resources.GetObject("btnFont.Image");
            btnFont.ImageTransparentColor = System.Drawing.Color.Magenta;
            btnFont.Name = "btnFont";
            btnFont.Size = new System.Drawing.Size(23, 22);
            btnFont.Text = "Выбрать шрифт";
            btnFont.ToolTipText = "Выбрать шрифт";
            btnFont.Click += btnFont_Click;
            // 
            // btnIncreaseFont
            // 
            btnIncreaseFont.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            btnIncreaseFont.Image = (System.Drawing.Image)resources.GetObject("btnIncreaseFont.Image");
            btnIncreaseFont.ImageTransparentColor = System.Drawing.Color.Magenta;
            btnIncreaseFont.Name = "btnIncreaseFont";
            btnIncreaseFont.Size = new System.Drawing.Size(23, 22);
            btnIncreaseFont.Text = "Увеличить размер шрифта";
            btnIncreaseFont.Click += btnIncreaseFont_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
            // 
            // btnLeftAlignment
            // 
            btnLeftAlignment.CheckOnClick = true;
            btnLeftAlignment.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            btnLeftAlignment.Image = (System.Drawing.Image)resources.GetObject("btnLeftAlignment.Image");
            btnLeftAlignment.ImageTransparentColor = System.Drawing.Color.Magenta;
            btnLeftAlignment.MergeIndex = 0;
            btnLeftAlignment.Name = "btnLeftAlignment";
            btnLeftAlignment.Size = new System.Drawing.Size(23, 22);
            btnLeftAlignment.Text = "Выравнивание по левому краю";
            btnLeftAlignment.Click += toolStripButton1_Click;
            // 
            // btnJustifyAlignment
            // 
            btnJustifyAlignment.CheckOnClick = true;
            btnJustifyAlignment.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            btnJustifyAlignment.Image = (System.Drawing.Image)resources.GetObject("btnJustifyAlignment.Image");
            btnJustifyAlignment.ImageTransparentColor = System.Drawing.Color.Magenta;
            btnJustifyAlignment.MergeIndex = 0;
            btnJustifyAlignment.Name = "btnJustifyAlignment";
            btnJustifyAlignment.Size = new System.Drawing.Size(23, 22);
            btnJustifyAlignment.Text = "Выравнивание по центру";
            btnJustifyAlignment.Click += toolStripButton2_Click;
            // 
            // btnRightAlignment
            // 
            btnRightAlignment.CheckOnClick = true;
            btnRightAlignment.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            btnRightAlignment.Image = (System.Drawing.Image)resources.GetObject("btnRightAlignment.Image");
            btnRightAlignment.ImageTransparentColor = System.Drawing.Color.Magenta;
            btnRightAlignment.MergeIndex = 0;
            btnRightAlignment.Name = "btnRightAlignment";
            btnRightAlignment.Size = new System.Drawing.Size(23, 22);
            btnRightAlignment.Text = "Выравнивание по правому краю";
            btnRightAlignment.Click += toolStripButton3_Click;
            // 
            // toolStripSeparator3
            // 
            toolStripSeparator3.Name = "toolStripSeparator3";
            toolStripSeparator3.Size = new System.Drawing.Size(6, 25);
            // 
            // btnAutosizeColumn
            // 
            btnAutosizeColumn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            btnAutosizeColumn.Image = (System.Drawing.Image)resources.GetObject("btnAutosizeColumn.Image");
            btnAutosizeColumn.ImageTransparentColor = System.Drawing.Color.Magenta;
            btnAutosizeColumn.Name = "btnAutosizeColumn";
            btnAutosizeColumn.Size = new System.Drawing.Size(23, 22);
            btnAutosizeColumn.Text = "Подобрать ширину колонки по содержимому";
            btnAutosizeColumn.Click += btnAutoResizeColumn_Click;
            // 
            // btnFreezeColumn
            // 
            btnFreezeColumn.CheckOnClick = true;
            btnFreezeColumn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            btnFreezeColumn.Image = (System.Drawing.Image)resources.GetObject("btnFreezeColumn.Image");
            btnFreezeColumn.ImageTransparentColor = System.Drawing.Color.Magenta;
            btnFreezeColumn.Name = "btnFreezeColumn";
            btnFreezeColumn.Size = new System.Drawing.Size(23, 22);
            btnFreezeColumn.Text = "Закрепить колонку слева";
            // 
            // panelRoot
            // 
            panelRoot.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panelRoot.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panelRoot.Controls.Add(tabControl1);
            panelRoot.Controls.Add(panel1);
            panelRoot.Controls.Add(toolStrip1);
            panelRoot.Controls.Add(statusStrip1);
            panelRoot.Location = new System.Drawing.Point(3, 3);
            panelRoot.Name = "panelRoot";
            panelRoot.Size = new System.Drawing.Size(224, 354);
            panelRoot.TabIndex = 9;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new System.Drawing.Size(61, 4);
            // 
            // ColumnFilterView
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(230, 360);
            Controls.Add(panelRoot);
            DoubleBuffered = true;
            MinimumSize = new System.Drawing.Size(200, 108);
            Name = "ColumnFilterView";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            Text = "ColumnFilterView";
            Leave += ColumnFilterView_Leave;
            ((System.ComponentModel.ISupportInitialize)FilterValuesGridView).EndInit();
            CheckColumnMenuStrip.ResumeLayout(false);
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            panel1.ResumeLayout(false);
            tabControl1.ResumeLayout(false);
            tabColumnValues.ResumeLayout(false);
            panel2.ResumeLayout(false);
            panel2.PerformLayout();
            tabPresets.ResumeLayout(false);
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            panelRoot.ResumeLayout(false);
            panelRoot.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.TextBox FindValueText;
        private System.Windows.Forms.DataGridView FilterValuesGridView;
        private System.Windows.Forms.Button ClearFilterTextButton;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.ContextMenuStrip CheckColumnMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemSelectNone;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemSelectAll;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemSelectInverse;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem выбратьToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem убратьToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem поменятьToolStripMenuItem;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripSplitButton toolStripSplitButton1;
        private System.Windows.Forms.ToolStripSplitButton toolStripSplitButton2;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.ComboBox ColumnFormat;
        private System.Windows.Forms.Button ClearFormatTextButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button ClearColumnCaptionButton;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.DataGridViewCheckBoxColumn Check;
        private System.Windows.Forms.DataGridViewTextBoxColumn Value;
        private System.Windows.Forms.ComboBox ColumnCaption;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPresets;
        private System.Windows.Forms.TabPage tabColumnValues;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnFont;
        private System.Windows.Forms.ToolStripButton btnLeftAlignment;
        private System.Windows.Forms.ToolStripButton btnJustifyAlignment;
        private System.Windows.Forms.ToolStripButton btnRightAlignment;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.FontDialog fontDialog1;
        private System.Windows.Forms.ColorDialog colorDialog1;
        private System.Windows.Forms.ToolStripButton btnDecreaseSize;
        private System.Windows.Forms.ToolStripButton btnIncreaseFont;
        private System.Windows.Forms.Panel panelRoot;
        private System.Windows.Forms.ToolStripButton btnAutosizeColumn;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.TabPage tabConstructor;
        private System.Windows.Forms.ToolStripButton btnFreezeColumn;
        private System.Windows.Forms.ProgressBar progressBar1;
        private ValueFilterTemplate valueFilterTemplate1;
    }
}