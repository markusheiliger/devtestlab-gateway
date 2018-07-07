# SAMPLES
# =>> for development
# .\Connect-VirtualMachine -MachineHostname markushe-devbox.westeurope.cloudapp.azure.com
# =>> for testing
# .\Connect-VirtualMachine -MachineHostname markushe-devbox.westeurope.cloudapp.azure.com -MachinePort 3389 -GatewayHostname rdgw.dash-two.de -GatewayCode
# .\Connect-VirtualMachine -MachineHostname markushe-devbox.westeurope.cloudapp.azure.com -MachinePort 3389 -GatewayHostname rdpgw.vsdth.visualstudio.com -GatewayCode

param (

    # The lab's target machine resource ID
    [Parameter(Mandatory = $true)]
    [string] $MachineHostname,

    # Parameter help description
    [Parameter(Mandatory = $false)]
    [int] $MachinePort = 3389,

    # The RD gateway hostname
    [Parameter(Mandatory = $false)]
    [string] $GatewayHostname,

    [Parameter(Mandatory = $false)]
    [string] $GatewayCode
)

$tokenServiceBaseUrl = "https://$GatewayHostname/api"

if (-not $GatewayHostname -or -not $GatewayCode) {

    # fall back to dev mode
    $tokenServiceBaseUrl = "http://localhost:7071/api"
}

$tokenServiceFullUrl = "$tokenServiceBaseUrl/host/$MachineHostname/port/$MachinePort" 

$headers = @{
    "x-ms-client-object-id" = (Get-AzureRmADUser -UserPrincipalName ((Get-AzureRmContext).Account.Id)).Id
    "x-functions-key" = $GatewayCode
}

Write-Host "Connecting token service: $tokenServiceFullUrl "
$headers | FT

$response = Invoke-RestMethod -Uri $tokenServiceFullUrl -Headers $headers

if ($response -and $response.token) {

    $tokenHost = (($response.token -split "&")[0] -split "=")[1]
    $tokenPort = (($response.token -split "&")[1] -split "=")[1]

    $rdpFilePath = Join-Path $PSScriptRoot "$tokenHost.rdp"

    @(
        "full address:s:${tokenHost}:${tokenPort}",
        "prompt for credentials:i:1",
        "bandwidthautodetect:i:0"
        "gatewayhostname:s:$GatewayHostname",
        "gatewayaccesstoken:s:$($response.token)",
        "gatewayusagemethod:i:1",
        "gatewaycredentialssource:i:5",
        "gatewayprofileusagemethod:i:1"

    ) | Out-File -FilePath $rdpFilePath -Force

    Start-Process "mstsc.exe" -ArgumentList @( $rdpFilePath )
    
} else {

    Write-Error "Failed to get token from gateway API."
}