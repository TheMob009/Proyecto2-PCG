param(
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$logDirectory = Join-Path $projectPath '.utmp'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory 'scene-checks.log'
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "No se encontró Unity: $UnityPath" }
$lockPath = Join-Path $projectPath 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $lockPath) {
    try { $lockStream = [System.IO.File]::Open($lockPath, 'Open', 'ReadWrite', 'None'); $lockStream.Dispose() }
    catch [System.IO.IOException] { throw 'Cierra este proyecto en Unity antes de ejecutar las pruebas sin renderizado.' }
}

# Sin dispositivo gráfico: las comprobaciones no presentan imágenes mediante Direct3D.
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectPath + '"'),
    '-executeMethod', 'PCG.Sudoku.Editor.SudokuPuzzleChecks.Run', '-quit', '-logFile', ('"' + $logPath + '"'))
$checkProcess = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($checkProcess.ExitCode -ne 0) { throw "Unity terminó con código $($checkProcess.ExitCode). Revisa $logPath" }
$report = Select-String -LiteralPath $logPath -Pattern '^PASS Sudoku scene checks:'
if ($null -eq $report) { throw "No se encontró la confirmación de las pruebas. Revisa $logPath" }
$report.Line
