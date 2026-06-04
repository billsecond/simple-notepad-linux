using System;
using System.Drawing;
using System.Windows.Forms;

namespace SimpleNotepad
{
    /// <summary>
    /// The modeless Find / Replace dialog, switchable between the two modes
    /// just like Notepad's shared dialog.
    /// </summary>
    public class FindReplaceForm : Form
    {
        private readonly MainForm _owner;

        private readonly TextBox _findBox;
        private readonly TextBox _replaceBox;
        private readonly Label _replaceLabel;
        private readonly CheckBox _matchCaseBox;
        private readonly RadioButton _upRadio;
        private readonly RadioButton _downRadio;
        private readonly GroupBox _directionGroup;
        private readonly Button _findNextButton;
        private readonly Button _replaceButton;
        private readonly Button _replaceAllButton;
        private readonly Button _cancelButton;

        private bool _replaceMode;

        public FindReplaceForm(MainForm owner)
        {
            _owner = owner;

            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 160);

            var findLabel = new Label { Text = "Fi&nd what:", Left = 12, Top = 18, Width = 80, TextAlign = ContentAlignment.MiddleLeft };
            _findBox = new TextBox { Left = 96, Top = 15, Width = 200 };
            _findBox.TextChanged += (s, e) => UpdateButtons();

            _replaceLabel = new Label { Text = "Re&place with:", Left = 12, Top = 48, Width = 80, TextAlign = ContentAlignment.MiddleLeft };
            _replaceBox = new TextBox { Left = 96, Top = 45, Width = 200 };

            _matchCaseBox = new CheckBox { Text = "Match &case", Left = 15, Top = 110, Width = 120 };

            _downRadio = new RadioButton { Text = "&Down", Left = 90, Top = 18, Width = 60, Checked = true };
            _upRadio = new RadioButton { Text = "&Up", Left = 15, Top = 18, Width = 60 };
            _directionGroup = new GroupBox { Text = "Direction", Left = 150, Top = 90, Width = 150, Height = 46 };
            _directionGroup.Controls.Add(_upRadio);
            _directionGroup.Controls.Add(_downRadio);

            _findNextButton = new Button { Text = "&Find Next", Left = 312, Top = 13, Width = 78 };
            _findNextButton.Click += (s, e) => DoFindNext();

            _replaceButton = new Button { Text = "&Replace", Left = 312, Top = 43, Width = 78 };
            _replaceButton.Click += (s, e) => _owner.ReplaceOne(_findBox.Text, _replaceBox.Text, _matchCaseBox.Checked);

            _replaceAllButton = new Button { Text = "Replace &All", Left = 312, Top = 73, Width = 78 };
            _replaceAllButton.Click += (s, e) => _owner.ReplaceAll(_findBox.Text, _replaceBox.Text, _matchCaseBox.Checked);

            _cancelButton = new Button { Text = "Cancel", Left = 312, Top = 103, Width = 78, DialogResult = DialogResult.Cancel };
            _cancelButton.Click += (s, e) => Hide();

            Controls.AddRange(new Control[]
            {
                findLabel, _findBox, _replaceLabel, _replaceBox,
                _matchCaseBox, _directionGroup,
                _findNextButton, _replaceButton, _replaceAllButton, _cancelButton
            });

            AcceptButton = _findNextButton;
            CancelButton = _cancelButton;

            // Hide instead of dispose so search state survives a close.
            FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                }
            };

            UpdateButtons();
        }

        public string FindText
        {
            get { return _findBox.Text; }
            set { _findBox.Text = value; }
        }

        public bool MatchCase
        {
            get { return _matchCaseBox.Checked; }
            set { _matchCaseBox.Checked = value; }
        }

        public void SetReplaceMode(bool replace)
        {
            _replaceMode = replace;
            Text = replace ? "Replace" : "Find";

            _replaceLabel.Visible = replace;
            _replaceBox.Visible = replace;
            _replaceButton.Visible = replace;
            _replaceAllButton.Visible = replace;
            // Direction only matters for plain Find in Notepad.
            _directionGroup.Visible = !replace;

            _findBox.Focus();
            _findBox.SelectAll();
            UpdateButtons();
        }

        private void DoFindNext()
        {
            bool down = !_directionGroup.Visible || _downRadio.Checked;
            _owner.FindNextFrom(_findBox.Text, _matchCaseBox.Checked, down);
        }

        private void UpdateButtons()
        {
            bool hasText = _findBox.Text.Length > 0;
            _findNextButton.Enabled = hasText;
            _replaceButton.Enabled = hasText;
            _replaceAllButton.Enabled = hasText;
        }
    }
}
