# Synesthesia Audio

A .NET MAUI media player for a locally stored Doujin audio library. It scans a folder of works,
reads metadata and artwork from DLsite, and plays the audio with per-track navigation, a queue,
and desktop and phone layouts.

## Building

```powershell
dotnet build "Synesthesia Audio/Synesthesia Audio.csproj" -f net9.0-windows10.0.19041.0
dotnet build "Synesthesia Audio/Synesthesia Audio.csproj" -f net9.0-android -c Release
dotnet test  "Synesthesia Audio.Tests/Synesthesia_Audio.Tests.csproj"
```

Requires the .NET 9 SDK plus the `maui`, `android`, `ios` and `maccatalyst` workloads.

## Translation

Titles and tags can be translated to English. The default provider is the free GTranslate chain
(Google, then Google's second endpoint, then Bing). You can supply your own paid key under
**Settings -> Translation**: DeepL, or a custom DeepL-compatible endpoint.

Rate limits are handled as a normal state rather than an error: when a provider returns HTTP 429
the app enters a cooldown, makes no further requests, and the Translate button tells you how many
seconds remain.

## Voice actor and circle names

Names are **never sent to a translation service**. They are romanised locally, because a machine
translation of a person's name is not a romanisation of it.

Romanisation uses a bundled kanji-to-romaji table derived from EDRDG JMnedict, overlaid with a
curated override file, plus katakana/hiragana conversion. A name that cannot be romanised is shown
as-is rather than dropped.

### Name data updates

The bundled name data is derived from JMnedict, which EDRDG updates continuously; shipping stale
data is a licence violation, so there is an update procedure:

1. **Automated refresh** - `.github/workflows/names-data.yml` regenerates the table weekly and
   publishes it as the `names-data` release asset. It only commits when the content actually
   changed.
2. **In-app silent refresh** - on launch the app checks `names-meta.json` and, if the published
   data is newer than what it has, downloads the table in the background, verifies its SHA-256 and
   uses it from then on. The bundled copy remains the offline fallback.
3. **Freshness shown in-app** - **Settings -> About & Sources** displays the creation date of the
   name data in use, and the licence notices.

The in-app check runs on any connection. There is deliberately **no Wi-Fi-only gating**: the check
itself is a few hundred bytes, and the actual table is about 1.4 MB, so it runs silently in the
background without interrupting playback.

## Licences and attribution

- Name data: EDRDG JMnedict, CC BY-SA 4.0, used in modified form (personal-name pairs only).
- GTranslate: MIT.
- Kawazu / IPADIC dictionary: MIT / NAIST.

Full notices are in [Synesthesia Audio/Resources/Raw/LICENSES.txt](Synesthesia%20Audio/Resources/Raw/LICENSES.txt)
and are shown in the app under **Settings -> About & Sources**, which the EDRDG licence requires to
be reachable from a menu rather than a splash screen.

Application code is licensed by the author.
