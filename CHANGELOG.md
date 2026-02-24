# Changelog

All notable changes to ALttP Enhancement Tools are documented here.

---

## [1.9.0] — 2026-02-24

### Added
- **Sprite browser card grid** — 600+ community sprites displayed as thumbnail cards with name and author; replaces the old plain list
- **Animated triforce loading indicator** — each sprite card shows a cascading triforce outline animation while its preview image loads
- **Sprite favorites** — star button on each card pins sprites to the top of the list; favorites persist across sessions in `%LocalAppData%\LTTPEnhancementTools\sprite_favorites.json`
- **Offline sprite browser** — sprite list and all preview images are cached to disk after first load; the browser opens instantly and works fully offline on subsequent uses
- **↻ Refresh button** — re-fetches the sprite list from alttpr.com when you want to pick up newly added sprites
- **Default Link preview** — Link's sprite thumbnail is shown in the main window when no custom sprite is selected; downloaded and cached automatically on startup
- **"Link" resets to default** — selecting the default "Link" entry in the sprite browser clears any custom sprite selection

### Fixed
- Sprite preview images in the browser were never loading (WPF's `BitmapImage.UriSource` silently fails for HTTPS in .NET 8); fixed by downloading via `HttpClient` and loading from `MemoryStream`
- Main window sprite preview was always blank (a `MemoryStream` was disposed before `BitmapImage.EndInit()` was called — the stream scope closed inside the `if/else` block but `EndInit()` was called after)
- Stale web preview URLs saved to auto-state before caching completed (e.g. after a crash) now re-download and cache correctly on next launch

---

## [1.8.0] — 2025-01-xx

### Added
- Launcher UI redesign
- Playlists support
- Auto-save of last session state (ROM, sprite, playlist)

---

## [1.7.0]

### Added
- First-run setup wizard (4-step: emulator, connector, SNI, tracker)
- Separate launch settings file (`launchSettings.json`) independent of app settings
- Sprite-only apply mode (no music tracks required)
- Dark theme ComboBox style fix

---

## [1.6.1]

### Fixed
- Library play button not working
- Save/Load config dialogs now default to the app's `Configs/` folder

---

## [1.6.0]

### Added
- Track search — filter the 61-slot list by name
- Library slot header
- Clear All button
- Overwrite by default behavior

---

## [1.5.0]

### Added
- Dedicated output folder
- Launch ROM button
- Sprite-only apply
- Sprite image display fix

---

## Earlier versions

See git history for changes prior to v1.5.0.
