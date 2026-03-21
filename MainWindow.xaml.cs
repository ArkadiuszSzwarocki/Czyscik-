using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Czyscik
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? cts;

        public MainWindow()
        {
            InitializeComponent();
            PopulateDefaultPaths();
            chkTurbo.Checked += ChkTurbo_Changed;
            chkTurbo.Unchecked += ChkTurbo_Changed;
            btnPick.Click += BtnPick_Click;
            btnRefreshLog.Click += BtnRefreshLog_Click;
        }

        private void PopulateDefaultPaths()
        {
            var temp = Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath();
            if (!lstPaths.Items.Contains(temp)) lstPaths.Items.Add(temp);
            var winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
            if (!lstPaths.Items.Contains(winTemp)) lstPaths.Items.Add(winTemp);
            LoadLogToTextbox();
        }

        private void ChkTurbo_Changed(object? sender, RoutedEventArgs e)
        {
            if (chkTurbo.IsChecked == true)
            {
                chkTemp.IsChecked = true;
                chkWindowsTemp.IsChecked = true;
                chkPrefetch.IsChecked = true;
                chkThumbs.IsChecked = true;
                chkRecycle.IsChecked = true;
                chkBrowsers.IsChecked = true;
                AddTurboBrowserPaths();
            }
        }

        private void AddTurboBrowserPaths()
        {
            try
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var chrome = Path.Combine(local, "Google", "Chrome", "User Data", "Default", "Cache");
                var edge = Path.Combine(local, "Microsoft", "Edge", "User Data", "Default", "Cache");
                if (Directory.Exists(chrome) && !lstPaths.Items.Contains(chrome)) lstPaths.Items.Add(chrome);
                if (Directory.Exists(edge) && !lstPaths.Items.Contains(edge)) lstPaths.Items.Add(edge);
            }
            catch { }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var p = txtNewPath.Text.Trim();
            if (!string.IsNullOrEmpty(p) && !lstPaths.Items.Contains(p)) lstPaths.Items.Add(p);
            txtNewPath.Clear();
        }

        private void BtnPick_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
                {
                    var res = dlg.ShowDialog();
                    if (res == System.Windows.Forms.DialogResult.OK)
                    {
                        var p = dlg.SelectedPath;
                        if (!lstPaths.Items.Contains(p)) lstPaths.Items.Add(p);
                    }
                }
            }
            catch (Exception ex) { Cleaner.Log("PICKERR|" + ex.Message); }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            var sel = lstPaths.SelectedItem;
            if (sel != null) lstPaths.Items.Remove(sel);
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            await StartCleaningAsync(dryRun: false);
        }

        private async void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            await StartCleaningAsync(dryRun: true);
        }

        private async Task StartCleaningAsync(bool dryRun)
        {
            btnStart.IsEnabled = false;
            btnPreview.IsEnabled = false;
            btnCancel.IsEnabled = true;
            cts = new CancellationTokenSource();
            lblStatus.Text = dryRun ? "Rozpoczynam podgląd..." : "Rozpoczynam...";
            progressBar.Value = 0;
            long totalFreed = 0;

            var targets = new List<string>();
            if (chkTemp.IsChecked == true)
                targets.Add(Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath());
            if (chkWindowsTemp.IsChecked == true)
                targets.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
            if (chkPrefetch.IsChecked == true)
                targets.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch"));
            if (chkThumbs.IsChecked == true)
                targets.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer"));
            if (chkBrowsers.IsChecked == true)
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                targets.Add(Path.Combine(local, "Google", "Chrome", "User Data", "Default", "Cache"));
                targets.Add(Path.Combine(local, "Microsoft", "Edge", "User Data", "Default", "Cache"));
            }

            foreach (var it in lstPaths.Items)
            {
                if (it is string s && !string.IsNullOrWhiteSpace(s) && !targets.Contains(s)) targets.Add(s);
            }

            int total = targets.Count + (chkRecycle.IsChecked == true ? 1 : 0);
            int done = 0;

            Cleaner.Log("--- RUN START ---");

            try
            {
                foreach (var t in targets)
                {
                    if (cts?.IsCancellationRequested == true) break;
                    lblStatus.Text = $"Czyszczenie: {t}";
                    long freed = await Cleaner.CleanPathAsync(t, dryRun);
                    totalFreed += Math.Max(0, freed);
                    done++;
                    progressBar.Value = Math.Round(100.0 * done / Math.Max(1, total));
                    lblFreed.Text = $"Odzyskane: {FormatBytes(totalFreed)}";
                }

                if (chkRecycle.IsChecked == true && !(cts?.IsCancellationRequested == true))
                {
                    lblStatus.Text = dryRun ? "Podgląd: Kosz (bez opróżniania)" : "Opróżnianie Kosza...";
                    await Cleaner.EmptyRecycleBinAsync(dryRun);
                    done++;
                    progressBar.Value = Math.Round(100.0 * done / Math.Max(1, total));
                }

                lblStatus.Text = dryRun ? "Podgląd zakończony" : "Zakończono";
                Cleaner.Log($"TOTAL_FREED|{totalFreed}");
                lblLog.Text = "Log: " + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Czyscik", "czyscik.log");
                LoadLogToTextbox();
            }
            finally
            {
                btnStart.IsEnabled = true;
                btnPreview.IsEnabled = true;
                btnCancel.IsEnabled = false;
                cts = null;
            }
        }

        private void BtnDetails_Click(object? sender, RoutedEventArgs e)
        {
            var sel = lstPaths.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(sel))
            {
                MessageBox.Show("Wybierz katalog z listy, aby zobaczyć pliki.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var win = new FilesPreviewWindow(sel);
            win.Owner = this;
            win.ShowDialog();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
            lblStatus.Text = "Anulowano";
            btnCancel.IsEnabled = false;
        }

        private void BtnRefreshLog_Click(object? sender, RoutedEventArgs e)
        {
            LoadLogToTextbox();
        }

        private void LoadLogToTextbox()
        {
            try
            {
                var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Czyscik", "czyscik.log");
                if (File.Exists(log))
                {
                    var lines = File.ReadAllLines(log);
                    var tail = lines.Length > 200 ? string.Join(Environment.NewLine, lines.Skip(lines.Length - 200)) : string.Join(Environment.NewLine, lines);
                    txtLog.Text = tail;
                }
                else txtLog.Text = "Brak logu";
            }
            catch (Exception ex) { txtLog.Text = "Błąd odczytu logu: " + ex.Message; }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "n/d";
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double dbl = bytes;
            while (dbl >= 1024 && i < suf.Length - 1) { dbl /= 1024; i++; }
            return $"{dbl:0.##} {suf[i]}";
        }
    }
}
