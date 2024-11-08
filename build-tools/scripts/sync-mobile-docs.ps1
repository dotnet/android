param (
    [string]$DocsMobilePath,
    [switch]$SyncToAndroid
)

$androidDocsPath = Join-Path -Path $PSScriptRoot -ChildPath "../../Documentation/docs-mobile"
$docsMobilePath = Join-Path -Path $DocsMobilePath -ChildPath "docs/android"
$sourcePath = $androidDocsPath 
$destinationPath = $docsMobilePath 
if ($SyncToAndroid) {
    $sourcePath = $docsMobilePath
    $destinationPath = $androidDocsPath
}
Write-Host "Syncing content from '$sourcePath' to Folder '$destinationPath'..."
try {
    Copy-Item -Path $sourcePath\* -Destination $destinationPath -Recurse -Force
    Write-Output "Files copied from '$sourcePath' to '$destinationPath' successfully."
} catch {
    Write-Error "Error copying files: $_"
}
