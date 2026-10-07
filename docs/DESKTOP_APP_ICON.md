# Desktop app icon

Windows uses the same brighter, owner-supplied full-body Troll as the Android
app. This replaces the old desktop mark without changing application identity,
shortcuts or installer behavior.

- `Chummer/chummer.ico`: 32-bit frames at 16, 24, 32, 48, 64, 128 and 256 px.
- `Chummer/chummer6-icon-preview.png`: matching 512 px About-window image.
- Existing references cover Avalonia's EXE, both main windows, its published
  sidecar icon, the Blazor desktop fallback and Windows installers/shortcuts.
  Other desktop heads that already reuse these assets inherit the same artwork.

## Source and regeneration

Source: `ArchonMegalon/chummer-android`, icon revision
`0df057db01f51d4b962258a74277a926d35e4da7`.
Use `play/assets/app-icon-512x512.png`, SHA-256
`15b1c25ca30a288137ed4d7f5e913539e87cae30d4f3b8c56b0664242d951330`.
It is the raster export of the current brighter `appiconfg.svg`
(SHA-256 `1258f544b17111d1424dc42ea7dacba04dd9515cd4156d98236ee046a63cabb9`)
on the mobile app's `#102426` background. Do not use the older generated
`appiconfg.png` head. The Android adaptive-mask padding is launcher-specific.

From this repository, with ImageMagick and an explicitly selected Android checkout:

```sh
magick "$ANDROID_SOURCE/play/assets/app-icon-512x512.png" -strip -depth 8 \
  PNG32:Chummer/chummer6-icon-preview.png
magick Chummer/chummer6-icon-preview.png \
  -define icon:auto-resize=256,128,64,48,32,24,16 Chummer/chummer.ico
python3 -m unittest discover -s tests -p test_desktop_app_icon.py
```

The Android checkout is only needed for a deliberate artwork refresh, not for
normal desktop builds. Check the source hash before regenerating and visually
review all sizes before updating the asset hashes in the focused test.
This is an asset change, not evidence of a new Windows build or installation.
