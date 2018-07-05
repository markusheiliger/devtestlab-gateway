# SAMPLE
# .\Connect-VirtualMachine -GatewayHostname rdgw.dash-two.de -VirtualMachineResourceId /subscriptions/d3fed5a4-2ebb-4ad4-93c6-51afcbe01350/resourceGroups/Lab4Bosch/providers/Microsoft.DevTestLab/labs/lab4bosch/virtualmachines/markushe-devbox

param (

    # The RD gateway hostname
    [Parameter(Mandatory = $true)]
    [string] $GatewayHostname,

    # The lab's target machine resource ID
    [Parameter(Mandatory = $true)]
    [string] $VirtualMachineResourceId,

    [Parameter(Mandatory = $false)]
    [string] $CreateTokenServiceCode
)

$tokenServiceBaseUrl = "https://$GatewayHostname/api/"

if (-not $CreateTokenServiceCode) {

    # no token service api key available - assume we are in dev mode
    $tokenServiceBaseUrl = "http://localhost:7071/api/"
}

$tokenServiceFullUrl = $tokenServiceBaseUrl + $VirtualMachineResourceId.Trim("/") + "/users/" + (Get-AzureRmADUser -UserPrincipalName ((Get-AzureRmContext).Account.Id)).Id

if ($CreateTokenServiceCode) {

    # add authentication token for create token service
    $tokenServiceFullUrl += "?code=$CreateTokenServiceCode"
}

Write-Host "Connecting token service: $tokenServiceFullUrl"

$response = Invoke-RestMethod -Uri $tokenServiceFullUrl

if ($response) {

    $virtualMachineHost = (($response.token -split "&")[0] -split "=")[1]
    $virtualMachinePort = (($response.token -split "&")[1] -split "=")[1]

    $rdpFilePath = Join-Path $PSScriptRoot "$virtualMachineHost.rdp"

    @(
        "full address:s:${virtualMachineHost}:${virtualMachinePort}",
        "prompt for credentials:i:1",
        "gatewayhostname:s:$GatewayHostname",
        "gatewayaccesstoken:s:$($response.token)",
        "gatewayusagemethod:i:1",
        "gatewaycredentialssource:i:5",
        "gatewayprofileusagemethod:i:1"

    ) | Out-File -FilePath $rdpFilePath -Force

    Start-Process "mstsc.exe" -ArgumentList @( $rdpFilePath )
}