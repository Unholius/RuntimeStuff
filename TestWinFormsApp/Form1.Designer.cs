namespace TestWinFormsApp
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            btnMemberCacheAllMembers = new Button();
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            flowLayoutPanel1 = new FlowLayoutPanel();
            propertyGrid1 = new PropertyGrid();
            textBox1 = new TextBox();
            checkBox1 = new CheckBox();
            textBox2 = new TextBox();
            label1 = new Label();
            tabPage2 = new TabPage();
            dgv = new DataGridView();
            btnLoad = new Button();
            tabPage3 = new TabPage();
            chkOffline = new CheckBox();
            btnOpenForm2 = new Button();
            btnStopServer = new Button();
            btnStart = new Button();
            btnSendMessage = new Button();
            listBox1 = new ListBox();
            tabPage4 = new TabPage();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            flowLayoutPanel1.SuspendLayout();
            tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgv).BeginInit();
            tabPage3.SuspendLayout();
            SuspendLayout();
            // 
            // btnMemberCacheAllMembers
            // 
            btnMemberCacheAllMembers.AutoSize = true;
            btnMemberCacheAllMembers.Location = new Point(3, 400);
            btnMemberCacheAllMembers.Name = "btnMemberCacheAllMembers";
            btnMemberCacheAllMembers.Size = new Size(159, 25);
            btnMemberCacheAllMembers.TabIndex = 0;
            btnMemberCacheAllMembers.Text = "MemberCacheAllMembers";
            btnMemberCacheAllMembers.UseVisualStyleBackColor = true;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Controls.Add(tabPage3);
            tabControl1.Controls.Add(tabPage4);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1245, 655);
            tabControl1.TabIndex = 1;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(flowLayoutPanel1);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(1237, 627);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "tabPage1";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Controls.Add(propertyGrid1);
            flowLayoutPanel1.Controls.Add(btnMemberCacheAllMembers);
            flowLayoutPanel1.Controls.Add(textBox1);
            flowLayoutPanel1.Controls.Add(checkBox1);
            flowLayoutPanel1.Controls.Add(textBox2);
            flowLayoutPanel1.Controls.Add(label1);
            flowLayoutPanel1.Dock = DockStyle.Fill;
            flowLayoutPanel1.FlowDirection = FlowDirection.TopDown;
            flowLayoutPanel1.Location = new Point(3, 3);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(1231, 621);
            flowLayoutPanel1.TabIndex = 0;
            // 
            // propertyGrid1
            // 
            propertyGrid1.Location = new Point(3, 3);
            propertyGrid1.Name = "propertyGrid1";
            propertyGrid1.Size = new Size(295, 391);
            propertyGrid1.TabIndex = 2;
            // 
            // textBox1
            // 
            textBox1.Location = new Point(3, 431);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(100, 23);
            textBox1.TabIndex = 1;
            textBox1.TextChanged += textBox1_TextChanged;
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Location = new Point(3, 460);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new Size(70, 19);
            checkBox1.TabIndex = 3;
            checkBox1.Text = "Четное?";
            checkBox1.UseVisualStyleBackColor = true;
            // 
            // textBox2
            // 
            textBox2.Location = new Point(3, 485);
            textBox2.Name = "textBox2";
            textBox2.Size = new Size(100, 23);
            textBox2.TabIndex = 4;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(3, 511);
            label1.Name = "label1";
            label1.Size = new Size(38, 15);
            label1.TabIndex = 5;
            label1.Text = "label1";
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(dgv);
            tabPage2.Controls.Add(btnLoad);
            tabPage2.Location = new Point(4, 24);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(1237, 627);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "tabPage2";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // dgv
            // 
            dataGridViewCellStyle1.BackColor = Color.FromArgb(255, 255, 192);
            dgv.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgv.Dock = DockStyle.Fill;
            dgv.Location = new Point(3, 26);
            dgv.Name = "dgv";
            dgv.Size = new Size(1231, 598);
            dgv.TabIndex = 2;
            dgv.ColumnHeaderMouseClick += dgv_ColumnHeaderMouseClick;
            // 
            // btnLoad
            // 
            btnLoad.Dock = DockStyle.Top;
            btnLoad.Location = new Point(3, 3);
            btnLoad.Name = "btnLoad";
            btnLoad.Size = new Size(1231, 23);
            btnLoad.TabIndex = 1;
            btnLoad.Text = "Load";
            btnLoad.UseVisualStyleBackColor = true;
            btnLoad.Click += btnLoad_Click;
            // 
            // tabPage3
            // 
            tabPage3.Controls.Add(chkOffline);
            tabPage3.Controls.Add(btnOpenForm2);
            tabPage3.Controls.Add(btnStopServer);
            tabPage3.Controls.Add(btnStart);
            tabPage3.Controls.Add(btnSendMessage);
            tabPage3.Controls.Add(listBox1);
            tabPage3.Location = new Point(4, 24);
            tabPage3.Name = "tabPage3";
            tabPage3.Padding = new Padding(3);
            tabPage3.Size = new Size(1237, 627);
            tabPage3.TabIndex = 2;
            tabPage3.Text = "tabPage3";
            tabPage3.UseVisualStyleBackColor = true;
            // 
            // chkOffline
            // 
            chkOffline.AutoSize = true;
            chkOffline.Location = new Point(11, 245);
            chkOffline.Name = "chkOffline";
            chkOffline.Size = new Size(62, 19);
            chkOffline.TabIndex = 4;
            chkOffline.Text = "Offline";
            chkOffline.UseVisualStyleBackColor = true;
            // 
            // btnOpenForm2
            // 
            btnOpenForm2.AutoSize = true;
            btnOpenForm2.Location = new Point(11, 62);
            btnOpenForm2.Name = "btnOpenForm2";
            btnOpenForm2.Size = new Size(96, 25);
            btnOpenForm2.TabIndex = 0;
            btnOpenForm2.Text = "Open Form 2";
            btnOpenForm2.UseVisualStyleBackColor = true;
            btnOpenForm2.Click += btnOpenForm2_Click;
            // 
            // btnStopServer
            // 
            btnStopServer.Location = new Point(11, 155);
            btnStopServer.Name = "btnStopServer";
            btnStopServer.Size = new Size(96, 25);
            btnStopServer.TabIndex = 1;
            btnStopServer.Text = "Stop Server";
            btnStopServer.UseVisualStyleBackColor = true;
            btnStopServer.Click += btnStopServer_Click;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(11, 93);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(96, 25);
            btnStart.TabIndex = 1;
            btnStart.Text = "Start Server";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // btnSendMessage
            // 
            btnSendMessage.Location = new Point(11, 124);
            btnSendMessage.Name = "btnSendMessage";
            btnSendMessage.Size = new Size(96, 25);
            btnSendMessage.TabIndex = 2;
            btnSendMessage.Text = "Send Message";
            btnSendMessage.UseVisualStyleBackColor = true;
            btnSendMessage.Click += btnSendMessage_Click;
            // 
            // listBox1
            // 
            listBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            listBox1.FormattingEnabled = true;
            listBox1.ItemHeight = 15;
            listBox1.Location = new Point(140, 6);
            listBox1.Name = "listBox1";
            listBox1.Size = new Size(1089, 604);
            listBox1.TabIndex = 3;
            // 
            // tabPage4
            // 
            tabPage4.Location = new Point(4, 24);
            tabPage4.Name = "tabPage4";
            tabPage4.Padding = new Padding(3);
            tabPage4.Size = new Size(1237, 627);
            tabPage4.TabIndex = 3;
            tabPage4.Text = "tabPage4";
            tabPage4.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1245, 655);
            Controls.Add(tabControl1);
            DoubleBuffered = true;
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Form1";
            Load += Form1_Load;
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            flowLayoutPanel1.ResumeLayout(false);
            flowLayoutPanel1.PerformLayout();
            tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgv).EndInit();
            tabPage3.ResumeLayout(false);
            tabPage3.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Button btnMemberCacheAllMembers;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private FlowLayoutPanel flowLayoutPanel1;
        private TabPage tabPage2;
        private TextBox textBox1;
        private PropertyGrid propertyGrid1;
        private CheckBox checkBox1;
        private Button btnLoad;
        private TabPage tabPage3;
        private TabPage tabPage4;
        private Button btnOpenForm2;
        private Button btnStart;
        private Button btnSendMessage;
        private ListBox listBox1;
        private CheckBox chkOffline;
        private Button btnStopServer;
        private TextBox textBox2;
        private Label label1;
        private DataGridView dgv;
    }
}
