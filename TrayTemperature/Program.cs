// TrayTemperature Optimized Version with Localization and Autostart
// Requires C# 7.3 or higher
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using LibreHardwareMonitor.Hardware;

namespace TrayTemperature
{
    static class Program
    {
        static readonly Computer computer = new Computer() { IsCpuEnabled = true, IsGpuEnabled = true };
        static IHardware selectedGpu;
        static string cpuName = "", gpuName = "";
        static int cpuTemp = 0, gpuTemp = 0, cpuTempMax = 0, gpuTempMax = 0, cpuTempMin = 99999, gpuTempMin = 99999;
        static ulong cpuTempSum = 0, gpuTempSum = 0, sampleCount = 0;
        static bool isLogging = false;
        static Timer updateTimer;
        static NotifyIcon notifyIcon;
        static ContextMenu contextMenu;
        static StreamWriter logWriter;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            InitializeDefaultSettings();

            computer.Open();
            InitializeTimer();
            InitializeContextMenu();
            InitializeNotifyIcon();

            SelectDefaultGpu();
            UpdateTemperatures();
            Application.Run();

            // Cleanup resources
            DynamicIcon.Dispose();
            computer?.Close();
            Properties.Settings.Default.Save();
            notifyIcon.Visible = false;
        }

        static void InitializeDefaultSettings()
        {
            Properties.Settings.Default.Upgrade();

            // Set default values if not configured
            if (Properties.Settings.Default.CPUTempHigh == 0)
            {

                Properties.Settings.Default.Celsius = true;
                Properties.Settings.Default.Refresh = 5;
                Properties.Settings.Default.Save();
            }
        }

        static void InitializeTimer()
        {
            updateTimer = new Timer
            {
                Interval = Properties.Settings.Default.Refresh * 1000,
                Enabled = true
            };
            updateTimer.Tick += UpdateTemperatures;
        }

        static void InitializeContextMenu()
        {
            contextMenu = new ContextMenu();
            RebuildContextMenu();
        }

        static void RebuildContextMenu()
        {
            contextMenu.MenuItems.Clear();

            contextMenu.MenuItems.AddRange(new MenuItem[] {
                new MenuItem("TrayTemperature") { Enabled = false },
                new MenuItem("-"),
                new MenuItem(Localization.GetString("Celsius")) { Name = "celsius", Checked = Properties.Settings.Default.Celsius },
                new MenuItem(Localization.GetString("Fahrenheit")) { Name = "fahrenheit", Checked = !Properties.Settings.Default.Celsius },
                new MenuItem("-"),
                new MenuItem(Localization.GetString("SelectGPU")) { Name = "gpuSelect" },
                new MenuItem(Localization.GetString("Refresh")) { Name = "refresh" },
                new MenuItem(Localization.GetString("Log")) { Name = "log", Checked = isLogging },
                new MenuItem(Localization.GetString("ResetStats")) { Name = "reset" },
                new MenuItem("-"),
                new MenuItem(Localization.GetString("Language")) { Name = "language" },
                new MenuItem(Localization.GetString("Autostart")) { Name = "autostart", Checked = IsAutostartEnabled() },
                new MenuItem("-"),
                new MenuItem(Localization.GetString("Exit")) { Name = "exit" }
            });

            // Add refresh interval submenu
            var refreshMenu = contextMenu.MenuItems["refresh"];
            var refreshIntervals = new[] { 1, 2, 5, 10, 15, 30, 60 };
            foreach (var interval in refreshIntervals)
            {
                refreshMenu.MenuItems.Add(new MenuItem($"{interval}s")
                {
                    Name = interval.ToString(),
                    Checked = interval == Properties.Settings.Default.Refresh
                });
            }

            // Add language submenu
            var languageMenu = contextMenu.MenuItems["language"];
            languageMenu.MenuItems.Add(new MenuItem("English") { Name = "en", Checked = Localization.CurrentLanguage == "en" });
            languageMenu.MenuItems.Add(new MenuItem("Русский") { Name = "ru", Checked = Localization.CurrentLanguage == "ru" });

            // Attach event handlers
            foreach (MenuItem item in contextMenu.MenuItems)
                item.Click += OnMenuClick;
            foreach (MenuItem item in refreshMenu.MenuItems)
                item.Click += OnRefreshMenuClick;
            foreach (MenuItem item in languageMenu.MenuItems)
                item.Click += OnLanguageMenuClick;

            UpdateGpuSelectionMenu();
        }

        static void InitializeNotifyIcon()
        {
            notifyIcon = new NotifyIcon
            {
                Visible = true,
                ContextMenu = contextMenu
            };
        }

        static void SelectDefaultGpu()
        {
            var gpus = computer.Hardware.Where(h =>
                h.HardwareType == HardwareType.GpuAmd ||
                h.HardwareType == HardwareType.GpuNvidia ||
                h.HardwareType == HardwareType.GpuIntel).ToList();

            if (selectedGpu == null && gpus.Count > 0)
            {
                // Priority: NVIDIA > AMD > Intel
                selectedGpu = gpus.FirstOrDefault(g => g.HardwareType == HardwareType.GpuNvidia) ??
                             gpus.FirstOrDefault(g => g.HardwareType == HardwareType.GpuAmd) ??
                             gpus.FirstOrDefault(g => g.HardwareType == HardwareType.GpuIntel);

                if (selectedGpu != null)
                    gpuName = selectedGpu.Name;
            }
        }

        static void UpdateGpuSelectionMenu()
        {
            var gpuMenu = contextMenu.MenuItems["gpuSelect"];
            gpuMenu.MenuItems.Clear();

            var gpus = computer.Hardware.Where(h =>
                h.HardwareType == HardwareType.GpuAmd ||
                h.HardwareType == HardwareType.GpuNvidia ||
                h.HardwareType == HardwareType.GpuIntel).ToList();

            foreach (var gpu in gpus)
            {
                var item = new MenuItem(gpu.Name) { Tag = gpu, Checked = gpu == selectedGpu };
                item.Click += (s, e) => {
                    selectedGpu = (IHardware)item.Tag;
                    gpuName = selectedGpu.Name;
                    UpdateGpuSelectionMenu();
                };
                gpuMenu.MenuItems.Add(item);
            }
        }

        static void OnRefreshMenuClick(object sender, EventArgs e)
        {
            var clicked = (MenuItem)sender;
            foreach (MenuItem item in clicked.Parent.MenuItems)
                item.Checked = false;

            clicked.Checked = true;
            Properties.Settings.Default.Refresh = Convert.ToInt32(clicked.Name);
            updateTimer.Interval = Properties.Settings.Default.Refresh * 1000;
        }

        static void OnLanguageMenuClick(object sender, EventArgs e)
        {
            var clicked = (MenuItem)sender;
            foreach (MenuItem item in clicked.Parent.MenuItems)
                item.Checked = false;

            clicked.Checked = true;
            Localization.SetLanguage(clicked.Name);
            RebuildContextMenu();
        }

        static void OnMenuClick(object sender, EventArgs e)
        {
            var menuItem = (MenuItem)sender;
            switch (menuItem.Name)
            {
                case "celsius":
                    Properties.Settings.Default.Celsius = true;
                    RebuildContextMenu();
                    break;
                case "fahrenheit":
                    Properties.Settings.Default.Celsius = false;
                    RebuildContextMenu();
                    break;
                case "exit":
                    Application.Exit();
                    break;
                case "reset":
                    ResetStatistics();
                    break;
                case "log":
                    ToggleLogging();
                    break;
                case "autostart":
                    ToggleAutostart();
                    RebuildContextMenu();
                    break;
            }
            UpdateTemperatures();
        }

        static void ResetStatistics()
        {
            cpuTempSum = gpuTempSum = sampleCount = 0;
            cpuTempMax = gpuTempMax = 0;
            cpuTempMin = gpuTempMin = 99999;
        }

        static void ToggleLogging()
        {
            if (!isLogging)
            {
                if (MessageBox.Show(Localization.GetString("StartLogging"), Localization.GetString("Log"), MessageBoxButtons.YesNo) == DialogResult.No)
                    return;

                logWriter = new StreamWriter("temp.log", false);
                logWriter.WriteLine("DateTime,CPU,GPU");
                ResetStatistics();
                isLogging = true;
            }
            else
            {
                logWriter?.Close();

                // Create summary file
                var summary = $"CPU Avg: {(float)cpuTempSum / sampleCount:F2} Min: {cpuTempMin} Max: {cpuTempMax}\n" +
                             $"GPU Avg: {(float)gpuTempSum / sampleCount:F2} Min: {gpuTempMin} Max: {gpuTempMax}";

                File.WriteAllText("summary.log", summary + "\n" + File.ReadAllText("temp.log"));
                File.Delete("temp.log");
                isLogging = false;
            }
            RebuildContextMenu();
        }

        static void ToggleAutostart()
        {
            if (IsAutostartEnabled())
                DisableAutostart();
            else
                EnableAutostart();
        }

        static bool IsAutostartEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key?.GetValue("TrayTemperature") != null;
                }
            }
            catch
            {
                return false;
            }
        }

        static void EnableAutostart()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    key?.SetValue("TrayTemperature", Application.ExecutablePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Localization.GetString("AutostartError")}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static void DisableAutostart()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    key?.DeleteValue("TrayTemperature", false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Localization.GetString("AutostartError")}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static void UpdateTemperatures(object sender = null, EventArgs e = null)
        {
            ReadHardwareTemperatures();
            UpdateStatistics();

            if (isLogging && logWriter != null)
            {
                var convertedCpu = ConvertTemperature(cpuTemp);
                var convertedGpu = ConvertTemperature(gpuTemp);
                logWriter.WriteLine($"{DateTime.Now},{convertedCpu},{convertedGpu}");
            }

            var tooltip = GenerateTooltip();
            Fixes.SetNotifyIconText(notifyIcon, tooltip);

            var cpuColor = GetTemperatureColor(cpuTemp, true);
            var gpuColor = GetTemperatureColor(gpuTemp, false);
            var tempUnit = "°";

            notifyIcon.Icon = DynamicIcon.CreateIcon(
                ConvertTemperature(cpuTemp) + tempUnit, cpuColor,
                ConvertTemperature(gpuTemp) + tempUnit, gpuColor);
        }

        static void ReadHardwareTemperatures()
        {
            foreach (var hardware in computer.Hardware)
            {
                hardware.Update();

                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    var sensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
                    if (sensor != null) cpuTemp = (int)sensor.Value.GetValueOrDefault();
                    cpuName = hardware.Name;
                }
                else if (hardware == selectedGpu)
                {
                    var sensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
                    if (sensor != null) gpuTemp = (int)sensor.Value.GetValueOrDefault();
                    gpuName = hardware.Name;
                }
            }
        }

        static void UpdateStatistics()
        {
            // Store temperatures in Celsius for consistent calculations
            cpuTempSum += (ulong)cpuTemp;
            gpuTempSum += (ulong)gpuTemp;
            sampleCount++;

            cpuTempMax = Math.Max(cpuTempMax, cpuTemp);
            gpuTempMax = Math.Max(gpuTempMax, gpuTemp);
            cpuTempMin = Math.Min(cpuTempMin, cpuTemp);
            gpuTempMin = Math.Min(gpuTempMin, gpuTemp);
        }

        static Color GetTemperatureColor(int temperature, bool isCpu)
        {
            var settings = Properties.Settings.Default;

            if (isCpu)
            {
                if (temperature >= settings.CPUTempHigh)
                    return ColorTranslator.FromHtml(settings.CPUHigh);
                if (temperature >= settings.CPUTempMed)
                    return ColorTranslator.FromHtml(settings.CPUMed);
                return ColorTranslator.FromHtml(settings.CPULow);
            }
            else
            {
                if (temperature >= settings.GPUTempHigh)
                    return ColorTranslator.FromHtml(settings.GPUHigh);
                if (temperature >= settings.GPUTempMed)
                    return ColorTranslator.FromHtml(settings.GPUMed);
                return ColorTranslator.FromHtml(settings.GPULow);
            }
        }

        static int ConvertTemperature(int celsiusTemp)
        {
            return Properties.Settings.Default.Celsius ? celsiusTemp : (int)(celsiusTemp * 1.8 + 32);
        }

        static string GenerateTooltip()
        {
            var sb = new StringBuilder();
            var shortCpuName = GetCleanHardwareName(cpuName);
            var shortGpuName = GetCleanHardwareName(gpuName);

            var cpuAvg = (float)cpuTempSum / sampleCount;
            var gpuAvg = (float)gpuTempSum / sampleCount;

            // Convert averages and min/max for display
            var convertedCpuAvg = Properties.Settings.Default.Celsius ? cpuAvg : (cpuAvg * 1.8f + 32);
            var convertedGpuAvg = Properties.Settings.Default.Celsius ? gpuAvg : (gpuAvg * 1.8f + 32);

            var tempUnit = "°";

            sb.AppendLine(shortCpuName);
            sb.AppendLine($"  Cur: {ConvertTemperature(cpuTemp)}{tempUnit}");
            sb.AppendLine($"  Avg: {convertedCpuAvg:F2}{tempUnit}");
            sb.AppendLine($"  Min: {ConvertTemperature(cpuTempMin)}{tempUnit}");
            sb.AppendLine($"  Max: {ConvertTemperature(cpuTempMax)}{tempUnit}");
            sb.AppendLine(shortGpuName);
            sb.AppendLine($"  Cur: {ConvertTemperature(gpuTemp)}{tempUnit}");
            sb.AppendLine($"  Avg: {convertedGpuAvg:F2}{tempUnit}");
            sb.AppendLine($"  Min: {ConvertTemperature(gpuTempMin)}{tempUnit}");
            sb.AppendLine($"  Max: {ConvertTemperature(gpuTempMax)}{tempUnit}");

            var tooltip = sb.ToString();
            return tooltip.Length > 127 ? tooltip.Substring(0, 127) : tooltip;
        }

        static string GetCleanHardwareName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            var cleaned = name;
            var prefix = "";

            // Determine CPU prefix
            var lowerName = name.ToLower();
            if (lowerName.Contains("ryzen") || lowerName.Contains("amd"))
                prefix = "R";
            else if (lowerName.Contains("intel") || lowerName.Contains("core"))
                prefix = "I";

            // Remove manufacturer keywords
            var wordsToRemove = new[] { "Intel", "AMD", "NVIDIA", "GeForce", "Radeon", "Core", "Ryzen", "Processor" };
            foreach (var word in wordsToRemove)
                cleaned = cleaned.Replace(word, "");

            // Clean up whitespace
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();

            return string.IsNullOrEmpty(prefix) ? cleaned : prefix + cleaned;
        }
    }

    public static class Localization
    {
        public static string CurrentLanguage { get; private set; } = "en";

        private static readonly Dictionary<string, Dictionary<string, string>> Translations = new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["Celsius"] = "Celsius",
                ["Fahrenheit"] = "Fahrenheit",
                ["SelectGPU"] = "Select GPU",
                ["Refresh"] = "Refresh",
                ["Log"] = "Log",
                ["ResetStats"] = "Reset statistics",
                ["Language"] = "Language",
                ["Autostart"] = "Autostart",
                ["Exit"] = "Exit",
                ["StartLogging"] = "Start logging?",
                ["AutostartError"] = "Failed to change autostart setting"
            },
            ["ru"] = new Dictionary<string, string>
            {
                ["Celsius"] = "Цельсий",
                ["Fahrenheit"] = "Фаренгейт",
                ["SelectGPU"] = "Выбрать GPU",
                ["Refresh"] = "Обновление",
                ["Log"] = "Лог",
                ["ResetStats"] = "Сбросить статистику",
                ["Language"] = "Язык",
                ["Autostart"] = "Автозапуск",
                ["Exit"] = "Выход",
                ["StartLogging"] = "Начать логирование?",
                ["AutostartError"] = "Не удалось изменить настройку автозапуска"
            }
        };

        public static void SetLanguage(string language)
        {
            if (Translations.ContainsKey(language))
            {
                CurrentLanguage = language;
                Properties.Settings.Default.Language = language;
                Properties.Settings.Default.Save();
            }
        }

        public static string GetString(string key)
        {
            if (Translations.ContainsKey(CurrentLanguage) && Translations[CurrentLanguage].ContainsKey(key))
                return Translations[CurrentLanguage][key];

            // Fallback to English
            if (Translations.ContainsKey("en") && Translations["en"].ContainsKey(key))
                return Translations["en"][key];

            return key; // Return key if translation not found
        }

        static Localization()
        {
            // Load saved language preference
            var savedLanguage = Properties.Settings.Default.Language;
            if (!string.IsNullOrEmpty(savedLanguage) && Translations.ContainsKey(savedLanguage))
                CurrentLanguage = savedLanguage;
        }
    }

    public static class Fixes
    {
        // Workaround for NotifyIcon text length limitation
        public static void SetNotifyIconText(NotifyIcon notifyIcon, string text)
        {
            if (text.Length >= 128)
                throw new ArgumentOutOfRangeException(nameof(text), "Text limited to 127 characters");

            var type = typeof(NotifyIcon);
            const BindingFlags hiddenField = BindingFlags.NonPublic | BindingFlags.Instance;

            type.GetField("text", hiddenField).SetValue(notifyIcon, text);
            if ((bool)type.GetField("added", hiddenField).GetValue(notifyIcon))
                type.GetMethod("UpdateIcon", hiddenField).Invoke(notifyIcon, new object[] { true });
        }
    }
}