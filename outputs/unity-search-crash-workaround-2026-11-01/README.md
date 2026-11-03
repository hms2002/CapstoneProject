# Unity Search crash workaround — 2026-11-01

- Mode / authority: Implementation; user requested fixing the Play-button crash without changing Unity version.
- Scope: local Search configuration and generated cache, reversible backups, native Editor verification. No gameplay, package, engine-version or persistent project-setting change.
- Version retained: **6000.4.2f1 (7a4c1aeef971)**.

## Evidence and changes

The original crash occurred during Play-mode assembly reload: `SearchTask 'Update' thread did not stop within 5 seconds`, followed by `mdb_cursor_sibling` / `LMDBIndexStorage::RemoveDocuments` / `MergeArtifactsBatch`. See [crash excerpt](crash-stack-excerpt.txt). This matches the stack in [Unity UUM-141720](https://issuetracker.unity.com/issues/11928/crash-on-mdbcursorsibling-when-performing-various-actions-in-the-editor).

| Location | Change / purpose |
|---|---|
| `UserSettings/Search.index` | `roots: ["Assets"]` → `roots: []`, removing assets from the background search index. |
| `UserSettings/Search.settings` | `indexOnEditorStartup = false`, preventing automatic startup indexing. |
| `Library/Search` | Original cache moved intact to `backup/Search-cache/`; no asset database or imported game assets deleted. |
| `.gitignore` in this evidence directory | Keeps the large cache, saves and local diagnostic backups out of Git. |

Empty search roots follow [Unity staff's workaround](https://discussions.unity.com/t/search-asset-index-keeps-rebuilding/932231?page=2). This avoids the problematic indexing path; it does not patch the engine's native implementation. Both settings are local and already excluded from Git by the repository.

## Verification

- Same installed Editor, actual TitleScene and native Play button; **three Play → title-rendered → Stop cycles** completed. No test-only isolation flags or error suppression used.
- Log: three Play assembly reloads, three title-runtime diagnostic milestones, three save-on-stop messages; **zero SearchTask timeouts, native crash markers or C# compiler errors**.
- Project search for `TitleScene` returned the scene, BGM and start-input script; search cleared afterward.
- No LMDB asset-index files were recreated; remaining Search bookkeeping totalled **2,984 bytes**.
- Editor closed normally. Six saves and the generated prewarm trace restored to fresh pre-test snapshots. All **51 protected file hashes** match, including version, package files and preexisting modified assets/scripts/settings.
- Native asset reserialization was cleaned up: GlobalUIRoot restored from an existing Git object only after its SHA256 exactly matched the pre-test hash; 13 originally clean Monster prefabs had no semantic Git diff and were returned to their original checkout formatting. No new scene/prefab change remains.
- Details: [verification record](verification.json). Unit tests, MSBuild and Player build were not run; no C# source was changed.

## Limitations / Suggested Later

- Indexed property/dependency searches are temporarily unavailable or limited. Ordinary Project name search was verified.
- Three transitions cannot prove that every native crash is eliminated. Keep indexing disabled while using this workaround.
- The existing, separate `PrewarmTraceRuntime.Flush` Windows error 1224 recurred during verification (2,967 messages). This is a generated trace-file write problem, not the original LMDB crash; it still requires a scoped code fix. This run is not a clean-Console acceptance result.

## Recovery

With Unity closed, restore `backup/00_Search.index` and `backup/01_Search.settings` to their original `UserSettings` paths to reverse these settings. This re-enables the crash-prone indexing path on this version. The old cache is retained for diagnosis; it is not required to rebuild an index. Save/config paths and original hashes are in local `before.json`.

Doc Impact: SessionLog + ErrorLog. DecisionLog unchanged; no StructureMemory or Architecture/Contracts change needed, and no Presentation HTML stale candidate identified.
