using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Czyscik
{
    public static class Scheduler
    {
        public static Task<bool> CreateDailyTaskAsync(string taskName, string exePath, string timeHHmm)
        {
            return Task.Run(() =>
            {
                try
                {
                    // Build schtasks arguments. /F to force replace if exists, /RL LIMITED to run without highest privileges
                    var tr = $"\"{exePath}\"";
                    var args = $"/Create /SC DAILY /TN \"{taskName}\" /TR {tr} /ST {timeHHmm} /F /RL LIMITED";
                    var psi = new ProcessStartInfo("schtasks.exe", args)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var p = Process.Start(psi);
                    if (p == null) return false;
                    var outp = p.StandardOutput.ReadToEnd();
                    var err = p.StandardError.ReadToEnd();
                    p.WaitForExit(10000);
                    if (p.ExitCode == 0)
                    {
                        Cleaner.Log($"SCHED|CREATED|{taskName}|{timeHHmm}");
                        return true;
                    }
                    else
                    {
                        Cleaner.Log($"SCHED|ERR|{taskName}|{p.ExitCode}|{err}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Cleaner.Log($"SCHED|EX|{ex.Message}");
                    return false;
                }
            });
        }
    }
}
