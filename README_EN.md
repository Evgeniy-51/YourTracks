# YourTracks (for Windows)


YourTracks helps you organize your music collection quickly and easily. Stop wasting time on repetitive work — editing metadata file by file by hand.

With YourTracks you can:

1. Edit metadata for any individual track.
2. Add or update metadata for an entire album at once.
3. Rename a file directly in the app.
4. Batch-rename files using a custom template.
5. Update track-number prefixes in one click by reordering tracks and changing the prefix format.
6. Automatically add track-number prefixes across a folder, using either tag values or the order in your editing list.
7. Remove track-number prefixes from a group of files or an album in one action when needed.
8. Add or replace cover art for a single track or the whole album.
9. Build a playlist by dragging tracks in the list and export it as m3u8, m3u, or pls.
10. Save changes in place or write copies to a separate folder.
11. Switch the UI language between Russian and English.

**Version:** v0.1

## Screenshots

![Main window](docs/screenshots/yourtracks_pic1.jpg)

![Metadata editing](docs/screenshots/yourtracks_pic2.jpg)

## Features

- Open a folder or individual files (MP3, FLAC, M4A)
- Edit artist, title, track number, album, year, and cover art
- Apply changes to selected files or the entire list
- Rename files with templates (`{artist}`, `{title}`, `{album}`, `{year}`, `{track:00}`, …)
- Renumber tracks sequentially by list order or existing tags
- Save in place or export copies to another folder
- Generate M3U playlists
- Russian and English UI

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — to build from source
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) — for framework-dependent builds

## Build and run

```powershell
dotnet build MetadataEditor.sln
dotnet run --project src/MetadataEditor.App/MetadataEditor.App.csproj
```

## Tests

```powershell
dotnet test MetadataEditor.sln
```

## Release

Framework-dependent build (requires .NET 8 Desktop Runtime on the target machine):

```powershell
dotnet publish src/MetadataEditor.App/MetadataEditor.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o publish/win-x64
```

Self-contained build (includes the runtime):

```powershell
dotnet publish src/MetadataEditor.App/MetadataEditor.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o publish/win-x64-selfcontained
```

The executable is `YourTracks.exe` in the output folder.

## Project layout

```text
MetadataEditor.sln
src/
  MetadataEditor.App/     WPF UI (MVVM)
  MetadataEditor.Core/    metadata I/O, templates, save logic
tests/
  MetadataEditor.Core.Tests/
```

## Settings

User settings (language, window geometry) are stored in:

`%AppData%\YourTracks\settings.json`

## License

MIT — see [LICENSE](LICENSE).
