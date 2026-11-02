param([string]$OutputName = 'PingTurntable.exe')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '需要 Windows .NET Framework 4.x，未找到 C# 编译器。' }
$sourcePaths = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
$outputPath = Join-Path $projectRoot $OutputName
& $compilerPath /nologo /target:winexe /platform:anycpu /optimize+ /utf8output /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll "/out:$outputPath" @sourcePaths
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
Write-Output "Built: $outputPath"
