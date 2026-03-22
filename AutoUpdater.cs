using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Czyscik
{
    public static class AutoUpdater
    {
        public static async Task<(bool available, string latestTag, string url)> CheckForUpdatesAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Czyscik-Updater");
                var api = "https://api.github.com/repos/ArkadiuszSzwarocki/Czyscik-/releases/latest";
                var resp = await client.GetAsync(api);
                if (!resp.IsSuccessStatusCode)
                {
                    Cleaner.Log($"UPDATE|ERR|HTTP|{(int)resp.StatusCode}");
                    return (false, null, null);
                }
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var tag = root.GetProperty("tag_name").GetString();
                var url = root.GetProperty("html_url").GetString();
                var current = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";
                Cleaner.Log($"UPDATE|CHECK|current:{current}|latest:{tag}");
                var available = !string.IsNullOrEmpty(tag) && tag != current;
                return (available, tag, url);
            }
            catch (Exception ex)
            {
                Cleaner.Log($"UPDATE|ERR|{ex.Message}");
                return (false, null, null);
            }
        }
    }
}