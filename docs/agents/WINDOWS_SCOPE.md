# Windows delivery scope

The user starts Friday Code on a separate Windows host. Continue the existing
`Chummer.Avalonia` desktop client, with `Chummer.Blazor.Desktop` as its documented
compatibility head. The controller retains Android, shared Core integration,
hosted services and provider operations. Windows work can progress independently
of Android releases and uses the Windows host's own build capacity.

## Delivery order

1. Build the pinned checkout on Windows. Launch the application, open or import
   a character, save it, close the process and reopen the saved character. Fix
   concrete Windows startup, interaction and persistence defects first.
2. Complete supported SR5 manual Creation and Career routes on Windows. Cover
   supported creation methods and archetypes, legal rules calculations, editing,
   resource spending, training, equipment and state changes. Verify cancellation,
   save/reopen and process restart for affected persistence routes. Shared rules
   come from Core packages; request an explicit shared change when necessary.
3. Complete Windows Origin reading/export and character-sheet PDF export, then
   portable per-character rule environments and amend-package workflows. Keep
   Chummer5a amend compatibility/import as the design target. Follow the existing
   rule-environment and portability specifications for character-bound rules,
   synchronization, missing-package recovery and conflict handling.
4. Produce the existing Windows installer/update artifact and verify installation,
   relaunch and recovery on Windows. Distinguish unsigned development artifacts,
   signed artifacts and published releases. Ask the controller for any required
   signing or release authority through the assigned task.

The complete SR5 AI Finish redesign is developed in parallel by the controller.
Its new-request action stays hidden until complete legal behavior across supported
archetypes and native review/apply/save/recovery are verified. Do not expose an
older partial implementation on Windows. SR6 and Linux retain their separate
delivery stages; they are not prerequisites for the first Windows increment.

Read `.codex-design/product/DESKTOP_CLIENT_PRODUCT_CUT.md`,
`.codex-design/product/DESKTOP_PLATFORM_ACCEPTANCE_MATRIX.yaml`,
`.codex-design/product/RULE_ENVIRONMENT_AND_AMEND_SYSTEM.md`, and
`.codex-design/product/INTEROP_AND_PORTABILITY_MODEL.md` for the existing design.
`config/product-requirements.reference.json` preserves the controller's 62
requirement identities and wording as a reference snapshot. Some requirements
are Android-specific or belong to later stages. The current Teable assignment
defines the Windows task and acceptance scope; the reference contains no claim
that an item is implemented, tested or delivered.

## Ownership and integration

Work on a dedicated `windows/...` branch from the assigned base. Preserve existing
work when resuming. Windows-specific UI, adapters, packaging and tests belong to
this lane within the exact assigned paths. Core rules, persistence authority,
shared Presentation APIs, package versions and contract changes need a controller
assignment. Submit the affected paths, problem, proposed change and Android
impact with the coordination client's `request` command; continue independent
assigned Windows work while the controller responds.

Own-branch commits and pull requests for assigned work are authorized. The
controller coordinates protected integration and shared package publication.
Do not force-push, change accounts/providers, operate Android signing/Play, or
restart hosted services as part of a Windows task.

Report the actual commit, Windows OS/runtime, build command, relevant tests,
artifact location and remaining blockers in Teable. A source patch, Linux
cross-build or passing managed harness is not a Windows launch/installation
result. Report incomplete routes explicitly and retain working user data.
