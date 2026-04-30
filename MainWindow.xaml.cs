using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using System.Windows;

namespace StandKnifeLauncher
{
    public partial class MainWindow : Window
    {
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint timeBeginPeriod(uint uMilliseconds);
        private string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StandKnifeLauncher", "settings.json");

        public MainWindow()
        {
            InitializeComponent();
            
            ComboFpsLimit.PreviewTextInput += (s, e) =>
            {
                e.Handled = !char.IsDigit(e.Text[0]);
            };
            
            this.Loaded += (s, e) =>
            {
                LoadSettings();
                InitializeCoreSelection();
            };
        }

        private void InitializeCoreSelection()
        {
            if (CoresWrapPanel == null) return;

            int totalCores = Environment.ProcessorCount;
            CoresWrapPanel.Children.Clear();

            for (int i = 0; i < totalCores; i++)
            {
                var checkBox = new System.Windows.Controls.CheckBox
                {
                    Content = i.ToString(),
                    Margin = new System.Windows.Thickness(10, 8, 10, 8),
                    MinWidth = 44,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    FontSize = 13,
                    IsChecked = customCoreSelection.Contains(i)
                };

                int coreIndex = i;
                checkBox.Checked += (s, e) =>
                {
                    if (!customCoreSelection.Contains(coreIndex))
                        customCoreSelection.Add(coreIndex);
                };
                checkBox.Unchecked += (s, e) =>
                {
                    customCoreSelection.Remove(coreIndex);
                };

                CoresWrapPanel.Children.Add(checkBox);
            }
        }

        private void ComboCpu_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ComboCpu != null && ComboCpu.SelectedIndex >= 0 && PanelCoreSelection != null)
            {
                PanelCoreSelection.Visibility = ComboCpu.SelectedIndex == 2 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }


        private void BtnFastStart_Click(object sender, RoutedEventArgs e) => LaunchGame("-force-d3d11 -single-instance -nolog -screen-quality 0", false);
        private void BtnMaxFps_Click(object sender, RoutedEventArgs e) => LaunchGame("-force-d3d11 -single-instance -nolog -screen-quality 0", true);
        private void BtnDx12_Click(object sender, RoutedEventArgs e) => LaunchGame("-force-d3d12", false);
        private void BtnDx11_Click(object sender, RoutedEventArgs e) => LaunchGame("-force-d3d11", false);
        private void BtnLaunch_Click(object sender, RoutedEventArgs e) => LaunchGame(BuildArguments(), false);
        private void BtnLaunchQuick_Click(object sender, RoutedEventArgs e) => LaunchGame(BuildArguments(), false);

        private string BuildArguments()
        {
            var args = new System.Text.StringBuilder();
            switch (ComboApi.SelectedIndex)
            {
                case 0:
                    args.Append("-force-d3d11 ");
                    break;
                case 1:
                    args.Append("-force-d3d12 ");
                    break;
            }
            if (ComboResolution.SelectedIndex == 1) args.Append("-screen-fullscreen 0 -screen-width 1280 -screen-height 720 ");
            else if (ComboResolution.SelectedIndex == 0) args.Append("-screen-fullscreen 0 -screen-width 1920 -screen-height 1080 ");
            args.Append($"-screen-quality {ComboQuality.SelectedIndex} ");

            if (ComboVSync.SelectedIndex == 1) args.Append("-vsync 0 ");
            else args.Append("-vsync 1 ");

            var fpsText = ComboFpsLimit.Text?.Trim();
            if (!string.IsNullOrEmpty(fpsText) && fpsText != "Без ограничений")
            {
                var fpsNumber = fpsText.Replace(" FPS", "").Trim();
                if (int.TryParse(fpsNumber, out int fps) && fps > 0)
                {
                    args.Append($"-fps {fps} ");
                }
            }

            switch (ComboWindowMode.SelectedIndex)
            {
                case 1:
                    args.Append("-window-mode windowed ");
                    break;
                case 2:
                    args.Append("-window-mode borderless ");
                    break;
                case 3:
                    args.Append("-window-mode exclusive ");
                    break;
                default:
                    args.Append("-window-mode exclusive ");
                    break;
            }

            if (CheckNoLog.IsChecked == true) args.Append("-nolog ");
            if (CheckSingleInstance.IsChecked == true) args.Append("-single-instance ");
            if (CheckNoGfxJobs.IsChecked == true) args.Append("-no-gfx-jobs ");
            if (CheckGcIncremental.IsChecked == true) args.Append("-gc-incremental ");
            if (CheckNoVR.IsChecked == true) args.Append("-vrmode none ");
            if (CheckDirectRender.IsChecked == true) args.Append("-force-gfx-direct ");
            if (CheckForceDiscreteGPU.IsChecked == true) args.Append("-adapter 1 ");
            if (CheckThreadedGPU.IsChecked == true) args.Append("-force-d3d11-no-singlethreaded ");
            return args.ToString().Trim();
        }

        private void ApplyWindowsCompatibilityFlags()
        {
            try
            {
                string gamePath = TextGamePath.Text;
                if (string.IsNullOrEmpty(gamePath) || !File.Exists(gamePath))
                    return;

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"))
                {
                    if (key == null) return;

                    var flags = new System.Text.StringBuilder();
                    
                    if (CheckDisableFSO.IsChecked == true)
                        flags.Append(" DISABLEDXMAXIMIZEDWINDOWEDMODE");
                    
                    if (CheckDisableDPI.IsChecked == true)
                        flags.Append(" HIGHDPIAWARE");
                    
                    if (flags.Length > 0)
                    {
                        key.SetValue(gamePath, "~" + flags.ToString());
                    }
                    else
                    {
                        if (key.GetValue(gamePath) != null)
                            key.DeleteValue(gamePath);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка реестра: {ex.Message}");
            }
        }

        private async void LaunchGame(string arguments, bool highPriority, bool? forcePhysicalCores = null)
        {
            try
            {
                if (!File.Exists(TextGamePath.Text)) { ShowDialog("Ошибка", "StandKnife.exe не найден!"); return; }
                
                ApplyWindowsCompatibilityFlags();
                
                if (CheckHighTimer.IsChecked == true)
                    timeBeginPeriod(1);
                
                if (CheckThreadedGPU.IsChecked == true)
                    Environment.SetEnvironmentVariable("__GL_THREADED_OPTIMIZATIONS", "1", EnvironmentVariableTarget.Process);
                
                await ApplyPowerPlanAsync(RadioLaptop.IsChecked == true);
                var psi = new ProcessStartInfo { FileName = TextGamePath.Text, Arguments = arguments, WorkingDirectory = Path.GetDirectoryName(TextGamePath.Text) ?? "", UseShellExecute = false };
                var process = Process.Start(psi);
                if (process == null) return;

                int cpuMode = forcePhysicalCores.HasValue ? (forcePhysicalCores.Value ? 1 : 0) : ComboCpu.SelectedIndex;

                if (cpuMode == 1)
                {
                    process.ProcessorAffinity = GetPhysicalCoresAffinity();
                }
                else if (cpuMode == 2)
                {
                    process.ProcessorAffinity = GetCustomAffinity();
                }

                if (highPriority)
                {
                    process.PriorityClass = ProcessPriorityClass.High;
                }
                else
                {
                    switch (ComboPriority.SelectedIndex)
                    {
                        case 1:
                            process.PriorityClass = ProcessPriorityClass.High;
                            break;
                        case 2:
                            process.PriorityClass = ProcessPriorityClass.RealTime;
                            break;
                    }
                }
            }
            catch (Exception ex) { ShowDialog("Ошибка", ex.Message); }
        }

        private IntPtr GetPhysicalCoresAffinity()
        {
            try
            {
                int physicalCores = 0;
                using (var searcher = new ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        physicalCores = Convert.ToInt32(obj["NumberOfCores"]);
                        break;
                    }
                }

                if (physicalCores <= 0) return new IntPtr(-1);

                IntPtr affinity = IntPtr.Zero;
                for (int i = 0; i < physicalCores; i++)
                {
                    affinity = new IntPtr(affinity.ToInt64() | (1L << (i * 2)));
                }

                return affinity;
            }
            catch
            {
                return new IntPtr(-1);
            }
        }

        private IntPtr GetCustomAffinity()
        {
            try
            {
                IntPtr affinity = IntPtr.Zero;
                int totalCores = Environment.ProcessorCount;

                for (int i = 0; i < totalCores; i++)
                {
                    if (IsCoreEnabled(i))
                    {
                        affinity = new IntPtr(affinity.ToInt64() | (1L << i));
                    }
                }

                return affinity;
            }
            catch
            {
                return new IntPtr(-1);
            }
        }

        private bool IsCoreEnabled(int coreIndex)
        {
            return customCoreSelection.Contains(coreIndex);
        }

        private System.Collections.Generic.List<int> customCoreSelection = new System.Collections.Generic.List<int>();

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
                var s = new LauncherSettings
                {
                    GamePath = TextGamePath.Text,
                    ApiIndex = ComboApi.SelectedIndex,
                    ResolutionIndex = ComboResolution.SelectedIndex,
                    QualityIndex = ComboQuality.SelectedIndex,
                    CpuIndex = ComboCpu.SelectedIndex,
                    PriorityIndex = ComboPriority.SelectedIndex,
                    VSyncIndex = ComboVSync.SelectedIndex,
                    FpsLimitText = ComboFpsLimit.Text?.Trim() ?? "Без ограничений",
                    WindowModeIndex = ComboWindowMode.SelectedIndex,
                    NoLog = CheckNoLog.IsChecked ?? true,
                    SingleInstance = CheckSingleInstance.IsChecked ?? true,
                    NoGfxJobs = CheckNoGfxJobs.IsChecked ?? false,
                    NoVR = CheckNoVR.IsChecked ?? true,
                    DirectRender = CheckDirectRender.IsChecked ?? false,
                    ForceDiscreteGPU = CheckForceDiscreteGPU.IsChecked ?? false,
                    DisableFSO = CheckDisableFSO.IsChecked ?? true,
                    DisableDPI = CheckDisableDPI.IsChecked ?? true,
                    HighTimer = CheckHighTimer.IsChecked ?? true,
                    ThreadedGPU = CheckThreadedGPU.IsChecked ?? true,
                    IsLaptopMode = RadioLaptop.IsChecked ?? false,
                    UseCustomPower = CheckCustomPower.IsChecked ?? true,
                    CustomCores = customCoreSelection,
                    GcIncremental = CheckGcIncremental.IsChecked ?? false
                };
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
                ComboApi.SelectedIndex = s.ApiIndex;
                ComboResolution.SelectedIndex = s.ResolutionIndex;
                ComboQuality.SelectedIndex = s.QualityIndex;
                ComboCpu.SelectedIndex = s.CpuIndex;
                ComboPriority.SelectedIndex = s.PriorityIndex;
                ComboVSync.SelectedIndex = s.VSyncIndex;
                if (!string.IsNullOrEmpty(s.FpsLimitText) && s.FpsLimitText != "Без ограничений")
                {
                    ComboFpsLimit.Text = s.FpsLimitText;
                }
                else
                {
                    ComboFpsLimit.SelectedIndex = 0;
                }
                ComboWindowMode.SelectedIndex = s.WindowModeIndex;
                CheckNoLog.IsChecked = s.NoLog;
                CheckSingleInstance.IsChecked = s.SingleInstance;
                CheckNoGfxJobs.IsChecked = s.NoGfxJobs;
                CheckNoVR.IsChecked = s.NoVR;
                CheckDirectRender.IsChecked = s.DirectRender;
                CheckForceDiscreteGPU.IsChecked = s.ForceDiscreteGPU;
                CheckDisableFSO.IsChecked = s.DisableFSO;
                CheckDisableDPI.IsChecked = s.DisableDPI;
                CheckHighTimer.IsChecked = s.HighTimer;
                CheckThreadedGPU.IsChecked = s.ThreadedGPU;
                CheckCustomPower.IsChecked = s.UseCustomPower;
                CheckGcIncremental.IsChecked = s.GcIncremental;
                if (s.IsLaptopMode) RadioLaptop.IsChecked = true; else RadioPC.IsChecked = true;

                if (s.CustomCores != null && s.CustomCores.Count > 0)
                {
                    customCoreSelection = s.CustomCores;
                }
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
        public int PriorityIndex { get; set; } = 1;
        public int VSyncIndex { get; set; } = 0;
        public string FpsLimitText { get; set; } = "Без ограничений";
        public int WindowModeIndex { get; set; } = 0;
        public bool NoLog { get; set; } = true;
        public bool SingleInstance { get; set; } = true;
        public bool NoGfxJobs { get; set; } = false;
        public bool NoVR { get; set; } = true;
        public bool DirectRender { get; set; } = false;
        public bool ForceDiscreteGPU { get; set; } = false;
        public bool DisableFSO { get; set; } = true;
        public bool DisableDPI { get; set; } = true;
        public bool HighTimer { get; set; } = true;
        public bool ThreadedGPU { get; set; } = true;
        public bool IsLaptopMode { get; set; } = false;
        public bool UseCustomPower { get; set; } = true;
        public System.Collections.Generic.List<int> CustomCores { get; set; } = new System.Collections.Generic.List<int>();
        public bool GcIncremental { get; set; } = false;
    }
}
