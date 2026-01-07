//------------------------------------
// Whack A Mole
// © 2017 tasonco limited company
// Version 1.0.0
//------------------------------------

Whack a Mole is a Complete game project.
you can make your own game using this asset.

Features:
- you can use 9 kinds of 3d models of mole.
- you can edit the level from inspector
- supports PC, Mac, iOS, Android

Required Assets:
This game uses Tonemapping and Tilt Shift Script in an effects package of a Standard Asset.
You should import Effect package in Standard Asset.
Effect package can be imported from
[menu bar]->[Assets]->[Import Package]->[Effects]

How to play game:
Click or tap the mole and the score is added.
you can play 30 seconds. the game is restarted automatically.

How this package works:
This package consist of 9 folders and each folder contains the following items.
GameManager script controls a levels, time and UI. 
Mole's speed is controlled by MoleController script, 
and movement of all moles is controlled by MoleManager script.

-Whack-A-Mole/
 -Animation/ contains UI animation files.
 -Audio/ sound effects and BGM files.
 -Fonts/ font file.
 -Materials/ contains all materials using this game.
 -Models/ 3D models of moles, hole, hummer, stone, tree. The file format is .fbx.
 -Prefabs/ 10 Moles, stone, tree, hole, explosion Prefabs.
 -Scenes/ game scene.
 -Scripts/ contains all the necessary scripts for this game.
 -Textures/ textures for 3d models of ground, hummer, mole, and trees.

Modify and expand:
You can change the difficulty of this game from inspector of the GameManager object,
and you can also change the time limit from the same object.
If you would like to add moles, you can only drag and drop moles 
in a Prefabs folder to a scene view. you also add holes on the added moles.

Build for smart device:
If the game is too slow, remove Tonemapping and Tilt Shift Script from Camera and
selsect [Edit]->[Project Settings]->[Quality] from menu bar and select the Levels "Simple" or "Fast".

Contact:
1ay9qw1@gmail.com

License:
BGM  is created by oo39.com（http://www.oo39.com）,
Hit sound is from maoudamashii.(http://maoudamashii.jokersounds.com)
Please refrain form secondary distribution only the bgm.
