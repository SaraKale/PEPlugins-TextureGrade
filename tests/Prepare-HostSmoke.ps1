param([string]$PmxEditorDir = '')
$ErrorActionPreference = 'Stop'
$repo = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$PmxEditorDir) { $PmxEditorDir = Join-Path $repo '..\PmxEditor_0275' }
$source = (Resolve-Path -LiteralPath $PmxEditorDir).Path
$destination = Join-Path $repo 'artifacts\pmx-host'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
foreach ($file in @('PmxEditor.exe', 'PmxEditor.exe.config')) {
    Copy-Item -LiteralPath (Join-Path $source $file) -Destination $destination -Force
}
foreach ($folder in @('Lib', '_data', 'x86')) {
    Copy-Item -LiteralPath (Join-Path $source $folder) -Destination $destination -Recurse -Force
}
$plugins = Join-Path $destination '_plugin\TextureGradeVerification'
$fixture = Join-Path $destination 'fixture'
New-Item -ItemType Directory -Force -Path $plugins, $fixture | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'bin\Release\net48\PEPlugins-TextureGrade.dll') -Destination $plugins -Force
Copy-Item -LiteralPath (Join-Path $repo 'tests\host\bin\Release\net48\HostSmoke.dll') -Destination $plugins -Force
Copy-Item -LiteralPath (Join-Path $repo 'artifacts\verification\source.png') -Destination $fixture -Force
Set-Content -LiteralPath (Join-Path $destination 'texturegrade-test-host.marker') -Value 'Isolated TextureGrade test host. Contains generated fixtures only.'
Write-Output (Join-Path $destination 'PmxEditor.exe')
