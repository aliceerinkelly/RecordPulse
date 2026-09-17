using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;

namespace RecordPulse
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup Error:\n\n{ex}", "RecordPulse Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public class ContactRecord
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Job { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string CustomJson { get; set; } = "{}";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class MainForm : Form
    {
        private string currentDbPath = "records.db";

        // UI Controls
        private TextBox txtSearch = null!;
        private DataGridView grid = null!;
        private Label lblStatus = null!;
        private Label lblDbName = null!;

        // Theme Colors
        private readonly Color bgDark = Color.FromArgb(24, 24, 27);
        private readonly Color panelDark = Color.FromArgb(39, 39, 42);
        private readonly Color inputBg = Color.FromArgb(50, 50, 56);
        private readonly Color accent = Color.FromArgb(14, 165, 233);
        private readonly Color accentHover = Color.FromArgb(56, 189, 248);
        private readonly Color textLight = Color.FromArgb(244, 244, 245);
        private readonly Color textMuted = Color.FromArgb(161, 161, 170);

        public MainForm()
        {
            InitializeComponent();
            this.Load += MainForm_Load;
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            try
            {
                EnsureDatabaseSchema();
                LoadRecords();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Initialization Error:\n\n{ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeComponent()
        {
            this.Text = "RecordPulse - Entity Management";
            this.Size = new Size(1250, 750);
            this.MinimumSize = new Size(900, 550);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = bgDark;
            this.ForeColor = textLight;
            this.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;

            // Top Header Bar
            FlowLayoutPanel topBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = panelDark,
                Padding = new Padding(12, 10, 12, 10),
                WrapContents = false
            };

            Label lblTitle = new Label
            {
                Text = "RECORDPULSE",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = accent,
                AutoSize = true,
                Margin = new Padding(0, 5, 14, 0)
            };

            Button btnSwitchDb = CreateButton("Switch DB", 100, 34);
            btnSwitchDb.Click += BtnSwitchDb_Click;

            lblDbName = new Label
            {
                Text = $"DB: {Path.GetFileName(currentDbPath)}",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                ForeColor = textMuted,
                AutoSize = true,
                Margin = new Padding(6, 7, 16, 0)
            };

            txtSearch = new TextBox
            {
                PlaceholderText = "Search records...",
                BackColor = inputBg,
                ForeColor = textLight,
                BorderStyle = BorderStyle.FixedSingle,
                Width = 260,
                Margin = new Padding(0, 3, 14, 0)
            };
            txtSearch.TextChanged += (s, e) => PerformSearch(txtSearch.Text);

            Button btnAdd = CreateButton("+ Add Record", 120, 34);
            btnAdd.BackColor = Color.FromArgb(16, 110, 170);
            btnAdd.Click += (s, e) => OpenEditor(null);

            Button btnEdit = CreateButton("Edit", 80, 34);
            btnEdit.Click += BtnEdit_Click;

            Button btnDelete = CreateButton("Delete", 80, 34);
            btnDelete.Click += BtnDelete_Click;

            Button btnImport = CreateButton("Import", 85, 34);
            btnImport.Click += BtnImport_Click;

            Button btnExport = CreateButton("Export", 85, 34);
            btnExport.Click += BtnExport_Click;

            Button btnEmail = CreateButton("Email", 80, 34);
            btnEmail.Click += BtnEmail_Click;

            topBar.Controls.AddRange(new Control[] { lblTitle, btnSwitchDb, lblDbName, txtSearch, btnAdd, btnEdit, btnDelete, btnImport, btnExport, btnEmail });

            // Bottom Status Bar
            Panel statusBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColor = panelDark,
                Padding = new Padding(12, 6, 12, 6)
            };
            lblStatus = new Label
            {
                Text = "Ready. Double-click any row to edit, or click '+ Add Record'.",
                ForeColor = textMuted,
                Dock = DockStyle.Fill
            };
            statusBar.Controls.Add(lblStatus);

            // Center DataGrid (Full Window Width)
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = bgDark,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = panelDark;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = textLight;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            grid.ColumnHeadersHeight = 36;

            grid.DefaultCellStyle.BackColor = bgDark;
            grid.DefaultCellStyle.ForeColor = textLight;
            grid.DefaultCellStyle.SelectionBackColor = accent;
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(30, 30, 34);

            grid.DataBindingComplete += Grid_DataBindingComplete;
            grid.CellDoubleClick += (s, e) => BtnEdit_Click(s, e);

            this.Controls.Add(grid);
            this.Controls.Add(topBar);
            this.Controls.Add(statusBar);
        }

        private void Grid_DataBindingComplete(object? sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (grid.Columns.Count == 0) return;

            if (grid.Columns.Contains("CustomJson")) grid.Columns["CustomJson"].Visible = false;
            if (grid.Columns.Contains("CreatedAt")) grid.Columns["CreatedAt"].Visible = false;

            if (grid.Columns.Contains("Id")) { grid.Columns["Id"].FillWeight = 40; grid.Columns["Id"].HeaderText = "ID"; }
            if (grid.Columns.Contains("Name")) { grid.Columns["Name"].FillWeight = 130; grid.Columns["Name"].HeaderText = "Name"; }
            if (grid.Columns.Contains("Email")) { grid.Columns["Email"].FillWeight = 140; grid.Columns["Email"].HeaderText = "Email"; }
            if (grid.Columns.Contains("Phone")) { grid.Columns["Phone"].FillWeight = 95; grid.Columns["Phone"].HeaderText = "Phone"; }
            if (grid.Columns.Contains("Job")) { grid.Columns["Job"].FillWeight = 120; grid.Columns["Job"].HeaderText = "Job / Company"; }
            if (grid.Columns.Contains("Address")) { grid.Columns["Address"].FillWeight = 150; grid.Columns["Address"].HeaderText = "Address / City"; }
        }

        private Button CreateButton(string text, int width, int height)
        {
            Button b = new Button
            {
                Text = text,
                Size = new Size(width, height),
                FlatStyle = FlatStyle.Flat,
                BackColor = panelDark,
                ForeColor = textLight,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(82, 82, 91);
            b.MouseEnter += (s, e) => b.BackColor = accentHover;
            b.MouseLeave += (s, e) => b.BackColor = panelDark;
            return b;
        }

        // ================= SQLite Backend =================

        private void EnsureDatabaseSchema()
        {
            using var conn = new SqliteConnection($"Data Source={currentDbPath};");
            conn.Open();

            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('Records', 'Contacts');";
            var existingTable = checkCmd.ExecuteScalar()?.ToString();

            if (string.IsNullOrEmpty(existingTable))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Records (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Email TEXT,
                        Phone TEXT,
                        Job TEXT,
                        Address TEXT,
                        CustomJson TEXT,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    );";
                cmd.ExecuteNonQuery();
            }
        }

        private void LoadRecords()
        {
            PerformSearch(string.Empty);
        }

        private void PerformSearch(string query)
        {
            List<ContactRecord> list = new();
            try
            {
                using var conn = new SqliteConnection($"Data Source={currentDbPath};");
                conn.Open();

                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('Records', 'Contacts') LIMIT 1;";
                var tableName = checkCmd.ExecuteScalar()?.ToString() ?? "Records";

                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var pragmaCmd = conn.CreateCommand())
                {
                    pragmaCmd.CommandText = $"PRAGMA table_info({tableName});";
                    using var rdr = pragmaCmd.ExecuteReader();
                    while (rdr.Read()) columns.Add(rdr.GetString(1));
                }

                bool hasName = columns.Contains("Name");
                bool hasFirst = columns.Contains("FirstName");
                bool hasJob = columns.Contains("Job");
                bool hasCompany = columns.Contains("Company");
                bool hasAddress = columns.Contains("Address");
                bool hasCity = columns.Contains("City");
                bool hasCustom = columns.Contains("CustomJson");
                bool hasNotes = columns.Contains("Notes");

                string nameSelect = hasName ? "Name" : (hasFirst ? "(FirstName || ' ' || LastName)" : "''");
                string jobSelect = hasJob ? "Job" : (hasCompany ? "Company" : "''");
                string addrSelect = hasAddress ? "Address" : (hasCity ? "City" : "''");
                string customSelect = hasCustom ? "CustomJson" : (hasNotes ? "Notes" : "''");

                using var cmd = conn.CreateCommand();
                if (string.IsNullOrWhiteSpace(query))
                {
                    cmd.CommandText = $"SELECT Id, {nameSelect} AS Name, Email, Phone, {jobSelect} AS Job, {addrSelect} AS Address, {customSelect} AS CustomJson FROM {tableName} ORDER BY Id DESC LIMIT 1000;";
                }
                else
                {
                    cmd.CommandText = $@"
                        SELECT Id, {nameSelect} AS Name, Email, Phone, {jobSelect} AS Job, {addrSelect} AS Address, {customSelect} AS CustomJson
                        FROM {tableName}
                        WHERE {nameSelect} LIKE @Contains 
                           OR Email LIKE @Contains 
                           OR Phone LIKE @Contains 
                           OR {jobSelect} LIKE @Contains 
                           OR {addrSelect} LIKE @Contains
                        ORDER BY Id DESC
                        LIMIT 500;";
                    cmd.Parameters.AddWithValue("@Contains", $"%{query.Trim()}%");
                }

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new ContactRecord
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        Email = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Phone = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        Job = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        Address = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        CustomJson = reader.IsDBNull(6) ? "{}" : reader.GetString(6),
                        CreatedAt = DateTime.Now
                    });
                }

                grid.DataSource = list;
                lblStatus.Text = $"Loaded {list.Count} records from {Path.GetFileName(currentDbPath)}.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Query Error: {ex.Message}";
            }
        }

        // ================= Popup Dialog Editor =================

        private void BtnEdit_Click(object? sender, EventArgs e)
        {
            if (grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a record from the table to edit.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (grid.SelectedRows[0].DataBoundItem is ContactRecord rec)
            {
                OpenEditor(rec);
            }
        }

        private void OpenEditor(ContactRecord? existingRecord)
        {
            using var editor = new RecordEditorForm(existingRecord);
            if (editor.ShowDialog(this) == DialogResult.OK)
            {
                var record = editor.RecordData;
                CommitRecord(record);
            }
        }

        private void CommitRecord(ContactRecord record)
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={currentDbPath};");
                conn.Open();

                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('Records', 'Contacts') LIMIT 1;";
                var tableName = checkCmd.ExecuteScalar()?.ToString() ?? "Records";

                using var cmd = conn.CreateCommand();

                if (record.Id > 0)
                {
                    if (tableName == "Contacts")
                    {
                        var names = record.Name.Split(' ', 2);
                        string first = names.Length > 0 ? names[0] : "";
                        string last = names.Length > 1 ? names[1] : "";

                        cmd.CommandText = @"
                            UPDATE Contacts 
                            SET FirstName = @First, LastName = @Last, Email = @Email, Phone = @Phone, Company = @Job, City = @Address
                            WHERE Id = @Id;";
                        cmd.Parameters.AddWithValue("@First", first);
                        cmd.Parameters.AddWithValue("@Last", last);
                    }
                    else
                    {
                        cmd.CommandText = @"
                            UPDATE Records 
                            SET Name = @Name, Email = @Email, Phone = @Phone, Job = @Job, Address = @Address, CustomJson = @CustomJson
                            WHERE Id = @Id;";
                        cmd.Parameters.AddWithValue("@Name", record.Name);
                        cmd.Parameters.AddWithValue("@CustomJson", record.CustomJson);
                    }
                    cmd.Parameters.AddWithValue("@Id", record.Id);
                }
                else
                {
                    if (tableName == "Contacts")
                    {
                        var names = record.Name.Split(' ', 2);
                        string first = names.Length > 0 ? names[0] : "";
                        string last = names.Length > 1 ? names[1] : "";

                        cmd.CommandText = @"
                            INSERT INTO Contacts (FirstName, LastName, Email, Phone, Company, City, Notes)
                            VALUES (@First, @Last, @Email, @Phone, @Job, @Address, '');";
                        cmd.Parameters.AddWithValue("@First", first);
                        cmd.Parameters.AddWithValue("@Last", last);
                    }
                    else
                    {
                        cmd.CommandText = @"
                            INSERT INTO Records (Name, Email, Phone, Job, Address, CustomJson)
                            VALUES (@Name, @Email, @Phone, @Job, @Address, @CustomJson);";
                        cmd.Parameters.AddWithValue("@Name", record.Name);
                        cmd.Parameters.AddWithValue("@CustomJson", record.CustomJson);
                    }
                }

                cmd.Parameters.AddWithValue("@Email", record.Email);
                cmd.Parameters.AddWithValue("@Phone", record.Phone);
                cmd.Parameters.AddWithValue("@Job", record.Job);
                cmd.Parameters.AddWithValue("@Address", record.Address);

                cmd.ExecuteNonQuery();
                LoadRecords();
                lblStatus.Text = "Record committed successfully!";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database error: {ex.Message}", "Commit Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (grid.SelectedRows.Count == 0) return;

            var rec = grid.SelectedRows[0].DataBoundItem as ContactRecord;
            if (rec == null) return;

            var confirm = MessageBox.Show($"Delete '{rec.Name}' permanently?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            using var conn = new SqliteConnection($"Data Source={currentDbPath};");
            conn.Open();

            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('Records', 'Contacts') LIMIT 1;";
            var tableName = checkCmd.ExecuteScalar()?.ToString() ?? "Records";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM {tableName} WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@Id", rec.Id);
            cmd.ExecuteNonQuery();

            LoadRecords();
            lblStatus.Text = "Record deleted.";
        }

        // ================= Database Switcher =================

        private void BtnSwitchDb_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "SQLite Databases (*.db;*.sqlite)|*.db;*.sqlite|All Files (*.*)|*.*",
                Title = "Select Database File"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                currentDbPath = ofd.FileName;
                lblDbName.Text = $"DB: {Path.GetFileName(currentDbPath)}";
                EnsureDatabaseSchema();
                LoadRecords();
            }
        }

        // ================= Ingestion & File Handling =================

        private void MainForm_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[]? files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0) IngestFile(files[0]);
        }

        private void BtnImport_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Supported Data Files (*.csv;*.tsv;*.json;*.xml)|*.csv;*.tsv;*.json;*.xml|All Files (*.*)|*.*",
                Title = "Import Contacts"
            };

            if (ofd.ShowDialog() == DialogResult.OK) IngestFile(ofd.FileName);
        }

        private void IngestFile(string filePath)
        {
            try
            {
                string ext = Path.GetExtension(filePath).ToLower();
                List<ContactRecord> incoming = new();

                if (ext == ".csv" || ext == ".tsv")
                {
                    char delimiter = ext == ".tsv" ? '\t' : ',';
                    var lines = File.ReadAllLines(filePath);
                    if (lines.Length > 1)
                    {
                        var headers = lines[0].Split(delimiter).Select(h => h.Trim().ToLower()).ToArray();
                        for (int i = 1; i < lines.Length; i++)
                        {
                            if (string.IsNullOrWhiteSpace(lines[i])) continue;
                            var parts = lines[i].Split(delimiter);
                            var rec = new ContactRecord();
                            for (int c = 0; c < Math.Min(parts.Length, headers.Length); c++)
                            {
                                string val = parts[c].Trim().Trim('"');
                                string h = headers[c];
                                if (h.Contains("name")) rec.Name = val;
                                else if (h.Contains("mail")) rec.Email = val;
                                else if (h.Contains("phone")) rec.Phone = val;
                                else if (h.Contains("job") || h.Contains("title") || h.Contains("company")) rec.Job = val;
                                else if (h.Contains("address") || h.Contains("city")) rec.Address = val;
                            }
                            if (!string.IsNullOrWhiteSpace(rec.Name)) incoming.Add(rec);
                        }
                    }
                }
                else if (ext == ".json")
                {
                    string json = File.ReadAllText(filePath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            var rec = new ContactRecord();
                            foreach (var prop in el.EnumerateObject())
                            {
                                string name = prop.Name.ToLower();
                                string val = prop.Value.ToString();
                                if (name.Contains("name")) rec.Name = val;
                                else if (name.Contains("mail")) rec.Email = val;
                                else if (name.Contains("phone")) rec.Phone = val;
                                else if (name.Contains("job") || name.Contains("title") || name.Contains("company")) rec.Job = val;
                                else if (name.Contains("address") || name.Contains("city")) rec.Address = val;
                            }
                            if (!string.IsNullOrWhiteSpace(rec.Name)) incoming.Add(rec);
                        }
                    }
                }
                else if (ext == ".xml")
                {
                    XDocument xdoc = XDocument.Load(filePath);
                    foreach (var elem in xdoc.Descendants().Where(d => d.HasElements && !d.Elements().Any(e => e.HasElements)))
                    {
                        var rec = new ContactRecord();
                        foreach (var child in elem.Elements())
                        {
                            string tag = child.Name.LocalName.ToLower();
                            string val = child.Value.Trim();
                            if (tag.Contains("name")) rec.Name = val;
                            else if (tag.Contains("mail")) rec.Email = val;
                            else if (tag.Contains("phone")) rec.Phone = val;
                            else if (tag.Contains("job") || tag.Contains("title") || tag.Contains("company")) rec.Job = val;
                            else if (tag.Contains("address") || tag.Contains("city")) rec.Address = val;
                        }
                        if (!string.IsNullOrWhiteSpace(rec.Name)) incoming.Add(rec);
                    }
                }

                if (incoming.Count > 0)
                {
                    using var conn = new SqliteConnection($"Data Source={currentDbPath};");
                    conn.Open();

                    using var checkCmd = conn.CreateCommand();
                    checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('Records', 'Contacts') LIMIT 1;";
                    var tableName = checkCmd.ExecuteScalar()?.ToString() ?? "Records";

                    using var tx = conn.BeginTransaction();
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;

                    if (tableName == "Contacts")
                    {
                        cmd.CommandText = @"
                            INSERT INTO Contacts (FirstName, LastName, Email, Phone, Company, City, Notes)
                            VALUES (@First, @Last, @Email, @Phone, @Job, @Address, '');";
                        var pFirst = cmd.Parameters.Add("@First", SqliteType.Text);
                        var pLast = cmd.Parameters.Add("@Last", SqliteType.Text);
                        var pEmail = cmd.Parameters.Add("@Email", SqliteType.Text);
                        var pPhone = cmd.Parameters.Add("@Phone", SqliteType.Text);
                        var pJob = cmd.Parameters.Add("@Job", SqliteType.Text);
                        var pAddress = cmd.Parameters.Add("@Address", SqliteType.Text);

                        foreach (var r in incoming)
                        {
                            var names = r.Name.Split(' ', 2);
                            pFirst.Value = names.Length > 0 ? names[0] : "";
                            pLast.Value = names.Length > 1 ? names[1] : "";
                            pEmail.Value = r.Email;
                            pPhone.Value = r.Phone;
                            pJob.Value = r.Job;
                            pAddress.Value = r.Address;
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        cmd.CommandText = @"
                            INSERT INTO Records (Name, Email, Phone, Job, Address, CustomJson)
                            VALUES (@Name, @Email, @Phone, @Job, @Address, @CustomJson);";
                        var pName = cmd.Parameters.Add("@Name", SqliteType.Text);
                        var pEmail = cmd.Parameters.Add("@Email", SqliteType.Text);
                        var pPhone = cmd.Parameters.Add("@Phone", SqliteType.Text);
                        var pJob = cmd.Parameters.Add("@Job", SqliteType.Text);
                        var pAddress = cmd.Parameters.Add("@Address", SqliteType.Text);
                        var pCustom = cmd.Parameters.Add("@CustomJson", SqliteType.Text);

                        foreach (var r in incoming)
                        {
                            pName.Value = r.Name;
                            pEmail.Value = r.Email;
                            pPhone.Value = r.Phone;
                            pJob.Value = r.Job;
                            pAddress.Value = r.Address;
                            pCustom.Value = "{}";
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                    LoadRecords();
                    MessageBox.Show($"Ingested {incoming.Count} records successfully!", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ingestion failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ================= Export & Email =================

        private void BtnExport_Click(object? sender, EventArgs e)
        {
            if (grid.DataSource is not List<ContactRecord> list || list.Count == 0) return;

            using SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv|JSON File (*.json)|*.json|TSV File (*.tsv)|*.tsv|XML File (*.xml)|*.xml",
                Title = "Export Records"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                string ext = Path.GetExtension(sfd.FileName).ToLower();
                if (ext == ".csv" || ext == ".tsv")
                {
                    char sep = ext == ".tsv" ? '\t' : ',';
                    StringBuilder sb = new();
                    sb.AppendLine($"Id{sep}Name{sep}Email{sep}Phone{sep}Job{sep}Address");
                    foreach (var r in list) sb.AppendLine($"{r.Id}{sep}\"{r.Name}\"{sep}\"{r.Email}\"{sep}\"{r.Phone}\"{sep}\"{r.Job}\"{sep}\"{r.Address}\"");
                    File.WriteAllText(sfd.FileName, sb.ToString());
                }
                else if (ext == ".json")
                {
                    File.WriteAllText(sfd.FileName, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
                }
                else if (ext == ".xml")
                {
                    new XElement("Records", list.Select(r => new XElement("Record",
                        new XElement("Id", r.Id),
                        new XElement("Name", r.Name),
                        new XElement("Email", r.Email),
                        new XElement("Phone", r.Phone),
                        new XElement("Job", r.Job),
                        new XElement("Address", r.Address)
                    ))).Save(sfd.FileName);
                }
            }
        }

        private void BtnEmail_Click(object? sender, EventArgs e)
        {
            if (grid.SelectedRows.Count == 0) return;
            var rec = grid.SelectedRows[0].DataBoundItem as ContactRecord;
            if (rec == null || string.IsNullOrWhiteSpace(rec.Email))
            {
                MessageBox.Show("Please select a record that contains a valid email address.", "No Email Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = $"mailto:{rec.Email}", UseShellExecute = true });
            }
            catch
            {
                Process.Start(new ProcessStartInfo { FileName = $"https://mail.google.com/mail/?view=cm&fs=1&to={Uri.EscapeDataString(rec.Email)}", UseShellExecute = true });
            }
        }
    }

    // ================= POPUP DIALOG FORM =================

    public class RecordEditorForm : Form
    {
        public ContactRecord RecordData { get; private set; }

        private TextBox txtName = null!;
        private TextBox txtEmail = null!;
        private TextBox txtPhone = null!;
        private TextBox txtJob = null!;
        private TextBox txtAddress = null!;

        private readonly Color bgDark = Color.FromArgb(28, 28, 32);
        private readonly Color inputBg = Color.FromArgb(50, 50, 56);
        private readonly Color accent = Color.FromArgb(14, 165, 233);
        private readonly Color textLight = Color.FromArgb(244, 244, 245);
        private readonly Color textMuted = Color.FromArgb(161, 161, 170);

        public RecordEditorForm(ContactRecord? existingRecord)
        {
            RecordData = existingRecord != null ? new ContactRecord
            {
                Id = existingRecord.Id,
                Name = existingRecord.Name,
                Email = existingRecord.Email,
                Phone = existingRecord.Phone,
                Job = existingRecord.Job,
                Address = existingRecord.Address,
                CustomJson = existingRecord.CustomJson
            } : new ContactRecord();

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = RecordData.Id > 0 ? "Edit Record" : "Add New Record";
            this.Size = new Size(520, 520);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = bgDark;
            this.ForeColor = textLight;
            this.Font = new Font("Segoe UI", 10f, FontStyle.Regular);

            int y = 20;

            this.Controls.Add(CreateLabel("Full Name", ref y));
            txtName = CreateTextBox(RecordData.Name, ref y);
            this.Controls.Add(txtName);

            this.Controls.Add(CreateLabel("Email Address", ref y));
            txtEmail = CreateTextBox(RecordData.Email, ref y);
            this.Controls.Add(txtEmail);

            this.Controls.Add(CreateLabel("Phone Number", ref y));
            txtPhone = CreateTextBox(RecordData.Phone, ref y);
            this.Controls.Add(txtPhone);

            this.Controls.Add(CreateLabel("Job Title / Company", ref y));
            txtJob = CreateTextBox(RecordData.Job, ref y);
            this.Controls.Add(txtJob);

            this.Controls.Add(CreateLabel("Address / Location", ref y));
            txtAddress = CreateTextBox(RecordData.Address, ref y);
            this.Controls.Add(txtAddress);

            y += 15;

            // Commit Button
            Button btnCommit = new Button
            {
                Text = "Commit",
                Size = new Size(130, 40),
                Location = new Point(110, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = accent,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCommit.FlatAppearance.BorderSize = 0;
            btnCommit.Click += BtnCommit_Click;
            this.Controls.Add(btnCommit);

            // Cancel Button
            Button btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(110, 40),
                Location = new Point(260, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(63, 63, 70),
                ForeColor = textLight,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            this.Controls.Add(btnCancel);
        }

        private Label CreateLabel(string text, ref int y)
        {
            Label lbl = new Label { Text = text, ForeColor = textMuted, AutoSize = true, Location = new Point(36, y) };
            y += 24;
            return lbl;
        }

        private TextBox CreateTextBox(string val, ref int y)
        {
            TextBox tb = new TextBox
            {
                Text = val,
                BackColor = inputBg,
                ForeColor = textLight,
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(430, 28),
                Location = new Point(36, y)
            };
            y += 40;
            return tb;
        }

        private void BtnCommit_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Contact Name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RecordData.Name = txtName.Text.Trim();
            RecordData.Email = txtEmail.Text.Trim();
            RecordData.Phone = txtPhone.Text.Trim();
            RecordData.Job = txtJob.Text.Trim();
            RecordData.Address = txtAddress.Text.Trim();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}