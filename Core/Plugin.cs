using System;
using System.Collections.Generic;
using System.IO;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers.Interacciones;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace CMAScenes
{
    // Successor to CMA Shake: ride scenes, configurable locations and poses, clothing, fluids, condom and X-ray.
    // The scene can move to furniture or wherever the player looks (Sex/Places.cs); arm poses respect the rig (Sex/Wrist.cs).
    //
    // It is the same code grown further, so the two must not run together: both would pose the same woman. CMA Shake's
    // settings and profiles are taken over the first time this one runs.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Corrupted Modeling Agency.exe")]
    [BepInIncompatibility("community.cma.shake")]
    public sealed partial class Plugin : BasePlugin
    {
        public const string PluginGuid = "community.cma.scenes";
        public const string PluginName = "CMA Scenes";
        public const string PluginVersion = "0.4.1";

        internal static ManualLogSource ModLog;
        internal static ConfigFile Cfg;

        public override void Load()
        {
            ModLog = Log;
            Cfg = Config;
            TakeOver();
            CondomHoldsSemen.Install();
            CameraGuard.Install();
            // The settings window changes values while a slider is dragged: write the file when it is let go, not on every step.
            Config.SaveOnConfigSet = false;

            BindSettings();

            AddComponent<ShakeController>();
            Log.LogInfo($"{PluginName} {PluginVersion} loaded: press {RideKey.Value} for the scene, {MenuKey.Value} for the settings window.");
        }

        // The first time: CMA Shake's settings and its profiles become this mod's (copied; CMA Shake's own are left).
        private void TakeOver()
        {
            try
            {
                string mine = Path.Combine(Paths.ConfigPath, PluginGuid + ".cfg");
                string old = Path.Combine(Paths.ConfigPath, "community.cma.shake.cfg");
                if (!File.Exists(mine) && File.Exists(old))
                {
                    File.Copy(old, mine);
                    Config.Reload();
                    Log.LogInfo("The settings of CMA Shake were taken over (community.cma.shake.cfg copied).");
                }
                string profiles = Path.Combine(Paths.ConfigPath, "CMAScenes-profiles");
                string oldProfiles = Path.Combine(Paths.ConfigPath, "CMAShake-profiles");
                if (!Directory.Exists(profiles) && Directory.Exists(oldProfiles))
                {
                    Directory.CreateDirectory(profiles);
                    foreach (string file in Directory.GetFiles(oldProfiles))
                    {
                        File.Copy(file, Path.Combine(profiles, Path.GetFileName(file)), false);
                    }
                    Log.LogInfo("The profiles of CMA Shake were taken over.");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning("CMA Shake's settings could not be taken over (" + ex.Message + "); this mod starts with its own.");
            }
        }
    }
}
