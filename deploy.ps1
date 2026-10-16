<#
.SYNOPSIS
    Compile MatroxMaevex et le déploie sur un processeur Crestron.

.DESCRIPTION
    Recompile le projet (pour être sûr de déployer le .cpz à jour), upload le .cpz par SFTP
    dans /programNN puis relance le programme via SSH (progload).
    Utilisé par deploy-test.ps1 et deploy-prod.ps1, mais peut être appelé directement.

    Login et mot de passe : paramètres, sinon deploy.config.psd1 (voir deploy.config.example.psd1).

.EXAMPLE
    .\deploy.ps1 -IpAddress 192.168.1.50
    .\deploy.ps1 -IpAddress 192.168.1.50 -Slot 2 -Password "AutreMotDePasse"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$IpAddress,

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, 10)]
    [int]$Slot = 2,

    [Parameter(Mandatory = $false)]
    [string]$Login = "",

    [Parameter(Mandatory = $false)]
    [string]$Password = "",

    [Parameter(Mandatory = $false)]
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "deploy-config.ps1")
$config = Get-DeployConfig
if (-not $Login) { $Login = $config["Login"] }
if (-not $Login) { $Login = "admin" }
if (-not $Password) { $Password = $config["Password"] }
if (-not $Password) {
    throw "Mot de passe non défini : le renseigner dans deploy.config.psd1 ou le passer avec -Password."
}

$projectName = "MatroxMaevex"
$cpzPath = Join-Path $PSScriptRoot "bin\Debug\net8.0\$projectName.cpz"

function Ensure-PoshSshModule {
    if (-not (Get-Module -ListAvailable -Name Posh-SSH)) {
        Write-Host "Posh-SSH module not found. Installing for current user..."
        Install-Module -Name Posh-SSH -Scope CurrentUser -Force
    }

    Import-Module Posh-SSH -Force
}

if (-not $SkipBuild) {
    Write-Host "Building $projectName.csproj (Debug)..."
    dotnet build (Join-Path $PSScriptRoot "$projectName.csproj") --nologo -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed (exit code $LASTEXITCODE), déploiement annulé."
    }
}

if (-not (Test-Path $cpzPath)) {
    throw "No .cpz found. Expected: $cpzPath"
}

Ensure-PoshSshModule

$securePassword = ConvertTo-SecureString $Password -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential($Login, $securePassword)

$cpzName = Split-Path $cpzPath -Leaf
$remoteDirectory = "/program{0:d2}" -f $Slot
$remotePath = "$remoteDirectory/$cpzName"

Write-Host "CPZ file: $cpzPath"
Write-Host "Target: $IpAddress (slot $Slot)"
Write-Host "Uploading CPZ via SFTP to $remoteDirectory ..."

$sftpSession = $null
$sshSession = $null

try {
    $sftpSession = New-SFTPSession -ComputerName $IpAddress -Credential $credential -AcceptKey

    if (Get-Command -Name Set-SFTPFile -ErrorAction SilentlyContinue) {
        Write-Host "Upload mode: Set-SFTPFile"
        Set-SFTPFile -SessionId $sftpSession.SessionId -LocalFile $cpzPath -RemotePath $remotePath -Overwrite
    }
    elseif (Get-Command -Name Set-SFTPItem -ErrorAction SilentlyContinue) {
        Write-Host "Upload mode: Set-SFTPItem"
        Set-SFTPItem -SessionId $sftpSession.SessionId -Path $cpzPath -Destination $remoteDirectory -Force
    }
    else {
        throw "No supported SFTP upload cmdlet found (Set-SFTPItem/Set-SFTPFile)."
    }
    Write-Host "SFTP upload complete."

    $sshSession = New-SSHSession -ComputerName $IpAddress -Credential $credential -AcceptKey
    $loadCommand = "progload -p:$Slot"

    Write-Host "Running SSH command: $loadCommand"
    $result = Invoke-SSHCommand -SessionId $sshSession.SessionId -Command $loadCommand

    if ($result.Output) {
        Write-Host "SSH output:"
        $result.Output | ForEach-Object { Write-Host $_ }
    }
    if ($result.Error) {
        Write-Host "SSH error:"
        $result.Error | ForEach-Object { Write-Host $_ }
    }
}
finally {
    if ($sftpSession) {
        Remove-SFTPSession -SessionId $sftpSession.SessionId | Out-Null
    }
    if ($sshSession) {
        Remove-SSHSession -SessionId $sshSession.SessionId | Out-Null
    }
}

Write-Host "Deploy completed."
