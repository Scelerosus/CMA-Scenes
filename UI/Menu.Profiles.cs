using System;
using System.Collections.Generic;
using Assets.TValle.Tools.Runtime.Moddding.Clothing.Maps;
using BepInEx.Configuration;
using UnityEngine;

namespace CMAScenes
{
    internal static partial class Menu
    {
        // ---- Profiles ----

        private static string s_profileName = "";
        private static string s_profileSelected = "";
        private static string s_profileStatus = "";
        private static List<string> s_profiles = new List<string>();
        private static Vector2 s_profileScroll;
        private static int s_profilesLoadedFor = -1;

        private static string ProfileLabel(string name)
        {
            return name == "_last" ? T("(settings before the last change)", "(настройки до последнего изменения)") : name;
        }

        private static void RefreshProfiles()
        {
            s_profiles = Profiles.List();
            s_profilesLoadedFor = s_version;
        }

        private static void DrawProfiles(ShakeController host, Styles st)
        {
            if (s_profilesLoadedFor < 0)
            {
                RefreshProfiles();
            }

            CardBegin(st, T("Active profile", "Текущий профиль"));
            GUILayout.Label(string.IsNullOrEmpty(Plugin.ActiveProfile.Value) ? T("None: the settings are as you left them.", "Нет: настройки такие, какими вы их оставили.") : Plugin.ActiveProfile.Value, st.status);
            if (!string.IsNullOrEmpty(s_profileStatus))
            {
                GUILayout.Label(s_profileStatus, st.dim);
            }
            CardEnd();

            CardBegin(st, T("Save the current settings", "Сохранить текущие настройки"));
            GUILayout.Label(T("A profile keeps everything about the twerk, the sex and the bikini under a name. Hotkeys are not part of it.",
                "Профиль хранит под именем всё про тверк, секс и бикини. Клавиши в него не входят."), st.dim);
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("profile");
            s_profileName = GUILayout.TextField(s_profileName, 40, st.field);
            if (s_profileName.Length == 0 && Repainting() && GUI.GetNameOfFocusedControl() != "profile")
            {
                Rect fr = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(fr.x + 12f, fr.y, fr.width - 12f, fr.height), T("Name of the profile...", "Имя профиля..."), st.placeholder);
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(T("Save", "Сохранить"), st.primary, GUILayout.Width(130f)))
            {
                string clean = Profiles.Clean(s_profileName);
                if (clean.Length == 0)
                {
                    s_profileStatus = T("Type a name first.", "Сначала введите имя.");
                }
                else if (Profiles.Save(clean))
                {
                    Plugin.ActiveProfile.Value = clean;
                    MarkDirty();
                    s_profileSelected = clean;
                    s_profileStatus = T("Saved as '", "Сохранено как «") + clean + T("'.", "».");
                    RefreshProfiles();
                }
                else
                {
                    s_profileStatus = T("Could not save it.", "Не удалось сохранить.");
                }
            }
            GUILayout.EndHorizontal();
            CardEnd();

            CardBegin(st, T("Saved profiles", "Сохранённые профили"));
            if (s_profiles.Count == 0)
            {
                GUILayout.Label(T("None yet. Save the current settings above.", "Пока нет. Сохраните текущие настройки выше."), st.dim);
            }
            s_profileScroll = GUILayout.BeginScrollView(s_profileScroll, GUILayout.Height(Mathf.Min(220f, 12f + 44f * Mathf.Max(1, s_profiles.Count))));
            foreach (string name in s_profiles)
            {
                bool sel = string.Equals(name, s_profileSelected, StringComparison.OrdinalIgnoreCase);
                if (GUILayout.Button(ProfileLabel(name), sel ? st.rowSel : st.row, GUILayout.Height(40f)))
                {
                    s_profileSelected = name;
                    if (name != "_last")
                    {
                        s_profileName = name;
                    }
                }
            }
            GUILayout.EndScrollView();
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            bool any = !string.IsNullOrEmpty(s_profileSelected);
            if (GUILayout.Button(T("Load", "Загрузить"), st.primary, GUILayout.Height(36f)) && any)
            {
                int applied = Profiles.Load(s_profileSelected);
                if (applied < 0)
                {
                    s_profileStatus = T("That profile is gone.", "Этого профиля больше нет.");
                }
                else
                {
                    if (s_profileSelected != "_last")
                    {
                        Plugin.ActiveProfile.Value = s_profileSelected;
                        MarkDirty();
                    }
                    s_profileStatus = T("Loaded '", "Загружен «") + ProfileLabel(s_profileSelected) + T("'.", "».");
                    host.ReapplyBikini();
                    RefreshProfiles();
                }
            }
            Tip(T("Puts the settings of the picked profile in place. What you had is kept as \"settings before the last change\".", "Применяет настройки выбранного профиля. Прежние сохраняются как «настройки до последнего изменения»."));
            GUILayout.Space(6f);
            if (GUILayout.Button(T("Overwrite", "Перезаписать"), st.button, GUILayout.Height(36f)) && any && s_profileSelected != "_last")
            {
                s_profileStatus = Profiles.Save(s_profileSelected) ? T("Overwrote '", "Перезаписан «") + s_profileSelected + T("' with the current settings.", "» текущими настройками.") : T("Could not save it.", "Не удалось сохранить.");
                RefreshProfiles();
            }
            Tip(T("Saves the current settings over the picked profile.", "Сохраняет текущие настройки поверх выбранного профиля."));
            GUILayout.Space(6f);
            if (GUILayout.Button(T("Delete", "Удалить"), st.ghost, GUILayout.Height(36f)) && any)
            {
                s_profileStatus = Profiles.Delete(s_profileSelected) ? T("Deleted '", "Удалён «") + ProfileLabel(s_profileSelected) + T("'.", "».") : T("Nothing to delete.", "Удалять нечего.");
                s_profileSelected = "";
                RefreshProfiles();
            }
            GUILayout.EndHorizontal();
            CardEnd();
        }
    }
}
