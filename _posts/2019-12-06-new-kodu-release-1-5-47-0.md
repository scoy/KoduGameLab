---
title: New Kodu Release (1.5.47.0)
subtitle: Critical fix for micro_bit users
layout: post
published: true
show_sidebar: true
---

test 8

I normally wouldn’t put out a release so quickly after the last one, but we’ve got a severe issue to fix.  A change in how Windows reports the hardware devices it detects has completely broken our micro:bit code.  On the latest version of Windows, older versions of Kodu no longer detect any micro:bits.  This release fixes the problem.

## New Features:

* First is the % sign.  You can now use this when doing math with scores.

* WHEN DO SetScore Red Green % 20Points 3Points   <-- this will set Red to 23% of Green (or Green% of 23, same thing)

* Since scores are integers, all results are rounded off.

* Second is the ability to rename characters.  You can now change a character’s name to make it easier to keep track of.

* The rename option is found in the character settings (DNA) menu.  Also, if you right click or tap on a character, the Rename option will appear in the menu.

* Multiple characters can have the same name.

* There are also new filter options derived from the named characters.  You can name a character “Fred” and then program another character with WHEN See Fred DO …
<br><br>Note that the name acts like a characteristic of the character.  So, for instance I could create a creatable Saucer and a creatable Sputnik, and name them both “Enemy”.  As they are spawned, they will all be called “Enemy” so I can then program my character with WHEN See Enemy DO …  and this will react to both Saucers and Sputniks.
<br><br>I think this is going to be a very powerful feature for more advanced users.  Not only does it allow some interesting capabilities, but it also can help to make the intent of characters more clear helping to make the game more understandable.

## Bugs Fixed:

* Tuned the way friction is calculated.  The old code was causing strange behavior (spirals) when some characters moved vertically.