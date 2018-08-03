param(

    # Parameter help description
    [Parameter(ParameterSetName = "SetLab", Mandatory = $true)]
    [string] $LabResourceId,

    # Parameter help description
    [Parameter(ParameterSetName = "SetLabs", Mandatory = $true)]
    [string] $LabName,

    # Parameter help description
    [Parameter(ParameterSetName = "SetLab")]
    [Parameter(ParameterSetName = "SetLabs")]
    [string] $GatewayHostname,

    # Parameter help description
    [Parameter(ParameterSetName = "SetLab")]
    [Parameter(ParameterSetName = "SetLabs")]
    [string] $GatewayAPIKey
)

function Export-AzureRmContextData {

    [CmdletBinding()]
    param()

    $ContextPath = [System.IO.Path]::ChangeExtension($PSCommandPath, '.ctx')
    if (Test-Path $ContextPath -PathType Leaf) { Remove-Item -Path $ContextPath -Force | Out-Null }

    $ContextClassic = [bool] (Get-Command -Name Save-AzureRmProfile -ErrorAction SilentlyContinue) # returns TRUE if AzureRM.profile version 2.7 or older is loaded
    if ($ContextClassic) { Save-AzureRmProfile -Path $ContextPath } else { Save-AzureRmContext -Path $ContextPath -Force }

    return $ContextPath
}

function Import-AzureRmContextData {

    [CmdletBinding()]
    param(
        [string] $ContextPath = [System.IO.Path]::ChangeExtension($PSCommandPath, '.ctx')
    )

    $ContextClassic = [bool] (Get-Command -Name Select-AzureRmProfile -ErrorAction SilentlyContinue) # returns TRUE if AzureRM.profile version 2.7 or older is loaded

    if ($contextClassic) { Select-AzureRMProfile -Path $ContextPath -ErrorAction=$error } else { Import-AzureRmContext -Path $ContextPath }
}

function Set-ExtendedProperty {

    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [psobject] $ExtendedProperties,

        [Parameter(Mandatory = $true)]
        [string] $Key,

        [Parameter(Mandatory = $false)]
        [string] $Value
    )

    if ($ExtendedProperties | Get-Member -Name $Key -MemberType NoteProperty) {

        if ($Value) {

            $ExtendedProperties.$Key = $Value

        } else {

            $ExtendedProperties.psobject.properties.remove($Key)
        }    

    } else {

        $ExtendedProperties | Add-Member -MemberType NoteProperty -Name $Key -Value $Value | Out-Null
    }

    return Get-ExtendedProperty -ExtendedProperties $ExtendedProperties -Key $Key
}

function Get-ExtendedProperty {

    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [psobject] $ExtendedProperties,

        [Parameter(Mandatory = $true)]
        [string] $Key
    )

    if ($ExtendedProperties | Get-Member -Name $Key -MemberType NoteProperty) {

        return $ExtendedProperties.$Key
    } 
    
    return $null
}

if ($LabName) {

    $context = Export-AzureRmContextData

    try {

        Find-AzureRmResource -ResourceType "Microsoft.DevTestLab/labs" | ? { $_.ResourceName -like $LabName } | select -ExpandProperty ResourceId | % {

            $jobName = Split-Path -Path $_ -Leaf
            $jobScript = { param([string] $resourceId) &$using:PSCommandPath -LabResourceId $resourceId -GatewayHostname $using:GatewayHostname -GatewayAPIKey $using:GatewayAPIKey }

            Start-Job -Name $jobName -ScriptBlock $jobScript -ArgumentList $_

        } | Receive-Job -Wait -AutoRemoveJob
    }
    finally {

        Remove-Item -Path $context -Force -ErrorAction SilentlyContinue | Out-Null
    }

}
else {

    Import-AzureRmContextData -ErrorAction SilentlyContinue | Out-Null

    $apiVersion = "2017-04-26-preview"

    $properties = Get-AzureRmResource -ResourceId $LabResourceId -ApiVersion $apiVersion -Pre -ExpandProperties -Verbose | select -ExpandProperty Properties

    if (-not ($properties | Get-Member -Name extendedProperties -MemberType NoteProperty)) {

        $properties | Add-Member -MemberType NoteProperty -Name extendedProperties 

        Set-ExtendedProperty -ExtendedProperties $properties.extendedProperties -Key "RdpConnectionType" -Value "2"
    }

    Set-ExtendedProperty -ExtendedProperties $properties.extendedProperties -Key "RdpGateway" -Value $GatewayHostname | Out-Null

    $tokenSecretName = Get-ExtendedProperty -ExtendedProperties $properties.extendedProperties -Key "RdgTokenSecretName"

    if ((-not $GatewayHostname) -or (-not $GatewayAPIKey)) {

        Set-ExtendedProperty -ExtendedProperties $properties.extendedProperties -Key "RdgTokenSecretName" | Out-Null
    }
    elseif (-not $tokenSecretName) {

        $tokenSecretName = Set-ExtendedProperty -ExtendedProperties $properties.extendedProperties -Key "RdgTokenSecretName" -Value (((65..90) + (97..122) | Get-Random -Count 13 | % {[char]$_ }) -join "")
    }
    
    $vaultName = Split-Path $properties.vaultName -Leaf

    try {
    
        Set-AzureRmKeyVaultAccessPolicy -VaultName $vaultName -UserPrincipalName ((Get-AzureRmContext).Account.Id) -PermissionsToSecrets list,set,delete -Verbose

        if ((-not $GatewayHostname) -or (-not $GatewayAPIKey)) {

            Set-AzureKeyVaultSecret -VaultName $vaultName -Name $tokenSecretName -Force

        } else {

            Set-AzureKeyVaultSecret -VaultName $vaultName -Name $tokenSecretName -SecretValue (ConvertTo-SecureString -String $GatewayAPIKey -AsPlainText -Force) -Verbose | Out-Null
        }        
    }
    finally {

        Remove-AzureRmKeyVaultAccessPolicy -VaultName $vaultName -UserPrincipalName ((Get-AzureRmContext).Account.Id) -Verbose
    }

    Set-AzureRmResource -ResourceId $LabResourceId -Properties $properties -ApiVersion $apiVersion -Pre -Force -Verbose | Out-Null

    Find-AzureRmResource -ResourceType 'Microsoft.Network/networkSecurityGroups' -ResourceGroupNameEquals (Get-AzureRmResource -ResourceId $LabResourceId | select -ExpandProperty ResourceGroupName) | % {

        $nsg = Get-AzureRmNetworkSecurityGroup -Name $_.ResourceName -ResourceGroupName $_.ResourceGroupName

        if ($nsg) {

            $nsg.SecurityRules | ? { $_.Protocol -eq "Tcp" -and $_.DestinationPortRange -eq 3389 } | % {

                $sourceAddressPrefix = New-Object System.Collections.Generic.List[string]

                if ($GatewayHostname) {

                    ([System.Net.Dns]::GetHostAddresses($GatewayHostname) | select -ExpandProperty IPAddressToString) | % { $sourceAddressPrefix.Add($_) }

                } else {

                    $sourceAddressPrefix.Add("*")
                }     
                
                $_.SourceAddressPrefix = $sourceAddressPrefix
            }

            Set-AzureRmNetworkSecurityGroup -NetworkSecurityGroup $nsg -Verbose | Out-Null
        }
    }

    Write-Output "Updated lab '$LabResourceId'"
}