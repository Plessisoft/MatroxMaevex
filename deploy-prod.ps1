<#
.SYNOPSIS
    Déploie MatroxMaevex sur le processeur de production.

.DESCRIPTION
    IP, login et mot de passe : paramètres, sinon deploy.config.psd1 (ProdIpAddress, Login, Password).

.EXAMPLE
    .\deploy-prod.ps1
    .\deploy-prod.ps1 -IpAddress 10.0.0.10 -Slot 2 -Password "AutreMotDePasse"
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
if (-not $IpAddress) { $IpAddress = (Get-DeployConfig)["ProdIpAddress"] }
if (-not $IpAddress) {
    throw "IP de production non définie : renseigner ProdIpAddress dans deploy.config.psd1 ou la passer avec -IpAddress."
}

Write-Host "=== Déploiement PROD MatroxMaevex -> $IpAddress (slot $Slot) ===" -ForegroundColor Yellow
& (Join-Path $PSScriptRoot "deploy.ps1") -IpAddress $IpAddress -Slot $Slot -Login $Login -Password $Password -SkipBuild:$SkipBuild
