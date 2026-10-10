using System;
using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;

namespace CMAScenes
{
    public sealed partial class ShakeController
    {
        // (Ride.cs clears these when a ride begins.) How far his hips are raised above where his buttocks alone would rest,
        // for what is under his back or his legs (worked out afresh every frame from what is under him, so it goes down
        // again as soon as that is gone); when the line about what he lies against is written next; when, and whether, the
        // line about what he lies on has been written; and how many times it was written again for a change of it.
        private float m_heroLift, m_heroLiftLogAt, m_heroBaseAt;
        private bool m_heroBaseSet;
        private int m_heroSunkLogs;

        // His body as a few capsules along his bones, where they lie this frame. Her hips, bottom and thighs are kept out
        // of them, so the two bodies do not pass through each other.
        private sealed class HeroSeg
        {
            public Transform a, b;
            public float radius;
            public Vector3 pa, pb;
        }

        private readonly List<HeroSeg> m_heroSegs = new List<HeroSeg>();
        private Transform m_heroChest;
        private Transform m_heroThighL, m_heroThighR, m_heroKneeL, m_heroKneeR;
        private bool m_heroSolid;
        private float m_heroPush;
        private float m_pushVel;

        private void AddHeroSeg(Transform a, Transform b, float radius)
        {
            if (a != null && b != null)
            {
                m_heroSegs.Add(new HeroSeg { a = a, b = b, radius = radius });
            }
        }

        private void BuildHeroSegs(Character hero)
        {
            m_heroSegs.Clear();
            m_heroChest = null;
            m_heroThighL = m_heroThighR = m_heroKneeL = m_heroKneeR = null;
            Animator anim = hero.bodyAnimator;
            if (anim != null && anim.isHuman)
            {
                Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
                m_heroThighL = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                m_heroThighR = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                m_heroKneeL = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                m_heroKneeR = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                m_heroChest = anim.GetBoneTransform(HumanBodyBones.Chest);
                if (m_heroChest == null)
                {
                    m_heroChest = anim.GetBoneTransform(HumanBodyBones.Spine);
                }
                // The thickness of a body, from the line of its bones to its skin: she sits on him, so these are kept close.
                AddHeroSeg(hips, anim.GetBoneTransform(HumanBodyBones.Spine), 0.12f);
                AddHeroSeg(anim.GetBoneTransform(HumanBodyBones.Spine), anim.GetBoneTransform(HumanBodyBones.Chest), 0.12f);
                AddHeroSeg(anim.GetBoneTransform(HumanBodyBones.Chest), anim.GetBoneTransform(HumanBodyBones.Neck), 0.13f);
                AddHeroSeg(anim.GetBoneTransform(HumanBodyBones.Neck), head, 0.10f);
                AddHeroSeg(hips, anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg), 0.09f);
                AddHeroSeg(hips, anim.GetBoneTransform(HumanBodyBones.RightUpperLeg), 0.09f);
                AddHeroSeg(anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg), anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg), 0.08f);
                AddHeroSeg(anim.GetBoneTransform(HumanBodyBones.RightUpperLeg), anim.GetBoneTransform(HumanBodyBones.RightLowerLeg), 0.08f);
                AddHeroSeg(anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg), anim.GetBoneTransform(HumanBodyBones.LeftFoot), 0.06f);
                AddHeroSeg(anim.GetBoneTransform(HumanBodyBones.RightLowerLeg), anim.GetBoneTransform(HumanBodyBones.RightFoot), 0.06f);
            }
            if (m_heroSegs.Count == 0)
            {
                AddHeroSeg(m_heroPivot, m_heroHead, 0.12f);
            }
        }

        // The height of the top of his body above a point, or minus infinity when nothing of him is under it.
        private float HeroTopAt(Vector3 p)
        {
            float best = float.NegativeInfinity;
            if (!m_heroSolid)
            {
                return best;
            }
            Vector2 q = new Vector2(p.x, p.z);
            foreach (HeroSeg s in m_heroSegs)
            {
                Vector2 a = new Vector2(s.pa.x, s.pa.z);
                Vector2 ab = new Vector2(s.pb.x, s.pb.z) - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2) : 0f;
                float d = (q - (a + ab * t)).magnitude;
                if (d >= s.radius)
                {
                    continue;
                }
                best = Mathf.Max(best, Mathf.Lerp(s.pa.y, s.pb.y, t) + Mathf.Sqrt(s.radius * s.radius - d * d));
            }
            return best;
        }

        // How far her back has been cut back so that her head, chest and shoulders do not go into his body, and since when
        // that was told in the log.
        private float m_bendCut, m_bendCutLogAt;

        // At the end of the frame, once she is posed: how far her head, her chest and her shoulders have sunk into his body
        // (his is a set of balls along his bones: HeroTopAt), and from that how far less she is bent next frame. A closed
        // loop: bending over him for her hands to reach (the pose "Over him", hands on his chest, hands aside) took her head
        // into his head and her chest into his, which nothing checked.
        private void HeadClearance(Woman w, float dt)
        {
            if (!m_heroSolid || w.sHead == null || w.sSp2 == null)
            {
                m_bendCut = Mathf.MoveTowards(m_bendCut, 0f, 40f * dt);
                return;
            }
            float sink = 0f;
            // (Where they will be drawn: where they are put, less what was put in to take up the game's lift.)
            Vector3 neck = w.sSp2.t.position;
            Vector3 head = w.sHead.t.position;
            Vector3 up = head - neck;
            up = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
            // (Her head, its middle a little above its bone, and in front of it where her face is: balls the size of a head.)
            Vector3 centre = head + up * 0.07f;
            float top = HeroTopAt(centre);
            if (!float.IsNegativeInfinity(top))
            {
                sink = Mathf.Max(sink, top + 0.105f - centre.y);
            }
            top = HeroTopAt(neck);
            if (!float.IsNegativeInfinity(top))
            {
                // (Her chest: the ball of her upper back, a little lower than the bone.)
                sink = Mathf.Max(sink, top + 0.11f - (neck.y - 0.04f));
            }
            for (int i = 0; i < 2; i++)
            {
                if (w.arms[i] == null)
                {
                    continue;
                }
                Vector3 sh = w.arms[i].upper.t.position;
                top = HeroTopAt(sh);
                if (!float.IsNegativeInfinity(top))
                {
                    sink = Mathf.Max(sink, top + 0.06f - sh.y);
                }
            }
            if (sink > 0.002f)
            {
                m_bendCut = Mathf.Clamp(m_bendCut + Mathf.Min(sink * 250f, 12f) * Mathf.Min(1f, dt * 12f), 0f, 70f);
                if (m_bendCut > 3f && Time.unscaledTime >= m_bendCutLogAt)
                {
                    m_bendCutLogAt = Time.unscaledTime + 8f;
                    Plugin.ModLog.LogInfo($"Ride: her head and chest were going into him by {sink * 100f:F1} cm: she is bent {m_bendCut:F0} deg less (pose {Plugin.RidePoseType.Value}).");
                }
            }
            else if (sink < -0.03f)
            {
                m_bendCut = Mathf.MoveTowards(m_bendCut, 0f, 12f * dt);
            }
        }

        // How far a ball of her body has sunk into his.
        private float SinkOf(Vector3 centre, float radius)
        {
            float top = HeroTopAt(centre);
            // Soft bodies give a little: she may sink into him by the amount the setting allows.
            return float.IsNegativeInfinity(top) ? 0f : Mathf.Max(0f, top + 0.005f + radius - centre.y - Plugin.RideSink.Value);
        }

        // How far her hips have to be lifted, with the offset they are about to be given, for her hips, bottom and thighs
        // to rest on him instead of going into him.
        internal float HeroPushFor(Woman w, Vector3 offset, Vector3 right, Vector3 fwd)
        {
            float want = 0f;
            if (m_heroSolid)
            {
                Vector3 hip = w.hip.position + offset;
                // Her body is soft: the balls that stand for it are small, so that she settles onto him rather than hovering.
                want = Mathf.Max(want, SinkOf(hip, 0.09f));
                Vector3 bottom = hip - fwd * 0.09f - Vector3.up * 0.03f;
                want = Mathf.Max(want, SinkOf(bottom + right * 0.1f, 0.07f));
                want = Mathf.Max(want, SinkOf(bottom - right * 0.1f, 0.07f));
                for (int i = 0; i < 2; i++)
                {
                    want = Mathf.Max(want, SinkOf(w.legs[i].upper.t.position + offset, 0.085f));
                }
            }
            // The lift follows what is needed the way a well damped spring would: quickly, but without a step when what is under
            // her changes (the edge of his body, his lying down) and without ringing after it.
            float dt = Mathf.Clamp(Time.deltaTime, 0.004f, 0.05f);
            float zeta = 1f;
            float omega = 2f * Mathf.PI * 7f;
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.004f), 1, 14);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                m_pushVel += (omega * omega * (want - m_heroPush) - 2f * zeta * omega * m_pushVel) * h;
                m_heroPush += m_pushVel * h;
            }
            if (m_heroPush < 0f)
            {
                m_heroPush = 0f;
                m_pushVel = Mathf.Max(0f, m_pushVel);
            }
            return m_heroPush;
        }

        // How much air there is between her crotch and his body below it (0 or less: she is down on him). Infinity when
        // nothing of him is under her.
        internal float HeroGapUnder(Woman w, Vector3 offset)
        {
            if (!m_heroSolid)
            {
                return float.PositiveInfinity;
            }
            Vector3 hip = w.hip.position + offset;
            float top = HeroTopAt(hip);
            return float.IsNegativeInfinity(top) ? float.PositiveInfinity : hip.y - 0.09f - top;
        }

        // ---- what he lies on ----
        //
        // He was laid at one height for the whole ride, worked out from his bones as he stood (the lowest of them, turned
        // onto his back, a hand above the floor of the sex), and lifted on top of that whenever his hips were found lower
        // than that. They were found lower by his own thrust: they were read before it was put on them in the frame, so
        // every thrust took the whole of him up a little more, and nothing ever let him down again (the head and chest
        // he was also measured by were lifted with him and so never said he was too high). In the log of the bench he
        // hung 9 cm higher after a minute of her riding, and she with him.
        //
        // Now he lies on what is under him. Under each place of his body that rests on something (his buttocks, his
        // shoulder blades, the back of his head, his thighs, his calves, his heels) a ray goes down, at where that place
        // will be once he lies, and he is laid so that his skin there lies on what it finds: his hips at the height his
        // buttocks rest at, his lower back bent so that his shoulder blades are down too, each leg turned at the hip until
        // the first of thigh, calf and heel is down, and bent at the knee where nothing is under his calf (past the end of
        // a bench his lower legs hang). It is worked out afresh every frame from what is under him, from the way he stood
        // when he was taken up and from how thick his body is under each place, never from where he was drawn the frame
        // before, so there is nothing to creep. How thick he is there is what his own colliders say (his physical body,
        // carried by his skeleton while he lies), and a guess until they have been measured.
        //
        // His thrust (HeroExtra.cs) lifts his hips off it: his lower back arches by what keeps his shoulder blades down
        // and his upper back is turned back by as much (his shoulders, his neck and his head stay as they were), and his
        // thighs turn at the hip by what keeps his heels and calves down.
        private sealed class HeroRest
        {
            public string name;
            // The place: on a bone, or a part of the way from it to the next one.
            public Transform a, b;
            public float t;
            // Where it was as he stood, when he was taken up.
            public Vector3 home;
            // How far his skin is under it as he lies (behind it as he stands), in metres: a guess, then what his colliders
            // say, never outside what a body can be.
            public float depth, lo, hi;
            public bool measured;
            // The body of his puppet whose colliders are his skin there (found by the bone it is carried by).
            public Transform bone, boneElse;
            public RootMotion.Dynamics.Muscle muscle;
            // The top of what is under it (eased), whether a ray found it, how far his skin was above it as he was drawn
            // at the end of the frame, and what it was when the log last said what he lies on.
            public float surface, gap, surfaceLogged;
            public bool surfaceHas, rayHit;

            public Vector3 Point => b == null ? a.position : Vector3.Lerp(a.position, b.position, t);
        }

        private const int RestPelvis = 0, RestBack = 1, RestHead = 2, RestThigh = 3;
        // How far each joint may be turned toward what he lies on, in degrees (below zero: away from it). His lower back
        // bends little either way; it arches further for the thrust; a hip extends little and flexes more; a knee bends.
        private const float WaistMin = -10f, WaistMax = 20f, ArchMax = 28f, ThighMin = -15f, ThighMax = 15f, KneeMax = 80f;

        private readonly List<HeroRest> m_rest = new List<HeroRest>();
        private Transform m_restWaist, m_restUpper, m_restNeck;
        private Vector3 m_restWaistHome, m_restUpperHome, m_restNeckHome;
        private readonly Vector3[] m_restHipHome = new Vector3[2], m_restKneeHome = new Vector3[2];
        private bool m_restReady, m_restMuscles;
        // His front and the way to his head in the turning of his root (lying: up, and along what he lies on).
        private Vector3 m_restFrontLocal = Vector3.forward, m_restHeadLocal = Vector3.up;
        private bool m_restFrameSet;
        // The plan of this frame: the height of his hips (their bone), and how far each joint is turned toward what he lies
        // on: his lower back (its own, and its arch for the thrust), his neck, each thigh (its own, and for the thrust), each
        // knee.
        private float m_restY;
        private bool m_restYValid, m_bendHas;
        private float m_bendWaist, m_bendArch, m_bendNeck;
        private readonly float[] m_bendThigh = new float[2], m_bendThighLift = new float[2], m_bendKnee = new float[2];
        // The lift of his hips put on him this frame (what the thrust asks for, no more than his back lets him), and that
        // lift as it is in the world.
        private float m_thrustNow;
        private Vector3 m_thrustWorld;
        private int m_restRayNext;
        private float m_restMeasureAt;
        private bool m_restMeasureTold, m_gapTold;
        private readonly Dictionary<IntPtr, bool> m_restSkip = new Dictionary<IntPtr, bool>();

        private void AddRest(string name, Transform a, Transform b, float t, float guess, float lo, float hi, Transform boneElse = null)
        {
            var r = new HeroRest { name = name, a = a, b = b, t = t, depth = guess, lo = lo, hi = hi, bone = a, boneElse = boneElse };
            r.home = r.Point;
            m_rest.Add(r);
        }

        private void RestForget()
        {
            m_rest.Clear();
            m_restReady = false;
            m_restMuscles = false;
            m_restFrameSet = false;
            m_restYValid = false;
            m_bendHas = false;
            m_restSkip.Clear();
            m_restMeasureTold = false;
            m_restMeasureAt = 0f;
            m_restRayNext = 0;
            m_thrustNow = 0f;
            m_thrustWorld = Vector3.zero;
            m_restWaist = m_restUpper = m_restNeck = null;
            m_heroLift = 0f;
        }

        // When he is taken up, as he stands: the places of him that rest on something, and the joints that are turned for
        // them to lie on it.
        private void BuildHeroRest(Character hero)
        {
            RestForget();
            Animator anim = hero.bodyAnimator;
            if (anim == null || !anim.isHuman || m_heroPivot == null)
            {
                Plugin.ModLog.LogInfo("Ride: his skeleton is not a humanoid one: he is laid as one piece, his hips a hand above what he lies on.");
                return;
            }
            Transform waist = anim.GetBoneTransform(HumanBodyBones.Spine);
            Transform chest = anim.GetBoneTransform(HumanBodyBones.Chest);
            Transform upper = anim.GetBoneTransform(HumanBodyBones.UpperChest);
            if (upper == null)
            {
                upper = chest;
            }
            Transform neck = anim.GetBoneTransform(HumanBodyBones.Neck);
            Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
            Transform footL = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform footR = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            if (waist == null || upper == null || upper == waist || neck == null || head == null || footL == null || footR == null
                || m_heroThighL == null || m_heroThighR == null || m_heroKneeL == null || m_heroKneeR == null
                || !waist.IsChildOf(m_heroPivot) || !upper.IsChildOf(waist) || !neck.IsChildOf(upper) || !m_heroThighL.IsChildOf(m_heroPivot) || !m_heroThighR.IsChildOf(m_heroPivot))
            {
                Plugin.ModLog.LogInfo("Ride: the bones of his back, neck or legs were not all found where they should be: he is laid as one piece, his hips a hand above what he lies on.");
                return;
            }
            m_restWaist = waist;
            m_restUpper = upper;
            m_restNeck = neck;
            m_restWaistHome = waist.position;
            m_restUpperHome = upper.position;
            m_restNeckHome = neck.position;
            m_restHipHome[0] = m_heroThighL.position;
            m_restHipHome[1] = m_heroThighR.position;
            m_restKneeHome[0] = m_heroKneeL.position;
            m_restKneeHome[1] = m_heroKneeR.position;
            // (In this order: RestPelvis, RestBack, RestHead, then thighs, calves and heels, left before right.)
            AddRest("buttocks", m_heroPivot, null, 0f, 0.115f, 0.06f, 0.2f);
            AddRest("shoulder blades", upper, null, 0f, 0.09f, 0.04f, 0.18f, chest);
            AddRest("head", head, null, 0f, 0.09f, 0.05f, 0.15f);
            AddRest("left thigh", m_heroThighL, m_heroKneeL, 0.55f, 0.075f, 0.035f, 0.14f);
            AddRest("right thigh", m_heroThighR, m_heroKneeR, 0.55f, 0.075f, 0.035f, 0.14f);
            AddRest("left calf", m_heroKneeL, footL, 0.3f, 0.055f, 0.025f, 0.11f);
            AddRest("right calf", m_heroKneeR, footR, 0.3f, 0.055f, 0.025f, 0.11f);
            AddRest("left heel", footL, null, 0f, 0.045f, 0.02f, 0.1f);
            AddRest("right heel", footR, null, 0f, 0.045f, 0.02f, 0.1f);
            m_restReady = true;
        }

        // How far above what he lies on his skin rests: the setting. (Below -5 cm it is a value set for the placing before
        // this one, which laid his bones, not his skin, a hand above the floor: he floated by some 10 cm, and the setting
        // was taken down by as much. His skin is what lies on it now, so that value is read as resting on it.)
        private float HeroGap()
        {
            float set = Plugin.RideHeroClearance.Value;
            bool legacy = set < -0.05f;
            float gap = legacy ? 0.003f : Mathf.Clamp(set, -0.03f, 0.1f);
            if (!m_gapTold)
            {
                m_gapTold = true;
                Plugin.ModLog.LogInfo(legacy
                    ? $"Ride: HeroClearance is {set * 100f:F1} cm, a value set against the floating of the placing before this one; his skin is laid {gap * 100f:F1} cm above what he lies on instead."
                    : $"Ride: his skin is laid {gap * 100f:F1} cm above what he lies on (HeroClearance).");
            }
            return gap;
        }

        // (Not a part of a character or of its puppet, not a trigger, not something loose that physics moves about: the
        // bodies of both of them are carried, kinematic, while they are posed, and a ray must not take them for a bench.)
        private bool RestSkips(Collider c)
        {
            if (c == null)
            {
                return true;
            }
            if (m_restSkip.TryGetValue(c.Pointer, out bool skip))
            {
                return skip;
            }
            try
            {
                Rigidbody body = c.attachedRigidbody;
                skip = c.isTrigger || (body != null && !body.isKinematic) || c.GetComponentInParent<Character>() != null
                    || c.GetComponentInParent<RootMotion.Dynamics.PuppetMaster>() != null;
            }
            catch (Exception)
            {
                skip = true;
            }
            m_restSkip[c.Pointer] = skip;
            return skip;
        }

        // The top of what is under a spot, by a ray from a little above the floor of the sex: the highest that faces up
        // and is no higher than a hand above that floor (a table's edge or her body over him is not what he lies on).
        private bool RestSurfaceAt(Vector3 at, out float y)
        {
            y = 0f;
            try
            {
                float highest = m_rideFloorY + 0.25f;
                var hits = Physics.RaycastAll(new Vector3(at.x, m_rideFloorY + 0.45f, at.z), Vector3.down, 1.9f, ~0, QueryTriggerInteraction.Ignore);
                bool any = false;
                for (int i = 0; hits != null && i < hits.Length; i++)
                {
                    if (hits[i].normal.y < 0.5f || hits[i].point.y > highest || RestSkips(hits[i].collider))
                    {
                        continue;
                    }
                    if (!any || hits[i].point.y > y)
                    {
                        y = hits[i].point.y;
                        any = true;
                    }
                }
                return any;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // A point at "along" ahead of a joint and "h" above it, turned about the joint by so many degrees down.
        private static void TurnBy(ref float along, ref float h, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            float na = along * c + h * s;
            h = h * c - along * s;
            along = na;
        }

        // How far (degrees, down) a point at "along" ahead of a joint and "h" above it has to be turned about the joint
        // to be "want" above it.
        private static float TurnDown(float along, float h, float want)
        {
            float r = Mathf.Sqrt(along * along + h * h);
            if (r < 1e-3f)
            {
                return 0f;
            }
            return (Mathf.Atan2(h, along) - Mathf.Asin(Mathf.Clamp(want / r, -1f, 1f))) * Mathf.Rad2Deg;
        }

        // Every frame, before he is laid: what is under him, and from that how high his hips go and how far his back,
        // his neck, his legs and his knees are turned to lie on it, with the lift his thrust asks for (m_thrust).
        private void HeroRestStep(float weight)
        {
            float gap = HeroGap();
            float dt = Mathf.Clamp(Time.deltaTime, 0.004f, 0.1f);
            float lift = Mathf.Clamp(m_thrust, 0f, ThrustCap);
            HeroGoal(out Vector3 goal, out Quaternion turn);
            if (!m_restReady || !m_restFrameSet)
            {
                // (No bones to bend: his hips a hand above the floor of the sex, as one piece; and no thrust, which with
                // nothing to keep his shoulders and heels down would lift the whole of him.)
                m_restY = m_rideFloorY + gap + 0.115f;
                m_restYValid = true;
                m_heroLift = 0f;
                m_thrustNow = 0f;
                m_bendHas = false;
                return;
            }
            Vector3 headDir = Vector3.ProjectOnPlane(m_heroFwd, Vector3.up);
            headDir = headDir.sqrMagnitude > 1e-6f ? headDir.normalized : Vector3.forward;
            Vector3 hips = m_heroHomeHips;

            // What is under each place, by a ray where it will be once he lies: three places a frame, all of them at first.
            int count = m_bendHas ? 3 : m_rest.Count;
            for (int k = 0; k < count; k++)
            {
                int i = (m_restRayNext + k) % m_rest.Count;
                HeroRest r = m_rest[i];
                bool hit = RestSurfaceAt(goal + turn * (r.home - hips), out float y0);
                if (!hit)
                {
                    y0 = m_rideFloorY;
                }
                // (Eased: past an edge it goes down in a few frames, not at once.)
                r.surface = r.surfaceHas ? Mathf.Lerp(r.surface, y0, 0.5f) : y0;
                r.surfaceHas = true;
                r.rayHit = hit;
            }
            m_restRayNext = (m_restRayNext + count) % m_rest.Count;

            HeroRest pel = m_rest[RestPelvis], bak = m_rest[RestBack], hed = m_rest[RestHead];
            Vector3 relW = turn * (m_restWaistHome - hips);
            Vector3 relU = turn * (m_restUpperHome - hips);
            Vector3 relN = turn * (m_restNeckHome - hips);
            Vector3 relP = turn * (pel.home - hips);
            Vector3 relB = turn * (bak.home - hips);
            Vector3 relH = turn * (hed.home - hips);

            // His hips: his buttocks on what is under them...
            float yRest = pel.surface + gap + pel.depth - relP.y;
            float y = yRest;
            // ...unless his back or a leg, turned up as far as it turns, would still be in what is under it: then they go up
            // by as much (a pillow under his head, a step under his heels).
            float aB = Vector3.Dot(relB - relW, headDir), hB = (relB - relW).y;
            {
                float a = aB, h = hB;
                TurnBy(ref a, ref h, WaistMin);
                y = Mathf.Max(y, bak.surface + gap + bak.depth - relW.y - h);
            }
            for (int side = 0; side < 2; side++)
            {
                Vector3 relT = turn * (m_restHipHome[side] - hips);
                for (int c = 0; c < 3; c++)
                {
                    HeroRest r = m_rest[RestThigh + 2 * c + side];
                    Vector3 v = turn * (r.home - hips) - relT;
                    float a = Vector3.Dot(v, -headDir), h = v.y;
                    TurnBy(ref a, ref h, ThighMin);
                    y = Mathf.Max(y, r.surface + gap + r.depth - relT.y - h);
                }
            }
            m_heroLift = Mathf.Clamp(y - yRest, 0f, 0.12f);
            y = yRest + m_heroLift;
            m_restY = y;
            m_restYValid = true;

            // His lower back: bent until his shoulder blades are down.
            float waist = Mathf.Clamp(TurnDown(aB, hB, bak.surface + gap + bak.depth - (y + relW.y)), WaistMin, WaistMax);
            // His head keeps the way it looks (the view is his): his neck is turned back by what turned his back. Only if it
            // would then be in what is under it is it lifted, by turning his neck up.
            float aN = Vector3.Dot(relN - relW, headDir), hN = (relN - relW).y;
            TurnBy(ref aN, ref hN, waist);
            float aH = Vector3.Dot(relH - relN, headDir), hH = (relH - relN).y;
            float headUp = Mathf.Clamp(Mathf.Min(0f, TurnDown(aH, hH, hed.surface + gap + hed.depth - (y + relW.y + hN))), -30f, 0f);
            float neck = -waist + headUp;

            // The thrust: his hips go up by the lift, his lower back arches by what brings his upper back (and his shoulder
            // blades on it) back down where it lay, and his upper back is turned back by as much. No more lift than an arch
            // his back has keeps them down.
            float aU = Vector3.Dot(relU - relW, headDir), hU = (relU - relW).y;
            TurnBy(ref aU, ref hU, waist);
            float arch = 0f;
            if (lift > 0.0005f)
            {
                arch = Mathf.Clamp(TurnDown(aU, hU, hU - lift), 0f, ArchMax);
                float a2 = aU, h2 = hU;
                TurnBy(ref a2, ref h2, arch);
                lift = Mathf.Min(lift, Mathf.Max(0f, hU - h2) + 0.002f);
            }
            else
            {
                lift = 0f;
            }
            m_thrustNow = lift;

            float kBend = m_bendHas ? 1f - Mathf.Exp(-dt / 0.08f) : 1f;
            float kKnee = m_bendHas ? 1f - Mathf.Exp(-dt / 0.2f) : 1f;
            for (int side = 0; side < 2; side++)
            {
                Vector3 relT = turn * (m_restHipHome[side] - hips);
                Vector3 relK = turn * (m_restKneeHome[side] - hips);
                // His leg turned at the hip until the first of thigh, calf and heel is down; lifted by the thrust, turned on
                // by what keeps it there.
                float rest = float.MaxValue, lifted = float.MaxValue;
                for (int c = 0; c < 3; c++)
                {
                    HeroRest r = m_rest[RestThigh + 2 * c + side];
                    Vector3 v = turn * (r.home - hips) - relT;
                    float a = Vector3.Dot(v, -headDir), h = v.y;
                    float want = r.surface + gap + r.depth - (y + relT.y);
                    rest = Mathf.Min(rest, TurnDown(a, h, want));
                    lifted = Mathf.Min(lifted, TurnDown(a, h, want - lift));
                }
                float thigh = Mathf.Clamp(rest, ThighMin, ThighMax);
                float thighLift = Mathf.Clamp(lifted, ThighMin, ThighMax) - thigh;
                // His lower leg: where nothing is under his calf (past the end of what he lies on), it hangs from the knee,
                // down to what is under it or as far as a knee bends. Where his calf is on it, it stays straight.
                Vector3 vK = relK - relT;
                float aK = Vector3.Dot(vK, -headDir), hK = vK.y;
                TurnBy(ref aK, ref hK, thigh);
                float knee = float.MaxValue;
                for (int c = 1; c < 3; c++)
                {
                    HeroRest r = m_rest[RestThigh + 2 * c + side];
                    Vector3 v = turn * (r.home - hips) - relT;
                    float a = Vector3.Dot(v, -headDir), h = v.y;
                    TurnBy(ref a, ref h, thigh);
                    knee = Mathf.Min(knee, TurnDown(a - aK, h - hK, r.surface + gap + r.depth - (y + relT.y + hK)));
                }
                knee = Mathf.Clamp(knee, 0f, KneeMax);
                m_bendThigh[side] = Mathf.Lerp(m_bendThigh[side], thigh, kBend);
                m_bendThighLift[side] = thighLift;
                m_bendKnee[side] = Mathf.Lerp(m_bendKnee[side], knee, kKnee);
            }
            m_bendWaist = Mathf.Lerp(m_bendWaist, waist, kBend);
            m_bendNeck = Mathf.Lerp(m_bendNeck, neck, kBend);
            m_bendArch = arch;
            m_bendHas = true;
        }

        // At the end of the frame, once his physical body has been put on his skeleton: how thick he is under each place,
        // by his own colliders (the farthest point of them behind the place, the way his back faces). A few times a second;
        // a body of his that the physics has just now (his head while his hand is in use) is left for later.
        private void HeroRestMeasure()
        {
            if (!m_restReady || !m_restFrameSet || m_heroPuppet == null || m_heroRoot == null || Time.unscaledTime < m_restMeasureAt)
            {
                return;
            }
            m_restMeasureAt = Time.unscaledTime + 0.3f;
            try
            {
                if (!m_restMuscles)
                {
                    m_restMuscles = true;
                    var muscles = m_heroPuppet.muscles;
                    foreach (HeroRest r in m_rest)
                    {
                        r.muscle = null;
                        for (int pass = 0; pass < 2 && r.muscle == null; pass++)
                        {
                            Transform want = pass == 0 ? r.bone : r.boneElse;
                            for (int i = 0; want != null && muscles != null && i < muscles.Length; i++)
                            {
                                var mu = muscles[i];
                                if (mu != null && mu.target != null && mu.target == want)
                                {
                                    r.muscle = mu;
                                    break;
                                }
                            }
                        }
                    }
                }
                Vector3 back = -(m_heroRoot.rotation * m_restFrontLocal);
                foreach (HeroRest r in m_rest)
                {
                    Rigidbody body = r.muscle != null ? r.muscle.rigidbody : null;
                    if (body == null || !body.isKinematic)
                    {
                        continue;
                    }
                    var cols = r.muscle.colliders;
                    Vector3 p = r.Point;
                    Vector3 far = p + back * 1.5f;
                    float most = float.NegativeInfinity;
                    for (int i = 0; cols != null && i < cols.Length; i++)
                    {
                        Collider c = cols[i];
                        if (c == null || !c.enabled || c.isTrigger)
                        {
                            continue;
                        }
                        Vector3 s = c.ClosestPoint(far);
                        // (A point given back as it was asked: not a shape that can be asked so.)
                        if ((s - far).sqrMagnitude < 1e-6f)
                        {
                            continue;
                        }
                        most = Mathf.Max(most, Vector3.Dot(s - p, back));
                    }
                    if (most >= r.lo && most <= r.hi)
                    {
                        r.depth = r.measured ? Mathf.Lerp(r.depth, most, 0.3f) : most;
                        r.measured = true;
                    }
                }
                if (!m_restMeasureTold && m_heroW >= 0.99f)
                {
                    m_restMeasureTold = true;
                    var sb = new System.Text.StringBuilder();
                    foreach (HeroRest r in m_rest)
                    {
                        sb.Append(sb.Length > 0 ? ", " : "").Append(r.name).Append(' ').Append((r.depth * 100f).ToString("F1")).Append(r.measured ? "" : " (guessed: " + (r.muscle == null ? "no body of his puppet there" : "its colliders said nothing a body can be") + ")");
                    }
                    Plugin.ModLog.LogInfo($"Ride: how thick he is under the places he rests on, in cm, by his own colliders: {sb}.");
                }
            }
            catch (Exception ex)
            {
                m_restMuscles = true;
                m_restMeasureAt = Time.unscaledTime + 30f;
                Plugin.ModLog.LogWarning("Ride: his colliders could not be measured (" + ex.Message + "); how thick he is stays guessed.");
            }
        }

        // At the end of the frame, as he is drawn: how far his skin is above what is under it at each place, and now and
        // then a line of it in the log (with what he lies on when that changes), so that it can be seen how he lies.
        private void HeroRestReport()
        {
            if (!m_restReady || !m_restFrameSet || !m_bendHas || m_heroPivot == null || m_heroRoot == null || m_heroW < 0.95f)
            {
                return;
            }
            float now = Time.unscaledTime;
            Vector3 back = -(m_heroRoot.rotation * m_restFrontLocal);
            foreach (HeroRest r in m_rest)
            {
                r.gap = (r.Point + back * r.depth).y - r.surface;
            }
            string Cm(float v) => (v * 100f).ToString("F1");
            HeroRest pel = m_rest[RestPelvis];
            if (m_heroBaseAt <= 0f)
            {
                m_heroBaseAt = now + 1.5f;
            }
            else if (now >= m_heroBaseAt && (!m_heroBaseSet || (m_heroSunkLogs < 12 && RestChanged())))
            {
                if (m_heroBaseSet)
                {
                    m_heroSunkLogs++;
                }
                m_heroBaseSet = true;
                m_heroBaseAt = now + 3f;
                var sb = new System.Text.StringBuilder();
                foreach (HeroRest r in m_rest)
                {
                    float below = m_rideFloorY - r.surface;
                    sb.Append(sb.Length > 0 ? ", " : "").Append(r.name).Append(' ')
                        .Append(!r.rayHit ? "nothing found (taken as the floor of the sex)" : Mathf.Abs(below) < 0.02f ? "on it" : below > 0f ? Cm(below) + " cm below it" : Cm(-below) + " cm above it");
                    r.surfaceLogged = r.surface;
                }
                Plugin.ModLog.LogInfo($"Ride: hero lies on what is under him (the floor of the sex at {m_rideFloorY:F2}{(PlaceActive ? ", the top of '" + m_place.name + "'" : "")}): {sb}.");
            }
            if (now >= m_heroLiftLogAt)
            {
                m_heroLiftLogAt = now + 10f;
                float lowest = float.MaxValue;
                foreach (Transform b in m_heroBones)
                {
                    if (b != null)
                    {
                        lowest = Mathf.Min(lowest, b.position.y);
                    }
                }
                HeroRest hed = m_rest[RestHead];
                float head = m_heroHead != null ? m_heroHead.position.y - hed.surface : 0f;
                var gaps = new System.Text.StringBuilder();
                foreach (HeroRest r in m_rest)
                {
                    gaps.Append(gaps.Length > 0 ? ", " : "").Append(r.name).Append(' ').Append(Cm(r.gap));
                }
                Plugin.ModLog.LogInfo($"Ride: hero against the floor: hips {Cm(m_heroPivot.position.y - pel.surface)} cm above what is under them (put {Cm(m_restY - pel.surface)}, lifted {Cm(m_thrustNow)} by his thrust), lowest bone {Cm(lowest - pel.surface)} cm, head {Cm(head)} cm; his skin above what is under it, in cm: {gaps}; raised by {Cm(m_heroLift)} cm for what is under his back or legs; bent, in degrees: lower back {m_bendWaist:F0} (arch {m_bendArch:F0}), neck {m_bendNeck:F0}, thighs {m_bendThigh[0] + m_bendThighLift[0]:F0}/{m_bendThigh[1] + m_bendThighLift[1]:F0}, knees {m_bendKnee[0]:F0}/{m_bendKnee[1]:F0}; wheel {Cm(m_thrustGoal)}; pose {Plugin.RidePoseType.Value}.");
            }
        }

        // (Has what is under him changed since it was last told, by more than a few centimetres anywhere?)
        private bool RestChanged()
        {
            foreach (HeroRest r in m_rest)
            {
                if (Mathf.Abs(r.surface - r.surfaceLogged) > 0.05f)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
