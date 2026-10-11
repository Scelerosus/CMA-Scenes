using System;
using System.Collections.Generic;
using HarmonyLib;
using com.ootii.Cameras;
using UnityEngine;
using GameCamera = Assets._ReusableScripts.CuchiCuchi.Dependentes.Ootii.Camaras.MainCharCamera;

namespace CMAScenes
{
    internal static class CameraGuard
    {
        // Keep native camera updates; detach only their actor-turning behaviour while he lies.
        private static void Prepare(CameraMotor __instance)
        {
            try { ShakeController.Instance?.PrepareRideCamera(__instance); }
            catch (Exception ex) { ShakeController.Instance?.ReportCameraFailure(ex); }
        }

        private static bool AllowMotorTarget(YawPitchMotor __instance)
        {
            try
            {
                var me = ShakeController.Instance;
                if (me == null || !Input.GetKey(KeyCode.Space)) return true;
                me.PrepareRideCamera(__instance);
                return !me.SuppressSpaceCameraTarget(__instance.RigController);
            }
            catch (Exception ex) { ShakeController.Instance?.ReportCameraFailure(ex); return true; }
        }

        private static bool AllowRigTarget(CameraController __instance)
        {
            try { return !(ShakeController.Instance?.SuppressSpaceCameraTarget(__instance) ?? false); }
            catch (Exception ex) { ShakeController.Instance?.ReportCameraFailure(ex); return true; }
        }

        private static bool AllowLookTarget(GameCamera __instance)
        {
            try { return !(ShakeController.Instance?.SuppressSpaceCameraTarget(__instance.m_CameraController) ?? false); }
            catch (Exception ex) { ShakeController.Instance?.ReportCameraFailure(ex); return true; }
        }

        internal static void Install()
        {
            var harmony = new Harmony(Plugin.PluginGuid + ".camera");
            foreach (Type type in new[] { typeof(ViewMotor), typeof(OrbitFixedMotor) })
            {
                foreach (string name in new[] { "RigLateUpdate", "PostRigLateUpdate" })
                {
                    try
                    {
                        var method = AccessTools.DeclaredMethod(type, name);
                        if (method == null) throw new MissingMethodException(type.FullName, name);
                        harmony.Patch(method, prefix: new HarmonyMethod(typeof(CameraGuard), nameof(Prepare)));
                        Plugin.ModLog.LogInfo($"Native camera: watching {type.Name}.{name} for actor rotation.");
                    }
                    catch (Exception ex)
                    {
                        Plugin.ModLog.LogWarning($"Native camera: cannot watch {type.Name}.{name}: {ex.Message}");
                    }
                }
            }
            InstallTargetGuard(harmony, typeof(YawPitchMotor), "SetTargetYawPitch", nameof(AllowMotorTarget));
            InstallTargetGuard(harmony, typeof(CameraController), "SetTargetYawPitch", nameof(AllowRigTarget));
            InstallTargetGuard(harmony, typeof(GameCamera), "Ver", nameof(AllowLookTarget));
            InstallTargetGuard(harmony, typeof(GameCamera), "ForzarVista", nameof(AllowLookTarget));
        }

        private static void InstallTargetGuard(Harmony harmony, Type type, string name, string prefix)
        {
            try
            {
                var method = AccessTools.DeclaredMethod(type, name);
                if (method == null) throw new MissingMethodException(type.FullName, name);
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(CameraGuard), prefix));
                Plugin.ModLog.LogInfo($"Native camera: watching Space target on {type.Name}.{name}.");
            }
            catch (Exception ex) { Plugin.ModLog.LogWarning($"Native camera: cannot watch {type.Name}.{name}: {ex.Message}"); }
        }
    }

    public sealed partial class ShakeController
    {
        private sealed class CameraMotorState
        {
            internal CameraMotor motor;
            internal ViewMotor view;
            internal OrbitFixedMotor orbit;
            internal bool rotateAnchor, rotateWithAnchor, matchActor;
            internal string rotateAlias;
            internal int spaceFrame = -1;
        }

        private readonly List<CameraMotorState> m_cameraMotors = new List<CameraMotorState>();
        private CameraController m_nativeCameraRig;
        private bool m_cameraFailureLogged;
        private float m_spaceTargetLogAt = -100f;

        internal bool SuppressSpaceCameraTarget(CameraController rig)
        {
            if (!Input.GetKey(KeyCode.Space) || !m_ride || !m_heroReady || rig == null) return false;
            if (m_nativeCameraRig == null) PrepareRideCamera(rig.ActiveMotor);
            if (m_nativeCameraRig == null || rig.Pointer != m_nativeCameraRig.Pointer) return false;
            rig.ClearTargetYawPitch();
            if (Time.unscaledTime - m_spaceTargetLogAt > 5f)
            {
                m_spaceTargetLogAt = Time.unscaledTime;
                Plugin.ModLog.LogInfo("Native camera: ignored Space auto-look target while lying; mouse look remains native.");
            }
            return true;
        }

        internal void ReportCameraFailure(Exception ex)
        {
            if (m_cameraFailureLogged) return;
            m_cameraFailureLogged = true;
            Plugin.ModLog.LogWarning("Native camera: actor rotation could not be detached: " + ex.Message);
        }

        internal void PrepareRideCamera(CameraMotor motor)
        {
            if (!m_ride || !m_heroReady || m_hero == null || motor == null) return;
            var rig = motor.RigController;
            if (rig == null) return;
            if (m_nativeCameraRig == null)
            {
                var attached = m_hero.cameraAtada;
                if (attached == null) return;
                GameCamera gameCamera = attached.GetComponent<GameCamera>();
                if (gameCamera == null) gameCamera = attached.GetComponentInChildren<GameCamera>();
                if (gameCamera == null) gameCamera = attached.GetComponentInParent<GameCamera>();
                if (gameCamera == null)
                {
                    // CamaraAtable may live on the actor rather than on the separate camera object.
                    var candidate = rig.GetComponent<GameCamera>();
                    if (candidate == null) candidate = rig.GetComponentInParent<GameCamera>();
                    if (candidate == null) candidate = rig.GetComponentInChildren<GameCamera>();
                    var anchor = candidate != null ? candidate.m_currentAnchor : null;
                    if (anchor != null && (anchor == attached.transform || anchor.IsChildOf(attached.transform)
                        || anchor == m_hero.transform || anchor.IsChildOf(m_hero.transform)
                        || (m_heroRoot != null && (anchor == m_heroRoot || anchor.IsChildOf(m_heroRoot)))))
                        gameCamera = candidate;
                }
                m_nativeCameraRig = gameCamera != null ? gameCamera.m_CameraController : null;
                if (m_nativeCameraRig == null) return;
            }
            if (rig.Pointer != m_nativeCameraRig.Pointer) return;

            CameraMotorState state = null;
            foreach (var saved in m_cameraMotors)
                if (saved.motor.Pointer == motor.Pointer) { state = saved; break; }
            if (state == null)
            {
                var view = motor.TryCast<ViewMotor>();
                var orbit = motor.TryCast<OrbitFixedMotor>();
                if (view == null && orbit == null) return;
                state = new CameraMotorState { motor = motor, view = view, orbit = orbit };
                if (view != null)
                {
                    state.rotateAnchor = view.RotateAnchor;
                    state.rotateAlias = view.RotateAnchorAlias;
                    state.matchActor = view.IsActorMatchingRotation;
                }
                else
                {
                    state.rotateAnchor = orbit.RotateAnchor;
                    state.rotateAlias = orbit.RotateAnchorAlias;
                    state.rotateWithAnchor = orbit.RotateWithAnchor;
                }
                m_cameraMotors.Add(state);
                motor.TryCast<YawPitchMotor>()?.ClearTargetYawPitch();
                Plugin.ModLog.LogInfo($"Native camera: detached actor rotation on {(view != null ? "ViewMotor" : "OrbitFixedMotor")}, alias '{state.rotateAlias}'.");
            }
            // The input alias is another path to rotation, even with RotateAnchor disabled.
            if (state.view != null)
            {
                state.view.RotateAnchor = false;
                state.view.RotateAnchorAlias = "";
                state.view.IsActorMatchingRotation = false;
                state.view.mWasRotatingAnchor = false;
            }
            else
            {
                state.orbit.RotateAnchor = false;
                state.orbit.RotateAnchorAlias = "";
                state.orbit.RotateWithAnchor = false;
            }
            var yawPitch = motor.TryCast<YawPitchMotor>();
            if (yawPitch != null)
            {
                yawPitch.mWasAnchorRotating = false;
                // Clear queued automatic turns, including direct native requests which bypass the patched setters.
                // Reset smoothing once on key-down, keeping the current direction rather than reinterpreting it
                // around the actor's upright (which is horizontal in this pose).
                if (Input.GetKey(KeyCode.Space)) yawPitch.ClearTargetYawPitch();
                if (Input.GetKeyDown(KeyCode.Space) && state.spaceFrame != Time.frameCount)
                {
                    state.spaceFrame = Time.frameCount;
                    Plugin.ModLog.LogInfo($"Native camera: Space keeps current view {yawPitch.Euler.ToString("F1")}; queued view was {yawPitch.EulerTarget.ToString("F1")}.");
                    yawPitch._EulerTarget = yawPitch.Euler;
                    yawPitch.mViewVelocityX = yawPitch.mViewVelocityY = 0f;
                }
            }
        }

        private void RestoreRideCamera()
        {
            foreach (var state in m_cameraMotors)
            {
                try
                {
                    if (state.view != null)
                    {
                        state.view.RotateAnchor = state.rotateAnchor;
                        state.view.RotateAnchorAlias = state.rotateAlias;
                        state.view.IsActorMatchingRotation = state.matchActor;
                    }
                    else
                    {
                        state.orbit.RotateAnchor = state.rotateAnchor;
                        state.orbit.RotateAnchorAlias = state.rotateAlias;
                        state.orbit.RotateWithAnchor = state.rotateWithAnchor;
                    }
                }
                catch (Exception ex) { ReportCameraFailure(ex); }
            }
            m_cameraMotors.Clear();
            m_nativeCameraRig = null;
            m_cameraFailureLogged = false;
            m_spaceTargetLogAt = -100f;
        }
    }
}
