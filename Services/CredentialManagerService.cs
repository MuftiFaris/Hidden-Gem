using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Assistant.Services
{
    /// <summary>
    /// Stores the Gemini API key in Windows Credential Manager via the
    /// advapi32 CredWrite / CredRead / CredDelete / CredFree APIs.
    ///
    /// The credential type is CRED_TYPE_GENERIC (1) so it appears in
    /// Control Panel ▶ Credential Manager ▶ Windows Credentials and the
    /// user can inspect or remove it there at any time.
    ///
    /// The blob is encoded as UTF-16LE (Encoding.Unicode) which matches
    /// the format expected by the native API and by the Credential Manager UI.
    /// </summary>
    public sealed class CredentialManagerService : ICredentialService
    {
        private const string CredentialTarget = "MicrosoftEdge_ApiKey";
        private const uint   CRED_TYPE_GENERIC        = 1;
        private const uint   CRED_PERSIST_LOCAL_MACHINE = 2;
        private const int    ERROR_NOT_FOUND           = 1168;

        private readonly ILogger<CredentialManagerService> _logger;

        public CredentialManagerService(ILogger<CredentialManagerService> logger)
            => _logger = logger;

        // ── P/Invoke declarations ─────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint   Flags;
            public uint   Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint   CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint   Persist;
            public uint   AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredWrite([In] ref CREDENTIAL credential, [In] uint flags);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern void CredFree([In] IntPtr buffer);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredDelete(string target, uint type, uint flags);

        // ── Public API ────────────────────────────────────────────────────────

        public bool SaveApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return false;

            try
            {
                var blob       = Encoding.Unicode.GetBytes(apiKey);
                var blobHandle = GCHandle.Alloc(blob, GCHandleType.Pinned);
                try
                {
                    var cred = new CREDENTIAL
                    {
                        Type               = CRED_TYPE_GENERIC,
                        TargetName         = CredentialTarget,
                        Comment            = "Microsoft Edge — API Key",
                        CredentialBlobSize = (uint)blob.Length,
                        CredentialBlob     = blobHandle.AddrOfPinnedObject(),
                        Persist            = CRED_PERSIST_LOCAL_MACHINE,
                        UserName           = Environment.UserName
                    };

                    bool ok = CredWrite(ref cred, 0);
                    if (!ok)
                        _logger.LogError("CredWrite failed. Win32={Error}", Marshal.GetLastWin32Error());
                    else
                        _logger.LogInformation("API key saved to Windows Credential Manager");
                    return ok;
                }
                finally { blobHandle.Free(); }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception saving API key");
                return false;
            }
        }

        public string? GetApiKey()
        {
            try
            {
                if (!CredRead(CredentialTarget, CRED_TYPE_GENERIC, 0, out var ptr))
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err != ERROR_NOT_FOUND)
                        _logger.LogWarning("CredRead failed. Win32={Error}", err);
                    return null;
                }

                try
                {
                    var cred = Marshal.PtrToStructure<CREDENTIAL>(ptr);
                    if (cred.CredentialBlobSize == 0) return null;

                    var bytes = new byte[cred.CredentialBlobSize];
                    Marshal.Copy(cred.CredentialBlob, bytes, 0, (int)cred.CredentialBlobSize);
                    return Encoding.Unicode.GetString(bytes);
                }
                finally { CredFree(ptr); }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception reading API key");
                return null;
            }
        }

        public bool DeleteApiKey()
        {
            try
            {
                bool ok = CredDelete(CredentialTarget, CRED_TYPE_GENERIC, 0);
                if (ok) _logger.LogInformation("API key removed from Windows Credential Manager");
                return ok;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception deleting API key");
                return false;
            }
        }

        public bool HasApiKey() => GetApiKey() is not null;
    }
}
