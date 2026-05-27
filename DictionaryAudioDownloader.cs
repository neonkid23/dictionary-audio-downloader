using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        if (args.Any(arg => arg.Equals("--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            DictionaryAudioDownloader.RunSelfTest();
            return;
        }

        if (args.Any(arg => arg.Equals("--options-self-test", StringComparison.OrdinalIgnoreCase)))
        {
            DictionaryAudioDownloader.RunOptionsSelfTest();
            return;
        }

        if (args.Any(arg => arg.Equals("--queue-self-test", StringComparison.OrdinalIgnoreCase)))
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (DownloaderForm form = new DownloaderForm())
                form.RunQueueSelfTest();
            return;
        }

        if (args.Any(arg => arg.Equals("--visible-queue-self-test", StringComparison.OrdinalIgnoreCase)))
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (DownloaderForm form = new DownloaderForm())
                form.RunVisibleQueueSelfTest();
            return;
        }

        if (args.Any(arg => arg.Equals("--bulk-words-self-test", StringComparison.OrdinalIgnoreCase)))
        {
            DictionaryAudioDownloader.RunBulkWordsSelfTest();
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new DownloaderForm());
    }
}

internal sealed class DownloaderForm : Form
{
    private readonly List<string> queue = new List<string>();
    private readonly TextBox inputBox = new TextBox();
    private readonly DataGridView wordPreviewGrid = new DataGridView();
    private readonly TextBox logBox = new TextBox();
    private readonly Label statusLabel = new Label();
    private readonly Panel emptyPanel = new Panel();
    private readonly Panel queuePanel = new Panel();
    private readonly Panel previewModal = new Panel();
    private readonly Label previewCountLabel = new Label();
    private readonly Button previewDownloadButton = new Button();
    private readonly Button previewCancelButton = new Button();
    private readonly Button clearListButton = new Button();
    private readonly Button downloadButton = new Button();
    private readonly Button doneButton = new Button();
    private readonly ProgressBar progressBar = new ProgressBar();

    private bool showingPlaceholder = true;
    private bool downloading;

    public DownloaderForm()
    {
        Text = "Dictionary Audio Downloader";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(725, 650);
        MinimumSize = new Size(620, 520);
        BackColor = Color.FromArgb(64, 64, 64);
        Font = new Font("Segoe UI", 10f);
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "blue-open-book.ico");
        if (File.Exists(iconPath))
            Icon = new Icon(iconPath);

        BuildUi();
        RefreshQueueUi();
    }

    private void BuildUi()
    {
        Panel topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 76,
            BackColor = Color.FromArgb(45, 45, 45)
        };
        Controls.Add(topBar);

        Panel searchPanel = new Panel
        {
            Left = 3,
            Top = 10,
            Width = 604,
            Height = 52,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.FromArgb(70, 70, 70)
        };
        topBar.Controls.Add(searchPanel);

        Label searchIcon = new Label
        {
            Text = "\uE721",
            Font = new Font("Segoe MDL2 Assets", 13f),
            ForeColor = Color.White,
            Left = 14,
            Top = 16,
            Width = 24,
            Height = 24
        };
        searchPanel.Controls.Add(searchIcon);

        inputBox.BorderStyle = BorderStyle.None;
        inputBox.BackColor = Color.FromArgb(70, 70, 70);
        inputBox.ForeColor = Color.DarkGray;
        inputBox.Font = new Font("Segoe UI", 10f);
        inputBox.Left = 50;
        inputBox.Top = 15;
        inputBox.Width = 495;
        inputBox.Height = 24;
        inputBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        inputBox.Text = "URL or search query";
        inputBox.Enter += delegate { ClearPlaceholder(); };
        inputBox.Leave += delegate { RestorePlaceholder(); };
        inputBox.KeyDown += InputBoxOnKeyDown;
        searchPanel.Controls.Add(inputBox);

        downloadButton.FlatStyle = FlatStyle.Flat;
        downloadButton.FlatAppearance.BorderSize = 0;
        downloadButton.BackColor = Color.FromArgb(70, 70, 70);
        downloadButton.ForeColor = Color.Gainsboro;
        downloadButton.Font = new Font("Segoe MDL2 Assets", 16f);
        downloadButton.Text = "\uE72A";
        downloadButton.Left = 555;
        downloadButton.Top = 9;
        downloadButton.Width = 45;
        downloadButton.Height = 34;
        downloadButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        downloadButton.Cursor = Cursors.Hand;
        downloadButton.Click += delegate { ShowDownloadPreviewFromInput(); };
        searchPanel.Controls.Add(downloadButton);

        Button clearButton = MakeTopButton("\uE74D", 619);
        clearButton.Click += delegate
        {
            queue.Clear();
            inputBox.Clear();
            logBox.Clear();
            showingPlaceholder = false;
            RestorePlaceholder();
            RefreshQueueUi();
            inputBox.Focus();
        };
        topBar.Controls.Add(clearButton);

        Button folderButton = MakeTopButton("\uE713", 666);
        folderButton.Click += delegate
        {
            Directory.CreateDirectory(DictionaryAudioDownloader.OutputDirectory);
            Process.Start("explorer.exe", "\"" + DictionaryAudioDownloader.OutputDirectory + "\"");
        };
        topBar.Controls.Add(folderButton);

        Panel mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(64, 64, 64)
        };
        Controls.Add(mainPanel);

        BuildEmptyPanel();
        BuildQueuePanel();
        mainPanel.Controls.Add(queuePanel);
        mainPanel.Controls.Add(emptyPanel);

        statusLabel.Dock = DockStyle.Bottom;
        statusLabel.Height = 32;
        statusLabel.Text = "Ready";
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusLabel.Padding = new Padding(14, 0, 0, 0);
        statusLabel.ForeColor = Color.Gainsboro;
        statusLabel.BackColor = Color.FromArgb(54, 54, 54);
        Controls.Add(statusLabel);
    }

    private static Button MakeTopButton(string text, int left)
    {
        Button button = new Button
        {
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.White,
            Font = new Font("Segoe MDL2 Assets", 15f),
            Text = text,
            Left = left,
            Top = 20,
            Width = 37,
            Height = 35,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void BuildEmptyPanel()
    {
        emptyPanel.Dock = DockStyle.Fill;
        emptyPanel.BackColor = Color.FromArgb(64, 64, 64);

        Label emptyText = new Label
        {
            Text = "Copy-paste a URL or enter a search query to start downloading\r\nShift+Enter adds one item. Ctrl+Shift+Enter opens unlimited input.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.WhiteSmoke,
            Font = new Font("Segoe UI", 10f),
            Padding = new Padding(24, 0, 24, 0)
        };
        emptyPanel.Controls.Add(emptyText);
    }

    private void BuildQueuePanel()
    {
        queuePanel.Dock = DockStyle.Fill;
        queuePanel.BackColor = Color.FromArgb(64, 64, 64);

        previewModal.Left = 30;
        previewModal.Top = 22;
        previewModal.Width = 650;
        previewModal.Height = 318;
        previewModal.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        previewModal.BackColor = Color.FromArgb(47, 47, 47);
        previewModal.BorderStyle = BorderStyle.FixedSingle;
        queuePanel.Controls.Add(previewModal);

        Label titleLabel = new Label
        {
            Text = "Word list preview",
            Left = 14,
            Top = 12,
            Width = 190,
            Height = 24,
            ForeColor = Color.WhiteSmoke,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        previewModal.Controls.Add(titleLabel);

        Label formatLabel = new Label
        {
            Text = "Format: MP3",
            Left = 214,
            Top = 14,
            Width = 120,
            Height = 22,
            ForeColor = Color.FromArgb(170, 170, 170),
            Font = new Font("Segoe UI", 9f)
        };
        previewModal.Controls.Add(formatLabel);

        previewCountLabel.Left = 340;
        previewCountLabel.Top = 14;
        previewCountLabel.Width = 150;
        previewCountLabel.Height = 22;
        previewCountLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        previewCountLabel.ForeColor = Color.FromArgb(165, 165, 165);
        previewCountLabel.Font = new Font("Segoe UI", 9f);
        previewCountLabel.TextAlign = ContentAlignment.MiddleRight;
        previewModal.Controls.Add(previewCountLabel);

        clearListButton.Text = "CLEAR LIST";
        clearListButton.Left = 510;
        clearListButton.Top = 9;
        clearListButton.Width = 122;
        clearListButton.Height = 30;
        clearListButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        clearListButton.FlatStyle = FlatStyle.Flat;
        clearListButton.BackColor = Color.FromArgb(55, 55, 55);
        clearListButton.ForeColor = Color.Gainsboro;
        clearListButton.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        clearListButton.FlatAppearance.BorderColor = Color.FromArgb(92, 92, 92);
        clearListButton.Click += delegate
        {
            queue.Clear();
            RefreshQueueUi();
        };
        previewModal.Controls.Add(clearListButton);

        wordPreviewGrid.Left = 14;
        wordPreviewGrid.Top = 48;
        wordPreviewGrid.Width = 618;
        wordPreviewGrid.Height = 214;
        wordPreviewGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        wordPreviewGrid.BackgroundColor = Color.FromArgb(42, 42, 42);
        wordPreviewGrid.BorderStyle = BorderStyle.None;
        wordPreviewGrid.CellBorderStyle = DataGridViewCellBorderStyle.None;
        wordPreviewGrid.ColumnHeadersVisible = false;
        wordPreviewGrid.RowHeadersVisible = false;
        wordPreviewGrid.AllowUserToAddRows = false;
        wordPreviewGrid.AllowUserToDeleteRows = false;
        wordPreviewGrid.AllowUserToResizeRows = false;
        wordPreviewGrid.MultiSelect = false;
        wordPreviewGrid.ReadOnly = true;
        wordPreviewGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        wordPreviewGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        wordPreviewGrid.ScrollBars = ScrollBars.Vertical;
        wordPreviewGrid.GridColor = Color.FromArgb(68, 68, 68);
        wordPreviewGrid.DefaultCellStyle.BackColor = Color.FromArgb(53, 53, 53);
        wordPreviewGrid.DefaultCellStyle.ForeColor = Color.White;
        wordPreviewGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(68, 74, 80);
        wordPreviewGrid.DefaultCellStyle.SelectionForeColor = Color.White;
        wordPreviewGrid.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
        wordPreviewGrid.RowTemplate.Height = 30;
        wordPreviewGrid.RowTemplate.Resizable = DataGridViewTriState.False;
        wordPreviewGrid.AdvancedCellBorderStyle.Left = DataGridViewAdvancedCellBorderStyle.None;
        wordPreviewGrid.AdvancedCellBorderStyle.Right = DataGridViewAdvancedCellBorderStyle.None;
        wordPreviewGrid.AdvancedCellBorderStyle.Top = DataGridViewAdvancedCellBorderStyle.None;
        wordPreviewGrid.AdvancedCellBorderStyle.Bottom = DataGridViewAdvancedCellBorderStyle.None;
        wordPreviewGrid.CellClick += WordPreviewGridOnCellContentClick;
        wordPreviewGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Word",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 100,
            ReadOnly = true
        });
        wordPreviewGrid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Remove",
            Text = "X",
            UseColumnTextForButtonValue = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 54,
            MinimumWidth = 54,
            ReadOnly = true,
            FlatStyle = FlatStyle.Flat
        });
        wordPreviewGrid.Columns["Remove"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        previewModal.Controls.Add(wordPreviewGrid);

        previewDownloadButton.Text = "DOWNLOAD (0)";
        previewDownloadButton.Left = 14;
        previewDownloadButton.Top = 276;
        previewDownloadButton.Width = 302;
        previewDownloadButton.Height = 30;
        previewDownloadButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        previewDownloadButton.FlatStyle = FlatStyle.Flat;
        previewDownloadButton.BackColor = Color.FromArgb(54, 61, 66);
        previewDownloadButton.ForeColor = Color.FromArgb(126, 137, 145);
        previewDownloadButton.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        previewDownloadButton.FlatAppearance.BorderColor = Color.FromArgb(96, 108, 118);
        previewDownloadButton.Enabled = false;
        previewDownloadButton.Click += async delegate
        {
            previewModal.Visible = false;
            await StartDownloadsAsync(false);
        };
        previewModal.Controls.Add(previewDownloadButton);

        previewCancelButton.Text = "CANCEL";
        previewCancelButton.Left = 330;
        previewCancelButton.Top = 276;
        previewCancelButton.Width = 302;
        previewCancelButton.Height = 30;
        previewCancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        previewCancelButton.FlatStyle = FlatStyle.Flat;
        previewCancelButton.BackColor = Color.FromArgb(64, 64, 64);
        previewCancelButton.ForeColor = Color.White;
        previewCancelButton.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        previewCancelButton.FlatAppearance.BorderColor = Color.FromArgb(170, 170, 170);
        previewCancelButton.Click += delegate
        {
            previewModal.Visible = false;
            RestorePlaceholder();
            inputBox.Focus();
        };
        previewModal.Controls.Add(previewCancelButton);

        progressBar.Left = 30;
        progressBar.Top = 270;
        progressBar.Width = 492;
        progressBar.Height = 22;
        progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        progressBar.Style = ProgressBarStyle.Continuous;
        queuePanel.Controls.Add(progressBar);

        doneButton.Text = "\uE73E";
        doneButton.Width = 42;
        doneButton.Height = 32;
        doneButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        doneButton.FlatStyle = FlatStyle.Flat;
        doneButton.BackColor = Color.FromArgb(64, 64, 64);
        doneButton.ForeColor = Color.White;
        doneButton.Font = new Font("Segoe MDL2 Assets", 13f);
        doneButton.FlatAppearance.BorderColor = Color.FromArgb(170, 170, 170);
        doneButton.Cursor = Cursors.Hand;
        doneButton.Visible = false;
        doneButton.Click += delegate
        {
            doneButton.Visible = false;
            previewModal.Visible = false;
            progressBar.Value = 0;
            emptyPanel.Visible = true;
            queuePanel.Visible = false;
            statusLabel.Text = "Ready";
            inputBox.Clear();
            showingPlaceholder = false;
            RestorePlaceholder();
            inputBox.Focus();
        };
        queuePanel.Controls.Add(doneButton);

        logBox.Left = 30;
        logBox.Top = 304;
        logBox.Width = 650;
        logBox.Height = 190;
        logBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        logBox.Multiline = true;
        logBox.ScrollBars = ScrollBars.Vertical;
        logBox.ReadOnly = true;
        logBox.BorderStyle = BorderStyle.None;
        logBox.BackColor = Color.FromArgb(50, 50, 50);
        logBox.ForeColor = Color.Gainsboro;
        logBox.Font = new Font("Consolas", 9f);
        queuePanel.Controls.Add(logBox);

        queuePanel.Resize += delegate { LayoutQueuePanel(); };
        LayoutQueuePanel();
    }

    private void LayoutQueuePanel()
    {
        int margin = 30;
        int width = Math.Max(100, queuePanel.ClientSize.Width - (margin * 2));
        int topAreaHeight = Math.Min(318, Math.Max(236, queuePanel.ClientSize.Height - 194));
        int modalWidth = width;

        previewModal.Left = margin;
        previewModal.Top = 22;
        previewModal.Width = modalWidth;
        previewModal.Height = topAreaHeight;

        wordPreviewGrid.Left = 14;
        wordPreviewGrid.Top = 48;
        wordPreviewGrid.Width = Math.Max(80, previewModal.ClientSize.Width - 28);
        wordPreviewGrid.Height = Math.Max(60, previewModal.ClientSize.Height - 104);
        previewCountLabel.Left = Math.Max(220, previewModal.ClientSize.Width - 310);
        clearListButton.Left = Math.Max(330, previewModal.ClientSize.Width - clearListButton.Width - 16);
        previewDownloadButton.Left = 14;
        previewDownloadButton.Top = previewModal.ClientSize.Height - 40;
        previewDownloadButton.Width = Math.Max(120, (previewModal.ClientSize.Width - 42) / 2);
        previewCancelButton.Left = previewDownloadButton.Right + 14;
        previewCancelButton.Top = previewDownloadButton.Top;
        previewCancelButton.Width = Math.Max(120, previewModal.ClientSize.Width - previewCancelButton.Left - 14);

        logBox.Left = margin;
        logBox.Top = previewModal.Bottom + 46;
        logBox.Width = width;
        logBox.Height = Math.Max(80, queuePanel.ClientSize.Height - logBox.Top - 20);

        progressBar.Left = margin;
        progressBar.Top = previewModal.Bottom + 14;
        progressBar.Width = Math.Max(120, queuePanel.ClientSize.Width - progressBar.Left - margin - (doneButton.Visible ? doneButton.Width + 12 : 0));

        doneButton.Left = queuePanel.ClientSize.Width - margin - doneButton.Width;
        doneButton.Top = queuePanel.ClientSize.Height - 44;
    }

    private void InputBoxOnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && e.Shift && e.Control)
        {
            ShowBulkInputWindow();
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Enter && e.Shift)
        {
            AddCurrentInputToQueue();
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            BeginInvoke(new Action(ShowDownloadPreviewFromInput));
        }
    }

    private void RebuildPreviewRows()
    {
        wordPreviewGrid.Rows.Clear();
        foreach (string item in queue)
            wordPreviewGrid.Rows.Add(item);
    }

    private void WordPreviewGridOnCellContentClick(object sender, DataGridViewCellEventArgs e)
    {
        if (downloading)
            return;

        if (e.RowIndex < 0 || e.ColumnIndex != wordPreviewGrid.Columns["Remove"].Index)
            return;

        if (e.RowIndex < queue.Count)
        {
            queue.RemoveAt(e.RowIndex);
            RefreshQueueUi();
        }
    }

    private void ShowBulkInputWindow()
    {
        using (Form bulkForm = new Form())
        using (TextBox bulkBox = new TextBox())
        using (Button addButton = new Button())
        using (Button cancelButton = new Button())
        {
            bulkForm.Text = "Add Words or URLs";
            bulkForm.StartPosition = FormStartPosition.CenterParent;
            bulkForm.Size = new Size(520, 420);
            bulkForm.MinimumSize = new Size(420, 320);
            bulkForm.BackColor = Color.FromArgb(64, 64, 64);
            bulkForm.Font = new Font("Segoe UI", 10f);
            bulkForm.ShowIcon = false;
            bulkForm.ShowInTaskbar = false;

            bulkBox.Multiline = true;
            bulkBox.ScrollBars = ScrollBars.Vertical;
            bulkBox.AcceptsReturn = true;
            bulkBox.AcceptsTab = false;
            bulkBox.BorderStyle = BorderStyle.None;
            bulkBox.BackColor = Color.FromArgb(50, 50, 50);
            bulkBox.ForeColor = Color.WhiteSmoke;
            bulkBox.Font = new Font("Segoe UI", 10f);
            bulkBox.Left = 16;
            bulkBox.Top = 16;
            bulkBox.Width = 472;
            bulkBox.Height = 300;
            bulkBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            addButton.Text = "Add to queue";
            addButton.Width = 120;
            addButton.Height = 32;
            addButton.Left = 248;
            addButton.Top = 332;
            addButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            addButton.FlatStyle = FlatStyle.Flat;
            addButton.BackColor = Color.FromArgb(64, 64, 64);
            addButton.ForeColor = Color.WhiteSmoke;
            addButton.DialogResult = DialogResult.OK;

            cancelButton.Text = "Cancel";
            cancelButton.Width = 100;
            cancelButton.Height = 32;
            cancelButton.Left = 388;
            cancelButton.Top = 332;
            cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.BackColor = Color.FromArgb(64, 64, 64);
            cancelButton.ForeColor = Color.WhiteSmoke;
            cancelButton.DialogResult = DialogResult.Cancel;

            bulkForm.Controls.Add(bulkBox);
            bulkForm.Controls.Add(addButton);
            bulkForm.Controls.Add(cancelButton);
            bulkForm.CancelButton = cancelButton;
            bulkBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && e.Control)
                {
                    bulkForm.DialogResult = DialogResult.OK;
                    bulkForm.Close();
                    e.SuppressKeyPress = true;
                }
            };

            if (bulkForm.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(bulkBox.Text))
                AddItemsFromText(bulkBox.Text);
        }
    }

    private void ClearPlaceholder()
    {
        if (!showingPlaceholder)
            return;

        inputBox.Clear();
        inputBox.ForeColor = Color.WhiteSmoke;
        showingPlaceholder = false;
    }

    private void RestorePlaceholder()
    {
        if (!string.IsNullOrWhiteSpace(inputBox.Text))
            return;

        inputBox.Text = "URL or search query";
        inputBox.ForeColor = Color.DarkGray;
        showingPlaceholder = true;
    }

    private void AddCurrentInputToQueue()
    {
        if (showingPlaceholder || string.IsNullOrWhiteSpace(inputBox.Text))
            return;

        AddItemsFromText(inputBox.Text);
        inputBox.Clear();
        showingPlaceholder = false;
    }

    private void AddItemsFromText(string text)
    {
        foreach (string item in DictionaryAudioDownloader.SplitInputItems(text))
        {
            if (!queue.Any(existing => string.Equals(existing, item, StringComparison.OrdinalIgnoreCase)))
                queue.Add(item);
        }

        RefreshQueueUi();
    }

    private void RefreshQueueUi()
    {
        RebuildPreviewRows();

        bool hasItems = queue.Count > 0;
        emptyPanel.Visible = !hasItems;
        queuePanel.Visible = hasItems;
        statusLabel.Text = hasItems ? queue.Count + " item(s) ready" : "Ready";
        previewCountLabel.Text = hasItems ? queue.Count + " ready" : "No words ready";
        previewDownloadButton.Text = "DOWNLOAD (" + queue.Count + ")";
        previewDownloadButton.Enabled = hasItems && !downloading;
        previewDownloadButton.BackColor = previewDownloadButton.Enabled ? Color.FromArgb(64, 64, 64) : Color.FromArgb(54, 61, 66);
        previewDownloadButton.ForeColor = previewDownloadButton.Enabled ? Color.White : Color.FromArgb(126, 137, 145);
        clearListButton.Enabled = hasItems && !downloading;
        foreach (DataGridViewRow row in wordPreviewGrid.Rows)
            row.Cells["Remove"].ReadOnly = downloading;
        progressBar.Minimum = 0;
        progressBar.Maximum = Math.Max(1, queue.Count);
        progressBar.Value = 0;
        wordPreviewGrid.Invalidate();
    }

    private void ShowDownloadPreviewFromInput()
    {
        if (!showingPlaceholder && !string.IsNullOrWhiteSpace(inputBox.Text))
        {
            AddItemsFromText(inputBox.Text);
            inputBox.Clear();
            showingPlaceholder = false;
        }

        if (queue.Count == 0)
        {
            statusLabel.Text = "Paste a URL or word first";
            return;
        }

        doneButton.Visible = false;
        emptyPanel.Visible = false;
        queuePanel.Visible = true;
        previewModal.Visible = true;
        previewModal.BringToFront();
        RefreshQueueUi();
    }

    public void RunQueueSelfTest()
    {
        inputBox.Focus();
        inputBox.Text = "devil";
        showingPlaceholder = false;
        AddCurrentInputToQueue();

        if (queue.Count != 1 || wordPreviewGrid.Rows.Count != 1 || Convert.ToString(wordPreviewGrid.Rows[0].Cells[0].Value) != "devil")
            throw new InvalidOperationException("Shift+Enter single-word queue path failed.");

        AddItemsFromText("toe big, apple; banana\r\nhttps://www.dictionary.com/browse/computer");

        if (queue.Count != 6 || wordPreviewGrid.Rows.Count != 6)
            throw new InvalidOperationException("Bulk queue path failed. Expected 6 items, got " + wordPreviewGrid.Rows.Count + ".");

        Console.WriteLine("Queue self-test OK");
        Console.WriteLine("Queued items: " + wordPreviewGrid.Rows.Count);
        foreach (DataGridViewRow item in wordPreviewGrid.Rows)
            Console.WriteLine(item.Cells[0].Value);
    }

    public void RunVisibleQueueSelfTest()
    {
        AddItemsFromText("my salsa all dance");

        string[] expected = { "my", "salsa", "all", "dance" };
        if (wordPreviewGrid.Rows.Count != expected.Length)
            throw new InvalidOperationException("Expected " + expected.Length + " visible queue rows, got " + wordPreviewGrid.Rows.Count + ".");

        for (int i = 0; i < expected.Length; i++)
        {
            string actual = Convert.ToString(wordPreviewGrid.Rows[i].Cells[0].Value);
            if (actual != expected[i])
                throw new InvalidOperationException("Queue row " + i + " expected " + expected[i] + ", got " + actual + ".");
        }

        AddItemsFromText("one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen");
        if (wordPreviewGrid.Rows.Count != 19)
            throw new InvalidOperationException("Scrollable queue test expected 19 rows, got " + wordPreviewGrid.Rows.Count + ".");

        Console.WriteLine("Visible queue self-test OK");
        Console.WriteLine("First four visible rows:");
        for (int i = 0; i < expected.Length; i++)
            Console.WriteLine(wordPreviewGrid.Rows[i].Cells[0].Value);
        Console.WriteLine("Total rows: " + wordPreviewGrid.Rows.Count);
    }

    private async Task StartDownloadsAsync(bool collectInput)
    {
        if (downloading)
            return;

        if (collectInput && !showingPlaceholder && !string.IsNullOrWhiteSpace(inputBox.Text))
        {
            AddItemsFromText(inputBox.Text);
            inputBox.Clear();
            showingPlaceholder = false;
        }

        if (queue.Count == 0)
        {
            statusLabel.Text = "Paste a URL or word first";
            return;
        }

        downloading = true;
        downloadButton.Enabled = false;
        previewDownloadButton.Enabled = false;
        clearListButton.Enabled = false;
        inputBox.Enabled = false;
        emptyPanel.Visible = false;
        queuePanel.Visible = true;
        previewModal.Visible = true;
        previewModal.BringToFront();
        doneButton.Visible = false;
        LayoutQueuePanel();

        List<string> items = queue.ToList();
        List<string> failures = new List<string>();
        int successCount = 0;
        int failureCount = 0;
        progressBar.Maximum = Math.Max(1, items.Count);
        progressBar.Value = 0;
        AddLog("Starting " + items.Count + " download(s)");

        for (int i = 0; i < items.Count; i++)
        {
            string item = items[i];
            try
            {
                statusLabel.Text = "Resolving " + (i + 1) + " of " + items.Count + ": " + item;
                DownloadResult result = await Task.Run(() => DictionaryAudioDownloader.Download(item));
                AddLog("Saved: " + result.OutputFile);
                AddLog("Source: " + result.SourceUrl);
                successCount++;
            }
            catch (Exception ex)
            {
                AddLog("Failed: " + item);
                AddLog("  " + ex.Message);
                failures.Add(item + ": " + ex.Message);
                failureCount++;
            }

            progressBar.Value = Math.Min(i + 1, progressBar.Maximum);
        }

        queue.Clear();
        statusLabel.Text = "Done. Saved " + successCount + " file(s), failed " + failureCount + " item(s).";
        AddLog("Done. Output folder: " + DictionaryAudioDownloader.OutputDirectory);

        if (failures.Count > 0)
        {
            string message = "The following item(s) could not be downloaded:" + Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, failures.ToArray());
            MessageBox.Show(this, message, "Dictionary Audio Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        downloadButton.Enabled = true;
        inputBox.Enabled = true;
        downloading = false;
        doneButton.Visible = true;
        LayoutQueuePanel();
        inputBox.Focus();
    }

    private void AddLog(string text)
    {
        logBox.AppendText(text + Environment.NewLine);
    }
}

internal sealed class PlayLogoPanel : Panel
{
    public PlayLogoPanel()
    {
        BackColor = Color.FromArgb(64, 64, 64);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle rect = new Rectangle(8, 8, 214, 132);
        using (GraphicsPath path = RoundedRectangle(rect, 34))
        using (SolidBrush brush = new SolidBrush(Color.FromArgb(92, 92, 92)))
            e.Graphics.FillPath(brush, path);

        Point[] points =
        {
            new Point(90, 50),
            new Point(90, 106),
            new Point(146, 78)
        };

        using (SolidBrush brush = new SolidBrush(Color.FromArgb(54, 54, 54)))
            e.Graphics.FillPolygon(brush, points);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, radius, radius, 180, 90);
        path.AddArc(bounds.Right - radius, bounds.Top, radius, radius, 270, 90);
        path.AddArc(bounds.Right - radius, bounds.Bottom - radius, radius, radius, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - radius, radius, radius, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal static class DictionaryAudioDownloader
{
    public static readonly string OutputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloaded MP3s");

    private const string DictionaryBrowseBase = "https://www.dictionary.com/browse/";
    private const string DictionaryAudioBase = "https://assets.dictionary.com/audio/";

    public static DownloadResult Download(string originalInput)
    {
        Directory.CreateDirectory(OutputDirectory);

        Uri audioUrl;
        try
        {
            audioUrl = ResolveAudioUrl(originalInput);
        }
        catch (Exception ex)
        {
            string trimmed = (originalInput ?? string.Empty).Trim();
            if (IsPlainWord(trimmed))
                throw new InvalidOperationException("The word \"" + trimmed + "\" is not available in the library.", ex);

            throw;
        }

        string outputFile = GetOutputFile(originalInput, audioUrl);
        Uri savedFromUrl = SaveAudioFile(audioUrl, outputFile);

        return new DownloadResult(outputFile, savedFromUrl.ToString());
    }

    public static void RunSelfTest()
    {
        DownloadResult result = Download("fartlek");
        FileInfo file = new FileInfo(result.OutputFile);
        Console.WriteLine("Self-test OK");
        Console.WriteLine("Input: fartlek");
        Console.WriteLine("Source: " + result.SourceUrl);
        Console.WriteLine("Saved: " + file.FullName);
        Console.WriteLine("Bytes: " + file.Length);
    }

    public static void RunOptionsSelfTest()
    {
        string[] bulkItems = SplitInputItems("fartlek\r\nhttps://www.dictionary.com/browse/fartlek\r\ndata-audiosrc=\"NEW/NEW11700.mp3\"");
        if (bulkItems.Length != 3)
            throw new InvalidOperationException("Bulk input parser failed. Expected 3 items, got " + bulkItems.Length + ".");

        List<DownloadResult> results = new List<DownloadResult>();
        results.Add(Download("fartlek"));
        results.Add(Download("https://www.dictionary.com/browse/fartlek"));

        foreach (string item in bulkItems)
            results.Add(Download(item));

        Console.WriteLine("Options self-test OK");
        Console.WriteLine("Bulk items parsed: " + bulkItems.Length);
        foreach (DownloadResult result in results)
        {
            FileInfo file = new FileInfo(result.OutputFile);
            Console.WriteLine(file.Name + " | " + file.Length + " bytes | " + result.SourceUrl);
        }
    }

    public static void RunBulkWordsSelfTest()
    {
        string[] words = SplitInputItems("devil toe big dog cat word");
        if (words.Length != 6)
            throw new InvalidOperationException("Bulk word parser failed. Expected 6 words, got " + words.Length + ".");

        Console.WriteLine("Bulk word parser OK: " + string.Join(", ", words));
        int successCount = 0;
        int failureCount = 0;
        foreach (string word in words)
        {
            try
            {
                DownloadResult result = Download(word);
                FileInfo file = new FileInfo(result.OutputFile);
                Console.WriteLine(file.Name + " | " + file.Length + " bytes | " + result.SourceUrl);
                successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAILED " + word + " | " + ex.Message);
                failureCount++;
            }
        }

        Console.WriteLine("Bulk word download test done. Saved " + successCount + ", failed " + failureCount + ".");
        if (successCount == 0)
            throw new InvalidOperationException("No bulk words downloaded.");
    }

    private static Uri ResolveAudioUrl(string text)
    {
        string trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new InvalidOperationException("Input is blank.");

        Match directMatch = Regex.Match(trimmed, "https?://(?:static\\.sfdict|assets\\.dictionary)\\.com/audio/[^'\"\\s<>]+?\\.mp3", RegexOptions.IgnoreCase);
        if (directMatch.Success)
            return new Uri(directMatch.Value);

        Match attrMatch = Regex.Match(trimmed, "data-audiosrc\\s*=\\s*[\"']?(?<path>[^'\"\\s<>]+?\\.mp3)", RegexOptions.IgnoreCase);
        if (attrMatch.Success)
            return new Uri(DictionaryAudioBase + attrMatch.Groups["path"].Value.TrimStart('/'));

        Match relativeMatch = Regex.Match(trimmed, "([A-Z0-9]{3}/[A-Z0-9]+/[A-Z0-9]+\\.mp3|[A-Z0-9]{3}/[A-Z0-9]+\\.mp3)", RegexOptions.IgnoreCase);
        if (relativeMatch.Success)
            return new Uri(DictionaryAudioBase + relativeMatch.Value.TrimStart('/'));

        Uri uri;
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out uri))
        {
            if (uri.AbsoluteUri.StartsWith(DictionaryBrowseBase, StringComparison.OrdinalIgnoreCase))
            {
                string pageText = DownloadString(uri);
                Uri pageAudio = ResolveAudioUrl(pageText);
                if (pageAudio != null)
                    return pageAudio;

                throw new InvalidOperationException("No data-audiosrc value was found on the Dictionary.com page.");
            }

            if (uri.AbsoluteUri.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                return uri;
        }

        string word = trimmed.Trim('/');
        if (IsPlainWord(word))
        {
            string encodedWord = HttpUtility.UrlEncode(word).Replace("+", "%20");
            return ResolveAudioUrl(DictionaryBrowseBase + encodedWord);
        }

        throw new InvalidOperationException("Could not understand input: " + trimmed);
    }

    private static string DownloadString(Uri uri)
    {
        return RunCurl("-L -f -sS -A \"Mozilla/5.0 DictionaryAudioDownloader/1.0\" " + Quote(uri.ToString()), true);
    }

    private static Uri SaveAudioFile(Uri audioUrl, string outputFile)
    {
        try
        {
            DownloadFileWithCurl(audioUrl, outputFile);
            return audioUrl;
        }
        catch
        {
            Uri fallbackUrl = GetFallbackAudioUrl(audioUrl);
            if (fallbackUrl == null)
                throw;

            DownloadFileWithCurl(fallbackUrl, outputFile);
            return fallbackUrl;
        }
    }

    private static void DownloadFileWithCurl(Uri uri, string outputFile)
    {
        RunCurl("-L -f -sS -A \"Mozilla/5.0 DictionaryAudioDownloader/1.0\" -o " + Quote(outputFile) + " " + Quote(uri.ToString()), false);
    }

    private static string RunCurl(string arguments, bool captureOutput)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "curl.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = true
        };

        using (Process process = Process.Start(startInfo))
        {
            string output = captureOutput ? process.StandardOutput.ReadToEnd() : string.Empty;
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "curl.exe failed with exit code " + process.ExitCode + "." : error.Trim());

            return output;
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static Uri GetFallbackAudioUrl(Uri audioUri)
    {
        string path = audioUri.AbsolutePath.TrimStart('/');
        Match match = Regex.Match(path, "^audio/(?<group>[^/]+)/(?<id>[^/]+)/\\k<id>\\.mp3$", RegexOptions.IgnoreCase);
        if (match.Success)
            return new Uri(DictionaryAudioBase + match.Groups["group"].Value + "/" + match.Groups["id"].Value + ".mp3");

        match = Regex.Match(path, "^(?<group>[^/]+)/(?<id>[^/]+)/\\k<id>\\.mp3$", RegexOptions.IgnoreCase);
        if (match.Success)
            return new Uri(DictionaryAudioBase + match.Groups["group"].Value + "/" + match.Groups["id"].Value + ".mp3");

        return null;
    }

    private static string GetOutputFile(string originalInput, Uri audioUri)
    {
        string name = null;
        Uri originalUri;
        if (Uri.TryCreate((originalInput ?? string.Empty).Trim(), UriKind.Absolute, out originalUri))
            name = GetWordFromBrowseUrl(originalUri);

        string trimmed = (originalInput ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) && IsPlainWord(trimmed))
            name = trimmed;

        if (string.IsNullOrWhiteSpace(name))
            name = Path.GetFileNameWithoutExtension(audioUri.AbsolutePath);

        string safeName = ConvertToSafeFileName(name);
        string path = Path.Combine(OutputDirectory, safeName + ".mp3");
        int counter = 2;
        while (File.Exists(path))
        {
            path = Path.Combine(OutputDirectory, safeName + "-" + counter + ".mp3");
            counter++;
        }

        return path;
    }

    private static string GetWordFromBrowseUrl(Uri uri)
    {
        if (!uri.AbsoluteUri.StartsWith(DictionaryBrowseBase, StringComparison.OrdinalIgnoreCase))
            return null;

        string word = Regex.Replace(uri.AbsolutePath, "^/browse/", string.Empty, RegexOptions.IgnoreCase);
        return HttpUtility.UrlDecode(word).Trim('/');
    }

    private static string ConvertToSafeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new string((name ?? string.Empty).Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim(' ', '.');
        return string.IsNullOrWhiteSpace(safe) ? "dictionary-audio" : safe;
    }

    private static bool IsPlainWord(string text)
    {
        return !string.IsNullOrWhiteSpace(text) && Regex.IsMatch(text, @"^[\p{L}\p{N}' -]+$");
    }

    public static string[] SplitInputItems(string text)
    {
        List<string> items = new List<string>();
        foreach (string rawLine in Regex.Split(text ?? string.Empty, "\r?\n"))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (LooksLikeSingleSourceInput(line))
            {
                items.Add(line);
                continue;
            }

            foreach (string part in Regex.Split(line, @"[\s,;]+").Select(item => item.Trim()).Where(item => item.Length > 0))
                items.Add(part);
        }

        return items.ToArray();
    }

    private static bool LooksLikeSingleSourceInput(string text)
    {
        return text.IndexOf("://", StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf("data-audiosrc", StringComparison.OrdinalIgnoreCase) >= 0 ||
            Regex.IsMatch(text, @"[A-Z0-9]{3}/[A-Z0-9]+(?:/[A-Z0-9]+)?\.mp3", RegexOptions.IgnoreCase);
    }
}

internal sealed class DownloadResult
{
    public DownloadResult(string outputFile, string sourceUrl)
    {
        OutputFile = outputFile;
        SourceUrl = sourceUrl;
    }

    public string OutputFile { get; private set; }
    public string SourceUrl { get; private set; }
}
