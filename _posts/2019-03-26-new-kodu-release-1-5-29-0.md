---
title: New Kodu Release (1.5.29.0)
description: New features and bug fixes.
layout: post
published: true
show_sidebar: true
author: "scoy"
---

test 13

## New Features

* Add MaxHealth, BlipDamage, and MissileDamage tiles.  Their values can be set and read just like any score tile.

* Updated Damage and Heal tiles to support full range of scores for their values rather than just numbers.

* Add OverPath tile.  This allows you to sense when a character is over a path.  Does not trigger when character is jumping.

* Change OnWater and OnTerrain tiles to both return false when the character is jumping.  A typical use for this would be to have the character die when touching water (or lava) but allowing them to jump over it safely.

* The MouseOver detection now also works for the onscreen GUI Buttons.

* World and private scores can now appear in Say commands much like the color scores can.  To display the current value of world score A use \<score A>.  To display the current value of private score b use \<private score b>.

* Add new settings slider tiles from controlling BlipReloadTime, BlipRange, 
MissileReloadTime, MissileRange, CloseByRange, FarAwayRange, and HearingRange.  These can now all be adjusted via programming during gameplay.

* Accented characters in Spanish now work properly with the Spanish keyboard.  For instance, typing ‘a now results in á.

* Micro:bit 1.5 is now supported!

* The edit/run cycle of complex levels should now be faster.  Previously moving from run mode back into edit mode caused the full terrain to be reloaded.  This no longer happens and so, should be much quicker.

* In game, when a full screen text dialog is displayed, we now wait half a second before accepting input.  This is to help prevent accidental closing of the dialog.
  
## Bug Fixes

* Fixed Rover’s rotation when scanning rocks.

* Increase friction for all characters so they don’t coast quite as much.

* Fixed TurnSpeed and MoveSpeed programming tiles.

* Fixed bug in terrain sensor when used over smooth terrain.

* Fixed a bunch of bugs related to movement when frame rate is very low (<10).  This caused characters to fall through the ground, to get stuck when moving, to bounce oddly, to overshoot when turning, and to be too twitchy to control easily.

* Fixed bug with blips not hitting some characters.  Problem was related to the order that the characters were added to the world.

* Fixed a bug in the Load World Menu when changing the sort order.

* Fixed bug in input arbitration which was causing unused inputs to sometimes block active ones.  For instance, if you programmed WHEN GamePad LeftStick DO Move  above WHEN Keyboard Arrows DO Move, the GamePad line would block the input from the keyboard line even when no gamepad was being used.

* Fixed a bug where rules which were first indented and then unindented would sometimes not be evaluated correctly since the system still thought they were indented.

* Fixed bug with detecting Bumped Missiles colored other than white.

* Fixed hearing range to be linear between 0 and 100 meters.  Setting to full is the same as setting to infinity.  By the way, the square terrain tiles are ½ a meter on a side, so 10 meters equals 20 blocks.

* Fixed missiles show by the GUI buttons to just fly forward.  Previously they flew toward to world origin.

* Fixed bug with GUI button labels where they would persist across worlds even when deleted.

* Fixed bug preventing Alt+NumPad special characters from being added.

* Fixed a bug in the scoring where sometimes the private scores from the target object where being used rather than the private scores from the object running the code.

* Fixed bug with scores set to Quiet being reset to Loud on reload of level.