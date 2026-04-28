using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using System.Windows;

namespace StandKnifeLauncher
{
    public partial class MainWindow : Window
    {
        private string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StandKnifeLauncher", "settings.json");

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void BtnFastStart_Click(object sender, RoutedEventArgs e) => LaunchGame("-force-d3d12 -single-instance -nolog -screen-quality 0", true, false);
        private void BtnMaxFps_Click(object sender, RoutedEventArgs e) => LaunchGame("-force-d3d12 -single-instance -nolog -screen-quality 0", true, true);
        private void BtnDx12_Click(object sender, RoutedEventArgs e) => LaunchGame("-force-d3d12", false, false);
        private void BtnDx11_Click(object sender, RoutedEventArgs e) => LaunchGame("-force-d3d11", false, false);
        private void BtnLaunch_Click(object sender, RoutedEventArgs e) => LaunchGame(BuildArguments(), CheckPriority.IsChecked ?? false, ComboCpu.SelectedIndex == 1);
        private void BtnLaunchQuick_Click(object sender, RoutedEventArgs e) => LaunchGame(BuildArguments(), CheckPriority.IsChecked ?? false, ComboCpu.SelectedIndex == 1);

        private string BuildArguments()
        {
            var args = new System.Text.StringBuilder();
            switch (ComboApi.SelectedIndex)
            {
                case 0:
                    args.Append("-force-d3d12 ");
                    break;
                case 1:
                    args.Append("-force-d3d11 ");
                    break;
            }
            if (ComboResolution.SelectedIndex == 1) args.Append("-screen-fullscreen 0 -screen-width 1280 -screen-height 720 ");
            else if (ComboResolution.SelectedIndex == 0) args.Append("-screen-fullscreen 0 -screen-width 1920 -screen-height 1080 ");
            args.Append($"-screen-quality {ComboQuality.SelectedIndex} ");
            if (CheckNoLog.IsChecked == true) args.Append("-nolog ");
            if (CheckSingleInstance.IsChecked == true) args.Append("-single-instance ");
            return args.ToString().Trim();
        }

        private async void LaunchGame(string arguments, bool highPriority, bool physicalCoresOnly)
        {
            try
            {
                if (!File.Exists(TextGamePath.Text)) { ShowDialog("Ошибка", "StandKnife.exe не найден!"); return; }
                await ApplyPowerPlanAsync(RadioLaptop.IsChecked == true);
                var psi = new ProcessStartInfo { FileName = TextGamePath.Text, Arguments = arguments, WorkingDirectory = Path.GetDirectoryName(TextGamePath.Text) ?? "", UseShellExecute = true };
                var process = Process.Start(psi);
                if (process == null) return;

                if (highPriority) process.PriorityClass = ProcessPriorityClass.High;
                if (physicalCoresOnly)
                {
                    foreach (ProcessThread thread in process.Threads)
                    {
                        thread.IdealProcessor = 0;
                    }
                }
            }
            catch (Exception ex) { ShowDialog("Ошибка", ex.Message); }
        }

        private async Task ApplyPowerPlanAsync(bool isLaptop)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (CheckCustomPower.IsChecked == true)
                    {
                        string planName = isLaptop ? "StandKnife Laptop" : "StandKnife PC";
                        string list = RunPowercfg("-list");
                        string guid = GetGuidByName(list, planName);

                        if (!string.IsNullOrEmpty(guid))
                        {
                            RunPowercfg($"-setactive {guid}");
                        }
                        else
                        {
                            string tempPow = Path.Combine(Path.GetTempPath(), "StandKnifePC.pow");
                            using (Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("StandKnifeLauncher.StandKnifePC.pow"))
                            {
                                if (s != null) using (FileStream fs = new FileStream(tempPow, FileMode.Create)) s.CopyTo(fs);
                            }

                            string tempBat = Path.Combine(Path.GetTempPath(), "apply.bat");
                            File.WriteAllText(tempBat, $"@echo off\r\nfor /f \"tokens=2 delims=:\" %%a in ('powercfg -import \"{tempPow}\"') do set newGuid=%%a\r\npowercfg -changename %newGuid% \"{planName}\"\r\npowercfg -setactive %newGuid%");
                            Process p = new Process();
                            p.StartInfo.FileName = tempBat;
                            p.StartInfo.Verb = "runas";
                            p.StartInfo.UseShellExecute = true;
                            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                            p.Start();
                            p.WaitForExit();

                            if (File.Exists(tempBat)) File.Delete(tempBat);
                            if (File.Exists(tempPow)) File.Delete(tempPow);
                        }
                    }
                    else
                    {
                        RunPowercfg("-setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
                    }
                }
                catch { }
            });
        }

        private void BtnDeletePlan_Click(object sender, RoutedEventArgs e)
        {
            string list = RunPowercfg("-list");
            string guid = GetGuidByName(list, "StandKnifePC");

            if (!string.IsNullOrEmpty(guid))
            {
                string tempBat = Path.Combine(Path.GetTempPath(), "delete_plan.bat");
                File.WriteAllText(tempBat, $"@echo off\r\npowercfg -setactive 381b4222-f694-41f0-9685-ff5bb260df2e\r\npowercfg -delete {guid}");
                Process.Start(new ProcessStartInfo { FileName = tempBat, Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden })?.WaitForExit();
                File.Delete(tempBat);
                ShowSnackbar("План питания удален.");
            }
        }

        private string GetGuidByName(string list, string name)
        {
            var lines = list.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains(name))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}");
                    if (match.Success) return match.Value;
                }
            }
            return null;
        }

        private string RunPowercfg(string args)
        {
            Process p = Process.Start(new ProcessStartInfo { FileName = "powercfg.exe", Arguments = args, CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true });
            string result = "";
            if (p != null)
            {
                result = p.StandardOutput.ReadToEnd();
                p.Dispose();
            }
            return result;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e) => SaveSettings();
        private void SaveSettings()
        {
            try
            {
                var s = new LauncherSettings { GamePath = TextGamePath.Text, ApiIndex = ComboApi.SelectedIndex, ResolutionIndex = ComboResolution.SelectedIndex, QualityIndex = ComboQuality.SelectedIndex, CpuIndex = ComboCpu.SelectedIndex, HighPriority = CheckPriority.IsChecked ?? true, NoLog = CheckNoLog.IsChecked ?? true, SingleInstance = CheckSingleInstance.IsChecked ?? true, IsLaptopMode = RadioLaptop.IsChecked ?? false, UseCustomPower = CheckCustomPower.IsChecked ?? true };
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(s, Formatting.Indented));
                ShowSnackbar("Настройки сохранены!");
            }
            catch (Exception ex) { ShowSnackbar($"Ошибка: {ex.Message}"); }
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                var s = JsonConvert.DeserializeObject<LauncherSettings>(File.ReadAllText(SettingsPath));
                if (s == null) return;
                if (File.Exists(s.GamePath)) TextGamePath.Text = s.GamePath;
                ComboApi.SelectedIndex = s.ApiIndex; ComboResolution.SelectedIndex = s.ResolutionIndex;
                ComboQuality.SelectedIndex = s.QualityIndex; ComboCpu.SelectedIndex = s.CpuIndex;
                CheckPriority.IsChecked = s.HighPriority; CheckNoLog.IsChecked = s.NoLog;
                CheckSingleInstance.IsChecked = s.SingleInstance;
                CheckCustomPower.IsChecked = s.UseCustomPower;
                if (s.IsLaptopMode) RadioLaptop.IsChecked = true; else RadioPC.IsChecked = true;
            }
            catch { }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var d = new OpenFileDialog { Filter = "Exe (*.exe)|*.exe|All (*.*)|*.*" };
            if (d.ShowDialog() == true) TextGamePath.Text = d.FileName;
        }

        private void ShowDialog(string t, string m) => MessageBox.Show(m, $"StandKnife - {t}", MessageBoxButton.OK, MessageBoxImage.Information);
        private void ShowSnackbar(string m) => MessageBox.Show(m, "StandKnife", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public class LauncherSettings
    {
        public string GamePath { get; set; } = "";
        public int ApiIndex { get; set; } = 0;
        public int ResolutionIndex { get; set; } = 0;
        public int QualityIndex { get; set; } = 3;
        public int CpuIndex { get; set; } = 0;
        public bool HighPriority { get; set; } = true;
        public bool NoLog { get; set; } = true;
        public bool SingleInstance { get; set; } = true;
        public bool IsLaptopMode { get; set; } = false;
        public bool UseCustomPower { get; set; } = true;
    }
}
