using System;
using System.Collections.Generic;
using Assets.TValle.Tools.Runtime.Moddding.Clothing.Maps;
using BepInEx.Configuration;
using UnityEngine;

namespace CMAScenes
{
    internal static partial class Menu
    {
        private sealed class Entry
        {
            public ClothingItemMap map;
            public string id;
            public string name;
            public string lower;
            public ClothingItemMap.Type type;
        }

        private static readonly Color[] Presets =
        {
            Hex("#D81B60"), Hex("#E53935"), Hex("#FB8C00"), Hex("#FDD835"), Hex("#9CCC24"), Hex("#43A047"),
            Hex("#00ACC1"), Hex("#29B6F6"), Hex("#1E88E5"), Hex("#8E24AA"), Hex("#FFFFFF"), Hex("#212121")
        };

        private static Vector2 s_listScroll;

        private static List<Entry> s_entries = new List<Entry>();
        private static List<Entry> s_shown = new List<Entry>();
        private static string s_shownKey = "";
        private static bool s_pickBottom;
        private static bool s_showAll;
        private static string s_search = "";
        private static string s_hex = "";
        private static string s_topKey, s_bottomKey;
        private static Entry s_top, s_bottom;
        private static int s_version;

        private static Color CurrentColor()
        {
            return ColorUtility.TryParseHtmlString(Plugin.BikiniColor.Value, out Color c) ? c : Presets[0];
        }

        // ---- clothing list ----

        // Reads the clothing items the game has loaded. They are only there once a game is running.
        private static void Refresh()
        {
            var list = new List<Entry>();
            var seen = new HashSet<string>();
            foreach (ClothingItemMap m in Bikini.LoadMaps())
            {
                if (m == null || string.IsNullOrEmpty(m.id) || m.sex == ClothingItemMap.Sex.male || !seen.Add(m.id))
                {
                    continue;
                }
                string name = !string.IsNullOrWhiteSpace(m.displayId) ? m.displayId : m.name;
                list.Add(new Entry { map = m, id = m.id, name = name, lower = (name + " " + m.id + " " + m.name).ToLowerInvariant(), type = m.type });
            }
            list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            s_entries = list;
            s_version++;
            s_shownKey = "";
            s_topKey = null;
            s_bottomKey = null;
            Plugin.ModLog.LogInfo($"Menu: {list.Count} clothing item(s) available for the bikini.");
        }

        // What belongs in a bikini list: swimwear and lingerie, found by the kind the game gives the item and by its
        // name, minus anything that is plainly another sort of clothing (a dress that is typed as swimwear, a shoe...).
        private static readonly string[] SwimWords = { "bikini", "bra", "bralette", "bandeau", "thong", "string", "tanga", "panty", "panties", "brief", "lingerie", "swim", "monokini", "microkini" };
        private static readonly string[] TopWords = { "bra", "top", "bandeau", "bikini", "breast", "bralette" };
        private static readonly string[] BottomWords = { "bot", "bottom", "panty", "panties", "brief", "thong", "string", "tanga", "gstring", "crotch" };
        private static readonly string[] OtherClothes =
        {
            "dress", "skirt", "gown", "shirt", "jacket", "coat", "pants", "jeans", "shorts", "hoodie", "sweater", "blouse", "romper", "jumpsuit",
            "catsuit", "shoe", "boot", "heel", "sock", "stocking", "glove", "hat", "glasses", "brace", "brand", "necklace", "earring", "jewel", "belt"
        };

        private static bool HasAny(string text, string[] words)
        {
            foreach (string w in words)
            {
                if (text.Contains(w))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsSwimwear(Entry e)
        {
            if (HasAny(e.lower, OtherClothes))
            {
                return false;
            }
            return e.type == ClothingItemMap.Type.swimsuit || e.type == ClothingItemMap.Type.upperBodyUnderwear
                || e.type == ClothingItemMap.Type.lowerBodyUnderwear || HasAny(e.lower, SwimWords);
        }

        private static bool IsTop(Entry e)
        {
            if (e.type == ClothingItemMap.Type.upperBodyUnderwear || e.type == ClothingItemMap.Type.swimsuit)
            {
                return true;
            }
            bool bottom = HasAny(e.lower, BottomWords);
            return e.type != ClothingItemMap.Type.lowerBodyUnderwear && (!bottom || HasAny(e.lower, TopWords));
        }

        private static bool IsBottom(Entry e)
        {
            if (e.type == ClothingItemMap.Type.lowerBodyUnderwear || e.type == ClothingItemMap.Type.swimsuit)
            {
                return true;
            }
            bool top = HasAny(e.lower, TopWords);
            return e.type != ClothingItemMap.Type.upperBodyUnderwear && (!top || HasAny(e.lower, BottomWords));
        }

        private static void RebuildShown()
        {
            string sort = Plugin.BikiniSort.Value ?? "";
            string key = $"{s_version}|{s_pickBottom}|{s_showAll}|{s_search}|{sort}";
            if (key == s_shownKey)
            {
                return;
            }
            s_shownKey = key;
            s_shown = new List<Entry>();
            string q = (s_search ?? "").Trim().ToLowerInvariant();
            foreach (Entry e in s_entries)
            {
                if (!s_showAll && !(IsSwimwear(e) && (s_pickBottom ? IsBottom(e) : IsTop(e))))
                {
                    continue;
                }
                if (q.Length > 0 && !e.lower.Contains(q))
                {
                    continue;
                }
                s_shown.Add(e);
            }
            // (The whole list comes sorted by name, A to Z.)
            if (string.Equals(sort, "NameDesc", StringComparison.OrdinalIgnoreCase))
            {
                s_shown.Reverse();
            }
            else if (string.Equals(sort, "Kind", StringComparison.OrdinalIgnoreCase))
            {
                s_shown.Sort((a, b) =>
                {
                    int byKind = string.Compare(a.type.ToString(), b.type.ToString(), StringComparison.OrdinalIgnoreCase);
                    return byKind != 0 ? byKind : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
                });
            }
        }

        // The items the Top and Bottom settings stand for right now, worked out again only when a setting changes.
        private static void ResolvePicks()
        {
            if (s_topKey != Plugin.BikiniTop.Value)
            {
                s_topKey = Plugin.BikiniTop.Value;
                s_top = FindEntry(s_topKey, false);
            }
            if (s_bottomKey != Plugin.BikiniBottom.Value)
            {
                s_bottomKey = Plugin.BikiniBottom.Value;
                s_bottom = FindEntry(s_bottomKey, true);
            }
        }

        private static Entry FindEntry(string setting, bool bottom)
        {
            var maps = new List<ClothingItemMap>();
            foreach (Entry e in s_entries)
            {
                maps.Add(e.map);
            }
            ClothingItemMap hit = Bikini.Resolve(maps, setting, bottom);
            foreach (Entry e in s_entries)
            {
                if (hit != null && e.map == hit)
                {
                    return e;
                }
            }
            return null;
        }

        // ---- Bikini ----

        private static void DrawBikini(ShakeController host, Styles st)
        {
            ResolvePicks();

            bool on = Bikini.AnyOn();
            CardBegin(st, null);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label(T("Bikini", "Бикини"), st.dim);
            GUILayout.Label(on ? T("Worn", "Надето") : T("Not worn", "Не надето"), on ? st.statusOn : st.status);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(on ? T("Take it off", "Снять") : T("Put it on", "Надеть"), on ? st.button : st.primary, GUILayout.Width(150f), GUILayout.Height(40f)))
            {
                host.ToggleBikini();
            }
            GUILayout.EndHorizontal();
            CardEnd();

            CardBegin(st, T("Pieces", "Что надеть"));
            PickRow(host, st, T("Top", "Верх"), s_top, Plugin.BikiniTop);
            PickRow(host, st, T("Bottom", "Низ"), s_bottom, Plugin.BikiniBottom);
            if (s_entries.Count > 0)
            {
                bool noTop = s_top == null;
                bool noBottom = s_bottom == null;
                if (noTop && noBottom)
                {
                    GUILayout.Label(T("Pick a top and a bottom from the list: only the clothes the bikini replaces are taken off.", "Выберите верх и низ в списке: снимается только та одежда, которую бикини заменяет."), st.dim);
                }
                else if (noTop || noBottom)
                {
                    GUILayout.Label(noTop ? T("No top picked: what she wears on top stays on.", "Верх не выбран: то, что на ней сверху, остаётся.")
                        : T("No bottom picked: what she wears below stays on.", "Низ не выбран: то, что на ней снизу, остаётся."), st.dim);
                }
            }
            Gap(6f);

            GUILayout.BeginHorizontal();
            int pick = Choice(st, new[] { T("Top", "Верх"), T("Bottom", "Низ") }, s_pickBottom ? 1 : 0, 84f);
            s_pickBottom = pick == 1;
            Tip(T("Which of the two the list below picks.", "Что из двух выбирается в списке ниже."));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(T("Refresh list", "Обновить список"), st.ghost, GUILayout.Height(32f)))
            {
                Refresh();
            }
            Tip(T("Reads the installed clothing again. Clothing is only there once a game is loaded.", "Заново читает установленную одежду. Она появляется только после загрузки игры."));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("search");
            s_search = GUILayout.TextField(s_search, st.field);
            if (s_search.Length == 0 && Repainting() && GUI.GetNameOfFocusedControl() != "search")
            {
                Rect fr = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(fr.x + 12f, fr.y, fr.width - 12f, fr.height), T("Search clothing...", "Поиск одежды..."), st.placeholder);
            }
            GUILayout.Space(10f);
            s_showAll = SwitchWide(st, T("All clothes", "Вся одежда"), s_showAll, 160f);
            Tip(T("Off: only swimwear and lingerie. On: every piece of clothing installed.", "Выключено: только купальники и бельё. Включено: вся установленная одежда."));
            GUILayout.EndHorizontal();

            ChoiceRow(st, T("Sort", "Сортировка"), new[] { T("A to Z", "А → Я"), T("Z to A", "Я → А"), T("By kind", "По типу") }, new[] { "Name", "NameDesc", "Kind" }, Plugin.BikiniSort, 100f,
                T("The order of the list below: by name, by name backwards, or by the kind of clothing and then by name.", "Порядок списка ниже: по имени, по имени в обратную сторону или по типу одежды, а внутри типа по имени."));

            RebuildShown();
            if (s_entries.Count == 0)
            {
                GUILayout.Label(T("No clothing is loaded yet. Start or load a game, then press Refresh list.", "Одежда ещё не загружена. Запустите или загрузите игру и нажмите «Обновить список»."), st.dim);
            }
            else if (s_shown.Count == 0)
            {
                GUILayout.Label(s_showAll || s_search.Length > 0 ? T("Nothing matches.", "Ничего не найдено.")
                    : T("No swimwear or lingerie of this kind is installed. Turn on All clothes to see the rest.", "Купальников и белья такого вида не установлено. Включите «Вся одежда», чтобы увидеть остальное."), st.dim);
            }

            s_listScroll = GUILayout.BeginScrollView(s_listScroll, GUILayout.Height(236f));
            Entry chosen = s_pickBottom ? s_bottom : s_top;
            int shown = 0;
            foreach (Entry e in s_shown)
            {
                if (shown++ >= 250)
                {
                    GUILayout.Label(T("More items: type in the search box to narrow the list.", "Есть ещё: введите что-нибудь в поиск, чтобы сузить список."), st.dim);
                    break;
                }
                bool sel = chosen != null && chosen.id == e.id;
                string text = e.name + "\n<size=12><color=#8D8DA3>" + e.id + "</color></size>";
                if (GUILayout.Button(text, sel ? st.rowSel : st.row, GUILayout.Height(48f)))
                {
                    Pick(host, e);
                }
            }
            GUILayout.EndScrollView();
            CardEnd();

            CardBegin(st, T("Colour", "Цвет"));
            Color cur = CurrentColor();
            GUILayout.BeginHorizontal();
            foreach (Color p in Presets)
            {
                Color bg = GUI.backgroundColor;
                GUI.backgroundColor = Dim(p);
                bool clicked = GUILayout.Button("", st.chip, GUILayout.Width(30f), GUILayout.Height(30f));
                GUI.backgroundColor = bg;
                if (Repainting() && SameColor(p, cur))
                {
                    Rect cr = GUILayoutUtility.GetLastRect();
                    GUI.DrawTexture(new Rect(cr.x - 5f, cr.y - 5f, cr.width + 10f, cr.height + 10f), tRing);
                }
                if (clicked)
                {
                    SetColor(p);
                    cur = p;
                }
            }
            GUILayout.EndHorizontal();
            Gap(6f);

            float r = SliderRow(st, T("Red", "Красный"), cur.r, 0f, 1f, "0", " %", 100f, Hex("#FF5A5A"), float.NaN, out bool _);
            float g = SliderRow(st, T("Green", "Зелёный"), cur.g, 0f, 1f, "0", " %", 100f, Hex("#4CD97B"), float.NaN, out bool _);
            float b = SliderRow(st, T("Blue", "Синий"), cur.b, 0f, 1f, "0", " %", 100f, Hex("#5AA9FF"), float.NaN, out bool _);
            if (Mathf.Abs(r - cur.r) > 0.0005f || Mathf.Abs(g - cur.g) > 0.0005f || Mathf.Abs(b - cur.b) > 0.0005f)
            {
                SetColor(new Color(r, g, b, 1f));
                cur = CurrentColor();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Colour code", "Код цвета"), st.label, GUILayout.Width(LabelWidth), GUILayout.Height(32f));
            if (GUI.GetNameOfFocusedControl() != "hex")
            {
                s_hex = "#" + ColorUtility.ToHtmlStringRGB(cur);
            }
            GUI.SetNextControlName("hex");
            string typed = GUILayout.TextField(s_hex, 7, st.field, GUILayout.Width(120f));
            if (typed != s_hex)
            {
                s_hex = typed;
                if (typed.Length == 7 && ColorUtility.TryParseHtmlString(typed, out Color parsed))
                {
                    SetColor(parsed);
                    cur = parsed;
                }
            }
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = Dim(cur);
            GUILayout.Box("", st.preview, GUILayout.Width(70f), GUILayout.Height(32f));
            GUI.backgroundColor = old;
            GUILayout.EndHorizontal();
            Tip(T("The colour as a code, such as #D81B60. It changes at once while she wears the bikini.", "Цвет в виде кода, например #D81B60. Меняется сразу, пока бикини на ней."));
            CardEnd();

            CardBegin(st, T("Options", "Дополнительно"));
            bool solid = SwitchRow(st, T("Plain colour (hide the item's own pattern)", "Однотонный цвет (без рисунка вещи)"), Plugin.BikiniSolid.Value);
            Tip(T("On: the piece is one flat colour. Off: its own pattern shows through, tinted.", "Включено: вещь одного ровного цвета. Выключено: виден её рисунок, подкрашенный цветом."));
            if (solid != Plugin.BikiniSolid.Value)
            {
                Plugin.BikiniSolid.Value = solid;
                MarkDirty();
                host.ReapplyBikini();
            }
            Check(st, T("Put it on by itself for the twerk and for sex", "Надевать само на время тверка и секса"), Plugin.BikiniWithTwerk,
                T("The bikini goes on when the twerk or the sex starts and comes off when it stops.", "Бикини надевается, когда начинается тверк или секс, и снимается, когда он заканчивается."));
            CardEnd();

            ResetButton(st, T("Reset the bikini settings", "Сбросить настройки бикини"), () =>
            {
                ResetAll(Plugin.BikiniTop, Plugin.BikiniBottom, Plugin.BikiniColor, Plugin.BikiniSolid, Plugin.BikiniWithTwerk);
                Bikini.Retint();
                host.ReapplyBikini();
            });
        }

        private static bool SameColor(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;
        }

        private static void PickRow(ShakeController host, Styles st, string name, Entry current, ConfigEntry<string> setting)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, st.dim, GUILayout.Width(64f), GUILayout.Height(30f));
            string text;
            if (string.IsNullOrWhiteSpace(setting.Value))
            {
                text = T("Nothing picked", "Не выбрано");
            }
            else if (current != null)
            {
                text = current.name;
            }
            else
            {
                text = T("Not found: ", "Не найдено: ") + setting.Value;
            }
            // (A long name wraps in what is left of the row: on one line it pushed the button out of the window.)
            float width = RowWidth - 64f - 90f - 8f;
            s_measure.text = text;
            float h = Mathf.Max(30f, st.labelWrap.CalcHeight(s_measure, width));
            GUILayout.Label(text, st.labelWrap, GUILayout.Width(width), GUILayout.Height(h));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(T("Clear", "Убрать"), st.ghost, GUILayout.Width(90f), GUILayout.Height(30f)))
            {
                setting.Value = "";
                MarkDirty();
                host.ReapplyBikini();
            }
            GUILayout.EndHorizontal();
        }

        private static void Pick(ShakeController host, Entry e)
        {
            (s_pickBottom ? Plugin.BikiniBottom : Plugin.BikiniTop).Value = e.id;
            MarkDirty();
            host.ReapplyBikini();
        }

        private static void SetColor(Color c)
        {
            Plugin.BikiniColor.Value = "#" + ColorUtility.ToHtmlStringRGB(c);
            MarkDirty();
            Bikini.Retint();
        }
    }
}
