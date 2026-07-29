using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SiliconSteelAdhesionTester.Data;
using SiliconSteelAdhesionTester.Models;

namespace SiliconSteelAdhesionTester.Forms
{
    [System.ComponentModel.DesignerCategory("Form")]
    public sealed class DataRecordsForm : Form
    {
        private readonly DatabaseService _database;
        private readonly TextBox _keyword = new TextBox();
        private readonly DataGridView _inspections = NewGrid();
        private readonly DataGridView _logs = NewGrid();

        public DataRecordsForm(DatabaseService database, bool showLogs)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            Text = "检测数据与运行日志";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(980, 620);
            Size = new Size(1320, 760);
            Font = new Font("Microsoft YaHei UI", 9F);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            TabPage inspectionPage = new TabPage("检测记录");
            TabPage logPage = new TabPage("运行日志");
            tabs.TabPages.Add(inspectionPage);
            tabs.TabPages.Add(logPage);
            tabs.SelectedTab = showLogs ? logPage : inspectionPage;

            FlowLayoutPanel tools = NewToolBar();
            _keyword.Width = 260;
            Button query = NewToolButton("查询", 82);
            Button openImage = NewToolButton("打开结果图片", 132);
            Button refreshLogs = NewToolButton("刷新日志", 100);
            query.Click += (s, e) => LoadInspections();
            openImage.Click += (s, e) => OpenSelectedImage();
            refreshLogs.Click += (s, e) => LoadLogs();
            tools.Controls.AddRange(new Control[] { _keyword, query, openImage });
            inspectionPage.Controls.Add(_inspections);
            inspectionPage.Controls.Add(tools);

            FlowLayoutPanel logTools = NewToolBar();
            logTools.Controls.Add(refreshLogs);
            logPage.Controls.Add(_logs);
            logPage.Controls.Add(logTools);
            Controls.Add(tabs);
            Shown += (s, e) => { LoadInspections(); LoadLogs(); };
        }

        private void LoadInspections()
        {
            _inspections.DataSource = _database.GetInspectionRecords(_keyword.Text.Trim(), 1000);
            Rename(_inspections, "Id", "编号");
            Rename(_inspections, "QrCodeContent", "二维码内容/检验号");
            Rename(_inspections, "MaterialType", "检测类型");
            Rename(_inspections, "LossRatePercent", "脱落率(%)");
            Rename(_inspections, "ParticleCount", "颗粒数");
            Rename(_inspections, "IsQualified", "合格");
            Rename(_inspections, "ImagePath", "结果图片");
            Rename(_inspections, "OperatorName", "操作员");
            Rename(_inspections, "CreatedAt", "检测时间");
            SetColumn(_inspections, "Id", 90);
            SetColumn(_inspections, "QrCodeContent", 300);
            SetColumn(_inspections, "MaterialType", 150);
            SetColumn(_inspections, "LossRatePercent", 140);
            SetColumn(_inspections, "ParticleCount", 115);
            SetColumn(_inspections, "IsQualified", 95);
            SetColumn(_inspections, "ImagePath", 420, true);
            SetColumn(_inspections, "OperatorName", 140);
            SetColumn(_inspections, "CreatedAt", 210);
        }

        private void LoadLogs()
        {
            _logs.DataSource = _database.GetSystemLogs(2000);
            Rename(_logs, "Category", "类别");
            Rename(_logs, "CodeOrAction", "代码/操作");
            Rename(_logs, "Node", "节点");
            Rename(_logs, "Message", "内容");
            Rename(_logs, "UserName", "用户");
            Rename(_logs, "CreatedAt", "时间");
            SetColumn(_logs, "Category", 100);
            SetColumn(_logs, "CodeOrAction", 190);
            SetColumn(_logs, "Node", 160);
            SetColumn(_logs, "Message", 720, true);
            SetColumn(_logs, "UserName", 140);
            SetColumn(_logs, "CreatedAt", 210);
        }

        private void OpenSelectedImage()
        {
            if (_inspections.CurrentRow == null) return;
            InspectionRecord record = _inspections.CurrentRow.DataBoundItem as InspectionRecord;
            if (record == null || string.IsNullOrWhiteSpace(record.ImagePath) || !File.Exists(record.ImagePath))
            {
                MessageBox.Show(this, "该记录的结果图片不存在或已移动。", "无法打开", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Process.Start(new ProcessStartInfo(record.ImagePath) { UseShellExecute = true });
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
                ColumnHeadersHeight = 54,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(224, 230, 238),
                ScrollBars = ScrollBars.Both,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Padding = new Padding(5, 4, 5, 4),
                    SelectionBackColor = Color.FromArgb(218, 235, 252),
                    SelectionForeColor = Color.FromArgb(30, 45, 60),
                    WrapMode = DataGridViewTriState.False
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(44, 68, 94),
                    ForeColor = Color.White,
                    Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                    Padding = new Padding(4),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    WrapMode = DataGridViewTriState.True
                },
                EnableHeadersVisualStyles = false
            };
        }

        private static FlowLayoutPanel NewToolBar()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 56,
                Padding = new Padding(12, 10, 12, 8),
                WrapContents = false,
                BackColor = Color.FromArgb(243, 246, 250)
            };
        }

        private static Button NewToolButton(string text, int width)
        {
            return new Button
            {
                Text = text,
                Width = width,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(38, 112, 190),
                ForeColor = Color.White,
                Margin = new Padding(8, 0, 0, 0)
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

        private static void Rename(DataGridView grid, string name, string header)
        {
            if (grid.Columns.Contains(name)) grid.Columns[name].HeaderText = header;
        }
    }
}
