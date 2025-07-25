using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using IWshRuntimeLibrary;

namespace WinFormsApp1
{
    public partial class Form1 : Form
    {
        enum RegistrationStatus { NotRegistered, Registered }
        enum AvState
        {
            Unknown,
            Normal,      // 0x00051000
        }

        private RegistrationStatus status1;
        private AvState status2;
        private string displayName;
        private string WscControlPath;
        private string appPath;

        // UI控件
        private Label lblTitle, lblStatus1, lblStatus2, lblAdmin, lblVersion;
        private Button btnRefresh, btnTakeOver, btnRelease;
        private System.Windows.Forms.Timer refreshTimer;

        public Form1()
        {
            InitializeComponent();
            appPath = AppDomain.CurrentDomain.BaseDirectory;
            WscControlPath = Path.Combine(appPath, "WscControl.exe");
            InitUI();
            CheckAdmin();
            RefreshStatus();
            SetupAutoRefresh();
        }

        private void InitUI()
        {
            this.Text = "360sdDefenderControl (by wwwab)";
            this.Size = new Size(620, 300);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Font = new Font("Segoe UI", 10);

            lblTitle = new Label { Text = "360sdDefenderControl", Font = new Font("Segoe UI", 16, FontStyle.Bold), Location = new Point(40, 15), AutoSize = true };
            lblAdmin = new Label { Location = new Point(40, 50), Size = new Size(400, 24), ForeColor = Color.Red };

            lblStatus1 = new Label { Text = "注册状态: 未注册", Location = new Point(40, 90), Size = new Size(400, 30) };
            lblStatus2 = new Label { Text = "工作状态: 未知", Location = new Point(40, 125), Size = new Size(400, 30) };

            btnRefresh = new Button { Text = "刷新状态", Location = new Point(40, 170), Size = new Size(100, 35) };
            btnTakeOver = new Button { Text = "接管 Windows Defender", Location = new Point(150, 170), Size = new Size(200, 35) };
            btnRelease = new Button { Text = "解除接管 Windows Defender", Location = new Point(360, 170), Size = new Size(200, 35) };

            lblVersion = new Label { Text = "方法2 - v1.0.0", Font = new Font("Segoe UI", 10), Location = new Point(265, 240), AutoSize = true };

            btnRefresh.Click += (s, e) => RefreshStatus();
            btnTakeOver.Click += (s, e) => TakeOverDefender();
            btnRelease.Click += (s, e) => ReleaseDefender();

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblAdmin);
            this.Controls.Add(lblStatus1);
            this.Controls.Add(lblStatus2);
            this.Controls.Add(btnRefresh);
            this.Controls.Add(btnTakeOver);
            this.Controls.Add(btnRelease);
            this.Controls.Add(lblVersion);
        }

        private void SetupAutoRefresh()
        {
            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 10000; // 10秒自动刷新
            refreshTimer.Tick += (s, e) => RefreshStatus();
            refreshTimer.Start();
        }

        // 检测管理员权限
        private void CheckAdmin()
        {
            bool isAdmin = false;
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { }

            if (isAdmin)
            {
                lblAdmin.Text = "当前以管理员权限运行";
                lblAdmin.ForeColor = Color.Green;
            }
            else
            {
                lblAdmin.Text = "当前未以管理员权限运行，部分功能可能无法使用";
                lblAdmin.ForeColor = Color.Red;
            }
        }

        // 刷新状态
        private void RefreshStatus()
        {
            status1 = RegistrationStatus.NotRegistered;
            status2 = AvState.Unknown;
            displayName = null;

            try
            {
                using (RegistryKey avKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Security Center\Provider\Av"))
                {
                    if (avKey != null)
                    {
                        foreach (string subKeyName in avKey.GetSubKeyNames())
                        {
                            using (RegistryKey subKey = avKey.OpenSubKey(subKeyName))
                            {
                                if (subKey == null) continue;
                                var dispName = subKey.GetValue("DISPLAYNAME") as string;
                                if (dispName == "360 杀毒")
                                {
                                    displayName = dispName;
                                    status1 = RegistrationStatus.Registered;
                                    object stateObj = subKey.GetValue("STATE");
                                    int stateVal = stateObj is int ? (int)stateObj : 0;
                                    switch (stateVal)
                                    {
                                        case 0x00051000:
                                            status2 = AvState.Normal;
                                            break;
                                        default:
                                            status2 = AvState.Unknown;
                                            break;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取注册表时出错: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            lblStatus1.Text = "注册状态: " + (status1 == RegistrationStatus.Registered ? "已注册" : "未注册");
            lblStatus2.Text = "工作状态: " + GetStateDescription(status2);

            // 按钮状态
            btnTakeOver.Text = status1 == RegistrationStatus.Registered ? "重新接管 Windows Defender" : "接管 Windows Defender";
            btnTakeOver.Enabled = true;
            btnRelease.Enabled = status1 == RegistrationStatus.Registered;
        }

        private string GetStateDescription(AvState state)
        {
            switch (state)
            {
                case AvState.Normal:
                    return "已打开 (正常启动)";
                default:
                    return "未知";
            }
        }

        // 通用命令执行函数
        private async Task ExecuteWscControlCommands(params string[] argumentsList)
        {
            foreach (string arguments in argumentsList)
            {
                if (!System.IO.File.Exists(WscControlPath))
                {
                    MessageBox.Show("未找到 WscControl.exe", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    var process = new Process();
                    process.StartInfo.FileName = WscControlPath;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.WorkingDirectory = appPath;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.Start();
                    await Task.Run(() => process.WaitForExit(3000));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"执行命令失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public static bool Check360LeakFixerExistence()
        {
            const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\360safe.exe";
            string basePath = string.Empty;

            // 尝试打开注册表项
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath, false))
            {
                if (key == null)
                    return false; // 注册表项不存在

                // 读取"Path"值
                object pathValue = key.GetValue("Path");
                if (pathValue != null)
                    basePath = pathValue.ToString();
            }

            // 处理路径格式
            if (!string.IsNullOrEmpty(basePath))
            {
                // 确保路径以反斜杠结尾
                if (basePath[basePath.Length - 1] != '\\')
                    basePath += '\\';
            }
            else
            {
                basePath = "\\"; // 空路径时使用根目录
            }

            // 拼接完整路径并检查文件是否存在
            string fullPath = Path.Combine(basePath, "360leakfixer.exe");
            return System.IO.File.Exists(fullPath);
        }

        // 接管 Windows Defender
        private async void TakeOverDefender()
        {

            if (status1 == RegistrationStatus.Registered)
            {
                Do_Shortcut();
            }

            if (!Check360LeakFixerExistence())
            {
                await ExecuteWscControlCommands("/regav:1_1");
                MessageBox.Show("接管操作完成！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("[Error]360杀毒注册程序不支持此操作\r\nReason: 已安装360安全卫士", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshStatus();
        }

        // 解除接管
        private async void ReleaseDefender()
        {
            if (!Check360LeakFixerExistence())
            {
                Do_Shortcut();
            }
            else
            {
                MessageBox.Show("[Error]360杀毒注册程序不支持此操作\r\nReason: 已安装360安全卫士", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshStatus();
        }

        private void Do_Shortcut()
        {
            string ShortcutPath = @".\\360sd_unregav.lnk";
            string targetExe = WscControlPath;
            string arguments = "/unregav";

            // 创建快捷方式
            CreateShortcut(ShortcutPath, targetExe, arguments, "Click to run MyApp");
            MessageBox.Show("请在Windows资源管理器中以管理员权限运行程序同目录下的\"360sd_unreg.lnk\"文件即可解除注册，感谢您的支持和使用！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void CreateShortcut(string shortcutPath, string targetPath,
                                  string arguments, string description)
        {
            var shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);

            shortcut.TargetPath = targetPath;
            shortcut.Arguments = arguments;
            shortcut.Description = description;
            shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(targetPath);
            shortcut.Save();
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}