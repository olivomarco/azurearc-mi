from azure.identity import ManagedIdentityCredential
from azure.keyvault.secrets import SecretClient
import os

os.environ["IDENTITY_ENDPOINT"] = "http://localhost:40342/metadata/identity/oauth2/token"
os.environ["IMDS_ENDPOINT"] = "http://localhost:40342"

credentials = ManagedIdentityCredential()

# Change the path to your actual KeyVault
secret_client = SecretClient(vault_url="https://passwordless-arc-mi.vault.azure.net", credential=credentials)
# Change the name of the secret to your actual secret
secret = secret_client.get_secret("my-onprem-secret")
print("KeyVault secret is: " + secret.value)
