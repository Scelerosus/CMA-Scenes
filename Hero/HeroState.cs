using System;
using System.Collections.Generic;
using System.Text;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Characters;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Characters.Controlladores;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers.UserControllers;
using UnityEngine;
using PuppetMode = RootMotion.Dynamics.PuppetMaster.Mode;

namespace CMAScenes
{
    // The hero while he lies: his physical body is held still, his arm is given back to the physics while the player uses
    // his hand, and what the game's own controls of his hips and hands are doing is written to the log.
    //
    // Held still: his body is a ragdoll that follows his skeleton by forces, and it is the ragdoll that is drawn. Lying
    // under her it is pushed by the floor and by her body at every step of the physics, which shows as a twitching on the
    // spot. For the ride each of its bodies is made kinematic (carried by his skeleton with all of its colliders, pushed
    // by nothing), and what is drawn is his skeleton itself.
    //
    // His arm: with all of him kinematic his hand went straight through her, for a kinematic body is stopped by nothing.
    // So while the game's own hand control has a hand of his in use, the bodies of that arm are given back to the
    // physics, as the game has them: the hand is then stopped by what it touches, and the arm that is drawn is the
    // physical one. When the hand is let go the arm is held still again.
    public sealed partial class ShakeController
    {
        private sealed class HeroMuscle
        {
            public RootMotion.Dynamics.Muscle muscle;
            public Rigidbody body;
            public Transform target;
            public bool wasKinematic;
            public float mappingWas;
            // 0: not of an arm. 1: of his left arm. 2: of his right arm.
            public int arm;
            public bool free;
        }

        private readonly List<HeroMuscle> m_heroMuscles = new List<HeroMuscle>();
        private PuppetMode m_heroPuppetMode;
        private bool m_heroPuppetHeld;
        private int m_heroPuppetLogs;
        // His upper arms (left, right): laid a little away from his body while she is on him.
        private readonly Transform[] m_heroUpperArm = new Transform[2];
        private readonly RotSlot[] m_heroArmSlot = new RotSlot[2];
        // How much the game's hand control has each of his hands in use (left, right), and what is noted of each use.
        private readonly float[] m_handUse = new float[2];
        private readonly bool[] m_handFree = new bool[2];
        private readonly float[] m_handFreeAt = new float[2];
        private readonly float[] m_handOffMax = new float[2];
        private int m_handLogs;

        private void HoldHeroPuppet()
        {
            m_heroMuscles.Clear();
            m_heroPuppetHeld = false;
            m_heroPuppetLogs = 0;
            m_handLogs = 0;
            m_handFree[0] = m_handFree[1] = false;
            try
            {
                if (m_heroPuppet == null)
                {
                    return;
                }
                m_heroPuppetMode = m_heroPuppet.mode;
                Animator anim = m_hero != null ? m_hero.bodyAnimator : null;
                bool human = anim != null && anim.isHuman;
                Transform armL = human ? anim.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
                Transform armR = human ? anim.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
                var muscles = m_heroPuppet.muscles;
                int arms = 0;
                for (int i = 0; muscles != null && i < muscles.Length; i++)
                {
                    var mu = muscles[i];
                    Rigidbody body = mu != null ? mu.rigidbody : null;
                    if (body == null)
                    {
                        continue;
                    }
                    var hm = new HeroMuscle { muscle = mu, body = body, target = mu.target, wasKinematic = body.isKinematic, mappingWas = mu.props != null ? mu.props.mappingWeight : 1f };
                    if (hm.target != null)
                    {
                        if (armL != null && hm.target.IsChildOf(armL))
                        {
                            hm.arm = 1;
                        }
                        else if (armR != null && hm.target.IsChildOf(armR))
                        {
                            hm.arm = 2;
                        }
                        else if (m_heroHead != null && hm.target == m_heroHead)
                        {
                            // His head: the view is on it. Left as the game has it whenever his puppet is in the game's
                            // own mode (drawn from his skeleton, the view ended up inside his head).
                            hm.arm = 3;
                        }
                    }
                    if (hm.arm != 0)
                    {
                        arms++;
                    }
                    m_heroMuscles.Add(hm);
                }
                m_heroPuppetHeld = true;
                KeepHeroPuppet();
                Plugin.ModLog.LogInfo($"Ride: hero puppet held still for the ride: {m_heroMuscles.Count} bodies are carried by his skeleton, {arms} of them of his arms (given back to the physics while his hand is in use); mode {m_heroPuppetMode}.");
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Hero puppet could not be held still: " + ex.Message);
            }
        }

        // Which of his hands the game's own control has in use, by side (0 left, 1 right): 0 not at all, 1 wholly.
        private void ReadHandUse()
        {
            m_handUse[0] = m_handUse[1] = 0f;
            try
            {
                for (int i = 0; m_ctlHands != null && i < m_ctlHands.Length; i++)
                {
                    HandControllerV2 h = m_ctlHands[i];
                    if (h == null || !h.isActiveAndEnabled)
                    {
                        continue;
                    }
                    string side = h.side.ToString();
                    int at = side.StartsWith("R", StringComparison.OrdinalIgnoreCase) || side.StartsWith("D", StringComparison.OrdinalIgnoreCase) ? 1
                        : side.StartsWith("L", StringComparison.OrdinalIgnoreCase) || side.StartsWith("I", StringComparison.OrdinalIgnoreCase) ? 0 : -1;
                    if (at < 0)
                    {
                        continue;
                    }
                    m_handUse[at] = Mathf.Max(m_handUse[at], h.handEstaSiendoUsada ? 1f : Mathf.Clamp01(h.weigth));
                }
            }
            catch (Exception)
            {
            }
        }

        // Every frame: each body of his is carried by his skeleton, but those of an arm whose hand is in use.
        private void KeepHeroPuppet()
        {
            if (!m_heroPuppetHeld || m_heroPuppet == null)
            {
                return;
            }
            try
            {
                ReadHandUse();
                bool lying = m_heroW >= 0.99f;
                bool handInUse = lying && (m_handUse[0] > 0.02f || m_handUse[1] > 0.02f);
                // With no hand in use his puppet is in its own kinematic mode: all of him carried by his skeleton, the way
                // the game itself does it, and the view where the game puts it. Only while a hand is in use is it in the
                // mode the game plays in, with that arm (and his head) the physics' and the rest of him held.
                PuppetMode want = handInUse ? PuppetMode.Active : PuppetMode.Kinematic;
                if (m_heroPuppet.mode != want)
                {
                    m_heroPuppet.mode = want;
                }
                if (!handInUse)
                {
                    foreach (HeroMuscle hm in m_heroMuscles)
                    {
                        if (hm.free && hm.muscle != null && hm.muscle.props != null)
                        {
                            hm.free = false;
                        }
                        if (hm.muscle != null && hm.muscle.props != null && hm.muscle.props.mappingWeight != hm.mappingWas)
                        {
                            hm.muscle.props.mappingWeight = hm.mappingWas;
                        }
                    }
                    HandReport();
                    return;
                }
                foreach (HeroMuscle hm in m_heroMuscles)
                {
                    if (hm.body == null || hm.muscle == null)
                    {
                        continue;
                    }
                    bool free = hm.arm == 3 || (hm.arm != 0 && m_handUse[hm.arm - 1] > 0.02f);
                    if (free && !hm.free)
                    {
                        hm.body.isKinematic = false;
                        hm.body.velocity = Vector3.zero;
                        hm.body.angularVelocity = Vector3.zero;
                        if (hm.muscle.props != null)
                        {
                            hm.muscle.props.mappingWeight = hm.mappingWas;
                        }
                    }
                    hm.free = free;
                    if (free)
                    {
                        if (hm.body.isKinematic)
                        {
                            hm.body.isKinematic = false;
                        }
                    }
                    else
                    {
                        if (!hm.body.isKinematic)
                        {
                            hm.body.isKinematic = true;
                        }
                        // What is drawn of a body that is carried is his skeleton, not the body.
                        if (hm.muscle.props != null && hm.muscle.props.mappingWeight != 0f)
                        {
                            hm.muscle.props.mappingWeight = 0f;
                        }
                    }
                }
                HandReport();
            }
            catch (Exception ex)
            {
                if (m_heroPuppetLogs < 6)
                {
                    m_heroPuppetLogs++;
                    Plugin.ModLog.LogWarning("Hero puppet could not be kept still: " + ex.Message);
                }
            }
        }

        // For the log: when a hand of his is taken into use and let go, and how far its physical hand got from where it
        // was aimed meanwhile (it is stopped by what it touches, so a hand that touched her shows some centimetres here).
        private void HandReport()
        {
            for (int side = 0; side < 2; side++)
            {
                bool free = false;
                float off = 0f;
                foreach (HeroMuscle hm in m_heroMuscles)
                {
                    if (hm.arm != side + 1 || !hm.free || hm.body == null)
                    {
                        continue;
                    }
                    free = true;
                    if (hm.target != null && hm.target == m_limb[side])
                    {
                        off = (hm.body.position - hm.target.position).magnitude;
                    }
                }
                string name = side == 0 ? "left" : "right";
                if (free && !m_handFree[side])
                {
                    m_handFreeAt[side] = Time.unscaledTime;
                    m_handOffMax[side] = 0f;
                    if (m_handLogs < 8)
                    {
                        Plugin.ModLog.LogInfo($"Ride: his {name} hand is in use: its arm is given back to the physics (puppet mode {m_heroPuppet.mode}).");
                    }
                }
                else if (!free && m_handFree[side])
                {
                    if (m_handLogs < 8)
                    {
                        m_handLogs++;
                        Plugin.ModLog.LogInfo($"Ride: his {name} hand was in use for {Time.unscaledTime - m_handFreeAt[side]:F1} s; its physical hand got at most {m_handOffMax[side] * 100f:F1} cm from where it was aimed. Its arm is held still again.");
                    }
                }
                if (free)
                {
                    m_handOffMax[side] = Mathf.Max(m_handOffMax[side], off);
                }
                m_handFree[side] = free;
            }
        }

        // His physical body is put on his skeleton: every body of his that is carried. (Before he is held, and should the
        // holding have failed, the puppet's own teleport of all of them.)
        private void TeleportHeroBodies()
        {
            if (m_heroPuppet == null)
            {
                return;
            }
            if (!m_heroPuppetHeld)
            {
                m_heroPuppet.MoveToTarget();
                CalmHeroBodies();
                return;
            }
            try
            {
                foreach (HeroMuscle hm in m_heroMuscles)
                {
                    if (!hm.free && hm.muscle != null && hm.body != null)
                    {
                        hm.muscle.MoveToTarget();
                    }
                }
            }
            catch (Exception ex)
            {
                // Body by body did not work: the whole of him at once, as before, and no arm of his is left to the physics.
                Plugin.ModLog.LogWarning("His bodies could not be carried one by one (" + ex.Message + "); the puppet carries them all.");
                FreeHeroPuppet();
                m_heroPuppet.MoveToTarget();
                CalmHeroBodies();
            }
        }

        private void FreeHeroPuppet()
        {
            for (int side = 0; side < 2; side++)
            {
                if (m_heroArmSlot[side] != null)
                {
                    m_heroArmSlot[side].Release();
                }
                m_heroArmSlot[side] = null;
                m_heroUpperArm[side] = null;
                m_handFree[side] = false;
            }
            if (!m_heroPuppetHeld)
            {
                m_heroMuscles.Clear();
                return;
            }
            m_heroPuppetHeld = false;
            try
            {
                foreach (HeroMuscle hm in m_heroMuscles)
                {
                    if (hm.body != null)
                    {
                        hm.body.isKinematic = hm.wasKinematic;
                    }
                    if (hm.muscle != null && hm.muscle.props != null)
                    {
                        hm.muscle.props.mappingWeight = hm.mappingWas;
                    }
                }
                if (m_heroPuppet != null && m_heroPuppet.mode != m_heroPuppetMode)
                {
                    m_heroPuppet.mode = m_heroPuppetMode;
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Hero puppet could not be given back: " + ex.Message);
            }
            m_heroMuscles.Clear();
        }

        // His arms lie a little away from his body while she is on him: her feet stand right beside his hips, where his
        // hands would be. Each upper arm is turned out from his body, up to 35 degrees from the line of it, and never
        // while the player has that hand in use. On a place narrower than that (a bench) no further than keeps his hand
        // over it, in to the side of his body if it hung out past the edge; and lying, each hand is let down onto what is
        // under it, not held in the air at the height of his shoulder.
        private void HeroArmsOut(float weight)
        {
            if (m_heroPivot == null || m_heroHead == null || m_armsFailed)
            {
                return;
            }
            try
            {
                HeroArmsOutNow(weight);
            }
            catch (Exception ex)
            {
                m_armsFailed = true;
                Plugin.ModLog.LogWarning("His arms could not be laid out: " + ex.Message);
            }
        }

        private bool m_armsFailed;
        // What is under each of his hands (eased), whether it was found, and the frame it was last looked for in.
        private readonly float[] m_handGround = new float[2];
        private readonly bool[] m_handGroundHas = new bool[2];
        private int m_handGroundFrame;

        private void HeroArmsOutNow(float weight)
        {
            Vector3 down = m_heroPivot.position - m_heroHead.position;
            if (down.sqrMagnitude < 1e-4f)
            {
                return;
            }
            down.Normalize();
            // On a place with edges: how far from the line of his body his hands may be, a little in from its edge.
            float room = float.PositiveInfinity;
            Vector3 across = Vector3.zero;
            if (PlaceActive && !m_place.open && m_place.halfWid > 0f)
            {
                PlaceFrame(m_place, out Vector3 _, out Vector3 placeAlong);
                across = Vector3.Cross(Vector3.up, placeAlong).normalized;
                room = m_place.halfWid - 0.05f - Mathf.Abs(Vector3.Dot(m_heroPivot.position - m_place.centre, across));
            }
            // (His back faces down as he lies; the way his hands are let down.)
            Vector3 back = m_restFrameSet && m_heroRoot != null ? -(m_heroRoot.rotation * m_restFrontLocal) : Vector3.down;
            bool look = Time.frameCount - m_handGroundFrame >= 4;
            if (look)
            {
                m_handGroundFrame = Time.frameCount;
            }
            for (int side = 0; side < 2; side++)
            {
                Transform upper = m_heroUpperArm[side];
                Transform hand = m_limb[side];
                RotSlot slot = m_heroArmSlot[side];
                if (upper == null || hand == null || slot == null)
                {
                    continue;
                }
                slot.Begin();
                float w = weight * (1f - Mathf.Clamp01(m_handUse[side] * 4f));
                Vector3 arm = hand.position - upper.position;
                float armLen = arm.magnitude;
                Vector3 outward = upper.position - m_heroPivot.position;
                outward -= down * Vector3.Dot(outward, down);
                if (w > 0.01f && armLen > 0.01f && outward.sqrMagnitude > 1e-4f)
                {
                    arm /= armLen;
                    float spread = 35f;
                    bool narrow = !float.IsPositiveInfinity(room);
                    if (narrow)
                    {
                        // (His hand as far out as his shoulder is, and as much again as the room left lets his arm be turned.)
                        float beyond = room - Mathf.Abs(Vector3.Dot(upper.position - m_heroPivot.position, across));
                        spread = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(beyond / armLen, -1f, 1f)) * Mathf.Rad2Deg, 4f, 35f);
                    }
                    float turn = spread * w - Vector3.Angle(arm, down);
                    // (Out only, unless it hangs out past the edge of a narrow place: then in, a little.)
                    turn = narrow ? Mathf.Max(turn, -15f * w) : turn;
                    Vector3 axis = Vector3.Cross(arm, outward.normalized);
                    if ((turn > 0.1f || (narrow && turn < -0.1f)) && axis.sqrMagnitude > 1e-4f)
                    {
                        upper.rotation = Quaternion.AngleAxis(turn, axis.normalized) * upper.rotation;
                    }
                    // Lying, his hand down onto what is under it (its back a couple of centimetres thick), no more than 25
                    // degrees; past the edge of what he lies on it is not let down after it.
                    if (m_heroW > 0.5f && m_restYValid)
                    {
                        if (look || !m_handGroundHas[side])
                        {
                            bool found = RestSurfaceAt(hand.position, out float g);
                            if (found && g > m_rideFloorY - 0.06f)
                            {
                                m_handGround[side] = m_handGroundHas[side] ? Mathf.Lerp(m_handGround[side], g, 0.5f) : g;
                                m_handGroundHas[side] = true;
                            }
                            else
                            {
                                m_handGroundHas[side] = false;
                            }
                        }
                        if (m_handGroundHas[side])
                        {
                            float above = hand.position.y - (m_handGround[side] + HeroGap() + 0.028f);
                            Vector3 armNow = hand.position - upper.position;
                            Vector3 lower = Vector3.Cross(armNow, back);
                            if (above > 0.005f && lower.sqrMagnitude > 1e-6f)
                            {
                                float let = Mathf.Min(25f, Mathf.Asin(Mathf.Clamp01(above / armLen)) * Mathf.Rad2Deg) * w;
                                upper.rotation = Quaternion.AngleAxis(let, lower.normalized) * upper.rotation;
                            }
                        }
                    }
                }
                slot.Commit();
            }
            HoldHands(weight);
        }

        // ---- how far his physical body is from his skeleton, and how much his limbs move, while he lies ----

        private float m_driftFrom;
        private float m_driftMax, m_driftSum;
        private int m_driftCount;
        private string m_driftWorst = "";
        private readonly Transform[] m_limb = new Transform[5];
        private readonly Vector3[] m_limbAt = new Vector3[5];
        private readonly float[] m_limbMoved = new float[5];
        private static readonly string[] LimbNames = { "left hand", "right hand", "left foot", "right foot", "head" };

        private void DriftReset()
        {
            m_driftFrom = 0f;
            m_holdArm[0] = m_holdArm[1] = null;
            m_holdW = 0f;
            m_holdFailed = false;
            m_holdTold = false;
            m_herJointValid = false;
            for (int i = 0; i < m_limb.Length; i++)
            {
                m_limb[i] = null;
            }
            try
            {
                Animator anim = m_hero != null ? m_hero.bodyAnimator : null;
                if (anim != null && anim.isHuman)
                {
                    m_limb[0] = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                    m_limb[1] = anim.GetBoneTransform(HumanBodyBones.RightHand);
                    m_limb[2] = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                    m_limb[3] = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                    m_limb[4] = anim.GetBoneTransform(HumanBodyBones.Head);
                    m_heroUpperArm[0] = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    m_heroUpperArm[1] = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    for (int side = 0; side < 2; side++)
                    {
                        m_heroArmSlot[side] = m_heroUpperArm[side] != null ? new RotSlot(m_heroUpperArm[side]) : null;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        // Called at the end of the frame, before his physical body is put on his skeleton again: over six seconds, how far
        // the physics carried it off meanwhile, and how far his hands, feet and head moved from where they lay.
        private void MeasureDrift()
        {
            if (m_driftFrom < 0f || m_heroW < 0.99f || m_heroPuppet == null)
            {
                return;
            }
            if (m_driftFrom == 0f)
            {
                m_driftFrom = Time.time;
                m_driftCount = -1;
                return;
            }
            if (Time.time - m_driftFrom < 3f)
            {
                return;
            }
            if (m_driftCount < 0)
            {
                m_driftCount = 0;
                m_driftMax = m_driftSum = 0f;
                m_driftWorst = "";
                for (int i = 0; i < m_limb.Length; i++)
                {
                    m_limbAt[i] = m_limb[i] != null ? m_limb[i].position : Vector3.zero;
                    m_limbMoved[i] = 0f;
                }
            }
            var muscles = m_heroPuppet.muscles;
            float worst = 0f;
            for (int i = 0; muscles != null && i < muscles.Length; i++)
            {
                var mu = muscles[i];
                // (Not the bodies of an arm that is the physics' own just now.)
                if (mu == null || mu.joint == null || mu.target == null || (mu.rigidbody != null && !mu.rigidbody.isKinematic))
                {
                    continue;
                }
                float d = (mu.joint.transform.position - mu.target.position).magnitude;
                if (d > worst)
                {
                    worst = d;
                }
                if (d > m_driftMax)
                {
                    m_driftMax = d;
                    m_driftWorst = mu.name;
                }
            }
            m_driftSum += worst;
            m_driftCount++;
            for (int i = 0; i < m_limb.Length; i++)
            {
                if (m_limb[i] != null)
                {
                    m_limbMoved[i] = Mathf.Max(m_limbMoved[i], (m_limb[i].position - m_limbAt[i]).magnitude);
                }
            }
            if (Time.time - m_driftFrom >= 9f)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < m_limb.Length; i++)
                {
                    sb.Append(i > 0 ? ", " : "").Append(LimbNames[i]).Append(' ').Append(m_limb[i] != null ? (m_limbMoved[i] * 100f).ToString("F1") : "?");
                }
                Plugin.ModLog.LogInfo($"Ride: hero stillness over 6 s: his physical body got at most {m_driftMax * 100f:F1} cm from his skeleton ('{m_driftWorst}'), {m_driftSum / Mathf.Max(1, m_driftCount) * 100f:F1} cm on average; moved, in cm: {sb}.");
                m_driftFrom = -1f;
            }
        }

        // ---- the game's own controls of his hips and hands ----

        private PelvisMovementController m_ctlPelvis;
        private PelvisMovementMouseWheelUserController m_ctlWheel;
        private PelvisMovementMouseUserController m_ctlMouse;
        private HandControllerV2[] m_ctlHands;
        private HandCameraControllerV2[] m_ctlHandCams;
        private RootMotion.FinalIK.FullBodyBipedIK m_ctlIk;
        private string m_ctlLast = "";
        private float m_ctlAt;
        private int m_ctlLogs;

        private static T FindOn<T>(Character hero) where T : Component
        {
            T found = hero.GetComponentInChildren<T>(true);
            if (found == null && hero.transform.root != null)
            {
                found = hero.transform.root.GetComponentInChildren<T>(true);
            }
            return found;
        }

        private static T[] FindAllOn<T>(Character hero) where T : Component
        {
            var all = hero.GetComponentsInChildren<T>(true);
            if ((all == null || all.Length == 0) && hero.transform.root != null)
            {
                all = hero.transform.root.GetComponentsInChildren<T>(true);
            }
            var list = new T[all != null ? all.Length : 0];
            for (int i = 0; i < list.Length; i++)
            {
                list[i] = all[i];
            }
            return list;
        }

        private void ControlsCapture(Character hero)
        {
            m_ctlPelvis = null;
            m_ctlWheel = null;
            m_ctlMouse = null;
            m_ctlHands = null;
            m_ctlHandCams = null;
            m_ctlIk = null;
            m_ctlLast = "";
            m_ctlLogs = 0;
            m_ctlAt = 0f;
            try
            {
                m_ctlPelvis = FindOn<PelvisMovementController>(hero);
                m_ctlWheel = FindOn<PelvisMovementMouseWheelUserController>(hero);
                m_ctlMouse = FindOn<PelvisMovementMouseUserController>(hero);
                m_ctlHands = FindAllOn<HandControllerV2>(hero);
                m_ctlHandCams = FindAllOn<HandCameraControllerV2>(hero);
                m_ctlIk = FindOn<RootMotion.FinalIK.FullBodyBipedIK>(hero);
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Hero controls could not be looked for: " + ex.Message);
            }
        }

        private void ControlsForget()
        {
            m_ctlPelvis = null;
            m_ctlWheel = null;
            m_ctlMouse = null;
            m_ctlHands = null;
            m_ctlHandCams = null;
            m_ctlIk = null;
        }

        private static string OnOff(Behaviour b)
        {
            return b.isActiveAndEnabled ? "on" : b.enabled ? "OFF (its object is off)" : "OFF";
        }

        // What his controls are doing now, in one line; numbers are rounded so that only a real change makes a new line.
        private string ControlsNow()
        {
            var sb = new StringBuilder();
            try
            {
                sb.Append("pelvis ");
                if (m_ctlPelvis == null)
                {
                    sb.Append("none");
                }
                else
                {
                    Vector3 t = m_ctlPelvis.currentLocalTarget;
                    sb.Append(OnOff(m_ctlPelvis)).Append(" weight ").Append(m_ctlPelvis.weight.ToString("F1")).Append(" moving ").Append(m_ctlPelvis.isMovingPelvis)
                        .Append(" at (").Append(t.x.ToString("F2")).Append(' ').Append(t.y.ToString("F2")).Append(' ').Append(t.z.ToString("F2")).Append(')');
                }
                sb.Append("; wheel ");
                if (m_ctlWheel == null)
                {
                    sb.Append("none");
                }
                else
                {
                    sb.Append(OnOff(m_ctlWheel)).Append(" depth ").Append(m_ctlWheel.controlProfundidadActivado).Append(" vertical ").Append(m_ctlWheel.controlVerticalActivado)
                        .Append(" moving ").Append(m_ctlWheel.isMoving).Append(" claimed ").Append(PelvisMovementMouseWheelUserController.WheelClaimedByAssist);
                }
                sb.Append("; mouse ");
                if (m_ctlMouse == null)
                {
                    sb.Append("none");
                }
                else
                {
                    sb.Append(OnOff(m_ctlMouse)).Append(" active ").Append(m_ctlMouse.activo);
                }
                for (int i = 0; m_ctlHands != null && i < m_ctlHands.Length; i++)
                {
                    HandControllerV2 h = m_ctlHands[i];
                    if (h == null)
                    {
                        continue;
                    }
                    sb.Append("; hand ").Append(h.side.ToString()).Append(' ').Append(OnOff(h)).Append(" used ").Append(h.handEstaSiendoUsada).Append(" moving ").Append(h.isMovingHand)
                        .Append(" weight ").Append(h.weigth.ToString("F1")).Append(" pose ").Append(h.tipoDePose.ToString());
                }
                for (int i = 0; m_ctlHandCams != null && i < m_ctlHandCams.Length; i++)
                {
                    HandCameraControllerV2 c = m_ctlHandCams[i];
                    if (c == null)
                    {
                        continue;
                    }
                    sb.Append("; handcam ").Append(i).Append(' ').Append(OnOff(c)).Append(" activated ").Append(c.IsActivated).Append(" may move ").Append(c.puedeMoverHand)
                        .Append(" weight ").Append(c.positionWeigth.ToString("F1"));
                }
                sb.Append("; ik ").Append(m_ctlIk == null ? "none" : OnOff(m_ctlIk));
                sb.Append("; puppet ").Append(m_heroPuppet == null ? "none" : m_heroPuppet.mode.ToString());
                sb.Append("; animator speed ").Append(m_heroAnimator != null ? m_heroAnimator.speed.ToString("F1") : "?");
            }
            catch (Exception ex)
            {
                sb.Append(" (failed: ").Append(ex.Message).Append(')');
            }
            return sb.ToString();
        }

        // Written when he is taken up, and then whenever something of it has changed (a few times a ride at most).
        private void ControlsReport(string when)
        {
            if (when == null)
            {
                if (m_ctlLogs >= 6 || Time.unscaledTime < m_ctlAt)
                {
                    return;
                }
                m_ctlAt = Time.unscaledTime + 0.5f;
            }
            string now = ControlsNow();
            if (when == null && now == m_ctlLast)
            {
                return;
            }
            m_ctlLast = now;
            if (when == null)
            {
                m_ctlLogs++;
            }
            Plugin.ModLog.LogInfo($"Ride: hero controls ({when ?? "changed"}): {now}");
        }
    }
}
