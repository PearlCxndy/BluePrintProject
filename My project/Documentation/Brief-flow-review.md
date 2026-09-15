# Brief and diagram compared with the Unity flow

This is the historical 10 September review. For the subsequent Quest 3 / 3S
integration and its validation status, see [Quest setup](Quest-setup.md).

Reviewed 10 September 2026 against `Unity_brief_for Tech Porto Sept 2026.docx`
and the supplied flow diagram. The similarly named `~$ity_brief...docx` is
Word's temporary lock file, not the full brief.

## Overall sequence

The implemented sequence matches the intended two-condition experiment:

Welcome → red-cross baseline → eyes-closed baseline → baseline SAM →
experiment introduction → first condition → its survey → second condition →
its survey → thank-you screen and CSV export.

There is also an experimenter-only setup screen before the welcome.
Both conditions run once; choosing a starting condition does not omit the other.

| Stage | Brief / diagram | Current implementation |
| --- | --- | --- |
| Eyes open | Red cross, 3 minutes | 180 seconds in normal mode; instructions, countdown and beeps |
| Eyes closed | Closed-eye cue, 3 minutes | 180 seconds in normal mode; automatically advances to baseline Valence |
| Baseline SAM | Valence then Arousal, 1–6 | Two separate screens; selecting a response advances automatically |
| Condition order | Nature then City, or City then Nature | Random starting condition by default; researcher can choose either order |
| Each condition | 5 minutes | 60 seconds looking around + 240 seconds still; prompts and countdown are inside those 300 seconds |
| Post-condition survey | SAM plus aesthetic judgments | Valence → Arousal → Liking → Beauty → Wanting, all 1–6 |
| Finish | Thank-you screen; save responses | Thank-you screen and CSV with named responses/events; 12 responses total |

## Important differences and remaining work

- **Test mode is enabled by default.** It uses 8-second baselines and
  12-second conditions. Uncheck it on setup for the diagram's 3/3/5/5-minute
  timed blocks (16 minutes total, excluding instructions and surveys).
- **Survey ordering:** the diagram lists Beauty before Liking. The detailed
  brief specifies Liking before Beauty, which the game follows. Survey-question
  order is fixed; only the two condition-plus-survey blocks change order.
  Random order is a per-session random choice, not enforced equal allocation
  across participants.
- **Brief timing inconsistency:** one instruction says 3.5 minutes still,
  but the later timing requirement says 4 minutes after the first minute.
  Unity follows the latter, giving 5 minutes per condition.
- **Baseline start timing is not exactly as described:** the word “Begin”
  appears 0.6 seconds before the beep and start marker in normal mode
  (0.35 seconds in Test mode). The timer and marker start at the beep.
  The brief requests simultaneous “Begin” and beep. No timing code was
  changed as part of this review.
- **EEG/LSL is not connected:** named events are recorded locally and an
  `ExperimentMarker.OnMarker` callback exists, but there is no LSL sender or
  verified EEG stream. Audio/EEG synchronization has not been measured.
- **Quest 3/controller interaction is not integrated:** the current UI uses
  a screen-space desktop canvas, mouse and keyboard. A headset rig, controller
  interaction, and on-headset testing remain necessary for the brief's VR setup.
- **Soundscapes are optional and unassigned:** the scenes support an audio clip,
  but currently provide no environmental recording. Countdown/end beeps exist.

## Closed-eye cue update

The existing closed-eye symbol is now opaque white instead of black, with
font size increased from 92 to 180 and its layout box enlarged from 480×120
to 800×260 reference-canvas units. It stays static against the dark background.
No baseline duration, survey order, SAM artwork, or participant data was changed.

## Evidence and validation scope

This comparison is based on the full brief, `ExperimentController.cs`,
`ExperimentConfig.cs` and its saved asset, `ExperimentUI.cs`,
`ExperimentLogger.cs`, `ExperimentMarker.cs`, and `Packages/manifest.json`.
The prior short-flow report in `Previews/flow-report.txt` confirms the earlier
desktop flow exercised both environments, all 12 ratings, duplicate-click
protection and CSV export. It is not a fresh full-duration or headset test.
The flow checker now also checks the closed-eye cue's size and white color
and captures it when it reaches that baseline.
