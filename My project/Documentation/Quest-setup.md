# Meta Quest 3 / 3S

This integration targets a seated, standalone Quest 3 / 3S experiment using Unity 6000.6.0f1 and OpenXR.

## Build and install

The ready-to-install [development APK](../Builds/Quest/TechPorto-Quest.apk) was built and its signature verified on 15 September 2026. To install it, start at step 5. Steps 1–4 rebuild it after source changes.

1. Restart Unity after installing Android Build Support, Android SDK/NDK and OpenJDK through Unity Hub.
2. Allow Package Manager to resolve the pinned XR packages.
3. Choose **Experiment > Quest > Configure Android OpenXR**. This sets IL2CPP, ARM64, Vulkan, API 32 minimum / API 34 target, the OpenXR loader, Quest support, Touch / Touch Plus controller profiles and all three experiment scenes.
4. Choose **Experiment > Quest > Build Development APK**. Output: `Builds/Quest/TechPorto-Quest.apk`.
5. Enable developer mode on the headset, connect it by USB and accept its USB debugging prompt. Install the APK with Meta Quest Developer Hub, or run `adb install -r Builds/Quest/TechPorto-Quest.apk` using the platform-tools supplied with Unity.
6. Launch Tech Porto's app from the headset's developer/unknown-sources app list. This is a local development build, not a store release.

Application identifier: `org.techporto.experiment`. The existing Built-in Render Pipeline is retained.

## Participant and researcher controls

- The shared start screen offers **Non-VR version** and **VR version**. Choose Non-VR on your Mac and VR inside the Quest. Selecting the other option shows guidance and a Back button.
- Remain seated. Head movement controls the view; there is no artificial walking or turning.
- Point either controller at a button and squeeze its trigger.
- Alternatively use either thumbstick to move the highlighted selection, then A (right controller) or X (left controller) to confirm.
- **Edit ID** opens an in-world keyboard supporting uppercase letters and digits, Delete, Cancel and Done. IDs are limited to 32 characters. A generated ID is available without typing.
- Setup, condition-order selection, short-timer toggle, previews, surveys and restarting a finished session all work in VR.
- Headset sessions default to full-length timers. Turn on Test mode explicitly for a rehearsal. Desktop previews retain their existing defaults and keyboard/mouse controls.
- Instructions and surveys occupy a world-space panel in front of the seated viewpoint. The five original SAM drawings and the six response choices are retained.
- During the still phase, participants follow the instruction to face forward. The application never freezes or resets their tracked head pose.

## Session continuity and data

One XR rig remains alive throughout the host scene and both additive environments. Environment cameras provide seated viewpoint anchors; they and their audio listeners are disabled in VR. The headset camera and its listener stay active through transitions.

Focus loss, application pause or loss of a previously tracked/present headset pauses the study clock and audio and shows a Resume screen. Resume requires regained focus/presence. Interruption and resume events are recorded. A paused recording should be assessed by the researcher before inclusion in study data; pausing does not make a continuous EEG recording uninterrupted.

Responses are checkpointed to CSV after each response and on interruptions. A successful final save adds `SessionCompleted`. Partial sessions lack that event. Files are written through a temporary file and replacement. A final save failure offers Retry save and does not claim that data was saved.

CSV directory on Quest:

`/sdcard/Android/data/org.techporto.experiment/files/ExperimentData`

With the headset connected and USB debugging authorized:

```sh
adb pull /sdcard/Android/data/org.techporto.experiment/files/ExperimentData ./QuestExperimentData
```

Copy participant data off the headset before uninstalling the app. No automatic upload or email is configured.

## Validation

The desktop and simulated VR short-flow checks passed on 15 September 2026. Both checked the shared mode-selection screen, the alternate-device guidance and its Back button, then exercised the two environments and all 12 responses. The VR input check also passed controller ray/trigger, thumbstick navigation, A/X confirmation and study-clock pause/resume. CSV contents were checked for 12 unique ratings in the 1–6 range and a final SessionCompleted event.

Reports: [VR flow](Previews/Quest/VR-flow-report.txt), [controller interaction](Previews/Quest/VR-interaction-report.txt), [desktop flow](Previews/Quest/Desktop-flow-report.txt).

The final Android build and APK checks passed on 15 September 2026: [build report and checksum](Previews/Quest/APK-build-report.txt).

Previews: [desktop start screen](Previews/Quest/Desktop-Launch.png), [VR start screen](Previews/Quest/VR-Launch.png), [VR setup](Previews/Quest/VR-Setup.png), [SAM survey](Previews/Quest/VR-SAM.png), [closed-eye cue](Previews/Quest/VR-EyesClosed.png).

No physical headset was connected during these checks. They do not establish on-device frame rate, stereo correctness, comfort or EEG synchronization.

**Experiment > Quest > Preview VR Layout in Editor** shows the world-space arrangement without a headset. This is a layout preview, not a substitute for stereo and controller testing on hardware.

The editor flow checker exercises both environments, all 12 responses, duplicate-click protection and CSV output. In VR layout mode it additionally uses a synthetic Touch controller to exercise XRI ray/trigger selection, thumbstick navigation and A/X submission. It checks the single-camera/listener arrangement and tracked-pose preservation. Synthetic verification output stays under `Temp`, separate from participant storage.

Before recording participants, run the complete 3 / 3 / 5 / 5-minute sequence on the actual Quest, check both condition orders, text and SAM readability in both eyes, controller use from either hand, seated height, headset removal/resume, sound, CSV retrieval, and sustained frame timing in both scenes. Initial VR quality settings use 4x MSAA, one pixel light and a 35-metre shadow distance; physical profiling determines whether further scene optimization is necessary.

## Separate research integration

`ExperimentMarker.OnMarker` remains an event hook. This work does not add an LSL sender or an EEG recorder connection, and audio/display/EEG latency is not calibrated. The existing baseline countdown displays Begin briefly before the beep/marker, as documented in Brief-flow-review.md; this protocol timing was not silently changed by the VR conversion. Supplied environmental soundscapes remain optional and unassigned.

## Official references

- [Meta Unity setup](https://developers.meta.com/horizon/documentation/unity/unity-project-setup/)
- [Unity OpenXR Meta Quest support](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.18/manual/features/metaquest.html)
- [Unity XR Interaction Toolkit UI setup](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.6/manual/ui-setup.html)
