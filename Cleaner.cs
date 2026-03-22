using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Czyscik
{
    public static class Cleaner
    {
        private static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Czyscik");
        private static readonly string LogFile = Path.Combine(LogDir, "czyscik.log");
        private static readonly string JsonLogFile = Path.Combine(LogDir, "czyscik.jsonl");

        static Cleaner()
        {
            try { Directory.CreateDirectory(LogDir); } catch { }
        }

        public static async Task<long> CleanPathAsync(string path, bool dryRun = false)
        {
            return await Task.Run(() => {
                long freed = 0;
                try
                {
                    if (Directory.Exists(path))
                    {
                        // Special-case: Prefetch folder — only top-level .pf files
                        if (string.Equals(new DirectoryInfo(path).Name, "Prefetch", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var file in Directory.EnumerateFiles(path, "*.pf", SearchOption.TopDirectoryOnly))
                            {
                                try
                                {
                                    var fi = new FileInfo(file);
                                    long size = fi.Length;
                                    if (!dryRun)
                                    {
                                        fi.IsReadOnly = false;
                                        File.Delete(file);
                                        Log($"DEL|{file}|{size}");
                                    }
                                    else
                                    {
                                        Log($"PREVIEW|{file}|{size}");
                                    }
                                    freed += size;
                                }
                                catch (Exception ex)
                                {
                                    Log($"SKIP|{file}|{ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                            {
                                try
                                {
                                    var fi = new FileInfo(file);
                                    long size = fi.Length;
                                    if (!dryRun)
                                    {
                                        fi.IsReadOnly = false;
                                        File.Delete(file);
                                        Log($"DEL|{file}|{size}");
                                    }
                                    else
                                    {
                                        Log($"PREVIEW|{file}|{size}");
                                    }
                                    freed += size;
                                }
                                catch (Exception ex)
                                {
                                    Log($"SKIP|{file}|{ex.Message}");
                                }
                            }
                            if (!dryRun)
                            {
                                foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
                                {
                                    try { Directory.Delete(dir, true); } catch { }
                                }
                            }
                        }
                    }
                    else if (File.Exists(path))
                    {
                        try {
                            var fi = new FileInfo(path);
                            long size = fi.Length;
                            if (!dryRun) { File.Delete(path); Log($"DEL|{path}|{size}"); }
                            else { Log($"PREVIEW|{path}|{size}"); }
                            freed += size;
                        } catch (Exception ex) { Log($"SKIP|{path}|{ex.Message}"); }
                    }
                    else
                    {
                        Log($"MISSING|{path}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"ERR|{path}|{ex.Message}");
                }
                return freed;
            });
        }

        public static void Log(string line)
        {
            try
            {
                // Human-readable legacy log
                File.AppendAllText(LogFile, DateTime.Now.ToString("s") + " " + line + Environment.NewLine);
            }
            catch { }
            try
            {
                // Also write structured JSONL
                var obj = ParseLogLine(line);
                var json = System.Text.Json.JsonSerializer.Serialize(obj);
                File.AppendAllText(JsonLogFile, json + Environment.NewLine);
            }
            catch { }
        }

        private static object ParseLogLine(string line)
        {
            // Expected formats: TYPE|path|size or TYPE|path|message or other
            var parts = line.Split('|');
            var type = parts.Length > 0 ? parts[0] : "INFO";
            string path = parts.Length > 1 ? parts[1] : null;
            long? size = null;
            string message = null;
            if (parts.Length > 2)
            {
                if (long.TryParse(parts[2], out var s)) size = s;
                else message = parts[2];
            }
            // if there are more parts, append them to message
            if (parts.Length > 3)
            {
                var rest = string.Join('|', parts, 3, parts.Length - 3);
                message = (message == null) ? rest : (message + "|" + rest);
            }
            return new
            {
                timestamp = DateTime.Now.ToString("o"),
                type = type,
                path = path,
                size = size,
                message = message
            };
        }

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        public static Task<long> EmptyRecycleBinAsync(bool dryRun = false)
        {
            return Task.Run(() => {
                try
                {
                    if (dryRun)
                    {
                        Log($"RECYCLE|PREVIEW|Would empty recycle bin");
                        return -1L;
                    }
                    // We can't get size easily — treat as unknown (-1)
                    uint res = SHEmptyRecycleBin(IntPtr.Zero, null, 0);
                    Log($"RECYCLE|EMPTIED|{res}");
                }
                catch (Exception ex) { Log($"RECYCLE|ERR|{ex.Message}"); }
                return -1L;
            });
        }
    }
}
