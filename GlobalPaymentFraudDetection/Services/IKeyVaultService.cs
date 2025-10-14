namespace GlobalPaymentFraudDetection.Services;

public interface IKeyVaultService
{
    Task<string> GetSecretAsync(string secretName);
    Task<Dictionary<string, string>> GetMultipleSecretsAsync(params string[] secretNames);
}
