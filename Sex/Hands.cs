using System;
using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;

namespace CMAScenes
{
    public sealed partial class ShakeController
    {
        // ---- her upper body: where it will be, and how far it has to bend ----

        // How far down her thighs her hands lie (eased), and where her hips were carried the frame before.
        private readonly float[] m_handPart = { 0.62f, 0.62f };
        // Each hand finds its own reachable contact point on him; the shared bend follows the farther reach.
        private readonly float[] m_chestFar = { 0.3f, 0.3f }, m_chestOk = new float[2];
        private float m_chestLean, m_chestLogAt;
        // (When a hand of hers was last told to be out of her reach.)
        private float m_reachLogAt;

        // Where a point of her upper body (her shoulders, her head) will be once she is posed: her back worked through
        // joint by joint as the pose turns it, from where her bones are before she is posed and where her hips were
        // put the frame before. (The lean is shared out: her pelvis 45 %, her waist 25, her spine 18 and 12; in this
        // rig her waist hangs from her hips beside her pelvis, so her shoulders turn by 55 % of it only.)
        private Vector3 SpineAt(Woman w, Vector3 right, float lean, float extra, float arch, float tilt, Vector3 point)
        {
            Vector3 p0 = w.sPelvis.t.position, p1 = w.sWaist.t.position, p2 = w.sSp1.t.position, p3 = w.sSp2.t.position;
            bool onPelvis = w.sWaist.t.IsChildOf(w.sPelvis.t);
            float pelvis = lean * 0.45f + arch + tilt;
            float back = onPelvis ? tilt : 0f;
            // (The bend over him: 35 % of it is her pelvis tipping forward at her hip joints, her back going with it;
            // most of the rest is low in her back, at her waist, and little of it higher up: her upper back stays
            // straight, so that she is held up on her arms and her head does not hang down into him.)
            float a1 = (onPelvis ? pelvis : 0f) + extra * 0.35f + lean * 0.25f - arch * 0.55f - back * 0.6f + extra * 0.45f;
            float a2 = a1 + lean * 0.18f - arch * 0.3f - back * 0.4f + extra * 0.15f;
            float a3 = a2 + lean * 0.12f - arch * 0.15f + extra * 0.05f;
            Vector3 from = onPelvis ? p0 + Quaternion.AngleAxis(pelvis, right) * (p1 - p0) : p1;
            return m_lastHipOffset + from
                + Quaternion.AngleAxis(a1, right) * (p2 - p1)
                + Quaternion.AngleAxis(a2, right) * (p3 - p2)
                + Quaternion.AngleAxis(a3, right) * (point - p3);
        }

        // His shoulders, as the game's own skeleton has them (looked for once for each hero).
        private readonly Transform[] m_heroShoulder = new Transform[2];
        private Character m_heroShoulderOf;

        // Where her wrist goes for her palm to lie on his shoulder, on the side that is given: on top of it (he lies on
        // his back), a little toward his neck.
        private bool HeroShoulderSpot(Vector3 outward, out Vector3 spot)
        {
            spot = Vector3.zero;
            if (m_hero == null)
            {
                return false;
            }
            if (m_heroShoulderOf != m_hero)
            {
                m_heroShoulderOf = m_hero;
                m_heroShoulder[0] = m_heroShoulder[1] = null;
                Animator anim = m_hero.bodyAnimator;
                if (anim != null && anim.isHuman)
                {
                    m_heroShoulder[0] = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    m_heroShoulder[1] = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                }
                Plugin.ModLog.LogInfo($"Ride: his shoulders: {(m_heroShoulder[0] != null ? m_heroShoulder[0].name : "not found")}, {(m_heroShoulder[1] != null ? m_heroShoulder[1].name : "not found")}.");
            }
            if (m_heroShoulder[0] == null || m_heroShoulder[1] == null)
            {
                return false;
            }
            Transform s = Vector3.Dot(m_heroShoulder[0].position - m_heroShoulder[1].position, outward) > 0f ? m_heroShoulder[0] : m_heroShoulder[1];
            Vector3 toNeck = m_heroHead != null ? m_heroHead.position - s.position : -outward;
            toNeck.y = 0f;
            toNeck = toNeck.sqrMagnitude > 1e-6f ? toNeck.normalized : -outward;
            // Behind his shoulder: on the floor just past it toward his head, and a little outside it (he lies on his
            // back; his shoulder joint is a hand's thickness above the floor). Not far past it: the further her hands
            // are from her hips, the lower she has to come down to reach them.
            spot = s.position + m_heroFwd * 0.06f + outward * 0.05f;
            spot.y = s.position.y - 0.05f;
            return true;
        }

        // Turns her hand so that its palm is down and its fingers point the way that is given. How her hand lies on its
        // bone is found once, from the bones of her fingers.
        private static void HandFlat(Limb arm, int side, Vector3 fingers, float weight)
        {
            Transform hand = arm.end.t;
            if (!arm.handSearched)
            {
                arm.handSearched = true;
                Transform mid = null, index = null, pinky = null;
                var under = hand.GetComponentsInChildren<Transform>(true);
                var names = new System.Text.StringBuilder();
                for (int c = 0; under != null && c < under.Length; c++)
                {
                    Transform f = under[c];
                    if (f == null || f == hand)
                    {
                        continue;
                    }
                    if (f.parent == hand && names.Length < 300)
                    {
                        names.Append(names.Length > 0 ? ", " : "").Append(f.name);
                    }
                    // (The first of each finger's bones comes first: the one at its base.)
                    string name = f.name.ToLowerInvariant();
                    if (mid == null && name.Contains("mid")) mid = f;
                    else if (index == null && name.Contains("index")) index = f;
                    else if (pinky == null && (name.Contains("pinky") || name.Contains("little"))) pinky = f;
                }
                if (mid == null || index == null || pinky == null)
                {
                    // Not by name: the four bones right under her hand that are furthest from her wrist are her bones
                    // (her thumb starts nearer to it), the two furthest apart of them her first and her little finger,
                    // and of those two the one nearer her thumb her first.
                    var bones = new List<Transform>();
                    Transform thumb = null;
                    for (int c = 0; c < hand.childCount; c++)
                    {
                        bones.Add(hand.GetChild(c));
                    }
                    bones.Sort((a, b) => (b.position - hand.position).sqrMagnitude.CompareTo((a.position - hand.position).sqrMagnitude));
                    if (bones.Count >= 5 && (bones[3].position - hand.position).magnitude > 0.04f)
                    {
                        thumb = bones[4];
                        float widest = 0f;
                        for (int a = 0; a < 4; a++)
                        {
                            for (int b = a + 1; b < 4; b++)
                            {
                                float apart = (bones[a].position - bones[b].position).sqrMagnitude;
                                if (apart > widest)
                                {
                                    widest = apart;
                                    bool aIsFirst = (bones[a].position - thumb.position).sqrMagnitude < (bones[b].position - thumb.position).sqrMagnitude;
                                    index = aIsFirst ? bones[a] : bones[b];
                                    pinky = aIsFirst ? bones[b] : bones[a];
                                }
                            }
                        }
                        for (int a = 0; a < 4; a++)
                        {
                            if (bones[a] != index && bones[a] != pinky)
                            {
                                mid = bones[a];
                                break;
                            }
                        }
                    }
                }
                if (mid == null || index == null || pinky == null)
                {
                    Plugin.ModLog.LogInfo($"Ride: the bones right under {arm.who} hand '{hand.name}': {(names.Length > 0 ? names.ToString() : "none")}.");
                }
                if (mid != null && index != null && pinky != null)
                {
                    Quaternion inv = Quaternion.Inverse(hand.rotation);
                    Vector3 along = inv * (mid.position - hand.position).normalized;
                    Vector3 across = inv * (index.position - pinky.position).normalized;
                    // (Her palm is on the other side of her hand for each arm.)
                    Vector3 palm = Vector3.Cross(along, across) * (side == 0 ? -1f : 1f);
                    if (palm.sqrMagnitude > 1e-4f)
                    {
                        arm.handFingers = along;
                        arm.handPalm = palm.normalized;
                        arm.handOk = true;
                    }
                }
                Plugin.ModLog.LogInfo($"Ride: {arm.who} {(side == 0 ? "left" : "right")} hand: {(arm.handOk ? "how it lies on its bone was found from the fingers; it is laid flat where it is put" : "the fingers were not found; it is left as the arm has it")}.");
            }
            if (!arm.handOk)
            {
                return;
            }
            Quaternion own = Quaternion.LookRotation(arm.handFingers, -arm.handPalm);
            Quaternion want = Quaternion.LookRotation(fingers, Vector3.up) * Quaternion.Inverse(own);
            hand.rotation = Quaternion.Slerp(hand.rotation, want, weight);
            arm.end.Commit();
        }

        // How far her back has to bend, over what the lean bends it, for her hands to reach the floor behind his
        // shoulders: the least that brings her shoulders within an arm of it. Eased: her hips move with every stroke,
        // and her hands, which are on him, stay where they are.
        private float m_overBend, m_sideBend;
        // (Faster than the speed that is set: her hands go to the floor either side of him. Plugin.cs.)
        private bool m_fastHands;

        private float OverBend(Woman w, Vector3 right, float lean, float arch, float tilt, float dt)
        {
            if (w.arms[0] == null || w.arms[1] == null || !HeroShoulderSpot(-right, out Vector3 left) || !HeroShoulderSpot(right, out Vector3 other))
            {
                return m_overBend;
            }
            return BendToReach(w, right, lean, arch, tilt, dt, 0.5f * (left + other), ref m_overBend);
        }

        // Where her hand goes on the floor beside him, on the side that is given: a little way up him from her hips,
        // clear of his body.
        private Vector3 SideSpot(Vector3 from, Vector3 fwd, Vector3 outward)
        {
            Vector3 spot = from + fwd * 0.3f + outward * 0.32f;
            spot.y = HeroShoulderSpot(outward, out Vector3 behind) ? behind.y : m_rideFloorY + 0.02f;
            return spot;
        }

        // How far her back has to bend, over what the lean bends it, for her shoulders to come within an arm of a place.
        private float BendToReach(Woman w, Vector3 right, float lean, float arch, float tilt, float dt, Vector3 mid, ref float eased)
        {
            if (w.arms[0] == null || w.arms[1] == null)
            {
                return eased;
            }
            Vector3 shoulders = 0.5f * (w.arms[0].upper.t.position + w.arms[1].upper.t.position);
            float arm = float.MaxValue;
            for (int i = 0; i < 2; i++)
            {
                Limb a = w.arms[i];
                arm = Mathf.Min(arm, (a.fore.t.position - a.upper.t.position).magnitude + (a.end.t.position - a.fore.t.position).magnitude);
            }
            float best = 0f, bestD = float.MaxValue;
            bool reached = false;
            for (float e = 0f; e <= 110f && !reached; e += 3f)
            {
                float d = (SpineAt(w, right, lean, e, arch, tilt, shoulders) - mid).magnitude;
                // (No more than it takes with her arms all but straight: the less she bends, the higher she is held.)
                if (d <= arm * 0.95f)
                {
                    best = e;
                    reached = true;
                }
                else if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            eased = Mathf.Lerp(eased, best, 1f - Mathf.Exp(-dt / 0.5f));
            return eased;
        }
    }
}
