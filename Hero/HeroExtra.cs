using System;
using System.Collections.Generic;
using System.Text;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;
using WheelControl = Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers.UserControllers.PelvisMovementMouseWheelUserController;

namespace CMAScenes
{
    // More of the hero and of her while he lies under her:
    //
    // His hips. The game's own wheel control moves his pelvis along the way he looks, which under her slides him along the
    // floor out from under her (and what it had moved stayed moved: that is why he kept missing her). For the ride it is
    // put back to rest and the wheel is taken from it; the wheel lifts his hips instead, up into her, his shoulders and
    // feet staying on what he lies on (HeroBody.cs works out how his back and legs bend for that).
    //
    // Her body. She is posed by this plugin after the game has animated her, and her physical body (the one a hand
    // touches) stayed where the game had her, standing: his hand went through her as she is drawn and was stopped by her
    // as she is not. While she is posed, the bodies of her puppet are carried by her skeleton as it is drawn.
    //
    // His view uses the native camera, with an optional field of view override.
    public sealed partial class ShakeController
    {
        // ---- his hips ----

        // How far his hips are lifted (what is asked: the wheel and his meeting her, eased), and the most they lift: past
        // about 10 cm his shoulders or his heels would have to come off what he lies on.
        private const float ThrustCap = 0.10f;
        // (And a little toward his head with it: up and in, the way hips thrust.)
        private const float ThrustAhead = 0.15f;
        private float m_thrust, m_thrustGoal, m_thrustVel;
        private PosSlot m_thHip;
        private RotSlot m_thSpine, m_thUpper, m_thNeck, m_thLegL, m_thLegR, m_thKneeL, m_thKneeR;
        private bool m_wheelWas, m_wheelHeld, m_thrustFailed, m_lieFailed;
        private int m_pelvisLogs;

        private void ThrustCapture(Character hero)
        {
            m_thrust = m_thrustGoal = m_thrustVel = 0f;
            m_thrustFailed = m_lieFailed = false;
            m_pelvisLogs = 0;
            m_thHip = null;
            m_thSpine = m_thUpper = m_thNeck = m_thLegL = m_thLegR = m_thKneeL = m_thKneeR = null;
            try
            {
                // His front and the way to his head, in his root's own turning: what his back, legs and hips are bent and
                // lifted along, wherever his root is turned (HeroBody.cs).
                if (m_heroRoot != null)
                {
                    Quaternion inv = Quaternion.Inverse(m_heroRootRot);
                    m_restFrontLocal = inv * m_heroHomeFacing;
                    m_restHeadLocal = inv * Vector3.up;
                    m_restFrameSet = true;
                }
                Animator anim = hero.bodyAnimator;
                if (anim != null && anim.isHuman && m_heroPivot != null)
                {
                    Transform spine = m_restWaist != null ? m_restWaist : anim.GetBoneTransform(HumanBodyBones.Spine);
                    Transform neck = m_restNeck != null ? m_restNeck : anim.GetBoneTransform(HumanBodyBones.Neck);
                    if (spine != null && neck != null && m_heroThighL != null && m_heroThighR != null)
                    {
                        m_thHip = new PosSlot(m_heroPivot);
                        m_thSpine = new RotSlot(spine);
                        m_thUpper = m_restUpper != null ? new RotSlot(m_restUpper) : null;
                        m_thNeck = new RotSlot(neck);
                        m_thLegL = new RotSlot(m_heroThighL);
                        m_thLegR = new RotSlot(m_heroThighR);
                        m_thKneeL = m_heroKneeL != null ? new RotSlot(m_heroKneeL) : null;
                        m_thKneeR = m_heroKneeR != null ? new RotSlot(m_heroKneeR) : null;
                    }
                }
                m_wheelWas = WheelControl.WheelClaimedByAssist;
                WheelControl.WheelClaimedByAssist = true;
                m_wheelHeld = true;
                if (m_ctlPelvis != null)
                {
                    Vector3 before = m_ctlPelvis.currentLocalTarget;
                    m_ctlPelvis.ResetTarget();
                    Plugin.ModLog.LogInfo($"Ride: the game's own control of his hips is put to rest for the ride (it had them at {before.ToString("F2")}, now {m_ctlPelvis.currentLocalTarget.ToString("F2")}); the wheel lifts his hips instead.");
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("The game's control of his hips could not be put to rest: " + ex.Message);
            }
        }

        private void ThrustRelease()
        {
            try
            {
                if (m_wheelHeld)
                {
                    WheelControl.WheelClaimedByAssist = m_wheelWas;
                }
            }
            catch (Exception)
            {
            }
            m_wheelHeld = false;
            m_thHip?.Release();
            m_thSpine?.Release();
            m_thUpper?.Release();
            m_thNeck?.Release();
            m_thLegL?.Release();
            m_thLegR?.Release();
            m_thKneeL?.Release();
            m_thKneeR?.Release();
            m_thHip = null;
            m_thSpine = m_thUpper = m_thNeck = m_thLegL = m_thLegR = m_thKneeL = m_thKneeR = null;
            m_thrust = m_thrustGoal = m_thrustVel = 0f;
            RestForget();
        }

        // Every frame, before he is laid: the game's control of his hips kept at rest, and how far his hips are to be lifted
        // (the wheel and his meeting her strokes). It is put on his skeleton once he is laid (HeroPoseLying), no more of it
        // than his back lets him (HeroRestStep).
        private void ThrustStep(float weight)
        {
            if (m_thrustFailed)
            {
                return;
            }
            try
            {
                if (m_wheelHeld)
                {
                    if (!WheelControl.WheelClaimedByAssist)
                    {
                        WheelControl.WheelClaimedByAssist = true;
                    }
                    if (m_ctlPelvis != null && m_ctlPelvis.currentLocalTarget.sqrMagnitude > 1e-4f)
                    {
                        if (m_pelvisLogs < 3)
                        {
                            m_pelvisLogs++;
                            Plugin.ModLog.LogInfo($"Ride: the game moved his hips to {m_ctlPelvis.currentLocalTarget.ToString("F2")} all the same; put back to rest.");
                        }
                        m_ctlPelvis.ControlForze(Vector3.zero);
                    }
                }
                float dt = Mathf.Clamp(Time.deltaTime, 0.004f, 0.05f);
                float wheel = weight > 0.99f && !Menu.IsOpen ? Input.mouseScrollDelta.y : 0f;
                // (Never down: under him is what he lies on.)
                m_thrustGoal = Mathf.Clamp(m_thrustGoal + wheel * 0.012f, 0f, ThrustCap);
                HoldStep(dt);
                // (And he meets her strokes by himself, if that is set: Body.cs.)
                m_thrust = Damp(m_thrust, Mathf.Min(ThrustCap, m_thrustGoal + HeroMeets()) * weight, ref m_thrustVel, 0.12f, dt);
                m_thrust = Mathf.Clamp(m_thrust, 0f, ThrustCap);
            }
            catch (Exception ex)
            {
                m_thrustFailed = true;
                Plugin.ModLog.LogWarning("His hips could not be moved by the wheel: " + ex.Message);
            }
        }

        // Every frame, once his root is where it goes this frame: he is posed to lie on what is under him, as HeroRestStep
        // worked it out (his lower back, his upper back, his neck, his thighs and his knees, each turned about the axis
        // across his body), and his hips are lifted by the thrust, up off it and a little toward his head. All of it in
        // the turning of his root, so it is the same whether he is still lying down or already lies.
        private void HeroPoseLying(float weight)
        {
            if (m_lieFailed || m_thHip == null || m_thSpine == null || m_thNeck == null || m_thLegL == null || m_thLegR == null || m_heroRoot == null)
            {
                return;
            }
            try
            {
                m_thHip.Begin();
                m_thSpine.Begin();
                m_thUpper?.Begin();
                m_thNeck.Begin();
                m_thLegL.Begin();
                m_thLegR.Begin();
                m_thKneeL?.Begin();
                m_thKneeR?.Begin();
                m_thrustWorld = Vector3.zero;
                Quaternion root = m_heroRoot.rotation;
                Vector3 front = root * m_restFrontLocal;
                Vector3 toHead = root * m_restHeadLocal;
                // (Turned about it the right way round, his head end goes down toward his back; his legs and lower legs
                // go down about it the other way round.)
                Vector3 axis = Vector3.Cross(front, toHead);
                if (m_bendHas && axis.sqrMagnitude > 1e-6f)
                {
                    axis.Normalize();
                    if (m_thrustNow > 0.0005f)
                    {
                        m_thrustWorld = (front + toHead * ThrustAhead) * m_thrustNow;
                        m_thHip.Offset(m_thrustWorld);
                    }
                    TurnBone(m_thSpine.t, axis, (m_bendWaist + m_bendArch) * weight);
                    if (m_thUpper != null)
                    {
                        TurnBone(m_thUpper.t, axis, -m_bendArch * weight);
                    }
                    TurnBone(m_thNeck.t, axis, (m_bendNeck - Mathf.Clamp(Plugin.RideHeadLift.Value, 0f, 30f)) * weight);
                    TurnBone(m_thLegL.t, -axis, (m_bendThigh[0] + m_bendThighLift[0]) * weight);
                    TurnBone(m_thLegR.t, -axis, (m_bendThigh[1] + m_bendThighLift[1]) * weight);
                    if (m_thKneeL != null)
                    {
                        TurnBone(m_thKneeL.t, -axis, m_bendKnee[0] * weight);
                    }
                    if (m_thKneeR != null)
                    {
                        TurnBone(m_thKneeR.t, -axis, m_bendKnee[1] * weight);
                    }
                }
                m_thSpine.Commit();
                m_thUpper?.Commit();
                m_thNeck.Commit();
                m_thLegL.Commit();
                m_thLegR.Commit();
                m_thKneeL?.Commit();
                m_thKneeR?.Commit();
            }
            catch (Exception ex)
            {
                m_lieFailed = true;
                Plugin.ModLog.LogWarning("He could not be posed to lie on what is under him: " + ex.Message);
            }
        }

        private static void TurnBone(Transform t, Vector3 axis, float degrees)
        {
            if (Mathf.Abs(degrees) > 0.01f)
            {
                t.rotation = Quaternion.AngleAxis(degrees, axis) * t.rotation;
            }
        }

        // ---- his view ----

        private float m_fovWas;
        private bool m_fovSet;
        private Camera m_fovCam;

        // The game owns camera position and rotation. Only apply an explicitly requested FOV.
        private void CameraFovStep()
        {
            try
            {
                Camera cam = Camera.main;
                if (cam == null)
                {
                    return;
                }
                float fov = Plugin.RideFov.Value;
                if (fov >= 20f && m_heroW > 0.5f)
                {
                    if (m_fovSet && m_fovCam != cam) ReleaseCameraFov();
                    if (!m_fovSet)
                    {
                        m_fovWas = cam.fieldOfView;
                        m_fovSet = true;
                        m_fovCam = cam;
                    }
                    cam.fieldOfView = Mathf.Clamp(fov, 20f, 120f);
                }
                else if (m_fovSet)
                {
                    ReleaseCameraFov();
                }
            }
            catch (Exception)
            {
            }
        }

        private void CameraRelease()
        {
            ReleaseCameraFov();
            RestoreRideCamera();
        }

        private void ReleaseCameraFov()
        {
            try
            {
                if (m_fovSet && m_fovCam != null)
                {
                    m_fovCam.fieldOfView = m_fovWas;
                }
            }
            catch (Exception)
            {
            }
            m_fovSet = false;
            m_fovCam = null;
        }

        // ---- her physical body ----

        private sealed class HerBody
        {
            public RootMotion.Dynamics.PuppetMaster puppet;
            public bool searched;
            public bool on;
            public readonly List<HeroMuscle> held = new List<HeroMuscle>();
        }

        private readonly Dictionary<Woman, HerBody> m_herBodies = new Dictionary<Woman, HerBody>();
        private int m_herBodyLogs;

        // After she has been posed (or let go) this frame: while she is posed the bodies of her puppet are carried by her
        // skeleton as it is drawn; let go, they are the game's again.
        // Her soft parts (breasts, nipples, buttocks, belly): the game moves these itself; held, they were pulled out of shape.
        private static readonly System.Text.RegularExpressions.Regex SoftPart = new System.Text.RegularExpressions.Regex("breast|boob|nipple|pezon|pecho|teta|seno|glute|nalga|butt|belly|vientre", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        internal void HerBodyFollow(Woman w, bool posed)
        {
            try
            {
                if (!m_herBodies.TryGetValue(w, out HerBody hb))
                {
                    if (!posed)
                    {
                        return;
                    }
                    hb = new HerBody();
                    m_herBodies[w] = hb;
                }
                if (posed && !hb.on)
                {
                    if (!hb.searched)
                    {
                        hb.searched = true;
                        var all = Resources.FindObjectsOfTypeAll<RootMotion.Dynamics.PuppetMaster>();
                        for (int i = 0; all != null && i < all.Length; i++)
                        {
                            var pm = all[i];
                            if (pm != null && pm.gameObject.scene.IsValid() && pm.targetRoot != null && w.hip != null && w.hip.IsChildOf(pm.targetRoot))
                            {
                                hb.puppet = pm;
                                break;
                            }
                        }
                    }
                    if (hb.puppet == null)
                    {
                        return;
                    }
                    hb.held.Clear();
                    var left = new StringBuilder();
                    var muscles = hb.puppet.muscles;
                    for (int i = 0; muscles != null && i < muscles.Length; i++)
                    {
                        var mu = muscles[i];
                        Rigidbody body = mu != null ? mu.rigidbody : null;
                        if (body == null || mu.props == null)
                        {
                            continue;
                        }
                        // Her body and limbs; what hangs on them and swings by itself (the game's own soft parts) is left to
                        // the physics, and now swings on her body where it is drawn.
                        var group = mu.props.group;
                        string mname = (mu.name ?? "") + " " + (mu.target != null ? mu.target.name : "");
                        if (group == RootMotion.Dynamics.Muscle.Group.Tail || group == RootMotion.Dynamics.Muscle.Group.Prop || SoftPart.IsMatch(mname))
                        {
                            left.Append(left.Length > 0 ? ", " : "").Append(mu.name);
                            continue;
                        }
                        hb.held.Add(new HeroMuscle { muscle = mu, body = body, target = mu.target, wasKinematic = body.isKinematic, mappingWas = mu.props.mappingWeight });
                    }
                    hb.on = true;
                    if (m_herBodyLogs < 4)
                    {
                        m_herBodyLogs++;
                        var names = new StringBuilder();
                        foreach (HeroMuscle hm in hb.held)
                        {
                            names.Append(names.Length > 0 ? ", " : "").Append(hm.target != null ? hm.target.name : "?");
                        }
                        Plugin.ModLog.LogInfo($"Her physical body follows her as she is drawn while she is posed: {hb.held.Count} bodies of '{w.name}' are carried by her skeleton ({names}); left to the physics: {(left.Length > 0 ? left.ToString() : "none")}.");
                    }
                }
                else if (!posed && hb.on)
                {
                    HerBodyRelease(hb);
                    if (m_carryJumpFrame == Time.frameCount)
                    {
                        HerBodyJump(hb);
                    }
                    return;
                }
                if (hb.on)
                {
                    HerBodyCarry(hb);
                    if (m_carryJumpFrame == Time.frameCount)
                    {
                        HerBodyJump(hb);
                    }
                }
            }
            catch (Exception ex)
            {
                if (m_herBodyLogs < 8)
                {
                    m_herBodyLogs++;
                    Plugin.ModLog.LogWarning("Her physical body could not be carried: " + ex.Message);
                }
            }
        }

        private static void HerBodyCarry(HerBody hb)
        {
            foreach (HeroMuscle hm in hb.held)
            {
                if (hm.body == null || hm.muscle == null)
                {
                    continue;
                }
                if (!hm.body.isKinematic)
                {
                    hm.body.isKinematic = true;
                }
                if (hm.muscle.props.mappingWeight != 0f)
                {
                    hm.muscle.props.mappingWeight = 0f;
                }
                hm.muscle.MoveToTarget();
            }
        }

        // She was carried to a place, or back from it, at once this frame: what of her puppet is left to the physics (her
        // soft parts) is put where her skeleton is now and brought to rest, so that it is not flung there by its joints.
        private int m_carryJumpFrame = -1;

        private static void HerBodyJump(HerBody hb)
        {
            try
            {
                var muscles = hb.puppet != null ? hb.puppet.muscles : null;
                int moved = 0;
                for (int i = 0; muscles != null && i < muscles.Length; i++)
                {
                    var mu = muscles[i];
                    Rigidbody body = mu != null ? mu.rigidbody : null;
                    if (body == null || mu.target == null)
                    {
                        continue;
                    }
                    bool held = false;
                    foreach (HeroMuscle hm in hb.held)
                    {
                        if (hm.body != null && hm.body.Pointer == body.Pointer)
                        {
                            held = true;
                            break;
                        }
                    }
                    if (held)
                    {
                        continue;
                    }
                    mu.MoveToTarget();
                    if (!body.isKinematic)
                    {
                        body.velocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                    moved++;
                }
                Plugin.ModLog.LogInfo($"Place: {moved} loose bodies of her puppet were put where she is now.");
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogInfo("Place: the loose bodies of her puppet could not be moved with her (" + ex.Message + ").");
            }
        }

        private static void HerBodyRelease(HerBody hb)
        {
            foreach (HeroMuscle hm in hb.held)
            {
                if (hm.body == null || hm.muscle == null)
                {
                    continue;
                }
                hm.muscle.MoveToTarget();
                hm.body.isKinematic = hm.wasKinematic;
                hm.muscle.props.mappingWeight = hm.mappingWas;
            }
            hb.held.Clear();
            hb.on = false;
        }

        // At the end of the frame, once the game has done all it does to her: carried once more; and a woman who is gone
        // from the list (made anew by the game) is let go.
        internal void HerBodiesFinal()
        {
            if (m_herBodies.Count == 0)
            {
                return;
            }
            try
            {
                Woman gone = null;
                foreach (KeyValuePair<Woman, HerBody> pair in m_herBodies)
                {
                    if (!m_women.Contains(pair.Key))
                    {
                        gone = pair.Key;
                    }
                    else if (pair.Value.on)
                    {
                        HerBodyCarry(pair.Value);
                    }
                }
                if (gone != null)
                {
                    HerBody hb = m_herBodies[gone];
                    m_herBodies.Remove(gone);
                    if (hb.on)
                    {
                        HerBodyRelease(hb);
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
