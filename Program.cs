using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GrandPOS.Deployer
{
    #region Configuration Data Model

    public class DeployMarker
    {
        public DateTime Timestamp { get; set; }
        public string Mode { get; set; }
        public string Configuration { get; set; }
        public string Platform { get; set; }
    }

    public class ConfigData
    {
        public string SourceCodeRoot { get; set; }
        public string InstallDir { get; set; }
        public string MSBuildPath { get; set; }
        public string Configuration { get; set; }
        public string Platform { get; set; }
        public bool AutoRestart { get; set; }
        public bool SwitchToOutputLogAfterDeploy { get; set; }
        public string Theme { get; set; }
        public List<DeployMarker> DeployHistory { get; set; }

        public ConfigData()
        {
            SourceCodeRoot = @"D:\KSM\Projects\POS\Source Code";
            InstallDir = @"C:\Grand\GOLDPOSECR";
            MSBuildPath = @"C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe";
            Configuration = "Release";
            Platform = "Win32";
            AutoRestart = true;
            SwitchToOutputLogAfterDeploy = false;
            Theme = "Dark";
            DeployHistory = new List<DeployMarker>();
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
                string switchOutput = ExtractJsonValue(json, "SwitchToOutputLogAfterDeploy", "false");
                cfg.SwitchToOutputLogAfterDeploy = switchOutput.ToLower() == "true";
                cfg.Theme = ExtractJsonValue(json, "Theme", "Dark");
                cfg.DeployHistory = ExtractDeployHistory(json);
            }
            catch { }

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
            sb.AppendFormat("  \"SwitchToOutputLogAfterDeploy\": {0},\r\n", SwitchToOutputLogAfterDeploy ? "true" : "false");
            sb.AppendFormat("  \"Theme\": \"{0}\",\r\n", EscapeJson(Theme));
            sb.AppendLine("  \"DeployHistory\": [");
            if (DeployHistory != null && DeployHistory.Count > 0)
            {
                int startIdx = Math.Max(0, DeployHistory.Count - 50);
                for (int i = startIdx; i < DeployHistory.Count; i++)
                {
                    DeployMarker m = DeployHistory[i];
                    string comma = (i < DeployHistory.Count - 1) ? "," : "";
                    sb.AppendFormat("    {{ \"Timestamp\": \"{0}\", \"Mode\": \"{1}\", \"Configuration\": \"{2}\", \"Platform\": \"{3}\" }}{4}\r\n",
                        m.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture),
                        EscapeJson(m.Mode ?? ""),
                        EscapeJson(m.Configuration ?? ""),
                        EscapeJson(m.Platform ?? ""),
                        comma);
                }
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString());
        }

        private static List<DeployMarker> ExtractDeployHistory(string json)
        {
            List<DeployMarker> list = new List<DeployMarker>();
            try
            {
                Match arrMatch = Regex.Match(json, "\"DeployHistory\"\\s*:\\s*\\[(?<items>[\\s\\S]*?)\\]");
                if (!arrMatch.Success) return list;
                MatchCollection objMatches = Regex.Matches(arrMatch.Groups["items"].Value, "\\{([^}]*)\\}");
                foreach (Match om in objMatches)
                {
                    string objText = om.Groups[1].Value;
                    string tsStr = ExtractJsonValue(objText, "Timestamp", "");
                    DateTime ts;
                    if (DateTime.TryParseExact(tsStr, "yyyy-MM-ddTHH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out ts)
                        || DateTime.TryParse(tsStr, out ts))
                    {
                        DeployMarker marker = new DeployMarker();
                        marker.Timestamp = ts;
                        marker.Mode = ExtractJsonValue(objText, "Mode", "All");
                        marker.Configuration = ExtractJsonValue(objText, "Configuration", "Release");
                        marker.Platform = ExtractJsonValue(objText, "Platform", "Win32");
                        list.Add(marker);
                    }
                }
            }
            catch { }
            return list;
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

    #endregion

    #region Custom UI Controls

    public class DarkComboBox : ComboBox
    {
        public DarkComboBox()
        {
            this.DrawMode = DrawMode.OwnerDrawFixed;
            this.DropDownStyle = ComboBoxStyle.DropDownList;
            this.BackColor = Color.FromArgb(20, 25, 35);
            this.ForeColor = Color.FromArgb(240, 243, 246);
            this.ItemHeight = 20;
            this.FlatStyle = FlatStyle.Flat;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            Graphics g = e.Graphics;
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color bg = isSelected ? Color.FromArgb(37, 99, 235) : Color.FromArgb(20, 25, 35);
            Color fg = isSelected ? Color.White : Color.FromArgb(240, 243, 246);

            using (SolidBrush b = new SolidBrush(bg))
            {
                g.FillRectangle(b, e.Bounds);
            }

            string text = this.Items[e.Index].ToString();
            Rectangle textRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(g, text, this.Font, textRect, fg, flags);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x000F) // WM_PAINT
            {
                using (Graphics g = Graphics.FromHwnd(this.Handle))
                {
                    using (Pen p = new Pen(Color.FromArgb(55, 65, 81), 1))
                    {
                        g.DrawRectangle(p, 0, 0, this.Width - 1, this.Height - 1);
                    }

                    int arrowX = this.Width - 16;
                    int arrowY = (this.Height / 2) - 2;
                    Point[] arrow = new Point[]
                    {
                        new Point(arrowX, arrowY),
                        new Point(arrowX + 8, arrowY),
                        new Point(arrowX + 4, arrowY + 5)
                    };
                    using (SolidBrush ab = new SolidBrush(Color.FromArgb(156, 163, 175)))
                    {
                        g.FillPolygon(ab, arrow);
                    }
                }
            }
        }
    }

    public class ModernButton : Button
    {
        public Color NormalColor { get; set; }
        public Color HoverColor { get; set; }
        public Color PressedColor { get; set; }
        public Color BorderColor { get; set; }
        public Color NormalTextColor { get; set; }
        public int CornerRadius { get; set; }

        private bool isHovered = false;
        private bool isPressed = false;

        public ModernButton()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.Cursor = Cursors.Hand;
            this.DoubleBuffered = true;
            this.CornerRadius = 4;

            NormalColor = Color.FromArgb(28, 35, 48);
            HoverColor = Color.FromArgb(42, 52, 70);
            PressedColor = Color.FromArgb(20, 25, 35);
            BorderColor = Color.FromArgb(55, 65, 81);
            NormalTextColor = Color.FromArgb(226, 232, 240);

            this.MouseEnter += (s, e) => { isHovered = true; Invalidate(); };
            this.MouseLeave += (s, e) => { isHovered = false; isPressed = false; Invalidate(); };
            this.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { isPressed = true; Invalidate(); } };
            this.MouseUp += (s, e) => { isPressed = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = this.ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            Color bgColor = NormalColor;
            Color bdColor = BorderColor;
            Color fgColor = NormalTextColor;

            if (!this.Enabled)
            {
                bgColor = Color.FromArgb(24, 28, 38);
                bdColor = Color.FromArgb(45, 52, 65);
                fgColor = Color.FromArgb(90, 100, 115);
            }
            else if (isPressed)
            {
                bgColor = PressedColor;
            }
            else if (isHovered)
            {
                bgColor = HoverColor;
            }

            using (GraphicsPath path = GetRoundedRect(rect, CornerRadius))
            {
                using (SolidBrush brush = new SolidBrush(bgColor))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(bdColor, 1.0f))
                {
                    g.DrawPath(pen, path);
                }
            }

            Rectangle textRect = this.ClientRectangle;
            if (isPressed) textRect.Offset(1, 1);
            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak;
            TextRenderer.DrawText(g, this.Text, this.Font, textRect, fgColor, flags);
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
    }

    public class FilterChipButton : Button
    {
        public bool IsActive { get; set; }
        public Color ActiveFilledColor { get; set; }

        public FilterChipButton()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.Cursor = Cursors.Hand;
            this.DoubleBuffered = true;
            this.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            ActiveFilledColor = Color.FromArgb(37, 99, 235);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = this.ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            Color bg = IsActive ? ActiveFilledColor : Color.FromArgb(20, 25, 35);
            Color bd = IsActive ? ActiveFilledColor : Color.FromArgb(55, 65, 81);
            Color fg = IsActive ? Color.White : Color.FromArgb(156, 163, 175);

            using (GraphicsPath path = GetRoundedRect(rect, 4))
            {
                using (SolidBrush brush = new SolidBrush(bg))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(bd, 1.0f))
                {
                    g.DrawPath(pen, path);
                }
            }

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(g, this.Text, this.Font, this.ClientRectangle, fg, flags);
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
    }

    public class SearchBoxControl : Panel
    {
        private TextBox textBox;
        private string placeholder = "🔍 Search log...";

        public TextBox InnerTextBox { get { return textBox; } }
        public string TextValue
        {
            get { return textBox.Text; }
            set { textBox.Text = value; }
        }

        public SearchBoxControl()
        {
            this.Size = new Size(160, 26);
            this.BackColor = Color.FromArgb(15, 20, 28);
            this.DoubleBuffered = true;

            textBox = new TextBox();
            textBox.BorderStyle = BorderStyle.None;
            textBox.BackColor = Color.FromArgb(15, 20, 28);
            textBox.ForeColor = Color.White;
            textBox.Font = new Font("Segoe UI", 8.5F);
            textBox.Location = new Point(6, 5);
            textBox.Width = this.Width - 12;
            textBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            textBox.GotFocus += (s, e) => this.Invalidate();
            textBox.LostFocus += (s, e) => this.Invalidate();
            textBox.TextChanged += (s, e) => this.Invalidate();

            this.Controls.Add(textBox);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            Color bd = textBox.Focused ? Color.FromArgb(59, 130, 246) : Color.FromArgb(55, 65, 81);
            using (Pen p = new Pen(bd, 1))
            {
                g.DrawRectangle(p, 0, 0, this.Width - 1, this.Height - 1);
            }

            if (string.IsNullOrEmpty(textBox.Text) && !textBox.Focused)
            {
                Rectangle r = new Rectangle(6, 2, this.Width - 12, this.Height - 4);
                TextRenderer.DrawText(g, placeholder, textBox.Font, r, Color.FromArgb(156, 163, 175), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }
    }

    public class SlimProgressBar : Control
    {
        private int progressValue = 0;
        private bool isMarquee = false;
        private float marqueeOffset = 0;
        private Timer animTimer;

        public int Value
        {
            get { return progressValue; }
            set
            {
                progressValue = Math.Max(0, Math.Min(100, value));
                Invalidate();
            }
        }

        public bool IsMarquee
        {
            get { return isMarquee; }
            set
            {
                isMarquee = value;
                if (isMarquee)
                {
                    if (animTimer == null)
                    {
                        animTimer = new Timer();
                        animTimer.Interval = 25;
                        animTimer.Tick += (s, e) =>
                        {
                            marqueeOffset += 0.035f;
                            if (marqueeOffset > 1.3f) marqueeOffset = -0.3f;
                            Invalidate();
                        };
                    }
                    animTimer.Start();
                }
                else
                {
                    if (animTimer != null) animTimer.Stop();
                    Invalidate();
                }
            }
        }

        public SlimProgressBar()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                           ControlStyles.AllPaintingInWmPaint |
                           ControlStyles.UserPaint |
                           ControlStyles.SupportsTransparentBackColor |
                           ControlStyles.ResizeRedraw, true);
            this.Size = new Size(180, 8);
            this.BackColor = Color.Transparent;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && animTimer != null)
            {
                animTimer.Stop();
                animTimer.Dispose();
                animTimer = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = this.ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            int radius = rect.Height / 2;

            using (GraphicsPath trackPath = GetRoundedRect(rect, radius))
            {
                using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(24, 30, 42)))
                {
                    g.FillPath(trackBrush, trackPath);
                }
                using (Pen trackPen = new Pen(Color.FromArgb(45, 55, 72), 1))
                {
                    g.DrawPath(trackPen, trackPath);
                }
            }

            if (isMarquee)
            {
                int fillW = (int)(rect.Width * 0.35f);
                int startX = rect.X + (int)(marqueeOffset * rect.Width);
                Rectangle mRect = new Rectangle(startX, rect.Y, fillW, rect.Height);
                mRect.Intersect(rect);

                if (mRect.Width > 0)
                {
                    using (GraphicsPath fillPath = GetRoundedRect(mRect, radius))
                    {
                        using (LinearGradientBrush brush = new LinearGradientBrush(
                            new Point(startX, rect.Y), new Point(startX + fillW, rect.Y),
                            Color.FromArgb(37, 99, 235), Color.FromArgb(56, 189, 248)))
                        {
                            g.FillPath(brush, fillPath);
                        }
                    }
                }
            }
            else if (progressValue > 0)
            {
                int fillW = (int)((rect.Width * progressValue) / 100.0f);
                if (fillW > 0)
                {
                    Rectangle fRect = new Rectangle(rect.X, rect.Y, fillW, rect.Height);
                    using (GraphicsPath fillPath = GetRoundedRect(fRect, radius))
                    {
                        Color c1 = progressValue >= 100 ? Color.FromArgb(16, 185, 129) : Color.FromArgb(37, 99, 235);
                        Color c2 = progressValue >= 100 ? Color.FromArgb(52, 211, 153) : Color.FromArgb(56, 189, 248);
                        using (LinearGradientBrush brush = new LinearGradientBrush(
                            fRect, c1, c2, LinearGradientMode.Horizontal))
                        {
                            g.FillPath(brush, fillPath);
                        }
                    }
                }
            }
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
    }

    public class StatusPill : Control
    {
        private bool isRunning = false;
        private int processId = 0;

        public StatusPill()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                           ControlStyles.AllPaintingInWmPaint |
                           ControlStyles.UserPaint |
                           ControlStyles.ResizeRedraw, true);
            this.Size = new Size(190, 28);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        }

        public void SetStatus(bool running, int pid)
        {
            isRunning = running;
            processId = pid;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = this.ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            Color bg = isRunning ? Color.FromArgb(16, 50, 30) : Color.FromArgb(50, 20, 25);
            Color bd = isRunning ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68);
            Color dot = isRunning ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68);
            Color fg = isRunning ? Color.FromArgb(187, 247, 208) : Color.FromArgb(254, 202, 202);

            string text = isRunning ? string.Format("POS running · PID {0}", processId) : "POS stopped";

            int radius = 12;
            using (GraphicsPath path = GetRoundedRect(rect, radius))
            {
                using (SolidBrush brush = new SolidBrush(bg))
                {
                    g.FillPath(brush, path);
                }
                using (Pen pen = new Pen(bd, 1.2f))
                {
                    g.DrawPath(pen, path);
                }
            }

            int dotSize = 8;
            int dotX = rect.X + 12;
            int dotY = rect.Y + (rect.Height - dotSize) / 2;
            using (SolidBrush bDot = new SolidBrush(dot))
            {
                g.FillEllipse(bDot, dotX, dotY, dotSize, dotSize);
            }

            Rectangle textRect = new Rectangle(dotX + dotSize + 8, rect.Y, rect.Width - (dotX + dotSize + 12), rect.Height);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(g, text, this.Font, textRect, fg, flags);
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
    }

    #endregion

    #region Log Data Models & Virtual Log Viewer

    public enum LogLevel
    {
        Info,
        Step,
        Success,
        Warning,
        Error,
        Other
    }

    public enum FilterMode
    {
        All,
        Errors,
        Warnings,
        Info
    }

    public class LogLine
    {
        public int LineNumber { get; set; }
        public string Timestamp { get; set; }
        public string Text { get; set; }
        public LogLevel Level { get; set; }
        public string WarningCode { get; set; }
        public bool IsWarningGroup { get; set; }
        public int WarningCount { get; set; }
        public List<LogLine> GroupChildren { get; set; }
        public bool IsExpanded { get; set; }

        public LogLine()
        {
            GroupChildren = new List<LogLine>();
        }
    }

    public class VirtualLogViewer : Control
    {
        private List<LogLine> allLines = new List<LogLine>();
        private List<LogLine> displayLines = new List<LogLine>();
        private readonly object syncLock = new object();
        private Queue<LogLine> pendingQueue = new Queue<LogLine>();
        private Timer flushTimer;

        private VScrollBar vScroll;
        private HScrollBar hScroll;

        private FilterMode activeFilter = FilterMode.All;
        private string searchQuery = "";
        private bool isWordWrap = false;
        private bool isAutoScroll = true;
        private bool isAutoScrollPaused = false;

        private int selectionStart = -1;
        private int selectionEnd = -1;
        private bool isSelecting = false;
        private int hoveredIndex = -1;

        private int totalCount = 0;
        private int errorCount = 0;
        private int warningCount = 0;
        private int infoCount = 0;
        private int maxLineWidth = 0;

        public event EventHandler CountsChanged;
        public event EventHandler<bool> AutoScrollPausedChanged;
        public event EventHandler<string> StatusNotice;

        public bool AutoScrollEnabled
        {
            get { return isAutoScroll; }
            set
            {
                isAutoScroll = value;
                if (value)
                {
                    isAutoScrollPaused = false;
                    ScrollToBottom();
                }
                Invalidate();
            }
        }

        public bool WordWrapEnabled
        {
            get { return isWordWrap; }
            set
            {
                isWordWrap = value;
                UpdateScrollBars();
                Invalidate();
            }
        }

        public bool IsAutoScrollPaused
        {
            get { return isAutoScrollPaused; }
        }

        public int TotalCount { get { return totalCount; } }
        public int ErrorCount { get { return errorCount; } }
        public int WarningCount { get { return warningCount; } }
        public int InfoCount { get { return infoCount; } }

        public VirtualLogViewer()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                           ControlStyles.AllPaintingInWmPaint |
                           ControlStyles.UserPaint |
                           ControlStyles.ResizeRedraw |
                           ControlStyles.Selectable, true);

            this.BackColor = Color.FromArgb(13, 17, 23);
            this.ForeColor = Color.FromArgb(201, 209, 217);
            this.Font = new Font("Consolas", 9.75F, FontStyle.Regular);

            vScroll = new VScrollBar();
            vScroll.Dock = DockStyle.Right;
            vScroll.Width = 16;
            vScroll.Scroll += (s, e) => HandleVScroll();
            this.Controls.Add(vScroll);

            hScroll = new HScrollBar();
            hScroll.Dock = DockStyle.Bottom;
            hScroll.Height = 16;
            hScroll.Visible = false;
            hScroll.Scroll += (s, e) => Invalidate();
            this.Controls.Add(hScroll);

            flushTimer = new Timer();
            flushTimer.Interval = 35;
            flushTimer.Tick += (s, e) => FlushPendingQueue();
            flushTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && flushTimer != null)
            {
                flushTimer.Stop();
                flushTimer.Dispose();
                flushTimer = null;
            }
            base.Dispose(disposing);
        }

        public void EnqueueLine(string text, LogLevel level)
        {
            LogLine line = new LogLine();
            line.Text = text;
            line.Level = level;
            line.Timestamp = DateTime.Now.ToString("HH:mm:ss");

            Match m = Regex.Match(text, @"warning\s+([A-Za-z0-9_]+)\s*:", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                line.WarningCode = m.Groups[1].Value.ToUpperInvariant();
            }

            lock (syncLock)
            {
                pendingQueue.Enqueue(line);
            }
        }

        private void FlushPendingQueue()
        {
            List<LogLine> batch = null;
            lock (syncLock)
            {
                if (pendingQueue.Count == 0) return;
                batch = new List<LogLine>(pendingQueue.Count);
                while (pendingQueue.Count > 0)
                {
                    batch.Add(pendingQueue.Dequeue());
                }
            }

            if (batch == null || batch.Count == 0) return;

            bool addedAny = false;
            foreach (LogLine line in batch)
            {
                totalCount++;
                if (line.Level == LogLevel.Error) errorCount++;
                else if (line.Level == LogLevel.Warning) warningCount++;
                else if (line.Level == LogLevel.Info || line.Level == LogLevel.Step || line.Level == LogLevel.Success) infoCount++;

                line.LineNumber = totalCount;

                int measuredW = TextRenderer.MeasureText(line.Text, this.Font).Width;
                if (measuredW > maxLineWidth) maxLineWidth = measuredW;

                // Warning grouping & collapsing rule:
                // Only collapse when there are 3 OR MORE warnings in a row!
                bool isWarning = (line.Level == LogLevel.Warning) || (!string.IsNullOrEmpty(line.WarningCode));
                LogLine lastLine = allLines.Count > 0 ? allLines[allLines.Count - 1] : null;

                if (isWarning && string.IsNullOrEmpty(line.WarningCode) && lastLine != null)
                {
                    if (lastLine.Text == line.Text || (lastLine.IsWarningGroup && lastLine.GroupChildren.Count > 0 && lastLine.GroupChildren[0].Text == line.Text))
                    {
                        line.WarningCode = "repeated";
                    }
                }

                if (isWarning && !string.IsNullOrEmpty(line.WarningCode))
                {
                    if (lastLine != null && lastLine.IsWarningGroup && lastLine.WarningCode == line.WarningCode)
                    {
                        // Already in a group of 3+ warnings -> add to existing group
                        lastLine.GroupChildren.Add(line);
                        lastLine.WarningCount++;
                        if (line.WarningCode == "repeated")
                        {
                            lastLine.Text = string.Format("... {0} more identical warnings", lastLine.WarningCount);
                        }
                        else
                        {
                            lastLine.Text = string.Format("... {0} more {1} warnings", lastLine.WarningCount, line.WarningCode);
                        }

                        if (lastLine.IsExpanded)
                        {
                            RebuildDisplayLines();
                        }
                        addedAny = true;
                        continue;
                    }
                    else if (lastLine != null && !lastLine.IsWarningGroup && lastLine.WarningCode == line.WarningCode)
                    {
                        // Check if the one BEFORE last was ALSO the same warning code
                        LogLine beforeLast = allLines.Count > 1 ? allLines[allLines.Count - 2] : null;
                        if (beforeLast != null && beforeLast.WarningCode == line.WarningCode)
                        {
                            // Exactly 3 warnings in a row! Collapse 3rd warning onwards into a group!
                            LogLine group = new LogLine();
                            group.LineNumber = line.LineNumber;
                            group.Level = LogLevel.Warning;
                            group.WarningCode = line.WarningCode;
                            group.IsWarningGroup = true;
                            group.WarningCount = 1;
                            group.IsExpanded = false;
                            if (line.WarningCode == "repeated")
                            {
                                group.Text = "... 1 more identical warning";
                            }
                            else
                            {
                                group.Text = string.Format("... 1 more {0} warning", line.WarningCode);
                            }
                            group.GroupChildren.Add(line);

                            allLines.Add(group);
                            if (MatchesFilterAndSearch(group))
                            {
                                displayLines.Add(group);
                            }
                            addedAny = true;
                            continue;
                        }
                    }
                }

                // Normal line append
                allLines.Add(line);
                if (MatchesFilterAndSearch(line))
                {
                    displayLines.Add(line);
                }
                addedAny = true;
            }

            if (addedAny)
            {
                UpdateScrollBars();
                if (isAutoScroll && !isAutoScrollPaused)
                {
                    ScrollToBottom();
                }
                Invalidate();
            }

            if (CountsChanged != null)
            {
                CountsChanged(this, EventArgs.Empty);
            }
        }

        public void SetFilter(FilterMode filter)
        {
            activeFilter = filter;
            RebuildDisplayLines();
        }

        public void SetSearchQuery(string query)
        {
            searchQuery = (query ?? "").Trim();
            RebuildDisplayLines();
        }

        public void ClearLog()
        {
            lock (syncLock)
            {
                pendingQueue.Clear();
            }
            allLines.Clear();
            displayLines.Clear();
            totalCount = 0;
            errorCount = 0;
            warningCount = 0;
            infoCount = 0;
            maxLineWidth = 0;
            selectionStart = -1;
            selectionEnd = -1;
            vScroll.Value = 0;
            hScroll.Value = 0;
            UpdateScrollBars();
            Invalidate();

            if (CountsChanged != null)
            {
                CountsChanged(this, EventArgs.Empty);
            }
        }

        private void RebuildDisplayLines()
        {
            displayLines.Clear();
            foreach (LogLine line in allLines)
            {
                if (MatchesFilterAndSearch(line))
                {
                    displayLines.Add(line);
                    if (line.IsWarningGroup && line.IsExpanded)
                    {
                        foreach (LogLine child in line.GroupChildren)
                        {
                            if (MatchesFilterAndSearch(child))
                            {
                                displayLines.Add(child);
                            }
                        }
                    }
                }
            }
            UpdateScrollBars();
            if (isAutoScroll && !isAutoScrollPaused)
            {
                ScrollToBottom();
            }
            Invalidate();
        }

        private bool MatchesFilterAndSearch(LogLine line)
        {
            if (activeFilter == FilterMode.Errors)
            {
                if (line.Level != LogLevel.Error) return false;
            }
            else if (activeFilter == FilterMode.Warnings)
            {
                if (line.Level != LogLevel.Warning && !line.IsWarningGroup && string.IsNullOrEmpty(line.WarningCode)) return false;
            }
            else if (activeFilter == FilterMode.Info)
            {
                if (line.Level != LogLevel.Info && line.Level != LogLevel.Step && line.Level != LogLevel.Success) return false;
            }

            if (!string.IsNullOrEmpty(searchQuery))
            {
                if (line.IsWarningGroup)
                {
                    bool match = line.Text != null && line.Text.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!match)
                    {
                        foreach (LogLine child in line.GroupChildren)
                        {
                            if (child.Text != null && child.Text.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                match = true;
                                break;
                            }
                        }
                    }
                    if (!match) return false;
                }
                else
                {
                    if (line.Text == null || line.Text.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void ToggleWarningGroup(LogLine group)
        {
            group.IsExpanded = !group.IsExpanded;
            RebuildDisplayLines();
        }

        private void UpdateScrollBars()
        {
            int rowHeight = Font.Height + 4;
            int viewHeight = this.ClientSize.Height - (hScroll.Visible ? hScroll.Height : 0);
            int visibleLines = Math.Max(1, viewHeight / rowHeight);

            if (displayLines.Count > visibleLines)
            {
                vScroll.Visible = true;
                vScroll.LargeChange = visibleLines;
                vScroll.SmallChange = 1;
                vScroll.Maximum = displayLines.Count - 1;
            }
            else
            {
                vScroll.Visible = false;
                vScroll.Value = 0;
            }

            int gutterWidth = GetGutterWidth();
            int clientW = this.ClientSize.Width - (vScroll.Visible ? vScroll.Width : 0);
            int totalW = gutterWidth + 12 + maxLineWidth + 30;

            if (!isWordWrap && totalW > clientW)
            {
                hScroll.Visible = true;
                hScroll.Maximum = totalW;
                hScroll.LargeChange = Math.Max(1, clientW);
                hScroll.SmallChange = 25;
            }
            else
            {
                hScroll.Visible = false;
                hScroll.Value = 0;
            }
        }

        public void ScrollToBottom()
        {
            int rowHeight = Font.Height + 4;
            int viewHeight = this.ClientSize.Height - (hScroll.Visible ? hScroll.Height : 0);
            int visibleLines = Math.Max(1, viewHeight / rowHeight);

            if (displayLines.Count > visibleLines)
            {
                vScroll.Value = Math.Max(0, displayLines.Count - visibleLines);
            }
            else
            {
                vScroll.Value = 0;
            }
        }

        private void HandleVScroll()
        {
            int rowHeight = Font.Height + 4;
            int viewHeight = this.ClientSize.Height - (hScroll.Visible ? hScroll.Height : 0);
            int visibleLines = Math.Max(1, viewHeight / rowHeight);
            int bottomVal = Math.Max(0, displayLines.Count - visibleLines);

            if (vScroll.Value < bottomVal - 1)
            {
                if (!isAutoScrollPaused)
                {
                    isAutoScrollPaused = true;
                    if (AutoScrollPausedChanged != null) AutoScrollPausedChanged(this, true);
                }
            }
            else
            {
                if (isAutoScrollPaused)
                {
                    isAutoScrollPaused = false;
                    if (AutoScrollPausedChanged != null) AutoScrollPausedChanged(this, false);
                }
            }
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!vScroll.Visible) return;

            int lines = (e.Delta / 120) * 3;
            int newVal = vScroll.Value - lines;
            newVal = Math.Max(0, Math.Min(vScroll.Maximum - vScroll.LargeChange + 1, newVal));
            vScroll.Value = newVal;
            HandleVScroll();
            Invalidate();
        }

        private int GetGutterWidth()
        {
            int digits = Math.Max(4, totalCount.ToString().Length);
            int charWidth = (int)Math.Ceiling(this.Font.SizeInPoints * 0.75f * 1.4f);
            return (digits * charWidth) + 20;
        }

        private Color GetLineColor(LogLine item)
        {
            string text = item.Text ?? "";
            if (text.Contains("[STEP]") || item.Level == LogLevel.Step)
            {
                return Color.FromArgb(96, 165, 250); // blue
            }
            if (text.Contains("[SUCCESS]") || item.Level == LogLevel.Success)
            {
                return Color.FromArgb(34, 197, 94); // bright green
            }
            if (text.Contains("[WARNING]") || item.Level == LogLevel.Warning || item.IsWarningGroup)
            {
                return Color.FromArgb(245, 158, 11); // amber
            }
            if (text.Contains("[ERROR]") || item.Level == LogLevel.Error)
            {
                return Color.FromArgb(239, 68, 68); // red
            }
            if (text.Contains("[OK]"))
            {
                return Color.FromArgb(16, 185, 129); // green
            }
            return Color.FromArgb(156, 163, 175); // dim gray (raw MSBuild output)
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;

            int gutterWidth = GetGutterWidth();
            int rowHeight = Font.Height + 4;
            int clientW = this.ClientSize.Width - (vScroll.Visible ? vScroll.Width : 0);
            int clientH = this.ClientSize.Height - (hScroll.Visible ? hScroll.Height : 0);

            // Gutter background
            using (SolidBrush bGutter = new SolidBrush(Color.FromArgb(20, 24, 34)))
            {
                g.FillRectangle(bGutter, 0, 0, gutterWidth, clientH);
            }

            // Gutter right border
            using (Pen pBorder = new Pen(Color.FromArgb(45, 55, 72), 1))
            {
                g.DrawLine(pBorder, gutterWidth - 1, 0, gutterWidth - 1, clientH);
            }

            int startIndex = vScroll.Value;
            int visibleCount = (clientH / rowHeight) + 2;
            int endIndex = Math.Min(displayLines.Count, startIndex + visibleCount);

            int selMin = Math.Min(selectionStart, selectionEnd);
            int selMax = Math.Max(selectionStart, selectionEnd);
            int scrollX = (hScroll.Visible && !isWordWrap) ? hScroll.Value : 0;

            Rectangle textClip = new Rectangle(gutterWidth, 0, clientW - gutterWidth, clientH);
            g.SetClip(textClip);

            for (int i = startIndex; i < endIndex; i++)
            {
                int y = (i - startIndex) * rowHeight;
                LogLine item = displayLines[i];

                // Selection highlight
                if (selMin >= 0 && i >= selMin && i <= selMax)
                {
                    using (SolidBrush bSel = new SolidBrush(Color.FromArgb(30, 58, 100)))
                    {
                        g.FillRectangle(bSel, gutterWidth, y, clientW - gutterWidth, rowHeight);
                    }
                }
                else if (i == hoveredIndex && item.IsWarningGroup)
                {
                    using (SolidBrush bHov = new SolidBrush(Color.FromArgb(32, 28, 20)))
                    {
                        g.FillRectangle(bHov, gutterWidth, y, clientW - gutterWidth, rowHeight);
                    }
                }

                int textX = gutterWidth + 10 - scrollX;
                int textW = isWordWrap ? (clientW - (gutterWidth + 10)) : Math.Max(clientW - textX, maxLineWidth + 50);

                if (item.IsWarningGroup)
                {
                    // Render collapsed row as a normal log line (not an overlapping box!)
                    string arrow = item.IsExpanded ? "▼" : "▶";
                    string actionText = item.IsExpanded ? "(click to collapse)" : "(click to expand)";
                    string pillText = string.Format("{0}  {1}  {2}", arrow, item.Text, actionText);

                    Rectangle textRect = new Rectangle(textX, y, textW, rowHeight);
                    TextRenderer.DrawText(g, pillText, Font, textRect, Color.FromArgb(245, 158, 11), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                else
                {
                    Color textColor = GetLineColor(item);
                    Rectangle textRect = new Rectangle(textX, y, textW, rowHeight);
                    TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
                    if (isWordWrap) flags |= TextFormatFlags.WordBreak;

                    TextRenderer.DrawText(g, item.Text, Font, textRect, textColor, flags);

                    // Search match highlight
                    if (!string.IsNullOrEmpty(searchQuery))
                    {
                        int matchIdx = item.Text.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase);
                        if (matchIdx >= 0)
                        {
                            string prefix = item.Text.Substring(0, matchIdx);
                            string matched = item.Text.Substring(matchIdx, Math.Min(searchQuery.Length, item.Text.Length - matchIdx));
                            int preW = TextRenderer.MeasureText(g, prefix, Font).Width;
                            int matchW = TextRenderer.MeasureText(g, matched, Font).Width;

                            Rectangle hlRect = new Rectangle(textX + preW - 2, y + 2, matchW + 4, rowHeight - 4);
                            using (SolidBrush bHl = new SolidBrush(Color.FromArgb(120, 202, 138, 4)))
                            {
                                g.FillRectangle(bHl, hlRect);
                            }
                            TextRenderer.DrawText(g, matched, Font, new Point(textX + preW, y), Color.FromArgb(254, 240, 138), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                        }
                    }
                }
            }

            g.ResetClip();

            // Line numbers in gutter (drawn over text if horizontally scrolled)
            for (int i = startIndex; i < endIndex; i++)
            {
                int y = (i - startIndex) * rowHeight;
                LogLine item = displayLines[i];
                string numStr = (!item.IsWarningGroup && item.LineNumber > 0) ? item.LineNumber.ToString() : (item.IsWarningGroup ? "…" : "");
                Rectangle gutterRect = new Rectangle(0, y, gutterWidth - 8, rowHeight);
                TextRenderer.DrawText(g, numStr, Font, gutterRect, Color.FromArgb(100, 116, 139), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            this.Focus();

            if (e.Button == MouseButtons.Left)
            {
                int rowHeight = Font.Height + 4;
                int clickedIdx = vScroll.Value + (e.Y / rowHeight);

                if (clickedIdx >= 0 && clickedIdx < displayLines.Count)
                {
                    LogLine item = displayLines[clickedIdx];
                    if (item.IsWarningGroup)
                    {
                        ToggleWarningGroup(item);
                        return;
                    }

                    selectionStart = clickedIdx;
                    selectionEnd = clickedIdx;
                    isSelecting = true;
                    Invalidate();
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int rowHeight = Font.Height + 4;
            int idx = vScroll.Value + (e.Y / rowHeight);

            if (isSelecting && e.Button == MouseButtons.Left)
            {
                if (idx >= 0 && idx < displayLines.Count)
                {
                    selectionEnd = idx;
                    Invalidate();
                }
            }
            else
            {
                if (idx >= 0 && idx < displayLines.Count && displayLines[idx].IsWarningGroup)
                {
                    if (hoveredIndex != idx)
                    {
                        hoveredIndex = idx;
                        this.Cursor = Cursors.Hand;
                        Invalidate();
                    }
                }
                else
                {
                    if (hoveredIndex != -1)
                    {
                        hoveredIndex = -1;
                        this.Cursor = Cursors.Default;
                        Invalidate();
                    }
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            isSelecting = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelectionToClipboard();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                selectionStart = 0;
                selectionEnd = displayLines.Count - 1;
                Invalidate();
                e.Handled = true;
            }
        }

        public void CopySelectionToClipboard()
        {
            StringBuilder sb = new StringBuilder();
            if (selectionStart >= 0 && selectionEnd >= 0 && displayLines.Count > 0)
            {
                int min = Math.Min(selectionStart, selectionEnd);
                int max = Math.Max(selectionStart, selectionEnd);
                min = Math.Max(0, min);
                max = Math.Min(displayLines.Count - 1, max);

                for (int i = min; i <= max; i++)
                {
                    sb.AppendLine(displayLines[i].Text);
                }
            }
            else
            {
                foreach (LogLine line in displayLines)
                {
                    sb.AppendLine(line.Text);
                }
            }

            if (sb.Length > 0)
            {
                Clipboard.SetText(sb.ToString());
                if (StatusNotice != null) StatusNotice(this, "Copied log to clipboard.");
            }
        }

        public void CopyAllToClipboard()
        {
            StringBuilder sb = new StringBuilder();
            foreach (LogLine line in displayLines)
            {
                sb.AppendLine(line.Text);
            }
            if (sb.Length > 0)
            {
                Clipboard.SetText(sb.ToString());
                if (StatusNotice != null) StatusNotice(this, "All displayed log lines copied to clipboard.");
            }
        }

        public void SaveLogToFile()
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Save Activity Log";
                sfd.Filter = "Log Files (*.log)|*.log|Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                sfd.FileName = string.Format("GrandPOS_Deploy_{0:yyyyMMdd_HHmmss}.log", DateTime.Now);

                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (LogLine line in displayLines)
                        {
                            sb.AppendLine(line.Text);
                        }
                        File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                        if (StatusNotice != null) StatusNotice(this, "Log saved to: " + Path.GetFileName(sfd.FileName));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Failed to save log: " + ex.Message, "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }

    #endregion

    #region Output.log Viewer

    public enum OutputLogLevel
    {
        Info,
        Warn,
        Err,
        Dbg
    }

    public enum OutputRowKind
    {
        Entry,
        StackLine,
        SessionBanner,
        DeployMarker
    }

    public class OutputLogEntry
    {
        public int LineNumber { get; set; }
        public DateTime? Timestamp { get; set; }
        public string TimestampText { get; set; }
        public OutputLogLevel Level { get; set; }
        public string Source { get; set; }
        public string Thread { get; set; }
        public string Message { get; set; }
        public string RawLine { get; set; }
        public List<string> StackLines { get; set; }
        public bool IsStackExpanded { get; set; }
        public bool IsBookmarked { get; set; }
        public int SessionIndex { get; set; }
        public bool IsSessionStart { get; set; }
        public string SessionBannerText { get; set; }

        public OutputLogEntry()
        {
            StackLines = new List<string>();
            Source = "";
            Thread = "-";
            Message = "";
            TimestampText = "";
        }
    }

    public class OutputSessionInfo
    {
        public int Index { get; set; }
        public string Label { get; set; }
        public DateTime? StartTime { get; set; }
    }

    public class OutputDisplayRow
    {
        public OutputRowKind Kind { get; set; }
        public OutputLogEntry Entry { get; set; }
        public int StackLineIndex { get; set; }
        public string BannerText { get; set; }
        public DateTime? TimelineTime { get; set; }
        public DeployMarker DeployInfo { get; set; }
    }

    public static class OutputLogParser
    {
        // Matches: 2026-Nov-10 15:09:49 CR.Pricing.DataObjectManager [INFO] [optional-thread] Message
        public static readonly Regex LineHeaderRegex = new Regex(
            @"^\s*\[?(?<ts>\d{4}-(?:[A-Za-z]{3}|\d{2})-\d{2}[T\s]\d{2}:\d{2}:\d{2}(?:[.,]\d{1,7})?)\]?\s+(?<source>[^\s\[]+)\s+\[(?<level>[^\]]*)\](?:\s+\[(?<thread>(?:T|TID|Thread)?[:#\s-]?\d+)\])?\s*(?<msg>.*)$",
            RegexOptions.Compiled);

        // Matches session separator banner in Output.log
        public static readonly Regex BeginBannerRegex = new Regex(
            @"^\s*=+\s*LOG\s+BEGIN\s*=+\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches startup version / optional PID line right after LOG BEGIN
        public static readonly Regex StartupInfoRegex = new Regex(
            @"(?:PosTerminal|GrandPOS)\s+version\s+(?<ver>v?\d+(?:\.\d+)+)(?:.*?\bPID[:=\s#]*(?<pid>\d+))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly string[] TimestampFormats = new string[]
        {
            "yyyy-MMM-dd HH:mm:ss",
            "yyyy-MMM-dd HH:mm:ss.fff",
            "yyyy-MMM-dd HH:mm:ss,fff",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss,fff",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff"
        };

        public static bool TryParseTimestamp(string tsText, out DateTime parsed)
        {
            if (DateTime.TryParseExact(tsText, TimestampFormats, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out parsed))
            {
                return true;
            }
            return DateTime.TryParse(tsText, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out parsed);
        }

        public static OutputLogLevel ParseLevel(string rawLevel)
        {
            string lvl = (rawLevel ?? "").Trim().ToUpperInvariant();
            if (lvl == "ERR" || lvl == "ERROR" || lvl == "FATAL" || lvl == "CRIT" || lvl == "CRITICAL")
            {
                return OutputLogLevel.Err;
            }
            if (lvl == "WARN" || lvl == "WARNING")
            {
                return OutputLogLevel.Warn;
            }
            if (lvl == "DBG" || lvl == "DEBUG" || lvl == "TRACE" || lvl == "VERB" || lvl == "VERBOSE")
            {
                return OutputLogLevel.Dbg;
            }
            return OutputLogLevel.Info;
        }

        public static string FormatLevelBadge(OutputLogLevel level)
        {
            switch (level)
            {
                case OutputLogLevel.Err: return "ERR";
                case OutputLogLevel.Warn: return "WARN";
                case OutputLogLevel.Dbg: return "DBG";
                default: return "INFO";
            }
        }
    }

    public class TabHeaderButton : Button
    {
        public bool IsActive { get; set; }
        public int BadgeCount { get; set; }

        public TabHeaderButton()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.Cursor = Cursors.Hand;
            this.DoubleBuffered = true;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.Height = 34;
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = this.ClientRectangle;
            Color bg = IsActive ? Color.FromArgb(13, 17, 23) : Color.FromArgb(19, 24, 34);
            Color fg = IsActive ? Color.White : Color.FromArgb(148, 163, 184);

            using (SolidBrush bBg = new SolidBrush(bg))
            {
                g.FillRectangle(bBg, r);
            }

            if (IsActive)
            {
                using (SolidBrush bTop = new SolidBrush(Color.FromArgb(57, 197, 232)))
                {
                    g.FillRectangle(bTop, 0, 0, r.Width, 2);
                }
            }
            else
            {
                using (Pen pBot = new Pen(Color.FromArgb(45, 55, 72), 1))
                {
                    g.DrawLine(pBot, 0, r.Height - 1, r.Width, r.Height - 1);
                }
            }

            using (Pen pRight = new Pen(Color.FromArgb(33, 38, 45), 1))
            {
                g.DrawLine(pRight, r.Width - 1, 4, r.Width - 1, r.Height - 4);
            }

            using (Font drawFont = new Font(this.Font.FontFamily, this.Font.Size, IsActive ? FontStyle.Bold : FontStyle.Regular))
            {
                if (BadgeCount > 0)
                {
                    string badgeText = string.Format("{0} new", BadgeCount);
                    Size textSize = TextRenderer.MeasureText(g, this.Text, drawFont);
                    Size badgeSize = TextRenderer.MeasureText(g, badgeText, new Font("Segoe UI", 7.25F, FontStyle.Bold));
                    int badgeW = badgeSize.Width + 10;
                    int badgeH = 16;
                    int totalW = textSize.Width + badgeW + 4;
                    int startX = Math.Max(8, (r.Width - totalW) / 2);

                    Rectangle textRect = new Rectangle(startX, 0, textSize.Width, r.Height);
                    TextRenderer.DrawText(g, this.Text, drawFont, textRect, fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                    Rectangle badgeRect = new Rectangle(startX + textSize.Width + 2, (r.Height - badgeH) / 2, badgeW, badgeH);
                    using (GraphicsPath bp = GetRoundedRect(badgeRect, 8))
                    {
                        using (SolidBrush bBadge = new SolidBrush(Color.FromArgb(220, 38, 38)))
                        {
                            g.FillPath(bBadge, bp);
                        }
                    }
                    using (Font bf = new Font("Segoe UI", 7.25F, FontStyle.Bold))
                    {
                        TextRenderer.DrawText(g, badgeText, bf, badgeRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                }
                else
                {
                    TextRenderer.DrawText(g, this.Text, drawFont, r, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }
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
    }

    public class OutputLogMinimap : Control
    {
        private List<OutputDisplayRow> rows = new List<OutputDisplayRow>();
        private int firstVisible = 0;
        private int visibleCount = 1;
        private bool isDragging = false;

        public event EventHandler<double> ScrollRatioRequested;

        public OutputLogMinimap()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                           ControlStyles.AllPaintingInWmPaint |
                           ControlStyles.UserPaint |
                           ControlStyles.ResizeRedraw, true);
            this.Width = 16;
            this.BackColor = Color.FromArgb(15, 20, 27);
            this.Cursor = Cursors.Hand;
        }

        public void UpdateState(List<OutputDisplayRow> displayRows, int firstVis, int visCount)
        {
            rows = displayRows ?? new List<OutputDisplayRow>();
            firstVisible = Math.Max(0, firstVis);
            visibleCount = Math.Max(1, visCount);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            using (Pen borderPen = new Pen(Color.FromArgb(33, 38, 45), 1))
            {
                g.DrawLine(borderPen, 0, 0, 0, h);
            }

            int total = rows.Count;
            if (total == 0) return;

            for (int i = 0; i < total; i++)
            {
                OutputDisplayRow row = rows[i];
                Color? tickColor = null;
                if (row.Kind == OutputRowKind.DeployMarker)
                {
                    tickColor = Color.FromArgb(16, 185, 129);
                }
                else if (row.Kind == OutputRowKind.Entry && row.Entry != null)
                {
                    if (row.Entry.Level == OutputLogLevel.Err) tickColor = Color.FromArgb(239, 68, 68);
                    else if (row.Entry.Level == OutputLogLevel.Warn) tickColor = Color.FromArgb(245, 158, 11);
                    else if (row.Entry.IsBookmarked) tickColor = Color.FromArgb(56, 189, 248);
                }

                if (tickColor.HasValue)
                {
                    int y = (int)(((double)i / total) * h);
                    int th = Math.Max(2, (int)Math.Ceiling((double)h / total));
                    if (th > 4) th = 4;
                    using (SolidBrush b = new SolidBrush(tickColor.Value))
                    {
                        g.FillRectangle(b, 2, Math.Min(h - th, y), w - 3, th);
                    }
                }
            }

            int vpY = (int)(((double)firstVisible / total) * h);
            int vpH = Math.Max(12, (int)(((double)visibleCount / total) * h));
            if (vpY + vpH > h) vpY = Math.Max(0, h - vpH);

            using (SolidBrush vpBrush = new SolidBrush(Color.FromArgb(55, 59, 130, 246)))
            {
                g.FillRectangle(vpBrush, 1, vpY, w - 2, vpH);
            }
            using (Pen vpPen = new Pen(Color.FromArgb(130, 96, 165, 250), 1))
            {
                g.DrawRectangle(vpPen, 1, vpY, w - 3, vpH - 1);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                EmitRatio(e.Y);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (isDragging && e.Button == MouseButtons.Left)
            {
                EmitRatio(e.Y);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            isDragging = false;
        }

        private void EmitRatio(int y)
        {
            double ratio = Math.Max(0.0, Math.Min(1.0, (double)y / Math.Max(1, this.ClientSize.Height)));
            if (ScrollRatioRequested != null)
            {
                ScrollRatioRequested(this, ratio);
            }
        }
    }

    public class OutputLogViewer : Control
    {
        private List<OutputDisplayRow> displayRows = new List<OutputDisplayRow>();
        private VScrollBar vScroll;
        private HScrollBar hScroll;

        private bool showLineNumbers = true;
        private bool showTimestamps = true;
        private bool isWordWrap = false;
        private bool isFollowing = true;
        private bool isPausedByScroll = false;

        private string searchQuery = "";
        private bool matchCase = false;
        private bool useRegex = false;
        private Regex compiledSearchRegex = null;

        private int selectedRowIndex = -1;
        private int hoveredRowIndex = -1;
        private int maxLineWidth = 800;
        private int maxOriginalLineNumber = 1;

        public event EventHandler<OutputLogEntry> SelectedEntryChanged;
        public event EventHandler<bool> FollowStateChanged;
        public event EventHandler StackToggled;
        public event EventHandler BookmarkToggleRequested;
        public event EventHandler ViewportChanged;

        public bool ShowLineNumbers
        {
            get { return showLineNumbers; }
            set { showLineNumbers = value; UpdateScrollBars(); Invalidate(); }
        }

        public bool ShowTimestamps
        {
            get { return showTimestamps; }
            set { showTimestamps = value; UpdateScrollBars(); Invalidate(); }
        }

        public bool WordWrapEnabled
        {
            get { return isWordWrap; }
            set { isWordWrap = value; UpdateScrollBars(); Invalidate(); }
        }

        public bool IsFollowing
        {
            get { return isFollowing && !isPausedByScroll; }
        }

        public int FirstVisibleIndex { get { return vScroll.Value; } }

        public int VisibleRowCount
        {
            get
            {
                int rowHeight = Font.Height + 6;
                int viewHeight = Math.Max(1, this.ClientSize.Height - (hScroll.Visible ? hScroll.Height : 0));
                return Math.Max(1, viewHeight / rowHeight);
            }
        }

        public OutputLogEntry SelectedEntry
        {
            get
            {
                if (selectedRowIndex >= 0 && selectedRowIndex < displayRows.Count)
                {
                    return displayRows[selectedRowIndex].Entry;
                }
                return null;
            }
        }

        public OutputLogViewer()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                           ControlStyles.AllPaintingInWmPaint |
                           ControlStyles.UserPaint |
                           ControlStyles.ResizeRedraw |
                           ControlStyles.Selectable, true);

            this.BackColor = Color.FromArgb(13, 17, 23);
            this.ForeColor = Color.FromArgb(201, 209, 217);
            this.Font = new Font("Consolas", 9.5F, FontStyle.Regular);

            vScroll = new VScrollBar();
            vScroll.Dock = DockStyle.Right;
            vScroll.Width = 16;
            vScroll.Scroll += (s, e) => HandleVScroll();
            this.Controls.Add(vScroll);

            hScroll = new HScrollBar();
            hScroll.Dock = DockStyle.Bottom;
            hScroll.Height = 16;
            hScroll.Visible = false;
            hScroll.Scroll += (s, e) => Invalidate();
            this.Controls.Add(hScroll);
        }

        public void SetFontSize(float pt)
        {
            float clamped = Math.Max(7.5F, Math.Min(16.0F, pt));
            this.Font = new Font("Consolas", clamped, FontStyle.Regular);
            UpdateScrollBars();
            Invalidate();
            NotifyViewportChanged();
        }

        public void SetSearchOptions(string query, bool isMatchCase, bool isRegex, Regex regexObj)
        {
            searchQuery = query ?? "";
            matchCase = isMatchCase;
            useRegex = isRegex;
            compiledSearchRegex = regexObj;
            Invalidate();
        }

        public void SetDisplayRows(List<OutputDisplayRow> rows, int maxLineNum, bool scrollToBottomIfFollowing)
        {
            OutputLogEntry prevSelected = SelectedEntry;
            displayRows = rows ?? new List<OutputDisplayRow>();
            maxOriginalLineNumber = Math.Max(1, maxLineNum);

            int estMaxW = 600;
            int step = Math.Max(1, displayRows.Count / 400);
            for (int i = 0; i < displayRows.Count; i += step)
            {
                OutputDisplayRow r = displayRows[i];
                int len = 0;
                if (r.Kind == OutputRowKind.Entry && r.Entry != null)
                {
                    len = (r.Entry.Source ?? "").Length + (r.Entry.Message ?? "").Length + 24;
                }
                else if (r.Kind == OutputRowKind.StackLine && r.Entry != null && r.StackLineIndex < r.Entry.StackLines.Count)
                {
                    len = r.Entry.StackLines[r.StackLineIndex].Length + 12;
                }
                else if (!string.IsNullOrEmpty(r.BannerText))
                {
                    len = r.BannerText.Length;
                }
                int w = len * (int)Math.Ceiling(this.Font.SizeInPoints * 0.8f);
                if (w > estMaxW) estMaxW = w;
            }
            maxLineWidth = estMaxW;

            if (prevSelected != null)
            {
                selectedRowIndex = -1;
                for (int i = 0; i < displayRows.Count; i++)
                {
                    if (displayRows[i].Kind == OutputRowKind.Entry && displayRows[i].Entry == prevSelected)
                    {
                        selectedRowIndex = i;
                        break;
                    }
                }
            }
            else
            {
                selectedRowIndex = -1;
            }

            UpdateScrollBars();
            if (scrollToBottomIfFollowing && isFollowing && !isPausedByScroll)
            {
                ScrollToBottom();
            }
            else
            {
                ClampScroll();
            }
            Invalidate();
            NotifyViewportChanged();
        }

        public void SetFollowing(bool follow)
        {
            isFollowing = follow;
            isPausedByScroll = !follow;
            if (follow)
            {
                ScrollToBottom();
            }
            Invalidate();
            NotifyViewportChanged();
            if (FollowStateChanged != null) FollowStateChanged(this, IsFollowing);
        }

        public void ScrollToBottom()
        {
            int vis = VisibleRowCount;
            if (displayRows.Count > vis)
            {
                vScroll.Value = Math.Max(0, displayRows.Count - vis);
            }
            else
            {
                vScroll.Value = 0;
            }
            NotifyViewportChanged();
        }

        public void ScrollToRow(int rowIndex, bool selectRow)
        {
            if (rowIndex < 0 || rowIndex >= displayRows.Count) return;
            if (selectRow)
            {
                selectedRowIndex = rowIndex;
                if (SelectedEntryChanged != null) SelectedEntryChanged(this, displayRows[rowIndex].Entry);
            }

            int vis = VisibleRowCount;
            int target = Math.Max(0, rowIndex - (vis / 3));
            int maxVal = Math.Max(0, displayRows.Count - vis);
            target = Math.Min(maxVal, target);
            vScroll.Value = target;

            if (target < maxVal - 1 && isFollowing)
            {
                isPausedByScroll = true;
                if (FollowStateChanged != null) FollowStateChanged(this, false);
            }

            Invalidate();
            NotifyViewportChanged();
        }

        public void ScrollToRatio(double ratio)
        {
            if (displayRows.Count == 0) return;
            int vis = VisibleRowCount;
            int maxVal = Math.Max(0, displayRows.Count - vis);
            int target = (int)Math.Round(ratio * maxVal);
            target = Math.Max(0, Math.Min(maxVal, target));
            vScroll.Value = target;

            if (target < maxVal - 1)
            {
                if (!isPausedByScroll)
                {
                    isPausedByScroll = true;
                    if (FollowStateChanged != null) FollowStateChanged(this, false);
                }
            }
            else if (isFollowing && isPausedByScroll)
            {
                isPausedByScroll = false;
                if (FollowStateChanged != null) FollowStateChanged(this, true);
            }

            Invalidate();
            NotifyViewportChanged();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollBars();
            ClampScroll();
            NotifyViewportChanged();
        }

        private void ClampScroll()
        {
            int vis = VisibleRowCount;
            int maxVal = Math.Max(0, displayRows.Count - vis);
            if (vScroll.Value > maxVal) vScroll.Value = maxVal;
        }

        private void UpdateScrollBars()
        {
            int rowHeight = Font.Height + 6;
            int viewHeight = Math.Max(1, this.ClientSize.Height - (hScroll.Visible ? hScroll.Height : 0));
            int visibleLines = Math.Max(1, viewHeight / rowHeight);

            if (displayRows.Count > visibleLines)
            {
                vScroll.Visible = true;
                vScroll.LargeChange = visibleLines;
                vScroll.SmallChange = 1;
                vScroll.Maximum = displayRows.Count - 1;
            }
            else
            {
                vScroll.Visible = false;
                vScroll.Value = 0;
            }

            int prefixW = GetPrefixWidth();
            int clientW = this.ClientSize.Width - (vScroll.Visible ? vScroll.Width : 0);
            int totalW = prefixW + maxLineWidth + 60;

            if (!isWordWrap && totalW > clientW)
            {
                hScroll.Visible = true;
                hScroll.Maximum = totalW;
                hScroll.LargeChange = Math.Max(1, clientW);
                hScroll.SmallChange = 28;
            }
            else
            {
                hScroll.Visible = false;
                hScroll.Value = 0;
            }
        }

        private void HandleVScroll()
        {
            int vis = VisibleRowCount;
            int bottomVal = Math.Max(0, displayRows.Count - vis);

            if (vScroll.Value < bottomVal - 1)
            {
                if (!isPausedByScroll)
                {
                    isPausedByScroll = true;
                    if (FollowStateChanged != null) FollowStateChanged(this, false);
                }
            }
            else
            {
                if (isFollowing && isPausedByScroll)
                {
                    isPausedByScroll = false;
                    if (FollowStateChanged != null) FollowStateChanged(this, true);
                }
            }
            Invalidate();
            NotifyViewportChanged();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!vScroll.Visible) return;

            int lines = (e.Delta / 120) * 3;
            int newVal = vScroll.Value - lines;
            int maxVal = Math.Max(0, displayRows.Count - VisibleRowCount);
            newVal = Math.Max(0, Math.Min(maxVal, newVal));
            vScroll.Value = newVal;
            HandleVScroll();
        }

        private void NotifyViewportChanged()
        {
            if (ViewportChanged != null) ViewportChanged(this, EventArgs.Empty);
        }

        private int GetGutterWidth()
        {
            if (!showLineNumbers) return 14;
            int digits = Math.Max(4, maxOriginalLineNumber.ToString().Length);
            int charWidth = (int)Math.Ceiling(this.Font.SizeInPoints * 0.78f * 1.25f);
            return (digits * charWidth) + 26;
        }

        private int GetTimeWidth()
        {
            if (!showTimestamps) return 0;
            return (int)Math.Ceiling(this.Font.SizeInPoints * 10.5f) + 12;
        }

        private int GetPrefixWidth()
        {
            return GetGutterWidth() + GetTimeWidth() + 58;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int gutterW = GetGutterWidth();
            int timeW = GetTimeWidth();
            int rowHeight = Font.Height + 6;
            int clientW = this.ClientSize.Width - (vScroll.Visible ? vScroll.Width : 0);
            int clientH = this.ClientSize.Height - (hScroll.Visible ? hScroll.Height : 0);
            if (clientW <= 0 || clientH <= 0) return;

            using (SolidBrush bGutter = new SolidBrush(Color.FromArgb(20, 24, 34)))
            {
                g.FillRectangle(bGutter, 0, 0, gutterW, clientH);
            }
            using (Pen pBorder = new Pen(Color.FromArgb(45, 55, 72), 1))
            {
                g.DrawLine(pBorder, gutterW - 1, 0, gutterW - 1, clientH);
            }

            if (displayRows.Count == 0)
            {
                Rectangle emptyRect = new Rectangle(gutterW + 16, 20, Math.Max(100, clientW - gutterW - 32), 40);
                TextRenderer.DrawText(g, "No log entries match the current filters.", this.Font, emptyRect, Color.FromArgb(148, 163, 184), TextFormatFlags.Left | TextFormatFlags.Top);
                return;
            }

            int startIndex = vScroll.Value;
            int visibleCount = (clientH / rowHeight) + 2;
            int endIndex = Math.Min(displayRows.Count, startIndex + visibleCount);
            int scrollX = (hScroll.Visible && !isWordWrap) ? hScroll.Value : 0;

            Rectangle contentClip = new Rectangle(gutterW, 0, Math.Max(1, clientW - gutterW), clientH);
            g.SetClip(contentClip);

            for (int i = startIndex; i < endIndex; i++)
            {
                int y = (i - startIndex) * rowHeight;
                OutputDisplayRow row = displayRows[i];
                bool isSelected = (i == selectedRowIndex);

                if (row.Kind == OutputRowKind.SessionBanner)
                {
                    using (SolidBrush bSess = new SolidBrush(Color.FromArgb(18, 30, 44)))
                    {
                        g.FillRectangle(bSess, gutterW, y + 1, clientW - gutterW, rowHeight - 2);
                    }
                    Rectangle bRect = new Rectangle(gutterW + 10 - scrollX, y, Math.Max(clientW, 600), rowHeight);
                    TextRenderer.DrawText(g, row.BannerText ?? "── Session ──", Font, bRect, Color.FromArgb(56, 189, 248), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    continue;
                }

                if (row.Kind == OutputRowKind.DeployMarker)
                {
                    using (SolidBrush bDep = new SolidBrush(Color.FromArgb(14, 48, 34)))
                    {
                        g.FillRectangle(bDep, gutterW, y + 1, clientW - gutterW, rowHeight - 2);
                    }
                    using (Pen pDep = new Pen(Color.FromArgb(16, 185, 129), 1))
                    {
                        g.DrawLine(pDep, gutterW, y + rowHeight - 1, clientW, y + rowHeight - 1);
                    }
                    Rectangle dRect = new Rectangle(gutterW + 10 - scrollX, y, Math.Max(clientW, 600), rowHeight);
                    TextRenderer.DrawText(g, row.BannerText ?? "▲ Deployed", Font, dRect, Color.FromArgb(52, 211, 153), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    continue;
                }

                if (isSelected)
                {
                    using (SolidBrush bSel = new SolidBrush(Color.FromArgb(30, 58, 100)))
                    {
                        g.FillRectangle(bSel, gutterW, y, clientW - gutterW, rowHeight);
                    }
                }
                else if (i == hoveredRowIndex)
                {
                    using (SolidBrush bHov = new SolidBrush(Color.FromArgb(22, 28, 39)))
                    {
                        g.FillRectangle(bHov, gutterW, y, clientW - gutterW, rowHeight);
                    }
                }

                if (row.Kind == OutputRowKind.StackLine && row.Entry != null)
                {
                    string sText = row.StackLineIndex < row.Entry.StackLines.Count ? row.Entry.StackLines[row.StackLineIndex] : "";
                    int sx = gutterW + timeW + 64 - scrollX;
                    Rectangle sRect = new Rectangle(sx, y, Math.Max(clientW - sx, maxLineWidth), rowHeight);
                    TextRenderer.DrawText(g, "│ " + sText, Font, sRect, Color.FromArgb(252, 165, 165), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    DrawSearchHighlights(g, "│ " + sText, sx, y, rowHeight);
                    continue;
                }

                OutputLogEntry entry = row.Entry;
                if (entry == null) continue;

                int x = gutterW + 8 - scrollX;

                // Optional timestamp column
                if (showTimestamps)
                {
                    Rectangle tRect = new Rectangle(x, y, timeW - 4, rowHeight);
                    TextRenderer.DrawText(g, entry.TimestampText, Font, tRect, Color.FromArgb(148, 163, 184), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    x += timeW;
                }

                // Fixed-width Level badge
                string badge = OutputLogParser.FormatLevelBadge(entry.Level);
                Color badgeBg;
                Color badgeFg;
                Color msgColor;
                switch (entry.Level)
                {
                    case OutputLogLevel.Err:
                        badgeBg = Color.FromArgb(127, 29, 29);
                        badgeFg = Color.FromArgb(254, 202, 202);
                        msgColor = Color.FromArgb(252, 165, 165);
                        break;
                    case OutputLogLevel.Warn:
                        badgeBg = Color.FromArgb(120, 53, 15);
                        badgeFg = Color.FromArgb(253, 230, 138);
                        msgColor = Color.FromArgb(252, 211, 77);
                        break;
                    case OutputLogLevel.Dbg:
                        badgeBg = Color.FromArgb(55, 65, 81);
                        badgeFg = Color.FromArgb(209, 213, 219);
                        msgColor = Color.FromArgb(156, 163, 175);
                        break;
                    default:
                        badgeBg = Color.FromArgb(30, 58, 138);
                        badgeFg = Color.FromArgb(191, 219, 254);
                        msgColor = Color.FromArgb(226, 232, 240);
                        break;
                }

                Rectangle badgeRect = new Rectangle(x, y + 3, 42, rowHeight - 6);
                using (SolidBrush bBadge = new SolidBrush(badgeBg))
                {
                    g.FillRectangle(bBadge, badgeRect);
                }
                using (Font bf = new Font(this.Font.FontFamily, Math.Max(7.5F, this.Font.Size - 1.0F), FontStyle.Bold))
                {
                    TextRenderer.DrawText(g, badge, bf, badgeRect, badgeFg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                x += 50;

                // Source / Thread prefix + Message
                StringBuilder lineSb = new StringBuilder();
                if (!string.IsNullOrEmpty(entry.Source))
                {
                    lineSb.Append("[").Append(entry.Source).Append("] ");
                }
                if (!string.IsNullOrEmpty(entry.Thread) && entry.Thread != "-")
                {
                    lineSb.Append("[").Append(entry.Thread).Append("] ");
                }
                lineSb.Append(entry.Message ?? "");
                if (entry.StackLines.Count > 0)
                {
                    lineSb.Append(entry.IsStackExpanded ? "  ▾ " : "  ▸ ");
                    lineSb.Append(entry.StackLines.Count).Append(" lines");
                }

                string displayText = lineSb.ToString();
                int textW = isWordWrap ? Math.Max(100, clientW - x - 4) : Math.Max(clientW - x, maxLineWidth + 60);
                Rectangle msgRect = new Rectangle(x, y, textW, rowHeight);
                TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
                if (isWordWrap) flags |= TextFormatFlags.WordBreak;

                TextRenderer.DrawText(g, displayText, Font, msgRect, msgColor, flags);
                DrawSearchHighlights(g, displayText, x, y, rowHeight);
            }

            g.ResetClip();

            // Gutter (bookmark star + preserved original line number)
            for (int i = startIndex; i < endIndex; i++)
            {
                int y = (i - startIndex) * rowHeight;
                OutputDisplayRow row = displayRows[i];

                if (row.Kind == OutputRowKind.Entry && row.Entry != null)
                {
                    if (row.Entry.IsBookmarked)
                    {
                        Rectangle starRect = new Rectangle(2, y, 14, rowHeight);
                        TextRenderer.DrawText(g, "★", Font, starRect, Color.FromArgb(56, 189, 248), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                    if (showLineNumbers)
                    {
                        Rectangle numRect = new Rectangle(12, y, gutterW - 18, rowHeight);
                        TextRenderer.DrawText(g, row.Entry.LineNumber.ToString(), Font, numRect, Color.FromArgb(100, 116, 139), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                    }
                }
                else if (row.Kind == OutputRowKind.StackLine && showLineNumbers)
                {
                    Rectangle numRect = new Rectangle(12, y, gutterW - 18, rowHeight);
                    TextRenderer.DrawText(g, "·", Font, numRect, Color.FromArgb(71, 85, 105), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                }
            }
        }

        private void DrawSearchHighlights(Graphics g, string text, int x, int y, int rowHeight)
        {
            if (string.IsNullOrEmpty(searchQuery) || string.IsNullOrEmpty(text)) return;

            if (useRegex)
            {
                if (compiledSearchRegex == null) return;
                MatchCollection matches = compiledSearchRegex.Matches(text);
                foreach (Match m in matches)
                {
                    if (!m.Success || m.Length == 0) continue;
                    PaintHighlightSpan(g, text, m.Index, m.Length, x, y, rowHeight);
                }
            }
            else
            {
                StringComparison cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                int pos = 0;
                while (pos < text.Length)
                {
                    int idx = text.IndexOf(searchQuery, pos, cmp);
                    if (idx < 0) break;
                    PaintHighlightSpan(g, text, idx, searchQuery.Length, x, y, rowHeight);
                    pos = idx + Math.Max(1, searchQuery.Length);
                }
            }
        }

        private void PaintHighlightSpan(Graphics g, string text, int start, int len, int x, int y, int rowHeight)
        {
            string prefix = text.Substring(0, start);
            string matched = text.Substring(start, Math.Min(len, text.Length - start));
            TextFormatFlags measureFlags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            int preW = prefix.Length > 0 ? TextRenderer.MeasureText(g, prefix, Font, new Size(int.MaxValue, rowHeight), measureFlags).Width : 0;
            int matchW = TextRenderer.MeasureText(g, matched, Font, new Size(int.MaxValue, rowHeight), measureFlags).Width;

            Rectangle hlRect = new Rectangle(x + preW, y + 2, Math.Max(4, matchW), rowHeight - 4);
            using (SolidBrush bHl = new SolidBrush(Color.FromArgb(145, 202, 138, 4)))
            {
                g.FillRectangle(bHl, hlRect);
            }
            TextRenderer.DrawText(g, matched, Font, new Point(x + preW, y + 3), Color.FromArgb(254, 240, 138), measureFlags);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            this.Focus();

            if (e.Button != MouseButtons.Left) return;
            int rowHeight = Font.Height + 6;
            int idx = vScroll.Value + (e.Y / rowHeight);
            if (idx < 0 || idx >= displayRows.Count) return;

            selectedRowIndex = idx;
            OutputDisplayRow row = displayRows[idx];
            if (row.Entry != null)
            {
                if (row.Kind == OutputRowKind.Entry && row.Entry.StackLines.Count > 0 &&
                    (row.Entry.Level == OutputLogLevel.Err || e.X > GetGutterWidth()))
                {
                    row.Entry.IsStackExpanded = !row.Entry.IsStackExpanded;
                    if (StackToggled != null) StackToggled(this, EventArgs.Empty);
                }
                if (SelectedEntryChanged != null) SelectedEntryChanged(this, row.Entry);
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int rowHeight = Font.Height + 6;
            int idx = vScroll.Value + (e.Y / rowHeight);
            if (idx < 0 || idx >= displayRows.Count) idx = -1;
            if (idx != hoveredRowIndex)
            {
                hoveredRowIndex = idx;
                this.Cursor = (idx >= 0 && displayRows[idx].Entry != null && displayRows[idx].Entry.StackLines.Count > 0)
                    ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Control && e.KeyCode == Keys.B)
            {
                if (BookmarkToggleRequested != null) BookmarkToggleRequested(this, EventArgs.Empty);
                e.Handled = true;
            }
        }
    }

    public class OutputLogPanel : Panel
    {
        private const int MaxDisplayRows = 50000;
        private const long LargeFileThresholdBytes = 100L * 1024 * 1024;
        private const long LargeFileTailBytes = 20L * 1024 * 1024;

        // Top Filter Bar (3a)
        private Panel pnlFilterBar;
        private FilterChipButton btnAll;
        private FilterChipButton btnErrors;
        private FilterChipButton btnWarnings;
        private FilterChipButton btnInfo;
        private FilterChipButton btnDebug;
        private SearchBoxControl searchBox;
        private Label lblMatchCounter;
        private ModernButton btnMatchPrev;
        private ModernButton btnMatchNext;
        private ModernButton btnFollow;
        private ModernButton btnWrap;
        private ModernButton btnCopy;
        private ModernButton btnSave;
        private ModernButton btnClear;

        // Second Toolbar (3b)
        private Panel pnlSecondToolbar;
        private DarkComboBox cmbFiles;
        private DarkComboBox cmbSessions;
        private FilterChipButton btnSinceDeploy;
        private DarkComboBox cmbTimeRange;
        private ModernButton btnMatchCase;
        private ModernButton btnRegex;
        private ModernButton btnLines;
        private ModernButton btnTime;
        private ModernButton btnFontDec;
        private ModernButton btnFontInc;
        private ModernButton btnPrevError;
        private ModernButton btnNextError;
        private ModernButton btnBookmarks;
        private ModernButton btnOpenNotepad;
        private ToolTip toolTip;

        // Notice Banner (for >100MB or missing/locked info)
        private Label lblNoticeBanner;

        // Viewer + Minimap + Details
        private Panel pnlViewerHost;
        private OutputLogViewer viewer;
        private OutputLogMinimap minimap;

        private Panel pnlDetails;
        private Label lblDetailsMeta;
        private Label lblNewSinceDeployPill;
        private TextBox txtDetailsBody;

        // State & Data
        private string installDir = "";
        private string activeFilePath = "";
        private long lastFileOffset = 0;
        private long lastFileSize = 0;
        private DateTime lastFileWriteTime = DateTime.MinValue;
        private Encoding detectedEncoding = null;
        private byte[] pendingOddByte = null;
        private string trailingPartialLine = "";
        private int nextOriginalLineNumber = 1;
        private bool pendingSessionBanner = false;
        private OutputSessionInfo activeSession = null;

        private readonly List<OutputLogEntry> allEntries = new List<OutputLogEntry>();
        private readonly List<OutputSessionInfo> sessions = new List<OutputSessionInfo>();
        private List<OutputDisplayRow> currentDisplayRows = new List<OutputDisplayRow>();
        private List<int> searchMatchRows = new List<int>();
        private int currentMatchCursor = -1;

        private List<DeployMarker> deployMarkers = new List<DeployMarker>();
        private DateTime postDeployWatchUntil = DateTime.MinValue;
        private int errorsSinceLastDeploy = 0;

        private OutputLogLevel? levelFilter = null; // null = All
        private bool sinceLastDeploy = false;
        private bool matchCase = false;
        private bool useRegex = false;
        private float currentFontSize = 9.5F;
        private bool suppressComboEvents = false;
        private string lastReportedIoError = "";
        private bool isCappedAtLimit = false;
        private string largeFileNoticeText = "";

        // Tailing Infrastructure
        private FileSystemWatcher watcher;
        private Timer pollTimer;
        private Timer debounceTimer;
        private volatile bool pendingCheckRequested = false;
        private volatile bool isBackgroundReading = false;

        public Action<string> LogErrorToActivity { get; set; }
        public Action<string> LogInfoToActivity { get; set; }
        public Action<int> DeployErrorsDetected { get; set; }
        public Action<string> FooterMetaChanged { get; set; }

        public OutputLogPanel()
        {
            this.BackColor = Color.FromArgb(13, 17, 23);
            this.Dock = DockStyle.Fill;
            BuildUi();
            SetupTimers();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (pollTimer != null) { pollTimer.Stop(); pollTimer.Dispose(); pollTimer = null; }
                if (debounceTimer != null) { debounceTimer.Stop(); debounceTimer.Dispose(); debounceTimer = null; }
                if (watcher != null) { watcher.EnableRaisingEvents = false; watcher.Dispose(); watcher = null; }
                if (toolTip != null) { toolTip.Dispose(); toolTip = null; }
            }
            base.Dispose(disposing);
        }

        private void BuildUi()
        {
            toolTip = new ToolTip();

            // 1. Filter Bar
            pnlFilterBar = new Panel();
            pnlFilterBar.Dock = DockStyle.Top;
            pnlFilterBar.Height = 38;
            pnlFilterBar.BackColor = Color.FromArgb(13, 17, 23);
            pnlFilterBar.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(45, 55, 72), 1))
                {
                    e.Graphics.DrawLine(pen, 0, pnlFilterBar.Height - 1, pnlFilterBar.Width, pnlFilterBar.Height - 1);
                }
            };

            btnAll = new FilterChipButton() { Text = "All (0)", Size = new Size(64, 26), IsActive = true, ActiveFilledColor = Color.FromArgb(37, 99, 235) };
            btnErrors = new FilterChipButton() { Text = "Errors (0)", Size = new Size(78, 26), ActiveFilledColor = Color.FromArgb(220, 38, 38) };
            btnWarnings = new FilterChipButton() { Text = "Warnings (0)", Size = new Size(92, 26), ActiveFilledColor = Color.FromArgb(217, 119, 6) };
            btnInfo = new FilterChipButton() { Text = "Info (0)", Size = new Size(68, 26), ActiveFilledColor = Color.FromArgb(2, 132, 199) };
            btnDebug = new FilterChipButton() { Text = "Debug (0)", Size = new Size(76, 26), ActiveFilledColor = Color.FromArgb(75, 85, 99) };

            btnAll.Click += (s, e) => SetLevelFilter(null);
            btnErrors.Click += (s, e) => SetLevelFilter(OutputLogLevel.Err);
            btnWarnings.Click += (s, e) => SetLevelFilter(OutputLogLevel.Warn);
            btnInfo.Click += (s, e) => SetLevelFilter(OutputLogLevel.Info);
            btnDebug.Click += (s, e) => SetLevelFilter(OutputLogLevel.Dbg);

            searchBox = new SearchBoxControl();
            searchBox.InnerTextBox.TextChanged += (s, e) => ApplyFiltersAndSearch();
            searchBox.InnerTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    JumpSearchMatch(e.Shift ? -1 : 1);
                    e.SuppressKeyPress = true;
                }
            };

            lblMatchCounter = new Label()
            {
                Text = "0 of 0",
                Size = new Size(58, 26),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8F)
            };

            btnMatchPrev = new ModernButton() { Text = "▲", Size = new Size(24, 26), Font = new Font("Segoe UI", 7.5F) };
            btnMatchNext = new ModernButton() { Text = "▼", Size = new Size(24, 26), Font = new Font("Segoe UI", 7.5F) };
            btnMatchPrev.Click += (s, e) => JumpSearchMatch(-1);
            btnMatchNext.Click += (s, e) => JumpSearchMatch(1);
            toolTip.SetToolTip(btnMatchPrev, "Previous search match");
            toolTip.SetToolTip(btnMatchNext, "Next search match");

            btnFollow = new ModernButton() { Text = "⬇ Following", Size = new Size(92, 26), Font = new Font("Segoe UI", 8.5F), NormalTextColor = Color.FromArgb(52, 211, 153) };
            btnFollow.Click += (s, e) =>
            {
                viewer.SetFollowing(!viewer.IsFollowing);
            };

            btnWrap = new ModernButton() { Text = "↩ Wrap: OFF", Size = new Size(80, 26), Font = new Font("Segoe UI", 8.5F) };
            btnWrap.Click += (s, e) =>
            {
                viewer.WordWrapEnabled = !viewer.WordWrapEnabled;
                btnWrap.Text = viewer.WordWrapEnabled ? "↩ Wrap: ON" : "↩ Wrap: OFF";
                btnWrap.NormalTextColor = viewer.WordWrapEnabled ? Color.FromArgb(52, 211, 153) : Color.FromArgb(226, 232, 240);
                btnWrap.Invalidate();
            };

            btnCopy = new ModernButton() { Text = "📋 Copy", Size = new Size(64, 26), Font = new Font("Segoe UI", 8.5F) };
            btnCopy.Click += (s, e) => CopyFilteredLines();

            btnSave = new ModernButton() { Text = "💾 Save", Size = new Size(62, 26), Font = new Font("Segoe UI", 8.5F) };
            btnSave.Click += (s, e) => SaveFilteredLines();

            btnClear = new ModernButton() { Text = "🗑 Clear", Size = new Size(64, 26), Font = new Font("Segoe UI", 8.5F) };
            btnClear.Click += (s, e) => ArchiveAndClearLog();

            pnlFilterBar.Controls.AddRange(new Control[] {
                btnAll, btnErrors, btnWarnings, btnInfo, btnDebug,
                searchBox, lblMatchCounter, btnMatchPrev, btnMatchNext,
                btnFollow, btnWrap, btnCopy, btnSave, btnClear
            });
            pnlFilterBar.Resize += (s, e) => RelayoutFilterBar();

            // 2. Second Toolbar
            pnlSecondToolbar = new Panel();
            pnlSecondToolbar.Dock = DockStyle.Top;
            pnlSecondToolbar.Height = 36;
            pnlSecondToolbar.BackColor = Color.FromArgb(15, 20, 27);
            pnlSecondToolbar.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(45, 55, 72), 1))
                {
                    e.Graphics.DrawLine(pen, 0, pnlSecondToolbar.Height - 1, pnlSecondToolbar.Width, pnlSecondToolbar.Height - 1);
                }
            };

            cmbFiles = new DarkComboBox() { Size = new Size(138, 24), Font = new Font("Segoe UI", 8.25F) };
            cmbFiles.SelectedIndexChanged += (s, e) =>
            {
                if (suppressComboEvents) return;
                string selectedName = cmbFiles.SelectedItem != null ? cmbFiles.SelectedItem.ToString() : "";
                if (!string.IsNullOrEmpty(selectedName))
                {
                    SwitchActiveFile(selectedName);
                }
            };
            toolTip.SetToolTip(cmbFiles, "Select Output.log or rotated log file");

            cmbSessions = new DarkComboBox() { Size = new Size(160, 24), Font = new Font("Segoe UI", 8.25F) };
            cmbSessions.Items.Add("Latest session");
            cmbSessions.Items.Add("All sessions");
            cmbSessions.SelectedIndex = 1; // All sessions default
            cmbSessions.SelectedIndexChanged += (s, e) => { if (!suppressComboEvents) ApplyFiltersAndSearch(); };
            toolTip.SetToolTip(cmbSessions, "Filter by GrandPOS session");

            btnSinceDeploy = new FilterChipButton() { Text = "Since last deploy", Size = new Size(110, 24), Font = new Font("Segoe UI", 8F), ActiveFilledColor = Color.FromArgb(5, 150, 105) };
            btnSinceDeploy.Click += (s, e) =>
            {
                sinceLastDeploy = !sinceLastDeploy;
                btnSinceDeploy.IsActive = sinceLastDeploy;
                btnSinceDeploy.Invalidate();
                ApplyFiltersAndSearch();
            };

            cmbTimeRange = new DarkComboBox() { Size = new Size(92, 24), Font = new Font("Segoe UI", 8.25F) };
            cmbTimeRange.Items.AddRange(new object[] { "All time", "Last 15 min", "Last 1 h", "Today" });
            cmbTimeRange.SelectedIndex = 0;
            cmbTimeRange.SelectedIndexChanged += (s, e) => ApplyFiltersAndSearch();

            btnMatchCase = new ModernButton() { Text = "Aa", Size = new Size(28, 24), Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
            btnMatchCase.Click += (s, e) =>
            {
                matchCase = !matchCase;
                UpdateToggleStyle(btnMatchCase, matchCase);
                ApplyFiltersAndSearch();
            };
            toolTip.SetToolTip(btnMatchCase, "Match case");

            btnRegex = new ModernButton() { Text = ".*", Size = new Size(28, 24), Font = new Font("Consolas", 8.5F, FontStyle.Bold) };
            btnRegex.Click += (s, e) =>
            {
                useRegex = !useRegex;
                UpdateToggleStyle(btnRegex, useRegex);
                ApplyFiltersAndSearch();
            };
            toolTip.SetToolTip(btnRegex, "Use Regular Expression");

            btnLines = new ModernButton() { Text = "Lines", Size = new Size(44, 24), Font = new Font("Segoe UI", 8F) };
            UpdateToggleStyle(btnLines, true);
            btnLines.Click += (s, e) =>
            {
                viewer.ShowLineNumbers = !viewer.ShowLineNumbers;
                UpdateToggleStyle(btnLines, viewer.ShowLineNumbers);
            };

            btnTime = new ModernButton() { Text = "Time", Size = new Size(44, 24), Font = new Font("Segoe UI", 8F) };
            UpdateToggleStyle(btnTime, true);
            btnTime.Click += (s, e) =>
            {
                viewer.ShowTimestamps = !viewer.ShowTimestamps;
                UpdateToggleStyle(btnTime, viewer.ShowTimestamps);
            };

            btnFontDec = new ModernButton() { Text = "A-", Size = new Size(28, 24), Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
            btnFontInc = new ModernButton() { Text = "A+", Size = new Size(28, 24), Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
            btnFontDec.Click += (s, e) => { currentFontSize = Math.Max(7.5F, currentFontSize - 0.5F); viewer.SetFontSize(currentFontSize); };
            btnFontInc.Click += (s, e) => { currentFontSize = Math.Min(16.0F, currentFontSize + 0.5F); viewer.SetFontSize(currentFontSize); };

            btnPrevError = new ModernButton() { Text = "▲ Err", Size = new Size(52, 24), Font = new Font("Segoe UI", 8F), NormalTextColor = Color.FromArgb(252, 165, 165) };
            btnNextError = new ModernButton() { Text = "▼ Err", Size = new Size(52, 24), Font = new Font("Segoe UI", 8F), NormalTextColor = Color.FromArgb(252, 165, 165) };
            btnPrevError.Click += (s, e) => JumpToError(-1);
            btnNextError.Click += (s, e) => JumpToError(1);

            btnBookmarks = new ModernButton() { Text = "★ 0 ▾", Size = new Size(58, 24), Font = new Font("Segoe UI", 8F), NormalTextColor = Color.FromArgb(56, 189, 248) };
            btnBookmarks.Click += (s, e) => ShowBookmarksDropdown();
            toolTip.SetToolTip(btnBookmarks, "Jump to bookmarked lines (Ctrl+B to toggle on selected line)");

            btnOpenNotepad = new ModernButton() { Text = "📝 Notepad", Size = new Size(82, 24), Font = new Font("Segoe UI", 8F) };
            btnOpenNotepad.Click += (s, e) => OpenInNotepad();

            pnlSecondToolbar.Controls.AddRange(new Control[] {
                cmbFiles, cmbSessions, btnSinceDeploy, cmbTimeRange,
                btnMatchCase, btnRegex, btnLines, btnTime, btnFontDec, btnFontInc,
                btnPrevError, btnNextError, btnBookmarks, btnOpenNotepad
            });
            pnlSecondToolbar.Resize += (s, e) => RelayoutSecondToolbar();

            // Notice banner (hidden unless >100 MB or missing/locked)
            lblNoticeBanner = new Label()
            {
                Dock = DockStyle.Top,
                Height = 22,
                Visible = false,
                BackColor = Color.FromArgb(45, 32, 12),
                ForeColor = Color.FromArgb(253, 224, 71),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 10, 0)
            };

            // 3. Details Pane (Bottom)
            pnlDetails = new Panel();
            pnlDetails.Dock = DockStyle.Bottom;
            pnlDetails.Height = 88;
            pnlDetails.BackColor = Color.FromArgb(15, 20, 27);
            pnlDetails.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(45, 55, 72), 1))
                {
                    e.Graphics.DrawLine(pen, 0, 0, pnlDetails.Width, 0);
                }
            };

            lblDetailsMeta = new Label()
            {
                Text = "Select a log line to inspect details · Click an error to expand stack trace · Ctrl+B to bookmark",
                Location = new Point(10, 6),
                Size = new Size(700, 18),
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblNewSinceDeployPill = new Label()
            {
                Text = "NEW SINCE DEPLOY",
                AutoSize = true,
                Visible = false,
                BackColor = Color.FromArgb(6, 78, 59),
                ForeColor = Color.FromArgb(110, 231, 183),
                Font = new Font("Segoe UI", 7.25F, FontStyle.Bold),
                Padding = new Padding(5, 2, 5, 2),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            lblNewSinceDeployPill.Location = new Point(pnlDetails.Width - 135, 5);

            txtDetailsBody = new TextBox()
            {
                Location = new Point(10, 26),
                Size = new Size(pnlDetails.Width - 20, 56),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(15, 20, 27),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 8.75F)
            };

            pnlDetails.Controls.Add(lblDetailsMeta);
            pnlDetails.Controls.Add(lblNewSinceDeployPill);
            pnlDetails.Controls.Add(txtDetailsBody);
            pnlDetails.Resize += (s, e) =>
            {
                lblNewSinceDeployPill.Location = new Point(Math.Max(10, pnlDetails.ClientSize.Width - lblNewSinceDeployPill.Width - 10), 5);
                lblDetailsMeta.Width = Math.Max(100, pnlDetails.ClientSize.Width - (lblNewSinceDeployPill.Visible ? lblNewSinceDeployPill.Width + 24 : 20));
            };

            // 4. Viewer Host (Viewer + Minimap)
            pnlViewerHost = new Panel();
            pnlViewerHost.Dock = DockStyle.Fill;
            pnlViewerHost.BackColor = Color.FromArgb(13, 17, 23);

            minimap = new OutputLogMinimap();
            minimap.Dock = DockStyle.Right;
            minimap.ScrollRatioRequested += (s, ratio) => viewer.ScrollToRatio(ratio);

            viewer = new OutputLogViewer();
            viewer.Dock = DockStyle.Fill;
            viewer.SelectedEntryChanged += (s, entry) => UpdateDetailsPane(entry);
            viewer.FollowStateChanged += (s, following) => UpdateFollowButton(following);
            viewer.StackToggled += (s, e) => RebuildDisplayRows(false);
            viewer.BookmarkToggleRequested += (s, e) => ToggleBookmarkOnSelected();
            viewer.ViewportChanged += (s, e) => minimap.UpdateState(currentDisplayRows, viewer.FirstVisibleIndex, viewer.VisibleRowCount);

            pnlViewerHost.Controls.Add(viewer);
            pnlViewerHost.Controls.Add(minimap);

            // Compose panel in Dock order
            this.Controls.Add(pnlViewerHost);
            this.Controls.Add(pnlDetails);
            this.Controls.Add(lblNoticeBanner);
            this.Controls.Add(pnlSecondToolbar);
            this.Controls.Add(pnlFilterBar);
        }

        private void SetupTimers()
        {
            pollTimer = new Timer();
            pollTimer.Interval = 500;
            pollTimer.Tick += (s, e) =>
            {
                if (!isBackgroundReading)
                {
                    TriggerTailCheck();
                }
            };
            pollTimer.Start();

            debounceTimer = new Timer();
            debounceTimer.Interval = 120;
            debounceTimer.Tick += (s, e) =>
            {
                debounceTimer.Stop();
                if (!isBackgroundReading)
                {
                    TriggerTailCheck();
                }
            };
        }

        public void Initialize(string dir, List<DeployMarker> initialMarkers)
        {
            deployMarkers = initialMarkers != null ? new List<DeployMarker>(initialMarkers) : new List<DeployMarker>();
            SetInstallDirectory(dir);
            RelayoutFilterBar();
            RelayoutSecondToolbar();
        }

        public void SetInstallDirectory(string dir)
        {
            string cleanDir = (dir ?? "").Trim();
            if (string.Equals(installDir, cleanDir, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(activeFilePath))
            {
                return;
            }

            installDir = cleanDir;
            SetupWatcher();
            RefreshLogFileList();
        }

        public void NotifyDeployCompleted(DeployMarker marker, bool autoRestart)
        {
            if (marker != null)
            {
                deployMarkers.Add(marker);
            }

            if (autoRestart)
            {
                errorsSinceLastDeploy = 0;
                postDeployWatchUntil = DateTime.Now.AddSeconds(60);

                sinceLastDeploy = true;
                btnSinceDeploy.IsActive = true;
                btnSinceDeploy.Invalidate();

                viewer.SetFollowing(true);
                UpdateFollowButton(true);
            }

            RebuildDisplayRows(true);
            TriggerTailCheck();
        }

        private void SetupWatcher()
        {
            try
            {
                if (watcher != null)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                    watcher = null;
                }

                if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir)) return;

                watcher = new FileSystemWatcher(installDir, "*.log");
                watcher.IncludeSubdirectories = false;
                watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime;
                FileSystemEventHandler handler = (s, e) =>
                {
                    try
                    {
                        Control invoker = this.IsHandleCreated ? (Control)this : (Control)this.FindForm();
                        if (invoker != null && !invoker.IsDisposed && invoker.IsHandleCreated)
                        {
                            invoker.BeginInvoke(new Action(() =>
                            {
                                pendingCheckRequested = true;
                                if (debounceTimer != null)
                                {
                                    debounceTimer.Stop();
                                    debounceTimer.Start();
                                }
                            }));
                        }
                    }
                    catch { }
                };
                watcher.Changed += handler;
                watcher.Created += handler;
                watcher.Renamed += (s, e) => handler(s, e);
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                ReportIoError("Output.log watcher setup failed: " + ex.Message);
            }
        }

        private void RefreshLogFileList()
        {
            try
            {
                List<string> files = new List<string>();
                if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                {
                    string[] found = Directory.GetFiles(installDir, "Output*.log");
                    Array.Sort(found, StringComparer.OrdinalIgnoreCase);
                    foreach (string f in found)
                    {
                        files.Add(Path.GetFileName(f));
                    }
                    string subOut = Path.Combine(installDir, "output");
                    if (Directory.Exists(subOut))
                    {
                        string[] subFound = Directory.GetFiles(subOut, "*.log");
                        Array.Sort(subFound, StringComparer.OrdinalIgnoreCase);
                        foreach (string sf in subFound)
                        {
                            files.Add(@"output\" + Path.GetFileName(sf));
                        }
                    }
                }

                if (files.Count == 0)
                {
                    files.Add("Output.log");
                }

                string prev = cmbFiles.SelectedItem != null ? cmbFiles.SelectedItem.ToString() : "Output.log";
                suppressComboEvents = true;
                cmbFiles.Items.Clear();
                foreach (string name in files) cmbFiles.Items.Add(name);

                int idx = cmbFiles.Items.IndexOf(prev);
                if (idx < 0)
                {
                    idx = cmbFiles.Items.IndexOf("Output.log");
                }
                if (idx < 0) idx = 0;
                cmbFiles.SelectedIndex = idx;
                suppressComboEvents = false;

                SwitchActiveFile(cmbFiles.Items[idx].ToString());
            }
            catch (Exception ex)
            {
                suppressComboEvents = false;
                ReportIoError("Failed to enumerate Output.log files: " + ex.Message);
            }
        }

        private void SwitchActiveFile(string relativeName)
        {
            string fullPath = string.IsNullOrEmpty(installDir) ? relativeName : Path.Combine(installDir, relativeName);
            activeFilePath = fullPath;
            ResetReaderState();
            TriggerTailCheck();
        }

        private void ResetReaderState()
        {
            lastFileOffset = 0;
            lastFileSize = 0;
            lastFileWriteTime = DateTime.MinValue;
            detectedEncoding = null;
            pendingOddByte = null;
            trailingPartialLine = "";
            nextOriginalLineNumber = 1;
            pendingSessionBanner = false;
            activeSession = null;
            largeFileNoticeText = "";
            lastReportedIoError = "";

            allEntries.Clear();
            sessions.Clear();
            currentDisplayRows.Clear();
            UpdateSessionDropdown();
            UpdateFilterChipsAndFooter();
            UpdateNoticeBanner();
            viewer.SetDisplayRows(currentDisplayRows, 1, false);
        }

        public void TriggerTailCheck()
        {
            if (isBackgroundReading || string.IsNullOrEmpty(activeFilePath)) return;

            string targetPath = activeFilePath;
            if (!File.Exists(targetPath))
            {
                if (allEntries.Count > 0)
                {
                    ResetReaderState();
                }
                SetMissingNotice("Waiting for " + Path.GetFileName(targetPath) + " in " + installDir + "...");
                UpdateFooterMetaOnly(0, 0, DateTime.MinValue);
                return;
            }

            long currentLen = 0;
            DateTime currentWrite = DateTime.MinValue;
            try
            {
                FileInfo fi = new FileInfo(targetPath);
                currentLen = fi.Length;
                currentWrite = fi.LastWriteTime;
            }
            catch (Exception ex)
            {
                ReportIoError("Cannot inspect Output.log: " + ex.Message);
                return;
            }

            // Detect truncation or file rotation
            bool resetNeeded = (currentLen < lastFileOffset);
            if (!resetNeeded && !pendingCheckRequested && currentLen == lastFileOffset && currentWrite == lastFileWriteTime && lastFileOffset > 0)
            {
                return;
            }

            pendingCheckRequested = false;
            isBackgroundReading = true;

            long startOffset = resetNeeded ? 0 : lastFileOffset;
            Encoding encSnap = resetNeeded ? null : detectedEncoding;
            byte[] oddSnap = resetNeeded ? null : pendingOddByte;
            string partialSnap = resetNeeded ? "" : trailingPartialLine;
            int lineNumStart = resetNeeded ? 1 : nextOriginalLineNumber;
            bool pendingSessSnap = resetNeeded ? false : pendingSessionBanner;
            int sessionCountSnap = resetNeeded ? 0 : sessions.Count;

            Control invokerTarget = this.IsHandleCreated ? (Control)this : (Control)this.FindForm();
            Task.Run(() =>
            {
                TailReadResult result = ReadAndParseDelta(targetPath, startOffset, encSnap, oddSnap, partialSnap, lineNumStart, pendingSessSnap, sessionCountSnap);
                try
                {
                    if (invokerTarget != null && !invokerTarget.IsDisposed && invokerTarget.IsHandleCreated)
                    {
                        invokerTarget.BeginInvoke(new Action(() => ApplyTailReadResult(result, resetNeeded)));
                    }
                    else
                    {
                        isBackgroundReading = false;
                    }
                }
                catch
                {
                    isBackgroundReading = false;
                }
            });
        }

        private class TailReadResult
        {
            public bool Success;
            public string ErrorMessage;
            public string FilePath;
            public long NewOffset;
            public long FileSize;
            public DateTime WriteTime;
            public Encoding Encoding;
            public byte[] OddByte;
            public string TrailingPartial;
            public int NextLineNumber;
            public bool PendingSessionBanner;
            public string LargeFileNotice;
            public List<OutputLogEntry> NewEntries = new List<OutputLogEntry>();
            public List<string> LeadingContinuationLines = new List<string>();
            public List<OutputSessionInfo> NewSessions = new List<OutputSessionInfo>();
        }

        private TailReadResult ReadAndParseDelta(
            string path,
            long startOffset,
            Encoding existingEncoding,
            byte[] oddByte,
            string partialLine,
            int startLineNum,
            bool pendingSess,
            int existingSessionCount)
        {
            TailReadResult res = new TailReadResult();
            res.FilePath = path;
            res.Encoding = existingEncoding;
            res.OddByte = oddByte;
            res.TrailingPartial = partialLine;
            res.NextLineNumber = startLineNum;
            res.PendingSessionBanner = pendingSess;

            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    FileInfo fi = new FileInfo(path);
                    res.FileSize = fs.Length;
                    res.WriteTime = fi.LastWriteTime;

                    if (fs.Length == 0)
                    {
                        res.NewOffset = 0;
                        res.Success = true;
                        return res;
                    }

                    // Detect encoding on initial load
                    if (res.Encoding == null)
                    {
                        byte[] probe = new byte[(int)Math.Min(512, fs.Length)];
                        int probeRead = fs.Read(probe, 0, probe.Length);
                        int bomSkip = 0;
                        res.Encoding = DetectEncoding(probe, probeRead, out bomSkip);
                        if (startOffset == 0)
                        {
                            startOffset = bomSkip;
                        }
                    }

                    // Handle very large files (> 100 MB -> read last 20 MB)
                    if (startOffset <= 3 && fs.Length > LargeFileThresholdBytes)
                    {
                        long tailStart = fs.Length - LargeFileTailBytes;
                        if (res.Encoding == Encoding.Unicode || res.Encoding == Encoding.BigEndianUnicode)
                        {
                            tailStart &= ~1L;
                        }
                        fs.Seek(tailStart, SeekOrigin.Begin);
                        res.LargeFileNotice = string.Format("Large file ({0}): showing last 20 MB only.", FormatBytes(fs.Length));
                        startOffset = tailStart;
                    }
                    else
                    {
                        fs.Seek(startOffset, SeekOrigin.Begin);
                    }

                    long remaining = fs.Length - startOffset;
                    if (remaining <= 0)
                    {
                        res.NewOffset = fs.Length;
                        res.Success = true;
                        return res;
                    }

                    byte[] rawDelta = new byte[remaining];
                    int totalRead = 0;
                    while (totalRead < rawDelta.Length)
                    {
                        int n = fs.Read(rawDelta, totalRead, rawDelta.Length - totalRead);
                        if (n <= 0) break;
                        totalRead += n;
                    }

                    res.NewOffset = startOffset + totalRead;

                    byte[] combinedBytes;
                    if (res.OddByte != null && res.OddByte.Length > 0)
                    {
                        combinedBytes = new byte[res.OddByte.Length + totalRead];
                        Buffer.BlockCopy(res.OddByte, 0, combinedBytes, 0, res.OddByte.Length);
                        Buffer.BlockCopy(rawDelta, 0, combinedBytes, res.OddByte.Length, totalRead);
                        res.OddByte = null;
                    }
                    else if (totalRead == rawDelta.Length)
                    {
                        combinedBytes = rawDelta;
                    }
                    else
                    {
                        combinedBytes = new byte[totalRead];
                        Buffer.BlockCopy(rawDelta, 0, combinedBytes, 0, totalRead);
                    }

                    int decodeLen = combinedBytes.Length;
                    if ((res.Encoding == Encoding.Unicode || res.Encoding == Encoding.BigEndianUnicode) && (decodeLen % 2) != 0)
                    {
                        res.OddByte = new byte[] { combinedBytes[decodeLen - 1] };
                        decodeLen -= 1;
                    }

                    string chunkText = DecodeBytesWithFallback(combinedBytes, decodeLen, res.Encoding);

                    // If we jumped into the middle of a >100MB file, drop the first partial line
                    if (!string.IsNullOrEmpty(res.LargeFileNotice))
                    {
                        int firstNl = chunkText.IndexOf('\n');
                        if (firstNl >= 0 && firstNl + 1 < chunkText.Length)
                        {
                            chunkText = chunkText.Substring(firstNl + 1);
                        }
                    }

                    string fullText = (res.TrailingPartial ?? "") + chunkText;
                    int lastNl = fullText.LastIndexOf('\n');
                    string completeBlock;
                    if (lastNl >= 0)
                    {
                        completeBlock = fullText.Substring(0, lastNl + 1);
                        res.TrailingPartial = fullText.Substring(lastNl + 1);
                    }
                    else
                    {
                        // Wait for line terminator unless file hasn't grown
                        completeBlock = "";
                        res.TrailingPartial = fullText;
                    }

                    if (!string.IsNullOrEmpty(completeBlock))
                    {
                        string[] lines = completeBlock.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
                        // Split leaves an empty trailing element after the ending \n
                        int limit = (lines.Length > 0 && lines[lines.Length - 1] == "") ? lines.Length - 1 : lines.Length;
                        OutputLogEntry lastEntryInBatch = null;
                        int currentSessIdx = existingSessionCount;

                        for (int i = 0; i < limit; i++)
                        {
                            string line = lines[i];
                            int lineNum = res.NextLineNumber++;

                            try
                            {
                                if (OutputLogParser.BeginBannerRegex.IsMatch(line))
                                {
                                    res.PendingSessionBanner = true;
                                    lastEntryInBatch = null;
                                    continue;
                                }

                                Match m = OutputLogParser.LineHeaderRegex.Match(line);
                                if (m.Success)
                                {
                                    OutputLogEntry entry = new OutputLogEntry();
                                    entry.LineNumber = lineNum;
                                    entry.RawLine = line;
                                    entry.Source = m.Groups["source"].Value.Trim();
                                    entry.Thread = m.Groups["thread"].Success ? m.Groups["thread"].Value.Trim() : "-";
                                    entry.Message = m.Groups["msg"].Value;
                                    entry.Level = OutputLogParser.ParseLevel(m.Groups["level"].Value);

                                    DateTime parsedTs;
                                    string rawTs = m.Groups["ts"].Value.Trim();
                                    if (OutputLogParser.TryParseTimestamp(rawTs, out parsedTs))
                                    {
                                        entry.Timestamp = parsedTs;
                                        entry.TimestampText = parsedTs.ToString("HH:mm:ss.fff");
                                    }
                                    else
                                    {
                                        entry.TimestampText = rawTs;
                                    }

                                    Match sm = OutputLogParser.StartupInfoRegex.Match(entry.Message);
                                    if (res.PendingSessionBanner || sm.Success)
                                    {
                                        res.PendingSessionBanner = false;
                                        currentSessIdx++;
                                        string timePart = entry.Timestamp.HasValue ? entry.Timestamp.Value.ToString("HH:mm:ss") : entry.TimestampText;
                                        string verPart = sm.Success && sm.Groups["ver"].Success ? (" · " + sm.Groups["ver"].Value) : "";
                                        string pidPart = (sm.Success && sm.Groups["pid"].Success && !string.IsNullOrEmpty(sm.Groups["pid"].Value))
                                            ? (" · PID " + sm.Groups["pid"].Value) : "";
                                        string label = string.Format("Session {0}{1}{2}", timePart, verPart, pidPart);

                                        entry.IsSessionStart = true;
                                        entry.SessionIndex = currentSessIdx;
                                        entry.SessionBannerText = string.Format("── {0} ──", label);

                                        OutputSessionInfo sInfo = new OutputSessionInfo();
                                        sInfo.Index = currentSessIdx;
                                        sInfo.Label = label;
                                        sInfo.StartTime = entry.Timestamp;
                                        res.NewSessions.Add(sInfo);
                                    }
                                    else
                                    {
                                        entry.SessionIndex = currentSessIdx;
                                    }

                                    res.NewEntries.Add(entry);
                                    lastEntryInBatch = entry;
                                }
                                else
                                {
                                    // Non-header line (stack trace / multi-line continuation)
                                    if (!string.IsNullOrWhiteSpace(line))
                                    {
                                        if (lastEntryInBatch != null)
                                        {
                                            lastEntryInBatch.StackLines.Add(line);
                                        }
                                        else if (startOffset > 3)
                                        {
                                            res.LeadingContinuationLines.Add(line);
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Never crash on a malformed line
                                res.NewEntries.Add(new OutputLogEntry
                                {
                                    LineNumber = lineNum,
                                    RawLine = line,
                                    Message = line,
                                    Level = OutputLogLevel.Info,
                                    SessionIndex = currentSessIdx
                                });
                            }
                        }
                    }

                    res.Success = true;
                }
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.ErrorMessage = ex.Message;
            }
            return res;
        }

        private static Encoding DetectEncoding(byte[] probe, int count, out int bomLength)
        {
            bomLength = 0;
            if (count >= 2 && probe[0] == 0xFF && probe[1] == 0xFE)
            {
                bomLength = 2;
                return Encoding.Unicode;
            }
            if (count >= 2 && probe[0] == 0xFE && probe[1] == 0xFF)
            {
                bomLength = 2;
                return Encoding.BigEndianUnicode;
            }
            if (count >= 3 && probe[0] == 0xEF && probe[1] == 0xBB && probe[2] == 0xBF)
            {
                bomLength = 3;
                return Encoding.UTF8;
            }

            // Detect BOM-less UTF-16 LE (used by GrandPOS Output.log)
            int zeroOdd = 0;
            int pairs = count / 2;
            if (pairs >= 4)
            {
                for (int i = 1; i < pairs * 2; i += 2)
                {
                    if (probe[i - 1] != 0x00 && probe[i] == 0x00) zeroOdd++;
                }
                if (zeroOdd * 2 >= pairs)
                {
                    return Encoding.Unicode;
                }
            }

            return new UTF8Encoding(false, true);
        }

        private static string DecodeBytesWithFallback(byte[] bytes, int len, Encoding enc)
        {
            if (len <= 0) return "";
            try
            {
                return enc.GetString(bytes, 0, len);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default.GetString(bytes, 0, len);
            }
            catch
            {
                return Encoding.Default.GetString(bytes, 0, len);
            }
        }

        private void ApplyTailReadResult(TailReadResult res, bool wasReset)
        {
            isBackgroundReading = false;
            if (this.IsDisposed) return;

            if (!string.Equals(res.FilePath, activeFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!res.Success)
            {
                ReportIoError("Output.log read error: " + res.ErrorMessage);
                return;
            }

            lastReportedIoError = "";
            if (wasReset)
            {
                allEntries.Clear();
                sessions.Clear();
            }

            lastFileOffset = res.NewOffset;
            lastFileSize = res.FileSize;
            lastFileWriteTime = res.WriteTime;
            detectedEncoding = res.Encoding;
            pendingOddByte = res.OddByte;
            trailingPartialLine = res.TrailingPartial;
            nextOriginalLineNumber = res.NextLineNumber;
            pendingSessionBanner = res.PendingSessionBanner;
            if (!string.IsNullOrEmpty(res.LargeFileNotice))
            {
                largeFileNoticeText = res.LargeFileNotice;
            }

            bool hasNewData = res.NewEntries.Count > 0 || res.LeadingContinuationLines.Count > 0 || res.NewSessions.Count > 0 || wasReset;
            if (res.LeadingContinuationLines.Count > 0 && allEntries.Count > 0)
            {
                allEntries[allEntries.Count - 1].StackLines.AddRange(res.LeadingContinuationLines);
            }
            if (res.NewSessions.Count > 0)
            {
                sessions.AddRange(res.NewSessions);
                activeSession = sessions[sessions.Count - 1];
                UpdateSessionDropdown();
            }

            if (res.NewEntries.Count > 0)
            {
                allEntries.AddRange(res.NewEntries);

                // Post-deploy 60s error monitor
                if (!wasReset && DateTime.Now <= postDeployWatchUntil)
                {
                    DateTime latestDeployTs = GetLatestDeployTimestamp() ?? DateTime.MinValue;
                    int newErrs = 0;
                    foreach (OutputLogEntry ne in res.NewEntries)
                    {
                        if (ne.Level == OutputLogLevel.Err && (!ne.Timestamp.HasValue || ne.Timestamp.Value >= latestDeployTs.AddSeconds(-2)))
                        {
                            newErrs++;
                        }
                    }
                    if (newErrs > 0)
                    {
                        errorsSinceLastDeploy += newErrs;
                        if (DeployErrorsDetected != null) DeployErrorsDetected(errorsSinceLastDeploy);
                    }
                }
            }

            if (hasNewData || allEntries.Count == 0)
            {
                RebuildDisplayRows(viewer.IsFollowing);
            }
            else
            {
                UpdateFooterMetaOnly(allEntries.Count, lastFileSize, lastFileWriteTime);
            }
            UpdateNoticeBanner();
        }

        private void UpdateSessionDropdown()
        {
            string prev = cmbSessions.SelectedItem != null ? cmbSessions.SelectedItem.ToString() : "All sessions";
            suppressComboEvents = true;
            cmbSessions.Items.Clear();
            cmbSessions.Items.Add("Latest session");
            cmbSessions.Items.Add("All sessions");
            foreach (OutputSessionInfo s in sessions)
            {
                cmbSessions.Items.Add(s.Label);
            }
            int idx = cmbSessions.Items.IndexOf(prev);
            if (idx < 0) idx = 1; // All sessions
            cmbSessions.SelectedIndex = idx;
            suppressComboEvents = false;
        }

        private DateTime? GetLatestDeployTimestamp()
        {
            if (deployMarkers.Count == 0) return null;
            return deployMarkers[deployMarkers.Count - 1].Timestamp;
        }

        private void SetLevelFilter(OutputLogLevel? level)
        {
            levelFilter = level;
            btnAll.IsActive = (level == null);
            btnErrors.IsActive = (level == OutputLogLevel.Err);
            btnWarnings.IsActive = (level == OutputLogLevel.Warn);
            btnInfo.IsActive = (level == OutputLogLevel.Info);
            btnDebug.IsActive = (level == OutputLogLevel.Dbg);

            btnAll.Invalidate();
            btnErrors.Invalidate();
            btnWarnings.Invalidate();
            btnInfo.Invalidate();
            btnDebug.Invalidate();

            ApplyFiltersAndSearch();
        }

        private void ApplyFiltersAndSearch()
        {
            RebuildDisplayRows(false);
        }

        private void RebuildDisplayRows(bool scrollToBottomIfFollowing)
        {
            int countAll = allEntries.Count;
            int countErr = 0, countWarn = 0, countInfo = 0, countDbg = 0;
            int bookmarkCount = 0;
            foreach (OutputLogEntry e in allEntries)
            {
                if (e.Level == OutputLogLevel.Err) countErr++;
                else if (e.Level == OutputLogLevel.Warn) countWarn++;
                else if (e.Level == OutputLogLevel.Dbg) countDbg++;
                else countInfo++;
                if (e.IsBookmarked) bookmarkCount++;
            }

            btnAll.Text = string.Format("All ({0})", countAll);
            btnErrors.Text = string.Format("Errors ({0})", countErr);
            btnWarnings.Text = string.Format("Warnings ({0})", countWarn);
            btnInfo.Text = string.Format("Info ({0})", countInfo);
            btnDebug.Text = string.Format("Debug ({0})", countDbg);
            btnBookmarks.Text = string.Format("★ {0} ▾", bookmarkCount);
            btnAll.Invalidate(); btnErrors.Invalidate(); btnWarnings.Invalidate(); btnInfo.Invalidate(); btnDebug.Invalidate(); btnBookmarks.Invalidate();

            // Session filter
            int targetSessionIdx = -1; // -1 = all
            if (cmbSessions.SelectedIndex == 0 && sessions.Count > 0)
            {
                targetSessionIdx = sessions[sessions.Count - 1].Index;
            }
            else if (cmbSessions.SelectedIndex >= 2)
            {
                int sessPos = cmbSessions.SelectedIndex - 2;
                if (sessPos >= 0 && sessPos < sessions.Count) targetSessionIdx = sessions[sessPos].Index;
            }

            // Time range filter
            DateTime? timeCutoff = null;
            int trIdx = cmbTimeRange.SelectedIndex;
            if (trIdx == 1) timeCutoff = DateTime.Now.AddMinutes(-15);
            else if (trIdx == 2) timeCutoff = DateTime.Now.AddHours(-1);
            else if (trIdx == 3) timeCutoff = DateTime.Today;

            DateTime? latestDeploy = sinceLastDeploy ? GetLatestDeployTimestamp() : null;

            string query = (searchBox.TextValue ?? "").Trim();
            Regex regexObj = null;
            bool regexValid = true;
            if (useRegex && !string.IsNullOrEmpty(query))
            {
                try
                {
                    RegexOptions opts = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                    regexObj = new Regex(query, opts);
                }
                catch
                {
                    regexValid = false;
                }
            }
            viewer.SetSearchOptions(query, matchCase, useRegex, regexObj);

            List<OutputLogEntry> matchingEntries = new List<OutputLogEntry>();
            foreach (OutputLogEntry entry in allEntries)
            {
                if (levelFilter.HasValue && entry.Level != levelFilter.Value) continue;
                if (targetSessionIdx >= 0 && entry.SessionIndex != targetSessionIdx) continue;
                if (timeCutoff.HasValue && (!entry.Timestamp.HasValue || entry.Timestamp.Value < timeCutoff.Value)) continue;
                if (latestDeploy.HasValue && (!entry.Timestamp.HasValue || entry.Timestamp.Value < latestDeploy.Value)) continue;

                if (!string.IsNullOrEmpty(query) && regexValid)
                {
                    if (!EntryMatchesQuery(entry, query, regexObj)) continue;
                }
                matchingEntries.Add(entry);
            }

            // Interleave deploy markers and session banners chronologically
            List<DeployMarker> orderedDeploys = new List<DeployMarker>(deployMarkers);
            orderedDeploys.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

            DateTime? minEntryTs = null;
            foreach (OutputLogEntry me in matchingEntries)
            {
                if (me.Timestamp.HasValue) { minEntryTs = me.Timestamp.Value; break; }
            }
            int deployCursor = 0;
            if (minEntryTs.HasValue && !sinceLastDeploy)
            {
                while (deployCursor < orderedDeploys.Count && orderedDeploys[deployCursor].Timestamp < minEntryTs.Value.AddMinutes(-5))
                {
                    deployCursor++;
                }
            }
            else if (sinceLastDeploy && orderedDeploys.Count > 0)
            {
                deployCursor = orderedDeploys.Count - 1;
            }

            List<OutputDisplayRow> built = new List<OutputDisplayRow>(matchingEntries.Count + 16);
            foreach (OutputLogEntry entry in matchingEntries)
            {
                while (deployCursor < orderedDeploys.Count && entry.Timestamp.HasValue && orderedDeploys[deployCursor].Timestamp <= entry.Timestamp.Value)
                {
                    DeployMarker dm = orderedDeploys[deployCursor++];
                    built.Add(new OutputDisplayRow
                    {
                        Kind = OutputRowKind.DeployMarker,
                        DeployInfo = dm,
                        TimelineTime = dm.Timestamp,
                        BannerText = string.Format("▲ Deployed {0} · {1}/{2} · {3:HH:mm:ss}", dm.Mode, dm.Configuration, dm.Platform, dm.Timestamp)
                    });
                }

                if (entry.IsSessionStart && !string.IsNullOrEmpty(entry.SessionBannerText))
                {
                    built.Add(new OutputDisplayRow
                    {
                        Kind = OutputRowKind.SessionBanner,
                        TimelineTime = entry.Timestamp,
                        BannerText = entry.SessionBannerText
                    });
                }

                built.Add(new OutputDisplayRow
                {
                    Kind = OutputRowKind.Entry,
                    Entry = entry,
                    TimelineTime = entry.Timestamp
                });

                if (entry.IsStackExpanded && entry.StackLines.Count > 0)
                {
                    for (int s = 0; s < entry.StackLines.Count; s++)
                    {
                        built.Add(new OutputDisplayRow
                        {
                            Kind = OutputRowKind.StackLine,
                            Entry = entry,
                            StackLineIndex = s,
                            TimelineTime = entry.Timestamp
                        });
                    }
                }
            }

            // Append any recent deploy marker after the latest log line
            while (deployCursor < orderedDeploys.Count)
            {
                DeployMarker dm = orderedDeploys[deployCursor++];
                if (matchingEntries.Count > 0 || sinceLastDeploy)
                {
                    built.Add(new OutputDisplayRow
                    {
                        Kind = OutputRowKind.DeployMarker,
                        DeployInfo = dm,
                        TimelineTime = dm.Timestamp,
                        BannerText = string.Format("▲ Deployed {0} · {1}/{2} · {3:HH:mm:ss}", dm.Mode, dm.Configuration, dm.Platform, dm.Timestamp)
                    });
                }
            }

            // Virtualization cap at 50,000 most recent rows
            isCappedAtLimit = false;
            if (built.Count > MaxDisplayRows)
            {
                isCappedAtLimit = true;
                built = built.GetRange(built.Count - MaxDisplayRows, MaxDisplayRows);
            }

            currentDisplayRows = built;

            // Update search match indices
            searchMatchRows.Clear();
            if (!string.IsNullOrEmpty(query) && regexValid)
            {
                for (int i = 0; i < currentDisplayRows.Count; i++)
                {
                    if (currentDisplayRows[i].Kind == OutputRowKind.Entry)
                    {
                        searchMatchRows.Add(i);
                    }
                }
            }
            currentMatchCursor = searchMatchRows.Count > 0 ? 0 : -1;
            UpdateMatchCounterLabel();

            int maxLineNum = allEntries.Count > 0 ? allEntries[allEntries.Count - 1].LineNumber : 1;
            viewer.SetDisplayRows(currentDisplayRows, maxLineNum, scrollToBottomIfFollowing);
            minimap.UpdateState(currentDisplayRows, viewer.FirstVisibleIndex, viewer.VisibleRowCount);
            UpdateNoticeBanner();
            UpdateFooterMetaOnly(allEntries.Count, lastFileSize, lastFileWriteTime);
        }

        private bool EntryMatchesQuery(OutputLogEntry entry, string query, Regex regexObj)
        {
            StringComparison cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            string combined = string.Format("{0} {1} {2}", entry.Source, entry.Thread, entry.Message);
            if (useRegex)
            {
                if (regexObj != null && regexObj.IsMatch(combined)) return true;
                foreach (string st in entry.StackLines)
                {
                    if (regexObj != null && regexObj.IsMatch(st)) return true;
                }
                return false;
            }

            if (combined.IndexOf(query, cmp) >= 0) return true;
            foreach (string st in entry.StackLines)
            {
                if (st.IndexOf(query, cmp) >= 0) return true;
            }
            return false;
        }

        private void JumpSearchMatch(int delta)
        {
            if (searchMatchRows.Count == 0) return;
            if (currentMatchCursor < 0) currentMatchCursor = 0;
            else
            {
                currentMatchCursor = (currentMatchCursor + delta + searchMatchRows.Count) % searchMatchRows.Count;
            }
            UpdateMatchCounterLabel();
            viewer.ScrollToRow(searchMatchRows[currentMatchCursor], true);
        }

        private void UpdateMatchCounterLabel()
        {
            if (searchMatchRows.Count == 0)
            {
                lblMatchCounter.Text = "0 of 0";
            }
            else
            {
                lblMatchCounter.Text = string.Format("{0} of {1}", currentMatchCursor + 1, searchMatchRows.Count);
            }
        }

        private void JumpToError(int delta)
        {
            if (currentDisplayRows.Count == 0) return;
            int start = viewer.SelectedEntry != null
                ? currentDisplayRows.FindIndex(r => r.Kind == OutputRowKind.Entry && r.Entry == viewer.SelectedEntry)
                : (delta > 0 ? -1 : currentDisplayRows.Count);

            int n = currentDisplayRows.Count;
            for (int step = 1; step <= n; step++)
            {
                int idx = ((start + (delta * step)) % n + n) % n;
                OutputDisplayRow r = currentDisplayRows[idx];
                if (r.Kind == OutputRowKind.Entry && r.Entry != null && r.Entry.Level == OutputLogLevel.Err)
                {
                    viewer.ScrollToRow(idx, true);
                    return;
                }
            }
        }

        private void ToggleBookmarkOnSelected()
        {
            OutputLogEntry sel = viewer.SelectedEntry;
            if (sel == null) return;
            sel.IsBookmarked = !sel.IsBookmarked;
            RebuildDisplayRows(false);
        }

        private void ShowBookmarksDropdown()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Color.FromArgb(20, 25, 35);
            menu.ForeColor = Color.FromArgb(226, 232, 240);
            menu.ShowImageMargin = false;

            int count = 0;
            foreach (OutputLogEntry e in allEntries)
            {
                if (!e.IsBookmarked) continue;
                count++;
                OutputLogEntry captured = e;
                string snippet = (e.Message ?? "").Replace("\r", "").Replace("\n", " ");
                if (snippet.Length > 55) snippet = snippet.Substring(0, 55) + "...";
                ToolStripMenuItem item = new ToolStripMenuItem(string.Format("Line {0} · {1} · {2}", e.LineNumber, e.TimestampText, snippet));
                item.BackColor = Color.FromArgb(20, 25, 35);
                item.ForeColor = Color.FromArgb(226, 232, 240);
                item.Click += (s, ev) =>
                {
                    int idx = currentDisplayRows.FindIndex(r => r.Kind == OutputRowKind.Entry && r.Entry == captured);
                    if (idx >= 0) viewer.ScrollToRow(idx, true);
                };
                menu.Items.Add(item);
            }

            if (count == 0)
            {
                ToolStripMenuItem empty = new ToolStripMenuItem("No bookmarks yet (select a line & press Ctrl+B)");
                empty.Enabled = false;
                menu.Items.Add(empty);
            }

            menu.Show(btnBookmarks, new Point(0, btnBookmarks.Height));
        }

        private void UpdateDetailsPane(OutputLogEntry entry)
        {
            if (entry == null)
            {
                lblDetailsMeta.Text = "Select a log line to inspect details · Click an error to expand stack trace · Ctrl+B to bookmark";
                lblNewSinceDeployPill.Visible = false;
                txtDetailsBody.Text = "";
                return;
            }

            string badge = OutputLogParser.FormatLevelBadge(entry.Level);
            string src = string.IsNullOrEmpty(entry.Source) ? "-" : entry.Source;
            string thr = string.IsNullOrEmpty(entry.Thread) ? "-" : entry.Thread;
            string ts = string.IsNullOrEmpty(entry.TimestampText) ? "-" : entry.TimestampText;

            lblDetailsMeta.Text = string.Format("Line #{0}   |   Level: {1}   |   Time: {2}   |   Source: {3}   |   Thread: {4}",
                entry.LineNumber, badge, ts, src, thr);

            DateTime? latestDeploy = GetLatestDeployTimestamp();
            bool isNewSinceDeploy = latestDeploy.HasValue && entry.Timestamp.HasValue && entry.Timestamp.Value >= latestDeploy.Value;
            lblNewSinceDeployPill.Visible = isNewSinceDeploy;
            lblNewSinceDeployPill.Location = new Point(Math.Max(10, pnlDetails.ClientSize.Width - lblNewSinceDeployPill.Width - 10), 5);
            lblDetailsMeta.Width = Math.Max(100, pnlDetails.ClientSize.Width - (lblNewSinceDeployPill.Visible ? lblNewSinceDeployPill.Width + 24 : 20));

            StringBuilder body = new StringBuilder();
            body.AppendLine(entry.Message ?? "");
            if (entry.StackLines.Count > 0)
            {
                body.AppendLine("--- Additional / Stack Lines (" + entry.StackLines.Count + ") ---");
                foreach (string st in entry.StackLines)
                {
                    body.AppendLine(st);
                }
            }
            txtDetailsBody.Text = body.ToString().TrimEnd();
        }

        private void UpdateFollowButton(bool following)
        {
            if (following)
            {
                btnFollow.Text = "⬇ Following";
                btnFollow.NormalTextColor = Color.FromArgb(52, 211, 153);
                btnFollow.BorderColor = Color.FromArgb(55, 65, 81);
            }
            else
            {
                btnFollow.Text = "⏸ Paused";
                btnFollow.NormalTextColor = Color.FromArgb(251, 191, 36);
                btnFollow.BorderColor = Color.FromArgb(217, 119, 6);
            }
            btnFollow.Invalidate();
        }

        private void UpdateToggleStyle(ModernButton btn, bool active)
        {
            btn.NormalColor = active ? Color.FromArgb(30, 64, 175) : Color.FromArgb(28, 35, 48);
            btn.BorderColor = active ? Color.FromArgb(59, 130, 246) : Color.FromArgb(55, 65, 81);
            btn.NormalTextColor = active ? Color.White : Color.FromArgb(203, 213, 225);
            btn.Invalidate();
        }

        private void CopyFilteredLines()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (OutputDisplayRow r in currentDisplayRows)
                {
                    if (r.Kind == OutputRowKind.Entry && r.Entry != null)
                    {
                        sb.AppendLine(r.Entry.RawLine ?? r.Entry.Message);
                        if (!r.Entry.IsStackExpanded && r.Entry.StackLines.Count > 0)
                        {
                            foreach (string st in r.Entry.StackLines) sb.AppendLine(st);
                        }
                    }
                    else if (r.Kind == OutputRowKind.StackLine && r.Entry != null && r.StackLineIndex < r.Entry.StackLines.Count)
                    {
                        sb.AppendLine(r.Entry.StackLines[r.StackLineIndex]);
                    }
                    else if (!string.IsNullOrEmpty(r.BannerText))
                    {
                        sb.AppendLine(r.BannerText);
                    }
                }
                if (sb.Length > 0)
                {
                    Clipboard.SetText(sb.ToString());
                }
            }
            catch (Exception ex)
            {
                ReportIoError("Failed to copy Output.log lines: " + ex.Message);
            }
        }

        private void SaveFilteredLines()
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Export Filtered Output.log";
                sfd.Filter = "Text Files (*.txt)|*.txt|CSV Files (*.csv)|*.csv";
                sfd.FileName = string.Format("Output_filtered_{0:yyyyMMdd_HHmmss}.txt", DateTime.Now);
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    bool isCsv = sfd.FilterIndex == 2 || sfd.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
                    StringBuilder sb = new StringBuilder();
                    if (isCsv)
                    {
                        sb.AppendLine("Line,Timestamp,Level,Source,Thread,Message");
                        foreach (OutputDisplayRow r in currentDisplayRows)
                        {
                            if (r.Kind != OutputRowKind.Entry || r.Entry == null) continue;
                            OutputLogEntry e = r.Entry;
                            string fullMsg = e.Message ?? "";
                            if (e.StackLines.Count > 0)
                            {
                                fullMsg += " | " + string.Join(" | ", e.StackLines.ToArray());
                            }
                            sb.AppendFormat("{0},{1},{2},{3},{4},{5}\r\n",
                                e.LineNumber,
                                CsvEscape(e.TimestampText),
                                OutputLogParser.FormatLevelBadge(e.Level),
                                CsvEscape(e.Source),
                                CsvEscape(e.Thread),
                                CsvEscape(fullMsg));
                        }
                    }
                    else
                    {
                        foreach (OutputDisplayRow r in currentDisplayRows)
                        {
                            if (r.Kind == OutputRowKind.Entry && r.Entry != null)
                            {
                                sb.AppendLine(r.Entry.RawLine ?? r.Entry.Message);
                                foreach (string st in r.Entry.StackLines) sb.AppendLine(st);
                            }
                            else if (!string.IsNullOrEmpty(r.BannerText))
                            {
                                sb.AppendLine(r.BannerText);
                            }
                        }
                    }
                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    if (LogInfoToActivity != null) LogInfoToActivity("Exported Output.log view to: " + sfd.FileName);
                }
                catch (Exception ex)
                {
                    ReportIoError("Failed to export Output.log: " + ex.Message);
                    MessageBox.Show(this, "Failed to export Output.log:\n" + ex.Message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string CsvEscape(string s)
        {
            string val = s ?? "";
            return "\"" + val.Replace("\"", "\"\"") + "\"";
        }

        private void ArchiveAndClearLog()
        {
            if (string.IsNullOrEmpty(activeFilePath) || !File.Exists(activeFilePath))
            {
                MessageBox.Show(this, "No active Output.log file to clear.", "Clear Output.log", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string dir = Path.GetDirectoryName(activeFilePath);
            string archiveName = string.Format("Output_{0:yyyyMMdd_HHmmss}.log", DateTime.Now);
            string archivePath = Path.Combine(dir ?? "", archiveName);

            DialogResult dr = MessageBox.Show(
                this,
                string.Format("Archive current log to '{0}' and start a new empty '{1}'?", archiveName, Path.GetFileName(activeFilePath)),
                "Archive & Clear Output.log",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (dr != DialogResult.Yes) return;

            try
            {
                File.Copy(activeFilePath, archivePath, true);
                using (FileStream fs = new FileStream(activeFilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                {
                    fs.SetLength(0);
                }
                if (LogInfoToActivity != null) LogInfoToActivity("Archived Output.log to: " + archivePath);
                ResetReaderState();
                RefreshLogFileList();
            }
            catch (Exception ex)
            {
                ReportIoError("Failed to archive/clear Output.log: " + ex.Message);
                MessageBox.Show(this, "Could not archive and clear Output.log:\n" + ex.Message, "Clear Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenInNotepad()
        {
            try
            {
                if (string.IsNullOrEmpty(activeFilePath) || !File.Exists(activeFilePath))
                {
                    MessageBox.Show(this, "Output.log file not found:\n" + activeFilePath, "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Process.Start("notepad.exe", activeFilePath);
            }
            catch (Exception ex)
            {
                ReportIoError("Failed to open Output.log in Notepad: " + ex.Message);
            }
        }

        private void SetMissingNotice(string text)
        {
            lblNoticeBanner.Text = text;
            lblNoticeBanner.BackColor = Color.FromArgb(30, 41, 59);
            lblNoticeBanner.ForeColor = Color.FromArgb(148, 163, 184);
            lblNoticeBanner.Visible = true;
        }

        private void UpdateNoticeBanner()
        {
            if (!string.IsNullOrEmpty(largeFileNoticeText))
            {
                lblNoticeBanner.Text = "⚠ " + largeFileNoticeText + (isCappedAtLimit ? " (display capped at 50,000 most recent rows)" : "");
                lblNoticeBanner.BackColor = Color.FromArgb(45, 32, 12);
                lblNoticeBanner.ForeColor = Color.FromArgb(253, 224, 71);
                lblNoticeBanner.Visible = true;
            }
            else if (isCappedAtLimit)
            {
                lblNoticeBanner.Text = "⚠ Showing the most recent 50,000 matching rows (full parsed list retained in memory).";
                lblNoticeBanner.BackColor = Color.FromArgb(45, 32, 12);
                lblNoticeBanner.ForeColor = Color.FromArgb(253, 224, 71);
                lblNoticeBanner.Visible = true;
            }
            else
            {
                lblNoticeBanner.Visible = false;
            }
        }

        private void UpdateFilterChipsAndFooter()
        {
            UpdateFooterMetaOnly(allEntries.Count, lastFileSize, lastFileWriteTime);
        }

        private void UpdateFooterMetaOnly(int lineCount, long sizeBytes, DateTime updated)
        {
            if (FooterMetaChanged == null) return;
            string name = string.IsNullOrEmpty(activeFilePath) ? "Output.log" : Path.GetFileName(activeFilePath);
            string updatedStr = updated == DateTime.MinValue ? "--:--:--" : updated.ToString("HH:mm:ss");
            string text = string.Format("{0} · {1:N0} lines · {2} · updated {3}", name, lineCount, FormatBytes(sizeBytes), updatedStr);
            FooterMetaChanged(text);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            return (bytes / (1024.0 * 1024.0)).ToString("0.00") + " MB";
        }

        private void ReportIoError(string msg)
        {
            if (lastReportedIoError == msg) return;
            lastReportedIoError = msg;
            if (LogErrorToActivity != null) LogErrorToActivity(msg);
        }

        private void RelayoutFilterBar()
        {
            pnlFilterBar.SuspendLayout();
            int w = pnlFilterBar.ClientSize.Width;

            Control[] leftChips = new Control[] { btnAll, btnErrors, btnWarnings, btnInfo, btnDebug };
            int x = 8;
            foreach (Control c in leftChips)
            {
                c.Location = new Point(x, 6);
                x += c.Width + 4;
            }

            int right = w - 8;
            Control[] rightBtns = new Control[] { btnClear, btnSave, btnCopy, btnWrap, btnFollow };
            if (w >= 920)
            {
                pnlFilterBar.Height = 38;
                foreach (Control c in rightBtns)
                {
                    c.Location = new Point(right - c.Width, 6);
                    right -= c.Width + 4;
                }
                right -= 4;

                btnMatchNext.Location = new Point(right - btnMatchNext.Width, 6);
                right -= btnMatchNext.Width + 2;
                btnMatchPrev.Location = new Point(right - btnMatchPrev.Width, 6);
                right -= btnMatchPrev.Width + 4;
                lblMatchCounter.Location = new Point(right - lblMatchCounter.Width, 6);
                right -= lblMatchCounter.Width + 4;

                int searchW = Math.Max(90, Math.Min(200, right - (x + 4)));
                searchBox.Location = new Point(x + 4, 6);
                searchBox.Size = new Size(searchW, 26);
            }
            else
            {
                pnlFilterBar.Height = 68;
                foreach (Control c in rightBtns)
                {
                    c.Location = new Point(right - c.Width, 36);
                    right -= c.Width + 4;
                }
                int searchRight = right - 4;
                btnMatchNext.Location = new Point(searchRight - btnMatchNext.Width, 36);
                searchRight -= btnMatchNext.Width + 2;
                btnMatchPrev.Location = new Point(searchRight - btnMatchPrev.Width, 36);
                searchRight -= btnMatchPrev.Width + 4;
                lblMatchCounter.Location = new Point(searchRight - lblMatchCounter.Width, 36);
                searchRight -= lblMatchCounter.Width + 4;

                searchBox.Location = new Point(8, 36);
                searchBox.Size = new Size(Math.Max(90, searchRight - 8), 26);
            }

            pnlFilterBar.ResumeLayout(true);
        }

        private void RelayoutSecondToolbar()
        {
            pnlSecondToolbar.SuspendLayout();
            int maxW = Math.Max(200, pnlSecondToolbar.ClientSize.Width - 8);
            int x = 8;
            int y = 5;
            int rowH = 28;

            Control[] items = new Control[] {
                cmbFiles, cmbSessions, btnSinceDeploy, cmbTimeRange,
                btnMatchCase, btnRegex, btnLines, btnTime, btnFontDec, btnFontInc,
                btnPrevError, btnNextError, btnBookmarks, btnOpenNotepad
            };

            foreach (Control c in items)
            {
                if (x > 8 && x + c.Width > maxW)
                {
                    x = 8;
                    y += rowH;
                }
                c.Location = new Point(x, y);
                x += c.Width + 5;
            }

            pnlSecondToolbar.Height = y + rowH + 2;
            pnlSecondToolbar.ResumeLayout(true);
        }
    }

    #endregion

    #region Main Application Form

    public class MainForm : Form
    {
        // Header
        private Panel pnlHeader;
        private ModernButton btnToggleSidebar;
        private ToolTip sidebarToolTip;
        private bool isSidebarVisible = true;
        private Label lblAppTitle;
        private Label lblAppSubtitle;
        private StatusPill statusPill;

        // Sidebar
        private Panel pnlSidebar;
        private Panel pnlConfigGroup;
        private DarkComboBox cmbConfig;
        private DarkComboBox cmbPlatform;
        private CheckBox chkAutoRestart;

        // Collapsible Paths & Settings
        private ModernButton btnTogglePaths;
        private Panel pnlPathsContent;
        private TextBox txtSourceRoot;
        private TextBox txtInstallDir;
        private TextBox txtMSBuild;
        private ModernButton btnBrowseSrc;
        private ModernButton btnBrowseInst;
        private ModernButton btnBrowseMsb;
        private CheckBox chkSwitchToOutputAfterDeploy;
        private ModernButton btnSaveConfig;
        private bool isPathsExpanded = false;

        // Collapsible Build & Deploy Group
        private ModernButton btnToggleBuild;
        private Panel pnlBuildContent;
        private ModernButton btnDeployDlls;
        private ModernButton btnDeployExe;
        private ModernButton btnDeployAll;
        private ModernButton btnRollback;
        private bool isBuildExpanded = true;

        // Collapsible POS Control Group
        private ModernButton btnToggleProcess;
        private Panel pnlProcessContent;
        private ModernButton btnStartPos;
        private ModernButton btnStopPos;
        private ModernButton btnRestartPos;
        private bool isProcessExpanded = true;

        // Collapsible Tools Group
        private ModernButton btnToggleTools;
        private Panel pnlToolsContent;
        private ModernButton btnOpenPosLog;
        private ModernButton btnCleanBuild;
        private ModernButton btnOpenInstallDir;
        private ModernButton btnOpenDeployerLog;
        private bool isToolsExpanded = true;

        // Right Log Panel
        private Panel pnlLogContainer;
        private Panel pnlLogTabs;
        private TabHeaderButton tabActivity;
        private TabHeaderButton tabOutputLog;
        private TabHeaderButton tabBuildHistory;
        private TabHeaderButton tabBackups;
        private Panel pnlTabHost;
        private Panel pnlActivityView;
        private OutputLogPanel outputLogPanel;
        private Panel pnlBuildHistoryView;
        private Panel pnlBackupsView;
        private int activeLogTabIndex = 0;

        private Panel pnlLogToolbar;
        private FilterChipButton btnFilterAll;
        private FilterChipButton btnFilterErrors;
        private FilterChipButton btnFilterWarnings;
        private FilterChipButton btnFilterInfo;
        private SearchBoxControl searchBox;
        private ModernButton btnAutoScroll;
        private ModernButton btnWrap;
        private ModernButton btnCopy;
        private ModernButton btnSave;
        private ModernButton btnClear;

        private VirtualLogViewer logViewer;

        // Bottom Footer (Inside Log Panel)
        private Panel pnlLogFooter;
        private Label lblFooterStatus;
        private Label lblOutputFooterMeta;
        private SlimProgressBar progressBar;

        // Logic & Timers
        private Timer tmrPosStatus;
        private ConfigData currentConfig;
        private bool isBusy = false;

        public MainForm()
        {
            InitializeComponent();
            currentConfig = ConfigData.Load();
            PopulateControlsFromConfig();
            outputLogPanel.Initialize(currentConfig.InstallDir, currentConfig.DeployHistory);

            LogInfo("Grand POS Development Tool initialized. Ready for deployment.");
        }

        private void InitializeComponent()
        {
            this.Text = "Grand POS Development Tool";
            this.Size = new Size(1180, 800);
            this.MinimumSize = new Size(960, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(13, 17, 23);
            this.ForeColor = Color.FromArgb(230, 237, 243);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // ==========================================
            // 1. TOP HEADER
            // ==========================================
            pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 52;
            pnlHeader.BackColor = Color.FromArgb(13, 17, 23);
            pnlHeader.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                using (Pen pen = new Pen(Color.FromArgb(31, 111, 139), 1))
                {
                    g.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
                }
                using (SolidBrush bIcon = new SolidBrush(Color.FromArgb(57, 197, 232)))
                {
                    using (Font fIcon = new Font("Consolas", 14F, FontStyle.Bold))
                    {
                        g.DrawString(">_", fIcon, bIcon, 48, 13);
                    }
                }
            };

            sidebarToolTip = new ToolTip();

            btnToggleSidebar = new ModernButton();
            btnToggleSidebar.Text = "◀";
            btnToggleSidebar.Location = new Point(12, 12);
            btnToggleSidebar.Size = new Size(28, 28);
            btnToggleSidebar.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnToggleSidebar.NormalColor = Color.FromArgb(24, 31, 44);
            btnToggleSidebar.HoverColor = Color.FromArgb(36, 47, 66);
            btnToggleSidebar.PressedColor = Color.FromArgb(18, 24, 34);
            btnToggleSidebar.BorderColor = Color.FromArgb(51, 65, 85);
            btnToggleSidebar.NormalTextColor = Color.FromArgb(203, 213, 225);
            sidebarToolTip.SetToolTip(btnToggleSidebar, "Collapse left panel");
            btnToggleSidebar.Click += (s, e) => ToggleSidebar();

            lblAppTitle = new Label();
            lblAppTitle.Text = "Grand POS Development Tool";
            lblAppTitle.Font = new Font("Segoe UI", 12.5F, FontStyle.Bold);
            lblAppTitle.ForeColor = Color.White;
            lblAppTitle.BackColor = Color.Transparent;
            lblAppTitle.Location = new Point(80, 8);
            lblAppTitle.AutoSize = true;

            lblAppSubtitle = new Label();
            lblAppSubtitle.Text = "Automated Build, Staging && Process Utility";
            lblAppSubtitle.UseMnemonic = false;
            lblAppSubtitle.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            lblAppSubtitle.ForeColor = Color.FromArgb(148, 163, 184);
            lblAppSubtitle.BackColor = Color.Transparent;
            lblAppSubtitle.Location = new Point(82, 30);
            lblAppSubtitle.AutoSize = true;

            statusPill = new StatusPill();
            statusPill.Location = new Point(pnlHeader.Width - 210, 12);
            statusPill.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            pnlHeader.Controls.Add(btnToggleSidebar);
            pnlHeader.Controls.Add(lblAppTitle);
            pnlHeader.Controls.Add(lblAppSubtitle);
            pnlHeader.Controls.Add(statusPill);

            // ==========================================
            // 2. LEFT SIDEBAR (Fixed width 215px, no horizontal scroll)
            // ==========================================
            pnlSidebar = new Panel();
            pnlSidebar.Dock = DockStyle.Left;
            pnlSidebar.Width = 215;
            pnlSidebar.BackColor = Color.FromArgb(15, 20, 27);
            pnlSidebar.AutoScroll = false;
            pnlSidebar.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                using (Pen pen = new Pen(Color.FromArgb(33, 38, 45), 1))
                {
                    g.DrawLine(pen, pnlSidebar.Width - 1, 0, pnlSidebar.Width - 1, pnlSidebar.Height);
                }
            };

            int sideW = 195;

            // 2A. CONFIG GROUP
            pnlConfigGroup = new Panel() { Width = sideW, Height = 135 };
            Label lblConfigHdr = CreateSectionHeader("CONFIGURATION", 0);

            Label lblCfg = new Label() { Text = "Config:", Location = new Point(0, 22), AutoSize = true, ForeColor = Color.FromArgb(156, 163, 175), Font = new Font("Segoe UI", 8.5F) };
            cmbConfig = new DarkComboBox() { Location = new Point(0, 39), Size = new Size(sideW, 24) };
            cmbConfig.Items.AddRange(new object[] { "Release", "Debug" });
            cmbConfig.SelectedIndex = 0;

            Label lblPlat = new Label() { Text = "Platform:", Location = new Point(0, 68), AutoSize = true, ForeColor = Color.FromArgb(156, 163, 175), Font = new Font("Segoe UI", 8.5F) };
            cmbPlatform = new DarkComboBox() { Location = new Point(0, 85), Size = new Size(sideW, 24) };
            cmbPlatform.Items.AddRange(new object[] { "Win32", "x64" });
            cmbPlatform.SelectedIndex = 0;

            chkAutoRestart = new CheckBox() { Text = "Auto-restart GrandPOS", Location = new Point(0, 114), Size = new Size(sideW, 20), Checked = true, ForeColor = Color.FromArgb(220, 225, 235), Font = new Font("Segoe UI", 8.5F) };

            pnlConfigGroup.Controls.AddRange(new Control[] { lblConfigHdr, lblCfg, cmbConfig, lblPlat, cmbPlatform, chkAutoRestart });

            // 2B. COLLAPSIBLE "Paths & settings"
            btnTogglePaths = CreateGroupHeaderButton("▶  Paths && settings", sideW);
            btnTogglePaths.Click += (s, e) =>
            {
                isPathsExpanded = !isPathsExpanded;
                RelayoutSidebar();
            };

            pnlPathsContent = new Panel() { Width = sideW, Height = 230, Visible = false };
            int py = 0;

            Label lblSrc = new Label() { Text = "Source Code Root:", Location = new Point(0, py), AutoSize = true, ForeColor = Color.FromArgb(156, 163, 175), Font = new Font("Segoe UI", 8F) }; py += 16;
            txtSourceRoot = new TextBox() { Location = new Point(0, py), Size = new Size(130, 22), BackColor = Color.FromArgb(15, 20, 28), ForeColor = Color.White, Font = new Font("Segoe UI", 8.5F) };
            btnBrowseSrc = new ModernButton() { Text = "Browse", Location = new Point(135, py), Size = new Size(60, 22), Font = new Font("Segoe UI", 8F) };
            btnBrowseSrc.Click += (s, e) => BrowseFolder(txtSourceRoot, "Select Source Code Root Folder");
            py += 28;

            Label lblInst = new Label() { Text = "Grand POS Install Dir:", Location = new Point(0, py), AutoSize = true, ForeColor = Color.FromArgb(156, 163, 175), Font = new Font("Segoe UI", 8F) }; py += 16;
            txtInstallDir = new TextBox() { Location = new Point(0, py), Size = new Size(130, 22), BackColor = Color.FromArgb(15, 20, 28), ForeColor = Color.White, Font = new Font("Segoe UI", 8.5F) };
            btnBrowseInst = new ModernButton() { Text = "Browse", Location = new Point(135, py), Size = new Size(60, 22), Font = new Font("Segoe UI", 8F) };
            btnBrowseInst.Click += (s, e) => BrowseFolder(txtInstallDir, "Select Grand POS Installation Folder");
            py += 28;

            Label lblMsb = new Label() { Text = "MSBuild Executable:", Location = new Point(0, py), AutoSize = true, ForeColor = Color.FromArgb(156, 163, 175), Font = new Font("Segoe UI", 8F) }; py += 16;
            txtMSBuild = new TextBox() { Location = new Point(0, py), Size = new Size(130, 22), BackColor = Color.FromArgb(15, 20, 28), ForeColor = Color.White, Font = new Font("Segoe UI", 8.5F) };
            btnBrowseMsb = new ModernButton() { Text = "Browse", Location = new Point(135, py), Size = new Size(60, 22), Font = new Font("Segoe UI", 8F) };
            btnBrowseMsb.Click += (s, e) => BrowseFile(txtMSBuild, "MSBuild.exe|MSBuild.exe|Executables (*.exe)|*.exe");
            py += 26;

            chkSwitchToOutputAfterDeploy = new CheckBox()
            {
                Text = "Switch to Output.log after deploy",
                Location = new Point(0, py),
                Size = new Size(sideW, 22),
                Checked = false,
                ForeColor = Color.FromArgb(203, 213, 225),
                Font = new Font("Segoe UI", 7.75F)
            };
            py += 26;

            btnSaveConfig = new ModernButton()
            {
                Text = "💾 Save Settings",
                Location = new Point(0, py),
                Size = new Size(sideW, 26),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                NormalColor = Color.FromArgb(37, 99, 235),
                HoverColor = Color.FromArgb(59, 130, 246),
                PressedColor = Color.FromArgb(29, 78, 216),
                BorderColor = Color.FromArgb(30, 64, 175)
            };
            btnSaveConfig.Click += (s, e) => SaveConfigFromControls(true);

            pnlPathsContent.Controls.AddRange(new Control[] {
                lblSrc, txtSourceRoot, btnBrowseSrc,
                lblInst, txtInstallDir, btnBrowseInst,
                lblMsb, txtMSBuild, btnBrowseMsb,
                chkSwitchToOutputAfterDeploy,
                btnSaveConfig
            });

            // 2C. COLLAPSIBLE BUILD & DEPLOY GROUP
            btnToggleBuild = CreateGroupHeaderButton("▼  BUILD && DEPLOY", sideW);
            btnToggleBuild.Click += (s, e) =>
            {
                isBuildExpanded = !isBuildExpanded;
                RelayoutSidebar();
            };

            pnlBuildContent = new Panel() { Width = sideW, Height = 168 };

            btnDeployDlls = new ModernButton()
            {
                Text = "⚡ Pricing DLL",
                Location = new Point(0, 4),
                Size = new Size(sideW, 36),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                NormalColor = Color.FromArgb(30, 64, 175),
                HoverColor = Color.FromArgb(37, 99, 235),
                PressedColor = Color.FromArgb(29, 78, 216),
                BorderColor = Color.FromArgb(59, 130, 246)
            };
            btnDeployDlls.Click += async (s, e) => await ExecuteDeployment("DLLsOnly");

            btnDeployExe = new ModernButton()
            {
                Text = "⚡ EXE",
                Location = new Point(0, 44),
                Size = new Size(sideW, 36),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                NormalColor = Color.FromArgb(109, 40, 217),
                HoverColor = Color.FromArgb(124, 58, 237),
                PressedColor = Color.FromArgb(91, 33, 182),
                BorderColor = Color.FromArgb(139, 92, 246)
            };
            btnDeployExe.Click += async (s, e) => await ExecuteDeployment("ExeOnly");

            btnDeployAll = new ModernButton()
            {
                Text = "🚀 ALL",
                Location = new Point(0, 84),
                Size = new Size(sideW, 38),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                NormalColor = Color.FromArgb(5, 150, 105),
                HoverColor = Color.FromArgb(16, 185, 129),
                PressedColor = Color.FromArgb(4, 120, 87),
                BorderColor = Color.FromArgb(52, 211, 153)
            };
            btnDeployAll.Click += async (s, e) => await ExecuteDeployment("All");

            btnRollback = new ModernButton()
            {
                Text = "🔄 Rollback (Last Backup)",
                Location = new Point(0, 126),
                Size = new Size(sideW, 34),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                NormalColor = Color.FromArgb(180, 83, 9),
                HoverColor = Color.FromArgb(217, 119, 6),
                PressedColor = Color.FromArgb(146, 64, 14),
                BorderColor = Color.FromArgb(245, 158, 11)
            };
            btnRollback.Click += async (s, e) => await ExecuteDeployment("Rollback");

            pnlBuildContent.Controls.AddRange(new Control[] { btnDeployDlls, btnDeployExe, btnDeployAll, btnRollback });

            // 2D. COLLAPSIBLE POS CONTROL GROUP
            btnToggleProcess = CreateGroupHeaderButton("▼  POS CONTROL", sideW);
            btnToggleProcess.Click += (s, e) =>
            {
                isProcessExpanded = !isProcessExpanded;
                RelayoutSidebar();
            };

            pnlProcessContent = new Panel() { Width = sideW, Height = 120 };

            btnStartPos = new ModernButton()
            {
                Text = "▶ Start GrandPOS",
                Location = new Point(0, 4),
                Size = new Size(sideW, 34),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                NormalColor = Color.FromArgb(21, 128, 61),
                HoverColor = Color.FromArgb(22, 163, 74),
                PressedColor = Color.FromArgb(20, 83, 45),
                BorderColor = Color.FromArgb(34, 197, 94)
            };
            btnStartPos.Click += (s, e) => StartGrandPOS();

            btnStopPos = new ModernButton()
            {
                Text = "⏹ Stop GrandPOS",
                Location = new Point(0, 42),
                Size = new Size(sideW, 34),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                NormalColor = Color.FromArgb(185, 28, 28),
                HoverColor = Color.FromArgb(220, 38, 38),
                PressedColor = Color.FromArgb(153, 27, 27),
                BorderColor = Color.FromArgb(239, 68, 68)
            };
            btnStopPos.Click += (s, e) => StopGrandPOS();

            btnRestartPos = new ModernButton()
            {
                Text = "🔄 Restart GrandPOS",
                Location = new Point(0, 80),
                Size = new Size(sideW, 34),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                NormalColor = Color.FromArgb(3, 105, 161),
                HoverColor = Color.FromArgb(2, 132, 199),
                PressedColor = Color.FromArgb(7, 89, 133),
                BorderColor = Color.FromArgb(14, 165, 233)
            };
            btnRestartPos.Click += async (s, e) => await RestartGrandPOS();

            pnlProcessContent.Controls.AddRange(new Control[] { btnStartPos, btnStopPos, btnRestartPos });

            // 2E. COLLAPSIBLE TOOLS GROUP
            btnToggleTools = CreateGroupHeaderButton("▼  TOOLS", sideW);
            btnToggleTools.Click += (s, e) =>
            {
                isToolsExpanded = !isToolsExpanded;
                RelayoutSidebar();
            };

            pnlToolsContent = new Panel() { Width = sideW, Height = 155 };

            btnOpenPosLog = new ModernButton()
            {
                Text = "📄 Open Output.log",
                Location = new Point(0, 4),
                Size = new Size(sideW, 32),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                NormalColor = Color.FromArgb(126, 34, 206),
                HoverColor = Color.FromArgb(147, 51, 234),
                PressedColor = Color.FromArgb(107, 33, 168),
                BorderColor = Color.FromArgb(168, 85, 247)
            };
            btnOpenPosLog.Click += (s, e) => SwitchLogTab(1);

            btnCleanBuild = new ModernButton()
            {
                Text = "🧹 Clean build",
                Location = new Point(0, 40),
                Size = new Size(sideW, 32),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                NormalColor = Color.FromArgb(35, 42, 56),
                HoverColor = Color.FromArgb(45, 55, 72),
                PressedColor = Color.FromArgb(25, 32, 44),
                BorderColor = Color.FromArgb(60, 72, 95)
            };
            btnCleanBuild.Click += (s, e) => CleanBuildPlaceholder();

            btnOpenInstallDir = new ModernButton()
            {
                Text = "📁 Open install folder",
                Location = new Point(0, 76),
                Size = new Size(sideW, 32),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                NormalColor = Color.FromArgb(35, 42, 56),
                HoverColor = Color.FromArgb(45, 55, 72),
                PressedColor = Color.FromArgb(25, 32, 44),
                BorderColor = Color.FromArgb(60, 72, 95)
            };
            btnOpenInstallDir.Click += (s, e) => OpenInstallDir();

            btnOpenDeployerLog = new ModernButton()
            {
                Text = "📝 Deployer log",
                Location = new Point(0, 112),
                Size = new Size(sideW, 32),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                NormalColor = Color.FromArgb(35, 42, 56),
                HoverColor = Color.FromArgb(45, 55, 72),
                PressedColor = Color.FromArgb(25, 32, 44),
                BorderColor = Color.FromArgb(60, 72, 95)
            };
            btnOpenDeployerLog.Click += (s, e) => OpenLogFile();

            pnlToolsContent.Controls.AddRange(new Control[] { btnOpenPosLog, btnCleanBuild, btnOpenInstallDir, btnOpenDeployerLog });

            pnlSidebar.Controls.AddRange(new Control[] {
                pnlConfigGroup,
                btnTogglePaths, pnlPathsContent,
                btnToggleBuild, pnlBuildContent,
                btnToggleProcess, pnlProcessContent,
                btnToggleTools, pnlToolsContent
            });

            RelayoutSidebar();

            // ==========================================
            // 3. RIGHT SIDE: LOG PANEL (Fills remaining)
            // ==========================================
            pnlLogContainer = new Panel();
            pnlLogContainer.Dock = DockStyle.Fill;
            pnlLogContainer.BackColor = Color.FromArgb(13, 17, 23);

            // 3A. Toolbar
            pnlLogToolbar = new Panel();
            pnlLogToolbar.Dock = DockStyle.Top;
            pnlLogToolbar.Height = 38;
            pnlLogToolbar.BackColor = Color.FromArgb(13, 17, 23);
            pnlLogToolbar.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                using (Pen pen = new Pen(Color.FromArgb(45, 55, 72), 1))
                {
                    g.DrawLine(pen, 0, pnlLogToolbar.Height - 1, pnlLogToolbar.Width, pnlLogToolbar.Height - 1);
                }
            };

            // Filter Chips with filled highlight background
            btnFilterAll = new FilterChipButton() { Text = "All (0)", Location = new Point(8, 6), Size = new Size(68, 26), IsActive = true, ActiveFilledColor = Color.FromArgb(37, 99, 235) };
            btnFilterErrors = new FilterChipButton() { Text = "Errors (0)", Location = new Point(80, 6), Size = new Size(82, 26), ActiveFilledColor = Color.FromArgb(220, 38, 38) };
            btnFilterWarnings = new FilterChipButton() { Text = "Warnings (0)", Location = new Point(166, 6), Size = new Size(98, 26), ActiveFilledColor = Color.FromArgb(217, 119, 6) };
            btnFilterInfo = new FilterChipButton() { Text = "Info (0)", Location = new Point(268, 6), Size = new Size(72, 26), ActiveFilledColor = Color.FromArgb(2, 132, 199) };

            btnFilterAll.Click += (s, e) => SetActiveFilter(FilterMode.All);
            btnFilterErrors.Click += (s, e) => SetActiveFilter(FilterMode.Errors);
            btnFilterWarnings.Click += (s, e) => SetActiveFilter(FilterMode.Warnings);
            btnFilterInfo.Click += (s, e) => SetActiveFilter(FilterMode.Info);

            // Search box (dark, thin-bordered with readable placeholder)
            searchBox = new SearchBoxControl();
            searchBox.InnerTextBox.TextChanged += (s, e) =>
            {
                logViewer.SetSearchQuery(searchBox.TextValue);
            };

            // Unified Flat Style Action Buttons
            btnClear = new ModernButton() { Text = "🗑 Clear", Size = new Size(70, 26), Font = new Font("Segoe UI", 8.5F) };
            btnClear.Click += (s, e) => logViewer.ClearLog();

            btnSave = new ModernButton() { Text = "💾 Save", Size = new Size(68, 26), Font = new Font("Segoe UI", 8.5F) };
            btnSave.Click += (s, e) => logViewer.SaveLogToFile();

            btnCopy = new ModernButton() { Text = "📋 Copy", Size = new Size(68, 26), Font = new Font("Segoe UI", 8.5F) };
            btnCopy.Click += (s, e) => logViewer.CopyAllToClipboard();

            btnWrap = new ModernButton() { Text = "↩ Wrap: OFF", Size = new Size(82, 26), Font = new Font("Segoe UI", 8.5F) };
            btnWrap.Click += (s, e) =>
            {
                logViewer.WordWrapEnabled = !logViewer.WordWrapEnabled;
                btnWrap.Text = logViewer.WordWrapEnabled ? "↩ Wrap: ON" : "↩ Wrap: OFF";
                btnWrap.NormalTextColor = logViewer.WordWrapEnabled ? Color.FromArgb(52, 211, 153) : Color.FromArgb(226, 232, 240);
                btnWrap.Invalidate();
            };

            btnAutoScroll = new ModernButton() { Text = "⬇ Auto-scroll: ON", Size = new Size(110, 26), Font = new Font("Segoe UI", 8.5F) };
            btnAutoScroll.Click += (s, e) =>
            {
                logViewer.AutoScrollEnabled = !logViewer.AutoScrollEnabled;
                UpdateAutoScrollButtonAppearance();
            };

            pnlLogToolbar.Controls.AddRange(new Control[] {
                btnFilterAll, btnFilterErrors, btnFilterWarnings, btnFilterInfo,
                searchBox, btnAutoScroll, btnWrap, btnCopy, btnSave, btnClear
            });

            // 3B. Bottom Footer (Slim rounded progress bar, ends strictly below log)
            pnlLogFooter = new Panel();
            pnlLogFooter.Dock = DockStyle.Bottom;
            pnlLogFooter.Height = 30;
            pnlLogFooter.BackColor = Color.FromArgb(13, 17, 23);
            pnlLogFooter.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                using (Pen pen = new Pen(Color.FromArgb(45, 55, 72), 1))
                {
                    g.DrawLine(pen, 0, 0, pnlLogFooter.Width, 0);
                }
            };

            lblFooterStatus = new Label()
            {
                Text = "Ready",
                Location = new Point(10, 7),
                AutoSize = true,
                ForeColor = Color.FromArgb(203, 213, 225),
                Font = new Font("Segoe UI", 8.5F)
            };

            lblOutputFooterMeta = new Label()
            {
                Text = "",
                Location = new Point(160, 7),
                AutoSize = true,
                Visible = false,
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.5F)
            };
            lblFooterStatus.SizeChanged += (s, e) => PositionFooterMeta();
            lblFooterStatus.TextChanged += (s, e) => PositionFooterMeta();

            progressBar = new SlimProgressBar()
            {
                Size = new Size(180, 8),
                Location = new Point(pnlLogFooter.Width - 192, 11),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Value = 0
            };

            pnlLogFooter.Controls.Add(lblFooterStatus);
            pnlLogFooter.Controls.Add(lblOutputFooterMeta);
            pnlLogFooter.Controls.Add(progressBar);

            // 3C. Virtual Log Viewer (Dock = Fill, strictly above footer)
            logViewer = new VirtualLogViewer();
            logViewer.Dock = DockStyle.Fill;
            logViewer.CountsChanged += (s, e) => UpdateFilterChipCounts();
            logViewer.AutoScrollPausedChanged += (s, isPaused) => UpdateAutoScrollButtonAppearance();
            logViewer.StatusNotice += (s, msg) => { lblFooterStatus.Text = msg; };

            // 3D. Tabs above log area: [Activity log] [Output.log] [Build history] [Backups]
            pnlLogTabs = new Panel();
            pnlLogTabs.Dock = DockStyle.Top;
            pnlLogTabs.Height = 34;
            pnlLogTabs.BackColor = Color.FromArgb(19, 24, 34);
            pnlLogTabs.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(45, 55, 72), 1))
                {
                    e.Graphics.DrawLine(pen, 0, pnlLogTabs.Height - 1, pnlLogTabs.Width, pnlLogTabs.Height - 1);
                }
            };

            tabActivity = new TabHeaderButton() { Text = "Activity log", Location = new Point(0, 0), Width = 116, IsActive = true };
            tabOutputLog = new TabHeaderButton() { Text = "Output.log", Location = new Point(116, 0), Width = 148, IsActive = false };
            tabBuildHistory = new TabHeaderButton() { Text = "Build history", Location = new Point(264, 0), Width = 118, IsActive = false };
            tabBackups = new TabHeaderButton() { Text = "Backups", Location = new Point(382, 0), Width = 104, IsActive = false };

            tabActivity.Click += (s, e) => SwitchLogTab(0);
            tabOutputLog.Click += (s, e) => SwitchLogTab(1);
            tabBuildHistory.Click += (s, e) => SwitchLogTab(2);
            tabBackups.Click += (s, e) => SwitchLogTab(3);

            pnlLogTabs.Controls.AddRange(new Control[] { tabActivity, tabOutputLog, tabBuildHistory, tabBackups });

            pnlActivityView = new Panel() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(13, 17, 23) };
            pnlActivityView.Controls.Add(logViewer);
            pnlActivityView.Controls.Add(pnlLogToolbar);

            outputLogPanel = new OutputLogPanel();
            outputLogPanel.Visible = false;
            outputLogPanel.LogErrorToActivity = (msg) => LogError(msg);
            outputLogPanel.LogInfoToActivity = (msg) => LogInfo(msg);
            outputLogPanel.DeployErrorsDetected = (count) =>
            {
                if (activeLogTabIndex != 1)
                {
                    tabOutputLog.BadgeCount = count;
                    tabOutputLog.Invalidate();
                }
                lblFooterStatus.Text = string.Format("{0} errors since deploy", count);
            };
            outputLogPanel.FooterMetaChanged = (metaText) =>
            {
                lblOutputFooterMeta.Text = metaText;
                PositionFooterMeta();
            };

            pnlBuildHistoryView = CreatePlaceholderTabPanel("Build history — coming soon");
            pnlBackupsView = CreatePlaceholderTabPanel("Backups — coming soon");

            pnlTabHost = new Panel() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(13, 17, 23) };
            pnlTabHost.Controls.Add(pnlActivityView);
            pnlTabHost.Controls.Add(outputLogPanel);
            pnlTabHost.Controls.Add(pnlBuildHistoryView);
            pnlTabHost.Controls.Add(pnlBackupsView);

            pnlLogContainer.Controls.Clear();
            pnlLogContainer.Controls.Add(pnlTabHost);
            pnlLogContainer.Controls.Add(pnlLogFooter);
            pnlLogContainer.Controls.Add(pnlLogTabs);

            this.Controls.Add(pnlLogContainer);
            this.Controls.Add(pnlSidebar);
            this.Controls.Add(pnlHeader);

            pnlHeader.Resize += (s, e) =>
            {
                statusPill.Location = new Point(pnlHeader.ClientSize.Width - statusPill.Width - 14, 12);
            };
            pnlLogToolbar.Resize += (s, e) => RelayoutLogToolbar();
            pnlLogFooter.Resize += (s, e) =>
            {
                progressBar.Location = new Point(pnlLogFooter.ClientSize.Width - progressBar.Width - 12, 11);
                PositionFooterMeta();
            };
            this.Shown += (s, e) =>
            {
                RelayoutLogToolbar();
                statusPill.Location = new Point(pnlHeader.ClientSize.Width - statusPill.Width - 14, 12);
                progressBar.Location = new Point(pnlLogFooter.ClientSize.Width - progressBar.Width - 12, 11);
            };

            // Process status checker timer
            tmrPosStatus = new Timer();
            tmrPosStatus.Interval = 2000;
            tmrPosStatus.Tick += (s, e) => UpdatePosStatus();
            tmrPosStatus.Start();
            UpdatePosStatus();
            UpdateAutoScrollButtonAppearance();
        }

        private static Label CreateSectionHeader(string text, int y)
        {
            return new Label()
            {
                Text = text,
                Location = new Point(0, y),
                AutoSize = true,
                UseMnemonic = false,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184)
            };
        }

        private static ModernButton CreateGroupHeaderButton(string text, int width)
        {
            return new ModernButton()
            {
                Text = text,
                Size = new Size(width, 26),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                NormalColor = Color.FromArgb(32, 38, 52),
                HoverColor = Color.FromArgb(42, 50, 68),
                PressedColor = Color.FromArgb(25, 30, 42),
                BorderColor = Color.FromArgb(55, 65, 85),
                NormalTextColor = Color.FromArgb(203, 213, 225)
            };
        }

        private void ToggleSidebar()
        {
            isSidebarVisible = !isSidebarVisible;
            pnlSidebar.Visible = isSidebarVisible;
            btnToggleSidebar.Text = isSidebarVisible ? "◀" : "▶";
            btnToggleSidebar.NormalTextColor = isSidebarVisible ? Color.FromArgb(203, 213, 225) : Color.FromArgb(57, 197, 232);
            sidebarToolTip.SetToolTip(btnToggleSidebar, isSidebarVisible ? "Collapse left panel" : "Expand left panel");
            btnToggleSidebar.Invalidate();
            RelayoutLogToolbar();
        }

        private Panel CreatePlaceholderTabPanel(string message)
        {
            Panel p = new Panel() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(13, 17, 23), Visible = false };
            Label lbl = new Label()
            {
                Text = message,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 10F, FontStyle.Regular)
            };
            p.Controls.Add(lbl);
            return p;
        }

        private void SwitchLogTab(int index)
        {
            activeLogTabIndex = index;
            tabActivity.IsActive = (index == 0);
            tabOutputLog.IsActive = (index == 1);
            tabBuildHistory.IsActive = (index == 2);
            tabBackups.IsActive = (index == 3);

            if (index == 1 && tabOutputLog.BadgeCount > 0)
            {
                tabOutputLog.BadgeCount = 0;
            }

            tabActivity.Invalidate();
            tabOutputLog.Invalidate();
            tabBuildHistory.Invalidate();
            tabBackups.Invalidate();

            pnlActivityView.Visible = (index == 0);
            outputLogPanel.Visible = (index == 1);
            pnlBuildHistoryView.Visible = (index == 2);
            pnlBackupsView.Visible = (index == 3);

            lblOutputFooterMeta.Visible = (index == 1);
            PositionFooterMeta();

            if (index == 1)
            {
                outputLogPanel.TriggerTailCheck();
            }
        }

        private void PositionFooterMeta()
        {
            if (lblOutputFooterMeta == null || lblFooterStatus == null || pnlLogFooter == null) return;
            int x = lblFooterStatus.Right + 16;
            lblOutputFooterMeta.Location = new Point(x, 7);
            int maxW = Math.Max(0, progressBar.Left - x - 12);
            lblOutputFooterMeta.MaximumSize = new Size(maxW, 20);
        }

        private void RelayoutSidebar()
        {
            pnlSidebar.SuspendLayout();
            int y = 10;

            pnlConfigGroup.Location = new Point(10, y);
            y += pnlConfigGroup.Height + 8;

            btnTogglePaths.Location = new Point(10, y);
            btnTogglePaths.Text = isPathsExpanded ? "▼  Paths && settings" : "▶  Paths && settings";
            y += btnTogglePaths.Height + 4;

            if (isPathsExpanded)
            {
                pnlPathsContent.Visible = true;
                pnlPathsContent.Location = new Point(10, y);
                y += pnlPathsContent.Height + 8;
            }
            else
            {
                pnlPathsContent.Visible = false;
                y += 4;
            }

            btnToggleBuild.Location = new Point(10, y);
            btnToggleBuild.Text = isBuildExpanded ? "▼  BUILD && DEPLOY" : "▶  BUILD && DEPLOY";
            y += btnToggleBuild.Height + 4;

            if (isBuildExpanded)
            {
                pnlBuildContent.Visible = true;
                pnlBuildContent.Location = new Point(10, y);
                y += pnlBuildContent.Height + 8;
            }
            else
            {
                pnlBuildContent.Visible = false;
                y += 4;
            }

            btnToggleProcess.Location = new Point(10, y);
            btnToggleProcess.Text = isProcessExpanded ? "▼  POS CONTROL" : "▶  POS CONTROL";
            y += btnToggleProcess.Height + 4;

            if (isProcessExpanded)
            {
                pnlProcessContent.Visible = true;
                pnlProcessContent.Location = new Point(10, y);
                y += pnlProcessContent.Height + 8;
            }
            else
            {
                pnlProcessContent.Visible = false;
                y += 4;
            }

            btnToggleTools.Location = new Point(10, y);
            btnToggleTools.Text = isToolsExpanded ? "▼  TOOLS" : "▶  TOOLS";
            y += btnToggleTools.Height + 4;

            if (isToolsExpanded)
            {
                pnlToolsContent.Visible = true;
                pnlToolsContent.Location = new Point(10, y);
                y += pnlToolsContent.Height + 10;
            }
            else
            {
                pnlToolsContent.Visible = false;
                y += 4;
            }

            pnlSidebar.ResumeLayout(true);
        }

        private void RelayoutLogToolbar()
        {
            pnlLogToolbar.SuspendLayout();
            int right = pnlLogToolbar.ClientSize.Width - 8;

            btnClear.Location = new Point(right - btnClear.Width, 6);
            right -= btnClear.Width + 4;

            btnSave.Location = new Point(right - btnSave.Width, 6);
            right -= btnSave.Width + 4;

            btnCopy.Location = new Point(right - btnCopy.Width, 6);
            right -= btnCopy.Width + 4;

            btnWrap.Location = new Point(right - btnWrap.Width, 6);
            right -= btnWrap.Width + 4;

            btnAutoScroll.Location = new Point(right - btnAutoScroll.Width, 6);
            right -= btnAutoScroll.Width + 8;

            int searchX = btnFilterInfo.Right + 8;
            int searchW = Math.Max(100, Math.Min(220, right - searchX));
            searchBox.Location = new Point(searchX, 6);
            searchBox.Size = new Size(searchW, 26);

            pnlLogToolbar.ResumeLayout(true);
        }

        private void SetActiveFilter(FilterMode filter)
        {
            btnFilterAll.IsActive = (filter == FilterMode.All);
            btnFilterErrors.IsActive = (filter == FilterMode.Errors);
            btnFilterWarnings.IsActive = (filter == FilterMode.Warnings);
            btnFilterInfo.IsActive = (filter == FilterMode.Info);

            btnFilterAll.Invalidate();
            btnFilterErrors.Invalidate();
            btnFilterWarnings.Invalidate();
            btnFilterInfo.Invalidate();

            logViewer.SetFilter(filter);
        }

        private void UpdateFilterChipCounts()
        {
            btnFilterAll.Text = string.Format("All ({0})", logViewer.TotalCount);
            btnFilterErrors.Text = string.Format("Errors ({0})", logViewer.ErrorCount);
            btnFilterWarnings.Text = string.Format("Warnings ({0})", logViewer.WarningCount);
            btnFilterInfo.Text = string.Format("Info ({0})", logViewer.InfoCount);

            btnFilterAll.Invalidate();
            btnFilterErrors.Invalidate();
            btnFilterWarnings.Invalidate();
            btnFilterInfo.Invalidate();
        }

        private void UpdateAutoScrollButtonAppearance()
        {
            if (!logViewer.AutoScrollEnabled)
            {
                btnAutoScroll.Text = "⬇ Auto-scroll: OFF";
                btnAutoScroll.NormalTextColor = Color.FromArgb(156, 163, 175);
                btnAutoScroll.BorderColor = Color.FromArgb(55, 65, 81);
            }
            else if (logViewer.IsAutoScrollPaused)
            {
                btnAutoScroll.Text = "⏸ Paused";
                btnAutoScroll.NormalTextColor = Color.FromArgb(251, 191, 36);
                btnAutoScroll.BorderColor = Color.FromArgb(217, 119, 6);
            }
            else
            {
                btnAutoScroll.Text = "⬇ Auto-scroll: ON";
                btnAutoScroll.NormalTextColor = Color.FromArgb(52, 211, 153);
                btnAutoScroll.BorderColor = Color.FromArgb(55, 65, 81);
            }
            btnAutoScroll.Invalidate();
        }

        #endregion

        #region Path Browsing & Config Persistence

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
            chkSwitchToOutputAfterDeploy.Checked = currentConfig.SwitchToOutputLogAfterDeploy;
        }

        private void SaveConfigFromControls(bool showMessage)
        {
            currentConfig.SourceCodeRoot = txtSourceRoot.Text.Trim();
            currentConfig.InstallDir = txtInstallDir.Text.Trim();
            currentConfig.MSBuildPath = txtMSBuild.Text.Trim();
            currentConfig.Configuration = cmbConfig.SelectedItem != null ? cmbConfig.SelectedItem.ToString() : "Release";
            currentConfig.Platform = cmbPlatform.SelectedItem != null ? cmbPlatform.SelectedItem.ToString() : "Win32";
            currentConfig.AutoRestart = chkAutoRestart.Checked;
            currentConfig.SwitchToOutputLogAfterDeploy = chkSwitchToOutputAfterDeploy.Checked;
            currentConfig.Theme = "Dark";
            currentConfig.Save();
            if (outputLogPanel != null)
            {
                outputLogPanel.SetInstallDirectory(currentConfig.InstallDir);
            }
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
            lblFooterStatus.Text = statusText;
            progressBar.IsMarquee = busy;
            progressBar.Value = busy ? 0 : (statusText.Contains("Failed") ? 0 : 100);
        }

        #endregion

        #region Grand POS Process & Diagnostics

        private void UpdatePosStatus()
        {
            if (this.IsDisposed || statusPill.IsDisposed) return;
            try
            {
                Process[] procs = Process.GetProcessesByName("GrandPOS");
                if (procs.Length > 0)
                {
                    statusPill.SetStatus(true, procs[0].Id);
                }
                else
                {
                    statusPill.SetStatus(false, 0);
                }
            }
            catch { }
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

        private void CleanBuildPlaceholder()
        {
            LogStep("Clean build requested...");
            LogInfo("Clean build placeholder triggered. Intermediate output files can be purged before rebuild.");
            LogSuccess("Workspace clean check completed.");
        }

        private void OpenInstallDir()
        {
            string installDir = txtInstallDir.Text.Trim();
            if (string.IsNullOrEmpty(installDir)) installDir = currentConfig.InstallDir;

            if (Directory.Exists(installDir))
            {
                Process.Start("explorer.exe", installDir);
                LogInfo("Opened Grand POS Install Directory in Explorer: " + installDir);
            }
            else
            {
                MessageBox.Show(this, "Install directory not found:\n" + installDir, "Folder Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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

        #endregion

        #region Logging Methods

        private static readonly object logFileLock = new object();
        private string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GrandPOS_Deployer.log");

        private void AppendLogInternal(string text, LogLevel level)
        {
            if (level == LogLevel.Info)
            {
                if (Regex.IsMatch(text, @":\s*(fatal\s+)?error\s+[A-Za-z0-9_]*\s*:", RegexOptions.IgnoreCase) || text.Contains("[ERROR]"))
                {
                    level = LogLevel.Error;
                }
                else if (Regex.IsMatch(text, @":\s*warning\s+[A-Za-z0-9_]*\s*:", RegexOptions.IgnoreCase) || text.Contains("[WARNING]"))
                {
                    level = LogLevel.Warning;
                }
            }

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
                catch { }
            }

            logViewer.EnqueueLine(text, level);
        }

        public void LogStep(string msg) { AppendLogInternal("[STEP] " + msg, LogLevel.Step); }
        public void LogSuccess(string msg) { AppendLogInternal("[SUCCESS] " + msg, LogLevel.Success); }
        public void LogWarning(string msg) { AppendLogInternal("[WARNING] " + msg, LogLevel.Warning); }
        public void LogError(string msg) { AppendLogInternal("[ERROR] " + msg, LogLevel.Error); }
        public void LogInfo(string msg) { AppendLogInternal(msg, LogLevel.Info); }

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

                    if (mode == "Rollback")
                    {
                        DoRollback(installDir, autoRestart);
                        return;
                    }

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
                            itemsToDeploy.Add(new DeploymentItem
                            {
                                Name = dll,
                                Source = Path.Combine(dllBuildDir, dll),
                                Destination = Path.Combine(installDir, dll),
                                Solution = dllSolutionPath
                            });
                        }
                    }

                    // Step 1: Build Solutions
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
                SetBusy(false, "Completed in " + sw.Elapsed.TotalSeconds.ToString("0.0") + "s");

                if (mode != "Rollback")
                {
                    DeployMarker marker = new DeployMarker
                    {
                        Timestamp = DateTime.Now,
                        Mode = mode == "DLLsOnly" ? "Pricing DLL" : (mode == "ExeOnly" ? "EXE" : "ALL"),
                        Configuration = config,
                        Platform = platform
                    };
                    if (currentConfig.DeployHistory == null) currentConfig.DeployHistory = new List<DeployMarker>();
                    currentConfig.DeployHistory.Add(marker);
                    currentConfig.Save();
                    outputLogPanel.NotifyDeployCompleted(marker, autoRestart);
                    if (autoRestart && currentConfig.SwitchToOutputLogAfterDeploy)
                    {
                        SwitchLogTab(1);
                    }
                }
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
                catch { }
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
}
