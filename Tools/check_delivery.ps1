param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe',
    [switch]$BuildWindows
)

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$logs = Join-Path $project 'Logs'
$results = Join-Path $logs 'utf-entrega.xml'
$testLog = Join-Path $logs 'utf-entrega.log'
New-Item -ItemType Directory -Path $logs -Force | Out-Null

function Invoke-Unity([string[]]$Arguments) {
    $process = Start-Process -FilePath $Unity -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    $process.Refresh()
    if ($process.ExitCode -ne 0) {
        throw "Unity terminó con código $($process.ExitCode). Revisá el archivo indicado por -logFile."
    }
}

$common = @('-batchmode', '-projectPath', ('"{0}"' -f $project))
Invoke-Unity ($common + @('-nographics', '-runTests', '-testPlatform', 'EditMode',
    '-testResults', ('"{0}"' -f $results), '-logFile', ('"{0}"' -f $testLog)))
[xml]$report = Get-Content -LiteralPath $results
$run = $report.'test-run'
if ($run.result -ne 'Passed' -or [int]$run.passed -lt 1) {
    throw "El informe de Unity Test Framework no contiene pruebas aprobadas. Revisá $results."
}
Write-Output "Unity Test Framework: $($run.passed) aprobadas, $($run.failed) fallidas."

if ($BuildWindows) {
    $player = Join-Path $project 'Build\Windows-Entrega\JurassicPix.exe'
    $buildLog = Join-Path $logs 'build-windows-entrega.log'
    Invoke-Unity ($common + @('-buildTarget', 'Win64', '-buildWindows64Player',
        ('"{0}"' -f $player), '-quit', '-logFile', ('"{0}"' -f $buildLog)))
    if (!(Test-Path -LiteralPath $player) -or !(Select-String -LiteralPath $buildLog -SimpleMatch 'Build Finished, Result: Success.' -Quiet)) {
        throw "No se pudo verificar la compilación. Revisá $buildLog."
    }
    Write-Output "Windows x64: $player"
}
