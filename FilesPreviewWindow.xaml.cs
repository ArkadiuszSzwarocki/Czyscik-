using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Czyscik
{
    public partial class FilesPreviewWindow : Window
    {
        private readonly string path;
        private System.Threading.CancellationTokenSource? cts;

        public FilesPreviewWindow(string path)
        {
            InitializeComponent();
            this.path = path;
            btnClose.Click += (s, e) => Close();
            btnDeleteHere.Click += BtnDeleteHere_Click;
            btnCancelDelete.Click += BtnCancelDelete_Click;
            LoadFilesAsync();
        }

        private async void LoadFilesAsync()
        {
            try
            {
                txtHeader.Text = "Podgląd: " + path;
                if (Directory.Exists(path))
                {
                    var files = await Task.Run(() => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).OrderBy(f => f).ToList());
                    long total = 0;
                    foreach (var f in files)
                    {
                        try { var fi = new FileInfo(f); total += fi.Length; } catch { }
                        lstFiles.Items.Add(f);
                    }
                    lblSummary.Text = $"Plików: {files.Count}  —  Rozmiar: {FormatBytes(total)}";
                    lblFreedPreview.Text = "Odzyskane: 0 B";
                    lblStatusPreview.Text = "Gotowe";
                    progressBarPreview.Value = 0;
                }
                else if (File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    lstFiles.Items.Add(path);
                    lblSummary.Text = $"Plik: 1  —  Rozmiar: {FormatBytes(fi.Length)}";
                    lblFreedPreview.Text = "Odzyskane: 0 B";
                    lblStatusPreview.Text = "Gotowe";
                }
                else txtHeader.Text += " (brak)";
            }
            catch (Exception ex) { txtHeader.Text += " — błąd: " + ex.Message; }
        }

        private void BtnCancelDelete_Click(object? sender, RoutedEventArgs e)
        {
            cts?.Cancel();
            lblStatusPreview.Text = "Anulowanie...";
        }

        private async void BtnDeleteHere_Click(object? sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Na pewno usunąć wszystkie pokazane pliki w tym katalogu?", "Potwierdź", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;

            btnDeleteHere.IsEnabled = false;
            btnClose.IsEnabled = false;
            btnCancelDelete.IsEnabled = true;
            cts = new System.Threading.CancellationTokenSource();

            try
            {
                if (Directory.Exists(path))
                {
                    var files = await Task.Run(() => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).OrderBy(f => f).ToList());
                    int total = files.Count;
                    long freed = 0;
                    for (int i = 0; i < files.Count; i++)
                    {
                        if (cts.IsCancellationRequested) break;
                        var f = files[i];
                        try
                        {
                            var fi = new FileInfo(f);
                            long size = fi.Length;
                            fi.IsReadOnly = false;
                            File.Delete(f);
                            Cleaner.Log($"DEL|{f}|{size}");
                            freed += size;
                        }
                        catch (Exception ex)
                        {
                            Cleaner.Log($"SKIP|{f}|{ex.Message}");
                        }
                        // update UI
                        var pct = Math.Round(100.0 * (i + 1) / Math.Max(1, total));
                        Dispatcher.Invoke(() => {
                            progressBarPreview.Value = pct;
                            lblStatusPreview.Text = $"Usuwanie: {i + 1}/{total} - {System.IO.Path.GetFileName(f)}";
                            lblFreedPreview.Text = $"Odzyskane: {FormatBytes(freed)}";
                        });
                        await Task.Delay(10); // let UI breathe
                    }

                    // attempt to delete empty directories (bottom-up)
                    if (!cts.IsCancellationRequested)
                    {
                        var dirs = Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories)
                                    .OrderByDescending(d => d.Length).ToList();
                        foreach (var d in dirs)
                        {
                            try { Directory.Delete(d, false); } catch { }
                        }
                    }

                    Dispatcher.Invoke(() => {
                        lblStatusPreview.Text = cts.IsCancellationRequested ? "Anulowano" : "Zakończono";
                        progressBarPreview.Value = cts.IsCancellationRequested ? progressBarPreview.Value : 100;
                        lblFreedPreview.Text = $"Odzyskane: {FormatBytes(freed)}";
                    });

                    MessageBox.Show($"Usunięto (szac.): {FormatBytes(freed)}", "Gotowe", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (File.Exists(path))
                {
                    long freed = 0;
                    try
                    {
                        var fi = new FileInfo(path);
                        long size = fi.Length;
                        File.Delete(path);
                        Cleaner.Log($"DEL|{path}|{size}");
                        freed = size;
                    }
                    catch (Exception ex) { Cleaner.Log($"SKIP|{path}|{ex.Message}"); }
                    Dispatcher.Invoke(() => {
                        progressBarPreview.Value = 100;
                        lblFreedPreview.Text = $"Odzyskane: {FormatBytes(freed)}";
                        lblStatusPreview.Text = "Zakończono";
                    });
                    MessageBox.Show($"Usunięto (szac.): {FormatBytes(freed)}", "Gotowe", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd: " + ex.Message);
            }
            finally
            {
                btnDeleteHere.IsEnabled = true;
                btnClose.IsEnabled = true;
                btnCancelDelete.IsEnabled = false;
                cts = null;
                Close();
            }
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
