namespace Assistant.Services
{
    public interface ICredentialService
    {
        /// <summary>Saves the API key to Windows Credential Manager.</summary>
        bool SaveApiKey(string apiKey, string provider = "Gemini");

        /// <summary>Retrieves the stored API key, or null if none is saved.</summary>
        string? GetApiKey(string provider = "Gemini");

        /// <summary>Removes the stored credential.</summary>
        bool DeleteApiKey(string provider = "Gemini");

        /// <summary>Returns true when a credential exists in the store.</summary>
        bool HasApiKey(string provider = "Gemini");
    }
}
