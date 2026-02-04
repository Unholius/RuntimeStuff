namespace TestWinFormsApp
{
    partial class Form2
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
            btnSendMessageToForm1 = new Button();
            SuspendLayout();
            // 
            // btnSendMessageToForm1
            // 
            btnSendMessageToForm1.Location = new Point(12, 12);
            btnSendMessageToForm1.Name = "btnSendMessageToForm1";
            btnSendMessageToForm1.Size = new Size(189, 48);
            btnSendMessageToForm1.TabIndex = 0;
            btnSendMessageToForm1.Text = "button1";
            btnSendMessageToForm1.UseVisualStyleBackColor = true;
            btnSendMessageToForm1.Click += btnSendMessageToForm1_Click;
            // 
            // Form2
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(btnSendMessageToForm1);
            Name = "Form2";
            Text = "Form2";
            ResumeLayout(false);
        }

        #endregion

        private Button btnSendMessageToForm1;
    }
}