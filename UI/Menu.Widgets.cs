using System;
using System.Collections.Generic;
using Assets.TValle.Tools.Runtime.Moddding.Clothing.Maps;
using BepInEx.Configuration;
using UnityEngine;

namespace CMAScenes
{
    internal static partial class Menu
    {
        // ---- widgets ----

        // What is on a tile (a button that was drawn with no text of its own): a coloured square with a picture (or a letter or two),
        // a name, and under it, if there is one, a note.
        private static void TileFace(Styles st, Rect rect, Color colour, string mark, string name, string note, bool stacked = false, Texture2D picture = null)
        {
            if (!Repainting())
            {
                return;
            }
            Color keep = GUI.color;
            float size = stacked ? 40f : Mathf.Min(36f, rect.height - 16f);
            Rect icon = stacked
                ? new Rect(rect.x + (rect.width - size) * 0.5f, rect.y + 7f, size, size)
                : new Rect(rect.x + 10f, rect.y + (rect.height - size) * 0.5f, size, size);
            GUI.color = new Color(colour.r * s_dim, colour.g * s_dim, colour.b * s_dim, keep.a);
            GUI.DrawTexture(icon, tIcon);
            // (Dark letters on the light squares, white on the others.)
            bool light = 0.299f * colour.r + 0.587f * colour.g + 0.114f * colour.b > 0.6f;
            GUI.color = light ? new Color(0.11f, 0.12f, 0.15f, keep.a) : keep;
            // The page's picture on its square (see Icons); the letters only if there is none.
            if (picture != null)
            {
                float inset = Mathf.Round(size * 0.16f);
                GUI.DrawTexture(new Rect(icon.x + inset, icon.y + inset, size - 2f * inset, size - 2f * inset), picture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Label(icon, mark, st.iconText);
            }
            GUI.color = keep;
            // (Each text a size smaller until it fits: a long name or a long key was cut off at the tile's edge.)
            if (stacked)
            {
                GUI.Label(new Rect(rect.x + 2f, icon.yMax + 3f, rect.width - 4f, 20f), name, Fit(st, st.tileUnder, name, rect.width - 6f));
                return;
            }
            Rect text = new Rect(icon.xMax + 10f, rect.y, rect.xMax - icon.xMax - 14f, rect.height);
            if (string.IsNullOrEmpty(note))
            {
                GUI.Label(text, name, Fit(st, st.tileText, name, text.width));
                return;
            }
            GUI.Label(new Rect(text.x, rect.y + rect.height * 0.5f - 20f, text.width, 22f), name, Fit(st, st.tileText, name, text.width));
            GUI.Label(new Rect(text.x, rect.y + rect.height * 0.5f + 1f, text.width, 18f), note, Fit(st, st.tileSub, note, text.width));
        }

        // The style a text is drawn with so that it fits on one line in the width given: the style itself if it fits,
        // else a copy of it a size or more smaller (no smaller than 9).
        private static readonly Dictionary<GUIStyle, Dictionary<int, GUIStyle>> s_fit = new Dictionary<GUIStyle, Dictionary<int, GUIStyle>>();
        private static Styles s_fitFor;
        private static readonly GUIContent s_measure = new GUIContent();

        private static GUIStyle Fit(Styles st, GUIStyle style, string text, float width)
        {
            if (string.IsNullOrEmpty(text) || width <= 0f)
            {
                return style;
            }
            if (s_fitFor != st)
            {
                s_fitFor = st;
                s_fit.Clear();
            }
            s_measure.text = text;
            if (style.CalcSize(s_measure).x <= width)
            {
                return style;
            }
            if (!s_fit.TryGetValue(style, out Dictionary<int, GUIStyle> sizes))
            {
                sizes = new Dictionary<int, GUIStyle>();
                s_fit[style] = sizes;
            }
            int from = style.fontSize > 0 ? style.fontSize : 14;
            GUIStyle smaller = style;
            for (int size = from - 1; size >= 9; size--)
            {
                if (!sizes.TryGetValue(size, out smaller))
                {
                    smaller = new GUIStyle(style) { fontSize = size };
                    sizes[size] = smaller;
                }
                if (smaller.CalcSize(s_measure).x <= width)
                {
                    break;
                }
            }
            return smaller;
        }

        // How tall a row is for its label to be read whole (wrapped in the label's column), and the label itself.
        private static readonly Dictionary<string, float> s_rowH = new Dictionary<string, float>();
        private static Styles s_rowHFor;

        private static float RowHeight(Styles st, string label, float least)
        {
            if (s_rowHFor != st)
            {
                s_rowHFor = st;
                s_rowH.Clear();
            }
            if (string.IsNullOrEmpty(label))
            {
                return least;
            }
            if (!s_rowH.TryGetValue(label, out float h))
            {
                s_measure.text = label;
                h = st.labelWrap.CalcHeight(s_measure, LabelWidth) + 4f;
                s_rowH[label] = h;
            }
            return Mathf.Max(least, h);
        }

        private static void RowLabel(Styles st, string label, float height)
        {
            GUILayout.Label(label, st.labelWrap, GUILayout.Width(LabelWidth), GUILayout.Height(height));
        }

        // A heading as the game writes them: small capitals set wide apart.
        private static readonly Dictionary<string, string> s_spaced = new Dictionary<string, string>();

        private static string Spaced(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            if (!s_spaced.TryGetValue(text, out string made))
            {
                var sb = new System.Text.StringBuilder();
                foreach (char c in text.ToUpperInvariant())
                {
                    sb.Append(c == ' ' ? "  " : c + " ");
                }
                made = sb.ToString().TrimEnd();
                s_spaced[text] = made;
            }
            return made;
        }

        private static void Hint(Styles st, KeyCode key, string what)
        {
            if (key == KeyCode.None)
            {
                return;
            }
            GUILayout.Label(key.ToString(), st.keycap);
            // (As wide as its word: in a fixed 58 the longer Russian words were cut.)
            GUILayout.Label(what, st.hint, GUILayout.Height(30f));
            GUILayout.Space(6f);
        }

        private static void CardBegin(Styles st, string title)
        {
            GUILayout.BeginVertical(st.card);
            if (!string.IsNullOrEmpty(title))
            {
                GUILayout.Label(Spaced(title), st.h2);
            }
        }

        private static void CardEnd()
        {
            GUILayout.EndVertical();
        }

        // A card that folds: its heading is a button that opens and closes it. Returns whether what is in it is to be drawn.
        // Always followed by CardEnd, open or not.
        private static bool FoldBegin(Styles st, string key, string title, string closedNote, bool openAtFirst)
        {
            GUILayout.BeginVertical(st.card);
            if (!s_foldsLoaded)
            {
                s_foldsLoaded = true;
                foreach (string part in (Plugin.UiFolds.Value ?? "").Split(';'))
                {
                    int eq = part.IndexOf('=');
                    if (eq > 0)
                    {
                        s_folds[part.Substring(0, eq)] = part.EndsWith("1", StringComparison.Ordinal);
                    }
                }
            }
            if (!s_folds.TryGetValue(key, out bool open))
            {
                open = openAtFirst;
            }
            bool openWas = open;
            string text = (open ? "–   " : "+   ") + Spaced(title);
            if (!open && !string.IsNullOrEmpty(closedNote))
            {
                string withNote = text + "     <size=12><color=#6B7280>" + closedNote + "</color></size>";
                // (The note only where it fits beside the heading.)
                s_measure.text = withNote;
                if (st.fold.CalcSize(s_measure).x <= RowWidth)
                {
                    text = withNote;
                }
            }
            if (GUILayout.Button(text, st.fold, GUILayout.Height(26f)))
            {
                open = !open;
            }
            s_folds[key] = open;
            if (open != openWas)
            {
                var kept = new System.Text.StringBuilder();
                foreach (KeyValuePair<string, bool> f in s_folds)
                {
                    kept.Append(kept.Length > 0 ? ";" : "").Append(f.Key).Append('=').Append(f.Value ? '1' : '0');
                }
                Plugin.UiFolds.Value = kept.ToString();
                MarkDirty();
            }
            if (open)
            {
                GUILayout.Space(6f);
            }
            return open;
        }

        private static bool Repainting()
        {
            Event e = Event.current;
            return e != null && e.type == EventType.Repaint;
        }

        // Gives the control (or row) just drawn a line of help, shown at the bottom of the window while the pointer is on it.
        private static void Tip(string tip)
        {
            if (string.IsNullOrEmpty(tip) || !Repainting())
            {
                return;
            }
            if (!s_tips.TryGetValue(tip, out GUIContent content))
            {
                content = new GUIContent("", tip);
                s_tips[tip] = content;
            }
            GUI.Label(GUILayoutUtility.GetLastRect(), content, GUIStyle.none);
        }

        // A slider with a filled track, a round handle and a small mark where its default is. Returns the new value;
        // a right click on the row asks for the default.
        private static float SliderRow(Styles st, string label, float value, float min, float max, string format, string unit, float scale, Color fill, float mark, out bool reset)
        {
            // (A label longer than its column takes two lines, and the slider stands in the middle of the row: the label
            // was cut off at the column's edge, on one line of a fixed height.)
            float rowH = RowHeight(st, label, 30f);
            GUILayout.BeginHorizontal();
            RowLabel(st, label, rowH);
            GUILayout.BeginVertical(GUILayout.Height(rowH));
            GUILayout.FlexibleSpace();
            Rect r = GUILayoutUtility.GetRect(60f, ThumbSize, GUILayout.ExpandWidth(true), GUILayout.Height(ThumbSize));
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            float t = Mathf.InverseLerp(min, max, value);
            if (Repainting())
            {
                Color keep = GUI.color;
                float cy = r.y + r.height * 0.5f;
                GUI.DrawTexture(new Rect(r.x, cy - 4f, r.width, 8f), tTrack);
                float centre = r.x + ThumbSize * 0.5f + t * (r.width - ThumbSize);
                GUI.color = new Color(fill.r * s_dim, fill.g * s_dim, fill.b * s_dim, keep.a);
                GUI.DrawTexture(new Rect(r.x, cy - 4f, Mathf.Max(8f, centre - r.x), 8f), tWhitePill);
                if (!float.IsNaN(mark) && mark >= min && mark <= max)
                {
                    float mx = r.x + ThumbSize * 0.5f + Mathf.InverseLerp(min, max, mark) * (r.width - ThumbSize);
                    GUI.color = new Color(1f, 1f, 1f, 0.55f * keep.a);
                    GUI.DrawTexture(new Rect(mx - 1f, cy + 6f, 2f, 5f), tWhitePill);
                }
                GUI.color = keep;
            }
            float nv = GUI.HorizontalSlider(r, value, min, max, st.sliderTrack, st.sliderThumb);
            string shown = (nv * scale).ToString(format) + unit;
            GUILayout.Label(shown, Fit(st, st.value, shown, 72f), GUILayout.Width(72f), GUILayout.Height(rowH));
            GUILayout.EndHorizontal();

            reset = false;
            Event ev = Event.current;
            if (ev != null && ev.type == EventType.MouseDown && ev.button == 1 && GUILayoutUtility.GetLastRect().Contains(ev.mousePosition))
            {
                reset = true;
                ev.Use();
            }
            return nv;
        }

        private static void Slider(Styles st, string label, ConfigEntry<float> e, float min, float max, string format, string unit, float scale, string tip)
        {
            float v = e.Value;
            float def = e.DefaultValue is float f ? f : float.NaN;
            float nv = SliderRow(st, label, v, min, max, format, unit, scale, Accent, def, out bool reset);
            Tip(tip);
            if (reset && !float.IsNaN(def))
            {
                nv = def;
            }
            if (Mathf.Abs(nv - v) > 1e-6f)
            {
                e.Value = nv;
                MarkDirty();
                if (reset)
                {
                    SaveIfDirty();
                }
            }
        }

        // The units values are shown in: what is kept in metres is shown in centimetres.
        private static string Cm => T(" cm", " см");
        private static string PerSec => T(" /s", " /с");

        // An on/off switch with its label. Returns the new value.
        private static bool SwitchRow(Styles st, string label, bool value)
        {
            // (The long labels of the switches, "Tour: after her orgasm she moves on...", were cut off at the column's
            // edge: wrapped now, and the switch in the middle of the row.)
            float rowH = RowHeight(st, label, 30f);
            GUILayout.BeginHorizontal();
            RowLabel(st, label, rowH);
            GUILayout.BeginVertical(GUILayout.Width(48f), GUILayout.Height(rowH));
            GUILayout.FlexibleSpace();
            Rect r = GUILayoutUtility.GetRect(48f, 30f, GUILayout.Width(48f), GUILayout.Height(30f));
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            bool nv = GUI.Toggle(r, value, GUIContent.none, GUIStyle.none);
            if (Repainting())
            {
                GUI.DrawTexture(new Rect(r.x, r.y + 2f, 48f, 26f), value ? tSwitchOn : tSwitchOff);
            }
            GUILayout.Space(10f);
            GUILayout.Label(value ? T("on", "вкл") : T("off", "выкл"), st.hint, GUILayout.Height(rowH));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return nv;
        }

        private static void Check(Styles st, string label, ConfigEntry<bool> e, string tip)
        {
            bool nv = SwitchRow(st, label, e.Value);
            Tip(tip);
            if (nv != e.Value)
            {
                e.Value = nv;
                MarkDirty();
            }
        }

        // A row of choices, one of them selected. Returns the index chosen.
        private static int Choice(Styles st, string[] names, int selected, float itemWidth, float height = 28f)
        {
            ChoiceFit(st, names, ref itemWidth, ref height, out bool wrap);
            int result = selected;
            GUILayout.BeginHorizontal(st.seg, GUILayout.ExpandWidth(false));
            for (int i = 0; i < names.Length; i++)
            {
                GUIStyle style = i == selected ? (wrap ? st.optOnWrap : st.optOn) : (wrap ? st.optWrap : st.opt);
                if (GUILayout.Button(names[i], style, GUILayout.Width(itemWidth), GUILayout.Height(height)))
                {
                    result = i;
                }
            }
            GUILayout.EndHorizontal();
            return result;
        }

        // How wide the choices of a row are and how tall: wide enough for the longest name (a name wider than its chip
        // was cut off, "Прозрачный" among them); and where they cannot all be that wide in what is left of the row
        // after its label, as wide as they can, with the names on two lines.
        private static void ChoiceFit(Styles st, string[] names, ref float itemWidth, ref float height, out bool wrap)
        {
            wrap = false;
            height = height > 0f ? height : 26f;
            float need = 0f;
            foreach (string name in names)
            {
                s_measure.text = name;
                need = Mathf.Max(need, st.opt.CalcSize(s_measure).x + 4f);
            }
            itemWidth = Mathf.Max(itemWidth, need);
            // (However many there are, they share what is left of the row after its label: a row wider than the window
            // widens everything in it and pushes the values of the sliders out of sight.)
            float room = RowWidth - LabelWidth - 8f;
            float gaps = st.opt.margin.right * names.Length;
            if (names.Length * itemWidth + gaps > room)
            {
                itemWidth = Mathf.Floor((room - gaps) / names.Length);
                if (need > itemWidth)
                {
                    wrap = true;
                    float tall = 0f;
                    foreach (string name in names)
                    {
                        s_measure.text = name;
                        tall = Mathf.Max(tall, st.optWrap.CalcHeight(s_measure, itemWidth));
                    }
                    height = Mathf.Max(height, tall + 2f);
                }
            }
        }

        // The height a row of choices takes (for its label), with the same widths as Choice works out.
        private static float ChoiceHeight(Styles st, string[] names, float itemWidth)
        {
            float h = 28f;
            ChoiceFit(st, names, ref itemWidth, ref h, out bool _);
            return h + st.seg.margin.vertical + st.seg.padding.vertical;
        }

        private const float IconTileGap = 6f;
        private static readonly Color IconGrey = Hex("#B4BAC4");
        private static GUIStyle s_iconName, s_iconNameWrap;
        private static Styles s_iconNameFor;

        // The same as Choice, but each choice is a tile with its picture (from Icons) over its name: the poses, the
        // hands, the legs. A choice without a picture shows only its name.
        // How wide the tiles of a row are, how tall, and how much of each is its name: a name too long for one line takes
        // two or three (it was cut off at two: "Сзади на его бёдрах" on six tiles of 69 pixels), and the tile grows so
        // that its picture keeps at least some 26 pixels.
        private static void IconFit(Styles st, string[] names, ref float itemWidth, ref float height, out float nameHeight, out bool wrap)
        {
            // The names a size smaller than under the pages: the Russian names of the poses are long for the narrow tiles.
            if (s_iconNameFor != st || s_iconName == null)
            {
                s_iconName = new GUIStyle(st.tileUnder) { fontSize = 12 };
                s_iconNameWrap = new GUIStyle(s_iconName) { wordWrap = true, alignment = TextAnchor.LowerCenter, clipping = TextClipping.Overflow };
                s_iconNameFor = st;
            }
            // (They share what is left of the row after its label, gaps included, as in Choice.)
            float room = RowWidth - LabelWidth - 8f;
            if (names.Length * (itemWidth + IconTileGap) - IconTileGap > room)
            {
                itemWidth = Mathf.Floor((room + IconTileGap) / names.Length - IconTileGap);
            }
            wrap = false;
            nameHeight = 18f;
            foreach (string name in names)
            {
                s_measure.text = name;
                if (s_iconName.CalcSize(s_measure).x > itemWidth - 6f)
                {
                    wrap = true;
                    nameHeight = Mathf.Max(nameHeight, Mathf.Min(48f, s_iconNameWrap.CalcHeight(s_measure, itemWidth - 4f) + 2f));
                }
            }
            height = Mathf.Max(height, nameHeight + 6f + 26f + 3f);
        }

        private static int IconChoice(Styles st, string[] names, string[] icons, int selected, float itemWidth, float height)
        {
            int result = selected;
            // (A name too long for one line takes more, and the pictures of the whole row give up the room, so they stay
            // the same size.)
            IconFit(st, names, ref itemWidth, ref height, out float nameHeight, out bool twoLines);
            Event ev = Event.current;
            GUILayout.BeginHorizontal(st.seg, GUILayout.ExpandWidth(false));
            for (int i = 0; i < names.Length; i++)
            {
                if (i > 0)
                {
                    GUILayout.Space(IconTileGap);
                }
                bool on = i == selected;
                if (GUILayout.Button("", on ? st.tileOn : st.tile, GUILayout.Width(itemWidth), GUILayout.Height(height)))
                {
                    result = i;
                }
                if (!Repainting())
                {
                    continue;
                }
                Rect r = GUILayoutUtility.GetLastRect();
                Texture2D picture = icons != null && i < icons.Length ? Icons.Get(icons[i]) : null;
                if (picture == null)
                {
                    GUI.Label(new Rect(r.x + 2f, r.y, r.width - 4f, r.height), names[i], twoLines ? s_iconNameWrap : s_iconName);
                    continue;
                }
                // The picked one in the accent, the one under the pointer white, the others grey; all darkened as the
                // rest of the window is (the pictures are white, made at full brightness).
                Color keep = GUI.color;
                Color tint = on ? Color.Lerp(Accent, Color.white, 0.25f) : ev != null && r.Contains(ev.mousePosition) ? Color.white : IconGrey;
                GUI.color = new Color(tint.r * s_dim, tint.g * s_dim, tint.b * s_dim, keep.a);
                float size = Mathf.Min(height - nameHeight - 6f, r.width - 8f);
                GUI.DrawTexture(new Rect(r.x + (r.width - size) * 0.5f, r.y + 3f, size, size), picture, ScaleMode.ScaleToFit);
                GUI.color = keep;
                GUI.Label(new Rect(r.x + 2f, r.yMax - nameHeight - 3f, r.width - 4f, nameHeight), names[i], twoLines ? s_iconNameWrap : s_iconName);
            }
            GUILayout.EndHorizontal();
            return result;
        }

        // A named row of choices that sets a setting kept as text. With pictures (names in Icons), the choices are tiles.
        private static void ChoiceRow(Styles st, string label, string[] shown, string[] values, ConfigEntry<string> e, float itemWidth, string tip, string[] icons = null)
        {
            GUILayout.BeginHorizontal();
            float tileHeight = icons != null ? 66f : 0f;
            float rowH;
            if (icons != null)
            {
                float w = itemWidth;
                IconFit(st, shown, ref w, ref tileHeight, out float _, out bool _);
                rowH = tileHeight + 4f;
            }
            else
            {
                rowH = Mathf.Max(34f, ChoiceHeight(st, shown, itemWidth));
            }
            RowLabel(st, label, RowHeight(st, label, rowH));
            int cur = Array.FindIndex(values, v => string.Equals(v, e.Value, StringComparison.OrdinalIgnoreCase));
            int next = icons != null ? IconChoice(st, shown, icons, cur, itemWidth, tileHeight) : Choice(st, shown, cur, itemWidth);
            if (next != cur && next >= 0)
            {
                e.Value = values[next];
                MarkDirty();
            }
            GUILayout.EndHorizontal();
            Tip(tip);
        }

        private static void Gap(float height)
        {
            GUILayout.Space(height);
        }

        private static void ResetButton(Styles st, string text, Action reset)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            // (Wider than 300 where its words need it: "Сбросить клавиши и эти настройки" all but touched its edges.)
            s_measure.text = text;
            float width = Mathf.Clamp(st.button.CalcSize(s_measure).x + 12f, 300f, RowWidth);
            bool hit = GUILayout.Button(text, Fit(st, st.button, text, width - st.button.padding.horizontal), GUILayout.Width(width), GUILayout.Height(32f));
            GUILayout.EndHorizontal();
            if (hit)
            {
                reset();
            }
            Tip(T("Puts every setting of this page back to what it was when the mod was installed.", "Возвращает все настройки этой страницы к тем, что были при установке мода."));
        }

        private static void ResetAll(params ConfigEntryBase[] entries)
        {
            foreach (ConfigEntryBase e in entries)
            {
                e.BoxedValue = e.DefaultValue;
            }
            MarkDirty();
        }

        // A switch that is a fixed width, for sitting in a row next to other controls.
        private static bool SwitchWide(Styles st, string label, bool value, float width)
        {
            Rect r = GUILayoutUtility.GetRect(width, 32f, GUILayout.Width(width), GUILayout.Height(32f));
            bool nv = GUI.Toggle(r, value, label, st.switchText);
            if (Repainting())
            {
                GUI.DrawTexture(new Rect(r.x, r.y + 3f, 48f, 26f), value ? tSwitchOn : tSwitchOff);
            }
            return nv;
        }

        // ---- shared rows: the same setting can be shown on more than one page ----

        private static readonly string[] PresetValues = { "Slow", "Soft", "Fast", "Mine" };
        private static readonly string[] RhythmValues = { "Steady", "Random", "Scenario" };

        private static void PresetRow(Styles st)
        {
            ChoiceRow(st, T("Mood", "Настрой"), new[] { Loc.Preset("Slow"), Loc.Preset("Soft"), Loc.Preset("Fast"), Loc.Preset("Mine") }, PresetValues, Plugin.PresetName, 112f,
                T($"How fast and deep she moves, on top of your own settings. Sensual: unhurried, at full depth. Gentle: slower and softer. Wild: quicker. As set: exactly your settings. Key {Loc.Key(Plugin.PresetKey.Value)} switches it.",
                  $"Темп и размах поверх ваших настроек. Чувственно: неторопливо, на всю глубину. Нежно: медленнее и мягче. Страстно: быстрее. Как настроено: ровно ваши настройки. Клавиша {Loc.Key(Plugin.PresetKey.Value)} переключает."));
        }

        private static void RhythmRow(Styles st)
        {
            ChoiceRow(st, T("Rhythm", "Ритм"), new[] { Loc.Rhythm("Steady"), Loc.Rhythm("Random"), Loc.Rhythm("Scenario") }, RhythmValues, Plugin.Rhythm, 100f,
                T("Steady: one even movement. Improvised: she changes moves and pauses by herself. Scenario: a set sequence that loops (slow circles of her hips, strokes, a pause, faster, side to side).",
                  "Ровно: одно равномерное движение. Импровизация: сама меняет движения и делает паузы. Сценарий: заданная последовательность по кругу (медленные круги бёдрами, толчки, пауза, быстрее, из стороны в сторону)."));
        }

        // Kinds of pose: choosing one sets a bundle of the settings. What was set before is kept as the profile "_last".
        private static void PoseRow(Styles st, string label, Poses.Pose[] set, ConfigEntry<string> current)
        {
            string[] names = new string[set.Length];
            string[] icons = new string[set.Length];
            for (int i = 0; i < set.Length; i++)
            {
                names[i] = Loc.Pose(set[i].name);
                // (Each pose has its little figure in Icons, named after it: "Over him" is "pose.over-him".)
                icons[i] = "pose." + set[i].name.ToLowerInvariant().Replace(' ', '-');
            }
            int cur = Poses.IndexOf(set, current.Value);
            float itemWidth = set.Length > 3 ? 104f : 134f, tileHeight = 76f, fitWidth = itemWidth;
            IconFit(st, names, ref fitWidth, ref tileHeight, out float _, out bool _);
            GUILayout.BeginHorizontal();
            RowLabel(st, label, RowHeight(st, label, tileHeight + 4f));
            int next = IconChoice(st, names, icons, cur, itemWidth, 76f);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (next != cur && next >= 0)
            {
                Poses.Apply(set[next], current);
            }
            Tip(set == Poses.Ride
                ? T("A ready-made position; each of its settings can still be changed after. Cowgirl: she squats over him, feet on the floor, hands on her thighs. Astride: she kneels over him and leans on his chest. Over him: on her knees she bends forward over him, her hands on the floor by his shoulders. Leaning back: she leans back, her hands behind her on his thighs. Reverse: squatting with her back to him.",
                    "Готовая поза; её настройки потом можно менять по отдельности. Наездница: на корточках над ним, ступни на полу, руки на своих бёдрах. Верхом: на коленях, руками опирается на его грудь. Над ним: на коленях наклоняется над ним, руки на полу у его плеч. Откинувшись: отклоняется назад, руки сзади на его бёдрах. Спиной: на корточках спиной к нему.")
                : T("A ready-made pose: it sets several of the pose settings at once, and she moves into it smoothly. You can still adjust each of them after.",
                    "Готовая поза: задаёт сразу несколько настроек позы, и она плавно в неё переходит. После этого каждую можно подправить отдельно."));
        }

        private const float BigWidth = (Width - 107f) / 3f;

        private static readonly string[] HoleValues = { "Vagina", "Anus" };

        private static void HoleRow(Styles st)
        {
            ChoiceRow(st, T("He goes into", "Куда входит"), new[] { T("Her vagina", "В вагину"), T("Her anus", "В анус") }, HoleValues, Plugin.RideHole, 110f,
                T("Which of her holes she takes him in. Changed during sex, she comes up off him and takes him in the other one.",
                  "Каким отверстием она насаживается на него. Если сменить во время секса, она приподнимется и примет его в другое."),
                new[] { "hole.vagina", "hole.anus" });
        }

        // The condom: what is going on with it, and the button that gets one out or takes it off.
        private static void CondomRow(ShakeController host, Styles st)
        {
            int state = host.CondomState;
            string status = state == 0 ? T("not on", "не надет")
                : state == 1 ? T("lies on his belly: take it with his hand", "лежит на животе: возьмите рукой")
                : state == 2 ? T("in his hand: bring it to his tip", "в руке: поднесите к головке")
                : state == 3 ? T("going on...", "надевается...")
                : state == 4 ? T("on him", "надет") : T("coming off...", "снимается...");
            if (state == 4 && host.CondomMl > 0.05f)
            {
                status += T(", ", ", ") + host.CondomMl.ToString("0.0") + T(" ml of his semen in it", " мл спермы в нём");
            }
            string button = state == 0 ? T("Get one out", "Достать") : state == 4 ? T("Take it off", "Снять") : state == 1 || state == 2 ? T("Put it away", "Убрать") : "...";
            button += Plugin.CondomKey.Value != KeyCode.None ? "   [" + Plugin.CondomKey.Value + "]" : "";
            bool riding = host.CondomPossible;
            if (!riding)
            {
                status = T("the hero was not found yet", "герой пока не найден");
            }
            // (The words beside the button wrap in what is left of the row: they were cut to a fixed height.)
            GUIStyle look = state == 4 ? st.statusOnWrap : st.dim;
            float statusW = RowWidth - LabelWidth - 190f - 10f;
            s_measure.text = status;
            float rowH = Mathf.Max(34f, look.CalcHeight(s_measure, statusW) + 2f);
            GUILayout.BeginHorizontal();
            RowLabel(st, T("Condom", "Презерватив"), RowHeight(st, T("Condom", "Презерватив"), rowH));
            bool was = GUI.enabled;
            GUI.enabled = was && riding;
            GUIStyle press = state == 0 && riding ? st.primary : st.button;
            if (GUILayout.Button(button, Fit(st, press, button, 190f - press.padding.horizontal), GUILayout.Width(190f), GUILayout.Height(32f)))
            {
                host.ToggleCondom();
            }
            GUI.enabled = was;
            GUILayout.Space(10f);
            GUILayout.Label(status, look, GUILayout.Width(statusW), GUILayout.Height(rowH));
            GUILayout.EndHorizontal();
            Tip(T("At any time his penis is shown: outside sex it rolls straight on, and off again. During sex: she comes up off him and a rolled condom lies on his belly. Take it with the hero's hand (as you move his hands in the game) and bring it to his tip: it rolls on, and she sits down again. When he comes in it, its teat fills with as much as the game lets out of him, and swells with it. Press again to take it off.",
                  "В любой момент, когда его член виден: вне секса он сразу раскатывается по члену и так же снимается. Во время секса: она приподнимается, а свёрнутый презерватив лежит у героя на животе. Возьмите его рукой героя (так же, как двигаете руками в игре) и поднесите к головке — он раскатается, и она снова сядет. Когда он кончает в него, резервуар наполняется столько, сколько игра выпускает из него, и раздувается. Нажмите ещё раз, чтобы снять."));
        }

        // A big button with a name, what pressing it does, and its key.
        private static bool BigButton(Styles st, string name, string action, KeyCode key, bool on, int tab)
        {
            bool hit = GUILayout.Button("", on ? st.tileOn : st.tile, GUILayout.Width(BigWidth), GUILayout.Height(62f));
            TileFace(st, GUILayoutUtility.GetLastRect(), TabColors[tab], Mark(tab), name, action + (key != KeyCode.None ? "   [" + key + "]" : ""), false, Icons.Get("tab." + TabSlugs[tab]));
            return hit;
        }
    }
}
