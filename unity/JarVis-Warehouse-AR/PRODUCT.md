# JarVis Warehouse

## Platform
Android app, built in Unity 6000.4.7f1 with uGUI/TextMeshPro. Primary device: Samsung S20 Ultra, portrait. Logical design viewport: 412 × 915.

## Users and purpose
Warehouse employees perform assigned work, scan barcode/QR codes, look up stock and locations, report problems, and ask JarVis for assistance. Managers administer inventory, accounts and assignments on the separate OMS web application. The owner confirmed app-first scope on 2026-10-06.

## Product constraints
Imported Figma content and approved JarVis mascot/Justin photograph are real assets. Keep warehouse data empty until managers enter real records. Figma numbers are never database truth. Secrets and model credentials stay on the backend. VI/EN interface selection is independent of AI response language.

## Online tracking
Authentication is separate from presence. A fresh account without an active presence session starts offline. Online time starts only after an explicit user action acknowledged by the server. Restore an existing valid online session from server state; do not count twice across devices. This measures online activity, not payroll attendance.

Daily statistics use Asia/Taipei and reset at Taiwan midnight. Preserve the underlying session across midnight. Current policy: 30-second foreground heartbeat, 120-second lease; an abandoned or disconnected session ends at its lease boundary. Explicit logout ends presence for the account on all devices.

## Confirmed identity
JarVis blue/white, Be Vietnam Pro, approved mascot and actual user photograph. Light and dark modes, three font sizes. Preserve warehouse terminology and existing functional integrations.

## Open implementation work
Physical Android safe area, microphone/camera permissions, IME and performance require Samsung testing. Task lists, status transitions and stock lookup/detail now use OMS APIs through ImportedWarehouseLists with loading, empty and recoverable error states. Stock movement confirmation, incident submission, label printing, team chat and live AR positioning remain unfinished; visual presence does not imply production readiness. Completed-task work duration is not supplied by the current API and must not be fabricated.
