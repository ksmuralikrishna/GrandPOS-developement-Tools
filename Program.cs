using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GrandPOS.Deployer
{
    public class ConfigData
    {
        public string SourceCodeRoot { get; set; }
        public string InstallDir { get; set; }
        public string MSBuildPath { get; set; }
        public string Configuration { get; set; }
        public string Platform { get; set; }
        public bool AutoRestart { get; set; }
        public string Theme { get; set; }

        public ConfigData()
        {
            SourceCodeRoot = @"D:\KSM\Projects\POS\Source Code";
            InstallDir = @"C:\Grand\GOLDPOSECR";
            MSBuildPath = @"C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe";
            Configuration = "Release";
            Platform = "Win32";
            AutoRestart = true;
            Theme = "Dark";
        }

        public static string GetConfigFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deployer_config.json");
        }

        public static ConfigData Load()
        {
            ConfigData cfg = new ConfigData();
            string path = GetConfigFilePath();
            if (!File.Exists(path))
            {
                // Auto-detect based on app location
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string parentDir = Path.GetFullPath(Path.Combine(appDir, ".."));
                if (Directory.Exists(Path.Combine(parentDir, "grand_pos")) && Directory.Exists(Path.Combine(parentDir, "grand_pos_pricing")))
                {
                    cfg.SourceCodeRoot = parentDir;
                }
                else if (Directory.Exists(Path.Combine(appDir, "grand_pos")) && Directory.Exists(Path.Combine(appDir, "grand_pos_pricing")))
                {
                    cfg.SourceCodeRoot = appDir;
                }
                cfg.MSBuildPath = AutoDetectMSBuild();
                return cfg;
            }

            try
            {
                string json = File.ReadAllText(path);
                cfg.SourceCodeRoot = ExtractJsonValue(json, "SourceCodeRoot", cfg.SourceCodeRoot);
                cfg.InstallDir = ExtractJsonValue(json, "InstallDir", cfg.InstallDir);
                cfg.MSBuildPath = ExtractJsonValue(json, "MSBuildPath", cfg.MSBuildPath);
                cfg.Configuration = ExtractJsonValue(json, "Configuration", cfg.Configuration);
                cfg.Platform = ExtractJsonValue(json, "Platform", cfg.Platform);
                string autoRest = ExtractJsonValue(json, "AutoRestart", "true");
                cfg.AutoRestart = autoRest.ToLower() != "false";
                cfg.Theme = ExtractJsonValue(json, "Theme", "Dark");
            }
            catch {}

            if (string.IsNullOrEmpty(cfg.MSBuildPath) || !File.Exists(cfg.MSBuildPath))
            {
                cfg.MSBuildPath = AutoDetectMSBuild();
            }

            if (string.IsNullOrEmpty(cfg.SourceCodeRoot) || !Directory.Exists(cfg.SourceCodeRoot))
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string parentDir = Path.GetFullPath(Path.Combine(appDir, ".."));
                if (Directory.Exists(Path.Combine(parentDir, "grand_pos")) && Directory.Exists(Path.Combine(parentDir, "grand_pos_pricing")))
                {
                    cfg.SourceCodeRoot = parentDir;
                }
                else if (Directory.Exists(Path.Combine(appDir, "grand_pos")) && Directory.Exists(Path.Combine(appDir, "grand_pos_pricing")))
                {
                    cfg.SourceCodeRoot = appDir;
                }
            }

            if (string.IsNullOrEmpty(cfg.InstallDir) || !Directory.Exists(cfg.InstallDir))
            {
                if (Directory.Exists(@"C:\Grand\GOLDPOSECR")) cfg.InstallDir = @"C:\Grand\GOLDPOSECR";
                else if (Directory.Exists(@"D:\Grand\GOLDPOSECR")) cfg.InstallDir = @"D:\Grand\GOLDPOSECR";
                else if (Directory.Exists(@"C:\AlData\GOLDPOSECR")) cfg.InstallDir = @"C:\AlData\GOLDPOSECR";
            }
            return cfg;
        }

        public void Save()
        {
            string path = GetConfigFilePath();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendFormat("  \"SourceCodeRoot\": \"{0}\",\r\n", EscapeJson(SourceCodeRoot));
            sb.AppendFormat("  \"InstallDir\": \"{0}\",\r\n", EscapeJson(InstallDir));
            sb.AppendFormat("  \"MSBuildPath\": \"{0}\",\r\n", EscapeJson(MSBuildPath));
            sb.AppendFormat("  \"Configuration\": \"{0}\",\r\n", EscapeJson(Configuration));
            sb.AppendFormat("  \"Platform\": \"{0}\",\r\n", EscapeJson(Platform));
            sb.AppendFormat("  \"AutoRestart\": {0},\r\n", AutoRestart ? "true" : "false");
            sb.AppendFormat("  \"Theme\": \"{0}\"\r\n", EscapeJson(Theme));
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString());
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string ExtractJsonValue(string json, string key, string defVal)
        {
            string pattern = "\"" + key + "\"\\s*:\\s*\"?([^,\"\r\n}]+)\"?";
            Match m = Regex.Match(json, pattern);
            if (m.Success)
            {
                return m.Groups[1].Value.Replace("\\\\", "\\").Trim();
            }
            return defVal;
        }

        public static string AutoDetectMSBuild()
        {
            string[] candidates = new string[]
            {
                @"C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe",
                @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\MSBuild\14.0\Bin\MSBuild.exe",
                @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
                @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
                @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
                @"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
                @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
                @"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
            };

            foreach (string p in candidates)
            {
                if (File.Exists(p)) return p;
            }
            return @"C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe";
        }
    }

    public class MainForm : Form
    {
        private TextBox txtSourceRoot;
        private TextBox txtInstallDir;
        private TextBox txtMSBuild;
        private ComboBox cmbConfig;
        private ComboBox cmbPlatform;
        private CheckBox chkAutoRestart;
        private Button btnSaveConfig;

        private Button btnDeployDlls;
        private Button btnDeployExe;
        private Button btnDeployAll;
        private Button btnRollback;

        private Button btnStartPos;
        private Button btnStopPos;
        private Button btnRestartPos;
        private Button btnOpenPosLog;
        private Label lblPosStatus;
        private Timer tmrPosStatus;

        private RichTextBox rtbLog;
        private Button btnClearLog;
        private ToolStripStatusLabel lblStatus;
        private ProgressBar progressBar;

        private Panel pnlHeader;
        private Label lblTitle;
        private Label lblSubtitle;
        private Button btnThemeToggle;
        private GroupBox grpPaths;
        private GroupBox grpActions;
        private GroupBox grpProcess;
        private GroupBox grpLog;
        private Panel pnlLogTools;
        private Button btnOpenLog;
        private Button btnBrowseSrc;
        private Button btnBrowseInst;
        private Button btnBrowseMsb;
        private StatusStrip statusStrip;

        private ConfigData currentConfig;
        private bool isBusy = false;
        private bool isDarkTheme = true;

        public MainForm()
        {
            InitializeComponent();
            currentConfig = ConfigData.Load();
            isDarkTheme = currentConfig.Theme != "Classic";
            PopulateControlsFromConfig();
            ApplyTheme(isDarkTheme);
        }

        private void InitializeComponent()
        {
            this.Text = "Grand POS Development Tool";
            this.Size = new Size(950, 850);
            this.MinimumSize = new Size(890, 740);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // 1. Header Banner
            pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 65;
            pnlHeader.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                if (isDarkTheme)
                {
                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        pnlHeader.ClientRectangle,
                        Color.FromArgb(18, 20, 26),
                        Color.FromArgb(34, 40, 52),
                        LinearGradientMode.Horizontal))
                    {
                        g.FillRectangle(brush, pnlHeader.ClientRectangle);
                    }
                    using (Pen penCyan = new Pen(Color.FromArgb(6, 182, 212), 2))
                    {
                        g.DrawLine(penCyan, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
                    }
                }
                else
                {
                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        pnlHeader.ClientRectangle,
                        Color.FromArgb(10, 36, 106),
                        Color.FromArgb(58, 110, 165),
                        LinearGradientMode.Horizontal))
                    {
                        g.FillRectangle(brush, pnlHeader.ClientRectangle);
                    }
                    using (Pen penBevel = new Pen(Color.FromArgb(128, 128, 128), 1))
                    {
                        g.DrawLine(penBevel, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
                    }
                }

                // Retro terminal prompt icon "[ >_ ]"
                int px = 18, py = 16;
                using (SolidBrush bPrompt = new SolidBrush(isDarkTheme ? Color.FromArgb(6, 182, 212) : Color.FromArgb(0, 255, 100)))
                {
                    using (Font fIcon = new Font("Consolas", 15F, FontStyle.Bold))
                    {
                        g.DrawString(">_", fIcon, bPrompt, px, py);
                    }
                }
            };

            lblTitle = new Label();
            lblTitle.Text = "Grand POS Development Tool";
            lblTitle.Font = new Font("Segoe UI", 13.5F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.BackColor = Color.Transparent;
            lblTitle.Location = new Point(56, 11);
            lblTitle.AutoSize = true;

            lblSubtitle = new Label();
            lblSubtitle.Text = "Automated Build, Staging, Backup & Restart Utility for Grand POS";
            lblSubtitle.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            lblSubtitle.ForeColor = Color.FromArgb(156, 163, 175);
            lblSubtitle.BackColor = Color.Transparent;
            lblSubtitle.Location = new Point(58, 37);
            lblSubtitle.AutoSize = true;

            btnThemeToggle = new Button();
            btnThemeToggle.Text = "☀️ Classic Retro";
            btnThemeToggle.Size = new Size(130, 28);
            btnThemeToggle.Location = new Point(790, 18);
            btnThemeToggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnThemeToggle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnThemeToggle.Click += (s, e) =>
            {
                isDarkTheme = !isDarkTheme;
                currentConfig.Theme = isDarkTheme ? "Dark" : "Classic";
                currentConfig.Save();
                ApplyTheme(isDarkTheme);
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            pnlHeader.Controls.Add(btnThemeToggle);

            // 2. Paths GroupBox
            grpPaths = new GroupBox();
            grpPaths.Text = " 1. Machine & Workspace Paths ";
            grpPaths.Location = new Point(15, 75);
            grpPaths.Size = new Size(905, 130);
            grpPaths.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            grpPaths.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            Label lblSrc = new Label() { Text = "Source Code Root:", Location = new Point(15, 27), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            txtSourceRoot = new TextBox() { Location = new Point(155, 24), Size = new Size(630, 23), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            btnBrowseSrc = new Button() { Text = "Browse...", Location = new Point(795, 22), Size = new Size(95, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 8.5F, FontStyle.Regular) };
            btnBrowseSrc.Click += (s, e) => BrowseFolder(txtSourceRoot, "Select Source Code Root Folder");

            Label lblInst = new Label() { Text = "Grand POS Install Dir:", Location = new Point(15, 60), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            txtInstallDir = new TextBox() { Location = new Point(155, 57), Size = new Size(630, 23), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            btnBrowseInst = new Button() { Text = "Browse...", Location = new Point(795, 55), Size = new Size(95, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 8.5F, FontStyle.Regular) };
            btnBrowseInst.Click += (s, e) => BrowseFolder(txtInstallDir, "Select Grand POS Installation Folder");

            Label lblMsb = new Label() { Text = "MSBuild Executable:", Location = new Point(15, 93), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            txtMSBuild = new TextBox() { Location = new Point(155, 90), Size = new Size(630, 23), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            btnBrowseMsb = new Button() { Text = "Browse...", Location = new Point(795, 88), Size = new Size(95, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 8.5F, FontStyle.Regular) };
            btnBrowseMsb.Click += (s, e) => BrowseFile(txtMSBuild, "MSBuild.exe|MSBuild.exe|Executables (*.exe)|*.exe");

            grpPaths.Controls.AddRange(new Control[] { lblSrc, txtSourceRoot, btnBrowseSrc, lblInst, txtInstallDir, btnBrowseInst, lblMsb, txtMSBuild, btnBrowseMsb });

            // 3. Options & Actions GroupBox
            grpActions = new GroupBox();
            grpActions.Text = " 2. Build & Deployment Actions ";
            grpActions.Location = new Point(15, 212);
            grpActions.Size = new Size(905, 115);
            grpActions.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            grpActions.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            Label lblCfg = new Label() { Text = "Config:", Location = new Point(15, 28), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            cmbConfig = new ComboBox() { Location = new Point(65, 25), Size = new Size(90, 23), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            cmbConfig.Items.AddRange(new object[] { "Release", "Debug" });

            Label lblPlat = new Label() { Text = "Platform:", Location = new Point(170, 28), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            cmbPlatform = new ComboBox() { Location = new Point(230, 25), Size = new Size(80, 23), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            cmbPlatform.Items.AddRange(new object[] { "Win32", "x64" });

            chkAutoRestart = new CheckBox() { Text = "Auto-restart GrandPOS", Location = new Point(330, 27), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };

            btnSaveConfig = new Button() { Text = "Save Settings", Location = new Point(775, 22), Size = new Size(115, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 8.5F, FontStyle.Regular) };
            btnSaveConfig.Click += (s, e) => SaveConfigFromControls(true);

            // Action Buttons
            btnDeployDlls = new Button()
            {
                Text = "⚡ Build + Deploy DLLs",
                Location = new Point(15, 58),
                Size = new Size(205, 46),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnDeployDlls.Click += async (s, e) => await ExecuteDeployment("DLLsOnly");

            btnDeployExe = new Button()
            {
                Text = "⚡ Build + deploy GrandPOS.exe",
                Location = new Point(230, 58),
                Size = new Size(220, 46),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnDeployExe.Click += async (s, e) => await ExecuteDeployment("ExeOnly");

            btnDeployAll = new Button()
            {
                Text = "🚀 Build + Deploy Everything",
                Location = new Point(460, 58),
                Size = new Size(225, 46),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnDeployAll.Click += async (s, e) => await ExecuteDeployment("All");

            btnRollback = new Button()
            {
                Text = "🔄 Rollback\n(Last Backup)",
                Location = new Point(695, 58),
                Size = new Size(195, 46),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnRollback.Click += async (s, e) => await ExecuteDeployment("Rollback");

            grpActions.Controls.AddRange(new Control[] {
                lblCfg, cmbConfig, lblPlat, cmbPlatform, chkAutoRestart, btnSaveConfig,
                btnDeployDlls, btnDeployExe, btnDeployAll, btnRollback
            });

            // 4. Grand POS Process & Diagnostics
            grpProcess = new GroupBox();
            grpProcess.Text = " 3. Grand POS Control & Diagnostics ";
            grpProcess.Location = new Point(15, 335);
            grpProcess.Size = new Size(905, 75);
            grpProcess.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            grpProcess.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            btnStartPos = new Button()
            {
                Text = "▶ Start GrandPOS",
                Location = new Point(15, 23),
                Size = new Size(155, 38),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnStartPos.Click += (s, e) => StartGrandPOS();

            btnStopPos = new Button()
            {
                Text = "⏹ Stop GrandPOS",
                Location = new Point(180, 23),
                Size = new Size(155, 38),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnStopPos.Click += (s, e) => StopGrandPOS();

            btnRestartPos = new Button()
            {
                Text = "🔄 Restart GrandPOS",
                Location = new Point(345, 23),
                Size = new Size(165, 38),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnRestartPos.Click += async (s, e) => await RestartGrandPOS();

            btnOpenPosLog = new Button()
            {
                Text = "📄 Open Output.log",
                Location = new Point(520, 23),
                Size = new Size(165, 38),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnOpenPosLog.Click += (s, e) => OpenOutputLog();

            lblPosStatus = new Label()
            {
                Text = "POS Status: Checking...",
                Location = new Point(700, 30),
                Size = new Size(190, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            grpProcess.Controls.AddRange(new Control[] {
                btnStartPos, btnStopPos, btnRestartPos, btnOpenPosLog, lblPosStatus
            });

            // 5. Output Console Panel
            grpLog = new GroupBox();
            grpLog.Text = " 4. Live Output & Activity Log ";
            grpLog.Location = new Point(15, 418);
            grpLog.Size = new Size(905, 355);
            grpLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grpLog.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            rtbLog = new RichTextBox();
            rtbLog.Dock = DockStyle.Fill;
            rtbLog.Font = new Font("Consolas", 9.5F, FontStyle.Regular);
            rtbLog.ReadOnly = true;
            rtbLog.WordWrap = false;
            rtbLog.BorderStyle = BorderStyle.Fixed3D;

            pnlLogTools = new Panel();
            pnlLogTools.Dock = DockStyle.Top;
            pnlLogTools.Height = 28;

            btnOpenLog = new Button() { Text = "Deployer Log", Size = new Size(100, 24), Location = new Point(700, 2), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 8.5F, FontStyle.Regular) };
            btnOpenLog.Click += (s, e) => OpenLogFile();
            pnlLogTools.Controls.Add(btnOpenLog);

            btnClearLog = new Button() { Text = "Clear Screen", Size = new Size(90, 24), Location = new Point(805, 2), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 8.5F, FontStyle.Regular) };
            btnClearLog.Click += (s, e) => rtbLog.Clear();
            pnlLogTools.Controls.Add(btnClearLog);

            grpLog.Controls.Add(rtbLog);
            grpLog.Controls.Add(pnlLogTools);

            // 6. Bottom Status Strip
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel() { Text = "Ready", Spring = true, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            progressBar = new ProgressBar() { Size = new Size(150, 16), Style = ProgressBarStyle.Blocks };
            statusStrip.Items.Add(lblStatus);
            statusStrip.Items.Add(new ToolStripControlHost(progressBar));

            this.Controls.Add(grpLog);
            this.Controls.Add(grpProcess);
            this.Controls.Add(grpActions);
            this.Controls.Add(grpPaths);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(statusStrip);

            tmrPosStatus = new Timer();
            tmrPosStatus.Interval = 2000;
            tmrPosStatus.Tick += (s, e) => UpdatePosStatus();
            tmrPosStatus.Start();

            LogInfo("Grand POS Development Tool initialized. Ready for deployment.");
        }

        #region Retro UI Theming Engine (Dark & Classic)

        private void ApplyTheme(bool isDark)
        {
            this.isDarkTheme = isDark;
            this.BackColor = isDark ? Color.FromArgb(24, 26, 32) : Color.FromArgb(212, 208, 200);
            this.ForeColor = isDark ? Color.FromArgb(220, 225, 235) : Color.Black;

            pnlHeader.BackColor = isDark ? Color.FromArgb(18, 20, 26) : Color.FromArgb(10, 36, 106);
            pnlHeader.Invalidate();

            lblTitle.ForeColor = Color.White;
            lblSubtitle.ForeColor = isDark ? Color.FromArgb(156, 163, 175) : Color.FromArgb(210, 225, 245);

            btnThemeToggle.Text = isDark ? "☀️ Classic Retro" : "🌙 Retro Dark";
            StyleRetroButton(btnThemeToggle, isDark ? Color.FromArgb(55, 65, 81) : Color.FromArgb(240, 238, 232),
                                            isDark ? Color.FromArgb(31, 41, 55) : Color.FromArgb(195, 190, 180),
                                            isDark ? Color.FromArgb(75, 85, 99) : Color.FromArgb(64, 64, 64),
                                            isDark ? Color.FromArgb(243, 244, 246) : Color.Black, isDark);

            // GroupBoxes & Controls
            Color grpBack = isDark ? Color.FromArgb(30, 34, 44) : Color.FromArgb(212, 208, 200);
            Color grpFore = isDark ? Color.FromArgb(56, 189, 248) : Color.FromArgb(0, 0, 128); // Neon Cyan vs Classic Navy
            Color labelFore = isDark ? Color.FromArgb(209, 213, 219) : Color.Black;
            Color boxBack = isDark ? Color.FromArgb(18, 20, 26) : Color.White;
            Color boxFore = isDark ? Color.FromArgb(243, 244, 246) : Color.Black;

            foreach (GroupBox grp in new GroupBox[] { grpPaths, grpActions, grpProcess, grpLog })
            {
                grp.BackColor = grpBack;
                grp.ForeColor = grpFore;
                foreach (Control c in grp.Controls)
                {
                    if (c is Label && c != lblPosStatus)
                    {
                        c.ForeColor = labelFore;
                        c.BackColor = Color.Transparent;
                    }
                    else if (c is TextBox || c is ComboBox)
                    {
                        c.BackColor = boxBack;
                        c.ForeColor = boxFore;
                    }
                    else if (c is CheckBox)
                    {
                        c.ForeColor = labelFore;
                        c.BackColor = Color.Transparent;
                    }
                }
            }

            // Deployment Buttons
            // DLLs (Blue)
            StyleRetroButton(btnDeployDlls, isDark ? Color.FromArgb(37, 99, 235) : Color.FromArgb(50, 100, 200),
                                            isDark ? Color.FromArgb(29, 78, 216) : Color.FromArgb(20, 60, 150),
                                            isDark ? Color.FromArgb(30, 64, 175) : Color.FromArgb(10, 40, 120),
                                            Color.White, isDark);
            // Exe (Indigo)
            StyleRetroButton(btnDeployExe, isDark ? Color.FromArgb(124, 58, 237) : Color.FromArgb(90, 60, 180),
                                           isDark ? Color.FromArgb(109, 40, 217) : Color.FromArgb(60, 35, 140),
                                           isDark ? Color.FromArgb(91, 33, 182) : Color.FromArgb(40, 20, 100),
                                           Color.White, isDark);
            // Everything (Emerald Green)
            StyleRetroButton(btnDeployAll, isDark ? Color.FromArgb(5, 150, 105) : Color.FromArgb(40, 140, 60),
                                           isDark ? Color.FromArgb(4, 120, 87) : Color.FromArgb(25, 100, 40),
                                           isDark ? Color.FromArgb(6, 95, 70) : Color.FromArgb(15, 70, 25),
                                           Color.White, isDark);
            // Rollback (Amber)
            StyleRetroButton(btnRollback, isDark ? Color.FromArgb(217, 119, 6) : Color.FromArgb(190, 90, 20),
                                          isDark ? Color.FromArgb(180, 83, 9) : Color.FromArgb(150, 65, 10),
                                          isDark ? Color.FromArgb(146, 64, 14) : Color.FromArgb(110, 45, 5),
                                          Color.White, isDark);

            // Process Buttons
            // Start POS (Green)
            StyleRetroButton(btnStartPos, isDark ? Color.FromArgb(22, 163, 74) : Color.FromArgb(34, 139, 34),
                                          isDark ? Color.FromArgb(21, 128, 61) : Color.FromArgb(20, 100, 20),
                                          isDark ? Color.FromArgb(22, 101, 52) : Color.FromArgb(10, 70, 10),
                                          Color.White, isDark);
            // Stop POS (Crimson)
            StyleRetroButton(btnStopPos, isDark ? Color.FromArgb(220, 38, 38) : Color.FromArgb(180, 30, 30),
                                         isDark ? Color.FromArgb(185, 28, 28) : Color.FromArgb(140, 20, 20),
                                         isDark ? Color.FromArgb(153, 27, 27) : Color.FromArgb(100, 10, 10),
                                         Color.White, isDark);
            // Restart POS (Cyan/Blue)
            StyleRetroButton(btnRestartPos, isDark ? Color.FromArgb(2, 132, 199) : Color.FromArgb(30, 110, 180),
                                            isDark ? Color.FromArgb(3, 105, 161) : Color.FromArgb(15, 80, 140),
                                            isDark ? Color.FromArgb(7, 89, 133) : Color.FromArgb(10, 55, 100),
                                            Color.White, isDark);
            // Output.log (Purple)
            StyleRetroButton(btnOpenPosLog, isDark ? Color.FromArgb(147, 51, 234) : Color.FromArgb(110, 50, 160),
                                            isDark ? Color.FromArgb(126, 34, 206) : Color.FromArgb(80, 30, 125),
                                            isDark ? Color.FromArgb(107, 33, 168) : Color.FromArgb(55, 20, 90),
                                            Color.White, isDark);

            // Utility buttons
            Color utilTop = isDark ? Color.FromArgb(55, 65, 81) : Color.FromArgb(240, 238, 232);
            Color utilBottom = isDark ? Color.FromArgb(31, 41, 55) : Color.FromArgb(195, 190, 180);
            Color utilBorder = isDark ? Color.FromArgb(75, 85, 99) : Color.FromArgb(64, 64, 64);
            Color utilText = isDark ? Color.FromArgb(243, 244, 246) : Color.Black;

            StyleRetroButton(btnBrowseSrc, utilTop, utilBottom, utilBorder, utilText, isDark);
            StyleRetroButton(btnBrowseInst, utilTop, utilBottom, utilBorder, utilText, isDark);
            StyleRetroButton(btnBrowseMsb, utilTop, utilBottom, utilBorder, utilText, isDark);
            StyleRetroButton(btnSaveConfig, utilTop, utilBottom, utilBorder, utilText, isDark);
            StyleRetroButton(btnOpenLog, utilTop, utilBottom, utilBorder, utilText, isDark);
            StyleRetroButton(btnClearLog, utilTop, utilBottom, utilBorder, utilText, isDark);

            // Log Console
            pnlLogTools.BackColor = grpBack;
            rtbLog.BackColor = isDark ? Color.FromArgb(15, 20, 28) : Color.FromArgb(0, 0, 0);
            rtbLog.ForeColor = isDark ? Color.FromArgb(226, 232, 240) : Color.FromArgb(51, 255, 51); // Classic Green CRT in retro classic

            // Status strip
            statusStrip.BackColor = isDark ? Color.FromArgb(18, 20, 26) : Color.FromArgb(212, 208, 200);
            lblStatus.ForeColor = isDark ? Color.FromArgb(156, 163, 175) : Color.Black;

            UpdatePosStatus();
        }

        private static void StyleRetroButton(Button btn, Color top, Color bottom, Color border, Color text, bool isDark)
        {
            RetroButtonTheme theme = btn.Tag as RetroButtonTheme;
            if (theme == null)
            {
                theme = new RetroButtonTheme();
                btn.Tag = theme;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
                btn.Cursor = Cursors.Hand;

                bool isHovered = false;
                bool isPressed = false;

                btn.MouseEnter += (s, e) => { isHovered = true; btn.Invalidate(); };
                btn.MouseLeave += (s, e) => { isHovered = false; isPressed = false; btn.Invalidate(); };
                btn.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isPressed = true; btn.Invalidate(); } };
                btn.MouseUp += (s, e) => { isPressed = false; btn.Invalidate(); };

                btn.Paint += (s, e) =>
                {
                    RetroButtonTheme t = btn.Tag as RetroButtonTheme;
                    if (t == null) return;

                    Graphics g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Rectangle rect = btn.ClientRectangle;
                    rect.Width -= 1;
                    rect.Height -= 1;

                    if (rect.Width <= 0 || rect.Height <= 0) return;

                    Color cTop = t.TopColor;
                    Color cBottom = t.BottomColor;

                    if (!btn.Enabled)
                    {
                        cTop = t.IsDark ? Color.FromArgb(50, 54, 62) : Color.FromArgb(200, 200, 200);
                        cBottom = t.IsDark ? Color.FromArgb(35, 38, 45) : Color.FromArgb(170, 170, 170);
                    }
                    else if (isPressed)
                    {
                        cTop = t.BottomColor;
                        cBottom = t.TopColor;
                    }
                    else if (isHovered)
                    {
                        cTop = Color.FromArgb(Math.Min(255, t.TopColor.R + 25), Math.Min(255, t.TopColor.G + 25), Math.Min(255, t.TopColor.B + 25));
                        cBottom = Color.FromArgb(Math.Min(255, t.BottomColor.R + 20), Math.Min(255, t.BottomColor.G + 20), Math.Min(255, t.BottomColor.B + 20));
                    }

                    // Draw retro rounded rectangle
                    int radius = 3;
                    using (GraphicsPath path = GetRoundedRect(rect, radius))
                    {
                        using (LinearGradientBrush brush = new LinearGradientBrush(rect, cTop, cBottom, LinearGradientMode.Vertical))
                        {
                            g.FillPath(brush, path);
                        }

                        // Subtle retro top highlight line
                        if (btn.Enabled && !isPressed)
                        {
                            using (Pen hlPen = new Pen(Color.FromArgb(t.IsDark ? 45 : 120, 255, 255, 255), 1))
                            {
                                g.DrawLine(hlPen, rect.X + 2, rect.Y + 1, rect.Right - 2, rect.Y + 1);
                            }
                        }

                        // Crisp Border
                        Color bColor = btn.Enabled ? t.BorderColor : (t.IsDark ? Color.FromArgb(60, 65, 75) : Color.FromArgb(140, 140, 140));
                        using (Pen pen = new Pen(bColor, 1.2F))
                        {
                            g.DrawPath(pen, path);
                        }
                    }

                    // Centered text with tactile press offset
                    TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak;
                    Rectangle textRect = btn.ClientRectangle;
                    if (isPressed) textRect.Offset(1, 1);

                    Color curTextColor = btn.Enabled ? t.TextColor : (t.IsDark ? Color.FromArgb(110, 115, 125) : Color.FromArgb(128, 128, 128));

                    if (btn.Enabled && t.IsDark && t.TextColor == Color.White)
                    {
                        Rectangle shadowRect = textRect;
                        shadowRect.Offset(1, 1);
                        TextRenderer.DrawText(g, btn.Text, btn.Font, shadowRect, Color.FromArgb(110, 0, 0, 0), flags);
                    }
                    TextRenderer.DrawText(g, btn.Text, btn.Font, textRect, curTextColor, flags);
                };
            }

            theme.TopColor = top;
            theme.BottomColor = bottom;
            theme.BorderColor = border;
            theme.TextColor = text;
            theme.IsDark = isDark;
            btn.Invalidate();
        }

        private static GraphicsPath GetRoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (rect.Width < d || rect.Height < d)
            {
                path.AddRectangle(rect);
                return path;
            }
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        #endregion

        private void BrowseFolder(TextBox targetBox, string desc)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = desc;
                if (Directory.Exists(targetBox.Text))
                {
                    fbd.SelectedPath = targetBox.Text;
                }
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    targetBox.Text = fbd.SelectedPath;
                    SaveConfigFromControls(false);
                }
            }
        }

        private void BrowseFile(TextBox targetBox, string filter)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = filter;
                if (File.Exists(targetBox.Text))
                {
                    ofd.InitialDirectory = Path.GetDirectoryName(targetBox.Text);
                }
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    targetBox.Text = ofd.FileName;
                    SaveConfigFromControls(false);
                }
            }
        }

        private void PopulateControlsFromConfig()
        {
            txtSourceRoot.Text = currentConfig.SourceCodeRoot;
            txtInstallDir.Text = currentConfig.InstallDir;
            txtMSBuild.Text = currentConfig.MSBuildPath;
            cmbConfig.SelectedItem = currentConfig.Configuration;
            if (cmbConfig.SelectedIndex < 0) cmbConfig.SelectedIndex = 0;
            cmbPlatform.SelectedItem = currentConfig.Platform;
            if (cmbPlatform.SelectedIndex < 0) cmbPlatform.SelectedIndex = 0;
            chkAutoRestart.Checked = currentConfig.AutoRestart;
        }

        private void SaveConfigFromControls(bool showMessage)
        {
            currentConfig.SourceCodeRoot = txtSourceRoot.Text.Trim();
            currentConfig.InstallDir = txtInstallDir.Text.Trim();
            currentConfig.MSBuildPath = txtMSBuild.Text.Trim();
            currentConfig.Configuration = cmbConfig.SelectedItem.ToString();
            currentConfig.Platform = cmbPlatform.SelectedItem.ToString();
            currentConfig.AutoRestart = chkAutoRestart.Checked;
            currentConfig.Theme = isDarkTheme ? "Dark" : "Classic";
            currentConfig.Save();
            if (showMessage)
            {
                MessageBox.Show(this, "Settings saved successfully to deployer_config.json!", "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SetBusy(bool busy, string statusText)
        {
            isBusy = busy;
            btnDeployDlls.Enabled = !busy;
            btnDeployExe.Enabled = !busy;
            btnDeployAll.Enabled = !busy;
            btnRollback.Enabled = !busy;
            btnSaveConfig.Enabled = !busy;
            btnStartPos.Enabled = !busy;
            btnStopPos.Enabled = !busy;
            btnRestartPos.Enabled = !busy;
            btnThemeToggle.Enabled = !busy;
            lblStatus.Text = statusText;
            progressBar.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
            progressBar.Value = busy ? 0 : (statusText.Contains("Failed") ? 0 : 100);
        }

        #region Grand POS Process & Diagnostics

        private void UpdatePosStatus()
        {
            if (this.IsDisposed || lblPosStatus.IsDisposed) return;
            try
            {
                Process[] procs = Process.GetProcessesByName("GrandPOS");
                if (procs.Length > 0)
                {
                    lblPosStatus.Text = string.Format("● POS: Running (PID: {0})", procs[0].Id);
                    lblPosStatus.ForeColor = isDarkTheme ? Color.FromArgb(16, 185, 129) : Color.FromArgb(34, 139, 34);
                }
                else
                {
                    lblPosStatus.Text = "○ POS: Stopped";
                    lblPosStatus.ForeColor = isDarkTheme ? Color.FromArgb(156, 163, 175) : Color.FromArgb(128, 128, 128);
                }
            }
            catch {}
        }

        private void StartGrandPOS()
        {
            string installDir = txtInstallDir.Text.Trim();
            if (string.IsNullOrEmpty(installDir)) installDir = currentConfig.InstallDir;

            string runExe = Path.Combine(installDir, "GrandPOS.exe");
            if (!File.Exists(runExe))
            {
                LogError("GrandPOS.exe not found at: " + runExe);
                MessageBox.Show(this, "GrandPOS.exe was not found at:\n" + runExe + "\n\nPlease verify your Grand POS installation path.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Process[] existing = Process.GetProcessesByName("GrandPOS");
            if (existing.Length > 0)
            {
                DialogResult dr = MessageBox.Show(this, string.Format("GrandPOS is already running (PID: {0}).\n\nDo you want to launch another instance?", existing[0].Id), "Already Running", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr != DialogResult.Yes) return;
            }

            try
            {
                LogStep("Starting Grand POS (" + runExe + ")...");
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = runExe;
                psi.WorkingDirectory = installDir;
                psi.UseShellExecute = true;
                Process p = Process.Start(psi);
                LogSuccess(string.Format("Grand POS started successfully (PID: {0}).", p != null ? p.Id.ToString() : "N/A"));
                UpdatePosStatus();
            }
            catch (Exception ex)
            {
                LogError("Failed to start Grand POS: " + ex.Message);
                MessageBox.Show(this, "Failed to start Grand POS:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopGrandPOS()
        {
            Process[] procs = Process.GetProcessesByName("GrandPOS");
            if (procs.Length == 0)
            {
                LogInfo("GrandPOS is not running.");
                MessageBox.Show(this, "GrandPOS is not currently running.", "GrandPOS Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            LogStep("Stopping Grand POS...");
            StopProcess("GrandPOS");
            UpdatePosStatus();
        }

        private async Task RestartGrandPOS()
        {
            if (isBusy) return;
            string installDir = txtInstallDir.Text.Trim();
            if (string.IsNullOrEmpty(installDir)) installDir = currentConfig.InstallDir;

            string runExe = Path.Combine(installDir, "GrandPOS.exe");
            if (!File.Exists(runExe))
            {
                LogError("GrandPOS.exe not found at: " + runExe);
                MessageBox.Show(this, "GrandPOS.exe not found at:\n" + runExe, "Restart Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SetBusy(true, "Restarting Grand POS...");
            try
            {
                await Task.Run(() =>
                {
                    LogStep("Restarting Grand POS...");
                    StopProcess("GrandPOS");
                    System.Threading.Thread.Sleep(700);

                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = runExe;
                    psi.WorkingDirectory = installDir;
                    psi.UseShellExecute = true;
                    Process p = Process.Start(psi);
                    LogSuccess(string.Format("Grand POS restarted successfully (PID: {0}).", p != null ? p.Id.ToString() : "N/A"));
                });
                SetBusy(false, "Grand POS restarted successfully");
                UpdatePosStatus();
            }
            catch (Exception ex)
            {
                LogError("Failed to restart Grand POS: " + ex.Message);
                SetBusy(false, "Restart Failed");
                MessageBox.Show(this, "Failed to restart Grand POS:\n" + ex.Message, "Restart Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenOutputLog()
        {
            string installDir = txtInstallDir.Text.Trim();
            if (string.IsNullOrEmpty(installDir)) installDir = currentConfig.InstallDir;

            string logPath = Path.Combine(installDir, "Output.log");
            if (!File.Exists(logPath))
            {
                string alt = Path.Combine(installDir, "output.log");
                if (File.Exists(alt)) logPath = alt;
            }

            if (!File.Exists(logPath))
            {
                LogError("Output.log was not found in: " + installDir);
                MessageBox.Show(this, "Output.log was not found at:\n" + logPath + "\n\nGrand POS might not have generated any logs yet in this directory.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                LogInfo("Opening Grand POS Output.log: " + logPath);
                Process.Start("notepad.exe", logPath);
            }
            catch (Exception ex)
            {
                LogError("Could not open Output.log: " + ex.Message);
                MessageBox.Show(this, "Could not open Output.log:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Logging Methods

        private static readonly object logFileLock = new object();
        private string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GrandPOS_Deployer.log");

        private void AppendText(string text, Color color, bool bold = false)
        {
            string clean = text.TrimStart('\r', '\n');
            if (!string.IsNullOrEmpty(clean))
            {
                string timeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string fileLine = string.Format("[{0}] {1}", timeStr, clean);
                try
                {
                    lock (logFileLock)
                    {
                        File.AppendAllText(logFilePath, fileLine + Environment.NewLine, Encoding.UTF8);
                    }
                }
                catch {}
            }

            if (rtbLog.InvokeRequired)
            {
                rtbLog.Invoke(new Action<string, Color, bool>(AppendText), text, color, bold);
                return;
            }
            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor = color;
            rtbLog.SelectionFont = new Font(rtbLog.Font, bold ? FontStyle.Bold : FontStyle.Regular);
            rtbLog.AppendText(text + Environment.NewLine);
            rtbLog.ScrollToCaret();
        }

        private void OpenLogFile()
        {
            try
            {
                if (!File.Exists(logFilePath))
                {
                    File.WriteAllText(logFilePath, "-- Grand POS Automated Deployer Activity Log --\r\n", Encoding.UTF8);
                }
                Process.Start("notepad.exe", logFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open log file: " + ex.Message, "Log File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void LogStep(string msg) { AppendText("\n[STEP] " + msg, Color.FromArgb(52, 152, 219), true); }
        public void LogSuccess(string msg) { AppendText("[SUCCESS] " + msg, Color.FromArgb(46, 204, 113), true); }
        public void LogWarning(string msg) { AppendText("[WARNING] " + msg, Color.FromArgb(241, 196, 15), false); }
        public void LogError(string msg) { AppendText("[ERROR] " + msg, Color.FromArgb(231, 76, 60), true); }
        public void LogInfo(string msg) { AppendText(msg, Color.FromArgb(200, 200, 200), false); }

        #endregion

        #region Execution Engine

        private async Task ExecuteDeployment(string mode)
        {
            if (isBusy) return;
            SaveConfigFromControls(false);

            string srcRoot = currentConfig.SourceCodeRoot;
            string installDir = currentConfig.InstallDir;
            string msbuildExe = currentConfig.MSBuildPath;
            string config = currentConfig.Configuration;
            string platform = currentConfig.Platform;
            bool autoRestart = currentConfig.AutoRestart;

            SetBusy(true, "Executing " + mode + "...");
            var sw = Stopwatch.StartNew();

            try
            {
                await Task.Run(() =>
                {
                    LogStep(string.Format("Starting Deployment Operation: Mode={0}, Config={1}, Platform={2}", mode, config, platform));

                    // Upfront Path Validations
                    if (string.IsNullOrEmpty(srcRoot) || !Directory.Exists(srcRoot))
                    {
                        throw new DirectoryNotFoundException(string.Format("Source Code Root directory does not exist:\n'{0}'\n\nPlease click 'Browse...' to select your local source code folder.", srcRoot));
                    }

                    if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
                    {
                        throw new DirectoryNotFoundException(string.Format("Grand POS Installation folder does not exist:\n'{0}'\n\nPlease click 'Browse...' to select the Grand POS folder.", installDir));
                    }

                    if (string.IsNullOrEmpty(msbuildExe) || !File.Exists(msbuildExe))
                    {
                        throw new FileNotFoundException(string.Format("MSBuild executable not found:\n'{0}'\n\nPlease click 'Browse...' to locate MSBuild.exe on this machine.", msbuildExe));
                    }

                    // Handle Rollback
                    if (mode == "Rollback")
                    {
                        DoRollback(installDir, autoRestart);
                        return;
                    }

                    // Setup Deployment Manifest
                    List<DeploymentItem> itemsToDeploy = new List<DeploymentItem>();

                    string exeSolutionPath = Path.Combine(srcRoot, @"grand_pos\pos_master\GOLDPOS.sln");
                    string exeSourceFile = Path.Combine(@"C:\AlData\GOLDPOSECR", "GrandPOS.exe");
                    string exeDestFile = Path.Combine(installDir, "GrandPOS.exe");

                    if (mode == "All" || mode == "ExeOnly")
                    {
                        if (!File.Exists(exeSolutionPath))
                        {
                            throw new FileNotFoundException(string.Format("Grand POS executable solution not found:\n'{0}'\n\nEnsure Source Code Root contains 'grand_pos\\pos_master\\GOLDPOS.sln'.", exeSolutionPath));
                        }
                        itemsToDeploy.Add(new DeploymentItem { Name = "GrandPOS.exe", Source = exeSourceFile, Destination = exeDestFile, Solution = exeSolutionPath });
                    }

                    string dllSolutionPath = Path.Combine(srcRoot, @"grand_pos_pricing\Pricing.sln");
                    string dllBuildDir = Path.Combine(srcRoot, @"grand_pos_pricing", config);

                    string[] dllNames = new string[] {
                        "MixAndMatch.dll",
                        "PriceManager.dll",
                        "FixedPrice.dll",
                        "LabelPrice.dll",
                        "ListPrice.dll",
                        "DataObjectManager.dll"
                    };

                    if (mode == "All" || mode == "DLLsOnly")
                    {
                        if (!File.Exists(dllSolutionPath))
                        {
                            throw new FileNotFoundException(string.Format("Pricing DLL solution not found:\n'{0}'\n\nEnsure Source Code Root contains 'grand_pos_pricing\\Pricing.sln'.", dllSolutionPath));
                        }
                        foreach (string dll in dllNames)
                        {
                            itemsToDeploy.Add(new DeploymentItem {
                                Name = dll,
                                Source = Path.Combine(dllBuildDir, dll),
                                Destination = Path.Combine(installDir, dll),
                                Solution = dllSolutionPath
                            });
                        }
                    }

                    // Step 1: Build Required Solutions
                    if (mode == "All" || mode == "ExeOnly")
                    {
                        BuildSolution(msbuildExe, exeSolutionPath, config, platform);
                    }

                    if (mode == "All" || mode == "DLLsOnly")
                    {
                        BuildSolution(msbuildExe, dllSolutionPath, config, platform);
                    }

                    // Step 2: Verify Build Outputs
                    LogStep("Verifying Build Output Files Before Stopping POS...");
                    foreach (var item in itemsToDeploy)
                    {
                        if (!File.Exists(item.Source))
                        {
                            throw new FileNotFoundException(string.Format("Expected build output missing: {0}", item.Source));
                        }
                        FileInfo fi = new FileInfo(item.Source);
                        LogInfo(string.Format("  [OK] {0} ({1} bytes, modified {2:HH:mm:ss})", item.Name, fi.Length, fi.LastWriteTime));
                    }
                    LogSuccess("All generated binaries verified.");

                    // Step 3: Safely Terminate GrandPOS process
                    LogStep("Checking for running GrandPOS process...");
                    StopProcess("GrandPOS");

                    // Step 4: Verify Files Are Unlocked
                    LogStep("Checking file write locks on target files...");
                    foreach (var item in itemsToDeploy)
                    {
                        if (File.Exists(item.Destination))
                        {
                            if (!IsFileUnlocked(item.Destination))
                            {
                                throw new IOException("Target file is locked by another program: " + item.Destination);
                            }
                        }
                    }
                    LogSuccess("Target files are unlocked.");

                    // Step 5: Create Timestamped Backup
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string backupFolder = Path.Combine(installDir, "Backups", timestamp);
                    Directory.CreateDirectory(backupFolder);
                    LogStep("Creating backup in: " + backupFolder);

                    int backedUp = 0;
                    foreach (var item in itemsToDeploy)
                    {
                        if (File.Exists(item.Destination))
                        {
                            string bkpPath = Path.Combine(backupFolder, item.Name);
                            File.Copy(item.Destination, bkpPath, true);
                            backedUp++;
                        }
                    }
                    LogSuccess(string.Format("Backed up {0} existing files successfully.", backedUp));

                    // Step 6: Deploy New Files
                    LogStep("Deploying updated files to " + installDir + "...");
                    try
                    {
                        foreach (var item in itemsToDeploy)
                        {
                            File.Copy(item.Source, item.Destination, true);
                            FileInfo destFi = new FileInfo(item.Destination);
                            FileInfo srcFi = new FileInfo(item.Source);
                            if (destFi.Length != srcFi.Length)
                            {
                                throw new IOException(string.Format("Verification failed: Size mismatch for {0} (src={1}, dst={2})", item.Name, srcFi.Length, destFi.Length));
                            }
                            LogInfo(string.Format("  Deployed: {0} [{1} bytes]", item.Name, destFi.Length));
                        }
                        LogSuccess(string.Format("All {0} files successfully deployed and verified.", itemsToDeploy.Count));
                    }
                    catch (Exception copyEx)
                    {
                        LogError("Error during copy: " + copyEx.Message);
                        LogWarning("Triggering automatic rollback from: " + backupFolder);
                        RestoreFromBackup(backupFolder, installDir);
                        throw new Exception("Deployment copy failed. Automatic rollback completed.", copyEx);
                    }

                    // Step 7: Restart Application
                    if (autoRestart)
                    {
                        string runExe = Path.Combine(installDir, "GrandPOS.exe");
                        if (File.Exists(runExe))
                        {
                            LogStep("Restarting Grand POS (" + runExe + ")...");
                            ProcessStartInfo psi = new ProcessStartInfo();
                            psi.FileName = runExe;
                            psi.WorkingDirectory = installDir;
                            psi.UseShellExecute = true;
                            Process.Start(psi);
                            LogSuccess("Grand POS restarted successfully.");
                        }
                    }
                    else
                    {
                        LogWarning("Auto-restart skipped as per option setting.");
                    }
                });

                sw.Stop();
                LogSuccess(string.Format("Operation '{0}' Completed in {1:0.0}s!", mode, sw.Elapsed.TotalSeconds));
                SetBusy(false, "Completed successfully in " + sw.Elapsed.TotalSeconds.ToString("0.0") + "s");
            }
            catch (Exception ex)
            {
                sw.Stop();
                LogError("FAILED: " + ex.Message);
                SetBusy(false, "Deployment Failed");
                MessageBox.Show(this, "Deployment encountered an error:\n\n" + ex.Message, "Deployment Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BuildSolution(string msbuildExe, string solutionPath, string config, string platform)
        {
            string solName = Path.GetFileName(solutionPath);
            LogStep(string.Format("Building {0} [{1}|{2}]...", solName, config, platform));

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = msbuildExe;
            psi.Arguments = string.Format("\"{0}\" /t:Build /p:Configuration={1} /p:Platform={2} /m /v:m /nr:false", solutionPath, config, platform);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) LogInfo("  " + e.Data); };
                p.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) LogWarning("  [ERR] " + e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();

                if (p.ExitCode != 0)
                {
                    throw new Exception(string.Format("MSBuild failed for '{0}' with exit code {1}.", solName, p.ExitCode));
                }
            }
            LogSuccess(string.Format("Build succeeded for '{0}'.", solName));
        }

        private void StopProcess(string procName)
        {
            Process[] procs = Process.GetProcessesByName(procName);
            if (procs.Length == 0)
            {
                LogInfo("  No running process named '" + procName + "' found.");
                return;
            }

            foreach (var p in procs)
            {
                try
                {
                    LogWarning(string.Format("  Terminating {0} (PID: {1})...", procName, p.Id));
                    p.CloseMainWindow();
                    if (!p.WaitForExit(5000))
                    {
                        p.Kill();
                        p.WaitForExit(3000);
                    }
                }
                catch {}
            }
            LogSuccess("Process '" + procName + "' stopped.");
        }

        private bool IsFileUnlocked(string filePath)
        {
            try
            {
                using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private void DoRollback(string installDir, bool autoRestart)
        {
            LogStep("Looking for previous backups in " + installDir + "...");
            string bkpRoot = Path.Combine(installDir, "Backups");
            if (!Directory.Exists(bkpRoot))
            {
                throw new DirectoryNotFoundException("No Backups folder found in " + installDir);
            }

            string[] dirs = Directory.GetDirectories(bkpRoot);
            if (dirs.Length == 0)
            {
                throw new FileNotFoundException("No backup directories found in " + bkpRoot);
            }

            Array.Sort(dirs);
            string latest = dirs[dirs.Length - 1];
            LogWarning("Restoring from latest backup: " + latest);

            StopProcess("GrandPOS");
            RestoreFromBackup(latest, installDir);
            LogSuccess("Rollback completed successfully.");

            if (autoRestart)
            {
                string runExe = Path.Combine(installDir, "GrandPOS.exe");
                if (File.Exists(runExe))
                {
                    Process.Start(new ProcessStartInfo(runExe) { WorkingDirectory = installDir, UseShellExecute = true });
                    LogSuccess("Grand POS restarted.");
                }
            }
        }

        private void RestoreFromBackup(string backupDir, string installDir)
        {
            foreach (string file in Directory.GetFiles(backupDir))
            {
                string fn = Path.GetFileName(file);
                string dst = Path.Combine(installDir, fn);
                File.Copy(file, dst, true);
                LogInfo("  Restored: " + fn);
            }
        }

        #endregion

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class DeploymentItem
    {
        public string Name { get; set; }
        public string Source { get; set; }
        public string Destination { get; set; }
        public string Solution { get; set; }
    }

    public class RetroButtonTheme
    {
        public Color TopColor;
        public Color BottomColor;
        public Color BorderColor;
        public Color TextColor;
        public bool IsDark;
    }
}
