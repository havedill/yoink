<p align="center">
  <img src="assets/banner-minimal.svg" alt="Yoink" width="95%"/>
</p>

<p align="center">
  <strong>Yoink: All-in-one open-source ShareX alternative</strong>
</p>

<p align="center">
  <em>Fork of <a href="https://github.com/jasperdevs/yoink">jasperdevs/yoink</a> with UX improvements for the capture overlay workflow.</em>
</p>

<p align="center">
  Capture, annotate, OCR, translate, make stickers, record video, save locally, search images with OCR, and many more features.
</p>

<p align="center">
  <a href="https://github.com/havedill/yoink/releases/latest">
    <img src="https://img.shields.io/github/v/release/havedill/yoink?style=flat-square&color=1962F4" alt="Release" />
  </a>
  <a href="https://github.com/havedill/yoink/releases">
    <img src="https://img.shields.io/github/downloads/havedill/yoink/total?style=flat-square&cacheSeconds=300" alt="Downloads" />
  </a>
  <a href="https://github.com/havedill/yoink/stargazers">
    <img src="https://img.shields.io/github/stars/havedill/yoink?style=flat-square" alt="Stars" />
  </a>
  <a href="https://github.com/havedill/yoink/blob/main/LICENSE">
    <img src="https://img.shields.io/github/license/havedill/yoink?style=flat-square" alt="License" />
  </a>
</p>

<p align="center">
  <a href="https://github.com/havedill/yoink/releases/latest">
    <img src="https://img.shields.io/badge/windows-download-1962F4?style=for-the-badge&logo=windows&logoColor=white" alt="Download for Windows" />
  </a>
  <img src="https://img.shields.io/badge/macos-planned-6b7280?style=for-the-badge&logo=apple&logoColor=white" alt="macOS Planned" />
  <img src="https://img.shields.io/badge/linux-planned-6b7280?style=for-the-badge&logo=linux&logoColor=white" alt="Linux Planned" />
</p>

<p align="center">
<img width="947" height="490" alt="image" src="assets/screenshot-main.png" />
</p>

## Fork Changes

This fork is based on [jasperdevs/yoink](https://github.com/jasperdevs/yoink) v0.8.3.2 and includes the following changes:

### Print Screen hotkey support
The Windows `RegisterHotKey()` API cannot intercept the Print Screen key because the OS handles it at a lower level. This fork adds a low-level keyboard hook (`WH_KEYBOARD_LL`) that catches Print Screen before Windows processes it, so it can be used as a capture hotkey.

### Capture overlay UX improvements
- **"Near mouse" toolbar dock position** -- Added alongside the existing Top/Bottom/Left/Right options in Settings → Capture dock. When selected (now the default), the annotation toolbar spawns ~150px to the top-right of where the cursor was when capture was triggered, instead of docking to a screen edge. Cursor position is captured on the main thread at hotkey press to avoid race conditions on multi-monitor setups. Users who prefer a fixed dock can still pick Top/Bottom/Left/Right.
- **Annotation tools flyout opens by default** -- The "more tools" flyout (arrow, text, blur, draw, etc.) opens automatically when the capture overlay appears, so tools are immediately accessible without clicking the "..." button.
- **Smarter Escape key behavior** -- Escape now follows a priority chain: (1) cancel an active operation (mid-drag, popup, typing), (2) return to the last capture tool if currently using an annotation tool, (3) close the overlay. In rectangle/freeform select mode, Escape exits the overlay in a single press instead of requiring two.
- **Keyboard focus fix** -- The overlay re-acquires keyboard focus after toolbar creation, fixing an issue where Escape and other keys would not register on the first capture.
- **Annotation color memory** -- The color you pick for annotations (toolbar swatch) is saved in settings and comes back on the next capture, so you do not have to re-select it every time.

### Upload-from-preview button
The snapshot preview toast now includes an always-visible **Upload** pill button in the lower-left corner. Clicking it kicks off an upload to your configured destination and swaps the preview for a pinned "Uploading to {host}…" status toast, which is then replaced by the usual success toast (URL copied to clipboard) or an error toast. This replaces the removed "auto-upload after capture" option: uploads are now explicit, one-click, and opt-in per capture.

### Auto-upload removed
The "Auto-upload screenshots / GIFs / videos" settings have been removed entirely. Automatically sending every screenshot to a remote host was too easy to trip over (accidentally shipping sensitive content to Imgur, getting rate-limited silently, etc.). Uploads are now only triggered by the explicit Upload button on the preview toast.

### Imgur anonymous uploads work without a Client-ID
Imgur ships with a built-in public Client-ID for anonymous uploads (~1,250 uploads/day shared across all Yoink users). Previously the upload flow would refuse to start with "No API key configured" even though the fallback was in place; now selecting Imgur as an upload destination works out-of-the-box without entering a personal Client-ID. Users who want their own rate-limit bucket can still configure one in Settings → Uploads.

### Auto-update pointed to this fork
Auto-update checks now point to `havedill/yoink` releases instead of the upstream repository.

---

## Download

Grab the latest release from the [**Releases page**](https://github.com/havedill/yoink/releases/latest).

> Upstream releases are available at [jasperdevs/yoink](https://github.com/jasperdevs/yoink/releases/latest).

## Why Yoink

- Region, fullscreen, active-window, and scrolling capture
- Built-in annotation tools (arrows, text, shapes, blur, freehand) with stroke/shadow effects
- OCR text extraction with built-in translation (Argos Translate offline, Google Translate API)
- Auto-download language packs for 100+ OCR languages
- Color picking, QR/barcode scanning, ruler, and step numbering
- Sticker creation with background removal
- Screen recording (GIF, MP4, WebM, MKV) with microphone and desktop audio
- Local history with image search using OCR and semantic matching
- Optional uploads to 15+ services including Imgur, S3, Dropbox, and self-hosted targets

## Stickers

Yoink can turn captures into stickers by removing the background, then saving, previewing, copying, and uploading them like normal images.

<p align="left">
  <img src="assets/sticker-showcase.png" alt="Before and after sticker example" width="92%" />
</p>

- Cloud sticker providers: `remove.bg`, `Photoroom`
- Local sticker models: `U2Netp`, `BRIA RMBG`
- Optional sticker finishing: drop shadow and white stroke

## OCR & Translate

Yoink can extract text from any region of your screen and translate it instantly. OCR results open in a dedicated window where you can edit, copy, or translate the text.

<p align="left">
  <img src="assets/screenshot-ocr.png" alt="OCR result window with translation" width="60%" />
</p>

- Extract text from screenshots with Tesseract OCR
- Auto-download language packs for 100+ languages
- Translate with Argos Translate (offline) or Google Translate API
- Dedicated result window with copy and translate buttons

## Search

Search your image history by filename, OCR text, and semantic matching, so you can find screenshots by what they say or by what they show.


<p align="left">
  <img src="assets/screenshot-search.png" alt="Searching image history with OCR and semantic matching" width="60%" />
</p>


- Search by text inside the image with OCR
- Search by semantic similarity to find visually related screenshots

## Default hotkeys

| Action | Hotkey |
|---|---|
| Screenshot | `Alt + `` ` |
| OCR | `Alt + Shift + `` ` |
| Color picker | `Alt + C` |
| QR/barcode scanner | `Unassigned` |
| Sticker | `Unassigned` |
| Fullscreen capture | `Unassigned` |
| Active window capture | `Unassigned` |
| Scroll capture | `Unassigned` |
| Ruler | `Unassigned` |
| Record | `Unassigned` |
| Annotation tools | `1-9`, `0`, `-`, `=`, `[`, `]`, `\` |

Annotation tool hotkeys can be configured in settings, and hover tooltips reflect the real assigned key.

## Uploads

Yoink can upload screenshots, stickers, and recordings after capture. Upload targets include:

- Public hosts like `Imgur`, `ImgBB`, `Catbox`, `Litterbox`, `Gyazo`, `file.io`, and `Uguu`
- Cloud targets like `Dropbox`, `Google Drive`, `OneDrive`, `Azure Blob`, and `S3-compatible storage`
- Self-hosted and developer targets like `GitHub`, `Immich`, `FTP`, `SFTP`, `WebDAV`, and `Custom HTTP`

Availability depends on the target service and your credentials.

Sticker uploads use the same upload destinations as normal image uploads.

## Build from source

Requires [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

### Quick publish

```
git clone https://github.com/havedill/yoink.git
cd yoink
dotnet publish src/Yoink/Yoink.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release
```

This writes the self-contained single-file app under `release/`. GitHub Actions uses the folder name `publish/` instead; both are valid—only the `-o` path differs.

### Build script (publish + zip)

From the repo root, Windows PowerShell:

```powershell
.\scripts\build.ps1
```

Optional: `-Rid win-x86` / `win-arm64`, `-SkipTests` if you need to bypass tests. Published files go to **`release/`** by default; the zip is **`dist/Yoink-{version}-win-x64.zip`** (version comes from `src/Yoink/Yoink.csproj`).

### What’s in the zip (and why you see `Assets`)

`PublishSingleFile` bundles the main executable and dependencies into **`Yoink.exe`**, but the publish output is **not** literally one file. The project copies **`Assets/Clip/**`** next to the exe (local CLIP / semantic image-search runtime files loaded from disk). **Ship the extracted folder as-is**—do not distribute only `Yoink.exe` without that `Assets/Clip` tree or bundled image search will not find its models.

Provider icons under **`src/Yoink/Assets`** (PNG/SVG) are WPF **resources** compiled into the app; they are not loose files in the zip. The top-level **`assets/`** folder in the repo (banner, screenshots for this README) is **not** part of the application build.

## Acknowledgments

This project is a fork of [jasperdevs/yoink](https://github.com/jasperdevs/yoink), licensed under [GPL-3.0](LICENSE). All credit for the core application goes to the original authors.
