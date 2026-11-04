# Monster HP pixel artwork

`monster_hp.aseprite` is the approved layered source. Frame40×8, track/fill/trail36×4 at offset(2,2), six opaque colors. `make_assets.lua` reproduces the initial concept; it is not an automatic import hook.

After editing the Aseprite source, use Aseprite batch mode with `export_sheet.lua`, passing `source=<absolute .aseprite path>` and `output=<absolute parent MonsterHealthBar directory>`. It copies the four layers into `MonsterHealthBar.png` without recoloring. `referenceDir=<absolute output directory>` optionally exports four source states for native comparison.

Run Unity's existing **Tools/Authoring/Upgrade Monster HP To Instanced Mesh** command to import the four named sprites and update only the shared HP presentation. The shader and overlapping UI Images use the same sheet. The2-pixel frame border is scaled with the existing10-unit HUD height; the middle widens for the75/100/140-unit widths. Current health and damage trail still control their displayed width.

Source colors: `000000 8F8C91 060508 CC0614 FF2B3B FFFFFF`. The shader samples these image pixels; it does not recreate their colors procedurally. Both rendering paths and the approved source are compared by `MonsterPixelHealthBarValidation`. Monster health, status, groggy and damage-trail timing remain owned by the existing runtime systems.
