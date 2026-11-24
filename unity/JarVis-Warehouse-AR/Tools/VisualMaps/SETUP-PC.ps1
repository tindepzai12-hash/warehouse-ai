param([Parameter(Mandatory=$true)][string]$Python)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
if (-not (Test-Path -LiteralPath '.venv\Scripts\python.exe')) { & $Python -m venv .venv; if ($LASTEXITCODE -ne 0) { throw 'Không tạo được môi trường Python.' } }
& '.\.venv\Scripts\python.exe' -m pip install --no-cache-dir -r requirements-lock.txt
if ($LASTEXITCODE -ne 0) { throw 'Không tải được thư viện. Kiểm tra mạng và thử lại.' }
if (-not (Test-Path -LiteralPath 'vendor\hloc\.git')) { git clone --no-checkout https://github.com/cvg/Hierarchical-Localization.git vendor/hloc; if ($LASTEXITCODE -ne 0) { throw 'Không tải được hloc.' } }
git -C vendor/hloc checkout c13273bd0ecc2917a35910fd843712a1c6243193
if ($LASTEXITCODE -ne 0) { throw 'Không chốt được phiên bản hloc đã kiểm thử.' }
Write-Output 'Đã cài riêng trong thư mục này. Chạy START-PC.cmd; không cần cài CUDA hoặc driver mới.'
