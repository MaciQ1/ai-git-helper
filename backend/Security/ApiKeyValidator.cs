using System.Security.Cryptography;
using System.Text;
using CommitGenerator.Configuration;
using Microsoft.Extensions.Options;

namespace CommitGenerator.Security;

public sealed class ApiKeyValidator(IOptions<SecurityOptions> options)
{
    private readonly IReadOnlyList<ApiKeyDefinition> _keys = options.Value.GetConfiguredKeys();

    public bool TryGetUserName(string providedKey, out string userName)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));

        foreach (var key in _keys)
        {
            var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(key.Key));
            if (CryptographicOperations.FixedTimeEquals(providedHash, configuredHash))
            {
                userName = key.Name;
                return true;
            }
        }

        userName = string.Empty;
        return false;
    }
}
