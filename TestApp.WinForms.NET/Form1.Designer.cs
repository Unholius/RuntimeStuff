namespace TestApp.WinForms.NET
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
            qdObjectEditor1 = new QuickDialogs.WinForms.NET.QDObjectEditor();
            SuspendLayout();
            // 
            // qdObjectEditor1
            // 
            qdObjectEditor1.Location = new Point(180, 72);
            qdObjectEditor1.Name = "qdObjectEditor1";
            qdObjectEditor1.Size = new Size(347, 252);
            qdObjectEditor1.TabIndex = 0;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(qdObjectEditor1);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            ResumeLayout(false);
        }

        #endregion

        private QuickDialogs.WinForms.NET.QDObjectEditor qdObjectEditor1;
    }
}
