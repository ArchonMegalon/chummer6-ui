## External audit: Windows proof installers are not acceptable release evidence

Recorded: 2026-04-12T21:24:17Z

Live audit summary:

- `avalonia-win-x64-installer` and `blazor-desktop-win-x64-installer` are proof-only binaries
- both binaries are unsigned
- Avalonia runtime result: process returns quickly with exit code `1`
- Blazor runtime result: launches into a black window
- user feedback: Avalonia still does not read like Chummer5a

Interpretation:

- Windows proof distribution is not a release-quality substitute for promoted Windows proof
- Blazor currently has a concrete Windows runtime defect
- Avalonia currently fails the product's flagship familiarity target even aside from release/signing posture

Required remediation:

1. Fix the Blazor Windows black-window startup path.
2. Re-run real Windows startup-smoke and retain the receipt in the published proof lane.
3. Stop treating unsigned proof-only Windows binaries as evidence of desktop closeout.
4. Run a real flagship UI pass on Avalonia against the Chummer5a familiarity bridge, dense-workbench budget, and veteran first-minute gate.

This packet is blocking, not advisory.
