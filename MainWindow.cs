using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using Microsoft.Win32;

namespace Czyscik
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? cts;

        public MainWindow()
        {
            InitializeComponent();
            PopulateDefaultPaths();
            // Podpinamy event handlerzy programowo (XAML nie zawiera Click)
            btnStart.Click += BtnStart_Click;
            btnPreview.Click += BtnPreview_Click;
            btnCancel.Click += BtnCancel_Click;
            btnAdd.Click += BtnAdd_Click;
            btnPick.Click += BtnPick_Click;
            btnRemove.Click += BtnRemove_Click;
            btnDetails.Click += BtnDetails_Click;
            btnAutostart.Click += BtnAutostart_Click;
            btnRefreshLog.Click += BtnRefreshLog_Click;
        }

        private void BtnAutostart_Click(object? sender, RoutedEventArgs e)
        {
            var win = new Window { Title = "Autostart - programy", Owner = this, Width = 900, Height = 500 };
            var grid = new System.Windows.Controls.Grid();
            var list = new System.Windows.Controls.ListBox { Margin = new Thickness(8) };
            grid.Children.Add(list);

            try
            {
                // Registry HKCU\Run
                using (var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
                {
                    if (key != null)
                    {
                        foreach (var name in key.GetValueNames())
                        {
                            var val = key.GetValue(name)?.ToString() ?? "";
                            list.Items.Add($"[HKCU\\Run] {name} -> {val}");
                        }
                    }
                }

                // Registry HKLM\Run
                using (var key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
                {
                    if (key != null)
                    {
                        foreach (var name in key.GetValueNames())
                        {
                            var val = key.GetValue(name)?.ToString() ?? "";
                            list.Items.Add($"[HKLM\\Run] {name} -> {val}");
                        }
                    }
                }

                // Startup folders
                var userStartup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs", "Startup");
                var commonStartup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "Start Menu", "Programs", "Startup");
                if (Directory.Exists(userStartup))
                {
                    foreach (var f in Directory.EnumerateFileSystemEntries(userStartup)) list.Items.Add($"[User Startup] {f}");
                }
                if (Directory.Exists(commonStartup))
                {
                    foreach (var f in Directory.EnumerateFileSystemEntries(commonStartup)) list.Items.Add($"[All Users Startup] {f}");
                }
            }
            catch (Exception ex)
            {
                list.Items.Add("Błąd podczas odczytu autostartu: " + ex.Message);
            }

            win.Content = list;
            win.ShowDialog();
        }

        private void PopulateDefaultPaths()
        {
            try
            {
                var temp = Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath();
                if (!lstPaths.Items.Contains(temp)) lstPaths.Items.Add(temp);
                var winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                if (!lstPaths.Items.Contains(winTemp)) lstPaths.Items.Add(winTemp);
                // Auto-detect user profile folders (Downloads, Desktop)
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrWhiteSpace(userProfile))
                {
                    var downloads = Path.Combine(userProfile, "Downloads");
                    var desktop = Path.Combine(userProfile, "Desktop");
                    if (Directory.Exists(downloads) && !lstPaths.Items.Contains(downloads)) lstPaths.Items.Add(downloads);
                    if (Directory.Exists(desktop) && !lstPaths.Items.Contains(desktop)) lstPaths.Items.Add(desktop);
                }
                LoadLogToTextbox();
            }
            catch { }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e) => await StartCleaningAsync(false);
        private async void BtnPreview_Click(object sender, RoutedEventArgs e) => await StartCleaningAsync(true);
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
            lblStatus.Text = "Anulowano";
            btnCancel.IsEnabled = false;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var p = txtNewPath.Text?.Trim();
            if (!string.IsNullOrEmpty(p) && !lstPaths.Items.Contains(p)) lstPaths.Items.Add(p);
            txtNewPath.Clear();
        }

        private void BtnPick_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var dlg = new System.Windows.Forms.FolderBrowserDialog();
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var p = dlg.SelectedPath;
                    if (!lstPaths.Items.Contains(p)) lstPaths.Items.Add(p);
                }
            }
            catch (Exception ex) { Cleaner.Log("PICKERR|" + ex.Message); }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            var sel = lstPaths.SelectedItem;
            if (sel != null) lstPaths.Items.Remove(sel);
        }

        private void BtnDetails_Click(object sender, RoutedEventArgs e)
        {
            var sel = lstPaths.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(sel))
            {
                MessageBox.Show("Wybierz katalog z listy, aby zobaczyć pliki.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var preview = new Window { Title = "Podgląd plików", Owner = this, Width = 800, Height = 500 };
            var list = new System.Windows.Controls.ListBox();
            preview.Content = list;
            try
            {
                if (Directory.Exists(sel))
                {
                    foreach (var f in Directory.EnumerateFiles(sel, "*", SearchOption.AllDirectories).Take(1000)) list.Items.Add(f);
                }
                else if (File.Exists(sel)) list.Items.Add(sel);
            }
            catch (Exception ex) { list.Items.Add("Błąd: " + ex.Message); }
            preview.ShowDialog();
        }

        private void BtnRefreshLog_Click(object sender, RoutedEventArgs e) => LoadLogToTextbox();

        private void LoadLogToTextbox()
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Czyscik");
                var jsonLog = Path.Combine(dir, "czyscik.jsonl");
                var legacyLog = Path.Combine(dir, "czyscik.log");
                if (File.Exists(jsonLog))
                {
                    // Read last ~1000 lines and present in friendly format
                    var lines = File.ReadAllLines(jsonLog);
                    var take = 1000;
                    var slice = lines.Length > take ? lines.Skip(lines.Length - take) : lines;
                    var outLines = new List<string>();
                    foreach (var l in slice)
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(l);
                            var root = doc.RootElement;
                            var ts = root.GetProperty("timestamp").GetString();
                            var type = root.GetProperty("type").GetString();
                            var path = root.TryGetProperty("path", out var p) && p.ValueKind != System.Text.Json.JsonValueKind.Null ? p.GetString() : "";
                            var size = root.TryGetProperty("size", out var sz) && sz.ValueKind == System.Text.Json.JsonValueKind.Number ? sz.GetInt64().ToString() : "";
                            var msg = root.TryGetProperty("message", out var m) && m.ValueKind != System.Text.Json.JsonValueKind.Null ? m.GetString() : "";
                            outLines.Add($"[{ts}] {type} {path} {size} {msg}");
                        }
                        catch { outLines.Add(l); }
                    }
                    txtLog.Text = string.Join(Environment.NewLine, outLines);
                }
                else if (File.Exists(legacyLog))
                {
                    txtLog.Text = File.ReadAllText(legacyLog);
                }
                else txtLog.Text = "Brak logu";
            }
            catch (Exception ex) { txtLog.Text = "Błąd odczytu logu: " + ex.Message; }
        }

        private async Task StartCleaningAsync(bool dryRun)
        {
            btnStart.IsEnabled = false; btnPreview.IsEnabled = false; btnCancel.IsEnabled = true;
            cts = new CancellationTokenSource();
            lblStatus.Text = dryRun ? "Rozpoczynam podgląd..." : "Rozpoczynam...";
            progressBar.Value = 0;
            long totalFreed = 0;
            // wolne miejsce przed
            long freeBefore = GetTotalFreeBytes();
            lblBefore.Text = $"Wolne przed: {FormatBytes(freeBefore)}";

            var targets = new List<string>();
            if (chkTemp.IsChecked == true) targets.Add(Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath());
            if (chkWindowsTemp.IsChecked == true) targets.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
            if (chkPrefetch.IsChecked == true) targets.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch"));
            var cleanEventLogsRequested = chkSystemLogs.IsChecked == true;
            if (chkWindowsUpdate.IsChecked == true) targets.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download"));
            if (chkThumbs.IsChecked == true) targets.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer"));
            if (chkBrowsers.IsChecked == true)
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                targets.Add(Path.Combine(local, "Google", "Chrome", "User Data", "Default", "Cache"));
                targets.Add(Path.Combine(local, "Microsoft", "Edge", "User Data", "Default", "Cache"));
            }

            foreach (var it in lstPaths.Items) if (it is string s && !string.IsNullOrWhiteSpace(s) && !targets.Contains(s)) targets.Add(s);

            int total = targets.Count + (chkRecycle.IsChecked == true ? 1 : 0); int done = 0;
            Cleaner.Log("--- RUN START ---");
            try
            {
                foreach (var t in targets)
                {
                    if (cts.IsCancellationRequested) break;
                    lblStatus.Text = $"Czyszczenie: {t}";
                    long freed = await Cleaner.CleanPathAsync(t, dryRun);
                    totalFreed += Math.Max(0, freed);
                    done++;
                    progressBar.Value = Math.Round(100.0 * done / Math.Max(1, total));
                    lblFreed.Text = $"Odzyskane: {FormatBytes(totalFreed)}";
                }

                if (chkRecycle.IsChecked == true && !cts.IsCancellationRequested) await Cleaner.EmptyRecycleBinAsync(dryRun);
                if (cleanEventLogsRequested && !cts.IsCancellationRequested)
                {
                    await Cleaner.CleanEventLogsAsync(dryRun);
                }
                // wolne miejsce po
                long freeAfter = GetTotalFreeBytes();
                lblAfter.Text = $"Wolne po: {FormatBytes(freeAfter)}";
                lblStatus.Text = dryRun ? "Podgląd zakończony" : "Zakończono";
                Cleaner.Log($"TOTAL_FREED|{totalFreed}");
                LoadLogToTextbox();
                // Aktualizacja głównego licznika odzyskanego miejsca (na podstawie rozmiaru usuniętych plików)
                lblFreed.Text = $"Odzyskane: {FormatBytes(totalFreed)} (Δ {FormatBytes(Math.Max(0, freeAfter - freeBefore))})";
            }
            finally
            {
                btnStart.IsEnabled = true; btnPreview.IsEnabled = true; btnCancel.IsEnabled = false; cts = null;
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "n/d";
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            int i = 0; double dbl = bytes;
            while (dbl >= 1024 && i < suf.Length - 1) { dbl /= 1024; i++; }
            return $"{dbl:0.##} {suf[i]}";
        }

        private static long GetTotalFreeBytes()
        {
            try
            {
                long total = 0;
                foreach (var d in System.IO.DriveInfo.GetDrives())
                {
                    try
                    {
                        if (d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
                        {
                            total += d.AvailableFreeSpace;
                        }
                    }
                    catch { }
                }
                return total;
            }
            catch { return -1; }
        }
    }
}
