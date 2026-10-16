<#
.SYNOPSIS
    Charge la configuration locale de déploiement (deploy.config.psd1, non versionné).

.DESCRIPTION
    Copier deploy.config.example.psd1 en deploy.config.psd1 et y renseigner les IP et identifiants.
    Les paramètres passés en ligne de commande restent prioritaires.
#>
function Get-DeployConfig {
    $path = Join-Path $PSScriptRoot "deploy.config.psd1"
    if (Test-Path $path) {
        return Import-PowerShellDataFile $path
    }
    return @{}
}
