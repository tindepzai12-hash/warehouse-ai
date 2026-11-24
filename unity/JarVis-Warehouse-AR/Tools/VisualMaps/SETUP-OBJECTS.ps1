$ErrorActionPreference = 'Stop'
$revision = '48b10fa505aeb51fbd127fc60a5c660040e823b7'
$target = Join-Path $PSScriptRoot 'vendor\nanodet'
New-Item -ItemType Directory -Path $target -Force | Out-Null
$base = "https://raw.githubusercontent.com/opencv/opencv_zoo/$revision/models/object_detection_nanodet"
foreach ($filename in @('LICENSE','nanodet.py')) {
    Invoke-WebRequest -Uri "$base/$filename" -OutFile (Join-Path $target $filename) -UseBasicParsing
}
Invoke-WebRequest -Uri "https://media.githubusercontent.com/media/opencv/opencv_zoo/$revision/models/object_detection_nanodet/object_detection_nanodet_2022nov.onnx" -OutFile (Join-Path $target 'model.onnx.pending') -UseBasicParsing
if ((Get-FileHash -LiteralPath (Join-Path $target 'model.onnx.pending')).Hash -ne '4B82DA9944B88577175EE23A459DCE2E26E6E4BE573DEF65B1055DC2D9720186') { throw 'Model checksum mismatch; not installed.' }
if ((Get-FileHash -LiteralPath (Join-Path $target 'nanodet.py')).Hash -ne '5BA84922EAEFDC48D0066CFFDFB721D0D434C5F3B92213563161E00DA26CEA1B') { throw 'Wrapper checksum mismatch.' }
Move-Item -LiteralPath (Join-Path $target 'model.onnx.pending') -Destination (Join-Path $target 'model.onnx') -Force
Write-Output 'NanoDet CPU ready (3.8 MB, Apache-2.0). No Torch/CUDA installed.'
