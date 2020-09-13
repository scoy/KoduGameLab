---
title: Kodu Update
layout: post
published: true
show_sidebar: true
author: "scoy"
---

New Kodu update.

The primary reason for this release is for some internal changes to ensure that we’re fully in compliance with all security and privacy requirements.  As part of this we’ve added more obvious warnings about not using your real name, especially your last name, when choosing a creator name.  If you are sharing your worlds with the community your creator name is public.  Picking a creator name that can’t be tied back to you is important for your safety and privacy.

Pipes have long had issues with how they are rotated and placed.  It turns out that the code causing them to snap to the nearest 45 degrees was buggy and only worked with certain rotations.  This has been fixed but it will cause pipes in older levels to appear rotated.  These will need to be fixed in the world editor.  Once fixed they will be fine.

I rewrote much of the code for putting buttons on the screen in a game.  The functionality should be the same, but they will no longer cause such a large performance hit.  Also made smaller performance improvements in other areas.

Cleaned up the turning code a bit.  At times it could lead to bots turning further than they should have.  Also made some changes so that programming WHEN Gamepad LeftStick DO Turn matches the results of WHEN Gamepad Leftstick DO Move.  Previously the turning rate was different depending on how you programmed it.

## Bug Fixes

* Fixed bug related to turning when programmed as user controlled using either WASD or arrow keys.

* Fixed a bug that happened while painting terrain.  Occasionally the cursor would jump back to the world origin.

* Fixed a bug where the hit points on a character were not being set correctly.  This only occurred upon freshly starting Kodu, adding a new character, and then running for the first time.