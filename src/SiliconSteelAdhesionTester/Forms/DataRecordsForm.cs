using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SiliconSteelAdhesionTester.Data;
using SiliconSteelAdhesionTester.Models;

namespace SiliconSteelAdhesionTester.Forms
{
    [System.ComponentModel.DesignerCategory("Form")]
    public sealed class DataRecordsForm : Form
    {
        private readonly DatabaseService _database;
        private readonly bool _showLogs;
        private readonly TextBox _keyword = new TextBox();
        private readonly DateTimePicker _inspectionFrom = NewDatePicker();
        private readonly DateTimePicker _inspectionTo = NewDatePicker();
        private readonly DateTimePicker _logFrom = NewDatePicker();
        private readonly DateTimePicker _logTo = NewDatePicker();
        private readonly DataGridView _inspections = NewGrid();
        private readonly DataGridView _logs = NewGrid();
        private readonly Label _resultCount = new Label();

        public DataRecordsForm(DatabaseService database, bool showLogs)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _showLogs = showLogs;
            Text = showLogs ? "运行日志" : "检测记录";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1280, 650);
            Size = new Size(1440, 820);
            Font = new Font("Microsoft YaHei UI", 9.5F);
            BackColor = Color.White;

            if (showLogs) BuildLogView();
            else BuildInspectionView();

            Shown += (s, e) =>
            {
                if (_showLogs) LoadLogs();
                else
                {
                    SetCueBanner(_keyword, "按检验号查询，例如：ENTITY-TEST-001");
                    LoadInspections();
                }
            };
        }

        private void BuildInspectionView()
        {
            TableLayoutPanel layout = NewPageLayout();
            Label title = NewTitle("检测记录");
            TableLayoutPanel tools = NewToolBar(9);
            ConfigureInspectionToolColumns(tools);
            _keyword.Dock = DockStyle.Fill;
            _keyword.Font = new Font("Microsoft YaHei UI", 10F);
            _keyword.Margin = new Padding(4, 10, 10, 10);
            Button query = NewToolButton("查询", 88);
            Button openImage = NewToolButton("打开结果图片", 132);
            Button clear = NewToolButton("清空条件", 100, Color.FromArgb(92, 108, 124));
            query.Click += (s, e) => LoadInspections();
            openImage.Click += (s, e) => OpenSelectedImage();
            clear.Click += (s, e) =>
            {
                _keyword.Clear();
                _inspectionFrom.Checked = false;
                _inspectionTo.Checked = false;
                LoadInspections();
            };
            _keyword.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                LoadInspections();
                e.SuppressKeyPress = true;
            };

            AddToolLabel(tools, "检验号", 0);
            tools.Controls.Add(_keyword, 1, 0);
            AddToolLabel(tools, "开始日期", 2);
            tools.Controls.Add(_inspectionFrom, 3, 0);
            AddToolLabel(tools, "结束日期", 4);
            tools.Controls.Add(_inspectionTo, 5, 0);
            tools.Controls.Add(query, 6, 0);
            tools.Controls.Add(openImage, 7, 0);
            tools.Controls.Add(clear, 8, 0);
            _resultCount.Dock = DockStyle.Fill;
            _resultCount.TextAlign = ContentAlignment.MiddleLeft;
            _resultCount.Padding = new Padding(12, 0, 0, 0);
            _resultCount.ForeColor = Color.DimGray;

            _inspections.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) OpenSelectedImage();
            };
            _inspections.CellFormatting += InspectionCellFormatting;
            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(tools, 0, 1);
            layout.Controls.Add(_resultCount, 0, 2);
            layout.Controls.Add(_inspections, 0, 3);
            Controls.Add(layout);
        }

        private void BuildLogView()
        {
            TableLayoutPanel layout = NewPageLayout();
            Label title = NewTitle("运行日志");
            TableLayoutPanel tools = NewToolBar(7);
            ConfigureLogToolColumns(tools);
            Button query = NewToolButton("查询", 88);
            Button refresh = NewToolButton("刷新日志", 130);
            Button clear = NewToolButton("清空条件", 100, Color.FromArgb(92, 108, 124));
            query.Click += (s, e) => LoadLogs();
            refresh.Click += (s, e) => LoadLogs();
            clear.Click += (s, e) =>
            {
                _logFrom.Checked = false;
                _logTo.Checked = false;
                LoadLogs();
            };
            AddToolLabel(tools, "开始日期", 0);
            tools.Controls.Add(_logFrom, 1, 0);
            AddToolLabel(tools, "结束日期", 2);
            tools.Controls.Add(_logTo, 3, 0);
            tools.Controls.Add(query, 4, 0);
            tools.Controls.Add(refresh, 5, 0);
            tools.Controls.Add(clear, 6, 0);

            _resultCount.Dock = DockStyle.Fill;
            _resultCount.TextAlign = ContentAlignment.MiddleLeft;
            _resultCount.Padding = new Padding(12, 0, 0, 0);
            _resultCount.ForeColor = Color.DimGray;
            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(tools, 0, 1);
            layout.Controls.Add(_resultCount, 0, 2);
            layout.Controls.Add(_logs, 0, 3);
            Controls.Add(layout);
        }

        private void LoadInspections()
        {
            DateTime? from = _inspectionFrom.Checked ? (DateTime?)_inspectionFrom.Value.Date : null;
            DateTime? to = _inspectionTo.Checked
                ? (DateTime?)_inspectionTo.Value.Date.AddDays(1).AddTicks(-1)
                : null;
            _inspections.DataSource = _database.GetInspectionRecords(_keyword.Text.Trim(), from, to, 1000);
            Rename(_inspections, "Id", "编号");
            Rename(_inspections, "QrCodeContent", "检验号");
            Rename(_inspections, "MaterialType", "检测类型");
            Rename(_inspections, "LossRatePercent", "脱落率(%)");
            Rename(_inspections, "ParticleCount", "缺陷区域");
            Rename(_inspections, "IsQualified", "检测结果");
            Rename(_inspections, "OperatorName", "操作员");
            Rename(_inspections, "CreatedAt", "检测时间");
            SetColumn(_inspections, "Id", 70);
            SetColumn(_inspections, "QrCodeContent", 300);
            SetColumn(_inspections, "MaterialType", 145);
            SetColumn(_inspections, "LossRatePercent", 120);
            SetColumn(_inspections, "ParticleCount", 110);
            SetColumn(_inspections, "IsQualified", 110);
            SetColumn(_inspections, "OperatorName", 125);
            SetColumn(_inspections, "CreatedAt", 230);
            if (_inspections.Columns.Contains("ImagePath"))
                _inspections.Columns["ImagePath"].Visible = false;
            SetFillColumn(_inspections, "Id", 7);
            SetFillColumn(_inspections, "QrCodeContent", 25);
            SetFillColumn(_inspections, "MaterialType", 12);
            SetFillColumn(_inspections, "LossRatePercent", 10);
            SetFillColumn(_inspections, "ParticleCount", 10);
            SetFillColumn(_inspections, "IsQualified", 10);
            SetFillColumn(_inspections, "OperatorName", 10);
            SetFillColumn(_inspections, "CreatedAt", 16);
            _inspections.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _resultCount.Text = "查询结果：" + _inspections.Rows.Count + " 条";
            ClearSelection(_inspections);
        }

        private void LoadLogs()
        {
            DateTime? from = _logFrom.Checked ? (DateTime?)_logFrom.Value.Date : null;
            DateTime? to = _logTo.Checked ? (DateTime?)_logTo.Value.Date.AddDays(1).AddTicks(-1) : null;
            _logs.DataSource = _database.GetSystemLogs(from, to, 2000);
            Rename(_logs, "Category", "类别");
            Rename(_logs, "CodeOrAction", "代码/操作");
            Rename(_logs, "Node", "节点");
            Rename(_logs, "Message", "内容");
            Rename(_logs, "UserName", "用户");
            Rename(_logs, "CreatedAt", "时间");
            SetColumn(_logs, "Category", 90);
            SetColumn(_logs, "CodeOrAction", 190);
            SetColumn(_logs, "Node", 180);
            SetColumn(_logs, "Message", 620, true);
            SetColumn(_logs, "UserName", 120);
            SetColumn(_logs, "CreatedAt", 230);
            SetFillColumn(_logs, "Category", 7);
            SetFillColumn(_logs, "CodeOrAction", 15);
            SetFillColumn(_logs, "Node", 14);
            SetFillColumn(_logs, "Message", 43, true);
            SetFillColumn(_logs, "UserName", 8);
            SetFillColumn(_logs, "CreatedAt", 13);
            _logs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _resultCount.Text = "查询结果：" + _logs.Rows.Count + " 条";
            ClearSelection(_logs);
        }

        private void InspectionCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            string property = _inspections.Columns[e.ColumnIndex].DataPropertyName;
            if (property == "IsQualified" && e.Value is bool)
            {
                bool qualified = (bool)e.Value;
                e.Value = qualified ? "合格" : "不合格";
                e.CellStyle.ForeColor = qualified ? Color.SeaGreen : Color.Firebrick;
                e.CellStyle.Font = new Font(_inspections.Font, FontStyle.Bold);
                e.FormattingApplied = true;
            }
            else if (property == "MaterialType" && e.Value != null)
            {
                string value = Convert.ToString(e.Value);
                e.Value = value.IndexOf("NonOriented", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "无取向"
                    : value.IndexOf("Oriented", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "取向"
                        : value;
                e.FormattingApplied = true;
            }
            else if (property == "LossRatePercent" && e.Value != null)
            {
                e.Value = Convert.ToDouble(e.Value).ToString("F3");
                e.FormattingApplied = true;
            }
            else if (property == "CreatedAt" && e.Value is DateTime)
            {
                e.Value = ((DateTime)e.Value).ToString("yyyy-MM-dd HH:mm:ss");
                e.FormattingApplied = true;
            }
        }

        private void OpenSelectedImage()
        {
            if (_inspections.CurrentRow == null)
            {
                MessageBox.Show(this, "请先选择一条检测记录。", "打开结果图片",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            InspectionRecord record = _inspections.CurrentRow.DataBoundItem as InspectionRecord;
            if (record == null || string.IsNullOrWhiteSpace(record.ImagePath) || !File.Exists(record.ImagePath))
            {
                MessageBox.Show(this, "该记录没有可用的结果图片，可能是测试数据或图片已移动。",
                    "无法打开", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Process.Start(new ProcessStartInfo(record.ImagePath) { UseShellExecute = true });
        }

        private static TableLayoutPanel NewPageLayout()
        {
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(18)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            return layout;
        }

        private static Label NewTitle(string title)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = title,
                Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
                ForeColor = Color.FromArgb(27, 52, 76),
                Padding = new Padding(4, 8, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static TableLayoutPanel NewToolBar(int columns)
        {
            TableLayoutPanel tools = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = columns,
                RowCount = 1,
                BackColor = Color.FromArgb(243, 246, 250),
                Padding = new Padding(10, 4, 10, 4)
            };
            for (int i = 0; i < columns; i++)
                tools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            if (columns > 1) tools.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 100);
            return tools;
        }

        private static void ConfigureInspectionToolColumns(TableLayoutPanel tools)
        {
            tools.ColumnStyles[0] = new ColumnStyle(SizeType.AutoSize);
            tools.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 100);
            tools.ColumnStyles[2] = new ColumnStyle(SizeType.AutoSize);
            tools.ColumnStyles[3] = new ColumnStyle(SizeType.Absolute, 240);
            tools.ColumnStyles[4] = new ColumnStyle(SizeType.AutoSize);
            tools.ColumnStyles[5] = new ColumnStyle(SizeType.Absolute, 240);
            tools.ColumnStyles[6] = new ColumnStyle(SizeType.Absolute, 96);
            tools.ColumnStyles[7] = new ColumnStyle(SizeType.Absolute, 142);
            tools.ColumnStyles[8] = new ColumnStyle(SizeType.Absolute, 112);
        }

        private static void ConfigureLogToolColumns(TableLayoutPanel tools)
        {
            tools.ColumnStyles[0] = new ColumnStyle(SizeType.AutoSize);
            tools.ColumnStyles[1] = new ColumnStyle(SizeType.Absolute, 240);
            tools.ColumnStyles[2] = new ColumnStyle(SizeType.AutoSize);
            tools.ColumnStyles[3] = new ColumnStyle(SizeType.Absolute, 240);
            tools.ColumnStyles[4] = new ColumnStyle(SizeType.Absolute, 96);
            tools.ColumnStyles[5] = new ColumnStyle(SizeType.Absolute, 145);
            tools.ColumnStyles[6] = new ColumnStyle(SizeType.Absolute, 112);
        }

        private static void AddToolLabel(TableLayoutPanel tools, string text, int column)
        {
            tools.Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(8, 15, 4, 0)
            }, column, 0);
        }

        private static DateTimePicker NewDatePicker()
        {
            return new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                ShowCheckBox = true,
                Checked = false,
                Width = 210,
                MinimumSize = new Size(210, 0),
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 9, 10, 9)
            };
        }

        private static Button NewToolButton(string text, int width, Color? color = null)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 38,
                FlatStyle = FlatStyle.Flat,
                BackColor = color ?? Color.FromArgb(38, 112, 190),
                ForeColor = Color.White,
                Margin = new Padding(8, 8, 0, 8)
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private static DataGridView NewGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                RowHeadersVisible = false,
                ColumnHeadersHeight = 50,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(224, 230, 238),
                ScrollBars = ScrollBars.Both,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Padding = new Padding(7, 5, 7, 5),
                    SelectionBackColor = Color.FromArgb(218, 235, 252),
                    SelectionForeColor = Color.FromArgb(30, 45, 60),
                    WrapMode = DataGridViewTriState.False
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(44, 68, 94),
                    ForeColor = Color.White,
                    Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
                    Padding = new Padding(6),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    WrapMode = DataGridViewTriState.False
                },
                EnableHeadersVisualStyles = false
            };
        }

        private static void SetColumn(DataGridView grid, string name, int width, bool wrap = false)
        {
            if (!grid.Columns.Contains(name)) return;
            DataGridViewColumn column = grid.Columns[name];
            column.Width = width;
            column.MinimumWidth = Math.Min(width, 70);
            column.DefaultCellStyle.WrapMode = wrap
                ? DataGridViewTriState.True
                : DataGridViewTriState.False;
        }

        private static void SetFillColumn(
            DataGridView grid,
            string name,
            float fillWeight,
            bool wrap = false)
        {
            if (!grid.Columns.Contains(name)) return;
            DataGridViewColumn column = grid.Columns[name];
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.FillWeight = fillWeight;
            column.MinimumWidth = 70;
            column.DefaultCellStyle.WrapMode = wrap
                ? DataGridViewTriState.True
                : DataGridViewTriState.False;
        }

        private static void Rename(DataGridView grid, string name, string header)
        {
            if (grid.Columns.Contains(name)) grid.Columns[name].HeaderText = header;
        }

        private static void ClearSelection(DataGridView grid)
        {
            grid.ClearSelection();
            grid.CurrentCell = null;
        }

        private static void SetCueBanner(TextBox textBox, string text)
        {
            SendMessage(textBox.Handle, 0x1501, (IntPtr)1, text);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int msg,
            IntPtr wParam,
            string lParam);
    }
}
