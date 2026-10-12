# ZXing.Net barcode decoder

- Author: Michael Jahn and ZXing contributors.
- Upstream: https://github.com/micjahn/ZXing.Net
- NuGet: ZXing.Net 0.16.10, `lib/netstandard2.0/zxing.dll`.
- Package: https://api.nuget.org/v3-flatcontainer/zxing.net/0.16.10/zxing.net.0.16.10.nupkg
- License: Apache-2.0, included in LICENSE.txt.
- DLL SHA-256: A61143D146593FB5EF89E98A2C969E3B81C553591800B069E3AFD1C2853F67C2

Used by ImportedCameraScanner for real camera frame decoding. No cloud upload is used for decoding. Recognized codes can be sent to the configured authenticated OMS inventory endpoint; camera frames stay on the device. Does not create warehouse transactions.
