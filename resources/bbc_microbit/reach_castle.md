---
title:
subtitle:
layout: page
show_sidebar: false
hide_hero: true
---

![BBC micro:bit](microbit_header.jpg)

[Home](../..)/[Resources](..)/Reach Castle

[![](https://www.kodugamelab.com/API/Thumbnail?world=pt-8eWj_fUiwVhE7ZEDk1w==)](https://worlds.kodugamelab.com/world/pt-8eWj_fUiwVhE7ZEDk1w==)

![Digital Technology](../dt.png) ![Computer Science](../cs.png) ![robotics](../r.png)

## Reach Castle

* Project 3: Reach Castle
* Student Ages: 09-14 years old
* Activity Time: 60 minutes 
* Activity Level: Beginner Coder

### Prerequisites
* Download and Installation of Kodu
* Get Started Instructions: [BBC micro:bit](microbit)
  * Connect your micro:bit to a computer via USB cable
  * Install the [mbed serial port driver](https://developer.mbed.org/media/downloads/drivers/mbedWinSerial_16466.exe)
  * Start Kodu (version 1.4.84.0 or later). It will detect your micro:bit and enable the micro:bit programming tiles.
  * [Project 1: Capture Love](capture_love)
  * [Project 2: Jump](jump)

![Get Started](connect_microbit.png)

### Learning Objectives
* Create a Kodu World with game effects using BBC micro:bit Show Pattern and Pattern.

### Contents
* [Completed World: Reach Castle](https://worlds.kodugamelab.com/world/3o1HjffZE0uTTgvL0KCtuQ==)
* [Completed Kode for Level: Reach Castle](Capture_Castle_Kode_for_Level.pdf)
* Project: Reach Castle (micro:bit show pattern, micro:bit pattern)

### Student Activities
To create a Kodu world using tiles specific the BBC micro:bit, make sure you connect a micro:bit device and install the mbed serial port driver

**Welcome! This activity will teach you how to show patterns when interacting with a computer opponent. Let's get started!**

#### Step 1: Add Objects

Start Kodu Game Lab. Select the New World option from the Main Menu, and Kodu Game Lab will open and display a patch of ground directly in the center of the screen.

Select the Object tool (the Kodu icon on the toolbar). With a game controller, select the Object tool using the left thumbstick.

* For mouse users, move the mouse pointer to the center of the terrain and click and release the left mouse, which open the pie menu. Use your mouse to select the Kodu object or another moving object (except Rover).
* For game controller users, move the camera to the center of the terrain and press the A button, which open the pie menu. Use the game controller to select the object. Use your game controller to select Kodu or another moving objects (except Rover). After adding the object to the terrain, press button B for the Tool Menu.

Finally, you want to add the computer's moving object, like a Push Pad and nonmoving object, such as the Castle.

![Add Objects](cc10.png)

#### Step 2: Path Following

We need to create a red path for the Push Pad to follow. The path might appear as a straight line, square, or random. Select the Path tool, it's to the right of the Object tool and looks like three balls connected with tubes. The path tool lets you add terrain and place small spheres.

* Using a mouse, every time you left-click with the mouse, a small sphere is placed and the tube will continue to connect. After you draw Push Pad's path to follow, click on the starting sphere to close the path.
* Using a game controller, the Path tool is selected using the Object tool and select the Path tool as a pie slice with the A button. You want to select the plain Path tool. Add an additional node (small sphere) with the A button. After you draw Push Pad's path to follow, press B button to close the path.

![Add Objects](cc0.png)

#### Tips and Tricks: Path Following

* Path Color. If you need to modify the path color, place the cursor over an existing sphere, use the Left or Right arrows to change the path color.
* Modify Path. If want to modify the path, left click and hold on the sphere to drag the sphere.
* Move Path. If you want to move the path, hold down Shift key and left click and hold on the path.

#### Step 3: Program Push Pad - Follow Red Path

Let's program the Push Pad to always move on the red path. Select the Object tool, right click on the Push Pad and select Program. Using a wired controller, move cursor over Push Pad, then press button Y.

![Path](cc11.png)

* Play Game to see if the code works as expected.

#### Step 4: Program Push Pad - Boom Me

Let's program Push Pad to Boom when it bumps Kodu.

![Boom](cc12.png)

#### Step 5: Push Pad Creatable - Change Settings

You want to make Push Pad a creatable. We want to make Push Pad a creatable to reuse the object. Push Pad will respawn when it has been destroyed.
* Using a mouse, select the object tool, the right-click on Push Pad then select Change Settings. You want to scroll down to Creatable option and turn it on, so it turns green.
* Using a wired controller, move cursor over Push Pad, then open Settings with B button. You want to scroll down to Creatable option and turn it on so it turns green.

![Creatable](cc00.png)

#### Step 6: Program Castle- Page 1 - Create Push Pad

You want to allow the castle to create an initial Push Pad. Let's program the nonmoving object, such as a castle. The nonmoving object will always create an initial Push Pad in the world. 

![Page 1](cc13.png)

#### Step 7: Program Castle- Page 1 - See Push Pad

You want to allow continuous gameplay between the Push Pad and Kodu. Using the Pages tool, you can change behaviors and create new scenes. Let's program the nonmoving object, such as a castle to always see when Push Pad is not in the world. 

![Page 1](cc14.png)

#### Step 8: Program Castle- Page 2

When Push Pad is not in the world, then the game will switch from Page 1 to Page 2! You need to tell Page 1 to move to Page 2, which ensures the game is affected by the programming on Page 2. You will switch from Page 1 to Page 2! Once your pencil moves to the Pages tool, you can click the left or right buttons with the mouse or toggle on the D-Pad with a wired controller. Try to move to Page 2. When you switch to Page 2, notice that it's completely blank.

![Page 2](mp_kodu1111.png)

#### Step 9: Program Castle- Page 2 - Create Push Pad

The Push Pad cannot create itself, so you need to the Castle to create a new Push Pad when it has been destroyed. You will find Push Pad as a creatable. When the Castle does not see Push Pad in the world, after 2 seconds, then the Castle will create a new Push Pad.

![Timer](t17a1.png)

#### Step 10: Program Castle- Page 2 - Switch Page 1

You want to create Push Pad after 2 seconds then move to Page 1. You want to tuck programming Row 2 underneath 1 so any programming placed in 2 is always being checked for a condition that will trigger the action. Row 2 moves to the right and is a child of 1.

![Switch to Page 1](t18.png)

* Play Game to see if the code works as expected.

#### Step 11: Program Kodu - Move

You want to move Kodu using the BBC micro:bit tilt.

* Using a mouse, select the Object tool and right-click the mouse on the object, Kodu. Select the Program option for Kodu. Select the When box with left-click of the mouse.
* Using a game controller, use the Object tool and move the control circle over the object and press Y button. Press the A button on the When box.

Kodu uses the accelerometer on the BBC micro:bit. The accelerometer detects movement on the BBC micro:bit with tilt. The accelerometer detects changes in the micro:bit’s speed and detects a standard action such as tilt. You use tilt to register an event that will run when a tilt event happens.

Alternatively, Kodu can use the L-stick on the Wired Controller or Keyboard Up/Down/Lt/Rt to move.

![Tilt](capture01.png)

* Play Game to see if the code works as expected.

#### Step 12: Program Kodu - Bump Castle

Let's prepare to program an action when Kodu bumps the Castle. 

#### Step 13: Program Kodu - Show Pattern

You want to tuck programming Row 4 underneath 3 so any programming placed in 4 is always being checked for a condition that will trigger the action. Row 4 moves to the right and is a child of 3.

Let's add Show Pattern and the Pattern when Kodu bumps the Castle! You want to add Pattern and draw a pattern on the micro:bit LED screen using Pattern. You can add several Pattern tiles. In this example, there are four (4) Pattern tiles. 

Kodu can also Say a message in game with a Thought Balloon. You can select Say, type a message, and press Save.

![Show Pattern and Say](capture02.png)

#### Tips and Tricks: Program Kodu - Pattern

You will left click with a mouse or press Y button on a wired controller to draw a pattern on the micro:bit LED screen. Create a pattern with an image to display a heart then click Save. Create a second pattern with an image with no LEDs lit up. If you repeat this logic in the pattern tiles, alternating between a heart and having no LEDs lit up, then you will have a pattern that appears as an animation.  

![Show Pattern - Smile](cc6.png)

![Show Pattern - Blank](t13.png)

* Play Game to see if the code works as expected.

#### Step 14: Program Kodu - Bump

Let's add code that will run when Kodu bumps Push Pad!  

![Bump2](cc4.png)

#### Step 15: Program Kodu - Pattern

Let's add Show Pattern and the Pattern when a Kodu bumps Push Pad! You want to draw a pattern on the micro:bit LED screen using Pattern. You can add several Pattern tiles. In this example, there are four (4) Pattern tiles. 

Kodu can also Say a message in game with a Thought Balloon. You can select Say, type a message, and press Save.

![Show Pattern](capture03.png)

#### Tips and Tricks: Program Kodu - Pattern

You want to left click or press Y button on a wired controller to draw a pattern on the micro:bit LED screen. Create a pattern with an image to display an X then click Save. Create a second pattern with an image with no LEDs lit up. If you repeat this logic in the pattern tiles, alternating between X and having no LEDs lit up, then you will have an animation.  

![Show Pattern - X](cc7.png)

![Show Pattern - Blank](t13.png)

* Play Game to see if the code works as expected.

#### Step 16: Save Your World

You want to save your work. The Game Save Screen is useful in managing the game development. You go to Home Menu then select Save my world. You want to type the game name in the Name field and describe the gameplay as well as rules of the game in the Description, then click or press Save. Finally, you want to select Change World Settings, scroll down to Start Game With, and select Description with Countdown. 

![Save World](capture04.png)

* Play Game to see if directions appear as expected.

![Save World](capture05.png)

### Skills
Character,
Citizenship,
Collaboration,
Communication,
Creativity,
Critical Thinking,
Project Based Learning

![BBC micro:bit](microbit_footer.jpg)
