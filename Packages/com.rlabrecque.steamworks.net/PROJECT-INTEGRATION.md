# Capstone integration

Embedded Steamworks.NET 2025.163.0 runtime package, reconstructed from the user's
`Steamworks.NET_2025.163.0.unitypackage` in Downloads (Steamworks SDK 1.63).
Upstream runtime sources, native plugins and their supplied metadata are unchanged.
The MIT license is included in this package.

The upstream Editor folder and external SteamManager sample are intentionally
omitted: RedistInstall automatically writes test App ID 480 and modifies project
defines. Platform guards in the runtime sources already support Unity desktop
targets. Lifecycle ownership is instead `SteamPlatformService` under the existing
`[RuntimeServices]` root. Updating this package must preserve this integration choice.

No App ID is committed. For local editor testing, create `steam_appid.txt` in the
project working directory containing your actual numeric App ID (UTF-8, no BOM),
then start Steam and enter Play Mode. The file is ignored by Git and must not ship
in Steam depots. Without it the editor skips Steam initialization.
Steam-launched desktop builds obtain their identity from the Steam launch context.
Directly launched builds can use a development App ID file next to the executable
in the working directory. Initialization failure keeps normal gameplay available.

This slice does not restart the game through Steam, require Steam for gameplay,
or implement achievements, cloud saves, stats, multiplayer, or purchase checks.

Sources: https://steamworks.github.io/installation/
and https://partner.steamgames.com/doc/sdk/api
