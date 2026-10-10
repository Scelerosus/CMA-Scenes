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
    // CMA Scenes: what CMA Shake was (the twerk, the sex with her on top, his wetness, the condom, the X-ray), carried on
    // under a new name, with the sex no longer tied to the spot of the floor she stands on: it can be put on the bench,
    // on a table, anywhere the player looks (Sex/Places.cs), and her arms are posed by what an arm can do (Sex/Wrist.cs).
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
        public const string PluginVersion = "0.3.27";

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
            Log.LogInfo($"{PluginName} {PluginVersion} loaded: press {RideKey.Value} for sex, {ToggleKey.Value} to twerk, {MenuKey.Value} for the settings window.");
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
