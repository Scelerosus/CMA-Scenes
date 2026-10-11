using System;
using HarmonyLib;
using UnityEngine;
using GameCamera = Assets._ReusableScripts.CuchiCuchi.Dependentes.Ootii.Camaras.MainCharCamera;
using CameraRig = com.ootii.Cameras.CameraController;

namespace CMAScenes
{
    // While the hero lies under her the game's own "look at this" for his camera (the space bar asks for it) is kept
    // from running: it turns his view toward a point by turning him about his own upright, and lying down his upright
    // is along the floor, so it never gets there and his view spins like a top.
    internal static class CameraGuard
    {
        private static float s_loggedAt = -100f;

        private static bool Skip()
        {
            try
            {
                ShakeController me = ShakeController.Instance;
                if (me != null && (me.Riding || me.HeroLying))
                {
                    if (Time.unscaledTime - s_loggedAt > 20f)
                    {
                        s_loggedAt = Time.unscaledTime;
                        Plugin.ModLog.LogInfo("Ride: the game asked his camera to turn to a point; left out while he lies (it would spin his view).");
                    }
                    return false;
                }
            }
            catch (Exception)
            {
            }
            return true;
        }

        // The free camera's "focus on the selected thing" (the space bar too): it takes the bounds of whatever is selected,
        // and her bodies and his are carried about by this plugin (to a place, under her), so those bounds are somewhere
        // else, or huge, and the camera flies off. Left out for as long as the ride is on.
        private static bool SkipFocus()
        {
            try
            {
                ShakeController me = ShakeController.Instance;
                if (me != null && (me.Riding || me.HeroLying))
                {
                    if (Time.unscaledTime - s_focusLoggedAt > 20f)
                    {
                        s_focusLoggedAt = Time.unscaledTime;
                        Plugin.ModLog.LogInfo("Ride: the free camera was asked to focus on the selection; left out during the ride (it flew off).");
                    }
                    return false;
                }
            }
            catch (Exception)
            {
            }
            return true;
        }

        private static bool SkipFocusKey(ref bool __result)
        {
            if (!SkipFocus())
            {
                __result = false;
                return false;
            }
            return true;
        }

        private static float s_focusLoggedAt = -100f;
        private static float s_yawLoggedAt = -100f;

        // The game's camera rig can also be given a target yaw/pitch directly. While Space is held
        // during a ride, do not queue an automatic turn around the hero's now-horizontal upright.
        private static bool SkipSpaceTurn(CameraRig __instance)
        {
            try
            {
                ShakeController me = ShakeController.Instance;
                if (me != null && (me.Riding || me.HeroLying) && Input.GetKey(KeyCode.Space))
                {
                    try
                    {
                        __instance.ClearTargetYawPitch();
                    }
                    catch (Exception)
                    {
                        // Still suppress the new turn if the rig is being replaced this frame.
                    }
                    if (Time.unscaledTime - s_yawLoggedAt > 20f)
                    {
                        s_yawLoggedAt = Time.unscaledTime;
                        Plugin.ModLog.LogInfo("Ride: a camera target turn was requested while Space was held; left out.");
                    }
                    return false;
                }
            }
            catch (Exception)
            {
            }
            return true;
        }

        internal static void Install()
        {
            try
            {
                var harmony = new Harmony(Plugin.PluginGuid + ".camera");
                int focus = 0;
                var type = typeof(Assets.TValle.Inputs.Runtime.Cameras.Camera3DViewInputs);
                foreach (var m in type.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    if (m.Name == "Focus")
                    {
                        harmony.Patch(m, prefix: new HarmonyMethod(typeof(CameraGuard), nameof(SkipFocus)));
                        focus++;
                    }
                    else if (m.Name == "FocusAction")
                    {
                        harmony.Patch(m, prefix: new HarmonyMethod(typeof(CameraGuard), nameof(SkipFocusKey)));
                        focus++;
                    }
                }
                Plugin.ModLog.LogInfo($"Ride: the free camera's focus is watched ({focus} overloads/actions): it is left out during the ride.");
                var skip = new HarmonyMethod(typeof(CameraGuard), nameof(Skip));
                int done = 0;
                foreach (string name in new[] { nameof(GameCamera.Ver), nameof(GameCamera.ForzarVista) })
                {
                    var method = AccessTools.Method(typeof(GameCamera), name);
                    if (method != null)
                    {
                        harmony.Patch(method, prefix: skip);
                        done++;
                    }
                }
                Plugin.ModLog.LogInfo($"Ride: the game's camera look-at is watched ({done} of 2 methods): it is left out while the hero lies.");
                var yaw = AccessTools.Method(typeof(CameraRig), nameof(CameraRig.SetTargetYawPitch));
                if (yaw != null)
                {
                    harmony.Patch(yaw, prefix: new HarmonyMethod(typeof(CameraGuard), nameof(SkipSpaceTurn)));
                    Plugin.ModLog.LogInfo("Ride: the camera rig's target turn is watched while Space is held.");
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Ride: the game's camera look-at could not be watched: " + ex.Message);
            }
        }
    }

    public sealed partial class ShakeController
    {
        // He is lying under her (or being laid down, or getting up).
        internal bool HeroLying => m_ride && m_heroReady;
    }
}
