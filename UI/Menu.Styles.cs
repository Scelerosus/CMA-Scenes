using System;
using System.Collections.Generic;
using Assets.TValle.Tools.Runtime.Moddding.Clothing.Maps;
using BepInEx.Configuration;
using UnityEngine;

namespace CMAScenes
{
    internal static partial class Menu
    {
        private sealed class Styles
        {
            public GUIStyle panel, inner, card, seg, title, badge, h2, label, dim, value, status, statusOn, keycap, placeholder;
            public GUIStyle button, primary, ghost, close, tab, tabOn, row, rowSel, field, switchText, chip, preview;
            public GUIStyle big, bigOn, tip, warn, opt, optOn;
            public GUIStyle sliderTrack, sliderThumb, scrollTrack, scrollThumb;
            public GUIStyle good, bar, addr, tile, tileOn, iconText, tileText, tileSub, brand, app, appOn, tileUnder;
            // The labels of the rows wrapped in their column, the choices with their names on two lines, the small words
            // that are as wide as they are (the keys at the bottom, on/off), the green status wrapped, the help box.
            public GUIStyle labelWrap, optWrap, optOnWrap, hint, statusOnWrap, tipBox;
        }

        // The look is that of the game's own windows (its browser, "the net"): near-black blue-grey panels, cards with a
        // hairline border, white names over grey notes, small spaced-out headings, and tiles with a coloured square.
        private static readonly Color Accent = Hex("#5B8DEF");
        private static readonly Color AccentDark = Hex("#4A7BE0");
        private static readonly Color TextColor = Hex("#F1F3F6");
        private static readonly Color DimColor = Hex("#9AA1AC");
        private static readonly Color Good = Hex("#4CC38A");

        // Textures, drawn in code.
        private static Texture2D tPanel, tCard, tBtn, tBtnHover, tBtnDown, tPrimary, tPrimaryHover, tPrimaryDown;
        private static Texture2D tGhostHover, tTabOn, tField, tFieldFocus, tRowHover, tRowSel, tKeycap, tBadge;
        private static Texture2D tSwitchOff, tSwitchOn, tCircle, tRing, tTrack, tThumb, tLine, tPreview;
        private static Texture2D tScrollTrack, tScrollThumb, tWhitePill;
        private static Texture2D tBar, tBarFlat, tAddr, tTile, tTileHover, tTileOn, tIcon, tChip, tChipHover, tChipOn;

        private static Styles s_st;
        private static GUISkin s_skin;
        private static readonly OwnedResources<Texture2D> s_ownedTextures = new(t => { if (t != null) UnityEngine.Object.Destroy(t); });

        private static Color WithAlpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        // ---- textures ----

        // The game shows what is drawn here brighter than it is (by its tone mapping; its own windows are not): every
        // colour of the window is darkened by as much beforehand. The setting is how much; 1 leaves them be.
        private static float s_dim = 1f;

        private static Color Dim(Color c)
        {
            return new Color(c.r * s_dim, c.g * s_dim, c.b * s_dim, c.a);
        }

        private static Texture2D Make(int w, int h, Func<float, float, Color> shade)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            s_ownedTextures.Add(t);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    t.SetPixel(x, y, Dim(shade(x + 0.5f, y + 0.5f)));
                }
            }
            t.Apply();
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        // Distance from a point to the edge of a rounded rectangle (negative inside).
        private static float RoundDist(float px, float py, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(px) - (hw - r);
            float qy = Mathf.Abs(py) - (hh - r);
            float ox = Mathf.Max(qx, 0f);
            float oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static Texture2D Rounded(int w, int h, float r, Color fill, Color border, float borderWidth)
        {
            return Make(w, h, (x, y) =>
            {
                float d = RoundDist(x - w * 0.5f, y - h * 0.5f, w * 0.5f, h * 0.5f, r);
                float outer = Mathf.Clamp01(0.5f - d);
                float inner = Mathf.Clamp01(0.5f - (d + borderWidth));
                Color c = borderWidth > 0f ? Color.Lerp(border, fill, inner) : fill;
                c.a *= outer;
                return c;
            });
        }

        private static Texture2D Solid(Color c)
        {
            return Make(2, 2, (x, y) => c);
        }

        private static Texture2D Switch(bool on)
        {
            const int w = 48;
            const int h = 26;
            Color track = on ? Accent : Hex("#3A3F4A");
            float knobX = on ? w - h * 0.5f : h * 0.5f;
            float knobR = h * 0.5f - 3.5f;
            return Make(w, h, (x, y) =>
            {
                float d = RoundDist(x - w * 0.5f, y - h * 0.5f, w * 0.5f, h * 0.5f, h * 0.5f);
                float kd = Mathf.Sqrt((x - knobX) * (x - knobX) + (y - h * 0.5f) * (y - h * 0.5f)) - knobR;
                Color c = Color.Lerp(track, Color.white, Mathf.Clamp01(0.5f - kd));
                c.a = Mathf.Clamp01(0.5f - d);
                return c;
            });
        }

        private static void MakeTextures()
        {
            // (Made again when the brightness is set anew, once the slider is let go.)
            float dim = Mathf.Clamp(Plugin.UiDim.Value, 0.3f, 1f);
            if (tPanel != null && (Mathf.Abs(dim - s_dim) < 0.005f || GUIUtility.hotControl != 0))
            {
                return;
            }
            // Styles borrow these textures; discard them before releasing and rebuilding the owned images.
            s_st = null;
            if (s_skin != null) { UnityEngine.Object.Destroy(s_skin); s_skin = null; }
            s_ownedTextures.Clear();
            s_dim = dim;
            s_st = null;
            Color none = new Color(0f, 0f, 0f, 0f);
            Color line = Hex("#2E333D");
            Color edge = Hex("#343946");
            Color sky = Hex("#6FC3F2");
            tPanel = Rounded(56, 56, 16f, Hex("#1C1F26"), Hex("#3A3F4A"), 1.5f);
            tCard = Rounded(32, 32, 9f, Hex("#262A34"), edge, 1f);
            // (Second buttons are outlined, the main one is sky blue, as "Clear" and "Run a deeper scrape" are.)
            tBtn = Rounded(32, 32, 7f, Hex("#1C1F26"), Hex("#4A505C"), 1.2f);
            tBtnHover = Rounded(32, 32, 7f, Hex("#2A2E38"), Hex("#6B7280"), 1.2f);
            tBtnDown = Rounded(32, 32, 7f, Hex("#343946"), Hex("#6B7280"), 1.2f);
            tPrimary = Rounded(32, 32, 7f, sky, none, 0f);
            tPrimaryHover = Rounded(32, 32, 7f, Hex("#8AD0F7"), none, 0f);
            tPrimaryDown = Rounded(32, 32, 7f, Hex("#58ACDC"), none, 0f);
            tGhostHover = Rounded(32, 32, 7f, Hex("#2A2E38"), none, 0f);
            tTabOn = Rounded(32, 32, 8f, AccentDark, none, 0f);
            tField = Rounded(32, 32, 7f, Hex("#15171B"), Hex("#3A3F4A"), 1.2f);
            tFieldFocus = Rounded(32, 32, 7f, Hex("#15171B"), Accent, 1.5f);
            tRowHover = Rounded(32, 32, 8f, Hex("#2A2E38"), none, 0f);
            tRowSel = Rounded(32, 32, 8f, WithAlpha(Accent, 0.2f), WithAlpha(Accent, 0.8f), 1.5f);
            tKeycap = Rounded(32, 32, 7f, Hex("#2D323D"), Hex("#3F4552"), 1f);
            tBadge = Rounded(32, 32, 10f, WithAlpha(Accent, 0.18f), WithAlpha(Accent, 0.6f), 1f);
            // The bar across the top of the window, its address box, and the tiles.
            tBar = Rounded(56, 56, 15f, Hex("#262A33"), none, 0f);
            tBarFlat = Solid(Hex("#262A33"));
            tAddr = Rounded(32, 32, 7f, Hex("#16181D"), none, 0f);
            tTile = Rounded(36, 36, 11f, Hex("#232730"), Hex("#323744"), 1f);
            tTileHover = Rounded(36, 36, 11f, Hex("#2A2E38"), Hex("#4A505C"), 1f);
            tTileOn = Rounded(36, 36, 11f, Hex("#252C3D"), Accent, 1.5f);
            tIcon = Rounded(40, 40, 11f, Color.white, none, 0f);
            float keepDim = s_dim;
            s_dim = 1f;
            tIcon = Rounded(40, 40, 11f, Color.white, none, 0f);
            s_dim = keepDim;
            // The chips of a choice: pills, blue when picked.
            tChip = Rounded(28, 28, 13f, Hex("#2D323D"), none, 0f);
            tChipHover = Rounded(28, 28, 13f, Hex("#3A404D"), none, 0f);
            tChipOn = Rounded(28, 28, 13f, Accent, none, 0f);
            tSwitchOff = Switch(false);
            tSwitchOn = Switch(true);
            s_dim = 1f;
            tCircle = Rounded(32, 32, 16f, Color.white, none, 0f);
            s_dim = keepDim;
            tRing = Rounded(40, 40, 20f, none, Accent, 2.5f);
            tTrack = Rounded(16, 8, 4f, Hex("#3A3F4A"), none, 0f);
            s_dim = 1f;
            tWhitePill = Rounded(16, 8, 4f, Color.white, none, 0f);
            s_dim = keepDim;
            tThumb = Rounded(44, 44, 22f, Color.white, Accent, 5f);
            tLine = Solid(WithAlpha(line, 1f));
            s_dim = 1f;
            tPreview = Rounded(32, 32, 9f, Color.white, WithAlpha(line, 1f), 1.5f);
            s_dim = keepDim;
            tScrollTrack = Rounded(12, 12, 6f, none, none, 0f);
            tScrollThumb = Rounded(12, 12, 6f, Hex("#4A5060"), none, 0f);
        }

        // ---- styles ----

        private static Font s_font;
        private static bool s_fontTried;

        private static Font s_osFont;
        private static bool s_gameFont;

        // The font of the game's own windows (Inter), if it is loaded and has the letters of both languages.
        private static Font GameFont()
        {
            try
            {
                var all = Resources.FindObjectsOfTypeAll<Font>();
                for (int i = 0; all != null && i < all.Length; i++)
                {
                    Font f = all[i];
                    if (f != null && f.dynamic && f.name != null && f.name.StartsWith("Inter", StringComparison.OrdinalIgnoreCase) && f.HasCharacter('a') && f.HasCharacter('\u042F'))
                    {
                        return f;
                    }
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        private static void Tint(GUIStyle s, Color c)
        {
            c = Dim(c);
            s.normal.textColor = c;
            s.hover.textColor = c;
            s.active.textColor = c;
            s.focused.textColor = c;
            s.onNormal.textColor = c;
            s.onHover.textColor = c;
            s.onActive.textColor = c;
            s.onFocused.textColor = c;
        }

        private static void Back(GUIStyle s, Texture2D normal, Texture2D hover, Texture2D active, int border)
        {
            s.normal.background = normal;
            s.hover.background = hover ?? normal;
            s.active.background = active ?? hover ?? normal;
            s.focused.background = normal;
            s.onNormal.background = normal;
            s.onHover.background = hover ?? normal;
            s.onActive.background = active ?? hover ?? normal;
            s.onFocused.background = normal;
            s.border = new RectOffset(border, border, border, border);
        }

        private static RectOffset Pad4(int l, int r, int t, int b)
        {
            return new RectOffset(l, r, t, b);
        }

        private static void MakeStyles()
        {
            MakeTextures();
            if (s_st != null)
            {
                return;
            }
            // (The skin copied for the last set of styles is thrown away with them.)
            if (s_skin != null)
            {
                UnityEngine.Object.Destroy(s_skin);
                s_skin = null;
            }
            GUISkin skin = GUI.skin;
            var st = new Styles();

            st.panel = new GUIStyle();
            Back(st.panel, tPanel, null, null, 18);

            st.inner = new GUIStyle { padding = Pad4((int)Pad, (int)Pad, 16, 14) };

            st.card = new GUIStyle { padding = Pad4(14, 14, 12, 12), margin = Pad4(0, 0, 0, 10) };
            Back(st.card, tCard, null, null, 13);

            st.seg = new GUIStyle { padding = Pad4(0, 0, 2, 2), margin = Pad4(0, 0, 4, 6) };

            st.title = new GUIStyle(skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            Tint(st.title, Color.white);

            st.badge = new GUIStyle(skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = Pad4(9, 9, 2, 3) };
            Tint(st.badge, Accent);
            Back(st.badge, tBadge, null, null, 11);

            st.h2 = new GUIStyle(skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, margin = Pad4(0, 0, 0, 8) };
            Tint(st.h2, DimColor);

            st.label = new GUIStyle(skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            Tint(st.label, TextColor);

            st.dim = new GUIStyle(st.label) { fontSize = 13, wordWrap = true };
            Tint(st.dim, DimColor);

            st.value = new GUIStyle(st.label) { fontSize = 14, alignment = TextAnchor.MiddleRight };
            Tint(st.value, TextColor);

            st.status = new GUIStyle(st.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            Tint(st.status, DimColor);
            st.statusOn = new GUIStyle(st.status);
            Tint(st.statusOn, Good);

            st.keycap = new GUIStyle(skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = Pad4(8, 8, 2, 3), margin = Pad4(0, 4, 3, 3) };
            Tint(st.keycap, TextColor);
            Back(st.keycap, tKeycap, null, null, 8);

            st.placeholder = new GUIStyle(st.label) { fontSize = 15 };
            Tint(st.placeholder, WithAlpha(DimColor, 0.8f));

            st.button = new GUIStyle(skin.button) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = Pad4(14, 14, 7, 8), margin = Pad4(0, 0, 2, 2) };
            Tint(st.button, TextColor);
            Back(st.button, tBtn, tBtnHover, tBtnDown, 11);

            st.primary = new GUIStyle(st.button) { fontStyle = FontStyle.Bold };
            Tint(st.primary, Color.white);
            Back(st.primary, tPrimary, tPrimaryHover, tPrimaryDown, 11);

            st.ghost = new GUIStyle(st.button);
            Tint(st.ghost, DimColor);
            Back(st.ghost, null, tGhostHover, tBtnDown, 11);
            st.ghost.normal.background = null;
            st.ghost.focused.background = null;
            st.ghost.onNormal.background = null;
            st.ghost.onFocused.background = null;

            st.close = new GUIStyle(st.ghost) { fontSize = 16, fontStyle = FontStyle.Bold, padding = Pad4(0, 0, 0, 2) };

            st.tab = new GUIStyle(st.ghost) { fontSize = 16, fontStyle = FontStyle.Bold, margin = Pad4(0, 0, 0, 0) };
            st.tabOn = new GUIStyle(st.button) { fontSize = 16, fontStyle = FontStyle.Bold, margin = Pad4(0, 0, 0, 0) };
            Tint(st.tabOn, Color.white);
            Back(st.tabOn, tTabOn, tTabOn, tTabOn, 11);
            // Narrow padding: the names of the pages and of the choices are longer in Russian.
            st.tab.fontSize = 15;
            st.tab.padding = Pad4(4, 4, 7, 8);
            st.tabOn.fontSize = 15;
            st.tabOn.padding = Pad4(4, 4, 7, 8);
            st.opt = new GUIStyle(st.button) { fontSize = 13, fontStyle = FontStyle.Normal, padding = Pad4(4, 4, 4, 5), margin = Pad4(0, 6, 0, 0) };
            Tint(st.opt, TextColor);
            Back(st.opt, tChip, tChipHover, tChipHover, 13);
            st.optOn = new GUIStyle(st.opt);
            Tint(st.optOn, Color.white);
            Back(st.optOn, tChipOn, tChipOn, tChipOn, 13);

            st.row = new GUIStyle(skin.button) { fontSize = 15, alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = false, clipping = TextClipping.Clip, padding = Pad4(14, 10, 4, 4), margin = Pad4(0, 0, 0, 3) };
            Tint(st.row, TextColor);
            Back(st.row, null, tRowHover, tRowHover, 11);
            st.row.normal.background = null;
            st.row.focused.background = null;
            st.row.onNormal.background = null;
            st.row.onFocused.background = null;
            st.rowSel = new GUIStyle(st.row);
            Back(st.rowSel, tRowSel, tRowSel, tRowSel, 11);

            st.field = new GUIStyle(skin.textField) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = Pad4(12, 12, 7, 8), margin = Pad4(0, 0, 2, 2), clipping = TextClipping.Clip };
            Tint(st.field, TextColor);
            Back(st.field, tField, tField, tFieldFocus, 10);
            st.field.focused.background = tFieldFocus;
            st.field.onFocused.background = tFieldFocus;
            st.field.onHover.background = tField;

            st.switchText = new GUIStyle(skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = Pad4(60, 0, 0, 1), margin = Pad4(0, 0, 3, 3) };
            Tint(st.switchText, TextColor);

            st.chip = new GUIStyle(skin.button) { margin = Pad4(0, 6, 2, 2), padding = Pad4(0, 0, 0, 0) };
            Back(st.chip, tCircle, tCircle, tCircle, 0);

            st.preview = new GUIStyle(skin.box) { margin = Pad4(8, 0, 2, 2) };
            Back(st.preview, tPreview, tPreview, tPreview, 11);

            st.sliderTrack = new GUIStyle(skin.horizontalSlider) { padding = Pad4(0, 0, 0, 0), margin = Pad4(0, 0, 0, 0), border = Pad4(0, 0, 0, 0), fixedHeight = 0f };
            st.sliderTrack.normal.background = null;
            st.sliderTrack.hover.background = null;
            st.sliderTrack.active.background = null;
            st.sliderTrack.focused.background = null;
            st.sliderThumb = new GUIStyle(skin.horizontalSliderThumb) { padding = Pad4(0, 0, 0, 0), margin = Pad4(0, 0, 0, 0), border = Pad4(0, 0, 0, 0), fixedWidth = ThumbSize, fixedHeight = ThumbSize, overflow = Pad4(0, 0, 0, 0) };
            Back(st.sliderThumb, tThumb, tThumb, tThumb, 0);

            st.scrollTrack = new GUIStyle(skin.verticalScrollbar) { fixedWidth = 8f, margin = Pad4(6, 0, 0, 0), padding = Pad4(0, 0, 0, 0) };
            Back(st.scrollTrack, tScrollTrack, tScrollTrack, tScrollTrack, 5);
            st.scrollThumb = new GUIStyle(skin.verticalScrollbarThumb) { fixedWidth = 8f, padding = Pad4(0, 0, 0, 0), margin = Pad4(0, 0, 0, 0) };
            Back(st.scrollThumb, tScrollThumb, tScrollThumb, tScrollThumb, 5);

            st.good = new GUIStyle(st.dim);
            Tint(st.good, Good);
            st.bar = new GUIStyle();
            Back(st.bar, tBar, null, null, 17);
            st.addr = new GUIStyle(skin.label) { fontSize = 14, alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = false, clipping = TextClipping.Clip, padding = Pad4(12, 12, 0, 1), margin = Pad4(0, 0, 2, 2) };
            Tint(st.addr, Hex("#C5CAD3"));
            Back(st.addr, tAddr, null, null, 9);
            st.tile = new GUIStyle(skin.button) { margin = Pad4(0, 0, 0, 0), padding = Pad4(0, 0, 0, 0) };
            Back(st.tile, tTile, tTileHover, tTileHover, 14);
            st.tileOn = new GUIStyle(st.tile);
            Back(st.tileOn, tTileOn, tTileOn, tTileOn, 14);
            st.iconText = new GUIStyle(skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, padding = Pad4(0, 0, 0, 1) };
            Tint(st.iconText, Color.white);
            st.tileText = new GUIStyle(skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip };
            Tint(st.tileText, Color.white);
            st.brand = new GUIStyle(skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false, margin = Pad4(4, 0, 0, 0) };
            Tint(st.brand, Color.white);
            // A page in the row of pages: an app on the phone, with nothing behind it unless it is the open one.
            st.app = new GUIStyle(st.tile);
            Back(st.app, null, tGhostHover, tGhostHover, 9);
            st.app.normal.background = null;
            st.app.focused.background = null;
            st.app.onNormal.background = null;
            st.app.onFocused.background = null;
            st.appOn = new GUIStyle(st.tile);
            Back(st.appOn, tTile, tTile, tTile, 14);
            st.tileUnder = new GUIStyle(skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Clip };
            Tint(st.tileUnder, Color.white);
            st.iconText.fontSize = 15;
            st.tileSub = new GUIStyle(skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip };
            Tint(st.tileSub, DimColor);

            st.big = new GUIStyle(st.button) { fontSize = 16, richText = true, padding = Pad4(8, 8, 6, 6), margin = Pad4(0, 0, 2, 2) };
            st.bigOn = new GUIStyle(st.big);
            Tint(st.bigOn, Color.white);
            Back(st.bigOn, tPrimary, tPrimaryHover, tPrimaryDown, 11);

            st.tip = new GUIStyle(st.dim) { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true, clipping = TextClipping.Clip };
            st.warn = new GUIStyle(st.dim);
            Tint(st.warn, Hex("#FFB454"));

            // (What could not be read whole in 0.2.0: the labels of the rows were one line of a fixed height in a column
            // of 236 pixels, the choices one line in a fixed width, the help line two lines of a box 40 pixels high. These
            // wrap, and the rows and boxes take the height their words need.)
            st.labelWrap = new GUIStyle(st.label) { wordWrap = true, clipping = TextClipping.Overflow, padding = Pad4(st.label.padding.left, 6, 2, 2) };
            st.optWrap = new GUIStyle(st.opt) { wordWrap = true, clipping = TextClipping.Overflow, fontSize = 12, padding = Pad4(3, 3, 3, 4) };
            st.optOnWrap = new GUIStyle(st.optOn) { wordWrap = true, clipping = TextClipping.Overflow, fontSize = 12, padding = Pad4(3, 3, 3, 4) };
            st.hint = new GUIStyle(st.dim) { wordWrap = false, clipping = TextClipping.Overflow };
            st.statusOnWrap = new GUIStyle(st.statusOn) { wordWrap = true, fontSize = 15, clipping = TextClipping.Overflow };
            st.tipBox = new GUIStyle(st.tip) { padding = Pad4(12, 12, 8, 9), clipping = TextClipping.Overflow };
            Back(st.tipBox, tCard, null, null, 13);

            // One font for the whole window, taken from the system, so that both languages have all their letters.
            Font font = UiFont;
            if (font != null)
            {
                foreach (GUIStyle s in new[]
                {
                    st.title, st.badge, st.h2, st.label, st.dim, st.value, st.status, st.statusOn, st.keycap, st.placeholder, st.button, st.primary, st.ghost,
                    st.close, st.tab, st.tabOn, st.row, st.rowSel, st.field, st.switchText, st.big, st.bigOn, st.tip, st.warn, st.opt, st.optOn,
                    st.good, st.addr, st.iconText, st.tileText, st.tileSub, st.brand, st.tileUnder,
                    st.labelWrap, st.optWrap, st.optOnWrap, st.hint, st.statusOnWrap, st.tipBox
                })
                {
                    s.font = font;
                }
            }

            s_st = st;

            // A copy of the skin with the scroll bars restyled, used only while this window is drawn.
            try
            {
                s_skin = UnityEngine.Object.Instantiate(skin);
                s_skin.verticalScrollbar = st.scrollTrack;
                s_skin.verticalScrollbarThumb = st.scrollThumb;
                s_skin.hideFlags = HideFlags.HideAndDontSave;
            }
            catch (Exception ex)
            {
                s_skin = null;
                Plugin.ModLog.LogWarning("Settings window: could not restyle the scroll bars: " + ex.Message);
            }
        }
    }
}
