---
title: New Kodu Release (1.5.42.0)
subtitle:
layout: post
published: true
---

test 10

## New features:

* Kodu now speaks Vietnamese!

* The “Print Kode” option has been updated to also include any tutorial instructions.

* More tuning on how things bounce and roll.

* The Octopus can now move on land again.

* When editing a level, you can now easily align characters with the N/S and E/W axes.  Hover the mouse over the character so it is highlighted and then use the up and down arrow keys to rotate the character.
 

## Bug Fixes:

* Fixed a bug in the missile code which would sometimes cause them to destroy the character they were launched from.

* Fixed a bug in the text string handling code which, in some cases, could cause the help descriptions to get cut short.

* Fixed a but in the mouse click tile.  When combined with a specific character type it would trigger when clicked on the background when it should only have triggered when clicked on a character of the specified type.

* The “timer” tiles were not being reset properly when a game was restarted.  Fixed.

* Fixed bug with movement.  Characters with no obvious facing direction (wisp, saucer, etc.) would not respond to absolute directions (Move North, …) properly.

* Fixed a bug in the language translation code which was preventing updates from being downloaded when needed.

* The alignment of pipes was originally broken.  When this was fixed it left old levels with pipes in skewed positions.  The code now detects old levels and fixes the pipe alignment.