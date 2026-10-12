# JarVis Warehouse Unity UI profile

Read this reference only for JarVis Warehouse or other Unity UI work in this repository.

## Product context

- Android-first warehouse assistant built with Unity 6, uGUI, and TextMeshPro.
- Primary users are warehouse associates, supervisors, and inventory staff who need fast, reliable, one-handed operation.
- Core flows include splash, login, dashboard, tasks, barcode scanning and label creation, inventory, indoor map/navigation, AR guidance, incident reporting, and JarVis chat.
- The interface supports Vietnamese and English. Keep operational terms concise and ensure both languages fit without clipping.
- Mock inventory, maps, tasks, or AI responses must be visibly labeled as demo/mock until backed by a real system.

## Visual direction

Use a bright blue-and-white identity with strong readability:

| Token | Value | Use |
|---|---:|---|
| Primary | `#1257C8` | Primary actions, selected navigation, key emphasis |
| Primary pressed | `#0B3F98` | Pressed/active state |
| Heading | `#123568` | Major headings |
| Background | `#F5F8FD` | App background |
| Surface | `#FFFFFF` | Cards, forms, sheets |
| Soft blue | `#EAF2FF` | Selected and informational surfaces |
| Main text | `#172B4D` | Body text |
| Secondary text | `#52627A` | Supporting information |
| Border | `#DCE5F0` | Dividers and input borders |
| Success | `#18794E` | Confirmed success |
| Warning | `#9A5B00` | Attention needed |
| Error | `#C62828` | Failure and destructive confirmation |

Use semantic tokens rather than scattering raw color values through scene-building code. The visual identity may evoke the clarity of familiar blue-and-white consumer brands, but must remain JarVis-specific and must not copy another company's logo or trade dress.

## Layout and interaction

- Design portrait-first and verify the existing Canvas Scaler/reference resolution before changing dimensions.
- Respect Android safe areas, camera cutouts, navigation bars, and the on-screen keyboard.
- Use at least 48 x 48 dp-equivalent touch targets with adequate separation.
- Keep body text readable at mobile distance; support Vietnamese diacritics and text expansion.
- Give each screen one clear primary action. Use progressive disclosure for secondary operations.
- Limit bottom navigation to five labeled destinations: Home, Tasks, Scan, Map, JarVis.
- Include visible loading, empty, offline, error, disabled, pressed, selected, and success states where applicable.
- Do not communicate inventory, expiry, route, or error status through color alone.

## Motion

- Use short, purposeful transitions, generally 200-300 ms.
- Give buttons immediate press feedback; animate sheets from their spatial origin.
- Suitable product motions include a brief robot greeting, logo fade, chat typing dots, barcode scan line, task completion feedback, and map/AR direction emphasis.
- Avoid continuous decorative motion during warehouse work. Provide a reduced-motion path and keep final semantic state correct when an animation is interrupted.

## Unity implementation

- Inspect `Assets/Editor/ProjectBootstrap/JarvisSceneBuilder.cs` and the existing scene/controller structure before editing.
- Prefer shared theme helpers, prefabs, and reusable components over repeated one-off GameObjects.
- Keep generated scenes reproducible through the project's builder when that is the established source of truth.
- Prefer uGUI layout components and anchors over fixed coordinates that only fit one Game view size.
- Keep icons, text, robot artwork, and animated parts as separate assets/layers.
- Use TextMeshPro font assets that contain all required Vietnamese and English glyphs.
- Avoid hand-editing large `.unity` YAML files when an editor script or scene builder can make the change safely.

## Verification

- Check the real Game view, not only the Scene view.
- Verify at narrow and target Android aspect ratios, safe areas, text expansion, keyboard-open forms, and scroll behavior.
- Confirm touch targets, navigation/back behavior, loading/error/offline states, and reduced motion.
- Compile after implementation changes and inspect the Unity Editor log for actual errors.
- Compare the result with the approved Figma/Canva reference and record intentional deviations.

