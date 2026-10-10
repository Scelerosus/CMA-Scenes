# CMA Scenes: journal

Successor of CMA Shake (`..\CMAShake`, last version 0.53.5). Game: `C:\Games\Corrupted Modeling Agency 0.66` (IL2CPP, BepInEx 6).
Plugin `BepInEx\plugins\CMAScenes\CMAScenes.dll`, GUID `community.cma.scenes`, config `BepInEx\config\community.cma.scenes.cfg`
(taken over from `community.cma.shake.cfg` on the first run, profiles too). `[BepInIncompatibility("community.cma.shake")]`:
the two must not run together. The old plugin is disabled in the game folder as `plugins\CMAShake\CMAShake.dll.off`
(rename back to `.dll` and remove `plugins\CMAScenes` to return to it).

Build: `dotnet build CMAScenes.csproj -c Release -p:SkipInstall=true`, copy `bin\Release\CMAScenes.dll` when the game is closed.

## 0.1.0 (2026-10-09) - forked from CMA Shake 0.53.5; NOT run in the game

User's request: analyse CMA Shake and make a new mod with new functions, a choice of places for the sex in the office
(the bench, the table), and take the twisted arm of the screenshot into account in every pose.

### What the game already has (recon, from the interop assemblies and the level files)
- Its own poses at furniture, as skeletons standing in the scene: `_LongTablePoses` (Doggy Mesa, Doggy.Lay Mesa, Misionero
  mesa, Misionero mesa erguida, Side.Front, Side.Back), `_ShortTablePoses` (Sentada Open Legs, Sentada Inv Open Legs),
  `_WallPoses` (four standing ones). Reached through the game's pose menu (the third-party "All Poses" plugin adds Table
  and Wall submenus). Classes: `BaseFemalePoseLoader`, `ConfiguracionDePoscionSexual` (joints that tie her hips, hands,
  feet), `FemalePosesConfigs`, `TipoDePose`, `FemaleAnimatedPoseIDs` (camillaCow, doggyEnCamilla ... for the stretcher).
- `Assets.NewFeatures.AutoSexAssist.AutoSexAssist` (SMA.Features.dll): the hero thrusting by himself (seek the hole,
  pacing Manual/Adaptive/PoundThrough, phases Warmup..Afterglow). So sex with HIM active in the game's own table and wall
  poses exists already; what did not exist is her on top anywhere but the floor, and anything on the bench.
- Office levels (level11, 26, 32) name: `bench`, `Table 2`, `Table 3`, `LongTable_Pivot`, `ShortTable_Pivot`,
  `GoToTarget_LongTable`, `Desk_GoTo*`. Whether the furniture has colliders is not known.
- Her skeleton: `<root>/FemaleAnimator/<name>/CC_Base_BoneRoot/CC_Base_Hip/...`; every piece of clothing has a skeleton of
  its own that follows hers; her puppet's bodies are carried by her skeleton while she is posed (HeroExtra.cs).

### New: places (Sex/Places.cs)
- The sex is still laid out from her body as the game has it, but her hips' bone is first carried, rigidly (a move and a
  turn about the upright, the same every frame), to the place: `PosSlot.Carry`, `Woman.sHipRot`, `PlaceCarry` first thing
  in `ApplyRide`. Everything after it finds her there. Her root stays where the game has it.
- Carried at once, with a cut to black (`FadeCut`, `Place/Fade`), her meshes set to `updateWhenOffscreen` meanwhile, the
  loose bodies of her puppet put where she is (`HerBodyJump`).
- The floor of the sex is the top of the place (`FloorLook`), he lies along it (`HeroHeadGoal`), with her back to him it
  is she who turns round. A foot that would be off the place goes down to the floor beside it if her leg reaches (a
  bench: she stands astride it), else it is brought in onto the place (a table); no kneeling where there is no room for
  her knees; no hands on the floor beside him there.
- Places are found by: the furniture's names (`PlacesScan`: bench, sofa, couch, table, desk, bed, mesa, escritorio ...),
  measured with rays from above (`Survey`: the largest flat top, its extent and direction by the spread of its points,
  the floor beside it), or from the box of its mesh where no collider is met; and by looking (`PlacePick`: the top of
  whatever is at the middle of the view). By hand: Along, Across, Height, Turn (kept per place while the game runs).
- `PoseBack`: carried, a foot the game's inverse kinematics dragged more than 15 cm from where it was put goes back.
- Tour (`Place/Tour`): after an orgasm of hers she moves on to the next place.
- Window: card "Place" on the Sex page. Config section `Place`.

### New: arms (Sex/Wrist.cs), `Ride/NaturalArms` (on)
- Every place of her hand now says how her hand lies there (fingers, back of the hand), also on her thighs and under
  her breasts (they were left as the game's animation had them on a forearm that pointed elsewhere: the wrung wrist of
  the screenshot).
- `ArmSolve`: her elbow is swung round the shoulder-hand line (up to 75 deg from the pose's side, smoothed) to where it
  asks least of her wrist; her hand is then turned as asked within 95 deg about her forearm and 72 deg against it; the
  turn is handed on to the forearm's twist bones (`ForearmTwist`: found by name "twist", the game's own twist taken out,
  each its share).
- The "unturned" side of her hand is reckoned from the axis her elbow bends about (right arm: against it, left: along
  it) and checked once on the game's own pose (`wristSign` flips if that reads over 125 deg).

### Not known until it is run
- Whether the office furniture has colliders (else: box of the mesh, and Height by hand).
- Whether the game's inverse kinematics leave her alone 2-3 m from her root; whether her hair and clothes come with her.
- Whether the reckoned wrist side is right (log lines "Arms: ...").
- He is a rigid plank as on the floor: on a 1.5 m bench his lower legs stick out past its end.

### Ideas not done
- He sits (couch edge), she on his lap: needs his limbs posed.
- Him active at the game's table and wall poses, driven by this mod's rhythm instead of AutoSexAssist.
- A summary of each scene written into the game's memory of her (for later mechanics: mood of the day, aftermath).

## 0.2.0 (2026-10-10) - after the first run (log of 0.1.0 read); NOT run in the game

From the log of 0.1.0: places found (Bench 1.71 x 0.63 m, 0.47 m high; Table 2 2.79 x 1.17 m, 0.75 m; Bench 2), rays met colliders
everywhere (729 of 729), she is carried with a cut to black, "the game moved her right foot 20-29 cm; put back" appeared a few
times on the bench, the left wrist read 126 deg turned, so the back of the hand was taken to be on the other side (flipped
once, as designed). The "look-at" guard (MainCharCamera) never fired: the space bar is something else.

User's list, and what was done:
1. X-ray darkening is fixed at 49 % (slider and setting removed). Hands: new `Ride/HandsMode` = Auto (default; the old `HandsOn`
   key is not read any more, so the old value in the cfg does not shadow it): Sex/Scenes.cs `AutoHands` picks thighs / his
   chest / behind / shoulders by lean, wetness, her orgasm (shoulders) and his (chest), with a 10 s hold; in an orgasm the hands
   draw in and tremble (`grip` in Ride.cs).
2. His scrotum removed (Hero/Balls.cs deleted, setting and menu row gone): the game has its own.
3. Womb: `Ride/WombReach` (3.5 cm): when he comes bare in her she sits that much deeper (`m_wombDive`, Sex/Scenes.cs), the
   X-ray womb is moved along the canal by how far his tip is past its end (XRayWomb.cs).
4. `HeMeetsHer` was 1 cm and she was lifted by it, so the pair bobbed together and nothing showed. Now `Ride/ThrustHeight`
   (new key) = 4.5 cm by default, up to 9, as a slider.
5. Space bar: the free camera's Focus (Camera3DViewInputs.Focus(), FocusAction) takes the bounds of the selection, which are
   where the bodies were before this plugin carried them; left out while riding (Hero/Guards.cs). It logs "the free camera
   was asked to focus on the selection" when it fires. UNVERIFIED that this is the cause.
6. Bench / table: his head is at the end of the place (`shift = halfLen - 0.74`), his hips 62 cm from his head, the rest along it.
7. Feet: `Ride/FeetFindGround` - a ray down at each foot (Floor.cs GroundAt): ankle as high above the ground there as the pose has
   it above the floor (heels), sole along the slope, brought onto the place if the floor is out of reach.
8. `Ride/HeHolds` - he holds her: his hands IK'd onto her hip joints (Sex/Scenes.cs HoldHands, from where her hips were put
   the frame before), his lift is up to 7.5 cm, her own stroke 55 % shorter, her hands on his chest.

## 0.3.0 (2026-10-10) - from the screenshots of 0.2.0 and the log; NOT run in the game

Log of 0.2.0: `hero against the floor ... lifted by 1,2 -> 3,5 -> 7,8 -> 9,1 cm for being pressed down` (a ratchet that never came
back) = he floated 10-20 cm over the bench with his legs in the air; the left wrist was wrung after the 126 deg "flip".
Done by three parallel Opus agents (hero / limbs / X-ray+UI), notes:
- Hero: HeroFloorGuard removed; surface model (rays under buttocks, shoulder blades, head, thighs, calves, heels: HeroBody.cs
  BuildHeroRest / RestSurfaceAt / HeroRestStep), his hips placed where the skin rests, back and legs bent to lie on it, thrust as an
  arch of the lower back capped at 10 cm (HeroPoseLying), she sits higher by the thrust room (Mount.cs). `Ride/SkinGap` (new key).
- Limbs: Wrist.cs auto-flip removed, finger bones laid/curled (FingersFind/FingersPose), place "HisArms" (her hands on his forearms
  when he holds her), unreachable target -> hand on her own thigh; Feet.cs KneelLeg (knee on the bench, outside him, 5.5 cm from
  the edge); HoldHands lays his palm on her buttock (HoldWrist).
- X-ray/UI: game's semen picture stretched to full alpha (XRaySemen.cs), outflow stream (XRayFlow.cs, reads SemenParaVag
  MililitrosAcumulados / expulsando), ghost cut at the entrance (`XRay/CutBelowEntrance`), `XRay/AfterSex`; condom teat = bulb of
  the game's ejaculation volume (Condom.cs TeatShape/TeatTint); semen-on-penis removed, wetness always on (RideWet gone); menu
  rows wrap (Menu.Widgets.cs, Styles).
Unverified: everything above in the game. Look in the log for "how thick he is under the places he rests on", "his left arm
reaches", "the hand hangs from", "his semen in her vagina", "the game lets his semen out", "Ride: condom: he came; the game has".

## 0.3.1 (2026-10-10) - after the first run of 0.3.0; NOT run in the game
Log of 0.3.0: no X-ray failures; semen: the game's emitter still shot its drops from his tip into her with the condom on (the
prefix on TryEyacular only shrank the amount, not the count), and the game lets what is in her out of her after.
- Condom.cs/Semen.cs: Harmony prefixes on EmisorDeSemenChain.EmitirDesdePene / EmitirDesdePeneConOverflow make nothing for his
  penis while the ejaculation the condom caught goes on (`CondomBlocks`, `m_condomShotUntil`). Watch for exceptions in the game's
  ejaculation coroutine (null chain returned) and for `m_eyaculando` sticking.
- X-ray: `XRay/InHerBody` is now a new key `InHerBody2`, default off. With it on, the second drawing of him was shown only where
  her skin hides it, so from the side where his penis is not behind her skin only the top part showed. The cut at her entrance
  (limited by his own skin surface) keeps his belly clean.
- Hands: not touched (no screenshot of 0.3.0). Log shows "her left/right hand cannot reach where it is to go (8-21 cm past her arm)"
  in Cowgirl/Astride: hand rests on her thigh instead.

## 0.3.2 (2026-10-10) - from the 0.3.1 log (wrist diagnostics); NOT run in the game
Log: hands on him (Astride/Cowgirl): both wrists pinned at pronation 85 deg and extension 63-75 deg, strain 2.5-4 all the while (at rest 0.06);
`Arms: her/his fingers: not found by name (0 of 4)`: the fingers stay as clawed as the game's animation has them (screenshot of
2026-10-10: curled claws on his belly). Done: `FingersFind` falls back to the bones of a finger by name when they are not nested
(sorted from the wrist outward), and logs the names under the hand when it still finds nothing (`Arms: the bones under ...`).
Neutral of the wrist turn (LayHand) was re-derived by hand: signs for both arms are consistent; left hand of the game's own pose
reads 125 deg palm up against 1 deg on the right, not explained. Wrist diagnostics (`wrist as laid`, every 6 s) stay in.

## 0.3.3 (2026-10-10) - from the 0.3.2 log; NOT run in the game
Log: finger bones are `CC_Base_Finger{0..4}{0..2}.L/R` (0 thumb, 1 first, 2 middle, 3 ring, 4 little finger): `IsFinger` knows them now.
Hands on his chest: the left wrist went to strain 18-33 (pronation pinned at 85) while the right stayed at 3: the elbow search jumped
between two turns of the arm about its axis (neutral of the left wrist 0.35,0,0.94 then 1.0,0,-0.05) = the arm wringing in motion.
`ArmSolve` now charges for leaving the swivel it is at (0.35 * ((ang - swivel)/30)^2). Not touched: her back bends 20-28 deg further
to reach his chest 37 cm up (`Ride: her hands on him`); the wrist wants that much lean (less lean = more extension).

## 0.3.4 (2026-10-10) - diagnostics only
0.3.3 log: fingers found (`Finger10..40 (3)`), swivel steady and mirrored left/right (strain 1-2 on his chest). Screenshots: her right hand lies
flat on his belly; her left hand hangs at her crotch as a clenched fist with a bent wrist ("cannot reach: rests on her thigh"). New log line
every 6 s: `Arms: ... fingers curled toward the palm, deg, by joint` (to see whether the left fingers are straightened at all).

## 0.3.5 (2026-10-10) - found by the finger log of 0.3.4; NOT run in the game
Her left fingers read as curled -38/-33 deg toward "the palm" (never changed: straightening only acts above 8 deg), the right +35/+37 by the same
formula: the palm of her LEFT hand was taken for its back (handPalm sign in HandFlat is wrong for that rig), so the whole left hand was laid
palm up / twisted and its fist never opened. Now `FingersFind` checks the average curl of the resting fingers against the palm and swaps the
palm if it is below -12 deg (log: "the palm is the other side, swapped"). Holds for his hands too. If the left wrist still looks wrong, the
cause is elsewhere (neutral of the wrist turn, `ArmSolve`).

## 0.3.6 (2026-10-10) - screenshots of 0.3.5; NOT run in the game
- Hands on his chest followed her hips' yaw (`right`/`fwd` from her hip joints) while she grinds: palms swept over him. Now along his body
  axis (`m_heroFwd`) and across it, where it agrees with her facing (Ride.cs, chestSpot).
- The extra bend of her back to reach his chest was searched up to 90 deg (and defaulted to 90 when no place was reached): she folded over him
  with her head at his face. Now at most 26 deg; farther than that her palms go lower on him instead.
- Reach for the palms 0.8 -> 0.85 of arm length squared; the hands rise toward his chest in 1.2 s (was 3 s).
Not done: the bend from the stroke itself (`lean += 70 * RideHipsWork * risen`, `arch`), which adds to this when she speeds up.

## 0.3.7 (2026-10-10) - user's request: no hand settings; NOT run in the game
Menu: the "Her hands" row and the "Lean toward him" slider are gone (the fold is now "Her legs"). `Ride/HandsMode` is not read any more: her hands
are always `AutoHands` (Scenes.cs): thighs while she sits quietly (her pleasure < 0.35, not wet), his chest from a pleasure of 0.35 or when the pose leans
her over him, behind her when the pose leans back, his chest in both orgasms (was: floor by his shoulders in hers: it folded her over him).
`Ride/Lean` stays as the pose presets' own value (Cowgirl 18, Astride 30, ...), unchanged, no longer in the menu.

## 0.3.8 (2026-10-10) - user: half of his penis in her canal froze against the womb; NOT run in the game
New `XRay/XRayCanal.cs`: while he is in her vagina the X-ray draws a canal of the plugin's own round him (tube from her entrance along his axis to his tip,
radius = his thickness x1.1 + 4.5 mm, narrower at the entrance, closed in a dome past his tip), and `WombStep` puts the womb on his tip
(`neck = tip + axis * 8 mm`) instead of at the end of the game's short canal bones (6.5 cm). The game's own drawing of her canal (renderer of `VagInternal`)
is switched off while it is on (restored in `GhostOff`). Out of her, or in her rectum, nothing changes. `XRay/OwnCanal` (default on) turns it off.
Log: `X-ray: through her body: her canal is drawn round him`.

## 0.3.9 (2026-10-10) - log of 0.3.8; NOT run in the game
Log: with the bend to reach him capped at 26 deg (0.3.6) she "does not reach him anywhere" (hands aimed at 17 cm up him) while sitting high: wrists in strain 12-52,
"80 deg palm up" flips = arms wrung as she moves. Now: bend up to 40 deg; where she still cannot reach him, `m_chestOk` falls to 0 over 0.5 s and the weight of
"his chest" goes to her thighs (and `chestBend` to 0, the default of `bestLean` is 0) instead of aiming the hands at a point out of reach.

## 0.3.10 (2026-10-10) - user: the plugin's canal hid the game's semen leaving her
`XRay/OwnCanal` -> new key `OwnCanal2`, default off: the game's own canal is drawn again (it stretches round him by itself); the plugin's canal and the
womb on his tip (0.3.8) are only with the setting on.

## 0.3.11 (2026-10-10) - Reverse Cowgirl: his hands on her buttocks
In `HoldStep`, `m_holdHandsW` is now a separate fade weight. It comes on while mounted/in the ride when facing Away, or when the existing `Ride/HeHolds` mode is enabled. `HoldHands` uses that hand-only weight; `m_holdW` remains exclusive to the existing HeHolds movement and her-hand behavior, so Reverse Cowgirl gets his hands on her buttocks without changing who drives the stroke. Reset with `DriftReset`. Strata reviewed this split and agreed it avoids the movement side effects; in-game reach and contact are unverified.

## 0.3.12 (2026-10-10) - Reverse Cowgirl: support her bottom on the downstroke
In Ride.cs, while facing Away and mounted, `m_holdSupport` follows the descending phase (`-cB`); in HoldHands, his glute target rises into her bottom 1.2 cm and the palms rotate upward to cup/support it. Hand IK weight stays continuous to avoid slipping; hip stroke and HeHolds drive remain unchanged. Reset in DriftReset. Built/installed, in-game appearance unverified.

## 0.3.13 (2026-10-10) - Reverse Cowgirl: no automatic hand hold
Removed facing Away from the hand-only auto hold. In Reverse, his hands stay with the game's animation; they only go to her in other poses when the explicit HeHolds setting is enabled. Built with SkipInstall because the game was open; hidden installer copies the DLL after the game closes.
