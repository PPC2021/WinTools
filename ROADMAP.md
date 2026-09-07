# WinTools Roadmap

## Phase 1: Stability and Usability
- Add persistent settings for schedule, break duration, and run state.
- Improve status messaging with clearer active/paused/out-of-hours indicators.
- Add graceful startup validation for invalid time ranges and empty day selections.

## Phase 2: Productivity Features
- Add profile presets (Office Hours, Night Shift, Custom) for quick switching.
- Add configurable keep-alive pulse interval and activity simulation mode.
- Add tray icon support with quick actions (Start/Stop, Break, Exit).

## Phase 3: Reliability and Packaging
- Add automated tests for `WorkingHoursSchedule` and idle-time formatting logic.
- Add CI workflow for build verification on pull requests.
- Add release packaging for x86 Windows distribution with versioned artifacts.
