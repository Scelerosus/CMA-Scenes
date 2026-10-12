using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;

namespace CMAScenes
{
    // Kinds of pose, each a bundle of settings that is put in place at once, and profiles: the whole set of settings
    // saved under a name, in a file of its own, and loaded back whenever it is wanted.
    internal static class Poses
    {
        internal sealed class Pose
        {
            public string name;
            public string[] values;
        }


        // The ride: each named position also fixes whether she kneels or squats. Her hands follow the position and her balance.
        internal static readonly Pose[] Ride =
        {
            new Pose { name = "Cowgirl", values = new[] { "Ride|Facing=Face", "Ride|Lean=18", "Ride|Legs=Feet" } },
            new Pose { name = "Astride", values = new[] { "Ride|Facing=Face", "Ride|Lean=34", "Ride|Legs=Knees" } },
            new Pose { name = "Over him", values = new[] { "Ride|Facing=Face", "Ride|Lean=38", "Ride|Legs=Knees" } },
            new Pose { name = "Leaning back", values = new[] { "Ride|Facing=Face", "Ride|Lean=-27", "Ride|Legs=Feet" } },
            new Pose { name = "Reverse", values = new[] { "Ride|Facing=Away", "Ride|Lean=26", "Ride|Legs=Feet" } },
            new Pose { name = "Squatting", values = new[] { "Ride|Facing=Face", "Ride|Lean=26", "Ride|Legs=Feet", "Ride|Squat=0.54" } },
            new Pose { name = "Kneeling", values = new[] { "Ride|Facing=Face", "Ride|Lean=28", "Ride|Legs=Knees" } }
        };

        internal static int IndexOf(Pose[] set, string name)
        {
            for (int i = 0; i < set.Length; i++)
            {
                if (string.Equals(set[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        // The same without keeping what was before (for a pose that changes by itself).
        internal static void Set(Pose pose, ConfigEntry<string> label)
        {
            foreach (string line in pose.values)
            {
                Profiles.SetLine(line);
            }
            label.Value = pose.name;
            Profiles.SaveConfig();
        }

        // Puts a pose's settings in place. What was set before is kept as the profile "_last", to go back to.
        internal static void Apply(Pose pose, ConfigEntry<string> label)
        {
            Profiles.Save("_last");
            foreach (string line in pose.values)
            {
                Profiles.SetLine(line);
            }
            label.Value = pose.name;
            Profiles.SaveConfig();
        }
    }

    internal static class Profiles
    {
        // What a profile holds: the motion, the pose, the rhythm, the ride and the bikini. Keys (hotkeys) are not part of it.
        private static readonly string[] Sections = { "Motion", "Pose", "Behavior", "Ride", "Bikini" };

        internal static string Dir
        {
            get
            {
                string dir = Path.Combine(BepInEx.Paths.ConfigPath, "CMAScenes-profiles");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static bool Included(ConfigDefinition d)
        {
            return Array.IndexOf(Sections, d.Section) >= 0 && !d.Key.EndsWith("Key", StringComparison.Ordinal);
        }

        // A name that is safe as a file name.
        internal static string Clean(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in (name ?? "").Trim())
            {
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '.')
                {
                    sb.Append(c);
                }
            }
            string s = sb.ToString().Trim().Trim('.');
            return s.Length > 40 ? s.Substring(0, 40).Trim() : s;
        }

        internal static List<string> List()
        {
            var names = new List<string>();
            try
            {
                foreach (string file in Directory.GetFiles(Dir, "*.profile"))
                {
                    names.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Could not list the profiles: " + ex.Message);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        internal static bool Save(string name)
        {
            string clean = Clean(name);
            if (clean.Length == 0)
            {
                return false;
            }
            try
            {
                var sb = new StringBuilder();
                foreach (ConfigDefinition def in Plugin.Cfg.Keys)
                {
                    if (!Included(def))
                    {
                        continue;
                    }
                    ConfigEntryBase entry = Plugin.Cfg[def];
                    sb.Append(def.Section).Append('|').Append(def.Key).Append('=').Append(entry.GetSerializedValue()).Append('\n');
                }
                File.WriteAllText(Path.Combine(Dir, clean + ".profile"), sb.ToString(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Could not save the profile '" + clean + "': " + ex.Message);
                return false;
            }
        }

        // One "Section|Key=value" line put into the setting it names.
        internal static bool SetLine(string line)
        {
            int eq = line.IndexOf('=');
            int bar = line.IndexOf('|');
            if (eq <= bar || bar <= 0)
            {
                return false;
            }
            var def = new ConfigDefinition(line.Substring(0, bar), line.Substring(bar + 1, eq - bar - 1));
            if (!Plugin.Cfg.Keys.Contains(def))
            {
                return false;
            }
            try
            {
                Plugin.Cfg[def].SetSerializedValue(line.Substring(eq + 1));
                return true;
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Profile line '" + line + "' was not applied: " + ex.Message);
                return false;
            }
        }

        // Loads a profile. What was set before is kept as the profile "_last". Returns how many settings were set.
        internal static int Load(string name)
        {
            string clean = Clean(name);
            string path = Path.Combine(Dir, clean + ".profile");
            if (clean.Length == 0 || !File.Exists(path))
            {
                return -1;
            }
            if (!string.Equals(clean, "_last", StringComparison.OrdinalIgnoreCase))
            {
                Save("_last");
            }
            int applied = 0;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Length > 0 && SetLine(line))
                {
                    applied++;
                }
            }
            SaveConfig();
            return applied;
        }

        internal static bool Delete(string name)
        {
            try
            {
                string path = Path.Combine(Dir, Clean(name) + ".profile");
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Could not delete the profile: " + ex.Message);
            }
            return false;
        }

        internal static void SaveConfig()
        {
            try
            {
                Plugin.Cfg.Save();
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Could not save the settings: " + ex.Message);
            }
        }
    }
}
