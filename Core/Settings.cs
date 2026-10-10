using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers.Interacciones;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace CMAScenes
{
    public sealed partial class Plugin
    {
        // Settings marked "what the window shows" have a row in the settings window; the others are the mod's
        // own mechanics, kept out of the window on purpose and changed in the config file
        // BepInEx\config\community.cma.scenes.cfg.
        // ---- The window and the keys ----
        internal static ConfigEntry<KeyCode> MenuKey;
        internal static ConfigEntry<string> Language;
        internal static ConfigEntry<float> WindowX;
        internal static ConfigEntry<float> WindowY;
        internal static ConfigEntry<float> WindowHeight;
        internal static ConfigEntry<float> UiScale;
        internal static ConfigEntry<float> UiDim;
        internal static ConfigEntry<string> UiFolds;
        internal static ConfigEntry<bool> WindowCollapsed;
        internal static ConfigEntry<KeyCode> ToggleKey;
        internal static ConfigEntry<KeyCode> RideKey;
        internal static ConfigEntry<KeyCode> BikiniKey;
        internal static ConfigEntry<KeyCode> CondomKey;
        internal static ConfigEntry<KeyCode> PresetKey;
        internal static ConfigEntry<KeyCode> FasterKey;
        internal static ConfigEntry<KeyCode> SlowerKey;
        internal static ConfigEntry<bool> ShowNotice;
        internal static ConfigEntry<bool> ShowStatus;
        internal static ConfigEntry<string> ActiveProfile;

        // ---- Rhythm and mood of both modes ----
        internal static ConfigEntry<string> Rhythm;
        internal static ConfigEntry<string> PresetName;
        internal static ConfigEntry<float> MoveSeconds;
        internal static ConfigEntry<float> Smoothness;
        internal static ConfigEntry<float> PoseRamp;
        internal static ConfigEntry<float> ShakeDelay;
        internal static ConfigEntry<float> LeanExtra;
        internal static ConfigEntry<float> SwayWidth;
        internal static ConfigEntry<float> CircleRadius;
        internal static ConfigEntry<float> RollDegrees;

        // ---- The twerk: what the window shows ----
        internal static ConfigEntry<string> TwerkPoseType;
        internal static ConfigEntry<float> Frequency;
        internal static ConfigEntry<float> BounceHeight;
        internal static ConfigEntry<string> HandsOn;
        internal static ConfigEntry<float> ButtAmplitude;
        internal static ConfigEntry<bool> ShakeChest;
        internal static ConfigEntry<float> LeanDegrees;
        internal static ConfigEntry<float> Arch;
        internal static ConfigEntry<float> SquatDepth;
        internal static ConfigEntry<float> Stance;

        // ---- The twerk: finer points, in this file only ----
        internal static ConfigEntry<float> TiltDegrees;
        internal static ConfigEntry<float> ChestAmplitude;
        internal static ConfigEntry<float> HipBack;
        internal static ConfigEntry<float> ToesOut;
        internal static ConfigEntry<float> HipWidth;
        internal static ConfigEntry<float> HipHeight;
        internal static ConfigEntry<float> HipForward;
        internal static ConfigEntry<float> ElbowBack;

        // ---- The sex: what the window shows ----
        internal static ConfigEntry<string> RideHole;
        internal static ConfigEntry<string> RidePoseType;
        internal static ConfigEntry<float> RideFrequency;
        internal static ConfigEntry<float> RideDepth;
        internal static ConfigEntry<float> RideBounce;
        internal static ConfigEntry<bool> RideLead;
        internal static ConfigEntry<bool> RideAlive;
        // The X-ray (XRay.cs): on or off, its key, what of it is shown, and (in the file only) how it looks.
        internal static ConfigEntry<bool> XRay;
        // The place of the sex (Places.cs): which, the key that picks the spot looked at, and what is set by hand.
        internal static ConfigEntry<string> PlaceUse;
        internal static ConfigEntry<KeyCode> PlacePickKey;
        internal static ConfigEntry<float> PlaceAlong, PlaceAcross, PlaceHeight, PlaceTurn;
        internal static ConfigEntry<bool> PlaceFade, PlaceTour;
        // Her arms posed by what an arm can do (Wrist.cs).
        internal static ConfigEntry<bool> RideNaturalArms;
        internal static ConfigEntry<KeyCode> XRayKey;
        internal static ConfigEntry<string> XRayView, XRayInsetSide;
        internal static ConfigEntry<bool> XRayOwnCanal;
        internal static ConfigEntry<float> XRayOpacity, XRayInsetSize, XRayBelow, XRayAfter;
        internal static ConfigEntry<bool> XRayInBody;
        internal static ConfigEntry<bool> RideFlow;
        internal static ConfigEntry<float> RideHeMeets, RideWomb;
        internal static ConfigEntry<bool> RideFeetAuto, RideHeHolds;
        internal static ConfigEntry<string> RideFinish;
        internal static ConfigEntry<bool> RideEndAfter;
        internal static ConfigEntry<string> RideHandsOn;
        internal static ConfigEntry<string> RideLegs;
        internal static ConfigEntry<float> RideLean;
        internal static ConfigEntry<bool> CondomAuto;
        internal static ConfigEntry<string> CondomColor;

        // ---- The sex: her living body, the parts of RideAlive ----
        internal static ConfigEntry<bool> RideWarmUp;
        internal static ConfigEntry<bool> RideListen;
        internal static ConfigEntry<bool> RideTires;
        internal static ConfigEntry<float> RideStamina;
        internal static ConfigEntry<bool> RideBreath;
        internal static ConfigEntry<bool> RideWaves;
        internal static ConfigEntry<bool> RideBuild;
        internal static ConfigEntry<bool> RideClimax;

        // ---- The sex: the mod's own mechanics, in this file only ----
        internal static ConfigEntry<string> RideFacing;
        internal static ConfigEntry<float> RideSquat;
        internal static ConfigEntry<float> RideSink;
        internal static ConfigEntry<float> RideSpring;
        internal static ConfigEntry<float> RideFeet;
        internal static ConfigEntry<float> RideHipsWork;
        internal static ConfigEntry<float> RideGrind;
        internal static ConfigEntry<float> RideTilt;
        internal static ConfigEntry<float> RideMaxSpeed;
        internal static ConfigEntry<bool> RideHandsFast;
        internal static ConfigEntry<bool> RideLookAtHim;
        internal static ConfigEntry<float> RideEnterSpeed;
        internal static ConfigEntry<bool> RideAutoAim;
        internal static ConfigEntry<float> RideAimForward;
        internal static ConfigEntry<float> RideAimUp;
        internal static ConfigEntry<float> RideAnalAimForward;
        internal static ConfigEntry<float> RideAnalAimUp;
        internal static ConfigEntry<bool> RideHoldHips;
        internal static ConfigEntry<bool> RideKeepHard;
        internal static ConfigEntry<bool> RideFlatFeet;
        internal static ConfigEntry<float> RideHeroClearance;
        internal static ConfigEntry<float> RideHeroShift;
        internal static ConfigEntry<bool> RideCamSteady;
        internal static ConfigEntry<float> RideFov;
        internal static ConfigEntry<float> RideWetShine;
        internal static ConfigEntry<bool> RideWetRelief;
        internal static ConfigEntry<float> RideWetDepth;
        internal static ConfigEntry<bool> RideWetStrings;

        // ---- The hero ----

        // ---- The bikini ----
        internal static ConfigEntry<string> BikiniTop;
        internal static ConfigEntry<string> BikiniBottom;
        internal static ConfigEntry<string> BikiniColor;
        internal static ConfigEntry<bool> BikiniSolid;
        internal static ConfigEntry<bool> BikiniWithTwerk;
        internal static ConfigEntry<string> BikiniSort;

        // ---- For debugging ----
        internal static ConfigEntry<bool> DebugPerf;

        // One of the settings of her living body (the warm-up, tiring, breathing and the like), which all go off
        // together with the one switch for them in the window.
        internal static bool Alive(ConfigEntry<bool> part)
        {
            return RideAlive.Value && part.Value;
        }

        private void BindSettings()
        {
            // ---- The window and the keys ----
            MenuKey = Config.Bind("Input", "MenuKey", KeyCode.J, "Key that opens or closes the settings window (bikini, colour, twerk and ride settings).");
            Language = Config.Bind("UI", "Language", "auto", "Language of the settings window and of the notices on the screen: ru, en, or auto (Russian when the system is in Russian, English otherwise).");
            WindowX = Config.Bind("UI", "WindowX", 24f, "Where the settings window was left: distance from the left edge of the screen.");
            WindowY = Config.Bind("UI", "WindowY", 24f, "Where the settings window was left: distance from the top of the screen.");
            WindowHeight = Config.Bind("UI", "WindowHeight", 0f, "Height of the settings window (drag its bottom-right corner). 0 = as tall as fits the screen.");
            UiScale = Config.Bind("UI", "Scale", 1f, "Size of the settings window: 0.75 to 1.5.");
            UiDim = Config.Bind("UI", "Brightness", 0.62f, "The game shows what the plugin draws brighter than it is meant (measured: about 1.6 times), which washes the colours of the settings window out. Its colours are darkened by this much to make up for it: 1 leaves them as they are.");
            UiFolds = Config.Bind("UI", "Folds", "", "Which of the folding cards of the settings window are open (kept by the window itself).");
            WindowCollapsed = Config.Bind("UI", "Collapsed", false, "The settings window is folded to a small bar with the start buttons.");
            ToggleKey = Config.Bind("Input", "ToggleKey", KeyCode.H, "Key that starts or stops the twerk.");
            RideKey = Config.Bind("Ride", "Key", KeyCode.L, "Key that starts or stops the ride: your view drops low in front of her and she rides you.");
            BikiniKey = Config.Bind("Bikini", "Key", KeyCode.K, "Key that puts her in the bikini, or back in her clothes.");
            CondomKey = Config.Bind("Condom", "Key", KeyCode.I, "Key that gets a condom out during the ride (it lies on his belly, to be taken with his hand and brought to his tip), or takes off the one he wears.");
            PresetKey = Config.Bind("Input", "PresetKey", KeyCode.P, "Key that switches to the next preset: Slow, Soft, Fast, Mine.");
            FasterKey = Config.Bind("Input", "FasterKey", KeyCode.KeypadPlus, "Key that makes the ride (or the twerk, while she twerks) a little faster. None: no key.");
            SlowerKey = Config.Bind("Input", "SlowerKey", KeyCode.KeypadMinus, "Key that makes the ride (or the twerk, while she twerks) a little slower. None: no key.");
            ShowNotice = Config.Bind("Input", "ShowNotice", true, "Show a line at the top of the screen while she twerks or rides: what is running and which key stops it.");
            ShowStatus = Config.Bind("Input", "ShowStatus", true, "Under that line, while she rides: what she is doing, the pose, her speed and how aroused she is.");
            ActiveProfile = Config.Bind("Profiles", "Active", "", "The name of the profile loaded or saved last in the window.");

            // ---- Rhythm and mood of both modes ----
            Rhythm = Config.Bind("Behavior", "Rhythm", "Scenario", "The rhythm of the motion: Steady (one bounce), Random (a move chosen now and then) or Scenario (a fixed sequence that loops: a slow circle, bounces, a pause, a faster stretch, side to side).");
            PresetName = Config.Bind("Behavior", "Preset", "Mine", "Slow (unhurried, full depth), Soft (slower and gentler), Fast (quicker) or Mine (your own settings exactly as they are). The preset only changes the speed and the depth of the motion on top of them.");
            MoveSeconds = Config.Bind("Behavior", "MoveSeconds", 6f, "About how long she keeps each move, in seconds.");
            Smoothness = Config.Bind("Motion", "Smoothness", 0.5f, "How smooth the motion is, from 0 to 1: every part of it is eased a little, so that nothing moves in steps and the knocks of the bounce are rounded off. 0 is the raw motion; more also takes some of the depth off a fast bounce.");
            PoseRamp = Config.Bind("Pose", "PoseRamp", 1.5f, "Seconds a transition takes: getting into and out of a pose, changing from the twerk to the ride (out of the one, then into the other), and moving to a new pose or new place for her hands. Every one of them starts and ends gently.");
            ShakeDelay = Config.Bind("Pose", "ShakeDelay", 0.9f, "Seconds after pressing the key before the bouncing starts.");
            LeanExtra = Config.Bind("Behavior", "LeanExtra", 15f, "Extra forward lean she drifts into now and then, in degrees.");
            SwayWidth = Config.Bind("Behavior", "SwayWidth", 0.07f, "Side to side move: how far the hips swing, in metres.");
            CircleRadius = Config.Bind("Behavior", "CircleRadius", 0.055f, "Hip circle move: radius of the circle, in metres.");
            RollDegrees = Config.Bind("Behavior", "RollDegrees", 4f, "Side to side and circle moves: how far the pelvis rolls, in degrees.");

            // ---- The twerk: what the window shows ----
            TwerkPoseType = Config.Bind("Pose", "Type", "", "The twerk pose chosen last in the window: Bent over, Deep squat, Hands on hips or Low arch (empty until one is chosen). Choosing one sets the lean, the arch, the squat, the stance and the hands.");
            Frequency = Config.Bind("Motion", "Frequency", 2.4f, "Bounces per second (the tempo drifts around this).");
            BounceHeight = Config.Bind("Motion", "BounceHeight", 0.035f, "How far the hips go up and down, in metres.");
            HandsOn = Config.Bind("Pose", "HandsOn", "Knees", "Where the hands rest: Hips, Knees or None.");
            ButtAmplitude = Config.Bind("Motion", "ButtAmplitude", 0.03f, "Extra up and down movement of the glute bones, in metres.");
            ShakeChest = Config.Bind("Motion", "ShakeChest", false, "Also bounce the chest.");
            LeanDegrees = Config.Bind("Pose", "LeanDegrees", 58f, "How far she bends forward, in degrees (mostly hinged at the hips).");
            Arch = Config.Bind("Pose", "Arch", 16f, "How far the lower back is arched, in degrees: the pelvis tips forward, the lower back curves the other way and the upper body stays where the lean puts it, which pushes the bottom up and back.");
            SquatDepth = Config.Bind("Pose", "SquatDepth", 0.2f, "How far the hips drop, in metres. The feet stay planted and the knees bend.");
            Stance = Config.Bind("Pose", "Stance", 0.22f, "How far each foot is moved out to the side from where it stands, in metres: a twerk is done with the feet wide apart.");

            // ---- The twerk: finer points, in this file only ----
            TiltDegrees = Config.Bind("Motion", "TiltDegrees", 8f, "How much the pelvis rocks with each bounce, in degrees.");
            ChestAmplitude = Config.Bind("Motion", "ChestAmplitude", 0.012f, "How far the chest bones move, in metres.");
            HipBack = Config.Bind("Pose", "HipBack", 0.12f, "How far the hips are pushed back, in metres.");
            ToesOut = Config.Bind("Pose", "ToesOut", 20f, "How far the toes are turned outward, in degrees. The knees are pushed out over them.");
            HipWidth = Config.Bind("Pose", "HipWidth", 0.09f, "Hands on hips: how far out from the hip joint the wrist sits, in metres.");
            HipHeight = Config.Bind("Pose", "HipHeight", 0.15f, "Hands on hips: how far up the flank from the hip joint the wrist sits, in metres.");
            HipForward = Config.Bind("Pose", "HipForward", 0.03f, "Hands on hips: how far behind the hip joint the wrist sits (the fingers then reach the front), in metres.");
            ElbowBack = Config.Bind("Pose", "ElbowBack", 0.4f, "How far the elbows point backwards as well as outwards.");

            // ---- The sex: what the window shows ----
            RideHole = Config.Bind("Ride", "Hole", "Vagina", "Which of her holes she takes him in during the ride: Vagina or Anus.");
            RidePoseType = Config.Bind("Ride", "PoseType", "", "The ride pose chosen last in the window: Cowgirl, Astride, Over him, Leaning back or Reverse (empty until one is chosen). Choosing one sets the hands, the legs, the facing and the lean.");
            RideFrequency = Config.Bind("Ride", "Frequency", 1.1f, "Strokes per second (the tempo drifts around this). A woman riding keeps up about one stroke a second, and a quick spell is some one and a half.");
            RideDepth = Config.Bind("Ride", "Depth", 1f, "How much of his penis she takes when she sits down: 1 is all of it (down to his skin), 0.5 half of it.");
            RideBounce = Config.Bind("Ride", "Bounce", 0.09f, "The length of the stroke, in metres: how far her hips rise from where she sits on him before they come down again.");
            PlaceUse = Config.Bind("Place", "Use", "", "Where the sex is: empty for where she stands, or the name of a place of the room (the window lists them: the furniture it finds, and the spot that was looked at).");
            PlacePickKey = Config.Bind("Place", "PickKey", KeyCode.None, "Key that puts the sex on the spot the player looks at: the top of a bench, of a table, the floor. None by default: the window has a button for it.");
            PlaceAlong = Config.Bind("Place", "Along", 0f, "Moves the sex along the place (toward his head), in metres, from where the mod puts it.");
            PlaceAcross = Config.Bind("Place", "Across", 0f, "Moves the sex across the place, in metres.");
            PlaceHeight = Config.Bind("Place", "Height", 0f, "Raises or lowers the sex on the place, in metres: for a top that was measured wrong (a couch taken at the height of its back).");
            PlaceTurn = Config.Bind("Place", "Turn", 0f, "Turns the sex on the place, in degrees: 180 lays him the other way round.");
            PlaceTour = Config.Bind("Place", "Tour", false, "After each orgasm of hers she takes him on to the next place of the room (the furniture that was found, the spot that was looked at, and where she stands).");
            PlaceFade = Config.Bind("Place", "Fade", true, "Cut to black for a moment as she is carried to the place and back: she is carried at once, not along a path.");
            RideNaturalArms = Config.Bind("Ride", "NaturalArms", true, "Her arms are posed by what an arm can do: her elbow goes where her wrist is spared, her hand is turned no further than a wrist turns, and the bones along her forearm carry its turn. Off: as CMA Shake posed them.");
            XRay = Config.Bind("XRay", "On", false, "The X-ray: during the sex her insides (as the game itself has them: her canal, her womb, her rectum), what of his penis is in them, and his semen in her as the game has it (filling her, and running out of her while the game lets it out) are shown. Switched by its key and in the window.");
            XRayKey = Config.Bind("XRay", "Key", KeyCode.U, "Key that switches the X-ray on and off.");
            XRayView = Config.Bind("XRay", "View", "Both", "What of the X-ray is shown: Through (her insides and his penis drawn over her body, wherever one looks from), Inset (a cut through her from front to back, in a frame at the side of the screen), Both, or Game (the game's own X-ray of her pelvis instead: its camera and its frame, switched on for the sex whatever level the agency has reached).");
            XRayOwnCanal = Config.Bind("XRay", "OwnCanal2", false, "Through her body: while he is in her, draw a canal of the plugin's own round him instead of the game's (off: the game's canal, which stretches by itself; it hid the game's semen leaving her).");
            XRayOpacity = Config.Bind("XRay", "Opacity", 0.55f, "Through her body: how solid her insides are drawn, from 0.15 (a faint veil) to 1.");
            XRayInBody = Config.Bind("XRay", "InHerBody2", false, "Through her body: of what of him is in her, show only what her body hides (by the picture's depth). Used when her skin writes its depth. He is cut at her entrance either way (the setting below).");
            // (A key of its own: "BelowEntrance" was 3 cm, and drew him down onto his own belly. The old key is not read.)
            XRayBelow = Config.Bind("XRay", "CutBelowEntrance", 0.5f, "Through her body: how many centimetres of him below the game's point of her entrance are still drawn; -1 to 3. Nothing of him is ever drawn below where his shaft comes out of his own skin. More if a dark gap shows at her lips, less if his penis shows on his belly.");
            XRayAfter = Config.Bind("XRay", "AfterSex", 90f, "After the sex the X-ray stays on her for up to this many seconds while the game still has his semen in her, so that it can be watched leaving her (the game lets it out once nothing is in her any more). 0: the X-ray goes with the sex.");
            XRayInsetSize = Config.Bind("XRay", "InsetSize", 0.36f, "The inset: its height as a part of the height of the screen, 0.2 to 0.7.");
            XRayInsetSide = Config.Bind("XRay", "InsetSide", "Right", "The inset: the side of the screen it is at, Right or Left.");
            RideLead = Config.Bind("Ride", "SheLeads", true, "She finds her own rhythm and her own depth, by what the game itself has of her body: the tempo is turned until the speed of him in her is the one the game's rhythm has her like best (it wanders, and is another one near her climax and after it), and she takes him just short of where the game has it hurt her. Off: the speed and depth that are set, as they are.");
            RideAlive = Config.Bind("Ride", "Alive", true, "Her living body, all of it at once: she warms up before she takes him deep, eases off while it hurts, tires and gets her breath back, breathes, and her tempo comes in waves. Off: none of that (each part has a setting of its own in this file as well).");
            RideFlow = Config.Bind("Ride", "PoseFlow", false, "She changes pose by herself as she gets aroused: Cowgirl, then Astride, then Over him, and back to Cowgirl after a climax.");
            RideHeMeets = Config.Bind("Ride", "ThrustHeight", 0.045f, "He meets her strokes: his hips come up by this much, in metres, as she comes down on him, in time with her (0: he lies still), and she is lifted by them. The mouse wheel still lifts his hips by hand, on top of it.");
            RideWomb = Config.Bind("Ride", "WombReach", 0.035f, "Her womb goes on from her vagina: when he comes in her, his head goes in through its neck by this much, in metres (her womb draws back round it in the X-ray). 0: his head stays in her canal.");
            RideFeetAuto = Config.Bind("Ride", "FeetFindGround", true, "Her feet find the ground under them (a ray down at each): the floor, a rug, the top of a bench, a step; her ankles stand as high above it as the game's pose has them (with heels, higher), and the soles lie along a slope. Off: her feet go by the height of the floor under her hips alone.");
            RideHeHolds = Config.Bind("Ride", "HeHolds", false, "He holds her: his hands take her by the hips and her bottom and he moves her on himself with his own hips, stroke by stroke; she lets him, her hands on his arms and chest.");
            RideFinish = Config.Bind("Ride", "Finish", "Free", "What she does as he nears coming (by the game's own pleasure of him): Free (nothing: she rides on), Edge (she all but stops, sitting on him, until he has cooled a little, again and again) or Together (she holds him back like that only until she is nearly there herself, then goes all out).");
            RideEndAfter = Config.Bind("Ride", "GetOffAfterHeComes", false, "After he has come in her she stays on him for a while, then gets off him by herself: the ride ends.");
            RideHandsOn = Config.Bind("Ride", "HandsMode", "Auto", "Where her hands are during the ride: Auto (she finds where to hold on to him herself: his chest, his belly, his shoulders; in her orgasm she takes hold of him and pulls), Behind (leaning back, on his thighs behind her), Thighs (on her own thighs), UnderBreasts (under her breasts) HisChest (on him, as far up toward his chest as she reaches) or Shoulders (on the floor just past his shoulders: she bends over him). With her back to him the last two stay on her thighs. Anything else is taken as Thighs.");
            RideLegs =Config.Bind("Ride", "Legs", "Feet", "How she is on him: Feet (squatting over him, her feet on the floor either side of him) or Knees (astride him, kneeling, her shins on the floor either side of him).");
            RideLean = Config.Bind("Ride", "Lean", 40f, "How far she leans forward over you, in degrees.");
            // (His penis gets wet from her, always: the switch "Ride/Wet" is gone from the window and is not read any more.)
            CondomAuto = Config.Bind("Condom", "PutOnByItself", false, "On: the condom goes onto him by itself as soon as she has come up off him, without his hand.");
            CondomColor = Config.Bind("Condom", "Color", "Clear", "The colour of the condom: Clear, Pink, Red, Blue or Black.");

            // ---- The sex: her living body, the parts of RideAlive ----
            RideWarmUp = Config.Bind("Ride", "WarmUp", true, "She warms up: to begin with she takes him shallow (a little over half of what the depth is set to) and slowly, deeper and quicker over the first half a minute or so of him in her, sooner the more she likes it.");
            RideListen = Config.Bind("Ride", "ListenToHerBody", true, "She listens to her body: while the game has her in pain (too deep, too soon) she takes him shallower and slower, and comes back to it once it has passed.");
            RideTires = Config.Bind("Ride", "Stamina", true, "Riding tires her: sooner on her feet than on her knees, sooner the faster she goes. Tired she slows down; out of breath she sits down on him and grinds until she has her breath back.");
            RideStamina = Config.Bind("Ride", "Endurance", 1.5f, "How long she lasts before she has to get her breath back: 1 is about a minute and a quarter of steady riding on her feet at one stroke a second, 2 twice that.");
            RideBreath = Config.Bind("Ride", "Breathing", true, "Her chest rises and falls with her breath, harder when she is tired and hardest right after a climax.");
            RideWaves = Config.Bind("Ride", "Waves", true, "The ride comes in waves: slower, then faster, and after a climax she stays still and breathes for a while before she goes on.");
            RideBuild = Config.Bind("Ride", "BuildUp", true, "(Part of her living body.) The ride speeds up as she gets aroused: up to a fifth faster than the speed that is set when she is as aroused as she gets.");
            RideClimax = Config.Bind("Ride", "Climax", true, "When he comes in her, or when she is as aroused as the game has her get, the ride stops for a few seconds: she is pressed down on him and trembles, then goes on.");

            // ---- The sex: the mod's own mechanics, in this file only ----
            RideFacing = Config.Bind("Ride", "Facing", "Face", "Which way she faces during the ride: Face (toward the hero, his head under her face) or Away (reverse: her back to him, his head behind her, her hands on his thighs or knees in front).");
            RideSquat = Config.Bind("Ride", "Squat", 0.64f, "How far her hips drop, in metres: the lowest point of the stroke. The feet stay planted and the knees fold. She cannot go lower than the hero's body: where it is in the way, the bottom of the stroke is on him.");
            RideSink = Config.Bind("Ride", "SinkIntoHim", 0.04f, "How far she may sink into the hero's body, in metres, as soft bodies give: 0 keeps her just touching him, more lets her settle lower onto him.");
            RideSpring = Config.Bind("Ride", "Springiness", 0.6f, "How much her body gives as she lands on him: 0 touches him and comes up again, 1 sinks about 2 cm further at the very bottom of every stroke. It is a smooth sinking, not a bounce.");
            RideFeet = Config.Bind("Ride", "FeetApart", 0.27f, "She squats over the hero with her feet on the floor either side of him: how far each foot is from the middle of her, in metres. They are never closer than just outside his body.");
            RideHipsWork = Config.Bind("Ride", "HipsWork", 1f, "How much it is her hips that do the work: as they rise her body hinges forward and her lower back arches, so her bottom goes up and back while her shoulders rise far less. 0 moves her up and down as one piece.");
            RideGrind = Config.Bind("Ride", "Grind", 0.06f, "How far her hips rock forward and back, in metres.");
            RideTilt = Config.Bind("Ride", "Tilt", 9f, "How much her pelvis rocks with each stroke, in degrees.");
            RideMaxSpeed = Config.Bind("Ride", "MaxSpeed", 1.7f, "The most strokes per second she ever makes, whatever the speed, the preset, the rhythm and her arousal ask for: close to it everything is eased down so that it is never passed. A real woman on top does not keep up more than some one and a half to two strokes a second, and not for long.");
            RideHandsFast = Config.Bind("Ride", "HandsAsideWhenFast", true, "When she rides faster than the speed that is set (the Fast preset, a quick spell of the rhythm, or her arousal), she puts her hands on the floor either side of him and takes her weight on them. Only facing him.");
            RideLookAtHim = Config.Bind("Ride", "LookAtHim", true, "While she rides she looks at the hero: her head and eyes are turned to his face, wherever the camera is (the game itself has her look at the camera, which in the free camera is not him).");
            RideEnterSpeed = Config.Bind("Ride", "EnterSpeed", 0.08f, "How fast she lowers herself onto him at the start, in metres a second.");
            RideAutoAim = Config.Bind("Ride", "AutoAim", true, "The aim corrects itself: her entrance is taken to be the ring her hole really opens by, as the game has it, followed as she moves, so that she comes down with it on his tip. The manual aim is used only with this off.");
            RideAimForward = Config.Bind("Ride", "AimForward", 0f, "Moves the place that is taken for her entrance forward (+) or back (-) along the way she faces, in metres: for when he goes past her.");
            RideAimUp = Config.Bind("Ride", "AimUp", 0f, "Moves the place that is taken for her entrance up (+) or down (-), in metres.");
            RideAnalAimForward = Config.Bind("Ride", "AnalAimForward", 0f, "Anal: moves the place taken for her entrance along the way she faces, in metres.");
            RideAnalAimUp = Config.Bind("Ride", "AnalAimUp", 0f, "Anal: moves the place taken for her entrance up or down, in metres.");
            RideHoldHips = Config.Bind("Ride", "HoldHipsForTheGame", true, "Her hips are kept where she is drawn for the whole of the game's frame, not only when it is drawn: the game looks for his penis at her entrance before she is posed, and without this it finds her standing, her entrance far above him, and never lets him in (squatting on her feet above all).");
            RideKeepHard = Config.Bind("Ride", "KeepHimHard", true, "She can only take him in while he is hard. With this on the game is asked to keep him hard for as long as the ride lasts.");
            RideFlatFeet = Config.Bind("Ride", "FlatFeetInHeels", false, "The game's heel system stands her on her toes and lifts her pelvis by the height of her heels after she is posed. That lift is measured and taken up by itself, so she keeps her heels. On: her heel system is let off for the sex instead (she stands flat, her feet in her shoes) and given back after; for when taking it up does not work.");
            RideHeroClearance = Config.Bind("Ride", "SkinGap", 0.003f, "The gap between his skin and the surface he lies on (floor, bench, table), in metres. The surface is found by rays under him; raise it if he sinks into it, lower it if he floats.");
            RideHeroShift = Config.Bind("Ride", "HeroShift", 0f, "Moves the hero along his own body, in metres (positive = toward his head).");
            RideCamSteady = Config.Bind("Ride", "SteadyCamera", false, "Smooths the hero's view while he lies under her, so it does not shake.");
            RideFov = Config.Bind("Ride", "FieldOfView", 0f, "The hero's field of view during the ride, in degrees; 0 leaves the game's own.");
            RideWetShine = Config.Bind("Ride", "WetShine", 0.8f, "How much of her fluids shows on him at most: 0 none, 1 the most.");
            RideWetRelief = Config.Bind("Ride", "WetRelief", true, "Her fluids on him are a thin clear film with a shape of its own: a normal map (his skin's own relief kept in it) smooths his skin under the film, so the highlights on him run long and clean, with a soft swell where wet meets dry and long soft runs along the shaft. Off: only the gloss of the wet look.");
            RideWetDepth = Config.Bind("Ride", "WetReliefDepth", 1f, "How strongly the shape of the liquid shows: 1 as it is, below 1 flatter, above 1 bolder (up to 3).");
            RideWetStrings = Config.Bind("Ride", "WetStrings", true, "Threads of her fluid are drawn out between his tip and her vagina (or her lips) as she comes off him; they sag, thin and snap.");

            // ---- The hero ----

            // ---- The bikini ----
            BikiniTop = Config.Bind("Bikini", "Top", "microbikinitop, bikinitop", "Part of the in-game name or id of the top, from the installed clothing mods. Several can be listed, comma separated; the first that exists is used.");
            BikiniBottom = Config.Bind("Bikini", "Bottom", "bikinibot, tanga, gstring", "Part of the in-game name or id of the bottom. Several can be listed; the first that exists is used.");
            BikiniColor = Config.Bind("Bikini", "Color", "#D81B60", "Colour of the bikini as #RRGGBB.");
            BikiniSolid = Config.Bind("Bikini", "SolidColor", true, "Use a plain colour instead of the item's own texture.");
            BikiniWithTwerk = Config.Bind("Bikini", "WithTwerk", false, "Put the bikini on automatically when the twerk starts, and take it off when it stops.");
            BikiniSort = Config.Bind("UI", "ClothingSort", "Name", "How the list of clothing is sorted: Name (A to Z), NameDesc (Z to A) or Kind (by the kind of clothing, then by name).");

            // ---- For debugging ----
            DebugPerf = Config.Bind("Debug", "Perf", true, "While the plugin is being tuned: every 20 seconds the log says how many milliseconds each part of the plugin takes in a frame, and how long the frame itself is.");
            Prof.Enabled = DebugPerf.Value;
            DebugPerf.SettingChanged += (s, e) => Prof.Enabled = DebugPerf.Value;
        }
    }
}
