using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SimpleNotepad
{
    /// <summary>
    /// The main editor window. Mirrors the feature set and behaviour of the
    /// classic Windows 10 Notepad: a single multiline edit control wrapped in
    /// File / Edit / Format / View / Help menus, a status bar, word wrap,
    /// font selection, find &amp; replace, go to line, and printing.
    /// </summary>
    public class MainForm : Form
    {
        private const string AppName = "Notepad";

        private readonly TextBox _editor;
        private readonly MenuStrip _menu;
        private readonly StatusStrip _statusStrip;
        private readonly ToolStripStatusLabel _positionLabel;
        private readonly ToolStripStatusLabel _zoomLabel;
        private readonly ToolStripStatusLabel _lineEndingLabel;
        private readonly ToolStripStatusLabel _encodingLabel;

        private ToolStripMenuItem _wordWrapItem;
        private ToolStripMenuItem _statusBarItem;
        private ToolStripMenuItem _goToItem;

        private string _currentPath;          // null => never saved ("Untitled")
        private Encoding _currentEncoding = new UTF8Encoding(false);
        private bool _isDirty;

        private FindReplaceForm _findReplaceForm;
        private string _lastSearchText = string.Empty;
        private bool _lastMatchCase;
        private bool _lastSearchDown = true;

        private int _zoomPercent = 100;
        private float _baseFontSize;

        // Used by the print path to walk through the document.
        private string _printText;
        private int _printCharIndex;

        public MainForm(string startupFile)
        {
            // ----- Editor -----
            _editor = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                AcceptsTab = true,
                Font = new Font("Consolas", 11f),
                BorderStyle = BorderStyle.None,
                MaxLength = 0 // no practical length limit
            };
            _baseFontSize = _editor.Font.Size;
            _editor.TextChanged += (s, e) => { _isDirty = true; UpdateTitle(); };
            _editor.KeyUp += (s, e) => UpdatePositionLabel();
            _editor.MouseUp += (s, e) => UpdatePositionLabel();

            // ----- Status bar -----
            _positionLabel = new ToolStripStatusLabel("Ln 1, Col 1")
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleRight
            };
            _zoomLabel = new ToolStripStatusLabel("100%") { AutoSize = true };
            _lineEndingLabel = new ToolStripStatusLabel("Windows (CRLF)") { AutoSize = true };
            _encodingLabel = new ToolStripStatusLabel("UTF-8") { AutoSize = true };
            _statusStrip = new StatusStrip { SizingGrip = true };
            _statusStrip.Items.AddRange(new ToolStripItem[]
            {
                _positionLabel,
                new ToolStripStatusLabel("|") { AutoSize = true },
                _zoomLabel,
                new ToolStripStatusLabel("|") { AutoSize = true },
                _lineEndingLabel,
                new ToolStripStatusLabel("|") { AutoSize = true },
                _encodingLabel
            });

            // ----- Menu -----
            _menu = BuildMenu();

            // ----- Form -----
            Text = AppName;
            ClientSize = new Size(800, 600);
            StartPosition = FormStartPosition.CenterScreen;
            MainMenuStrip = _menu;
            KeyPreview = true;

            Controls.Add(_editor);
            Controls.Add(_statusStrip);
            Controls.Add(_menu);

            FormClosing += MainForm_FormClosing;

            if (!string.IsNullOrEmpty(startupFile))
                LoadFile(startupFile);
            else
                NewDocument(prompt: false);

            UpdatePositionLabel();
        }

        // ------------------------------------------------------------------
        // Menu construction
        // ------------------------------------------------------------------

        private MenuStrip BuildMenu()
        {
            var menu = new MenuStrip();

            // ----- File -----
            var file = new ToolStripMenuItem("&File");
            file.DropDownItems.Add(MakeItem("&New", Keys.Control | Keys.N, (s, e) => NewDocument(prompt: true)));
            file.DropDownItems.Add(MakeItem("&Open...", Keys.Control | Keys.O, (s, e) => OpenFile()));
            file.DropDownItems.Add(MakeItem("&Save", Keys.Control | Keys.S, (s, e) => Save()));
            file.DropDownItems.Add(MakeItem("Save &As...", Keys.Control | Keys.Shift | Keys.S, (s, e) => SaveAs()));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(MakeItem("Page Set&up...", Keys.None, (s, e) => PageSetup()));
            file.DropDownItems.Add(MakeItem("&Print...", Keys.Control | Keys.P, (s, e) => Print()));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(MakeItem("E&xit", Keys.None, (s, e) => Close()));
            menu.Items.Add(file);

            // ----- Edit -----
            var edit = new ToolStripMenuItem("&Edit");
            edit.DropDownItems.Add(MakeItem("&Undo", Keys.Control | Keys.Z, (s, e) => { if (_editor.CanUndo) _editor.Undo(); }));
            edit.DropDownItems.Add(new ToolStripSeparator());
            edit.DropDownItems.Add(MakeItem("Cu&t", Keys.Control | Keys.X, (s, e) => _editor.Cut()));
            edit.DropDownItems.Add(MakeItem("&Copy", Keys.Control | Keys.C, (s, e) => _editor.Copy()));
            edit.DropDownItems.Add(MakeItem("&Paste", Keys.Control | Keys.V, (s, e) => _editor.Paste()));
            edit.DropDownItems.Add(MakeItem("De&lete", Keys.Delete, (s, e) => DeleteSelection()));
            edit.DropDownItems.Add(new ToolStripSeparator());
            edit.DropDownItems.Add(MakeItem("&Find...", Keys.Control | Keys.F, (s, e) => ShowFind(replaceMode: false)));
            edit.DropDownItems.Add(MakeItem("Find &Next", Keys.F3, (s, e) => FindNext(_lastSearchDown)));
            edit.DropDownItems.Add(MakeItem("Find Pre&vious", Keys.Shift | Keys.F3, (s, e) => FindNext(false)));
            edit.DropDownItems.Add(MakeItem("&Replace...", Keys.Control | Keys.H, (s, e) => ShowFind(replaceMode: true)));
            _goToItem = MakeItem("&Go To...", Keys.Control | Keys.G, (s, e) => GoTo());
            edit.DropDownItems.Add(_goToItem);
            edit.DropDownItems.Add(new ToolStripSeparator());
            edit.DropDownItems.Add(MakeItem("Select &All", Keys.Control | Keys.A, (s, e) => _editor.SelectAll()));
            edit.DropDownItems.Add(MakeItem("Time/&Date", Keys.F5, (s, e) => InsertTimeDate()));
            menu.Items.Add(edit);

            // ----- Format -----
            var format = new ToolStripMenuItem("F&ormat");
            _wordWrapItem = MakeItem("&Word Wrap", Keys.None, (s, e) => ToggleWordWrap());
            format.DropDownItems.Add(_wordWrapItem);
            format.DropDownItems.Add(MakeItem("&Font...", Keys.None, (s, e) => ChooseFont()));
            menu.Items.Add(format);

            // ----- View -----
            var view = new ToolStripMenuItem("&View");
            var zoom = new ToolStripMenuItem("&Zoom");
            zoom.DropDownItems.Add(MakeItem("Zoom &In", Keys.Control | Keys.Oemplus, (s, e) => SetZoom(_zoomPercent + 10)));
            zoom.DropDownItems.Add(MakeItem("Zoom &Out", Keys.Control | Keys.OemMinus, (s, e) => SetZoom(_zoomPercent - 10)));
            zoom.DropDownItems.Add(MakeItem("&Restore Default Zoom", Keys.Control | Keys.D0, (s, e) => SetZoom(100)));
            view.DropDownItems.Add(zoom);
            _statusBarItem = MakeItem("&Status Bar", Keys.None, (s, e) => ToggleStatusBar());
            _statusBarItem.Checked = true;
            view.DropDownItems.Add(_statusBarItem);
            menu.Items.Add(view);

            // ----- Help -----
            var help = new ToolStripMenuItem("&Help");
            help.DropDownItems.Add(MakeItem("&About Notepad", Keys.None, (s, e) => ShowAbout()));
            menu.Items.Add(help);

            return menu;
        }

        private static ToolStripMenuItem MakeItem(string text, Keys shortcut, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text);
            if (shortcut != Keys.None)
            {
                item.ShortcutKeys = shortcut;
                // Some shortcuts (e.g. Delete, F-keys) look cleaner hidden.
                item.ShowShortcutKeys = true;
            }
            item.Click += onClick;
            return item;
        }

        // ------------------------------------------------------------------
        // File operations
        // ------------------------------------------------------------------

        private void NewDocument(bool prompt)
        {
            if (prompt && !ConfirmDiscardChanges())
                return;

            _editor.Clear();
            _currentPath = null;
            _currentEncoding = new UTF8Encoding(false);
            _isDirty = false;
            _editor.ClearUndo();
            UpdateTitle();
            UpdateEncodingLabel();
        }

        private void OpenFile()
        {
            if (!ConfirmDiscardChanges())
                return;

            using (var dlg = new OpenFileDialog
            {
                Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*",
                DefaultExt = "txt"
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    LoadFile(dlg.FileName);
            }
        }

        private void LoadFile(string path)
        {
            try
            {
                // Detect the encoding from any byte-order mark, defaulting to UTF-8.
                var detector = new StreamReader(path, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
                string text;
                using (detector)
                {
                    text = detector.ReadToEnd();
                    _currentEncoding = detector.CurrentEncoding;
                }

                // TextBox expects CRLF line endings; remember the original style
                // for the status bar but normalise for display.
                bool hasCrlf = text.Contains("\r\n");
                _editor.Text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

                _currentPath = path;
                _isDirty = false;
                _editor.ClearUndo();
                _lineEndingLabel.Text = hasCrlf || !text.Contains("\n") ? "Windows (CRLF)" : "Unix (LF)";
                UpdateTitle();
                UpdateEncodingLabel();
                UpdatePositionLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Cannot open file:\n" + ex.Message, AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool Save()
        {
            if (_currentPath == null)
                return SaveAs();
            return WriteFile(_currentPath);
        }

        private bool SaveAs()
        {
            using (var dlg = new SaveFileDialog
            {
                Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = _currentPath != null ? Path.GetFileName(_currentPath) : "Untitled.txt"
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    return WriteFile(dlg.FileName);
            }
            return false;
        }

        private bool WriteFile(string path)
        {
            try
            {
                File.WriteAllText(path, _editor.Text, _currentEncoding);
                _currentPath = path;
                _isDirty = false;
                UpdateTitle();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Cannot save file:\n" + ex.Message, AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        /// <summary>
        /// Returns true if it is safe to discard the current document, prompting
        /// the user to save when there are unsaved changes.
        /// </summary>
        private bool ConfirmDiscardChanges()
        {
            if (!_isDirty)
                return true;

            string name = _currentPath != null ? Path.GetFileName(_currentPath) : "Untitled";
            var result = MessageBox.Show(this,
                "Do you want to save changes to " + name + "?",
                AppName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            switch (result)
            {
                case DialogResult.Yes:
                    return Save();
                case DialogResult.No:
                    return true;
                default:
                    return false;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!ConfirmDiscardChanges())
                e.Cancel = true;
        }

        // ------------------------------------------------------------------
        // Edit helpers
        // ------------------------------------------------------------------

        private void DeleteSelection()
        {
            if (_editor.SelectionLength > 0)
                _editor.SelectedText = string.Empty;
        }

        private void InsertTimeDate()
        {
            // Matches Notepad's F5 behaviour: "HH:mm M/d/yyyy".
            string stamp = DateTime.Now.ToString("h:mm tt M/d/yyyy");
            int pos = _editor.SelectionStart;
            _editor.SelectedText = stamp;
            _editor.SelectionStart = pos + stamp.Length;
        }

        // ------------------------------------------------------------------
        // Find / Replace
        // ------------------------------------------------------------------

        private void ShowFind(bool replaceMode)
        {
            if (_findReplaceForm == null || _findReplaceForm.IsDisposed)
            {
                _findReplaceForm = new FindReplaceForm(this);
                _findReplaceForm.FindText = _lastSearchText;
                _findReplaceForm.MatchCase = _lastMatchCase;
            }

            // Pre-fill with the current selection, like Notepad does.
            if (_editor.SelectionLength > 0 && !_editor.SelectedText.Contains("\n"))
                _findReplaceForm.FindText = _editor.SelectedText;

            _findReplaceForm.SetReplaceMode(replaceMode);
            if (!_findReplaceForm.Visible)
                _findReplaceForm.Show(this);
            _findReplaceForm.Activate();
        }

        /// <summary>Called by the find/replace dialog. Returns true on a hit.</summary>
        public bool FindNextFrom(string text, bool matchCase, bool searchDown)
        {
            _lastSearchText = text;
            _lastMatchCase = matchCase;
            _lastSearchDown = searchDown;
            return FindNext(searchDown);
        }

        private bool FindNext(bool searchDown)
        {
            if (string.IsNullOrEmpty(_lastSearchText))
            {
                ShowFind(replaceMode: false);
                return false;
            }

            var comparison = _lastMatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            string haystack = _editor.Text;
            int index;

            if (searchDown)
            {
                int start = _editor.SelectionStart + _editor.SelectionLength;
                index = haystack.IndexOf(_lastSearchText, Math.Min(start, haystack.Length), comparison);
            }
            else
            {
                int start = _editor.SelectionStart - 1;
                index = start >= 0
                    ? haystack.LastIndexOf(_lastSearchText, Math.Min(start, haystack.Length - 1), comparison)
                    : -1;
            }

            if (index < 0)
            {
                MessageBox.Show(this, "Cannot find \"" + _lastSearchText + "\"", AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            _editor.Select(index, _lastSearchText.Length);
            _editor.ScrollToCaret();
            UpdatePositionLabel();
            return true;
        }

        /// <summary>Replace the current match (if it matches) then find the next.</summary>
        public void ReplaceOne(string findText, string replaceText, bool matchCase)
        {
            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (_editor.SelectionLength > 0 &&
                string.Equals(_editor.SelectedText, findText, comparison))
            {
                _editor.SelectedText = replaceText;
            }
            FindNextFrom(findText, matchCase, true);
        }

        public void ReplaceAll(string findText, string replaceText, bool matchCase)
        {
            if (string.IsNullOrEmpty(findText))
                return;

            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            string text = _editor.Text;
            var sb = new StringBuilder();
            int i = 0, count = 0;
            while (i < text.Length)
            {
                int next = text.IndexOf(findText, i, comparison);
                if (next < 0)
                {
                    sb.Append(text, i, text.Length - i);
                    break;
                }
                sb.Append(text, i, next - i);
                sb.Append(replaceText);
                i = next + findText.Length;
                count++;
            }

            if (count > 0)
            {
                int caret = _editor.SelectionStart;
                _editor.Text = sb.ToString();
                _editor.SelectionStart = Math.Min(caret, _editor.TextLength);
            }
            UpdatePositionLabel();
        }

        // ------------------------------------------------------------------
        // Go To line
        // ------------------------------------------------------------------

        private void GoTo()
        {
            if (_editor.WordWrap)
                return; // Notepad disables Go To while word wrap is on.

            int currentLine = _editor.GetLineFromCharIndex(_editor.SelectionStart) + 1;
            using (var dlg = new GoToForm(currentLine))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    int line = Math.Max(1, dlg.LineNumber) - 1;
                    int totalLines = _editor.Lines.Length;
                    if (line >= totalLines)
                        line = Math.Max(0, totalLines - 1);

                    int charIndex = _editor.GetFirstCharIndexFromLine(line);
                    if (charIndex < 0)
                        charIndex = _editor.TextLength;
                    _editor.SelectionStart = charIndex;
                    _editor.SelectionLength = 0;
                    _editor.ScrollToCaret();
                    UpdatePositionLabel();
                }
            }
        }

        // ------------------------------------------------------------------
        // Format
        // ------------------------------------------------------------------

        private void ToggleWordWrap()
        {
            _editor.WordWrap = !_editor.WordWrap;
            _wordWrapItem.Checked = _editor.WordWrap;
            _editor.ScrollBars = _editor.WordWrap ? ScrollBars.Vertical : ScrollBars.Both;
            _goToItem.Enabled = !_editor.WordWrap;
            UpdatePositionLabel();
        }

        private void ChooseFont()
        {
            using (var dlg = new FontDialog { Font = _editor.Font, ShowEffects = false })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // Adopt the chosen size as the new 100% baseline so a later
                    // zoom stays relative to it.
                    _editor.Font = dlg.Font;
                    _baseFontSize = dlg.Font.Size;
                    ApplyZoom();
                }
            }
        }

        // ------------------------------------------------------------------
        // View
        // ------------------------------------------------------------------

        private void ToggleStatusBar()
        {
            _statusStrip.Visible = !_statusStrip.Visible;
            _statusBarItem.Checked = _statusStrip.Visible;
        }

        private void SetZoom(int percent)
        {
            _zoomPercent = Math.Max(10, Math.Min(500, percent));
            ApplyZoom();
        }

        private void ApplyZoom()
        {
            float size = _baseFontSize * (_zoomPercent / 100f);
            _editor.Font = new Font(_editor.Font.FontFamily, size, _editor.Font.Style);
            _zoomLabel.Text = _zoomPercent + "%";
        }

        // ------------------------------------------------------------------
        // Printing
        // ------------------------------------------------------------------

        private PageSettings _pageSettings = new PageSettings();

        private void PageSetup()
        {
            using (var dlg = new PageSetupDialog { PageSettings = _pageSettings })
            {
                dlg.ShowDialog(this);
            }
        }

        private void Print()
        {
            using (var doc = new PrintDocument())
            {
                doc.DefaultPageSettings = _pageSettings;
                doc.DocumentName = _currentPath != null ? Path.GetFileName(_currentPath) : "Untitled";
                doc.BeginPrint += (s, e) => { _printText = _editor.Text; _printCharIndex = 0; };
                doc.PrintPage += PrintPage;

                using (var dlg = new PrintDialog { Document = doc })
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                        doc.Print();
                }
            }
        }

        private void PrintPage(object sender, PrintPageEventArgs e)
        {
            // Note: do not dispose _editor.Font — it is the live editor font.
            Font font = _editor.Font;
            float lineHeight = font.GetHeight(e.Graphics);
            float yPos = e.MarginBounds.Top;
            int linesPerPage = (int)(e.MarginBounds.Height / lineHeight);
            int printedLines = 0;

            var reader = new StringReader(_printText.Substring(_printCharIndex));
            string line;
            while (printedLines < linesPerPage && (line = reader.ReadLine()) != null)
            {
                e.Graphics.DrawString(line, font, Brushes.Black,
                    e.MarginBounds.Left, yPos, StringFormat.GenericTypographic);
                yPos += lineHeight;
                printedLines++;
                _printCharIndex += line.Length + Environment.NewLine.Length;
            }

            e.HasMorePages = _printCharIndex < _printText.Length;
        }

        // ------------------------------------------------------------------
        // Help
        // ------------------------------------------------------------------

        private void ShowAbout()
        {
            MessageBox.Show(this,
                "Simple Notepad\nVersion 1.0\n\nA lightweight Notepad clone built with .NET Framework " +
                "and Windows Forms.",
                "About Notepad", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ------------------------------------------------------------------
        // Status bar / title helpers
        // ------------------------------------------------------------------

        private void UpdateTitle()
        {
            string name = _currentPath != null ? Path.GetFileName(_currentPath) : "Untitled";
            Text = (_isDirty ? "*" : string.Empty) + name + " - " + AppName;
        }

        private void UpdateEncodingLabel()
        {
            if (_currentEncoding is UTF8Encoding)
                _encodingLabel.Text = "UTF-8";
            else if (_currentEncoding.Equals(Encoding.Unicode))
                _encodingLabel.Text = "UTF-16 LE";
            else if (_currentEncoding.Equals(Encoding.BigEndianUnicode))
                _encodingLabel.Text = "UTF-16 BE";
            else
                _encodingLabel.Text = _currentEncoding.WebName.ToUpperInvariant();
        }

        private void UpdatePositionLabel()
        {
            int caret = _editor.SelectionStart;
            int line = _editor.GetLineFromCharIndex(caret);
            int col = caret - _editor.GetFirstCharIndexFromLine(line);
            _positionLabel.Text = "Ln " + (line + 1) + ", Col " + (col + 1);
        }
    }
}
