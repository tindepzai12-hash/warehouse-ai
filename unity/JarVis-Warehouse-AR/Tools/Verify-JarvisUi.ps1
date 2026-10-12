param([switch]$Rebuild)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$editorPath = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
$logDirectory = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
if (Get-Process Unity -ErrorAction SilentlyContinue) {
    [ordered]@{ checkedUtc=[DateTime]::UtcNow.ToString('o'); passed=$false; status='blocked_editor_open'; note='Run the menu verification in the existing Editor after saving and stopping Play. Previous PASS is historical.' } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logDirectory 'ScannerUiVerification.latest.json') -Encoding utf8
    throw 'Unity is open. Save and stop Play, then use Tools > JarVis > Verify Scanner and Settings in that Editor. Do not start a second instance.'
}
if (!(Test-Path -LiteralPath $editorPath)) { throw 'Required Unity Editor is unavailable.' }
$method = if ($Rebuild) { 'ProjectBootstrap.ScannerUiVerification.RebuildAndRun' } else { 'ProjectBootstrap.ScannerUiVerification.Run' }
$logPath = Join-Path $logDirectory 'ScannerUiVerification.log'
$resultPath = Join-Path $logDirectory 'ScannerUiVerification.txt'
$startedUtc = [DateTime]::UtcNow
$arguments = '-batchmode -quit -projectPath "{0}" -executeMethod {1} -logFile "{2}"' -f $projectRoot, $method, $logPath
$checkProcess = Start-Process -FilePath $editorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$checkProcess.WaitForExit()
$freshResult = (Test-Path -LiteralPath $resultPath) -and ((Get-Item -LiteralPath $resultPath).LastWriteTimeUtc -ge $startedUtc)
$passed = $checkProcess.ExitCode -eq 0 -and $freshResult -and ((Get-Content -LiteralPath $resultPath -Raw).StartsWith('PASS:'))
[ordered]@{
    startedUtc = $startedUtc.ToString('o')
    finishedUtc = [DateTime]::UtcNow.ToString('o')
    method = $method
    exitCode = $checkProcess.ExitCode
    freshResult = $freshResult
    passed = $passed
    log = $logPath
    note = 'Physical Android camera and authenticated backend lookup require separate integration checks.'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logDirectory 'ScannerUiVerification.latest.json') -Encoding utf8
if (!$passed) { throw 'Verification failed or did not produce a fresh result. A previous PASS file is not evidence for this run. See ScannerUiVerification.latest.json and relevant error lines in the log.' }
Get-Content -LiteralPath $resultPath
