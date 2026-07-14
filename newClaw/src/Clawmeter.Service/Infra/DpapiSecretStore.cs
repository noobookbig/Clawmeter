using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Clawmeter.Service.Infra;

/// <summary>
/// Windows DPAPI-backed secret store. Plaintext never touches disk.
///   * Protect() / Unprotect() wrap System.Security.Cryptography.ProtectedData
///     with CurrentUser scope + LocalMachine entropy (default for desktop).
///   * Storage path: %APPDATA%\Clawmeter\secrets\*.key
///   * Used for: MiniMax API key, future WiFi PSK, future OAuth refresh tokens.
/// On Linux/macOS the call site is swapped for libsecret / Keychain —
/// kept behind a single interface so the rest of the service is portable.
/// </summary>
public sealed class DpapiSecretStore
{
    private readonly ILogger<DpapiSecretStore> _log;

    public DpapiSecretStore(ILogger<DpapiSecretStore> log)
    {
        _log = log;
    }

    public byte[] Protect(byte[] plaintext)
    {
        var plaintextBlob = System.Text.Encoding.UTF8.GetBytes("CLAW");
        return ProtectedData.Protect(plaintext, plaintextBlob,
            DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(byte[] ciphertext)
    {
        var plaintextBlob = System.Text.Encoding.UTF8.GetBytes("CLAW");
        return ProtectedData.Unprotect(ciphertext, plaintextBlob,
            DataProtectionScope.CurrentUser);
    }
}
