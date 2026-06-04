using System;
using System.Drawing;
using System.Windows.Forms;

namespace SimpleNotepad
{
    /// <summary>The small "Go To Line" dialog (Ctrl+G).</summary>
    public class GoToForm : Form
    {
        private readonly TextBox _lineBox;

        public GoToForm(int currentLine)
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(260, 110);
            Text = "Go To Line";

            var label = new Label { Text = "&Line number:", Left = 12, Top = 12, Width = 200 };
            _lineBox = new TextBox { Left = 12, Top = 32, Width = 236, Text = currentLine.ToString() };
            _lineBox.KeyPress += (s, e) =>
            {
                // Digits and editing keys only.
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                    e.Handled = true;
            };

            var ok = new Button { Text = "Go To", Left = 88, Top = 70, Width = 75, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", Left = 172, Top = 70, Width = 75, DialogResult = DialogResult.Cancel };

            Controls.AddRange(new Control[] { label, _lineBox, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;

            Shown += (s, e) => { _lineBox.Focus(); _lineBox.SelectAll(); };
        }

        public int LineNumber
        {
            get
            {
                int value;
                return int.TryParse(_lineBox.Text, out value) ? value : 1;
            }
        }
    }
}
