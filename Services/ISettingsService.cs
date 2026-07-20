using Assistant.Models;

namespace Assistant.Services
{
    public interface ISettingsService
    {
        AppSettings Load();
        void Save(AppSettings settings);
    }
}
