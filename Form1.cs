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

        // UI�ؼ�
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

            lblStatus1 = new Label { Text = "ע��״̬: δע��", Location = new Point(40, 90), Size = new Size(400, 30) };
            lblStatus2 = new Label { Text = "����״̬: δ֪", Location = new Point(40, 125), Size = new Size(400, 30) };

            btnRefresh = new Button { Text = "ˢ��״̬", Location = new Point(40, 170), Size = new Size(100, 35) };
            btnTakeOver = new Button { Text = "�ӹ� Windows Defender", Location = new Point(150, 170), Size = new Size(200, 35) };
            btnRelease = new Button { Text = "����ӹ� Windows Defender", Location = new Point(360, 170), Size = new Size(200, 35) };

            lblVersion = new Label { Text = "����2 - v1.0.0", Font = new Font("Segoe UI", 10), Location = new Point(265, 240), AutoSize = true };

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
            refreshTimer.Interval = 10000; // 10���Զ�ˢ��
            refreshTimer.Tick += (s, e) => RefreshStatus();
            refreshTimer.Start();
        }

        // ������ԱȨ��
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
                lblAdmin.Text = "��ǰ�Թ���ԱȨ������";
                lblAdmin.ForeColor = Color.Green;
            }
            else
            {
                lblAdmin.Text = "��ǰδ�Թ���ԱȨ�����У����ֹ��ܿ����޷�ʹ��";
                lblAdmin.ForeColor = Color.Red;
            }
        }

        // ˢ��״̬
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
                                if (dispName == "360 ɱ��")
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
                MessageBox.Show("��ȡע���ʱ����: " + ex.Message, "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            lblStatus1.Text = "ע��״̬: " + (status1 == RegistrationStatus.Registered ? "��ע��" : "δע��");
            lblStatus2.Text = "����״̬: " + GetStateDescription(status2);

            // ��ť״̬
            btnTakeOver.Text = status1 == RegistrationStatus.Registered ? "���½ӹ� Windows Defender" : "�ӹ� Windows Defender";
            btnTakeOver.Enabled = true;
            btnRelease.Enabled = status1 == RegistrationStatus.Registered;
        }

        private string GetStateDescription(AvState state)
        {
            switch (state)
            {
                case AvState.Normal:
                    return "�Ѵ� (��������)";
                default:
                    return "δ֪";
            }
        }

        // ͨ������ִ�к���
        private async Task ExecuteWscControlCommands(params string[] argumentsList)
        {
            foreach (string arguments in argumentsList)
            {
                if (!System.IO.File.Exists(WscControlPath))
                {
                    MessageBox.Show("δ�ҵ� WscControl.exe", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    MessageBox.Show($"ִ������ʧ��: {ex.Message}", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public static bool Check360LeakFixerExistence()
        {
            const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\360safe.exe";
            string basePath = string.Empty;

            // ���Դ�ע�����
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath, false))
            {
                if (key == null)
                    return false; // ע��������

                // ��ȡ"Path"ֵ
                object pathValue = key.GetValue("Path");
                if (pathValue != null)
                    basePath = pathValue.ToString();
            }

            // ����·����ʽ
            if (!string.IsNullOrEmpty(basePath))
            {
                // ȷ��·���Է�б�ܽ�β
                if (basePath[basePath.Length - 1] != '\\')
                    basePath += '\\';
            }
            else
            {
                basePath = "\\"; // ��·��ʱʹ�ø�Ŀ¼
            }

            // ƴ������·��������ļ��Ƿ����
            string fullPath = Path.Combine(basePath, "360leakfixer.exe");
            return System.IO.File.Exists(fullPath);
        }

        // �ӹ� Windows Defender
        private async void TakeOverDefender()
        {

            if (status1 == RegistrationStatus.Registered)
            {
                Do_Shortcut();
            }

            if (!Check360LeakFixerExistence())
            {
                await ExecuteWscControlCommands("/regav:1_1");
                MessageBox.Show("�ӹܲ�����ɣ�", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("[Error]360ɱ��ע�����֧�ִ˲���\r\nReason: �Ѱ�װ360��ȫ��ʿ", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshStatus();
        }

        // ����ӹ�
        private async void ReleaseDefender()
        {
            if (!Check360LeakFixerExistence())
            {
                Do_Shortcut();
            }
            else
            {
                MessageBox.Show("[Error]360ɱ��ע�����֧�ִ˲���\r\nReason: �Ѱ�װ360��ȫ��ʿ", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshStatus();
        }

        private void Do_Shortcut()
        {
            string ShortcutPath = @".\\360sd_unregav.lnk";
            string targetExe = WscControlPath;
            string arguments = "/unregav";

            // ������ݷ�ʽ
            CreateShortcut(ShortcutPath, targetExe, arguments, "Click to run MyApp");
            MessageBox.Show("����Windows��Դ���������Թ���ԱȨ�����г���ͬĿ¼�µ�\"360sd_unreg.lnk\"�ļ����ɽ��ע�ᣬ��л����֧�ֺ�ʹ�ã�", "��ʾ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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