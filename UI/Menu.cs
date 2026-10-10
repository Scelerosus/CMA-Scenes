using System;
using System.Collections.Generic;
using Assets.TValle.Tools.Runtime.Moddding.Clothing.Maps;
using BepInEx.Configuration;
using UnityEngine;

namespace CMAScenes
{
    // The settings window: pick the bikini top and bottom from the installed clothing, change its colour while she
    // wears it, and tune the twerk and the ride. Everything it changes is the same setting as in the config file,
    // which is written when a slider is let go and when the window is closed.
    //
    // It is drawn with the game's immediate-mode GUI, so every look here (rounded panels, switches, sliders with a
    // filled track, round colour chips) is made from textures drawn in code and styles built from them.
    internal static partial class Menu
    {
        private const float Width = 790f;
        private const float Pad = 18f;
        private const float ThumbSize = 22f;
        private const float LabelWidth = 236f;
        // How wide a row inside a card is: the window, less its sides, the card's own and the scroll bar with its gap.
        private const float RowWidth = Width - 2f * Pad - 28f - 14f;

        // The squares of the pages, in the colours of the apps on the game's phone (its "Agency Hub").
        private static readonly Color[] TabColors = { Hex("#5BC0F8"), Hex("#F781C3"), Hex("#F26D63"), Hex("#F2D544"), Hex("#C99CF5"), Hex("#3EDDB6") };
        private static readonly string[] TabSlugs = { "home", "twerk", "ride", "bikini", "profiles", "settings" };

        private static bool s_open;
        private static bool s_typing;
        private static bool s_failed;
        private static bool s_dirty;
        private static float s_openedAt;
        private static Vector2 s_pos = new Vector2(24f, 24f);
        private static bool s_drag;
        private static Vector2 s_dragOffset;
        private static bool s_resize;
        private static float s_height;
        private static bool s_layoutLoaded;
        private static float s_uiScale;
        private const float MinHeight = 380f;
        private static int s_tab = TabHome;
        private static Vector2 s_scroll;

        // True while a text box of the window has the keyboard, so typing in it does not trigger the hotkeys.
        internal static bool TypingText => s_open && (s_typing || s_capture != null);

        // The hotkey that is waiting for its new key, the help line under the pointer, and which cards are folded open.
        private static ConfigEntry<KeyCode> s_capture;
        private static string s_tipShown = "";
        private static readonly Dictionary<string, GUIContent> s_tips = new Dictionary<string, GUIContent>();
        private static readonly Dictionary<string, bool> s_folds = new Dictionary<string, bool>();
        private static bool s_foldsLoaded;

        internal static bool IsOpen => s_open;

        internal static void Toggle(ShakeController host)
        {
            SetOpen(host, !s_open);
        }

        // Opening shows the mouse pointer and holds the game's own input; closing gives both back.
        private static void SetOpen(ShakeController host, bool open)
        {
            if (open == s_open)
            {
                return;
            }
            s_open = open;
            s_typing = false;
            s_drag = false;
            s_capture = null;
            s_resize = false;
            if (open)
            {
                LoadLayout();
                // (The game's font is there only once one of its own windows has been shown: looked for again.)
                if (!s_gameFont && GameFont() != null)
                {
                    s_fontTried = false;
                    s_st = null;
                }
                s_failed = false;
                s_openedAt = Time.unscaledTime;
                Refresh();
            }
            else
            {
                SaveIfDirty();
            }
            host.SetMenuCursor(open);
        }

        // ---- helpers ----

        private static Color Hex(string html)
        {
            ColorUtility.TryParseHtmlString(html, out Color c);
            return c;
        }

        private static void MarkDirty()
        {
            s_dirty = true;
        }

        private static void SaveIfDirty()
        {
            if (!s_dirty)
            {
                return;
            }
            s_dirty = false;
            try
            {
                Plugin.Cfg.Save();
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Could not save the settings: " + ex.Message);
            }
        }

        // Where the window was left, how tall and how big it is: kept in the config file between games.
        private static void LoadLayout()
        {
            if (s_layoutLoaded)
            {
                return;
            }
            s_layoutLoaded = true;
            s_pos = new Vector2(Plugin.WindowX.Value, Plugin.WindowY.Value);
            s_height = Plugin.WindowHeight.Value;
        }

        private static void StoreLayout()
        {
            Plugin.WindowX.Value = Mathf.Round(s_pos.x);
            Plugin.WindowY.Value = Mathf.Round(s_pos.y);
            Plugin.WindowHeight.Value = Mathf.Round(s_height);
            MarkDirty();
        }

        // The font of the window: the game's own when there is one; else one of the system's that has the letters of
        // both languages; else whatever the game draws its debug text with.
        internal static Font UiFont
        {
            get
            {
                if (!s_fontTried)
                {
                    s_fontTried = true;
                    Font game = GameFont();
                    if (game != null)
                    {
                        s_font = game;
                        s_gameFont = true;
                        Plugin.ModLog.LogInfo("Settings window: drawn in the game's own font, '" + game.name + "'.");
                        return s_font;
                    }
                    try
                    {
                        if (s_osFont == null)
                        {
                            s_osFont = Font.CreateDynamicFontFromOSFont("Segoe UI", 15);
                            if (s_osFont != null)
                            {
                                s_osFont.hideFlags = HideFlags.HideAndDontSave;
                            }
                        }
                        s_font = s_osFont;
                    }
                    catch (Exception ex)
                    {
                        s_font = null;
                        Plugin.ModLog.LogWarning("Settings window: no system font could be made, the game's own is used: " + ex.Message);
                    }
                }
                return s_font;
            }
        }

        // ---- window ----

        internal static void Draw(ShakeController host)
        {
            if (!s_open)
            {
                s_typing = false;
                return;
            }
            Matrix4x4 keepMatrix = GUI.matrix;
            Color keepColor = GUI.color;
            GUISkin keepSkin = GUI.skin;
            try
            {
                MakeStyles();
                if (s_skin != null)
                {
                    GUI.skin = s_skin;
                }
                DrawWindow(host);
            }
            catch (Exception ex)
            {
                if (!s_failed)
                {
                    s_failed = true;
                    Plugin.ModLog.LogWarning("Settings window failed and was closed: " + ex);
                }
                s_typing = false;
                SetOpen(host, false);
            }
            finally
            {
                GUI.skin = keepSkin;
                GUI.color = keepColor;
                GUI.matrix = keepMatrix;
            }
        }

        private static string T(string en, string ru)
        {
            return Loc.T(en, ru);
        }

        private const int TabHome = 0, TabTwerk = 1, TabRide = 2, TabBikini = 3, TabProfiles = 4, TabSettings = 5;

        private static readonly Color[] Dots = { Hex("#FF5F57"), Hex("#FEBC2E"), Hex("#28C840") };

        // The letters on the square of a page.
        private static string Mark(int tab)
        {
            switch (tab)
            {
                case TabTwerk: return T("Tw", "Тв");
                case TabRide: return T("R", "Р");
                case TabBikini: return T("B", "Б");
                case TabProfiles: return T("P", "П");
                case TabSettings: return T("S", "Н");
                default: return T("H", "Г");
            }
        }

        private static void DrawWindow(ShakeController host)
        {
            Styles st = s_st;
            Event ev = Event.current;

            // The size slider takes effect once it is let go: changing the size under the pointer while it drags would make it jump.
            if (s_uiScale <= 0f || GUIUtility.hotControl == 0)
            {
                s_uiScale = Mathf.Clamp(Plugin.UiScale.Value, 0.75f, 1.5f);
            }
            float scale = Mathf.Max(1f, Screen.height / 1080f) * s_uiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float sw = Screen.width / scale;
            float sh = Screen.height / scale;
            bool collapsed = Plugin.WindowCollapsed.Value;
            float maxH = Mathf.Max(MinHeight, sh - 20f);
            s_pos.x = Mathf.Clamp(s_pos.x, 0f, Mathf.Max(0f, sw - 140f));
            s_pos.y = Mathf.Clamp(s_pos.y, 0f, Mathf.Max(0f, sh - 90f));
            // Never taller than the room below the window, so its bottom line and the corner that resizes it stay on screen.
            maxH = Mathf.Clamp(sh - 10f - s_pos.y, MinHeight, maxH);
            float h = collapsed ? 128f : s_height > 1f ? Mathf.Clamp(s_height, MinHeight, maxH) : Mathf.Min(sh - 40f, 920f, maxH);
            if (!collapsed && s_pos.y + h > sh - 10f)
            {
                // Not even the shortest window fits below: lift it.
                s_pos.y = Mathf.Max(0f, sh - 10f - h);
            }

            // A key is being chosen for a hotkey: the next key pressed is it. Esc leaves it as it was, Backspace sets none.
            if (s_capture != null && ev != null && ev.type == EventType.KeyDown && ev.keyCode != KeyCode.None)
            {
                if (ev.keyCode == KeyCode.Backspace || ev.keyCode == KeyCode.Delete)
                {
                    s_capture.Value = KeyCode.None;
                    MarkDirty();
                }
                else if (ev.keyCode != KeyCode.Escape)
                {
                    s_capture.Value = ev.keyCode;
                    MarkDirty();
                }
                s_capture = null;
                ev.Use();
                SaveIfDirty();
            }

            // Esc: first lets go of a text box, then closes the window. (While a hotkey waits for its key, Esc cancels that above.)
            if (s_capture == null && ev != null && ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            {
                ev.Use();
                if (s_typing)
                {
                    GUI.FocusControl(null);
                    s_typing = false;
                }
                else
                {
                    SetOpen(host, false);
                    return;
                }
            }

            // Fades and slides in when opened.
            float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - s_openedAt) / 0.18f));
            GUI.color = new Color(1f, 1f, 1f, appear);
            Rect r = new Rect(s_pos.x, s_pos.y + (1f - appear) * 14f, Width, h);

            // Drag the window by its header (the part of it that has no buttons); change its height by the bottom-right corner.
            Rect header = new Rect(r.x, r.y, r.width - 230f, 66f);
            Rect grip = new Rect(r.xMax - 26f, r.yMax - 26f, 26f, 26f);
            if (ev != null)
            {
                if (ev.type == EventType.MouseDown && ev.button == 0 && !collapsed && grip.Contains(ev.mousePosition))
                {
                    s_resize = true;
                    ev.Use();
                }
                else if (ev.type == EventType.MouseDown && ev.button == 0 && header.Contains(ev.mousePosition))
                {
                    s_drag = true;
                    s_dragOffset = ev.mousePosition - s_pos;
                }
                else if (s_resize && ev.type == EventType.MouseDrag)
                {
                    s_height = Mathf.Clamp(ev.mousePosition.y - r.y + 8f, MinHeight, maxH);
                    ev.Use();
                }
                else if (s_drag && ev.type == EventType.MouseDrag)
                {
                    s_pos = ev.mousePosition - s_dragOffset;
                }
                else if (ev.rawType == EventType.MouseUp)
                {
                    if (s_drag || s_resize)
                    {
                        StoreLayout();
                    }
                    s_drag = false;
                    s_resize = false;
                }
            }

            GUI.Box(r, "", st.panel);
            if (Repainting())
            {
                // The bar across the top, as on the game's own windows: three dots, then the address.
                Rect bar = new Rect(r.x + 1.5f, r.y + 1.5f, r.width - 3f, 58f);
                GUI.Box(bar, "", st.bar);
                GUI.DrawTexture(new Rect(bar.x, bar.yMax - 14f, bar.width, 14f), tBarFlat);
                GUI.DrawTexture(new Rect(bar.x, bar.yMax, bar.width, 1f), tLine);
                Color keepDots = GUI.color;
                for (int i = 0; i < 3; i++)
                {
                    Color dot = Dots[i];
                    GUI.color = new Color(dot.r * s_dim, dot.g * s_dim, dot.b * s_dim, keepDots.a);
                    GUI.DrawTexture(new Rect(r.x + 20f + i * 20f, r.y + 25f, 12f, 12f), tCircle);
                }
                GUI.color = keepDots;
            }
            GUILayout.BeginArea(r);
            GUILayout.BeginVertical(st.inner);

            GUILayout.BeginHorizontal();
            GUILayout.Space(66f);
            GUILayout.Label("the-net://cma-shake/" + (collapsed ? "bar" : s_tab == TabRide ? "sex" : TabSlugs[Mathf.Clamp(s_tab, 0, TabSlugs.Length - 1)]) + "   <color=#6B7280>v" + Plugin.PluginVersion + "</color>", st.addr, GUILayout.Height(30f), GUILayout.ExpandWidth(true));
            GUILayout.Space(10f);
            int lang = Loc.Ru ? 0 : 1;
            int langNext = Choice(st, new[] { "RU", "EN" }, lang, 46f, 0f);
            if (langNext != lang)
            {
                Plugin.Language.Value = langNext == 0 ? "ru" : "en";
                MarkDirty();
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(collapsed ? "+" : "–", st.close, GUILayout.Width(32f), GUILayout.Height(32f)))
            {
                Plugin.WindowCollapsed.Value = !collapsed;
                s_capture = null;
                s_resize = false;
                MarkDirty();
                SaveIfDirty();
            }
            Tip(collapsed ? T("Unfold the window.", "Развернуть окно.") : T("Fold the window to a small bar with the start buttons.", "Свернуть окно в маленькую панель с кнопками запуска."));
            if (GUILayout.Button("X", st.close, GUILayout.Width(32f), GUILayout.Height(32f)))
            {
                SetOpen(host, false);
            }
            GUILayout.EndHorizontal();

            if (collapsed)
            {
                DrawQuickBar(host, st);
                GUILayout.EndVertical();
                GUILayout.EndArea();
                FinishFrame(ev);
                return;
            }

            GUILayout.Space(12f);

            // The pages, as the bookmarks of the game's browser: a tile each, with a coloured square.
            string[] tabs = { T("Home", "Главная"), T("Twerk", "Тверк"), T("Sex", "Секс"), T("Bikini", "Бикини"), T("Profiles", "Профили"), T("Settings", "Настройки") };
            GUILayout.BeginHorizontal();
            float tabWidth = (Width - 2f * Pad - 6f * (tabs.Length - 1)) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                if (i > 0)
                {
                    GUILayout.Space(6f);
                }
                if (GUILayout.Button("", i == s_tab ? st.appOn : st.app, GUILayout.Width(tabWidth - 0.5f), GUILayout.Height(74f)))
                {
                    s_tab = i;
                    s_capture = null;
                    s_scroll = Vector2.zero;
                }
                TileFace(st, GUILayoutUtility.GetLastRect(), TabColors[i], Mark(i), tabs[i], null, true, Icons.Get("tab." + TabSlugs[i]));
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);

            if (s_tab == TabRide)
            {
                DrawRideHeader(host, st);
            }
            s_scroll = GUILayout.BeginScrollView(s_scroll, GUILayout.ExpandHeight(true));
            switch (s_tab)
            {
                case TabTwerk: DrawTwerk(host, st); break;
                case TabRide: DrawRideContent(host, st); break;
                case TabBikini: DrawBikini(host, st); break;
                case TabProfiles: DrawProfiles(host, st); break;
                case TabSettings: DrawSettings(host, st); break;
                default: DrawHome(host, st); break;
            }
            GUILayout.EndScrollView();
            Rect pageRect = GUILayoutUtility.GetLastRect();

            // One line at the bottom: what the setting under the pointer does, or the keys when the pointer is on nothing.
            GUILayout.Space(6f);
            if (!string.IsNullOrEmpty(s_tipShown))
            {
                Rect slot = GUILayoutUtility.GetRect(10f, 40f, GUILayout.ExpandWidth(true), GUILayout.Height(40f));
                TipBox(st, slot, pageRect, ev);
            }
            else
            {
                GUILayout.BeginHorizontal(GUILayout.Height(40f));
                Hint(st, Plugin.MenuKey.Value, T("menu", "меню"));
                Hint(st, Plugin.ToggleKey.Value, T("twerk", "тверк"));
                Hint(st, Plugin.RideKey.Value, T("sex", "секс"));
                Hint(st, Plugin.BikiniKey.Value, T("bikini", "бикини"));
                Hint(st, Plugin.PresetKey.Value, T("mood", "настрой"));
                GUILayout.Label("Esc", st.keycap);
                GUILayout.Label(T("close", "закрыть"), st.hint, GUILayout.Height(30f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();

            // The corner that changes the height: three short diagonal strokes.
            if (Repainting())
            {
                Color keep = GUI.color;
                bool hot = s_resize || (ev != null && grip.Contains(ev.mousePosition));
                GUI.color = new Color(1f, 1f, 1f, (hot ? 0.75f : 0.3f) * keep.a);
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        if (i + j >= 2)
                        {
                            GUI.DrawTexture(new Rect(grip.x + 6f + i * 5f, grip.y + 6f + j * 5f, 3f, 3f), tWhitePill);
                        }
                    }
                }
                GUI.color = keep;
            }

            FinishFrame(ev);
        }

        // The help of the setting under the pointer. In the two lines at the bottom when it fits; a longer one (most of
        // them: a box of 40 pixels showed their first two lines and cut the rest) in a box over the page that is as tall
        // as its words, rising from the bottom, or hanging from the top of the page when the pointer is down there (the
        // box would hide what it tells of).
        private static readonly GUIContent s_tipContent = new GUIContent();

        private static void TipBox(Styles st, Rect slot, Rect page, Event ev)
        {
            if (!Repainting())
            {
                return;
            }
            s_tipContent.text = s_tipShown;
            if (st.tip.CalcHeight(s_tipContent, slot.width) <= slot.height)
            {
                GUI.Label(slot, s_tipContent, st.tip);
                return;
            }
            float h = Mathf.Min(st.tipBox.CalcHeight(s_tipContent, slot.width), Mathf.Max(slot.height, page.height + slot.height));
            Rect box = new Rect(slot.x, slot.yMax - h, slot.width, h);
            if (ev != null && box.Contains(ev.mousePosition))
            {
                box.y = page.y;
            }
            GUI.Label(box, s_tipContent, st.tipBox);
        }

        // The end of every frame of the window, folded or not.
        private static void FinishFrame(Event ev)
        {
            if (Repainting())
            {
                s_tipShown = GUI.tooltip;
            }

            string focus = GUI.GetNameOfFocusedControl();
            s_typing = focus == "search" || focus == "hex" || focus == "profile";

            if (ev != null && ev.rawType == EventType.MouseUp)
            {
                SaveIfDirty();
            }
        }

        // The folded window: the three start buttons and nothing else.
        private static void DrawQuickBar(ShakeController host, Styles st)
        {
            bool twerk = host.Twerking;
            bool ride = host.Riding;
            bool bikini = Bikini.AnyOn();
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Twerk", "Тверк"), twerk ? st.bigOn : st.big, GUILayout.Height(40f)))
            {
                host.ToggleMode(false);
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(T("Sex", "Секс"), ride ? st.bigOn : st.big, GUILayout.Height(40f)))
            {
                host.ToggleMode(true);
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(T("Bikini", "Бикини"), bikini ? st.bigOn : st.big, GUILayout.Height(40f)))
            {
                host.ToggleBikini();
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(Loc.Preset(Plugin.PresetName.Value), st.big, GUILayout.Width(156f), GUILayout.Height(40f)))
            {
                host.CyclePreset();
            }
            Tip(T("The mood: click for the next one.", "Настрой: щелчок — следующий."));
            GUILayout.EndHorizontal();
        }
    }
}
