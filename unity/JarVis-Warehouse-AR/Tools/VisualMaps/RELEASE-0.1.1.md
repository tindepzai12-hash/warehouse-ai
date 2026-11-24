# JarVis Maps 0.1.1 — initial alignment correction

## Report and scope

The owner reports JarVis Maps 0.1.0 is displaced immediately after scanning A, not only after walking. No original room capture ZIP, published local room map, or new test recording is available to diagnose that physical result. Do not call the cause confirmed from source inspection alone.

## Confirmed source defect

Version 0.1.0 calculated a full camera-to-camera rigid transform, kept its translation, then replaced its rotation by a yaw-only rotation. With a nonzero roll/pitch residual and a map origin away from the camera, that replacement breaks the camera-position correspondence. It can displace the route from its first rendered frame.

Version 0.1.1 computes the yaw first, then calculates translation as `observedCamera.position - yaw * mapCamera.position`. The camera pair remains coincident, and shifting the stored map origin cannot independently shift the rendered world route. Results differing from gravity by more than 3 degrees are not shown.

## Independent confirmation

The second pose must use a newer camera frame and a changed observed viewpoint: at least 15 cm translation or 8 degrees rotation. Repeating the same view cannot increment 1/2 to 2/2. Both alignments must still pass the existing 25 cm / 8 degree consistency gate. These thresholds are acceptance policy, not physical error bounds; repeated geometry can still give a consistent wrong localization.

The observed camera pose comes from the metadata paired with the image, not a separate later transform read. Existing frame freshness, map/request identity, tracking epoch, reprojection, inlier and anchor gates remain in force.

## Validation and remaining work

`ProjectBootstrap.VisualMapReview.Run()` includes a fixture reproducing the old displacement, a corrected camera-correspondence check, shifted-origin invariance, upright floor, rejection of excess tilt/non-finite poses, and different-view acceptance/rejection. Build, UI and signing results belong in `QA-0.1.1.md` after completion, not before.

The PC reconstruction/localization code is unchanged by this alignment fix. The earlier synthetic scene and loopback HTTP tests validate plumbing only, not room accuracy. This version still requires PC processing and a real published map.

For a physical retest, install over 0.1.0 without clearing data, keep the same phone/map/lighting, scan A, face the captured room, obtain two different-view matches, and measure initial route displacement before walking. If wrong, export the original capture ZIP and supply a screen recording starting before A. Do not compensate by manually shifting arrows or assuming an iPhone will fix a transform error.
