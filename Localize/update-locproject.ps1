param ($SourcesDirectory, $LocProjectPath)

$jsonFiles = @()
$jsonLocalizationFiles = Get-ChildItem -Recurse -Path "$SourcesDirectory" -Filter "*.en.json" | Where-Object { $_.Directory.Name -eq "localize" }
$jsonLocalizationFiles | ForEach-Object {
    $null = $_.Name -Match "(.+)\.[\w-]+\.json" # matches '[filename].[langcode].json'

    $destinationFile = "$($_.Directory.FullName)\$($Matches.1).json"
    $jsonFiles += Copy-Item "$($_.FullName)" -Destination $destinationFile -PassThru
    Write-Host "JSON localization file generated: $destinationFile"
}

Push-Location "$SourcesDirectory"
$projectObject = Get-Content $LocProjectPath | ConvertFrom-Json
$jsonFiles | ForEach-Object {
    $sourceFile = ($_.FullName | Resolve-Path -Relative)
    $outputPath = "$(($_.DirectoryName | Resolve-Path -Relative) + "\")"
    $projectObject.Projects[0].LocItems += (@{
        SourceFile = $sourceFile
        CopyOption = "LangIDOnName"
        OutputPath = $outputPath
    })
}
Pop-Location

$locProjectJson = ConvertTo-Json $projectObject -Depth 5
Set-Content $LocProjectPath $locProjectJson
Write-Host "LocProject.json was updated to contain JSON localizations:`n`n$locProjectJson`n`n"
