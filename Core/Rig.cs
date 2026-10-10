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
    // A rotation the game animates every frame, that we modify on top and put back first thing the next frame.
    internal sealed class RotSlot
    {
        public readonly Transform t;
        private bool m_has;
        private Quaternion m_last;
        private Quaternion m_base;

        public RotSlot(Transform t)
        {
            this.t = t;
        }

        public void Begin()
        {
            Quaternion cur = t.localRotation;
            if (m_has && Quaternion.Angle(cur, m_last) < 0.01f)
            {
                cur = m_base;
            }
            m_base = cur;
            t.localRotation = cur;
        }

        public void Commit()
        {
            m_last = t.localRotation;
            m_has = true;
        }

        public void Release()
        {
            m_has = false;
        }

        // How the bone is turned on its parent by the game's own animation this frame (before anything of ours).
        public Quaternion BaseLocal => m_base;
    }

    internal sealed class PosSlot
    {
        public readonly Transform t;
        private bool m_has;
        private Vector3 m_last;
        private Vector3 m_base;
        // (Her hips, carried to a place of the room for the sex: Places.cs. What is offset afterwards is offset from there.)
        private Vector3 m_carry;

        public PosSlot(Transform t)
        {
            this.t = t;
        }

        public Vector3 Begin()
        {
            Vector3 cur = t.localPosition;
            if (m_has && (cur - m_last).sqrMagnitude < 1e-12f)
            {
                cur = m_base;
            }
            m_base = cur;
            m_carry = Vector3.zero;
            t.localPosition = cur;
            return cur;
        }

        // The bone is carried by that much from where the game has it, whole: everything that is read from it and from
        // what hangs on it after this is read from there, and Offset is counted from there.
        public void Carry(Vector3 worldBy)
        {
            m_carry = t.parent != null ? t.parent.InverseTransformVector(worldBy) : worldBy;
            t.localPosition = m_base + m_carry;
            m_last = t.localPosition;
            m_has = true;
        }

        public void Offset(Vector3 worldOffset)
        {
            Vector3 local = t.parent != null ? t.parent.InverseTransformVector(worldOffset) : worldOffset;
            t.localPosition = m_base + m_carry + local;
            m_last = t.localPosition;
            m_has = true;
        }

        // The bone was moved again after it was put (her hips, put back once the game's inverse kinematics have run:
        // PoseFix.cs): where it is now is what it is taken to have been left at.
        public void Seal()
        {
            if (m_has)
            {
                m_last = t.localPosition;
            }
        }

        public void Release()
        {
            m_has = false;
        }
    }

    // What she is doing right now: how much of each move, and where she is in the beat.
    internal sealed class Motion
    {
        public float phase;
        public float bounce = 1f;
        public float sway;
        public float circle;
        public float circleDir = 1f;
        public float leanExtra;
        public float intensity = 1f;
        // Natural motion: how far the weight is on one leg (-1..1) and a slow drift of the depth of each bounce.
        public float weight;
        public float jitter = 1f;
        // What the preset does to the speed and to the depth of everything.
        public float speed = 1f;
        public float amp = 1f;
    }

    // Soft tissue: a mass on a spring that is shaken by the body's own acceleration, so it lags behind each bounce and
    // keeps wobbling for a moment after it, a little differently on each side.
    internal sealed class Jiggle
    {
        public Vector3 x, v;
        public readonly float freq, damping;

        public Jiggle(float freq, float damping)
        {
            this.freq = freq;
            this.damping = damping;
        }

        public void Reset()
        {
            x = Vector3.zero;
            v = Vector3.zero;
        }

        public void Step(Vector3 shake, float gain, float dt, float maxOffset = 0.06f)
        {
            float k = (2f * Mathf.PI * freq) * (2f * Mathf.PI * freq);
            float c = 2f * damping * Mathf.Sqrt(k);
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.004f), 1, 14);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                v += (-k * x - c * v - shake * gain) * h;
                x += v * h;
            }
            float limit = Mathf.Max(0f, maxOffset);
            if (x.sqrMagnitude > limit * limit)
            {
                x = x.normalized * limit;
            }
        }
    }

    // How a face lies on the bone of its head, in that bone's own turning: which way it looks, which way the crown is,
    // and where the mouth is from the bone (in metres).
    internal sealed class HeadFrame
    {
        public Vector3 looks, crown, mouth, eyes;
        public bool byEyes;
        // The bones of the eyes, and which way each looks along its own axes (zero when that could not be told).
        public Transform eyeA, eyeB;
        public Vector3 eyeAxisA, eyeAxisB;

        // The one of a bone's own six axes that points nearest a direction, if any points near it at all.
        private static Vector3 AxisToward(Transform bone, Vector3 direction)
        {
            Vector3 local = Quaternion.Inverse(bone.rotation) * direction;
            Vector3 best = Vector3.zero;
            float most = 0.8f;
            foreach (Vector3 axis in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                float d = Vector3.Dot(axis, local);
                if (d > most)
                {
                    most = d;
                    best = axis;
                }
            }
            return best;
        }

        private static readonly System.Text.RegularExpressions.Regex NotAnEye = new System.Text.RegularExpressions.Regex("lash|brow|lid|tear|occl|socket|shadow", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // From the bones of the eyes when the head has them (the mouth is under their middle), else from the way the
        // body faces.
        public static HeadFrame Of(Transform head, Vector3 bodyForward)
        {
            var f = new HeadFrame();
            Quaternion inv = Quaternion.Inverse(head.rotation);
            Vector3 up = head.parent != null ? head.position - head.parent.position : Vector3.up;
            up = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(bodyForward, up);
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : bodyForward;
            Vector3 mouth = head.position + forward * 0.095f + up * 0.02f;
            Vector3 eyesAt = head.position + forward * 0.08f + up * 0.09f;
            try
            {
                var eyes = new System.Collections.Generic.List<Transform>();
                foreach (Transform t in head.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && t != head && t.name.IndexOf("eye", StringComparison.OrdinalIgnoreCase) >= 0 && !NotAnEye.IsMatch(t.name) && eyes.Count < 16)
                    {
                        eyes.Add(t);
                    }
                }
                for (int i = 0; i < eyes.Count && !f.byEyes; i++)
                {
                    for (int j = i + 1; j < eyes.Count; j++)
                    {
                        float apart = (eyes[i].position - eyes[j].position).magnitude;
                        if (apart > 0.04f && apart < 0.09f)
                        {
                            Vector3 mid = 0.5f * (eyes[i].position + eyes[j].position);
                            Vector3 across = (eyes[i].position - eyes[j].position).normalized;
                            Vector3 toEyes = Vector3.ProjectOnPlane(mid - head.position, up);
                            if (toEyes.magnitude > 0.03f)
                            {
                                forward = Vector3.ProjectOnPlane(toEyes, across).normalized;
                                up = Vector3.Cross(forward, across);
                                up = Vector3.Dot(up, mid - head.position) < 0f ? -up : up;
                                mouth = mid - up * 0.068f + forward * 0.012f;
                                eyesAt = mid;
                                f.byEyes = true;
                                f.eyeA = eyes[i];
                                f.eyeB = eyes[j];
                                f.eyeAxisA = AxisToward(eyes[i], forward);
                                f.eyeAxisB = AxisToward(eyes[j], forward);
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            f.looks = inv * forward;
            f.crown = inv * up;
            f.mouth = inv * (mouth - head.position);
            f.eyes = inv * (eyesAt - head.position);
            return f;
        }
    }

    internal sealed class Limb
    {
        public RotSlot upper, fore, end;
        public int side;
        // (Of a leg: the bone of her toes, looked for once.)
        public Transform toe;
        public bool toeSearched;
        // (Of an arm: how her hand lies on its bone, in that bone's own turning: which way her fingers point and which
        // way her palm faces. Found once from the bones of her fingers.)
        public Vector3 handFingers, handPalm;
        public bool handSearched, handOk;
        // (Of an arm: the axis her elbow bends about, in the turning of her upper arm. Found once, from her arm as the
        // game's animation has it bent.)
        public Vector3 hinge;
        public bool hingeOk;
        // (Of an arm, Wrist.cs: how far her elbow is swung round from where the pose would have it, to spare her wrist;
        // and the bones along her forearm that carry the turn of her hand, with how much of it each takes.)
        public float swivel;
        public bool swivelHas, twistSearched;
        // (How her hand lies in the game's own animation, told once in the log: Wrist.cs.)
        public bool wristSeen;
        public RotSlot[] twists;
        public float[] twistPart;
        public int twistLogs;
        // (Of an arm: the bones of four of its fingers, each from its root to its tip, found once; and how far its hand has
        // gone from where it was to be put to a place it reaches, because the place was out of its reach. Wrist.cs.)
        public RotSlot[][] fingers;
        public bool fingersSearched;
        public float reachFall;
        // (Whose limb it is, for the log: hers, or his when he holds her.)
        public string who = "her";
    }

    // A hair's anchor, kept rigidly attached to the head while we move the body, plus the game's own
    // followers and hair updaters that have to be run again once the body has been posed.
    internal sealed class HairRig
    {
        public Transform pivot;
        public GPUTools.Hair.Scripts.HairSettings settings;
        public List<Assets._ReusableScripts.Miscellaneous.BaseFolowTransform> followers = new List<Assets._ReusableScripts.Miscellaneous.BaseFolowTransform>();
        public List<Assets._ReusableScripts.Miscellaneous.MatrixFollowerBase> matrixFollowers = new List<Assets._ReusableScripts.Miscellaneous.MatrixFollowerBase>();
        public List<Assets._ReusableScripts.CuchiCuchi.Dependentes.Hair.HairUpdater> updaters = new List<Assets._ReusableScripts.CuchiCuchi.Dependentes.Hair.HairUpdater>();
        public Matrix4x4 rel;
        public bool has;
        public bool warned;
    }

    internal sealed class Woman
    {
        public Transform head;
        public readonly Dictionary<int, HairRig> hair = new Dictionary<int, HairRig>();
        public Character character;
        public long key;
        public string name;
        public Transform hip;
        public PosSlot sHip;
        // (Her hips' own turn: only ever turned about the upright, when she is carried to a place. Places.cs.)
        public RotSlot sHipRot;
        public RotSlot sPelvis, sWaist, sSp1, sSp2;
        // Her head, turned to look at him, and how her face lies on it (found once).
        public RotSlot sHead;
        public HeadFrame face;
        // The bones of her neck (between her upper spine and her head), and how they and her back were turned once she
        // was posed this frame: what the game's own look-at does to them after that is taken back.
        public Transform[] neck;
        public Quaternion[] neckLocal;
        public readonly Quaternion[] backLocal = new Quaternion[3];
        public PosSlot glL, glR, peL, peR;
        public Limb[] legs = new Limb[2];
        public Limb[] arms = new Limb[2];
        // Her vagina, if her rig has a bone for it (looked for once).
        public Transform vulva;
        public bool vulvaSearched;
        // The entrance the game itself goes by, as a fixed place on that bone (found when the hero is taken up).
        public Vector3 vulvaLocal;
        public bool vulvaLocalSet;
        // The bone of hers that place is kept on (her vagina's bone, or her hips when her rig has none).
        public Transform holeAnchor;

        // The wobble of the soft bones, and the hips' motion it is driven by.
        public readonly Jiggle jglL = new Jiggle(5.3f, 0.2f);
        public readonly Jiggle jglR = new Jiggle(4.7f, 0.24f);
        // The chest is stiffer and better damped than it was: it stays on the body and does not ring.
        public readonly Jiggle jpeL = new Jiggle(5.6f, 0.45f);
        public readonly Jiggle jpeR = new Jiggle(5.2f, 0.5f);
        public Vector3 lastHip, lastHipVel, hipAcc;
        public int hipSamples;

        // The smoothing of what poses her (two stages of easing), so that nothing moves in steps.
        public Vector3 fHipA, fHipB;
        public float fPitchA, fPitchB, fRollA, fRollB, fLeanA, fLeanB;
        public bool fHas;

        // The settings that pose her glide to a new value instead of jumping to it (a slider moved, a kind of pose picked,
        // her hands put somewhere else): two stages of easing for each, by the slots of G.
        public readonly float[] gA = new float[G.Count], gB = new float[G.Count];
        public readonly bool[] gHas = new bool[G.Count];
    }

    // The slots of the settings that glide.
    internal static class G
    {
        public const int Squat = 0, HipBack = 1, Lean = 2, Arch = 3, Stance = 4, Toes = 5, HandsKnees = 6, HandsHips = 7, HandsAny = 8;
        public const int RideSquat = 9, RideLean = 10, RideStance = 11, RideHandsUnder = 12, RideKneel = 13, RideHandsChest = 14;
        public const int RideOnHim = 15, RideHandsFloor = 16, RideHandsSide = 17, RideHandsBack = 18, RideHandsArms = 19;
        public const int RideHandsHips = 20;
        public const int Count = 21;
    }
}
