# Create a RDGateway

## Requirements

## Provision the Gateway

## Report licenses

# Create a RDGateway enabled lab

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fmarkusheiliger%2Fdevtestlab-gateway%2Fmaster%2Farm%2Flab%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

## Requirements

The following to parameters are required to setup a RDGateway enabled lab:
* rdGatewayHostname -The public FQDN of the RDGateway host.
* rdGatewayAPIKey - The key to authorize at the RDGateway API.

*The 'rdGatewayAPIKey' value is returned by the Deploy-AzureResourceGroup.ps1 in the /arm/gateway folder after the provisioning finished. If you missed to copy the APIKey or the PowerShell script failed to resolve the key, please go to the provisioned Azure Function in the Azure portal, expand the 'CreateToken' function node, and select 'Manage'. Then click on the copy link for the default key in the 'Host Keys' section.*

![copy APIKey](https://github.com/markusheiliger/devtestlab-gateway/blob/master/img/CopyAPIKey.png)

## Provision the Lab