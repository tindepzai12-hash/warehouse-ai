# Inventory polish — 2026-11-23

Small UGUI layout repair in existing JarvisLocalCatalogue; no new screen, no new art or font, no workflow or quantity changes.

Hierarchy: existing card -> original product photo (left) / measured SKU, title, stock badge + location, barcode/lot (right). Green quantity badge uses actual quantity, VI/EN text and theme-specific contrast. Title uses existing font and tries original size, minus one, minus two; wraps only if still necessary. Following rows and card height derive from text, instead of a fixed two-line title slot.

Detail modal uses the same badge and bounded title sizing. Its header/photo/body positions derive from measured title and summary; red close control and all existing information remain.

Review: native Unity screenshots in VI/EN, light/dark, normal text, large English text, 1080x2400 and 1080x1920. No rebuild of imported scenes. Last installed Samsung version before this edit is 0.4.10; this inventory edit is not installed until explicitly recorded otherwise.

## Kết quả

Native Unity capture completed, build succeeded; signed APK 0.4.11/code35 verified and replacement-installed on Samsung R3CN20R5Q2F. Calibration and mission files byte-identical before/after. App was not launched after install, per owner request to stop. Native screenshots and text overflow checks completed for VI/EN, light/dark and large English text; normal English FreshDelight title uses size13 instead of14 and fits one line. At large text sizes, full name/volume may use two deliberate lines.

SHA256: 9E300117BE024393AEC58FE94D532B15B7610597428BCEC7585EAA4464D331F0
