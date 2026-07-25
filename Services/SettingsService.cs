using System;
using System.IO;
using System.Text.Json;
using Assistant.Models;
using Microsoft.Extensions.Logging;

namespace Assistant.Services
{
    /// <summary>
    /// Persists AppSettings as JSON to %LOCALAPPDATA%\HiddenGem\settings.json.
    /// API keys are NEVER written here; only non-sensitive preferences are stored.
    /// </summary>
    public sealed class SettingsService : ISettingsService
    {
        private readonly string                _path;
        private readonly ILogger<SettingsService> _logger;
        private readonly JsonSerializerOptions  _json = new() { WriteIndented = true };

        public SettingsService(ILogger<SettingsService> logger)
        {
            _logger = logger;
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HiddenGem");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "settings.json");
        }

        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(_path)) return new AppSettings();
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<AppSettings>(json, _json) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings — using defaults");
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, _json);
                File.WriteAllText(_path, json);
                _logger.LogDebug("Settings saved to {Path}", _path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
            }
        }
    }
}
