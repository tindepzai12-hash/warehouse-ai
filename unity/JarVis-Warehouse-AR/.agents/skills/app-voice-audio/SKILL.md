---
name: app-voice-audio
description: "Implement and debug real microphone capture, original playback, voice delivery and optional STT/TTS."
---

# Voice and audio

## Purpose
Implement and debug real microphone capture, original playback, voice delivery and optional STT/TTS.

## Trigger
Microphone, recording, waveform, voice upload, audible playback, STT or TTS.

## Pre-check
Enumerate actual input devices and selected/default endpoint; do not assume device index zero is correct. Check OS/app permission, Editor mute, listener/source/output, sample capabilities and capture lifecycle. Determine local-only vs remote STT/AI and privacy/cost approval.

## Workflow
Use explicit Idle → Recording → Stopped → Finalizing → Sending → Processing → Completed/Failed states. Cancel sends nothing; Stop does not auto-upload. Use true sample cursor timing and PCM amplitude, encode the entire utterance once, validate header/rate/channel/duration/bytes and reject silence. Preserve an optimistic playable audio message; transcript is optional, never a second send. Coordinate one active playback source, original clip/cache ownership and opt-in TTS. Remote retries retain message ID and server receipts; define receipt retention/crash behavior.

## Safety Rules
No random waveform, fake duration, cropped first seconds, automatic mic capture or unapproved voice upload/provider spend. Do not hard-code a person's device name into portable code. Keep generated PCM test fixtures labeled.

## Verification
Verify full short/long capture, selected real device, deny/busy/unplug, cancel/stop/send, double tap, WAV envelope, pause/exclusivity, lost response retry and shared context. Separately require human speech, audible playback and target-device permission evidence.

## Failure Handling
Check capture → payload → HTTP → STT → context → UI → output in order. An advancing AudioSource timer does not prove sound. Keep audio on delivery failure and expose a specific safe error.

## Output
Chosen input/output, state transitions, audio bounds/privacy, exact verified layer and remaining human/device acceptance.
