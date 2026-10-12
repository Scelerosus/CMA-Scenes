# CMA Scenes: journal

## 0.4.1 — Individual garment colours

- Clicking a catalog piece adds it if needed and opens its colour/material editor. Selected catalog pieces open editing rather than removing themselves. Removal remains explicit in Outfit.
- Each garment has an independent #RRGGBB colour. Native loading, delayed callbacks, tint materials, both previews, copied drafts and saved outfits preserve the per-piece colour map. Editing a piece never changes the legacy shared colour or another piece's colour.
- Saved outfit schema 3 records per-piece colours, with schemas 1/2 retaining their original shared-colour fallback. Invalid saved colours protect the file from replacement; invalid config overrides are ignored. Preferences, config and the old DLL are backed up for rollback before installation.
- Outfit-wide Pattern/Solid style stays under More outfit options; the global colour editor and global random-colour action are removed.
- Validation: 45 catalog/storage checks, 45 actual wardrobe-code fixture checks (including two differently coloured delayed native loads and reapplication), 34 UI source guards, clean Release build and installed-interoperability audit. User will check the layout in game.
- Installed on 2026-10-12 while the game was closed; Release hash matched. Old DLL, scene config and saved-outfit preferences backed up with timestamp 20261012-185422; manifest `.rune/install-0.4.1-20261012-185422.json`. Clothing Fix remains 0.4.0. Compiled Unity audit: 554 API entry points passed.

## 0.4.0 — Outfit workflow and wardrobe lifecycle

- Reorganize the English Outfit page into Browse, Outfit and Saved, with a compact character selector, staged material editor and a separate native worn list. Use explicit Dress character / Restore previous outfit actions; saved drafts do not auto-apply.
- Show the cursor through the game's CursorInputDriver and maintain its visibility while a mod menu is open. Avoid overriding another native cursor modifier when closing.
- Apply material, colour and Solid to already-worn selected pieces by reloading them with independently cloned original slots. Use native coverage flags before catalog type for replacement regions.
- Cancel failed outfit loads and roll back originals; retain failed-removal ownership and original snapshots for retries. Reconcile native undress and retain authoritative subskin references to remove orphan geometry even after native unregistering.
- Release owned tint materials when garments leave. Isolate per-character update failures. Retain failure messages after rollback; explicit Dress/Restore clears them.
- Distinguish Solid and Pattern thumbnail requests/cache entries. Preserve all catalog categories, saved drafts and favorites; destructive preference actions require confirmation.
- Validation: 38 catalog/resource/source checks, 42 real-code wardrobe fixture checks, 29 UI source guards. Compiled Unity calls are audited against the installed interop API, including a self-check for the earlier stripped SelectionGrid dependency. In-game visual and native-loader validation is left to the user.
- Installed on 2026-10-12 alongside Clothing Fix 0.4.0 while the game was closed. Both Release hashes verified; prior DLLs and both configs backed up with timestamp 20261012-054917. Saved outfit preferences preserved.

## 0.3.37 — synchronize clothing replacement with the native equipped wardrobe

- Replace OcultarPieza with RemovePieza for replaced items. Hidden equipped entries caused old clothing to remain in the native Outfit menu.
- Clone original material-slot data before removal and restore it through AddPiezaAsync on toggle off. Keep snapshots until successful restoration, allowing retry on failure.
- Retain originals across successive selections; restore the original pieces that a new partial selection no longer replaces. Serialize replacement against restoration callbacks and allow canceling a pending selection.
- Release build and standalone checks pass. Native menu behavior still requires a live game check.

## 0.3.36 — larger clothing previews and twerk removal

- Enlarge cards from 88 × 88 to 160 × 160, list area from 236 to 420, and generated images from 256 × 256 to 384 × 384. Revised cache key regenerates smaller cached previews.
- Delete the twerk posing implementation, pose bank, menu page, launch buttons, hotkey and dedicated settings. Remove its mode selection and speed routing. Move wardrobe code into Clothing.
- Retain shared restoration and body-motion compatibility settings; unrelated mode implementations remain unchanged. Existing automatic clothing config/profile key is preserved under an internally renamed setting.
- Independent review confirmed no reachable twerk path and equivalent shared cleanup; wardrobe persistence finding fixed by retaining its storage key. Build and standalone checks pass; new card layout still needs live observation.

## 0.3.35 — tolerate stripped optional LOD type

- Live 0.3.34 log identified the next blocker: LODGroup has no IL2CPP class pointer in this player. Guard optional LOD detection before constructing its native Type; a player without that class cannot have LODGroup prefab components.
- Correct the missing fallback icon key to the existing tab.bikini asset.
- Release compilation succeeds. Native thumbnail appearance remains unverified until the restarted game exercises generation.

## 0.3.34 — fix unavailable previews in the IL2CPP player

- User's screenshot showed blank white thumbnail cards. Live BepInEx log reproduced failures in generic GetComponentsInChildren<T> initialization for every preview; no preview PNGs were generated.
- Use native component-query/creation overloads with explicit Il2CppType instead of stripped generic methods in preview staging and exposure setup.
- Give thumbnail cards a dark background and tint the fallback icon so loading/failure no longer appears as a blank white rectangle. Preview failures include a full exception and cache key for diagnosis.
- Release build passes with zero warnings/errors; 30 standalone checks pass. The reported native-call failure is addressed; appearance after restarting with this build still requires runtime verification.

## 0.3.33 — clothing author credits and model previews

- Author credits from item metadata, duplicate-name cleanup, explicit missing-author label, author search and full-name tooltips.
- Garment-only 256 × 256 previews queued from Addressables model meshes; isolated HDRP camera, approximate unlit garment materials, aspect-preserving cards.
- Disk cache keyed by item, model address and version; bounded memory cache, safe PNG checks, resource cleanup on close/refresh/destruction.
- Disabled/inactive mesh alternatives are excluded. LOD prefabs use a fallback icon because GetLODs is stripped from this game's API. Skinned deformation and custom shader appearance remain limitations.
- Independent review completed; standalone checks and Release build pass. Actual HDRP thumbnails require in-game verification.

## 0.3.32 — technical clothing and UI update

- Independent nearby wardrobe contexts, selected-character outfit changes, configurable 8 m default radius, no character cap.
- Clothing categories, English game names and Russian fallback names, search, favorites, saved outfits with color and pattern visibility; paged catalog without the 250-row cutoff.
- Per-character delayed outfit snapshots prevent edits to another character's pending outfit. Selected color changes update that character's pending snapshot.
- One-piece items are added once; selecting separates clears conflicting whole-garment slots. Items already in the original outfit are not treated as temporary additions.
- Versioned cosmetic JSON with atomic replacement; damaged or future-version documents remain protected until explicit reset.
- Menu texture and icon ownership, release on brightness rebuild/controller destruction; game assets are borrowed.
- Fresh independent review and standalone regression checks. Release builds without installing. In-game validation and frame-time comparison remain pending.
- Forked from committed 0.3.30 at 0e19b0c. Original uncommitted 0.3.31 changes were excluded by the user's choice of starting state.

Successor of CMA Shake (`..\CMAShake`, last version 0.53.5). Game: `C:\Games\Corrupted Modeling Agency 0.66` (IL2CPP, BepInEx 6).
Plugin `BepInEx\plugins\CMAScenes\CMAScenes.dll`, GUID `community.cma.scenes`, config `BepInEx\config\community.cma.scenes.cfg`
(taken over from `community.cma.shake.cfg` on the first run, profiles too). `[BepInIncompatibility("community.cma.shake")]`:
the two must not run together. The old plugin is disabled in the game folder as `plugins\CMAShake\CMAShake.dll.off`
(rename back to `.dll` and remove `plugins\CMAScenes` to return to it).

Build: `dotnet build CMAScenes.csproj -c Release -p:SkipInstall=true`, copy `bin\Release\CMAScenes.dll` when the game is closed.

## 0.3.30 (2026-10-11) - preserve view on Space

- User confirms Space is better in 0.3.29 but still turns the view/head left. Runtime log confirms the hero's ViewMotor was found and actor rotation detached. The remaining auto-look path is a hypothesis, not yet confirmed by an observed call.
- Suppress Space-driven automatic target commands on the hero's rig, its yaw/pitch motor and MainCharCamera look requests while lying. Other cameras and normal mouse input keep their native processing.
- Clear pending target turns while Space is held; on key-down discard smoothing toward an old target, retaining the current camera direction. Added event logs for diagnosis if the sideways movement persists.
- Release build succeeds. Running game still has 0.3.29 until restart; install 0.3.30 after it exits, preserving the previous DLL under `%LOCALAPPDATA%\CMAScenes\backups\space-view-0.3.30`. In-game effect remains unverified.

## 0.3.29 (2026-10-11) - native camera and raised head

- User reports that the 0.3.28 Space guards did not stop spinning. Replaced those suppression patches with prefixes on ViewMotor and OrbitFixedMotor RigLateUpdate/PostRigLateUpdate. Native updates still execute.
- Temporarily disable RotateAnchor, its input alias, actor matching (ViewMotor), and RotateWithAnchor (OrbitFixedMotor) only on the hero's native rig during the lying pose, including transitions. Save and restore original settings when he gets up. This addresses a suspected conflict between camera-driven actor rotation and the mod's lying pose; runtime confirmation is still needed.
- Removed camera position smoothing. The game owns camera position and rotation. An optional configured FOV still works and is restored separately from motor settings.
- Raise the head through the neck by 18 degrees by default, with a 0-30 degree slider under Scene / View. The neck adjustment blends with the existing pose weight.
- Release build succeeds without warnings. Game was closed during installation; Space behaviour and framing have not yet been observed with this version.
- Previous installed DLL backed up under `%LOCALAPPDATA%\CMAScenes\backups\native-camera-0.3.29\CMAScenes.dll`.

## 0.3.28 (2026-10-11) - Space camera guard

- Inspected the installed 0.66 interop metadata: Camera3DViewInputs has three Focus overloads, but the old Harmony guard patched only the parameterless overload. All three and FocusAction are now guarded.
- Guarded CameraController.SetTargetYawPitch while Space is held during the ride; clear an existing target turn before suppressing the new one.
- Retained camera protection while the hero is returning from the ride pose. Release build succeeds; the Space interaction still needs observation in a running scene.
- The installed previous DLL is backed up under `%LOCALAPPDATA%\CMAScenes\backups\camera-space-0.3.28\CMAScenes.dll` before installation.

## 0.3.27 (2026-10-11) - live pose control and fuller pose flow

- `KeypadMultiply` cycles poses during riding or twerking; Shift reverses direction. The shortcut is configurable in the settings window.
- Optional automatic pose flow now uses six front-facing ride poses, adapting the squat/kneel choice to fatigue. Reverse remains manual so the player controls the 180-degree turn.
- A manual pose choice pauses automatic changes for at least 30 seconds. Starting a new ride also starts with a 30-second settling period.

## 0.3.26 (2026-10-11) - smoother hand contact

- The reachable point on each thigh is now solved continuously, so the palm does not move in 2 cm steps as the leg bends.
- During hand-contact transitions the wrist is guided out of the thigh before arm IK runs, preventing it from crossing the leg mesh.
- This changes only hand placement. Observe all ride poses in motion in the game to judge model-specific clearance.

## 0.3.25 (2026-10-11) - maintenance and hot-path cleanup

- Removed legacy hand settings that no longer selected contact points. Fast-hand state had still reduced fatigue despite no longer changing the hands; fatigue now follows the actual pose and pace.
- Removed the optional plugin-made vaginal canal and its renderer switching. X-ray continues to draw the game's canal and its fluids.
- Cached spine transforms and arm lengths for reach searches, avoiding repeated IL2CPP transform reads inside the bend loops.
- Reused the small wetness curves across mask redraws instead of allocating three arrays each time.
- Updated the README status and version. Built in Release; runtime animation and performance require observation in the game.

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

## 0.3.17 (2026-10-10) - limit chest reach bend in kneeling ride poses
The live Kneeling scene showed her adding 32–40 degrees of forward bend to reach his chest, while her left hand still missed by 8–18 cm. Astride, Kneeling and Over him now search for a chest contact point within 22 degrees of added bend, so the target moves closer when necessary and her torso does not fold forward to chase an unreachable point.

## 0.3.16 (2026-10-10) - reduce forward bend in Over him
The live Over him scene repeatedly put her head and chest into his body; the collision correction had to remove as much as 24 degrees of added bend. The preset lean is now 22 instead of 40 degrees, her hands target his chest rather than the unreachable shoulder-side floor, and the extra chest bend is capped at 22 degrees for this pose.

## 0.3.15 (2026-10-10) - keep buttock shape steady as ride speed rises
Reduced ride-only glute-bone wobble to 30% of its former response and limited its offset to 2 cm. The spring was driven by hip acceleration, which grows quickly with stroke tempo and could pull the glute bones far enough to distort the silhouette. The twerk wobble is unchanged. Built/installed; live shape comparison still needed.

## 0.3.14 (2026-10-10) - ride hand placement and standalone leg poses
- Her chest contact points are spread farther apart to prevent her palms converging at the middle of his torso. Hand placement is pose-aware: Kneeling/Astride reach to his chest, Over him braces by his shoulders, Leaning back rests on her own thighs, and Reverse keeps her hands on her thighs.
- With `HeHolds` on, his hands now follow through in every facing direction; in Reverse they cup the lower outer sides of her bottom. With it off his automatic hold stays disabled.
- Squatting and Kneeling are separate choices in the ride position cards instead of one `Her legs` sub-option. Squatting gets its own deeper preset; both presets select their own leg style and stay out of the existing Cowgirl→Astride→Over him pose flow.
- Built with `SkipInstall` while the game was running; in-game hand contact and pose transitions still need a live check.

## 0.3.18 (2026-10-10) - distinct hand rests for the ride poses

- Auto hand placement now chooses different resting places by pose: Kneeling uses thighs or her ribs, Astride her ribs or his chest, Over him his chest, Leaning back his thighs, and Reverse her hips or thighs. Cowgirl keeps the pleasure driven chest reach, while Squatting uses her thighs or ribs and can brace at the sides at speed.
- Her own hips are a new hand target that follows her pelvis. Pose changes and orgasms can select a new target immediately; other changes keep the existing ten second dwell and the per arm reach fallback.

## 0.3.19 (2026-10-10) - sex menu navigation

- The Sex page has three short views: Scene for place, hole and pose; Movement for tempo, depth and movement options; Details for X-ray, orgasm and condom. Start/stop and current status stay above the navigation on every view.
- Switching views returns to their top so settings cannot open halfway down the previous view. Existing configuration keys and actions are unchanged.

## 0.3.20 (2026-10-10) - smoother riding and pose changes

- When the game leaves very little safe vertical travel, the rider switches to a small pelvic rock and the automatic tempo stops chasing invisible strokes. A previously learned tip margin relaxes slowly only when the game's entrance reports several safe strokes and she has no pain.
- Each palm searches for its own reachable place on his torso and glides between that contact and her own thigh. The contact points are fixed to his body, with extra room for her wrist; one shorter arm no longer sends both hands away.
- A pose change eases down her stroke, puts her hands on her thighs, holds the old leg support briefly, then moves the legs and restores the new pose's hands and stroke. The game's depth and remount safeguards remain in control.
- Compiled with `SkipInstall`; live visual comparison remains necessary.

## 0.3.21 (2026-10-10) - follow-up from live Kneeling observation

- An apparent loss of penetration with no upward stroke waits briefly for game contact to settle and no longer tightens the top-of-stroke margin. The log now records the actual upward travel and both contact signals.
- During lively Kneeling her hands seek independently reachable places on his torso, falling back to her own thighs; the hands-under-breasts pose no longer leaves both elbows raised during the stroke.

## 0.3.38 (2026-10-11) — detached Outfit renderers

- Installed 0.3.37 matched this worktree's previous Release DLL by SHA256 (081C2A83...). User reported garment texture remaining after Outfit removal with CMA Clothing Fix enabled.
- Runtime evidence: reports 20261011-181848-328-482922 through 182031-363 contain enabled Dress(Clone) at Dress[spruceless.clothing.clubdress], classified @body with no equipped Club Dress ID. It remains after Clothing Fix is disabled, so triangle masking alone does not account for the leftover geometry.
- WardrobeRemoval captures authoritative garment primary/subskin renderers before native RemovePieza(id, destroy:true). After successful removal, renderers not retained by another equipped item are disabled and destroyed. Shared renderers and unsuccessful native removals are preserved. Used for replacement, take-off and late load completion.
- Release build: zero warnings/errors. Six isolated checks pass with fake wardrobe/renderer lifetime; this does not emulate the native engine. User must restart and repeat apply/remove and rapid Outfit changes. Existing session orphans are not swept by name.
- Installed while game closed, with previous installed DLL saved next to it as .before-outfit-cleanup-0.3.38-<timestamp>.bak. No CMA Clothing Fix code or configuration changes.

## 0.3.39 — sequential native wardrobe loads

0.3.38 did not resolve the user's leftover Club Dress. Fresh session log proves 0.3.38 loaded, then Outfit selected Club Dress and Hoodie; only Hoodie completed its callback. Reports still contain active unregistered Dress(Clone), even with Clothing Fix disabled. Concurrent native AddPiezaAsync calls are the leading cause, not yet proven by an after-fix run.

Queued Outfit additions: Step starts only one native load while loading is empty. TakeOff clears unstarted additions; late completion removes the active item. Original restoration is serialized too, and failure retains original data without repeatedly retrying. Release build zero warnings/errors. Installed after user closed the game with backup .before-sequential-outfit-0.3.39.bak. Runtime apply/remove and rapid reapply checks pending. Ctrl+F12 toggles clipping masks; it is not the Outfit removal control.

## 0.3.40

Outfit reconciles native undress/hidden additions with retained retry state on failure. Added native glasses and exposed-geometry variant names. Material/color choices are staged until explicit application. Refactored catalog caching, material snapshots and validation; removed pure-choice dependency on native wardrobe class. Release builds clean; 34 catalog checks and 10 cleanup/synchronization checks pass. Runtime verification pending.

## 0.3.41 — Outfit menu regression

0.3.40 runtime log confirmed NotSupportedException in GUIContent.Temp(string[]) called by the new material SelectionGrid, closing the whole settings window. Replaced it with the existing Choice buttons in two-column rows. Added a source regression guard against reintroducing the stripped call. Material selection and explicit application remain. Native UI verification still required.

## 0.3.42 — material loading regression

Player.log confirms native LoaderMaterialesRutine throws NotSupportedException: Todos los materiales deben ser instancias. New selected material slots lacked the inherited instance flag and renderer slot index. Initialize materialSlot, field_Public_Boolean_0=true and color before loading. Keep generic PiezaDeRopaBase: the observed error is in material loading, not generic dispatch. Track missing addition/restoration callbacks after 60 seconds; block further loads, display a restart warning, retain original snapshot and queued load safety. Release build, 36 catalog/source guards and 13 removal/material checks pass. Native application needs user verification; instance flag semantics are inferred from the native loader error and the sole boolean on MaterialDataIDSlotPar.
