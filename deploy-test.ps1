<#
.SYNOPSIS
    Déploie MatroxMaevex sur le processeur de test.

.DESCRIPTION
    IP, login et mot de passe : paramètres, sinon deploy.config.psd1 (TestIpAddress, Login, Password).

.EXAMPLE
    .\deploy-test.ps1
    .\deploy-test.ps1 -Slot 2 -Password "AutreMotDePasse"
#>
param(
    [ValidateRange(1, 10)]
    [int]$Slot = 2,
    [string]$Login = "",
    [string]$Password = "",
    [string]$IpAddress = "",
    [switch]$SkipBuild
)

. (Join-Path $PSScriptRoot "deploy-config.ps1")
if (-not $IpAddress) { $IpAddress = (Get-DeployConfig)["TestIpAddress"] }
if (-not $IpAddress) {
    throw "IP de test non définie : renseigner TestIpAddress dans deploy.config.psd1 ou la passer avec -IpAddress."
}

Write-Host "=== Déploiement TEST MatroxMaevex -> $IpAddress (slot $Slot) ===" -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "deploy.ps1") -IpAddress $IpAddress -Slot $Slot -Login $Login -Password $Password -SkipBuild:$SkipBuild
