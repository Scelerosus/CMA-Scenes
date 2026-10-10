using System;
using UnityEngine;

namespace CMAScenes
{
    // What CMA Scenes adds to the ride on top of CMA Shake's: her womb, her hands finding their own hold on him, and
    // his hands taking hold of her.
    public sealed partial class ShakeController
    {
        // ---- her womb ----
        //
        // Her womb is the continuation of her vagina, not a wall at the end of it. When he comes in her, bare, his head goes
        // in through the neck of it: she sits down on him that much deeper, for as long as he is coming and a little after.
        // (The X-ray draws her womb back round his head: XRayWomb.cs.)
        private float m_wombDive;

        private void WombDiveStep(float dt)
        {
            float reach = Mathf.Clamp(Plugin.RideWomb.Value, 0f, 0.06f);
            float since = Time.unscaledTime - m_cameAt;
            bool coming = reach > 0.001f && m_ride && m_toggled && m_mount == 3 && m_in && m_condom != 4 && !m_holeAnal && since >= 0f && since < 8f;
            float goal = coming ? reach * Mathf.SmoothStep(0f, 1f, Mathf.Min(since / 0.9f, (8f - since) / 2.5f)) : 0f;
            m_wombDive = Mathf.MoveTowards(m_wombDive, goal, dt * 0.04f);
        }

        // ---- her hands on him, found by her ----
        //
        // "Auto": where her hands are is chosen from what she is doing. Sitting up they rest on her thighs; as she warms up
        // and leans over him they go to his chest; leaning back, to his thighs behind her; in her orgasm, and in his, she
        // takes hold of him (his shoulders when it is hers, his chest when it is his). A choice stands for a while, so that
        // her hands do not wander about, and is made at once for an orgasm.
        private string m_autoHands = "Thighs";
        private float m_autoHandsAt = -100f;

        private string AutoHands(bool herBack)
        {
            if (herBack)
            {
                return "Thighs";
            }
            float now = Time.unscaledTime;
            float lean = Plugin.RideLean.Value;
            bool hers = m_climaxHers && ClimaxNow() > 0.15f;
            bool his = !m_climaxHers && ClimaxNow() > 0.15f;
            string want;
            if (m_mount < 3)
            {
                want = "Thighs";
            }
            else if (hers)
            {
                // (In her orgasm she holds on to his chest: no longer folded over him to the floor by his shoulders.)
                want = "HisChest";
            }
            else if (his)
            {
                want = "HisChest";
            }
            else if (lean < -8f)
            {
                want = "Behind";
            }
            else if (m_herPleasure > 0.35f || m_herWet > 0.6f || lean > 26f)
            {
                want = "HisChest";
            }
            else
            {
                want = "Thighs";
            }
            if (want != m_autoHands && (hers || his || now - m_autoHandsAt > 10f))
            {
                Plugin.ModLog.LogInfo($"Ride: her hands: she moves them from '{m_autoHands}' to '{want}' ({(hers ? "her orgasm" : his ? "his orgasm" : "how she is")}).");
                m_autoHands = want;
                m_autoHandsAt = now;
            }
            return m_autoHands;
        }

        // ---- his hands on her ----
        //
        // "He holds her": his hands take her by the hips and her bottom, and he moves her on himself with his hips (his lift,
        // HeroMeets, is longer and her own stroke is shorter by as much). His arms are put on her hips as she is posed
        // (where her hips were the frame before, so no later than a frame behind them).
        //
        // In the run of 0.2.0 his hands were only carried to a point beside her hip joint: the hand itself was left turned
        // as the game's animation had it at his side, so it stood off her with its fingers spread to either side of her
        // buttock. Now his palm is laid on her: on her buttock, its skin facing back, out and a little down, his fingers
        // the way his forearm runs, turned toward her cleft and under her as far as a wrist bends, held together; out of
        // the reach of his arm (it is some 55 cm, his shoulders lie 50 to 60 cm from her bottom, and she rises with every
        // stroke) it goes along her flank to the side of her hip, so that his arm stays a little bent.
        private float m_holdW, m_holdHandsW, m_holdSupport;
        private readonly Limb[] m_holdArm = new Limb[2];
        // (Which of her hips each of his hands has: 0 her left, 1 her right; and how far each has gone from her buttock
        // toward the side of her hip for want of reach.)
        private readonly int[] m_holdHip = { -1, -1 };
        private readonly float[] m_holdSlide = new float[2];
        private readonly Vector3[] m_herJoint = new Vector3[2];
        private bool m_herJointValid, m_holdFailed, m_holdTold, m_holdReachTold;

        internal float HoldWeight => m_holdW;

        private void HoldStep(float dt)
        {
            bool mounted = m_ride && m_toggled && m_mount == 3 && m_in && m_heroReady && !m_holdFailed && m_herJointValid;
            bool want = Plugin.RideHeHolds.Value && mounted;
            bool facingAway = string.Equals(Plugin.RideFacing.Value, "Away", StringComparison.OrdinalIgnoreCase);
            bool wantHands = mounted && (Plugin.RideHeHolds.Value || facingAway);
            m_holdW = Mathf.MoveTowards(m_holdW, want ? 1f : 0f, dt / 1.2f);
            m_holdHandsW = Mathf.MoveTowards(m_holdHandsW, wantHands ? 1f : 0f, dt / 0.45f);
        }

        private void HoldHands(float weight)
        {
            if (m_holdHandsW < 0.01f || m_rideWoman == null || !m_herJointValid || m_holdFailed || m_hero == null)
            {
                m_holdHip[0] = m_holdHip[1] = -1;
                if (m_holdHandsW < 0.01f)
                {
                    m_holdReachTold = false;
                }
                return;
            }
            try
            {
                Animator anim = m_hero.bodyAnimator;
                if (anim == null || !anim.isHuman)
                {
                    return;
                }
                Vector3 mid = 0.5f * (m_herJoint[0] + m_herJoint[1]);
                Vector3 right = Vector3.ProjectOnPlane(m_herJoint[1] - m_herJoint[0], Vector3.up);
                if (right.sqrMagnitude < 1e-6f)
                {
                    return;
                }
                right.Normalize();
                Vector3 fwd = Vector3.Cross(right, Vector3.up);
                float dt = Mathf.Clamp(Time.deltaTime, 0.004f, 0.1f);
                for (int side = 0; side < 2; side++)
                {
                    m_holdHip[side] = -1;
                    if (m_heroUpperArm[side] == null || m_limb[side] == null || m_heroArmSlot[side] == null || m_handUse[side] > 0.02f)
                    {
                        if (m_holdArm[side] != null)
                        {
                            // (His hand taken up by the game's own control of it: his hand and fingers as the game has them.)
                            m_holdArm[side].end.Begin();
                            BeginExtras(m_holdArm[side]);
                        }
                        continue;
                    }
                    Limb arm = m_holdArm[side];
                    if (arm == null)
                    {
                        Transform fore = anim.GetBoneTransform(side == 0 ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                        if (fore == null)
                        {
                            continue;
                        }
                        arm = new Limb { upper = m_heroArmSlot[side], fore = new RotSlot(fore), end = new RotSlot(m_limb[side]), side = side, who = "his" };
                        m_holdArm[side] = arm;
                    }
                    arm.fore.Begin();
                    arm.end.Begin();
                    BeginExtras(arm);
                    // (How his hand lies on its bone, from his fingers: found once.)
                    HandFlat(arm, side, fwd, 0f);
                    // The hip his hand takes: the one on his side of her.
                    int j = Vector3.Dot(m_heroUpperArm[side].position - mid, right) > 0f ? 1 : 0;
                    m_holdHip[side] = j;
                    Vector3 outward = j == 0 ? -right : right;
                    Vector3 shoulder = m_heroUpperArm[side].position;
                    float reach = (arm.fore.t.position - shoulder).magnitude + (arm.end.t.position - arm.fore.t.position).magnitude;
                    // Where the middle of his palm goes. On her buttock, low and on the outside of it: a hand's breadth out
                    // from her cleft, a little below her hip joints, behind them by the flesh of her bottom; her skin there
                    // faces back, out and a little down. His fingers would rather point across it toward her cleft and under
                    // her. Or on the side of her hip: skin facing out, fingers back round her hip toward her bottom.
                    float support = m_holdSupport;
                    Vector3 glute = mid + outward * 0.085f - Vector3.up * 0.045f - fwd * 0.105f + Vector3.up * (0.012f * support);
                    Vector3 gluteN = Vector3.Slerp(
                        (-fwd + outward * 0.5f - Vector3.up * 0.2f).normalized,
                        (Vector3.up * 0.72f - fwd * 0.42f + outward * 0.18f).normalized,
                        support).normalized;
                    Vector3 gluteWish = Vector3.Slerp(
                        (-outward * 0.55f - Vector3.up * 0.6f - fwd * 0.1f).normalized,
                        (-outward * 0.42f + Vector3.up * 0.65f - fwd * 0.12f).normalized,
                        support).normalized;
                    Vector3 hipSide = m_herJoint[j] + outward * 0.095f + Vector3.up * 0.01f - fwd * 0.02f;
                    Vector3 hipN = (outward - fwd * 0.35f).normalized;
                    Vector3 hipWish = (-fwd * 0.8f - Vector3.up * 0.3f).normalized;
                    // As far toward her buttock as his arm reaches with its elbow still a little bent.
                    float slideWant = 1f;
                    for (float t = 0f; t <= 1.001f; t += 0.1f)
                    {
                        if ((HoldWrist(shoulder, Vector3.Lerp(glute, hipSide, t), Vector3.Slerp(gluteN, hipN, t)) - shoulder).magnitude <= reach * 0.92f)
                        {
                            slideWant = t;
                            break;
                        }
                    }
                    m_holdSlide[side] = Mathf.Lerp(m_holdSlide[side], slideWant, 1f - Mathf.Exp(-dt / (slideWant > m_holdSlide[side] ? 0.3f : 0.9f)));
                    float k = m_holdSlide[side];
                    Vector3 skin = Vector3.Slerp(gluteN, hipN, k).normalized;
                    Vector3 wish = Vector3.Slerp(gluteWish, hipWish, k).normalized;
                    Vector3 target = HoldWrist(shoulder, Vector3.Lerp(glute, hipSide, k), skin);
                    // (His elbow out to the side and down toward what he lies on.)
                    Vector3 pole = outward * 0.6f - Vector3.up * 0.8f;
                    float w = weight * m_holdHandsW;
                    Quaternion foreRot0 = arm.fore.t.rotation, handRot0 = arm.end.t.rotation;
                    Ik(arm, target, pole, w, true);
                    // His palm on her skin, within what his wrist does; his fingers together, a little curled to her.
                    HandLay(arm, side, skin, wish, w, foreRot0, handRot0);
                    FingersPose(arm, 0.4f * w, 0.1f * w, 0.65f * w);
                    if (!m_holdReachTold && m_holdHandsW > 0.9f)
                    {
                        m_holdReachTold = true;
                        float dGlute = (HoldWrist(shoulder, glute, gluteN) - shoulder).magnitude;
                        float dHip = (HoldWrist(shoulder, hipSide, hipN) - shoulder).magnitude;
                        Plugin.ModLog.LogInfo($"Ride: he holds her: his {(side == 0 ? "left" : "right")} arm reaches {reach * 100f:F0} cm to the wrist; for his palm on her buttock his wrist would be {dGlute * 100f:F0} cm from his shoulder, on the side of her hip {dHip * 100f:F0} cm; his hand lies {(k < 0.2f ? "on her buttock" : k > 0.8f ? "on the side of her hip" : "between her buttock and the side of her hip")} ({(arm.handOk ? "palm on her" : "his fingers not found: not laid")}).");
                    }
                }
                if (!m_holdTold)
                {
                    m_holdTold = true;
                    Plugin.ModLog.LogInfo("Ride: he holds her: his hands are put on her hips.");
                }
            }
            catch (Exception ex)
            {
                m_holdFailed = true;
                Plugin.ModLog.LogWarning("His hands could not be put on her: " + ex.Message);
            }
        }

        // Where his wrist goes for the middle of his palm to lie on her at "spot", her skin there facing "skin": off her skin
        // by the thickness of a hand, and back from the middle of his palm the way his forearm comes from (from his shoulder,
        // as near as matters), by the length of a palm.
        private static Vector3 HoldWrist(Vector3 shoulder, Vector3 spot, Vector3 skin)
        {
            Vector3 from = Vector3.ProjectOnPlane(spot - shoulder, skin);
            return spot + skin * 0.025f - (from.sqrMagnitude > 1e-6f ? from.normalized : Vector3.zero) * 0.05f;
        }

        // Whether he holds her with a hand of his on her (for her hands to take hold of his forearms).
        private bool HisForearmsThere()
        {
            return (m_holdHip[0] >= 0 && m_holdArm[0] != null) || (m_holdHip[1] >= 0 && m_holdArm[1] != null);
        }

        // Where her hand of the side that is given goes on his forearm that holds her hip of that side: on top of it and to
        // the outside, a little before his wrist, with which way her palm faces and which way her fingers would rather
        // point (round it, toward its underside). His arm is posed earlier in the frame than she is (PoseHero), so it is
        // where it will be drawn.
        private bool HisForearmSpot(int herSide, Vector3 outward, out Vector3 spot, out Vector3 back, out Vector3 wish)
        {
            spot = back = wish = Vector3.zero;
            for (int k = 0; k < 2; k++)
            {
                Limb arm = m_holdArm[k];
                if (arm == null || m_holdHip[k] != herSide || arm.fore.t == null || arm.end.t == null)
                {
                    continue;
                }
                Vector3 elbow = arm.fore.t.position, wrist = arm.end.t.position;
                Vector3 run = wrist - elbow;
                if (run.sqrMagnitude < 1e-4f)
                {
                    return false;
                }
                run.Normalize();
                back = Vector3.ProjectOnPlane(Vector3.up * 0.6f + outward * 0.8f, run);
                if (back.sqrMagnitude < 1e-6f)
                {
                    return false;
                }
                back.Normalize();
                // (His forearm is some 3.5 cm thick there, her hand some 2.5.)
                spot = Vector3.Lerp(elbow, wrist, 0.62f) + back * 0.06f;
                Vector3 round = Vector3.Cross(run, back).normalized;
                wish = Vector3.Dot(round, -Vector3.up - outward * 0.5f) > 0f ? round : -round;
                return true;
            }
            return false;
        }
    }
}
