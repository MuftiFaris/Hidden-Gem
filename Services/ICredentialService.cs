namespace Assistant.Services
{
    public interface ICredentialService
    {
        /// <summary>Saves the API key to Windows Credential Manager.</summary>
        bool SaveApiKey(string apiKey);

        /// <summary>Retrieves the stored API key, or null if none is saved.</summary>
        string? GetApiKey();

        /// <summary>Removes the stored credential.</summary>
        bool DeleteApiKey();

        /// <summary>Returns true when a credential exists in the store.</summary>
        bool HasApiKey();
    }
}
