# Passwordless authentication in hybrid/multicloud scenarios using Azure Arc

In a world where IT is subject to challenges of using cloud services from multiple cloud service providers and having a legacy of IT systems on-prem or on the edge, customers desperately search a technology to **seamlessly integrate applications** running in these different worlds and **improve your security posture** by using passwordless authentication.

Consider this sample hybrid scenario: a business application (say, a web portal) resides on-prem and receives orders from customers. In an evolutionary path, this application needs to add modern functionalities such as custom vision or voice recognition to better serve its customers.

These are challenges well solved by cloud AI services, and implementing them on-prem would poise significant technological and economical challenges. Therefore, hybrid adoption of cloud in this case is a *no-brainer*.

![Diagram of the hybrid and multi-cloud challenge](/images/diagram.png)

In this article we will implement a passwordless authentication mechanism between resources on-prem (hybrid) or other Cloud Service Providers (multi-cloud) that need to access to Azure resources, all using Azure Arc.

We will dig through the technology at high level, and then share some sample code.

## What is Azure Arc?

In a nutshell, Azure Arc is a set of technologies that brings Azure security and cloud-native services to hybrid and multicloud environments. It enables you to secure and govern infrastructure and apps anywhere, build cloud-native apps faster with familiar tools and services, and modernize your data estate with Azure data and machine-learning services.

One not very well known and often overlooked feature of Arc is the one described in this article: a VM, even if on-prem or on another cloud provider, can actually be given an identity that applications running on top of it can use to authenticate transparently (i.e. without using *any* username or password) with *any* Azure service: a storage, a KeyVault, a DB, a Cognitive Service... you name it.

## Authentication across hybrid and multi-cloud resources

When you have an application that runs on prem and requires access to an Azure resource, you typically have to resort to having a service-principal with its own secret; you then have to store that secret somewhere, in a place accessible to application and developers. The problem is that once this secret is passed to a developer, at least one human person knows it, and it's not a secret anymore.

Alas, sometimes these secrets are not handled properly (often even committed to Git repos!) and result in being the possible breach that can be used by malicious actors to exploit your systems, simply by... authenticating to them.

*Because hackers don't break in, they log in.*

## The solution in Azure cloud: Managed Identity

You need a way to authenticate your application **transparently** by trusting the service that it runs on against your target. For instance, if your application is a simple application running on a VM and you need to connect the application to a DB, you need a way to get an identity from the VM, and spend it against your DB.

In Azure terms, this is known as a [Managed Identity](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview). To simplify things, we won't make a distinction between System Assigned and User Assigned Managed Identities: let's just take for granted that, *inside* Azure, you can use a Managed Identity to do a passwordless authentication between applications and the resources they use.

## Bringing Managed Identity to hybrid and multi-cloud

But what if your application runs on premise and needs to connect to a DB in Azure? Or if your application runs on a VM in AWS or GCP, and its DB is in Azure? Shall we revert back to the legacy mechanism of service principals?

Luckily, no.

Azure, through Azure Arc, transparently creates a Managed Identity in Azure Active Directory and assigns it to the VM that is being installed on. This identity can then be profiled on target resources (i.e. a DB) and the application has a way to gain a token and use it for authentication. And this happens magically no matter where the VM resides: in fact, the identity is stored in Azure AD, in the cloud, and the Azure Arc agent installed in the VM provides a way to transparently gain an auth token even if the machine is on-prem, on AWS, GCP... wherever.

This rules out passwords completely, and makes an attack harder: attackers now have to break in the VM to be able to gain an auth token.

This diagram shows the solution to our challenge:

![Authentication flow diagram](/images/solution.png)

## Enough talking, show me the code

In this paragraph we will explore how SDKs provided by Microsoft can be used to authenticate a VM to, for example, an Azure KeyVault to read a secret without having to use a secret to authenticate to the KeyVault itself.
We will use a specific language (Python) and one operating system (Linux) to do this, just to demonstrate that this technology is totally portable.

KeyVault, of course, is just an example and can be substituted with any Azure service.

Our machine will be a Virtual Machine named `arc-server.lab` running on a local Proxmox (KVM) server, and will connect to Azure Cloud to our KeyVault named `zerotrust-arc-kv`.

We will also assume that this machine has already been onboarded to Arc by installing the agent on it [as per instructions](https://docs.microsoft.com/en-us/azure/azure-arc/servers/onboard-portal). The VM will be onboarded in Azure Arc with the name `arc-server`.

![Objects inside resource group](/images/1.png)

*NOTE: SDK is provided for [many more languages](https://github.com/MicrosoftDocs/azure-docs/blob/main/articles/app-service/overview-managed-identity.md#connect-to-azure-services-in-app-code), and you can still use raw HTTP GET requests if your chosen language is not implemented out of the box. Also, you can connect Linux and Windows machines to Azure Arc, and therefore have Managed Identity on multiple OS. Yes, [Microsoft actually loves Linux](https://cloudblogs.microsoft.com/windowsserver/2015/05/06/microsoft-loves-linux/).*

First of all, we need to make sure that the Managed Identity (MI) generated by Azure Arc for our server is trusted on our KeyVault. For the specificity of the KeyVault, we need to make sure that we define an access policy for our MI and give it privileges to read secrets:

![Access policy on Azure KeyVault](/images/2.png)

Then, in the KeyVault, add a secret called `my-onprem-secret`:

![Create a secret inside Azure KeyVault](/images/3.png)

As value, I will type in "Azure Arc is cool", but you are free to type whatever you wish.

Now it's time to go on the VM and start writing our code. For our purpose we will use the Azure libraries for Python and we will write a very small script that will:

- Ask the local Azure Arc agent a token (linked to our MI)
- Use this token to gain access to the secret `my-onprem-secret` that we just created, and print it on screen

As simple as it is, in our shell we first spin up a Python virtualenv and install the required libraries:

```bash
pip install virtualenv
virtualenv myarcenv
cd myarcenv
source bin/activate
pip install azure-identity azure-keyvault
```

Then, create a file named `access.py` using your favorite editor and paste in this content:

```python
from azure.identity import ManagedIdentityCredential
from azure.keyvault.secrets import SecretClient
import os

os.environ["IDENTITY_ENDPOINT"] = "http://localhost:40342/metadata/identity/oauth2/token"
os.environ["IMDS_ENDPOINT"] = "http://localhost:40342"

credentials = ManagedIdentityCredential()

secret_client = SecretClient(vault_url="https://zerotrust-arc-kv.vault.azure.net", credential=credentials)
secret = secret_client.get_secret("my-onprem-secret")
print("KeyVault secret is: " + secret.value)
```

(refer also to [this file](/code/access.py))

See? No username or passwords used or written in the code or anywhere else.

Before actually using this code, make sure that your current user belongs to the `himds` group (needed for Azure Arc):

```bash
sudo usermod -a -G himds $USER
```

Finally, let's run the script and see the output:

```bash
$ python access.py
KeyVault secret is: Azure Arc is cool
```

And voilà! The magic is served: you can now access Azure resources from your local on-prem machine (or AWS, GCP, Alibaba, DigitalOcean...) and transparently authenticate without any username or password.

Your mileage may vary if you adopt other programming languages, but at the end it's the same story, period.

## Conclusions

Hybrid and multicloud workloads are actually becoming a thing that many big companies have, and deserve to be treated with proper tools and by bringing the best of cloud technology to on-prem and edge environments: in this article we explored how Azure Arc can help in improving your security posture in this hybrid and multi-cloud world.

Of course, you are not bound only to accessing secrets stored in a KeyVault: you can use storage accounts, connect to SQL Servers using Managed Identities, and in general gain access to any kind of Azure service you might want to use.
As long as it is integrated with Azure AD, you can get access to it and leverage on this authentication mechanism.

You can find more details on the steps outlined in this document in the [official documentation](https://docs.microsoft.com/en-us/azure/azure-arc/servers/managed-identity-authentication), and further use cases and features of Azure Arc [here](https://docs.microsoft.com/en-us/azure/azure-arc/overview).
