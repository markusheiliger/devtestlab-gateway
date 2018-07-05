# SAMPLE
# .\Connect-VirtualMachine -GatewayHostname rdgw.dash-two.de -MachineHostname markushe-devbox.westeurope.cloudapp.azure.com -MachinePort 3389 

param (

    # The RD gateway hostname
    [Parameter(Mandatory = $true)]
    [string] $GatewayHostname,

    # The lab's target machine resource ID
    [Parameter(Mandatory = $true)]
    [string] $MachineHostname,

    # Parameter help description
    [Parameter(Mandatory = $false)]
    [int] $MachinePort = 3389,

    [Parameter(Mandatory = $false)]
    [string] $APICode
)

$tokenServiceBaseUrl = "https://$GatewayHostname/api"

if (-not $APICode) {

    # no token service api key available - assume we are in dev mode
    $tokenServiceBaseUrl = "http://localhost:7071/api"
}

$tokenServiceFullUrl = "$tokenServiceBaseUrl/host/$MachineHostname/port/$MachinePort" 

$headers = @{
    "x-ms-client-object-id" = (Get-AzureRmADUser -UserPrincipalName ((Get-AzureRmContext).Account.Id)).Id
    "x-functions-key" = $APICode
}

Write-Host "Connecting token service: $tokenServiceFullUrl"

$response = Invoke-RestMethod -Uri $tokenServiceFullUrl -Headers $headers

if ($response) {

    $tokenHost = (($response.token -split "&")[0] -split "=")[1]
    $tokenPort = (($response.token -split "&")[1] -split "=")[1]

    $rdpFilePath = Join-Path $PSScriptRoot "$tokenHost.rdp"

    @(
        "full address:s:${tokenHost}:${tokenPort}",
        "prompt for credentials:i:1",
        "gatewayhostname:s:$GatewayHostname",
        "gatewayaccesstoken:s:$($response.token)",
        "gatewayusagemethod:i:1",
        "gatewaycredentialssource:i:5",
        "gatewayprofileusagemethod:i:1"

    ) | Out-File -FilePath $rdpFilePath -Force

    Start-Process "mstsc.exe" -ArgumentList @( $rdpFilePath )
}