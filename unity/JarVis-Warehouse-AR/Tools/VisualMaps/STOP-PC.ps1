$ErrorActionPreference = 'Stop'
$config = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'data\connection.json') -Raw | ConvertFrom-Json
Invoke-RestMethod -Uri 'http://127.0.0.1:8765/stop' -Method Post -Headers @{Authorization=('Bearer ' + $config.token)} -ContentType 'application/json' -Body '{}'
