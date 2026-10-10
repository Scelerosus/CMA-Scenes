using System;
using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;

namespace CMAScenes
{
    // The ride: you are the first-person camera, so the camera drops low in front of her and she folds into a wide
    // squat over where you would be, leaning toward you with her hands reaching for your chest, and moves with the
    // same choreography as the twerk, at a slower pace. The hero, the male character the camera is attached to, is
    // carried with it: he lies on his back under her while it lasts and goes back to where he stood when it ends.
    public sealed partial class ShakeController
    {
        private bool m_ride;
        private Woman m_rideWoman;
        private Vector3 m_camPos;
        private bool m_camValid;
        private bool m_rideGeoInit;
        private bool m_herCrotchValid;
        private Vector3 m_herCrotch;
        private Vector3 m_crotchS;
        // Where her vagina is from the middle of her hip joints, across the floor, smoothed so her rocking does not move him.
        private Vector3 m_vulvaOff;
        private bool m_vulvaOffValid;
        private const float CamDistance = 0.6f;
        private const float CamHeight = 0.38f;
        private Vector3 m_rideFwd = Vector3.forward;
        private Vector3 m_ridePelvis;
        private Vector3 m_rideAnchor;
        private float m_rideGroundY;
        // How deep he is in her right now (0: out of her, 1: all the way), and the frame it was last worked out in.
        private float m_rideIn;
        private int m_rideInFrame = -10;
        private Vector3 m_lastHipOffset;
        private string m_ridePoseName = "";
        private float m_ridePoseChangedAt = -10f, m_rideKneelFrom;

        // The nearest woman to the player's view is the one who rides.
        private void StartRide()
        {
            // Started again while she is still in the pose of the ride before: the same woman keeps the layout she has, so
            // that nothing of it is found afresh and jumps.
            Woman before = m_poseW > 0.001f ? m_rideWoman : null;
            // (Where the sex is: Places.cs. At another place than the one before, nothing of the old layout holds.)
            Place placeNext = PlaceResolve();
            if (placeNext != m_place)
            {
                before = null;
                PlaceCarryOff();
            }
            m_place = placeNext;
            m_carryFails = 0;
            bool hadCam = m_camValid, hadGeo = m_rideGeoInit, hadCrotch = m_herCrotchValid, hadVulva = m_vulvaOffValid;
            m_rideWoman = null;
            m_camValid = false;
            m_rideGeoInit = false;
            m_herCrotchValid = false;
            m_vulvaOffValid = false;
            m_heroLowTries = 0;
            m_floorKnown = false;
            PoseFixReset();
            m_ridePoseName = "";
            m_ridePoseChangedAt = -10f;
            m_chestFar[0] = m_chestFar[1] = 0.3f;
            m_chestOk[0] = m_chestOk[1] = 0f;
            m_chestLean = 0f;
            m_bendCut = 0f;
            m_heroLift = 0f;
            m_heroBaseSet = false;
            m_heroBaseAt = 0f;
            m_heroSunkLogs = 0;
            m_noHeroTold = false;
            MountReset();
            BodyReset();
            Camera cam = Camera.main;
            if (m_women.Count == 0)
            {
                FindWomen();
            }
            float best = float.MaxValue;
            foreach (Woman w in m_women)
            {
                if (w == null || w.hip == null)
                {
                    continue;
                }
                float d = cam != null ? (w.hip.position - cam.transform.position).sqrMagnitude : 0f;
                if (d < best)
                {
                    best = d;
                    m_rideWoman = w;
                }
            }
            if (before != null && m_rideWoman == before)
            {
                m_camValid = hadCam;
                m_rideGeoInit = hadGeo;
                m_herCrotchValid = hadCrotch;
                m_vulvaOffValid = hadVulva;
            }
            Plugin.ModLog.LogInfo(m_rideWoman != null ? $"Ride: with '{m_rideWoman.name}'." : "Ride: no model found yet.");
        }

        // (Kept from frame to frame instead of made anew in every one.)
        private static readonly Vector3[] s_rideFootPos = new Vector3[2], s_rideChestSpot = new Vector3[2];
        private static readonly Quaternion[] s_rideFootRot = new Quaternion[2];
        private static readonly float[] s_rideHandOff = new float[2];

        private void ApplyRide(Woman w, float poseW, float shakeW, Motion mo)
        {
            float phase = mo.phase;
            BeginAll(w);

            if (poseW <= 0.0005f && shakeW <= 0.0005f)
            {
                ReleaseAll(w);
                PlaceCarryOff();
                return;
            }
            // With a place set for the sex: she is carried there, whole, before anything of her is read. Everything
            // below then finds her there and does what it did where she stood. (Places.cs)
            PlaceCarry(w);

            Vector3 right = Vector3.ProjectOnPlane(w.legs[1].upper.t.position - w.legs[0].upper.t.position, Vector3.up).normalized;
            Vector3 fwd = Vector3.Cross(right, Vector3.up);

            // Where her feet are on the floor, before anything moves.
            Vector3[] footPos = s_rideFootPos;
            Quaternion[] footRot = s_rideFootRot;
            for (int i = 0; i < 2; i++)
            {
                footPos[i] = w.legs[i].end.t.position;
                footRot[i] = w.legs[i].end.t.rotation;
            }
            float groundY = Mathf.Min(footPos[0].y, footPos[1].y);
            Vector3 pelvisBase = w.sPelvis.t.position;

            // The ride is laid out from the unposed body, and the layout is eased: her own idle sway must not move him.
            float geoK = 1f - Mathf.Exp(-Mathf.Clamp(Time.deltaTime, 0.004f, 0.1f) / 0.3f);
            // Her own idle animation shifts her hips about (8 cm from side to side in the log), and he, laid to match her, was
            // carried about with them. Her hips are held to an anchor instead: the place they have on average, which follows
            // where the game has her only very slowly. So her crotch stays in one place over him, and he stays still.
            Vector3 baseCrotch = 0.5f * (w.legs[0].upper.t.position + w.legs[1].upper.t.position);
            bool firstGeo = !m_rideGeoInit;
            if (firstGeo)
            {
                m_rideAnchor = baseCrotch;
            }
            else
            {
                // The anchor all but stands still (half a minute to follow), so the sway, which comes back every few
                // seconds, is held off whole and not just smoothed. Only when the game has truly moved her does it go after
                // her: quickly from 15 cm away, almost at once from 30 cm.
                Vector3 off = baseCrotch - m_rideAnchor;
                off.y = 0f;
                float gone = off.magnitude;
                float tau = gone > 0.3f ? 0.4f : gone > 0.15f ? 1.5f : 30f;
                m_rideAnchor = Vector3.Lerp(m_rideAnchor, baseCrotch, 1f - Mathf.Exp(-Mathf.Clamp(Time.deltaTime, 0.004f, 0.1f) / tau));
            }
            // How far her hips are carried back to the anchor: never so far that her legs, planted where the game has her
            // feet, would be stretched.
            Vector3 hold = Vector3.ClampMagnitude(new Vector3(m_rideAnchor.x - baseCrotch.x, 0f, m_rideAnchor.z - baseCrotch.z), 0.12f);
            if (firstGeo)
            {
                m_rideFwd = fwd;
                m_rideGroundY = groundY;
                m_groundStart = groundY;
                m_ridePelvis = pelvisBase;
                m_rideGeoInit = true;
                Instance.FloorLook(new Vector3(baseCrotch.x, 0f, baseCrotch.z), groundY, true);
            }
            else
            {
                m_rideFwd = Vector3.Slerp(m_rideFwd, fwd, geoK).normalized;
                // (Within a few centimetres of where it was: her feet kneeling are lower than standing, and this followed them
                // down, taking the floor of the hero with it.)
                m_rideGroundY = Mathf.Lerp(m_rideGroundY, Mathf.Clamp(groundY, m_groundStart - 0.03f, m_groundStart + 0.1f), geoK);
                Instance.FloorLook(new Vector3(baseCrotch.x, 0f, baseCrotch.z), m_groundStart, false);
                // The pelvis the ride is laid out from is the held one too, so the view point and his aim do not sway either.
                m_ridePelvis = Vector3.Lerp(m_ridePelvis, pelvisBase + hold, geoK);
            }
            Vector3 pin = hold * poseW;
            m_camPos = new Vector3(m_ridePelvis.x, m_rideGroundY, m_ridePelvis.z) + m_rideFwd * CamDistance + Vector3.up * CamHeight;
            m_camValid = true;

            float skew = Mathf.Lerp(0.45f, 0.2f, Plugin.Smoothness.Value);
            float s = Mathf.Sin(phase);
            float c = Mathf.Cos(phase);
            float sB = Skew(phase, skew);
            float cB = Skew(phase + Mathf.PI * 0.5f, skew);
            float dir = mo.circleDir;
            float depth = mo.jitter * mo.amp;

            // Where she is in getting onto him (Mount.cs): how high her vagina is to be above his root, and how much of the
            // stroke there is. With him under her and his penis known she is placed by her vagina on it; with nobody there
            // she squats and moves as she used to.
            // (This was a static method once and went by "me" for the one instance: kept as a name for "this".)
            ShakeController me = this;
            float dt = Mathf.Clamp(Time.deltaTime, 0.004f, 0.05f);
            bool posing = me.m_ride && me.m_toggled && !me.m_switching;
            bool onHim = me.m_heroReady && me.m_penisValid && me.m_penisGoalValid;
            me.MountStep(onHim, posing, dt);
            float onW = Glide(w, G.RideOnHim, onHim ? 1f : 0f);
            if (onW > 0.98f)
            {
                // (The glide only ever comes close to its goal: close enough is there.)
                onW = 1f;
            }
            bool reverse = string.Equals(Plugin.RideFacing.Value, "Away", StringComparison.OrdinalIgnoreCase);
            string poseName = Plugin.RidePoseType.Value ?? "";
            if (!string.Equals(poseName, me.m_ridePoseName, StringComparison.OrdinalIgnoreCase))
            {
                if (me.m_ridePoseName.Length > 0)
                {
                    me.m_ridePoseChangedAt = Time.unscaledTime;
                    me.m_rideKneelFrom = w.gHas[G.RideKneel] ? w.gB[G.RideKneel] : 0f;
                }
                me.m_ridePoseName = poseName;
            }
            float poseT = Mathf.Clamp01((Time.unscaledTime - me.m_ridePoseChangedAt) / Mathf.Max(0.8f, Plugin.PoseRamp.Value));
            // Ease the load off her legs, let her hands find support, then bring the stroke back after the new stance.
            float poseStroke = poseT < 0.4f
                ? Mathf.Lerp(1f, 0.3f, Mathf.SmoothStep(0f, 1f, poseT / 0.4f))
                : Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 1f, (poseT - 0.4f) / 0.6f));
            // (On the downstroke in Reverse Cowgirl his palms cup under her bottom and rise into it a little, as if supporting her.
            // The hip stroke itself is unchanged.)
            me.m_holdSupport = reverse && me.m_mount == 3 && me.m_in && me.m_toggled
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-cB))
                : 0f;

            // The stroke: her hips rise from where she sits and come back down, one whole smooth wave. On him it is no
            // longer than there is room for on his penis (she does not come off his tip), it is there only once he is in
            // her, and what slides her hips across the floor is kept small: she is on a shaft.
            float climax = me.ClimaxNow();
            me.WombDiveStep(dt);
            float strokeW = shakeW * Mathf.Lerp(1f, me.m_rideW, onW) * (1f - climax) * (1f - 0.55f * me.m_holdW) * poseStroke;
            // (Longer or shorter where she leads and the tempo alone does not bring her to her rhythm: Lead.cs.)
            float riseSet = Plugin.RideBounce.Value * me.m_leadStroke * depth * (mo.bounce + 0.5f * mo.circle);
            float riseTop = Mathf.Lerp(riseSet, Mathf.Min(riseSet, Mathf.Max(0f, me.m_sTop - me.m_sSeat)), onW);
            // (For her rhythm, Lead.cs: how long a stroke is set to be, how much room there is for one on him, and how
            // long they are now.)
            me.m_strokeSet = Plugin.RideBounce.Value * mo.amp;
            me.m_strokeRoom = Mathf.Max(0f, me.m_sTop - me.m_sSeat);
            me.m_strokeNow = riseTop * strokeW;
            float shortRoom = me.m_mount == 3 && me.m_in
                ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(me.m_strokeRoom / 0.025f)) : 0f;
            Vector3 slide =
                fwd * ((s * Plugin.RideGrind.Value * mo.sway + s * dir * Plugin.CircleRadius.Value * mo.circle) * mo.amp)
                + right * (c * Plugin.CircleRadius.Value * mo.circle * mo.amp)
                + right * (mo.weight * 0.012f);
            slide = Vector3.Lerp(slide, Vector3.ClampMagnitude(slide * 0.5f, 0.025f), onW);
            // When depth or the learned tip margin leaves little vertical travel, she rolls her pelvis within the
            // line of his shaft instead of accelerating an invisible up/down stroke.
            slide += fwd * (sB * 0.004f * depth * mo.bounce * shortRoom * onW);
            Vector3 stroke = (Vector3.up * (0.5f * (1f + sB) * riseTop) + slide) * strokeW;

            // The lean, and the rock of the pelvis with every stroke, which travels up the spine a moment later.
            // Where her hands are: on her own thighs, under her breasts, or on his chest (then she leans over him as far as
            // it takes to reach it; with her back to him his chest is behind her, and her hands stay on her thighs). Each
            // place has a weight that glides, so changing it moves her arms there.
            // Her contact follows the pose and her pleasure (Scenes.cs AutoHands).
            bool herBack = string.Equals(Plugin.RideFacing.Value, "Away", StringComparison.OrdinalIgnoreCase);
            string handsOn = me.AutoHands(herBack);
            if (poseT < 0.45f)
            {
                handsOn = "Thighs";
            }
            else if (me.m_holdW > 0.5f && !herBack
                && !string.Equals(poseName, "Over him", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(poseName, "Leaning back", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(poseName, "Squatting", StringComparison.OrdinalIgnoreCase))
            {
                // In forward-facing poses she can rest on his forearms while he holds her. Leaning back
                // and squatting keep their own stable contacts instead of twisting both wrists toward him.
                handsOn = "HisArms";
            }
            bool chestThere = !herBack && me.m_heroReady && me.m_heroChest != null;
            // (She bends over him only once she rides him, or with nobody under her: she gets onto him sitting up, her
            // hole in line with him. Until then her hands, if they are to be behind his shoulders, are on him.)
            bool riding = !onHim || me.m_mount >= 3;
            // (On his shoulders: "Floor" is what that setting was called when her hands were on the floor by his head.)
            bool floorSet = string.Equals(handsOn, "Shoulders", StringComparison.OrdinalIgnoreCase) || string.Equals(handsOn, "Floor", StringComparison.OrdinalIgnoreCase);
            bool floorNow = floorSet && riding;
            float onChest = Glide(w, G.RideHandsChest, chestThere && (string.Equals(handsOn, "HisChest", StringComparison.OrdinalIgnoreCase) || (floorSet && !floorNow)) ? 1f : 0f);
            float onUnder = Glide(w, G.RideHandsUnder, string.Equals(handsOn, "UnderBreasts", StringComparison.OrdinalIgnoreCase) ? 1f : 0f);
            // (Or on the floor either side of his head, as she lies over him; with no head of his to go by, on his chest.)
            bool floorThere = !herBack && me.m_heroReady && me.m_heroHead != null;
            float onFloor = Glide(w, G.RideHandsFloor, floorThere && floorNow ? 1f : 0f);
            // Over him keeps both palms planted beside his torso at every pace. Changing contact on a
            // speed threshold made them sweep sideways while her knees stayed in place.
            float onSide = Glide(w, G.RideHandsSide,
                floorThere && riding && string.Equals(handsOn, "HisSides", StringComparison.OrdinalIgnoreCase) ? 1f : 0f);
            onChest *= 1f - onSide;
            onUnder *= 1f - onSide;
            onFloor *= 1f - onSide;
            // (Leaning back, her hands behind her on his thighs, whose upper sides she reaches back to.)
            bool backThere = !herBack && me.m_heroReady && me.m_heroThighL != null && me.m_heroThighR != null && me.m_heroKneeL != null && me.m_heroKneeR != null;
            float onBack = Glide(w, G.RideHandsBack, backThere && string.Equals(handsOn, "Behind", StringComparison.OrdinalIgnoreCase) && riding ? 1f : 0f);
            onBack *= 1f - onSide;
            float onHips = Glide(w, G.RideHandsHips, string.Equals(handsOn, "OwnHips", StringComparison.OrdinalIgnoreCase) ? 1f : 0f);
            onHips *= 1f - onSide;
            onChest *= 1f - onBack;
            onUnder *= 1f - onBack;
            onFloor *= 1f - onBack;
            // (Held by him: on his forearms, Scenes.cs HisForearmSpot.)
            bool armsThere = !herBack && me.m_holdW > 0.5f && me.HisForearmsThere();
            float onArms = Glide(w, G.RideHandsArms, armsThere && string.Equals(handsOn, "HisArms", StringComparison.OrdinalIgnoreCase) ? 1f : 0f);
            onArms *= 1f - onSide;
            onChest *= 1f - onArms;
            onUnder *= 1f - onArms;
            onFloor *= 1f - onArms;
            onBack *= 1f - onArms;
            onHips *= 1f - onArms;
            float onThigh = Mathf.Clamp01(1f - onChest - onUnder - onFloor - onSide - onBack - onArms - onHips);
            Vector3 sideFrom = me.m_vulvaHomeValid ? new Vector3(me.m_vulvaHome.x, pelvisBase.y, me.m_vulvaHome.z) : pelvisBase + me.m_lastHipOffset;
            Vector3[] chestSpot = s_rideChestSpot;
            chestSpot[0] = chestSpot[1] = Vector3.zero;
            float chestBend = 0f;
            float leanSet = Plugin.RideLean.Value;
            bool overHim = string.Equals(poseName, "Over him", StringComparison.OrdinalIgnoreCase);
            bool chestBracePose = overHim
                || string.Equals(poseName, "Astride", StringComparison.OrdinalIgnoreCase)
                || string.Equals(poseName, "Kneeling", StringComparison.OrdinalIgnoreCase);
            float chestBendLimit = overHim ? 54f : chestBracePose ? 42f : 40f;
            if (onChest > 0.001f && me.m_heroChest != null)
            {
                // Search separately for each palm. A shorter arm can settle lower on his torso without dragging the
                // other one off its contact or making both wrists reach for the same unreachable point.
                Vector3 pelvisThen = pelvisBase + me.m_lastHipOffset;
                Vector3 home = me.m_vulvaHomeValid ? new Vector3(me.m_vulvaHome.x, pelvisThen.y, me.m_vulvaHome.z) : pelvisThen;
                float far = Mathf.Clamp(Vector3.Dot(me.m_heroChest.position - home, fwd), 0.2f, 0.6f);
                Vector3 hFwd = Vector3.ProjectOnPlane(me.m_heroFwd, Vector3.up);
                Vector3 handSide = right;
                if (hFwd.sqrMagnitude > 0.5f && Vector3.Dot(hFwd.normalized, fwd) > 0.7f)
                {
                    hFwd.Normalize();
                    Vector3 hSide = Vector3.Cross(Vector3.up, hFwd).normalized;
                    handSide = Vector3.Dot(hSide, right) >= 0f ? hSide : -hSide;
                }
                else
                {
                    hFwd = fwd;
                }
                float bestLeanTogether = 0f;
                SpineFrame spine = new SpineFrame(w);
                for (int i = 0; i < 2; i++)
                {
                    Limb arm = w.arms[i];
                    if (arm == null)
                    {
                        me.m_chestOk[i] = 0f;
                        continue;
                    }
                    Vector3 shoulder = arm.upper.t.position;
                    float armLen = (arm.fore.t.position - shoulder).magnitude + (arm.end.t.position - arm.fore.t.position).magnitude;
                    Vector3 side = (i == 0 ? -handSide : handSide) * 0.14f;
                    float bestFar = 0.12f, bestLean = 0f;
                    bool found = false;
                    for (float f = far; f >= 0.12f && !found; f -= 0.025f)
                    {
                        Vector3 candidate = home + hFwd * f + side;
                        float surface = me.HeroTopAt(candidate);
                        candidate.y = (float.IsNegativeInfinity(surface) ? me.m_penisGoalBase.y + 0.02f : surface) + 0.025f;
                        for (float a = 0f; a <= chestBendLimit; a += 2f)
                        {
                            if ((me.SpineAt(spine, right, leanSet, a, 0f, me.m_holeTilt, shoulder) - candidate).sqrMagnitude <= armLen * armLen * 0.90f)
                            {
                                bestFar = f;
                                bestLean = a;
                                found = true;
                                break;
                            }
                        }
                    }
                    // Draw in quickly if reach is lost, move up slowly once she has a reliable place to bear weight.
                    me.m_chestFar[i] = Mathf.Lerp(me.m_chestFar[i], bestFar, 1f - Mathf.Exp(-dt / (bestFar < me.m_chestFar[i] ? 0.25f : 1.2f)));
                    me.m_chestOk[i] = Mathf.MoveTowards(me.m_chestOk[i], found ? 1f : 0f, dt / (found ? 0.65f : 0.25f));
                    if (found)
                    {
                        bestLeanTogether = Mathf.Max(bestLeanTogether, bestLean * me.m_chestOk[i]);
                    }
                    Vector3 spot = home + hFwd * me.m_chestFar[i] + side;
                    float top = me.HeroTopAt(spot);
                    spot.y = (float.IsNegativeInfinity(top) ? me.m_penisGoalBase.y + 0.02f : top) + 0.025f;
                    chestSpot[i] = spot;
                    if (Time.unscaledTime >= me.m_chestLogAt)
                    {
                        Plugin.ModLog.LogInfo($"Ride: her {(i == 0 ? "left" : "right")} palm: {(found ? "reaches" : "cannot reach")} {bestFar * 100f:F0} cm along him (chest {far * 100f:F0} cm); arm {armLen * 100f:F0} cm, added bend {bestLean:F0} deg.");
                    }
                }
                if (Time.unscaledTime >= me.m_chestLogAt)
                {
                    me.m_chestLogAt = Time.unscaledTime + 20f;
                }
                me.m_chestLean = Mathf.Lerp(me.m_chestLean, bestLeanTogether, 1f - Mathf.Exp(-dt / 0.6f));
                chestBend = Mathf.Min(me.m_chestLean, chestBendLimit) * onChest;
            }
            float lean = (Glide(w, G.RideLean, leanSet) + mo.leanExtra * 0.5f) * poseW;
            float pitch = Plugin.RideTilt.Value * depth * (cB * mo.bounce + s * mo.sway + s * dir * mo.circle) * strokeW;
            pitch += 3f * depth * mo.bounce * shortRoom * cB * strokeW;
            float roll = Plugin.RollDegrees.Value * c * mo.circle * strokeW + mo.weight * 1.6f * strokeW;
            float wave = Plugin.RideTilt.Value * depth * mo.bounce * strokeW;
            float waistPitch = -wave * 0.3f * Skew(phase + Mathf.PI * 0.5f - 0.75f, skew);
            float sp1Pitch = -wave * 0.12f * Skew(phase + Mathf.PI * 0.5f - 1.5f, skew);
            Ease(w, ref stroke, ref pitch, ref roll, ref lean);
            float climaxArch = 0f;
            if (climax > 0.001f)
            {
                // (After the smoothing, which would take her contractions out again. Climax.cs.)
                me.ClimaxPose(poseW, fwd, ref stroke, ref pitch, ref roll, ref lean, out climaxArch);
            }

            // It is her hips that do the work. As they rise her body hinges forward over them and her lower back arches
            // (her pelvis tips forward, her spine curves back by as much), so her bottom goes up and back while her
            // shoulders and head rise far less than her hips do; coming down it all unwinds.
            float work = Mathf.Clamp(Plugin.RideHipsWork.Value, 0f, 1.5f);
            float risen = Mathf.Max(0f, stroke.y);
            float arch = 90f * work * risen + climaxArch;
            lean += 70f * work * risen * poseW;

            // (And her pelvis alone is tipped by what brings her hole in line with him; her back takes that up.)
            float holeTilt = me.HoleTilt(right, lean * 0.45f + arch, dt) * poseW;
            // (Her back hangs from her hips beside her pelvis in this rig, not from her pelvis: tipping her pelvis does
            // not carry it, and there is nothing for it to take up. Where it does hang from her pelvis, it takes it up.)
            float tiltBack = w.sWaist.t.IsChildOf(w.sPelvis.t) ? holeTilt : 0f;
            // Over him, her hands on the floor behind his shoulders: bent forward by what it takes to reach it.
            float overBend = onFloor > 0.001f ? me.OverBend(w, right, lean, arch, holeTilt, dt) * onFloor * poseW : 0f;
            // (Or with her hands on him: by what it takes to reach where they lie.)
            overBend = Mathf.Max(overBend, chestBend * poseW);
            if (onSide > 0.001f)
            {
                Vector3 leftSide = me.SideSpot(sideFrom, fwd, -right);
                Vector3 rightSide = me.SideSpot(sideFrom, fwd, right);
                overBend = Mathf.Max(overBend,
                    me.BendToReachSides(w, right, lean, arch, holeTilt, dt, leftSide, rightSide) * onSide * poseW);
            }
            // (Her pelvis tips forward with it, at her hip joints; where her back does not hang from her pelvis it is
            // turned by as much itself, to stay in one line with it.)
            // (Less bent by what the head and chest of her were found to go into his body, HeadClearance: first what bends her
            // over him, and if that is not enough, her lean.)
            float cutOver = Mathf.Min(overBend, me.m_bendCut);
            overBend -= cutOver;
            float cutLean = (me.m_bendCut - cutOver) * 0.7f;
            if (cutLean > 0.01f)
            {
                lean = Mathf.Max(Mathf.Min(lean, 0f), lean - cutLean);
            }
            float waistOver = overBend * (w.sWaist.t.IsChildOf(w.sPelvis.t) ? 0.45f : 0.8f);
            w.sPelvis.t.rotation = Quaternion.AngleAxis(roll, fwd) * Quaternion.AngleAxis(lean * 0.45f + pitch + arch + holeTilt + overBend * 0.35f, right) * w.sPelvis.t.rotation;
            w.sWaist.t.rotation = Quaternion.AngleAxis(lean * 0.25f + waistPitch - arch * 0.55f - tiltBack * 0.6f + waistOver, right) * w.sWaist.t.rotation;
            // (Her breath lifts her chest: her upper back is turned back a little with every breath in. Body.cs.)
            float breath = me.Breath() * poseW;
            w.sSp1.t.rotation = Quaternion.AngleAxis(lean * 0.18f + sp1Pitch - arch * 0.3f - tiltBack * 0.4f + overBend * 0.15f - breath * 0.4f, right) * w.sSp1.t.rotation;
            w.sSp2.t.rotation = Quaternion.AngleAxis(lean * 0.12f - arch * 0.15f + overBend * 0.05f - breath * 0.6f, right) * w.sSp2.t.rotation;
            w.sPelvis.Commit();
            w.sWaist.Commit();
            w.sSp1.Commit();
            w.sSp2.Commit();

            // Her vagina now that her pelvis is turned, her hips still where the game has them: her hips are carried by
            // what it takes to bring it from here to where it has to be.
            Vector3 v0 = VulvaOf(w);
            // A soft body gives as it lands: at the very bottom she sinks a little further, by as much as the setting allows.
            float low = 0.5f * (1f - sB);
            float give = Mathf.Clamp01(Plugin.RideSpring.Value) * 0.02f * strokeW * low * low * low;

            Vector3 offset = Vector3.zero;
            if (onW < 0.999f)
            {
                // Nobody under her (or his penis was not found): a squat of the depth that is set, kept out of his body if
                // he is there, with the stroke on top of it.
                float squatDepth = Glide(w, G.RideSquat, Mathf.Min(Plugin.RideSquat.Value, 0.6f)) * poseW;
                Vector3 seat = new Vector3(stroke.x, -squatDepth, stroke.z) + pin;
                float lift = me.HeroPushFor(w, seat, right, fwd);
                offset = Vector3.down * squatDepth + stroke + pin + Vector3.up * (lift - give);
            }
            if (onW > 0.001f)
            {
                // On him. Across the floor her vagina stays where it is on average (her feet are planted about that place
                // and he is laid under it), moved by the little it takes to be on the line of his penis exactly; and its
                // height is the height of his root plus how far up him she is.
                Vector3 homeNow = new Vector3(v0.x + pin.x, 0f, v0.z + pin.z);
                if (!me.m_vulvaHomeValid)
                {
                    me.m_vulvaHome = homeNow;
                    me.m_vulvaHomeValid = true;
                }
                else
                {
                    // Slowly: the rock of her pelvis with every stroke carries her vagina to and fro under her, and what is
                    // followed here is where it is on average, not that.
                    me.m_vulvaHome = Vector3.Lerp(me.m_vulvaHome, homeNow, 1f - Mathf.Exp(-dt / 2.5f));
                }
                Vector3 line = me.MountLine(me.m_sBase + stroke.y);
                Vector3 corrGoal = Vector3.ClampMagnitude(new Vector3(line.x - me.m_vulvaHome.x, 0f, line.z - me.m_vulvaHome.z), 0.1f);
                me.m_mountCorr = Vector3.Lerp(me.m_mountCorr, corrGoal, 1f - Mathf.Exp(-dt / 0.12f));
                float y = me.m_penisGoalBase.y + me.m_sBase + stroke.y - give - me.m_wombDive;
                if (me.m_mount >= 2 && me.PenisNow(out Vector3 rootNow, out Vector3 tipNow))
                {
                    // When the player pushes his hips up under her she is lifted by them: she sits on him, not in him.
                    y = Mathf.Max(y, rootNow.y + 0.008f);
                }
                // (With the little circle she feels for the way in with, while she takes him in: Mount.cs.)
                Vector3 target = new Vector3(me.m_vulvaHome.x + me.m_mountCorr.x + stroke.x + me.m_searchOff.x, y, me.m_vulvaHome.z + me.m_mountCorr.z + stroke.z + me.m_searchOff.z);
                offset = Vector3.Lerp(offset, (target - v0) * poseW, onW);
            }
            w.sHip.Offset(offset);
            me.m_lastHipOffset = offset;
            // (Where her hips are put, kept: once the game's inverse kinematics have run, later in this frame, they are put
            // back there, as far as she is posed on him. PoseFix.cs.)
            // (Her pelvis bone: rigid, and the one her vagina hangs from.)
            me.m_hipPosed = w.sPelvis.t.position;
            me.m_hipPosedFrame = Time.frameCount;
            me.m_posedW = poseW * onW;
            me.m_herJoint[0] = w.legs[0].upper.t.position;
            me.m_herJoint[1] = w.legs[1].upper.t.position;
            me.m_herJointValid = true;
            // (Now that her hips are where they are put: her head, chest and shoulders against his body. It was asked
            // before her hips were moved, with her still standing half a metre above him, and so never found anything.)
            me.HeadClearance(w, dt);

            // How deep he is in her, for the wet look: only while he is in her, by how far down him her vagina is.
            // (Where it is put is where it will be drawn: she is put back there after the game's inverse kinematics.)
            Vector3 vulva = VulvaOf(w);
            float inHer;
            if (onHim)
            {
                float his = Mathf.Max(0.05f, me.m_penisLenFree);
                inHer = me.m_in ? Mathf.Clamp01(Mathf.Max(me.DepthIn(vulva), 1f - (vulva.y - me.m_penisGoalBase.y) / his)) : 0f;
                Vector3 lineNow = me.MountLine(vulva.y - me.m_penisGoalBase.y);
                me.m_alignErr = new Vector2(vulva.x - lineNow.x, vulva.z - lineNow.z).magnitude;
            }
            else
            {
                inHer = Mathf.Max(0f, me.DepthIn(vulva));
                float gap = me.HeroGapUnder(w, Vector3.zero) + Plugin.RideSink.Value;
                if (!float.IsPositiveInfinity(gap))
                {
                    inHer = Mathf.Max(inHer, Mathf.Clamp01(1f - gap / Mathf.Max(0.05f, me.m_penisLength)));
                }
            }
            me.m_rideIn = inHer * poseW;
            me.m_rideInFrame = Time.frameCount;
            me.m_vulvaLast = vulva;
            me.m_vulvaLastValid = true;

            // Her feet stay on the floor, planted either side of him a little ahead of her hips, close to his body, with
            // the toes turned out; her knees go up and forward over them as she sits down. They do not move with the
            // stroke: it is her hips that move between them. (They are laid out from where she stands on average and the
            // way she faces on average, neither of which her own idle sway or the stroke shifts.)
            float legW = Mathf.Max(poseW, shakeW);
            float feet = Glide(w, G.RideStance, Mathf.Clamp(Plugin.RideFeet.Value, 0.27f, 0.6f));
            Vector3 ahead = me.m_rideFwd;
            Vector3 across = Vector3.Cross(Vector3.up, ahead);
            Vector3 between = new Vector3(pelvisBase.x + hold.x, 0f, pelvisBase.z + hold.z);
            // (On a place too narrow for a knee of hers between his body and its edge, she stands astride it instead, her feet on
            // the floor. Feet.cs KneelRoom.)
            bool kneelSet = string.Equals(Plugin.RideLegs.Value, "Knees", StringComparison.OrdinalIgnoreCase) && me.KneelRoom(between, across);
            float kneel = Glide(w, G.RideKneel, poseT < 0.25f ? me.m_rideKneelFrom : kneelSet ? 1f : 0f);
            me.m_kneelPosed = kneel * poseW;
            for (int i = 0; i < 2; i++)
            {
                Limb leg = w.legs[i];
                Vector3 outward = i == 0 ? -across : across;
                Vector3 plant = new Vector3(between.x, me.m_rideGroundY, between.z) + outward * feet + ahead * 0.08f;
                Vector3 pole = ahead * 0.75f + outward * 0.65f + Vector3.up * 0.2f;
                Quaternion footTurn = Quaternion.AngleAxis((i == 1 ? 1f : -1f) * 26f, Vector3.up) * footRot[i];
                if (kneel > 0.001f)
                {
                    // Astride him on her knees: her knee on the place beside him, as far ahead of her hip as her thigh reaches
                    // down; her shin back along the place from it, round his leg, or over the edge with her foot hanging down;
                    // her foot in the line of her shin, its top down, its heel turned out. (Feet.cs KneelLeg.)
                    bool turned = me.KneelLeg(leg, between, ahead, outward, feet, Mathf.Max(0f, stroke.y), footRot[i], out Vector3 knee, out Vector3 foot, out Quaternion footLay);
                    Vector3 toKnee = knee - 0.5f * (leg.upper.t.position + foot);
                    plant = Vector3.Lerp(plant, foot, kneel);
                    pole = Vector3.Slerp(pole.normalized, toKnee.sqrMagnitude > 1e-6f ? toKnee.normalized : pole.normalized, kneel);
                    if (!turned)
                    {
                        // (Her toes not found: her foot turned straight back about the one axis across her, as it always was.)
                        footLay = Quaternion.AngleAxis(158f, across) * footRot[i];
                    }
                    footTurn = Quaternion.Slerp(footTurn, footLay, kneel);
                }
                // (No longer pushed out by where his body is this frame: turning him round under her for the reverse swept
                // it through her feet and threw her legs about. They are never set closer than outside his body.)
                // On a place that is not wide enough for her foot to be on it there (a bench): her foot goes down beside it
                // onto the floor, if her leg reaches that far; if not (a table), it is brought in onto the place.
                // Her foot finds its own ground: a ray down at the spot it is put at says how high the floor is there and how
                // it slopes (the floor, a rug, the top of a bench, a step), and her ankle is put as high above it as the game's
                // pose has it above the floor (the heel in it), the sole laid along the slope. Where nothing is found the
                // place's height is the guess it was.
                bool probed = false;
                if (Plugin.RideFeetAuto.Value && kneel < 0.5f && me.GroundAt(plant, plant.y, out float groundHere, out Vector3 groundNormal))
                {
                    float wantY = groundHere + Mathf.Max(0f, me.m_rideGroundY - me.m_rideFloorY);
                    me.m_footGy[i] = float.IsNaN(me.m_footGy[i]) ? wantY : Mathf.Lerp(me.m_footGy[i], wantY, 1f - Mathf.Exp(-dt / 0.1f));
                    plant.y = me.m_footGy[i];
                    Quaternion slope = Quaternion.FromToRotation(Vector3.up, groundNormal);
                    footTurn = Quaternion.Slerp(Quaternion.identity, slope, Mathf.Clamp01(20f / Mathf.Max(1f, Quaternion.Angle(Quaternion.identity, slope)))) * footTurn;
                    probed = true;
                    float legReach = (leg.fore.t.position - leg.upper.t.position).magnitude + (leg.end.t.position - leg.fore.t.position).magnitude;
                    if (me.m_place != null && (plant - leg.upper.t.position).magnitude > legReach * 0.985f)
                    {
                        // Too far down for her leg (beside a table): brought in onto the place.
                        plant = me.PlaceOnto(plant);
                        plant.y = me.m_carryTop + Mathf.Max(0f, me.m_rideGroundY - me.m_rideFloorY);
                    }
                }
                else
                {
                    me.m_footGy[i] = float.NaN;
                }
                // (On her knees her foot is where KneelLeg put it: on the place, or hanging over its edge on purpose.)
                float drop = probed || kneel > 0.5f ? 0f : me.PlaceDrop(plant);
                if (drop > 0.02f)
                {
                    float legLen = (leg.fore.t.position - leg.upper.t.position).magnitude + (leg.end.t.position - leg.fore.t.position).magnitude;
                    Vector3 beside = plant - Vector3.up * drop;
                    plant = (beside - leg.upper.t.position).magnitude <= legLen * 0.985f ? beside : me.PlaceOnto(plant);
                }
                Vector3 target = Vector3.Lerp(footPos[i], plant, poseW);
                Ik(leg, target, pole, legW);
                leg.end.t.rotation = Quaternion.Slerp(footRot[i], footTurn, poseW);
                leg.end.Commit();
                // (Kept: should the game's inverse kinematics drag her foot away from here, it is put back. PoseFix.cs.)
                me.m_footPosed[i] = leg.end.t.position;
                me.m_footPosedRot[i] = leg.end.t.rotation;
                me.m_footPole[i] = pole;
                me.m_footPosedFrame = Time.frameCount;
            }

            // Her hands, at the blend of the places. On her thighs they lie a little above the knees, on the upper side, as
            // far down the thigh as her arm reaches; how far down is eased, so that her hands rest there and go with her
            // thighs instead of jumping from place to place along them with every stroke (that was the waving).
            // Every place says where her hand goes, which way the surface there faces (the back of her hand is to face that
            // way) and which way her fingers would rather point there; her arm then does what an arm does to lay it so
            // (Wrist.cs). The three are blended as the places are.
            float[] handOff = s_rideHandOff;
            handOff[0] = handOff[1] = -1f;
            Quaternion bend = Quaternion.AngleAxis(lean, right);
            Vector3 chestUp = bend * Vector3.up;
            Vector3 front = bend * fwd;
            // In an orgasm she takes hold: her fingers close on what they lie on and her hands press on it, with a tremor of
            // their own. (They no longer slide toward her: a hand that holds on stays where it holds.)
            float grip = Mathf.Clamp01(me.m_tenseNow + 0.6f * me.m_squeezeNow) * poseW;
            for (int i = 0; i < 2; i++)
            {
                Limb arm = w.arms[i];
                if (arm == null)
                {
                    continue;
                }
                Vector3 outward = i == 0 ? -right : right;
                Vector3 hipJoint = w.legs[i].upper.t.position;
                Vector3 knee = w.legs[i].fore.t.position;
                Vector3 thigh = knee - hipJoint;
                thigh = thigh.sqrMagnitude > 1e-6f ? thigh.normalized : Vector3.down;
                // The upper side of the thigh: straight up from it, as far as that is across it.
                Vector3 top = Vector3.up - thigh * Vector3.Dot(Vector3.up, thigh);
                top = top.sqrMagnitude > 1e-4f ? top.normalized : fwd;
                Vector3 shoulder = arm.upper.t.position;
                float armLen = (arm.fore.t.position - shoulder).magnitude + (arm.end.t.position - arm.fore.t.position).magnitude;
                float reach = armLen * 0.9f;
                float part = ThighReachPart(hipJoint, knee, top, shoulder, reach);
                // (Never further down than her arm reached at the top of the last strokes: it settles there and stays.)
                me.m_handPart[i] = part < me.m_handPart[i] ? Mathf.Lerp(me.m_handPart[i], part, 1f - Mathf.Exp(-dt / 0.15f)) : Mathf.Lerp(me.m_handPart[i], part, 1f - Mathf.Exp(-dt / 4f));
                // The wrist must clear the thigh's skin and the thickness of her palm. The old 8.5 cm
                // offset placed the wrist inside the upper thigh when her knee rose; curling the fingers
                // there made them emerge through its surface. Keep the palm on its outer upper side.
                float thighSurface = Mathf.Lerp(0.14f, 0.12f, me.m_handPart[i]);
                Vector3 thighSpot = Vector3.Lerp(hipJoint, knee, me.m_handPart[i]) + top * thighSurface + outward * 0.055f;
                Vector3 thighPole = outward * 0.9f - fwd * 0.5f - Vector3.up * 0.2f;
                Vector3 thighWish = (thigh - outward * 0.22f).normalized;
                float chestHere = onChest * me.m_chestOk[i];
                float thighHere = onThigh + onChest - chestHere;
                Vector3 target = thighHere * thighSpot;
                Vector3 pole = thighHere * thighPole;
                Vector3 layBack = thighHere * top;
                Vector3 layWish = thighHere * thighWish;
                if (onHips > 0.001f)
                {
                    // Her own iliac crest follows the pelvis, so this resting place stays under her palms in Reverse.
                    target += onHips * (hipJoint + outward * 0.09f + front * 0.04f + Vector3.up * 0.08f);
                    pole += onHips * (outward * 0.9f - Vector3.up * 0.2f);
                    layBack += onHips * (outward * 0.8f + Vector3.up * 0.2f).normalized;
                    layWish += onHips * (Vector3.down * 0.8f + front * 0.2f).normalized;
                }
                if (onUnder > 0.001f)
                {
                    // Under her breast of that side, the palm against her ribs, fingers inward and up, the elbow down and out.
                    PosSlot breast = i == 0 ? w.peL : w.peR;
                    Vector3 under = breast != null
                        ? breast.t.position + outward * 0.06f + front * 0.08f - chestUp * 0.09f
                        : w.sSp2.t.position + chestUp * 0.02f + front * 0.14f + outward * 0.09f;
                    target += onUnder * under;
                    pole += onUnder * (outward * 0.8f - Vector3.up * 0.7f - fwd * 0.2f);
                    layBack += onUnder * (front * 0.85f - chestUp * 0.4f + outward * 0.2f).normalized;
                    layWish += onUnder * (-outward * 0.8f + chestUp * 0.35f + front * 0.15f).normalized;
                }
                if (chestHere > 0.001f)
                {
                    // On him: palm down, fingers toward his head and a little inward.
                    target += chestHere * chestSpot[i];
                    pole += chestHere * (outward + Vector3.up * 0.4f);
                    layBack += chestHere * Vector3.up;
                    layWish += chestHere * (fwd * 0.9f - outward * 0.42f).normalized;
                }
                if (onFloor > 0.001f)
                {
                    // On his shoulder of that side: her palm on top of it, her elbow out and up. It is a place on him, so
                    // her hand stays there whatever her hips do. (Should his shoulders not be found: the floor by his head.)
                    if (!me.HeroShoulderSpot(outward, out Vector3 beside))
                    {
                        beside = me.m_heroHead.position + outward * 0.25f - fwd * 0.1f;
                        beside.y = me.m_rideFloorY + 0.02f;
                    }
                    target += onFloor * beside;
                    pole += onFloor * (outward + Vector3.up * 0.6f - fwd * 0.2f);
                    layBack += onFloor * Vector3.up;
                    layWish += onFloor * (fwd * 0.9f - outward * 0.42f).normalized;
                }
                if (onSide > 0.001f)
                {
                    // Beside him: fingers ahead and a little outward.
                    target += onSide * me.SideSpot(sideFrom, fwd, outward);
                    pole += onSide * (outward * 0.7f - fwd * 0.6f + Vector3.up * 0.4f);
                    layBack += onSide * Vector3.up;
                    layWish += onSide * (fwd * 0.95f + outward * 0.3f).normalized;
                }
                if (onBack > 0.001f)
                {
                    // His thigh of that side (the one that lies on her side of him), a little below its top, her elbow back and
                    // out, her palm on its upper side and her fingers back along it, toward his feet.
                    bool leftHis = Vector3.Dot(me.m_heroThighL.position - me.m_heroThighR.position, outward) > 0f;
                    Transform hHip = leftHis ? me.m_heroThighL : me.m_heroThighR;
                    Transform hKnee = leftHis ? me.m_heroKneeL : me.m_heroKneeR;
                    Vector3 hisLeg = hKnee.position - hHip.position;
                    hisLeg = hisLeg.sqrMagnitude > 1e-6f ? hisLeg.normalized : -fwd;
                    Vector3 hisTop = Vector3.ProjectOnPlane(Vector3.up, hisLeg);
                    hisTop = hisTop.sqrMagnitude > 1e-4f ? hisTop.normalized : Vector3.up;
                    target += onBack * (Vector3.Lerp(hHip.position, hKnee.position, 0.25f) + hisTop * 0.09f + outward * 0.06f);
                    pole += onBack * (outward * 0.7f - fwd * 0.7f - Vector3.up * 0.1f);
                    layBack += onBack * hisTop;
                    layWish += onBack * (-fwd * 0.9f + outward * 0.2f).normalized;
                }
                if (onArms > 0.001f)
                {
                    // Held by him: on his forearm of her side, from above and outside, her fingers round it.
                    if (me.HisForearmSpot(i, outward, out Vector3 armSpot, out Vector3 armBack, out Vector3 armWish))
                    {
                        target += onArms * armSpot;
                        pole += onArms * (outward * 0.8f - fwd * 0.3f - Vector3.up * 0.3f);
                        layBack += onArms * armBack;
                        layWish += onArms * armWish;
                    }
                    else
                    {
                        target += onArms * thighSpot;
                        pole += onArms * thighPole;
                        layBack += onArms * top;
                        layWish += onArms * thighWish;
                    }
                }
                // (Between behind her and before her, her hand goes round by her side, not through her body.)
                float round = Mathf.Clamp01(4f * onBack * (chestHere + onFloor + onSide + onUnder + onArms + 0.5f * thighHere + 0.5f * onHips));
                target += round * (outward * 0.12f + Vector3.up * 0.05f);
                // Out of her reach (his chest when she cannot bend so far, his thighs behind her when she sits up): her hand
                // goes to her own thigh rather than her arm being stretched straight at the air over him. Only for what is
                // well out of reach, and eased, so that it does not come and go with every stroke.
                float beyond = (target - shoulder).magnitude - armLen;
                float fallWant = thighHere > 0.98f ? 0f : Mathf.Clamp01((beyond - 0.04f) / 0.08f);
                arm.reachFall = Mathf.Lerp(arm.reachFall, fallWant, 1f - Mathf.Exp(-dt / (fallWant > arm.reachFall ? 0.5f : 1.2f)));
                if (fallWant > 0.5f && Time.unscaledTime >= me.m_reachLogAt)
                {
                    me.m_reachLogAt = Time.unscaledTime + 20f;
                    Plugin.ModLog.LogInfo($"Ride: her {(i == 0 ? "left" : "right")} hand cannot reach where it is to go ({beyond * 100f:F0} cm past her arm of {armLen * 100f:F0} cm): it rests on her thigh instead.");
                }
                if (arm.reachFall > 0.001f)
                {
                    target = Vector3.Lerp(target, thighSpot, arm.reachFall);
                    pole = Vector3.Lerp(pole, thighPole, arm.reachFall);
                    layBack = Vector3.Lerp(layBack, top, arm.reachFall);
                    layWish = Vector3.Lerp(layWish, thighWish, arm.reachFall);
                }
                if (grip > 0.01f)
                {
                    // (A tremor of a few millimetres, and her hand pressed onto what it lies on.)
                    float tg = Time.unscaledTime;
                    float g1 = Mathf.PerlinNoise(tg * 6.1f + i * 3.7f, me.m_seed + 31f) * 2f - 1f;
                    float g2 = Mathf.PerlinNoise(tg * 4.7f + i * 5.3f, me.m_seed + 47f) * 2f - 1f;
                    Vector3 press = layBack.sqrMagnitude > 1e-6f ? layBack.normalized : Vector3.up;
                    target += (outward * (g1 * 0.004f) + fwd * (g2 * 0.004f)
                        - press * (0.008f - 0.006f * Mathf.Clamp01(thighHere + arm.reachFall))) * grip;
                }
                // (Between places behind her and before her the ways her fingers would rather point cancel out: then they go on
                // the way her arm reaches. It is only a wish: the way her forearm runs decides the most of it.)
                if (layWish.sqrMagnitude < 0.04f)
                {
                    layWish += (target - shoulder).normalized * 0.3f;
                }
                // (Her hand lies on its palm, whose middle is a few centimetres past her wrist the way her fingers point.)
                Vector3 layAlong = Vector3.ProjectOnPlane(layWish, layBack.sqrMagnitude > 1e-6f ? layBack.normalized : Vector3.up);
                if (layAlong.sqrMagnitude > 1e-6f)
                {
                    target -= layAlong.normalized * 0.03f;
                }
                target = ClearThigh(target, hipJoint, knee, top, outward);
                // How her hand lies, should her arms be posed as CMA Shake posed them. On him or on the floor: flat, palm down,
                // fingers toward his head and a little inward. (Beside him her fingers point ahead and a little outward; on him,
                // a little inward; behind her on his thighs they point back along them, toward his feet.)
                float flatAll = chestHere + onFloor + onSide + onBack + onArms;
                Vector3 fingers = Vector3.Slerp((fwd * 0.9f - outward * 0.42f).normalized, (fwd * 0.95f + outward * 0.3f).normalized, onSide);
                fingers = Vector3.Slerp(fingers, (-fwd * 0.9f + outward * 0.2f).normalized, onBack / Mathf.Max(0.001f, flatAll));
                if (Plugin.RideNaturalArms.Value)
                {
                    // (How her hand lies on its bone is found here, once.)
                    HandFlat(arm, i, fingers, 0f);
                    // Her elbow goes where her wrist is spared, her hand lies as far as a wrist lets it, her fingers lie on
                    // what her hand lies on (round his forearm, they hold it). (Wrist.cs)
                    float straight = Mathf.Clamp01(1f - 0.5f * onUnder - 0.7f * onArms) * 0.85f;
                    // A palm resting on her thigh can tense, but closing the fingers around it buries
                    // the fingertips in the leg, especially when a chest reach falls back mid-stroke.
                    float holdOn = Mathf.Clamp01(grip * (1f - 0.85f * Mathf.Clamp01(thighHere + arm.reachFall)) + 0.45f * onArms);
                    ArmSolve(arm, i, target, pole, layWish, layBack, poseW, 1f, straight, holdOn);
                }
                else
                {
                    Ik(arm, target, pole, poseW, true);
                    float flat = flatAll * poseW;
                    if (flat > 0.001f)
                    {
                        HandFlat(arm, i, fingers, flat);
                    }
                }
                handOff[i] = (arm.end.t.position - target).magnitude;
                // (Kept: should the game move her hand after she is posed, it is brought back. PoseFix.cs.)
                me.m_handPosed[i] = arm.end.t.position;
                me.m_handPosedRot[i] = arm.end.t.rotation;
                me.m_handPosedFrame = Time.frameCount;
            }
            // Her head and eyes are turned later in the frame, once the game's own look-at has had its turn (HeadLate):
            // what is to be done then is kept, with how her back and neck stand now that she is posed.
            me.LookKeep(w, poseW);
            me.RideReport(w, vulva, inHer, lean, handOff, onHim);

            // Where he is laid: with his penis known, its root under where her vagina is on average; otherwise the middle
            // of his hip joints under the middle of hers, as before.
            Vector3 hipsMid = 0.5f * (w.legs[0].upper.t.position + w.legs[1].upper.t.position);
            if (onHim && me.m_vulvaHomeValid)
            {
                me.m_herCrotch = new Vector3(me.m_vulvaHome.x, hipsMid.y, me.m_vulvaHome.z);
                me.m_herCrotchValid = true;
                me.m_vulvaOff = Vector3.zero;
                me.m_vulvaOffValid = true;
            }
            else
            {
                me.m_herCrotch = hipsMid - new Vector3(stroke.x, 0f, stroke.z);
                me.m_herCrotchValid = true;
                Vector3 vOff = new Vector3(vulva.x - hipsMid.x, 0f, vulva.z - hipsMid.z);
                if (!me.m_vulvaOffValid)
                {
                    me.m_vulvaOff = vOff;
                    me.m_vulvaOffValid = true;
                }
                else
                {
                    me.m_vulvaOff = Vector3.Lerp(me.m_vulvaOff, vOff, 1f - Mathf.Exp(-dt / 1.5f));
                }
            }

            // The soft bones add their own bounce, shaken by how the hips really moved.
            // The ride's hip acceleration rises with the square of its tempo. Keep the glute bones' spring motion subtle
            // and bounded here, so faster strokes cannot pull the buttock shape inward; the twerk keeps its fuller wobble.
            Wobble(w, stroke, shakeW, 0.3f, 0.02f);
        }
    }
}
