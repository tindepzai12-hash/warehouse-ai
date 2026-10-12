# JarVis employee app design contract

This records the established blue/white identity and the confirmed app-first redesign direction. Product truth lives in PRODUCT.md.

## Intent
Operate: one-handed warehouse work. Status, next action and actual stock information outrank decoration. Avoid fabricated charts, redundant nested cards and decorative glass. Glass is reserved for the already-requested appearance controls.

## Hierarchy and navigation
Online Home exposes confirmed state, today's duration, current session, Taiwan reset policy and one status action. Below it: warehouse shortcuts and real recent activity. The owner restored the blurred dashboard approach: offline Home follows the owner's reference: a compact 64-unit header with Justin's portrait/greeting, Go Online and notification action; the entire dashboard and bottom navigation below it are genuinely blurred under a dim veil, with a sharp centered lock message. The timer card is shown only online. Today's total stays persisted while offline.

Four destinations: Home, Tasks, Map, JarVis. Scan is a separate raised action between the destinations. Account, security, language, text size and theme stay under Settings. Back from Tasks returns Home. Detail flows keep a back stack and their underlying form state.

## Shared tokens
JarVisDesignTokens.cs owns accent, primary/secondary ink, subtle border, dark ink, radius and target size. ImportedUiQuality and UniformUiSurface provide sharp consistent surfaces across the 22 routes. Spacing rhythm: 4/8/12/16/20/24/32. Card radius 16; controls 12. Use 48 logical units for new primary controls where feasible, 44 minimum.

## Type and imagery
Be Vietnam Pro. Clear title/body/caption hierarchy, no stretched text or portraits. The main time readout uses HH:MM:SS with a fixed rectangle; dynamic text respects three size choices. Approved user portrait is cropped, never regenerated. JarVis mascot keeps its identity and dark-mode contrast backing.

## States and motion
Presence: Loading, Offline, Connecting, Online, Reconnecting, ConnectionLost. No optimistic online claim. Missing server state uses --:--:--; network errors explain recovery. Freeze an unconfirmed display and re-fetch state. Do not animate business values.

Keep animation short, explain state changes, and avoid repeated bouncing/glow across the app. Existing task chip motion is local feedback. QR sweep stays inside its blue target. The online timer updates a small component once per second; server synchronization is separate.

## Native constraints
Keep logical 412×915 geometry proportional, respecting Android safe area. Editor capture uses the logical canvas directly. Do not claim desktop/web breakpoint testing proves Android keyboard, camera or accessibility services work. Screen-reader integration for Unity remains an explicit gap.
