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
            Size = new Size(1180, 720);
            Font = new Font("Microsoft YaHei UI", 9F);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            TabPage inspectionPage = new TabPage("检测记录");
            TabPage logPage = new TabPage("运行日志");
            tabs.TabPages.Add(inspectionPage);
            tabs.TabPages.Add(logPage);
            tabs.SelectedTab = showLogs ? logPage : inspectionPage;

            FlowLayoutPanel tools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8), WrapContents = false };
            _keyword.Width = 260;
            Button query = new Button { Text = "查询", Width = 82 };
            Button openImage = new Button { Text = "打开结果图片", Width = 120 };
            Button refreshLogs = new Button { Text = "刷新日志", Width = 92 };
            query.Click += (s, e) => LoadInspections();
            openImage.Click += (s, e) => OpenSelectedImage();
            refreshLogs.Click += (s, e) => LoadLogs();
            tools.Controls.AddRange(new Control[] { _keyword, query, openImage });
            inspectionPage.Controls.Add(_inspections);
            inspectionPage.Controls.Add(tools);

            FlowLayoutPanel logTools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8) };
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
            Rename(_inspections, "Barcode", "二维码/检验号");
            Rename(_inspections, "MaterialType", "检测类型");
            Rename(_inspections, "LossRatePercent", "脱落率(%)");
            Rename(_inspections, "ParticleCount", "颗粒数");
            Rename(_inspections, "IsQualified", "合格");
            Rename(_inspections, "ImagePath", "结果图片");
            Rename(_inspections, "OperatorName", "操作员");
            Rename(_inspections, "CreatedAt", "检测时间");
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White
            };
        }

        private static void Rename(DataGridView grid, string name, string header)
        {
            if (grid.Columns.Contains(name)) grid.Columns[name].HeaderText = header;
        }
    }
}
