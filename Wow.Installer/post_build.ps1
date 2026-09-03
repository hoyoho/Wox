param(
    [string]$solution = "."
)
$ErrorActionPreference = "Stop"

$path = "$solution\Output\packages"
$installer = "$path\Wow-Full-Installer.exe"
$version = (Get-Command $installer).FileVersionInfo.ProductVersion
$newName = "$path\Wow-Full-Installer.$version.exe"
Move-Item -Force $installer $newName