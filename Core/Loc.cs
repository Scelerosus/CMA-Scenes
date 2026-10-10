using System;
using UnityEngine;

namespace CMAScenes
{
    // The two languages of the window and of the notices on the screen. Every text is written where it is used, in both,
    // and this picks the one to show: Russian or English as set, or by the language of the system when set to "auto".
    internal static class Loc
    {
        private static string s_seen;
        private static bool s_ru;

        internal static bool Ru
        {
            get
            {
                string set = Plugin.Language != null ? Plugin.Language.Value : "auto";
                if (!ReferenceEquals(set, s_seen))
                {
                    s_seen = set;
                    if (string.Equals(set, "ru", StringComparison.OrdinalIgnoreCase))
                    {
                        s_ru = true;
                    }
                    else if (string.Equals(set, "en", StringComparison.OrdinalIgnoreCase))
                    {
                        s_ru = false;
                    }
                    else
                    {
                        s_ru = Application.systemLanguage == SystemLanguage.Russian || Application.systemLanguage == SystemLanguage.Ukrainian
                            || Application.systemLanguage == SystemLanguage.Belarusian;
                    }
                }
                return s_ru;
            }
        }

        internal static string T(string en, string ru)
        {
            return Ru ? ru : en;
        }

        // The names the settings keep (in the config file they stay in English) as they are shown.
        internal static string Preset(string name)
        {
            switch ((name ?? "").ToLowerInvariant())
            {
                case "slow": return T("Sensual", "Чувственно");
                case "soft": return T("Gentle", "Нежно");
                case "fast": return T("Wild", "Страстно");
                default: return T("As set", "Как настроено");
            }
        }

        internal static string Rhythm(string name)
        {
            switch ((name ?? "").ToLowerInvariant())
            {
                case "steady": return T("Steady", "Ровно");
                case "random": return T("Improvised", "Импровизация");
                default: return T("Scenario", "Сценарий");
            }
        }

        internal static string Pose(string name)
        {
            switch ((name ?? "").ToLowerInvariant())
            {
                case "bent over": return T("Bent over", "Наклон");
                case "deep squat": return T("Deep squat", "Присед");
                case "hands on hips": return T("Hands on hips", "Руки на бёдрах");
                case "low arch": return T("Low arch", "Прогиб");
                case "cowgirl": return T("Cowgirl", "Наездница");
                case "astride": return T("Astride", "Верхом");
                case "over him": return T("Over him", "Над ним");
                case "leaning back": return T("Leaning back", "Откинувшись");
                case "reverse": return T("Reverse", "Спиной");
                case "squatting": return T("Squatting", "На корточках");
                case "kneeling": return T("Kneeling", "На коленях");
                default: return name ?? "";
            }
        }

        internal static string Key(KeyCode key)
        {
            return key == KeyCode.None ? T("none", "нет") : key.ToString();
        }
    }
}
