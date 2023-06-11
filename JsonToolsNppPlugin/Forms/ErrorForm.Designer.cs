﻿namespace JSON_Tools.Forms
{
    partial class ErrorForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ErrorForm));
            this.ErrorGrid = new System.Windows.Forms.DataGridView();
            this.Severity = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Description = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Position = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.ErrorGrid)).BeginInit();
            this.SuspendLayout();
            // 
            // ErrorGrid
            // 
            this.ErrorGrid.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ErrorGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.ErrorGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Severity,
            this.Description,
            this.Position});
            this.ErrorGrid.Location = new System.Drawing.Point(4, 4);
            this.ErrorGrid.Name = "ErrorGrid";
            this.ErrorGrid.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
            this.ErrorGrid.RowTemplate.Height = 24;
            this.ErrorGrid.Size = new System.Drawing.Size(771, 277);
            this.ErrorGrid.TabIndex = 0;
            this.ErrorGrid.CellEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.ErrorGrid_CellEnter);
            this.ErrorGrid.RowEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.ErrorGrid_CellEnter);
            this.ErrorGrid.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ErrorForm_KeyDown);
            this.ErrorGrid.Resize += new System.EventHandler(this.ErrorGrid_Resize);
            // 
            // Severity
            // 
            this.Severity.HeaderText = "Severity";
            this.Severity.MinimumWidth = 6;
            this.Severity.Name = "Severity";
            this.Severity.ReadOnly = true;
            this.Severity.Width = 75;
            // 
            // Description
            // 
            this.Description.HeaderText = "Description";
            this.Description.MinimumWidth = 6;
            this.Description.Name = "Description";
            this.Description.ReadOnly = true;
            this.Description.Width = 125;
            // 
            // Position
            // 
            this.Position.HeaderText = "Position";
            this.Position.MinimumWidth = 6;
            this.Position.Name = "Position";
            this.Position.ReadOnly = true;
            this.Position.Width = 75;
            // 
            // ErrorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(780, 284);
            this.Controls.Add(this.ErrorGrid);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "ErrorForm";
            this.Text = "Syntax errors in JSON";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ErrorForm_KeyDown);
            ((System.ComponentModel.ISupportInitialize)(this.ErrorGrid)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView ErrorGrid;
        private System.Windows.Forms.DataGridViewTextBoxColumn Severity;
        private System.Windows.Forms.DataGridViewTextBoxColumn Description;
        private System.Windows.Forms.DataGridViewTextBoxColumn Position;
    }
}