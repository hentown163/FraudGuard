using Azure.Security.KeyVault.Secrets;
using Azure.Identity;

namespace GlobalPaymentFraudDetection.Services;

public class KeyVaultService : IKeyVaultService
{
    private readonly SecretClient _client;

    public KeyVaultService(IConfiguration configuration)
    {
        var vaultUri = configuration["KeyVault:VaultUri"] ?? throw new ArgumentNullException("KeyVault:VaultUri");
        _client = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        var secret = await _client.GetSecretAsync(secretName);
        return secret.Value.Value;
    }

    public async Task<Dictionary<string, string>> GetMultipleSecretsAsync(params string[] secretNames)
    {
        var secrets = new Dictionary<string, string>();
        
        foreach (var secretName in secretNames)
        {
            var secret = await _client.GetSecretAsync(secretName);
            secrets[secretName] = secret.Value.Value;
        }

        return secrets;
    }
}
