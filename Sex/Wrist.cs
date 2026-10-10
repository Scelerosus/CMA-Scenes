using System;
using System.Collections.Generic;
using UnityEngine;

namespace CMAScenes
{
    // Her arms, posed by what an arm can do.
    //
    // What was wrong (the user's picture of 2026-10-09: her hand on her thigh, wrung round at the wrist). Her arm was
    // put with its hand at a place and its elbow toward a side that was fixed for that place; her hand was then either
    // laid flat by itself, whatever her forearm had come to (on him, on the floor), or not turned at all and left as
    // the game's animation had it on a forearm that now pointed somewhere else (on her thighs, under her breasts).
    // Nothing looked at what that asked of her wrist, and nothing told the bones along her forearm, which carry the
    // turn of her hand up her arm, that her hand had been turned: the skin of her wrist was wrung.
    //
    // 0.2.0 swung her elbow to spare her wrist, held the turn of her hand to 95 deg and its bend to 72, and gave the bones
    // along her forearm their share. What was still wrong in the run of 0.2.0 (her left hand before her chest, turned and
    // bent at the wrist; hands like claws over him):
    //  - Which side of her arm the back of her hand is on was "checked" once against the game's own pose, and for her left
    //    arm (126 deg) turned round. But it is no guess: it follows from where her fingers are (HandFlat) and from the axis
    //    her elbow bends about, both found on her own bones, the same for any body. The check took a pose of the game's
    //    (her left hand palm up in its idle) for a wrong reckoning, and from then on all of her left hand was reckoned
    //    with the back of her hand on the wrong side: her elbow swung to its limit to spare a wrist that was not strained,
    //    and her hand was "turned back" into a turn it could not take.
    //  - The bones along her forearm were given a share of the turn of her hand counted from thumb-up. They are at rest
    //    where the body was built (arms out, palms down): a hand laid palm down had them turned 30 and 60 deg more than its
    //    hand, and the skin of her forearm was wrung against it.
    //  - Her fingers were sent the way they were to point whatever her forearm did, within 72 deg in any direction. A
    //    wrist bends sideways by 20 to 30 deg only: a hand pointing across the line of its forearm is a broken wrist.
    //  - Her fingers stayed as curled as the game's animation has them (claws), lying flat on him as well.
    //
    // Now, for every place her hand goes, it is said which way the surface there faces (the back of her hand is to face
    // that way) and which way her fingers would rather point. Then:
    //  1. her elbow is swung round the line from her shoulder to her hand to where her wrist is asked least and her
    //     forearm runs the way her fingers would rather point (that is what one's own elbow does);
    //  2. her fingers point the way her forearm runs, laid onto the surface, turned toward the way they would rather point
    //     by what a wrist bends sideways; her hand is bent back or forward no further than a wrist bends (beyond that her
    //     fingers lift and the heel of her hand bears) and turned about her forearm no further than a forearm turns;
    //  3. what that turns her hand against her forearm, beyond what the game's own pose had, is handed on to the bones
    //     along her forearm, each its share;
    //  4. her fingers are straightened to lie on what her hand lies on, and close on it in her orgasm.
    // The same is done for his hands when he holds her (Scenes.cs).
    public sealed partial class ShakeController
    {
        // (Degrees, from the hand in line with its forearm, thumb up. A forearm turns the palm down some 85 and up some 80;
        // a wrist bends back some 75 and forward some 70, and sideways 20 toward the thumb and 30 toward the little finger.)
        private static readonly float[] s_wristLogAt = new float[2];
        private const float PronateMost = 85f, SupinateMost = 80f, ExtendMost = 75f, FlexMost = 70f, RadialMost = 20f, UlnarMost = 30f;

        // Her arm put with its hand at "target", her elbow toward "pole" or round from it as her wrist asks, and her hand
        // laid with its back toward "back" (the way the surface faces) and its fingers the way her forearm runs, turned
        // toward "fingers" (either zero: her hand is not turned). "straight" (0..1): how far her fingers straighten to lie
        // on it; "grip" (0..1): how far they close on it.
        internal static void ArmSolve(Limb arm, int side, Vector3 target, Vector3 pole, Vector3 fingers, Vector3 back, float weight, float layWeight, float straight = 0f, float grip = 0f)
        {
            Transform upper = arm.upper.t, fore = arm.fore.t, hand = arm.end.t;
            bool lay = layWeight > 0.001f && arm.handOk && fingers.sqrMagnitude > 0.01f && back.sqrMagnitude > 0.01f;
            float dt = Mathf.Clamp(Time.deltaTime, 0.004f, 0.1f);
            // (Her forearm and her hand as the game's own animation has them, before anything of ours this frame.)
            Quaternion foreRot0 = fore.rotation, handRot0 = hand.rotation;
            if (lay && !arm.wristSeen)
            {
                WristSeen(arm, side);
            }
            if (lay)
            {
                fingers.Normalize();
                back.Normalize();
                // 1. Where her elbow goes: tried round the line from her shoulder to her hand, the cost of each place being
                // what it asks of her wrist and how far it is from where the pose has her elbow.
                Vector3 s = upper.position;
                float a = (fore.position - s).magnitude, b = (hand.position - fore.position).magnitude;
                Vector3 st = target - s;
                float d = Mathf.Clamp(st.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
                Vector3 u = st.sqrMagnitude > 1e-8f ? st.normalized : Vector3.down;
                Vector3 reach = s + u * d;
                float x = (a * a - b * b + d * d) / (2f * d);
                float h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
                Vector3 p0 = pole - u * Vector3.Dot(pole, u);
                p0 = p0.sqrMagnitude < 1e-6f ? Vector3.forward : p0.normalized;
                float best = 0f, bestCost = float.MaxValue;
                if (h > 0.02f)
                {
                    for (float ang = -75f; ang <= 75.01f; ang += 7.5f)
                    {
                        Vector3 p = Quaternion.AngleAxis(ang, u) * p0;
                        Vector3 elbow = s + u * x + p * h;
                        Vector3 f = (reach - elbow).normalized;
                        Vector3 hingeAxis = Vector3.Cross(elbow - s, reach - elbow);
                        if (hingeAxis.sqrMagnitude < 1e-10f)
                        {
                            continue;
                        }
                        float cost = LayHand(side, f, hingeAxis.normalized, back, fingers, out _, out _) + (ang / 70f) * (ang / 70f) * 0.55f;
                        if (arm.swivelHas)
                        {
                            // (Her elbow keeps to the place it is in: no jump from one turn of her arm about its axis to another, which
                            // was her arm wringing itself round as she moved.)
                            cost += 0.35f * Sq((ang - arm.swivel) / 30f);
                        }
                        if (cost < bestCost)
                        {
                            bestCost = cost;
                            best = ang;
                        }
                    }
                }
                best *= Mathf.Clamp01(layWeight);
                if (!arm.swivelHas)
                {
                    arm.swivel = best;
                    arm.swivelHas = true;
                }
                else
                {
                    // (Her elbow moves there, it does not jump from one tried place to the next.)
                    arm.swivel = Mathf.Lerp(arm.swivel, best, 1f - Mathf.Exp(-dt / 0.3f));
                }
                pole = Quaternion.AngleAxis(arm.swivel, u) * p0;
            }
            else
            {
                arm.swivelHas = false;
            }
            Ik(arm, target, pole, weight, true);
            if (!lay)
            {
                return;
            }
            // 2. and 3. Her hand, as asked, within what her wrist can do on her forearm as it lies now.
            HandLay(arm, side, back, fingers, Mathf.Clamp01(weight * layWeight), foreRot0, handRot0);
            // 4. Her fingers.
            FingersPose(arm, straight * weight, grip * weight, 0.25f * straight * weight);
        }

        // A hand laid on its forearm as the forearm lies now: its back toward "back", its fingers as LayHand finds them, by
        // "w" (0..1); and what that turns it against its forearm, beyond what the game had ("foreRot0", "handRot0": the
        // forearm and the hand as the game's animation had them this frame), handed on to the bones along the forearm.
        internal static void HandLay(Limb arm, int side, Vector3 back, Vector3 wish, float w, Quaternion foreRot0, Quaternion handRot0)
        {
            Transform upper = arm.upper.t, fore = arm.fore.t, hand = arm.end.t;
            Vector3 along = hand.position - fore.position;
            if (along.sqrMagnitude < 1e-8f || w < 0.001f || !arm.handOk || back.sqrMagnitude < 1e-6f || wish.sqrMagnitude < 1e-6f)
            {
                return;
            }
            along.Normalize();
            // (The axis the elbow bends about, as the upper arm carries it: steady even when the arm is all but straight.
            // Not found on this arm: from how it is bent now, if it is bent enough to tell; else the turn is not held.)
            Vector3 hinge = Vector3.zero;
            if (arm.hingeOk)
            {
                hinge = (upper.rotation * arm.hinge).normalized;
            }
            else
            {
                Vector3 upperRun = fore.position - upper.position;
                Vector3 bent = Vector3.Cross(upperRun, along);
                if (bent.magnitude > upperRun.magnitude * 0.25f)
                {
                    hinge = bent.normalized;
                }
            }
            LayHand(side, along, hinge, back.normalized, wish.normalized, out Vector3 fg, out Vector3 bk);
            if (hinge.sqrMagnitude > 0.5f && Time.unscaledTime >= s_wristLogAt[side & 1])
            {
                // (For the record, every few seconds: how far each wrist is turned and bent, to check the neutral it is counted from.)
                s_wristLogAt[side & 1] = Time.unscaledTime + 6f;
                Vector3 neutral = side == 1 ? -hinge : hinge;
                float turn = WristTurn(along, neutral, bk);
                float pron = side == 1 ? turn : -turn;
                float ext = -Mathf.Asin(Mathf.Clamp(Vector3.Dot(along, bk), -1f, 1f)) * Mathf.Rad2Deg;
                float cost = LayHand(side, along, hinge, back.normalized, wish.normalized, out Vector3 _, out Vector3 _);
                Plugin.ModLog.LogInfo($"Arms: {arm.who} {(side == 0 ? "left" : "right")} wrist as laid: {Mathf.Abs(pron):F0} deg {(pron >= 0f ? "palm down" : "palm up")} from thumb-up (neutral {neutral.x:F2} {neutral.y:F2} {neutral.z:F2}, forearm {along.x:F2} {along.y:F2} {along.z:F2}), bent {ext:F0} deg, strain {cost:F2}.");
            }
            Quaternion own = Quaternion.LookRotation(arm.handFingers, -arm.handPalm);
            Quaternion want = Quaternion.LookRotation(fg, bk) * Quaternion.Inverse(own);
            Quaternion handNew = Quaternion.Slerp(hand.rotation, want, w);
            // The bones along the forearm: by how much further the hand is now turned about the forearm than the game had it
            // turned against it. (Not its turn from thumb-up: they are at rest where the body was built, palm down.)
            Vector3 axisF = Quaternion.Inverse(fore.rotation) * along;
            float had = TwistAbout(Quaternion.Inverse(foreRot0) * handRot0, axisF);
            float now = TwistAbout(Quaternion.Inverse(fore.rotation) * handNew, axisF);
            ForearmTwist(arm, side, along, Mathf.DeltaAngle(had, now));
            // (The hand last: should it hang from those bones, turning them has turned it as well.)
            hand.rotation = handNew;
            arm.end.Commit();
        }

        // How a hand lies on a surface that faces "back", at the end of a forearm that runs along "f" with its elbow bending
        // about "hinge" (zero: not known, and the turn of the forearm is then not held): the way its fingers point and the
        // way its back faces. Given back: what that asks of the wrist (0 when it is at ease, 1 and more when it strains).
        private static float LayHand(int side, Vector3 f, Vector3 hinge, Vector3 back, Vector3 wish, out Vector3 fingers, out Vector3 backOut)
        {
            float cost = 0f;
            Vector3 n = back;
            // The fingers: the way the forearm runs, laid onto the surface (a forearm that comes straight down onto it gives no
            // way: then the way they would rather point), turned toward the way they would rather point by what a wrist bends
            // sideways (toward the thumb less than toward the little finger).
            Vector3 wp = Vector3.ProjectOnPlane(wish, n);
            Vector3 fp = Vector3.ProjectOnPlane(f, n);
            if (wp.sqrMagnitude < 1e-4f)
            {
                wp = fp.sqrMagnitude > 1e-6f ? fp : Vector3.Cross(n, Vector3.right);
            }
            wp.Normalize();
            Vector3 fd = fp + wp * 0.15f;
            fd = fd.sqrMagnitude > 1e-8f ? fd.normalized : wp;
            float dev = Vector3.SignedAngle(fd, wp, n);
            // (For a right hand palm down the thumb is on the left of its fingers; for a left hand, on the right.)
            Vector3 thumb = side == 1 ? Vector3.Cross(fd, n) : Vector3.Cross(n, fd);
            bool radial = Vector3.Dot(Quaternion.AngleAxis(Mathf.Sign(dev) * 10f, n) * fd, thumb) > 0f;
            float lim = radial ? RadialMost : UlnarMost;
            float devTaken = Mathf.Clamp(dev, -lim, lim);
            Vector3 fg = Quaternion.AngleAxis(devTaken, n) * fd;
            float miss = Mathf.Abs(dev) - Mathf.Abs(devTaken);
            cost += Sq(miss / 45f) * 0.6f + Sq(devTaken / lim) * 0.1f;

            // Bent back (the forearm comes down onto the back of the hand) or forward no further than a wrist bends: beyond, the
            // fingers lift toward the line of the forearm and the heel of the hand bears.
            float ext = -Mathf.Asin(Mathf.Clamp(Vector3.Dot(f, n), -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 bk = n;
            float over = ext > ExtendMost ? ext - ExtendMost : (ext < -FlexMost ? -FlexMost - ext : 0f);
            if (over > 0f)
            {
                Vector3 ax = Vector3.Cross(fg, f);
                if (ax.sqrMagnitude > 1e-8f)
                {
                    Quaternion lift = Quaternion.AngleAxis(over, ax.normalized);
                    fg = lift * fg;
                    bk = lift * bk;
                }
            }
            cost += Sq(Mathf.Max(0f, ext - 50f) / 25f) + Sq(Mathf.Max(0f, -ext - 35f) / 35f) + Sq(over / 12f);

            // Turned about the forearm no further than a forearm turns (palm down: pronation; palm up: supination), counted
            // from thumb-up, where the back of the hand faces out from the body: for a right arm against the axis its elbow
            // bends about, for a left arm along it.
            if (hinge.sqrMagnitude > 0.5f)
            {
                Vector3 neutral = side == 1 ? -hinge : hinge;
                float turn = WristTurn(f, neutral, bk);
                float pron = side == 1 ? turn : -turn;
                float excess = pron > PronateMost ? pron - PronateMost : (pron < -SupinateMost ? pron + SupinateMost : 0f);
                if (excess != 0f)
                {
                    // (Turned back about the forearm by what is too much: fingers and back of the hand together. The palm then
                    // does not lie quite flat: the forearm cannot turn it so.)
                    Quaternion undo = Quaternion.AngleAxis(side == 1 ? -excess : excess, f);
                    fg = undo * fg;
                    bk = undo * bk;
                }
                cost += Sq(Mathf.Max(0f, pron - 55f) / 30f) + Sq(Mathf.Max(0f, -pron - 35f) / 40f) + Sq(excess / 12f);
            }
            fingers = fg.normalized;
            backOut = Vector3.ProjectOnPlane(bk, fingers);
            backOut = backOut.sqrMagnitude > 1e-8f ? backOut.normalized : Vector3.ProjectOnPlane(n, fingers).normalized;
            return cost;
        }

        private static float Sq(float v)
        {
            return v * v;
        }

        private static float WristTurn(Vector3 f, Vector3 neutral, Vector3 back)
        {
            Vector3 n = Vector3.ProjectOnPlane(neutral, f), bw = Vector3.ProjectOnPlane(back, f);
            if (n.sqrMagnitude < 1e-6f || bw.sqrMagnitude < 1e-6f)
            {
                return 0f;
            }
            return Vector3.SignedAngle(n, bw, f);
        }

        // How far a rotation turns about an axis (degrees, -180..180).
        private static float TwistAbout(Quaternion q, Vector3 axis)
        {
            float along = q.x * axis.x + q.y * axis.y + q.z * axis.z;
            return Mathf.DeltaAngle(0f, 2f * Mathf.Atan2(along, q.w) * Mathf.Rad2Deg);
        }

        // Once, on an arm as the game's own animation has it (nothing of ours on it yet this frame): how its hand is turned
        // and bent, for the log. (This was once taken as a check of which side the back of her hand is on, and it turned
        // her left hand round: see the top of this file.)
        private static void WristSeen(Limb arm, int side)
        {
            Transform upper = arm.upper.t, fore = arm.fore.t, hand = arm.end.t;
            Vector3 run = hand.position - fore.position;
            Vector3 bends = Vector3.Cross(fore.position - upper.position, run);
            float aa = (fore.position - upper.position).magnitude, bb = run.magnitude;
            if (bends.magnitude < aa * bb * 0.2f)
            {
                bends = arm.hingeOk ? upper.rotation * arm.hinge : Vector3.zero;
            }
            if (bends.sqrMagnitude <= 1e-10f || bb <= 1e-4f)
            {
                return;
            }
            arm.wristSeen = true;
            Vector3 neutral = side == 1 ? -bends.normalized : bends.normalized;
            float turn = WristTurn(run.normalized, neutral, hand.rotation * -arm.handPalm);
            float pron = side == 1 ? turn : -turn;
            float bend = Vector3.Angle(run, hand.rotation * arm.handFingers);
            Plugin.ModLog.LogInfo($"Arms: {arm.who} {(side == 0 ? "left" : "right")} hand, as the game's own pose has it, is turned {Mathf.Abs(pron):F0} deg {(pron >= 0f ? "palm down" : "palm up")} from thumb-up and bent {bend:F0} deg against the forearm (for the record: which side the back of the hand is on follows from the bones, it is not checked against this).");
        }

        // The bones along a forearm that carry the turn of its hand (CC rigs: ForearmTwist01, 02): each is turned about the
        // forearm by its share of "delta", how much further the hand is turned against the forearm than the game's own pose
        // had it, on top of how the game has the bone. Should the game turn them itself after this (it has a stage for such
        // constraints), that is as good: it then goes by the hand as it is put here.
        private static void ForearmTwist(Limb arm, int side, Vector3 along, float delta)
        {
            if (!arm.twistSearched)
            {
                arm.twistSearched = true;
                try
                {
                    var found = new List<Transform>();
                    var under = arm.fore.t.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; under != null && i < under.Length; i++)
                    {
                        Transform t = under[i];
                        if (t == null || t == arm.fore.t || t == arm.end.t || t.IsChildOf(arm.end.t))
                        {
                            continue;
                        }
                        if ((t.name ?? "").IndexOf("twist", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            found.Add(t);
                        }
                    }
                    if (found.Count > 0 && found.Count <= 4)
                    {
                        // (Nearest the elbow first.)
                        found.Sort((p, q) => (p.position - arm.fore.t.position).sqrMagnitude.CompareTo((q.position - arm.fore.t.position).sqrMagnitude));
                        arm.twists = new RotSlot[found.Count];
                        arm.twistPart = new float[found.Count];
                        float len = Mathf.Max(0.05f, (arm.end.t.position - arm.fore.t.position).magnitude);
                        var names = new System.Text.StringBuilder();
                        for (int i = 0; i < found.Count; i++)
                        {
                            arm.twists[i] = new RotSlot(found[i]);
                            arm.twists[i].Begin();
                            // (Its share: by where it sits along the forearm, and never less than its turn in the row.)
                            float byPlace = Mathf.Clamp01((found[i].position - arm.fore.t.position).magnitude / len);
                            arm.twistPart[i] = Mathf.Clamp(Mathf.Max(byPlace, (i + 1f) / (found.Count + 1f)), 0.2f, 0.85f);
                            names.Append(i > 0 ? ", " : "").Append(found[i].name).Append(" (").Append((arm.twistPart[i] * 100f).ToString("F0")).Append(" %, under ").Append(found[i].parent != null ? found[i].parent.name : "?").Append(')');
                        }
                        Plugin.ModLog.LogInfo($"Arms: {arm.who} {(side == 0 ? "left" : "right")} forearm has {found.Count} bone(s) that carry the turn of the hand: {names}; the hand hangs from '{(arm.end.t.parent != null ? arm.end.t.parent.name : "?")}'.");
                    }
                    else
                    {
                        Plugin.ModLog.LogInfo($"Arms: {arm.who} {(side == 0 ? "left" : "right")} forearm has no bones of its own for the turn of the hand ({found.Count} named so); only the hand is turned.");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.ModLog.LogInfo("Arms: the bones along the forearm could not be looked for (" + ex.Message + ").");
                }
            }
            if (arm.twists == null)
            {
                return;
            }
            float given = 0f;
            for (int i = 0; i < arm.twists.Length; i++)
            {
                Transform t = arm.twists[i].t;
                if (t == null || t.parent == null)
                {
                    continue;
                }
                // (A bone that hangs from the one before it is already turned by that one's share.)
                bool chained = i > 0 && arm.twists[i - 1].t != null && t.parent == arm.twists[i - 1].t;
                float share = arm.twistPart[i] * delta - (chained ? given : 0f);
                given = arm.twistPart[i] * delta;
                // (About the forearm, in the bone's own turning: its own axes need not lie as the forearm's do.)
                Vector3 axis = Quaternion.Inverse(t.rotation) * along;
                if (Mathf.Abs(share) > 0.01f && axis.sqrMagnitude > 1e-8f)
                {
                    t.localRotation = t.localRotation * Quaternion.AngleAxis(share, axis.normalized);
                }
                arm.twists[i].Commit();
                if (arm.twistLogs < 2 && Mathf.Abs(delta) > 20f && i == arm.twists.Length - 1)
                {
                    arm.twistLogs++;
                    Plugin.ModLog.LogInfo($"Arms: {arm.who} {(side == 0 ? "left" : "right")} hand is turned {delta:F0} deg further about the forearm than the game had it; '{t.name}' takes {arm.twistPart[i] * 100f:F0} % of that.");
                }
            }
        }

        // ---- fingers ----

        private static readonly string[] s_fingerKeys = { "index", "mid", "ring", "pinky" };

        private static bool IsFinger(string name, string key)
        {
            string n = (name ?? "").ToLowerInvariant();
            // (CC rigs of this game: CC_Base_Finger00.L is the thumb, Finger1x the first finger, 2x the middle, 3x the ring,
            // 4x the little finger; x counts the joints, and a "Nub" ends the chain.)
            string num = key == "index" ? "finger1" : key == "mid" ? "finger2" : key == "ring" ? "finger3" : key == "pinky" ? "finger4" : "";
            return n.Contains(key) || (key == "pinky" && n.Contains("little")) || n.Contains(num);
        }

        // The bones of four fingers of a hand (first, middle, ring, little), each from its root toward its tip, by their
        // names (CC rigs: Index1..3 and so on). Found once.
        private static void FingersFind(Limb arm)
        {
            arm.fingersSearched = true;
            try
            {
                Transform hand = arm.end.t;
                var under = hand.GetComponentsInChildren<Transform>(true);
                var chains = new List<RotSlot[]>();
                var names = new System.Text.StringBuilder();
                foreach (string key in s_fingerKeys)
                {
                    // (The first bone under the hand with its name: its root, which comes before the bones that hang from it.)
                    Transform root = null;
                    for (int c = 0; under != null && c < under.Length && root == null; c++)
                    {
                        if (under[c] != null && under[c] != hand && IsFinger(under[c].name, key))
                        {
                            root = under[c];
                        }
                    }
                    if (root == null)
                    {
                        continue;
                    }
                    var bones = new List<RotSlot> { new RotSlot(root) };
                    Transform at = root;
                    while (bones.Count < 3)
                    {
                        Transform next = null;
                        for (int c = 0; c < at.childCount && next == null; c++)
                        {
                            Transform ch = at.GetChild(c);
                            if (ch != null && IsFinger(ch.name, key))
                            {
                                next = ch;
                            }
                        }
                        if (next == null)
                        {
                            break;
                        }
                        bones.Add(new RotSlot(next));
                        at = next;
                    }
                    if (bones.Count < 2)
                    {
                        // (Not nested: the bones of this finger by their names, from the one nearest the wrist outward.)
                        var same = new List<Transform>();
                        for (int c = 0; under != null && c < under.Length; c++)
                        {
                            if (under[c] != null && under[c] != hand && IsFinger(under[c].name, key))
                            {
                                same.Add(under[c]);
                            }
                        }
                        same.Sort((p, q) => (p.position - hand.position).sqrMagnitude.CompareTo((q.position - hand.position).sqrMagnitude));
                        if (same.Count >= 2)
                        {
                            bones = new List<RotSlot>();
                            for (int k = 0; k < same.Count && k < 3; k++)
                            {
                                bones.Add(new RotSlot(same[k]));
                            }
                            root = same[0];
                        }
                    }
                    if (bones.Count >= 2)
                    {
                        foreach (RotSlot b in bones)
                        {
                            b.Begin();
                        }
                        chains.Add(bones.ToArray());
                        names.Append(names.Length > 0 ? ", " : "").Append(root.name).Append(" (").Append(bones.Count).Append(')');
                    }
                }
                if (chains.Count >= 3)
                {
                    arm.fingers = chains.ToArray();
                }
                if (arm.fingers == null)
                {
                    var kids = new System.Text.StringBuilder();
                    for (int c = 0; under != null && c < under.Length && c < 40; c++)
                    {
                        if (under[c] != null && under[c] != hand)
                        {
                            kids.Append(kids.Length > 0 ? ", " : "").Append(under[c].name).Append(under[c].parent == hand ? "*" : "");
                        }
                    }
                    Plugin.ModLog.LogInfo($"Arms: the bones under {arm.who} hand '{hand.name}' (* right under it): {kids}.");
                }
                Plugin.ModLog.LogInfo($"Arms: {arm.who} {(arm.side == 0 ? "left" : "right")} fingers: {(arm.fingers != null ? names.ToString() + "; they are laid flat and close where the hand holds on" : "not found by name (" + chains.Count + " of 4); they are left as the game has them")}.");
            }
            catch (Exception ex)
            {
                arm.fingers = null;
                Plugin.ModLog.LogInfo("Arms: the bones of the fingers could not be looked for (" + ex.Message + ").");
            }
        }

        // The fingers of a hand: straightened by "straight" (0..1) from as curled as the game's animation has them to all
        // but straight (they lie on what the hand lies on), curled in by "grip" (0..1: some 35 deg at each joint, they take
        // hold), and drawn together toward the middle finger by "close" (0..1).
        internal static void FingersPose(Limb arm, float straight, float grip, float close)
        {
            if (!arm.fingersSearched)
            {
                FingersFind(arm);
            }
            if (arm.fingers == null || !arm.handOk || (straight < 0.001f && grip < 0.001f && close < 0.001f))
            {
                return;
            }
            straight = Mathf.Clamp01(straight);
            grip = Mathf.Clamp01(grip);
            close = Mathf.Clamp01(close);
            Transform hand = arm.end.t;
            Vector3 palm = (hand.rotation * arm.handPalm).normalized;
            Vector3 ahead = (hand.rotation * arm.handFingers).normalized;
            // (Toward the middle finger: the way its root points, in the plane of the palm.)
            Vector3 mid = ahead;
            foreach (RotSlot[] f in arm.fingers)
            {
                if (f[0].t != null && f[1].t != null && IsFinger(f[0].t.name, "mid"))
                {
                    mid = f[1].t.position - f[0].t.position;
                    break;
                }
            }
            Vector3 midFlat = Vector3.ProjectOnPlane(mid, palm);
            foreach (RotSlot[] f in arm.fingers)
            {
                if (f[0].t == null || f[1].t == null)
                {
                    continue;
                }
                // Together: its root turned toward the middle finger, in the plane of the palm.
                if (close > 0.001f && !IsFinger(f[0].t.name, "mid") && midFlat.sqrMagnitude > 1e-8f)
                {
                    Vector3 d = Vector3.ProjectOnPlane(f[1].t.position - f[0].t.position, palm);
                    if (d.sqrMagnitude > 1e-8f)
                    {
                        f[0].t.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(d, midFlat, palm) * close, palm) * f[0].t.rotation;
                    }
                }
                // Straighter, then curled in: each joint about the axis it bends on toward the palm.
                for (int j = 0; j < f.Length; j++)
                {
                    Transform b = f[j].t;
                    Vector3 to;
                    if (j + 1 < f.Length)
                    {
                        to = f[j + 1].t.position;
                    }
                    else if (b.childCount > 0)
                    {
                        to = b.GetChild(0).position;
                    }
                    else
                    {
                        // (Its tip is not known: the last joint is left as it is.)
                        break;
                    }
                    Vector3 run = to - b.position;
                    Vector3 from = j == 0 ? ahead : b.position - f[j - 1].t.position;
                    Vector3 axis = Vector3.Cross(from, palm);
                    if (run.sqrMagnitude < 1e-10f || axis.sqrMagnitude < 1e-10f)
                    {
                        continue;
                    }
                    axis.Normalize();
                    // (How far it is curled toward the palm at this joint now; a hand at rest on something keeps a little.)
                    float curled = Vector3.SignedAngle(from, Vector3.ProjectOnPlane(run, axis), axis);
                    float want = curled > 8f ? Mathf.Lerp(curled, 8f, straight) : curled;
                    want += 35f * grip * (j == 0 ? 0.6f : 1f);
                    float by = want - curled;
                    if (Mathf.Abs(by) > 0.05f)
                    {
                        b.rotation = Quaternion.AngleAxis(by, axis) * b.rotation;
                    }
                }
                foreach (RotSlot b in f)
                {
                    b.Commit();
                }
            }
        }
    }
}
