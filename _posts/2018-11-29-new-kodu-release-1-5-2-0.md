---
title: New Kodu Release (1.5.2.0)
subtitle: New features and bug fixes.
layout: post
published: true
show_sidebar: true
author: "scoy"
---

A few new features and a bunch of bug fixes.  Have fun!

## New features:

* Completely changed how movement is handled internally.  For the most part I’ve tried to keep the behavior of the characters the same or at least close to the same as they previously were.  Probably the most obvious effect of the changes is that we can now handle more complicated movement scenarios such as “mouse-look”.  Mouse-look is the name given to the control scheme used by most first person shooters.  Typically, the WASD keys are used to control relative movement while the mouse is used to control turning and shooting.  For example:![Mouse Look](https://scoy.github.io/KoduGameLab/images/mouse_look.jpg)
<br>In this example, line 1 forces the camera to stay in first person mode. 
<br><br>Line 2 maps the WASD keys to movement.  You could also use the arrow keys. 
<br><br>Line 3 says that the mouse should control turning.  If you move the mouse to the right, the character will turn right, etc.

* You can now delete worlds you have shared with the community.  The “delete” option will show up on the community page for worlds you uploaded.  You must be signed in with he same creator name and PIN you used when you originally uploaded the world.  You can use the “My Worlds” tab at the top of the Community screen to find the worlds you have uploaded.

* Added a new timing tile for 1/8 of a second.  This will give more fine-grained control over timers.  Should be useful for music!

* Updated Missiles to be a bit more like ordinary characters.  Missiles can now be seen, shot at, run away from, and even grabbed.  Blips can also be used to shoot missiles.  In order to minimize the back-compatibility issues, missiles are only sensed when explicitly called out.  For instance:
<br>**WHEN See Anything DO Color It Red**   *<-- this will not affect missiles*
<br>**WHEN See Missile DO Color It Red**    *<-- this will work as expected*  

* Missiles can now be programmed to Squash their targets.
 

## Bug fixes:

* Fixed a bug which was causing newly added characters to not start their smoke emitters until after going from edit mode, to run mode, and back again.  Now, newly added characters should show smoke as soon as they are added.

* Removed the HeldBy tile from Clam and Seagrass characters since they can’t be held.

* The soccer ball now floats.

* Removed the Glow option from Lights.  They already glow. 

* Fixed a bug with using the mouse to pick small items from near big ones, like the Castle.  
The mouse picking code should be much more accurate now.

* When testing for EndOfPath, we not longer allow the “Me” tile for characters than don’t move, since they’ll never be following a path.

* Removed the Underwater filter tile.  This was supposed to filter on the “underwater” characters but was never implemented and so, did nothing.

* Cleaned up some invalid programming tile options.  For instance, if we have WHEN See Kodu Not it doesn’t make sense to have the toward, away, avoid, circle, and it tiles on the DO side since the WHEN side never generates a target for them.

* Fixed a bug with the .MSI installer related to using command line options when installing.  They should all work correctly now.

* Fixed a bug in the UI Slider code which would cause it to sometime start at the wrong position.

* Fixed an issue with deleting terrain.  We were setting the altitude to 0 but not clearing the material value.  In some cases, this was causing the terrain file to not be valid.

* Fixed a bug in the calculation of rotation angles for turning.  In cases where Quickly, Quickly, Quickly was being used, the character would turn the wrong way.

* Fixed a bug with the Rover not scanning rocks correctly.  When a rock is beamed or scanned, the first thing that happens is that the rover turns to face the rock.  The code was testing for the Rover to reach an exact angle instead of allowing it to be close enough.  Because of other affects in the movement system, the exact angle wasn’t always being achieved.

* Update the font choice for Chinese, Korean, and Arabic translations.

* Added a command line option to detect micro:bit devices.  This is only used if the normal detection code fails.

* Added a 5% deadzone for controller stick inputs.

* Fixed bug in the way options settings were being saved (or not saved…).

* Fixed crashing bug when run on displays > 4096 pixels wide.

* Fixed bug in stretch brushes.

* Fixed bug in the delete character brush.

* If Cursor filter is used, prevent additional filtering on Dead, Squashed, Colors, and Expressions since the cursor has none of those.  Also, the reverse, if any of those filters are used don’t let them be applied to the cursor.

* Same as above but with Cursor and Cluster, Few, and Many filters.

* Removed use of Dead or Squashed filters with use of Expression or Moving filters.

* Don’t allow Cursor filter with Said, Bumped, Got, or Held By sensors.

* Prevent Moving filter being used when sensing static objects.

* Don’t allow Camouflage filter on missiles.

* Removed second Lili Pad filter since these are treated the same whether it’s a single pad or a cluster of them.

* When changing missile colors (WHEN See Missile DO Color It Red), also change the smoke color.

* Fixed bug where sometimes with linked levels the characters would temporarily disappear.

* Fixed bug with knocked out characters still being tilted after being revived.