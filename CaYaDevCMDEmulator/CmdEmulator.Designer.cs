namespace CaYaDevCMDEmulator
{
    partial class CmdEmulator
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CmdEmulator));
            outputBox = new RichTextBox();
            inputBox = new TextBox();
            executeButton = new Button();
            clearButton = new Button();
            historyBox = new ComboBox();
            infoLabel = new Label();
            SuspendLayout();
            // 
            // outputBox
            // 
            outputBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            outputBox.BackColor = Color.Black;
            outputBox.Font = new Font("Consolas", 10F);
            outputBox.ForeColor = Color.White;
            outputBox.Location = new Point(10, 10);
            outputBox.Name = "outputBox";
            outputBox.ReadOnly = true;
            outputBox.Size = new Size(854, 376);
            outputBox.TabIndex = 0;
            outputBox.Text = "";
            outputBox.WordWrap = false;
            // 
            // inputBox
            // 
            inputBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            inputBox.BackColor = Color.Black;
            inputBox.Font = new Font("Consolas", 10F);
            inputBox.ForeColor = Color.White;
            inputBox.Location = new Point(12, 392);
            inputBox.Name = "inputBox";
            inputBox.Size = new Size(734, 23);
            inputBox.TabIndex = 1;
            inputBox.KeyDown += InputBox_KeyDown;
            // 
            // executeButton
            // 
            executeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            executeButton.BackColor = Color.DarkBlue;
            executeButton.FlatStyle = FlatStyle.Flat;
            executeButton.ForeColor = Color.White;
            executeButton.Location = new Point(756, 392);
            executeButton.Name = "executeButton";
            executeButton.Size = new Size(110, 25);
            executeButton.TabIndex = 2;
            executeButton.Text = "Çalıştır";
            executeButton.UseVisualStyleBackColor = false;
            executeButton.Click += ExecuteButton_Click;
            // 
            // clearButton
            // 
            clearButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            clearButton.BackColor = Color.DarkRed;
            clearButton.FlatStyle = FlatStyle.Flat;
            clearButton.ForeColor = Color.White;
            clearButton.Location = new Point(756, 427);
            clearButton.Name = "clearButton";
            clearButton.Size = new Size(110, 25);
            clearButton.TabIndex = 3;
            clearButton.Text = "Temizle";
            clearButton.UseVisualStyleBackColor = false;
            clearButton.Click += ClearButton_Click;
            // 
            // historyBox
            // 
            historyBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            historyBox.BackColor = Color.Black;
            historyBox.DropDownStyle = ComboBoxStyle.DropDownList;
            historyBox.Font = new Font("Consolas", 10F);
            historyBox.ForeColor = Color.White;
            historyBox.FormattingEnabled = true;
            historyBox.Location = new Point(12, 427);
            historyBox.Name = "historyBox";
            historyBox.Size = new Size(734, 23);
            historyBox.TabIndex = 4;
            historyBox.SelectedIndexChanged += HistoryBox_SelectedIndexChanged;
            // 
            // infoLabel
            // 
            infoLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            infoLabel.BackColor = Color.Black;
            infoLabel.Font = new Font("Consolas", 10F);
            infoLabel.ForeColor = Color.Yellow;
            infoLabel.Location = new Point(12, 455);
            infoLabel.Name = "infoLabel";
            infoLabel.Size = new Size(854, 38);
            infoLabel.TabIndex = 5;
            infoLabel.Text = "Yardım için: help/?\r\nCaYaDevCommandLine CaYaSystem";
            // 
            // CmdEmulator
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Black;
            ClientSize = new Size(874, 502);
            Controls.Add(infoLabel);
            Controls.Add(historyBox);
            Controls.Add(clearButton);
            Controls.Add(executeButton);
            Controls.Add(inputBox);
            Controls.Add(outputBox);
            ForeColor = Color.White;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "CmdEmulator";
            Text = "CaYaDev CMD Emulator v1.0";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.RichTextBox outputBox;
        private System.Windows.Forms.TextBox inputBox;
        private System.Windows.Forms.Button executeButton;
        private System.Windows.Forms.Button clearButton;
        private System.Windows.Forms.ComboBox historyBox;
        private System.Windows.Forms.Label infoLabel;
    }
}