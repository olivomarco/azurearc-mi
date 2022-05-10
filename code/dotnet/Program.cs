using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System;

Console.WriteLine("Trying to read a secret from a KeyVault using transparent authentication...");

// Azure-Arc specific environment variables
Environment.SetEnvironmentVariable("IDENTITY_ENDPOINT", "http://localhost:40342/metadata/identity/oauth2/token");
Environment.SetEnvironmentVariable("IMDS_ENDPOINT", "http://localhost:40342");

var client = new SecretClient(new Uri("https://passwordless-arc-mi.vault.azure.net/"), new DefaultAzureCredential());
try
{
    KeyVaultSecret secret = await client.GetSecretAsync("my-onprem-secret");
    Console.WriteLine(secret.Value);
}
catch (AuthenticationFailedException e)
{
    Console.WriteLine($"Authentication Failed. {e.Message}");
}
