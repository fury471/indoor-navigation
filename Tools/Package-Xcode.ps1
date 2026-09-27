[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExportDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ZipPath
)

$ErrorActionPreference = 'Stop'
[System.AppContext]::SetSwitch('Switch.System.IO.Compression.ZipFile.UseBackslash', $false)
Add-Type -AssemblyName System.IO.Compression.FileSystem

$sourcePath = (Resolve-Path -LiteralPath $ExportDirectory).Path.TrimEnd('\', '/')
$destinationPath = [System.IO.Path]::GetFullPath($ZipPath)
$projectFile = Join-Path $sourcePath 'Unity-iPhone.xcodeproj\project.pbxproj'

if (-not (Test-Path -LiteralPath $projectFile -PathType Leaf)) {
    throw 'Select the export folder that directly contains Unity-iPhone.xcodeproj.'
}
if ([System.IO.Path]::GetFileName($destinationPath) -ne 'xcode-export.zip') {
    throw 'Name the ZIP xcode-export.zip; the workflow expects that name.'
}
if ($destinationPath.StartsWith(
        $sourcePath + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Save the ZIP outside the export folder.'
}
if (Test-Path -LiteralPath $destinationPath) {
    throw 'The ZIP already exists. Choose a new output folder for this export.'
}

$parentDirectory = [System.IO.Path]::GetDirectoryName($destinationPath)
[System.IO.Directory]::CreateDirectory($parentDirectory) | Out-Null

# Include the export's contents at the ZIP root, including hidden files.
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $sourcePath,
    $destinationPath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false
)

$archive = [System.IO.Compression.ZipFile]::OpenRead($destinationPath)
try {
    if ($null -eq $archive.GetEntry('Unity-iPhone.xcodeproj/project.pbxproj')) {
        throw 'ZIP layout check failed: the Xcode project must be at the ZIP root.'
    }
} finally {
    $archive.Dispose()
}

$zipFile = Get-Item -LiteralPath $destinationPath
if ($zipFile.Length -ge 2GB) {
    throw 'This export exceeds the single GitHub release asset limit. Do not upload it with this workflow.'
}

Write-Host "ZIP ready: $destinationPath"
Write-Host ("Size: {0:N1} MiB" -f ($zipFile.Length / 1MB))
Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256
