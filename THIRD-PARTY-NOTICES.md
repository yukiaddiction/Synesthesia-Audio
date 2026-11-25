# Third-party notices

The full notices ship inside the app (`Synesthesia Audio/Resources/Raw/LICENSES.txt`) and are shown
under **Settings -> About & Sources**.

| Component | Licence | Used for |
|---|---|---|
| EDRDG JMnedict | CC BY-SA 4.0 | Bundled kanji -> romaji name table (modified: personal-name pairs only) |
| GTranslate | MIT | Machine translation of titles and tags |
| Kawazu | MIT | Japanese romanisation |
| IPADIC dictionary (NAIST) | NAIST/IPA licence | Dictionary shipped with Kawazu |

## CC BY-SA data

`Synesthesia Audio/Resources/Raw/name_readings.tsv.gz` is a **derived work**: a reduced set of
kanji -> romanisation pairs for personal names extracted from JMnedict. It is distributed under
CC BY-SA 4.0, not under the application licence, and no copyright is claimed over it.

Because EDRDG requires bundled data to stay current, an automated refresh procedure exists
(`.github/workflows/names-data.yml` plus the in-app silent refresh). See `README.md`.

Application code is licensed by the author and is not affected by the ShareAlike term, which
reaches the data rather than the surrounding program.
