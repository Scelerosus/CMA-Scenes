using System;
using System.Collections.Generic;
using System.Linq;
using Assets.TValle.Tools.Runtime.Moddding.Clothing.Maps;
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
            public string authors;
            public ClothingItemMap.Type type;
            public ClothingCategory category;
        }

        private static readonly Color[] Presets =
        {
            Hex("#D81B60"), Hex("#E53935"), Hex("#FB8C00"), Hex("#FDD835"), Hex("#9CCC24"), Hex("#43A047"),
            Hex("#00ACC1"), Hex("#29B6F6"), Hex("#1E88E5"), Hex("#8E24AA"), Hex("#FFFFFF"), Hex("#212121")
        };

        private static Vector2 s_listScroll;
        private static Vector2 s_characterScroll;

        private static List<Entry> s_entries = new List<Entry>();
        private static List<Entry> s_shown = new List<Entry>();
        private static string s_shownKey = "";
        private static bool s_showAll = true;
        private static string s_search = "";
        private static string s_hex = "";
        private static string s_hexItem = "";
        private static int s_version;
        private static ClothingCategory s_category;
        private static bool s_favoritesOnly;
        private static int s_clothingPage;
        private const int ClothingPageSize = 12;
        private static string s_outfitName = "";
        private static string s_clothingError = "";
        private static ClothingPreferences s_clothingPreferences;
        private static int s_outfitView;
        private static bool s_chooseCharacter, s_showWorn, s_browseFilters, s_colorDetails, s_outfitOptions, s_resetPreferences;
        private static string s_editItem = "", s_deleteOutfit = "";
        private static GUIStyle s_clothingRow, s_clothingRowSelected;
        private static Styles s_clothingRowFor;

        private static ClothingPreferences ClothingPrefs
        {
            get
            {
                if (s_clothingPreferences != null) return s_clothingPreferences;
                s_clothingPreferences = new ClothingPreferences(System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "CMAScenes-clothing.json"));
                try { s_clothingPreferences.Load(); }
                catch (Exception ex) { s_clothingError = ex.Message; Plugin.ModLog.LogWarning("Clothing preferences: " + ex.Message); }
                return s_clothingPreferences;
            }
        }

        private static void SaveClothingPrefs()
        {
            try { ClothingPrefs.Save(); s_clothingError = ""; }
            catch (Exception ex) { s_clothingError = ex.Message; Plugin.ModLog.LogWarning("Clothing preferences: " + ex.Message); }
        }

        private static string CategoryName(ClothingCategory category)
        {
            switch (category)
            {
                case ClothingCategory.All: return "All clothing";
                case ClothingCategory.Top: return "Swimwear tops";
                case ClothingCategory.Bottom: return "Swimwear bottoms";
                case ClothingCategory.UpperUnderwear: return "Bras & underwear tops";
                case ClothingCategory.LowerUnderwear: return "Briefs & underwear bottoms";
                case ClothingCategory.OnePiece: return "Dresses & one-piece";
                case ClothingCategory.Set: return "Matching sets";
                case ClothingCategory.UpperBody: return "Tops";
                case ClothingCategory.LowerBody: return "Bottoms";
                case ClothingCategory.Shoes: return "Shoes";
                case ClothingCategory.Jacket: return "Jackets & coats";
                case ClothingCategory.Accessories: return "Accessories";
                case ClothingCategory.Glasses: return "Glasses";
                case ClothingCategory.Socks: return "Socks & stockings";
                case ClothingCategory.Gloves: return "Gloves";
                case ClothingCategory.Hat: return "Hats";
                case ClothingCategory.UpperUnderwearAccessories: return "Upper lingerie accessories";
                case ClothingCategory.LowerUnderwearAccessories: return "Lower lingerie accessories";
                default: return "Other";
            }
        }

        private static readonly ClothingCategory[] ClothingCategories = { ClothingCategory.All, ClothingCategory.UpperUnderwear, ClothingCategory.LowerUnderwear, ClothingCategory.Top, ClothingCategory.Bottom, ClothingCategory.UpperBody,
            ClothingCategory.LowerBody, ClothingCategory.OnePiece, ClothingCategory.Set, ClothingCategory.Shoes, ClothingCategory.Jacket, ClothingCategory.Accessories,
            ClothingCategory.Glasses, ClothingCategory.Socks, ClothingCategory.Gloves, ClothingCategory.Hat, ClothingCategory.UpperUnderwearAccessories,
            ClothingCategory.LowerUnderwearAccessories, ClothingCategory.Other };

        private static Color CurrentColor(ClothingChoice choice, string id)
        {
            return ColorUtility.TryParseHtmlString(choice.ColorFor(id), out Color c) ? c : Presets[0];
        }

        // ---- clothing list ----

        // Reads the clothing items the game has loaded. They are only there once a game is running.
        private static void Refresh()
        {
            ClothingPreviews.Release();
            var list = new List<Entry>();
            var seen = new HashSet<string>();
            foreach (ClothingItemMap m in Bikini.LoadMaps())
            {
                if (m == null || string.IsNullOrEmpty(m.id) || m.sex == ClothingItemMap.Sex.male || !seen.Add(m.id))
                {
                    continue;
                }
                string nativeName = null;
                try { nativeName = m.GetIngameName(Assets.TValle.Tools.Runtime.Language.en); }
                catch { /* Some mod maps have no game translation. */ }
                string name = ClothingCatalog.VariantName(!string.IsNullOrWhiteSpace(nativeName) && nativeName != m.id && nativeName != m.displayId
                    ? nativeName : ClothingCatalog.DisplayName(m.id, m.displayId, m.name, false), m.id, false);
                var authors = new List<string>();
                if (m.authors != null) foreach (var author in m.authors) if (author != null) authors.Add(author.name);
                list.Add(new Entry { map = m, id = m.id, name = name, lower = (name + " " + m.id + " " + m.name + " " + m.displayId).ToLowerInvariant(), type = m.type,
                    authors = ClothingCardInfo.Authors(authors),
                    category = ClothingCatalog.Category(m.type.ToString(), m.id + " " + m.name) });
            }
            list.Sort((a, b) => { int n = string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase); return n != 0 ? n : string.Compare(a.id, b.id, StringComparison.Ordinal); });
            s_entries = list;
            s_version++;
            s_shownKey = "";
            Plugin.ModLog.LogInfo($"Menu: {list.Count} clothing item(s) available for Outfit.");
        }

        // The default Outfit filter shows swimwear and lingerie, found by the kind the game gives the item and by its
        // name, minus anything that is plainly another sort of clothing (a dress that is typed as swimwear, a shoe...).
        private static readonly string[] SwimWords = { "bikini", "bra", "bralette", "bandeau", "thong", "string", "tanga", "panty", "panties", "brief", "lingerie", "swim", "monokini", "microkini" };
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

        private static void RebuildShown()
        {
            string sort = Plugin.BikiniSort.Value ?? "";
            string key = $"{s_version}|{s_category}|{s_showAll}|{s_search}|{sort}|{s_favoritesOnly}";
            if (key == s_shownKey)
            {
                return;
            }
            s_shownKey = key;
            s_clothingPage = 0;
            s_listScroll = Vector2.zero;
            s_shown = new List<Entry>();
            string q = (s_search ?? "").Trim().ToLowerInvariant();
            foreach (Entry e in s_entries)
            {
                if ((!s_showAll && !IsSwimwear(e)) || (s_category != ClothingCategory.All && e.category != s_category))
                {
                    continue;
                }
                if (s_favoritesOnly && !ClothingPrefs.IsFavorite(e.id)) continue;
                if (q.Length > 0 && !e.lower.Contains(q) && !e.authors.ToLowerInvariant().Contains(q))
                {
                    continue;
                }
                s_shown.Add(e);
            }
            // Name order is stable (name, then ID); kind order uses a fixed game-type sequence.
            if (string.Equals(sort, "NameDesc", StringComparison.OrdinalIgnoreCase))
            {
                s_shown.Reverse();
            }
            else if (string.Equals(sort, "Kind", StringComparison.OrdinalIgnoreCase))
            {
                s_shown.Sort((a, b) =>
                {
                    int byKind = TypeOrder(a.type).CompareTo(TypeOrder(b.type));
                    if (byKind != 0) return byKind;
                    int byName = string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
                    return byName != 0 ? byName : string.Compare(a.id, b.id, StringComparison.Ordinal);
                });
            }
        }

        private static int TypeOrder(ClothingItemMap.Type type)
        {
            switch (type)
            {
                case ClothingItemMap.Type.upperBodyUnderwear: return 0;
                case ClothingItemMap.Type.lowerBodyUnderwear: return 1;
                case ClothingItemMap.Type.upperBody: return 2;
                case ClothingItemMap.Type.lowerBody: return 3;
                case ClothingItemMap.Type.swimsuit: return 4;
                case ClothingItemMap.Type.jacket: return 5;
                case ClothingItemMap.Type.shoes: return 6;
                case ClothingItemMap.Type.socks: return 7;
                case ClothingItemMap.Type.gloves: return 8;
                case ClothingItemMap.Type.hat: return 9;
                case ClothingItemMap.Type.glasses: return 10;
                case ClothingItemMap.Type.accessories: return 11;
                case ClothingItemMap.Type.upperBodyUnderwearAccessories: return 12;
                case ClothingItemMap.Type.lowerBodyUnderwearAccessories: return 13;
                default: return 100 + (int)type;
            }
        }

        // ---- Bikini ----

        private static void DrawBikini(ShakeController host, Styles st)
        {
            if (s_clothingRowFor != st)
            {
                s_clothingRow = new GUIStyle(st.row) { wordWrap = true };
                s_clothingRowSelected = new GUIStyle(st.rowSel) { wordWrap = true };
                s_clothingRowFor = st;
            }
            ClothingChoice choice = Bikini.CurrentChoice();
            var wornVariants = Bikini.ReadWorn(out var wornIds);
            DrawOutfitCharacter(host, st, choice, wornIds, wornVariants);

            GUILayout.BeginHorizontal();
            string[] views = { "Browse", "Outfit · " + choice.Items.Count, "Saved · " + ClothingPrefs.Sets.Count };
            for (int i = 0; i < views.Length; i++)
            {
                if (GUILayout.Button(views[i], i == s_outfitView ? st.optOn : st.opt, GUILayout.Height(34f)))
                {
                    s_outfitView = i;
                    s_scroll = Vector2.zero;
                }
            }
            GUILayout.EndHorizontal();
            Gap(8f);

            if (s_outfitView == 0) DrawClothingBrowser(st, choice, wornIds, wornVariants);
            else if (s_outfitView == 1) DrawOutfitEditor(st, choice);
            else DrawOutfits(st);

            if (s_clothingError.Length > 0) GUILayout.Label(ClothingCatalog.Escape(s_clothingError), st.labelWrap);
        }

        private static void DrawOutfitCharacter(ShakeController host, Styles st, ClothingChoice choice,
            HashSet<string> wornIds, Dictionary<string, Dictionary<int, string>> wornVariants)
        {
            CardBegin(st, "Character");
            var selected = WardrobeCharacters.Selected;
            bool stalled = Bikini.LoadingStalled();
            bool loading = host.OutfitChanging;
            GUILayout.BeginHorizontal();
            GUILayout.Label(selected != null ? ClothingCatalog.Escape(selected.name) : "No character selected", st.labelWrap);
            if (GUILayout.Button(s_chooseCharacter ? "Done" : "Change", st.ghost, GUILayout.Width(100f))) s_chooseCharacter = !s_chooseCharacter;
            GUILayout.EndHorizontal();
            if (s_chooseCharacter || selected == null)
            {
                var nearby = WardrobeCharacters.Nearby;
                if (nearby.Count == 0) GUILayout.Label("Move near a character to use their wardrobe.", st.dim);
                else
                {
                    s_characterScroll = GUILayout.BeginScrollView(s_characterScroll, GUILayout.Height(Mathf.Min(132f, nearby.Count * 34f + 8f)));
                    foreach (var context in nearby)
                    {
                        if (context.Character == null || !context.Character.gameObject.activeInHierarchy) continue;
                        string label = ClothingCatalog.Escape(context.Character.name) + " · " + Mathf.Sqrt(context.DistanceSquared).ToString("0.0") + " m";
                        if (GUILayout.Button(label, context.Character == selected ? st.rowSel : st.row, GUILayout.Height(30f)))
                        {
                            WardrobeCharacters.Select(context.Key);
                            s_chooseCharacter = false;
                        }
                    }
                    GUILayout.EndScrollView();
                }
            }

            GUILayout.Label("Build an outfit, choose its look, then dress the selected character.", st.dim);
            GUILayout.BeginHorizontal();
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && selected != null && choice.Items.Count > 0 && !stalled && !loading;
            if (GUILayout.Button(loading ? "Changing outfit..." : "Dress character", st.primary, GUILayout.Height(38f))) host.ApplyOutfit();
            GUI.enabled = enabled && selected != null && host.CanRestoreOutfit && !stalled;
            if (GUILayout.Button("Restore previous outfit", st.button, GUILayout.Height(38f))) host.RestoreOutfit();
            GUI.enabled = enabled;
            GUILayout.EndHorizontal();
            if (stalled) GUILayout.Label("Clothing loading failed. Reload the game before trying again.", st.labelWrap);
            else GUILayout.Label(choice.Items.Count == 0 ? "Add clothes from Browse to begin." : choice.Items.Count + " items in your outfit draft. Editing the draft does not change worn clothes.", st.dim);
            string error = Bikini.SelectedError();
            if (!string.IsNullOrEmpty(error)) GUILayout.Label(ClothingCatalog.Escape(error), st.labelWrap);

            if (GUILayout.Button((s_showWorn ? "− " : "+ ") + "Currently worn · " + wornIds.Count, st.ghost, GUILayout.Height(28f))) s_showWorn = !s_showWorn;
            if (s_showWorn)
            {
                if (wornIds.Count == 0) GUILayout.Label("No visible clothing detected.", st.dim);
                foreach (string id in wornIds.OrderBy(x => x, StringComparer.Ordinal))
                {
                    Entry entry = s_entries.Find(e => e.id == id);
                    GUILayout.Label("• " + ClothingCatalog.Escape(entry != null ? entry.name : id), st.labelWrap);
                }
                GUI.enabled = enabled && wornIds.Count > 0;
                if (GUILayout.Button("Copy worn clothes into draft", st.ghost, GUILayout.Height(28f)))
                {
                    StoreDraft(new ClothingChoice(wornIds, Plugin.BikiniColor.Value, Plugin.BikiniSolid.Value, wornVariants, choice.Colors));
                    s_outfitView = 1;
                }
                Tip("Copy the pieces and their materials. Keep each matching piece's draft colour and the outfit style.");
                GUI.enabled = enabled;
            }
            CardEnd();
        }

        private static void DrawClothingBrowser(Styles st, ClothingChoice choice, HashSet<string> wornIds,
            Dictionary<string, Dictionary<int, string>> wornVariants)
        {
            CardBegin(st, "Browse clothing");
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("search");
            s_search = GUILayout.TextField(s_search, st.field);
            if (s_search.Length == 0 && Repainting() && GUI.GetNameOfFocusedControl() != "search")
            {
                Rect rect = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(rect.x + 12f, rect.y, rect.width - 12f, rect.height), "Search clothing or author...", st.placeholder);
            }
            if (s_search.Length > 0 && GUILayout.Button("Clear", st.ghost, GUILayout.Width(70f))) s_search = "";
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((s_browseFilters ? "− " : "+ ") + CategoryName(s_category), st.ghost, GUILayout.Height(30f))) s_browseFilters = !s_browseFilters;
            if (GUILayout.Button(s_favoritesOnly ? "★ Favorites" : "☆ Favorites", s_favoritesOnly ? st.optOn : st.opt, GUILayout.Width(130f), GUILayout.Height(30f))) s_favoritesOnly = !s_favoritesOnly;
            GUILayout.EndHorizontal();
            if (s_browseFilters)
            {
                float width = (RowWidth - 12f) / 3f;
                for (int start = 0; start < ClothingCategories.Length; start += 3)
                {
                    GUILayout.BeginHorizontal();
                    for (int i = start; i < Math.Min(start + 3, ClothingCategories.Length); i++)
                    {
                        var category = ClothingCategories[i];
                        if (GUILayout.Button(CategoryName(category), s_category == category ? st.optOnWrap : st.optWrap, GUILayout.Width(width), GUILayout.Height(42f)))
                        {
                            s_category = category;
                            s_browseFilters = false;
                        }
                    }
                    GUILayout.EndHorizontal();
                }
                ChoiceRow(st, "Sort", new[] { "A to Z", "Z to A", "By type" }, new[] { "Name", "NameDesc", "Kind" }, Plugin.BikiniSort, 100f, "Sort the clothing catalog.");
                s_showAll = !SwitchRow(st, "Swimwear & lingerie only", !s_showAll);
            }

            RebuildShown();
            GUILayout.Label(s_shown.Count + " items · click a piece to choose its colour and material", st.dim);
            if (s_entries.Count == 0) GUILayout.Label("Load a game, then refresh the clothing catalog.", st.labelWrap);
            else if (s_shown.Count == 0) GUILayout.Label("No matching clothes. Try another category or clear the search and filters.", st.labelWrap);

            s_listScroll = GUILayout.BeginScrollView(s_listScroll, GUILayout.Height(360f));
            int begin = s_clothingPage * ClothingPageSize;
            int end = Math.Min(begin + ClothingPageSize, s_shown.Count);
            for (int index = begin; index < end; index++)
            {
                Entry entry = s_shown[index];
                bool selected = choice.Items.Contains(entry.id);
                string credit = entry.authors.Length == 0 ? "Author not specified" : "By " + entry.authors;
                string status = selected ? "✓ In draft · Edit colour" : "Add & choose colour";
                if (wornIds.Contains(entry.id)) status += " · Currently worn";
                string text = ClothingCatalog.Escape(entry.name) + "\n<size=12><color=#B9B9CD>" + ClothingCatalog.Escape(credit) + " · " + CategoryName(entry.category)
                    + "</color></size>\n<size=12><color=#B9B9CD>" + status + "</color></size>";
                s_measure.text = text;
                float rowHeight = Mathf.Max(96f, s_clothingRow.CalcHeight(s_measure, RowWidth - 156f));
                GUILayout.BeginHorizontal();
                Dictionary<int, string> variants = choice.Materials.TryGetValue(entry.id, out var picked)
                    ? picked : wornVariants.TryGetValue(entry.id, out var worn) ? worn : null;
                Color? tint = selected && ColorUtility.TryParseHtmlString(choice.ColorFor(entry.id), out Color color) ? color : (Color?)null;
                DrawClothingPreview(st, entry, variants, tint, 96f, selected && choice.Solid);
                if (GUILayout.Button(new GUIContent(text, credit), selected ? s_clothingRowSelected : s_clothingRow, GUILayout.Height(rowHeight)))
                {
                    if (!selected) TogglePiece(entry.id);
                    s_editItem = entry.id;
                    s_outfitView = 1;
                    s_scroll = Vector2.zero;
                }
                if (GUILayout.Button(ClothingPrefs.IsFavorite(entry.id) ? "★" : "☆", st.ghost, GUILayout.Width(36f), GUILayout.Height(rowHeight)))
                {
                    ClothingPrefs.ToggleFavorite(entry.id);
                    SaveClothingPrefs();
                    s_shownKey = "";
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            int pages = Math.Max(1, (s_shown.Count + ClothingPageSize - 1) / ClothingPageSize);
            GUILayout.BeginHorizontal();
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && s_clothingPage > 0;
            if (GUILayout.Button("Previous", st.ghost)) { s_clothingPage--; s_listScroll = Vector2.zero; }
            GUI.enabled = enabled;
            GUILayout.Label("Page " + (s_clothingPage + 1) + " of " + pages, st.dim);
            GUI.enabled = enabled && s_clothingPage + 1 < pages;
            if (GUILayout.Button("Next", st.ghost)) { s_clothingPage++; s_listScroll = Vector2.zero; }
            GUI.enabled = enabled;
            if (GUILayout.Button("Refresh catalog", st.ghost)) Refresh();
            GUILayout.EndHorizontal();
            CardEnd();
        }

        private static void DrawClothingPreview(Styles st, Entry entry, Dictionary<int, string> variants, Color? tint, float size, bool solid)
        {
            Texture2D preview = ClothingPreviews.Get(ClothingPreviews.Key(entry.map, variants, tint, solid), entry.map, variants, tint, solid);
            Color background = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.16f, 0.17f, 0.21f, 1);
            GUILayout.Box("", st.preview, GUILayout.Width(size), GUILayout.Height(size));
            GUI.backgroundColor = background;
            if (!Repainting()) return;
            Rect box = GUILayoutUtility.GetLastRect();
            Texture image = preview != null ? preview : Icons.Get("tab.bikini");
            if (image == null) return;
            var fit = ClothingCardInfo.Fit(image.width, image.height, box.width - 8f, box.height - 8f);
            Color keep = GUI.color;
            if (preview == null) GUI.color = new Color(0.55f, 0.60f, 0.70f, keep.a);
            GUI.DrawTexture(new Rect(box.x + 4f + fit.X, box.y + 4f + fit.Y, fit.Width, fit.Height), image, ScaleMode.ScaleToFit);
            GUI.color = keep;
        }

        private static void DrawOutfitEditor(Styles st, ClothingChoice choice)
        {
            CardBegin(st, "Your outfit");
            if (choice.Items.Count == 0)
            {
                GUILayout.Label("Your draft is empty. Browse clothing to add your first piece.", st.labelWrap);
                if (GUILayout.Button("Browse clothing", st.ghost)) s_outfitView = 0;
            }
            else
            {
                GUILayout.Label("Click a piece to choose its own colour and material. Other pieces keep their colours.", st.dim);
                foreach (string id in choice.Items)
                {
                    Entry entry = s_entries.Find(e => e.id == id);
                    GUILayout.BeginHorizontal();
                    string name = ClothingCatalog.Escape(entry != null ? entry.name : id);
                    s_measure.text = name;
                    float rowHeight = Mathf.Max(36f, s_clothingRow.CalcHeight(s_measure, RowWidth - 96f));
                    if (GUILayout.Button(name, s_editItem == id ? s_clothingRowSelected : s_clothingRow, GUILayout.Height(rowHeight))) s_editItem = s_editItem == id ? "" : id;
                    if (GUILayout.Button("Remove", st.ghost, GUILayout.Width(88f), GUILayout.Height(rowHeight))) TogglePiece(id);
                    GUILayout.EndHorizontal();
                    if (s_editItem != id) continue;
                    if (entry == null) GUILayout.Label("This item is no longer available in the catalog.", st.dim);
                    else
                    {
                        DrawMaterialChoices(st, entry, choice);
                        DrawPieceColor(st, choice, id);
                    }
                }
                Gap(6f);
                if (GUILayout.Button("Add more clothes", st.ghost, GUILayout.Height(30f))) s_outfitView = 0;
            }
            CardEnd();
            DrawOutfitOptions(st, choice);
        }

        private static void DrawPieceColor(Styles st, ClothingChoice choice, string id)
        {
            GUILayout.Label("Piece colour", st.label);
            GUILayout.Label("This colour changes only this garment. Press Dress character to apply the draft.", st.dim);
            Color current = CurrentColor(choice, id);
            if (s_hexItem != id) { s_hexItem = id; s_hex = "#" + ColorUtility.ToHtmlStringRGB(current); }
            GUILayout.BeginHorizontal();
            foreach (Color preset in Presets)
            {
                Color background = GUI.backgroundColor;
                GUI.backgroundColor = Dim(preset);
                bool clicked = GUILayout.Button("", st.chip, GUILayout.Width(30f), GUILayout.Height(30f));
                GUI.backgroundColor = background;
                if (Repainting() && SameColor(preset, current))
                {
                    Rect rect = GUILayoutUtility.GetLastRect();
                    GUI.DrawTexture(new Rect(rect.x - 5f, rect.y - 5f, rect.width + 10f, rect.height + 10f), tRing);
                }
                if (clicked) { SetColor(id, preset); current = preset; }
            }
            GUILayout.EndHorizontal();
            Gap(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Colour code", st.label, GUILayout.Width(LabelWidth), GUILayout.Height(32f));
            if (GUI.GetNameOfFocusedControl() != "hex") s_hex = "#" + ColorUtility.ToHtmlStringRGB(current);
            GUI.SetNextControlName("hex");
            string typed = GUILayout.TextField(s_hex, 7, st.field, GUILayout.Width(120f));
            if (typed != s_hex)
            {
                s_hex = typed;
                if (typed.Length == 7 && ColorUtility.TryParseHtmlString(typed, out Color parsed)) { SetColor(id, parsed); current = parsed; }
            }
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = Dim(current);
            GUILayout.Box("", st.preview, GUILayout.Width(70f), GUILayout.Height(32f));
            GUI.backgroundColor = old;
            GUILayout.EndHorizontal();
            if (GUILayout.Button((s_colorDetails ? "− " : "+ ") + "Custom colour", st.ghost, GUILayout.Height(28f))) s_colorDetails = !s_colorDetails;
            if (s_colorDetails)
            {
                float r = SliderRow(st, "Red", current.r, 0f, 1f, "0", " %", 100f, Hex("#FF5A5A"), float.NaN, out bool _);
                float g = SliderRow(st, "Green", current.g, 0f, 1f, "0", " %", 100f, Hex("#4CD97B"), float.NaN, out bool _);
                float b = SliderRow(st, "Blue", current.b, 0f, 1f, "0", " %", 100f, Hex("#5AA9FF"), float.NaN, out bool _);
                if (Mathf.Abs(r - current.r) > 0.0005f || Mathf.Abs(g - current.g) > 0.0005f || Mathf.Abs(b - current.b) > 0.0005f) SetColor(id, new Color(r, g, b, 1f));
            }
        }

        private static void DrawOutfitOptions(Styles st, ClothingChoice choice)
        {
            CardBegin(st, null);
            if (GUILayout.Button((s_outfitOptions ? "− " : "+ ") + "More outfit options", st.ghost, GUILayout.Height(28f))) s_outfitOptions = !s_outfitOptions;
            if (s_outfitOptions)
            {
                GUILayout.Label("Style for all pieces", st.dim);
                int style = Choice(st, new[] { "Material pattern", "Solid colour" }, choice.Solid ? 1 : 0, 130f);
                if ((style == 1) != choice.Solid) StoreDraft(Bikini.CurrentChoice().WithSolid(style == 1));
                Check(st, "Wear this outfit during scenes", Plugin.OutfitWithScene, "Automatically dress for a scene and restore the previous outfit when it ends.");
                GUILayout.BeginHorizontal();
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && choice.Items.Count > 0;
                if (IconButton("Random materials", "clothes.texture", st.ghost, GUILayout.Height(30f)))
                {
                    if (Bikini.RandomizeMaterials(choice)) StoreDraft(choice);
                    else s_clothingError = "These pieces have no material variants.";
                }
                GUI.enabled = enabled;
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Clear outfit draft", st.ghost, GUILayout.Height(28f))) StoreDraft(new ClothingChoice(Array.Empty<string>(), Plugin.BikiniColor.Value, Plugin.BikiniSolid.Value));
                if (GUILayout.Button("Reset outfit settings", st.ghost, GUILayout.Height(28f)))
                    ResetAll(Plugin.BikiniTop, Plugin.BikiniBottom, Plugin.BikiniItems, Plugin.BikiniMaterials, Plugin.BikiniColor, Plugin.BikiniColors, Plugin.BikiniSolid, Plugin.OutfitWithScene);
            }
            CardEnd();
        }

        private static bool IconButton(string label, string icon, GUIStyle style, params GUILayoutOption[] options)
        {
            bool clicked = GUILayout.Button(label.Length == 0 ? "" : "   " + label, style, options);
            if (Repainting())
            {
                Rect rect = GUILayoutUtility.GetLastRect();
                Texture2D picture = Icons.Get(icon);
                if (picture != null)
                {
                    Color keep = GUI.color;
                    GUI.color = Color.Lerp(Accent, Color.white, 0.35f);
                    float size = Mathf.Min(18f, rect.height - 8f);
                    GUI.DrawTexture(new Rect(rect.x + 6f, rect.y + (rect.height - size) * 0.5f, size, size), picture, ScaleMode.ScaleToFit);
                    GUI.color = keep;
                }
            }
            return clicked;
        }

        private static bool SameColor(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;
        }

        private static void TogglePiece(string id)
        {
            ClothingChoice choice = Bikini.CurrentChoice().Pick(id);
            StoreDraft(choice);
            if (choice.Items.Contains(id)) s_editItem = id;
            else if (s_editItem == id) s_editItem = "";
        }

        private static void StoreDraft(ClothingChoice choice)
        {
            Plugin.BikiniItems.Value = choice.ToConfigItems();
            Plugin.BikiniTop.Value = "";
            Plugin.BikiniBottom.Value = "";
            Plugin.BikiniMaterials.Value = choice.ToConfigMaterials();
            Plugin.BikiniColor.Value = choice.Color;
            Plugin.BikiniColors.Value = choice.ToConfigColors();
            Plugin.BikiniSolid.Value = choice.Solid;
            s_clothingError = "";
            MarkDirty();
        }

        private static void DrawOutfits(Styles st)
        {
            CardBegin(st, "Saved outfits");
            GUILayout.Label("Save your draft with its materials and colour. Load a saved outfit to edit it before dressing.", st.dim);
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("outfit");
            s_outfitName = GUILayout.TextField(s_outfitName, 60, st.field);
            if (s_outfitName.Length == 0 && Repainting() && GUI.GetNameOfFocusedControl() != "outfit")
            {
                Rect rect = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(rect.x + 12f, rect.y, rect.width - 12f, rect.height), "Outfit name...", st.placeholder);
            }
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && ClothingPrefs.CanSave && Bikini.CurrentChoice().Items.Count > 0 && s_outfitName.Trim().Length > 0;
            bool replaces = ClothingPrefs.Sets.Any(x => string.Equals(x.Name, s_outfitName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (GUILayout.Button(replaces ? "Replace saved" : "Save draft", st.ghost, GUILayout.Width(130f)))
            {
                try
                {
                    ClothingPrefs.SaveSet(s_outfitName, Bikini.CurrentChoice());
                    SaveClothingPrefs();
                }
                catch (Exception ex) { s_clothingError = ex.Message; }
            }
            Tip(replaces ? "Replace the saved outfit with this name using your current draft." : "Save the draft's pieces, materials, colour and style.");
            GUI.enabled = enabled;
            GUILayout.EndHorizontal();
            if (ClothingPrefs.Sets.Count == 0) GUILayout.Label("No saved outfits yet. Build one in Browse, then give it a name here.", st.dim);
            foreach (var outfit in new List<ClothingPreferences.Outfit>(ClothingPrefs.Sets))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(ClothingCatalog.Escape(outfit.Name) + " · " + outfit.Items.Count + " items", st.labelWrap);
                if (GUILayout.Button("Load draft", st.ghost, GUILayout.Width(110f)))
                {
                    StoreDraft(new ClothingChoice(outfit.Items, outfit.Color, outfit.Solid, outfit.Materials, outfit.Colors));
                    s_outfitName = outfit.Name;
                    s_editItem = outfit.Items.FirstOrDefault() ?? "";
                    s_outfitView = 1;
                    s_deleteOutfit = "";
                }
                GUI.enabled = enabled && ClothingPrefs.CanSave;
                if (GUILayout.Button(s_deleteOutfit == outfit.Name ? "Delete now" : "Delete", st.ghost, GUILayout.Width(100f)))
                {
                    if (s_deleteOutfit == outfit.Name) { ClothingPrefs.DeleteSet(outfit.Name); SaveClothingPrefs(); s_deleteOutfit = ""; }
                    else s_deleteOutfit = outfit.Name;
                }
                GUI.enabled = enabled;
                GUILayout.EndHorizontal();
            }
            if (s_deleteOutfit.Length > 0 && GUILayout.Button("Cancel deletion", st.ghost)) s_deleteOutfit = "";
            if (!ClothingPrefs.CanSave)
                GUILayout.Label("The preferences file is damaged or unsupported. It has been preserved; reset it below only if you want to replace it.", st.labelWrap);
            if (GUILayout.Button(s_resetPreferences ? "Confirm: delete saved outfits and favorites" : "Reset saved outfits and favorites", st.ghost, GUILayout.Height(28f)))
            {
                if (s_resetPreferences) { ClothingPrefs.Reset(); SaveClothingPrefs(); s_shownKey = ""; s_resetPreferences = false; }
                else s_resetPreferences = true;
            }
            if (s_resetPreferences && GUILayout.Button("Cancel reset", st.ghost)) s_resetPreferences = false;
            CardEnd();
        }

        private static void SetColor(string id, Color c)
        {
            s_hex = "#" + ColorUtility.ToHtmlStringRGB(c);
            s_hexItem = id;
            StoreDraft(Bikini.CurrentChoice().WithPieceColor(id, s_hex));
        }

        private static void DrawMaterialChoices(Styles st, Entry entry, ClothingChoice choice)
        {
            choice.Materials.TryGetValue(entry.id, out var previewVariants);
            Color? tint = ColorUtility.TryParseHtmlString(choice.ColorFor(entry.id), out Color parsed) ? parsed : (Color?)null;
            DrawClothingPreview(st, entry, previewVariants, tint, 128f, choice.Solid);
            if (entry.map?.materialsPerIndex == null || entry.map.materialsPerIndex.Count == 0)
            {
                GUILayout.Label("This piece has no selectable material variants.", st.dim);
                return;
            }
            foreach (var group in entry.map.materialsPerIndex)
            {
                if (group?.materials == null || group.materials.Count == 0) continue;
                if (!choice.Materials.TryGetValue(entry.id, out var selected)) selected = new Dictionary<int, string>();
                selected.TryGetValue(group.materialIndex, out string current);
                var variants = new List<string>(); var names = new List<string>();
                for (int i = 0; i < group.materials.Count; i++)
                {
                    var material = group.materials[i];
                    if (material == null || string.IsNullOrWhiteSpace(material.id) || variants.Contains(material.id)) continue;
                    variants.Add(material.id);
                    names.Add(ClothingCatalog.Escape(ClothingCatalog.DisplayName(material.id, material.displayId, material.name, false)));
                }
                if (variants.Count == 0) continue;
                GUILayout.Label(entry.map.materialsPerIndex.Count == 1 ? "Material" : "Material " + (group.materialIndex + 1), st.dim);
                int index = variants.IndexOf(current); if (index < 0) index = 0;
                if (variants.Count == 1) { GUILayout.Label(names[0], st.dim); continue; }
                // This IL2CPP build strips GUIContent.Temp(string[]), used by SelectionGrid.
                int pick = index;
                for (int start = 0; start < names.Count; start += 2)
                {
                    int count = Math.Min(2, names.Count - start);
                    float width = (RowWidth - 8f) / 2f, height = 36f;
                    for (int i = start; i < start + count; i++)
                    {
                        s_measure.text = names[i];
                        height = Mathf.Max(height, st.optWrap.CalcHeight(s_measure, width));
                    }
                    GUILayout.BeginHorizontal();
                    for (int i = start; i < start + count; i++)
                        if (GUILayout.Button(names[i], i == index ? st.optOnWrap : st.optWrap, GUILayout.Width(width), GUILayout.Height(height))) pick = i;
                    GUILayout.EndHorizontal();
                }
                if (pick == index || pick < 0 || pick >= variants.Count) continue;
                selected[group.materialIndex] = variants[pick]; choice.Materials[entry.id] = selected;
                Plugin.BikiniMaterials.Value = choice.ToConfigMaterials(); MarkDirty();
            }
        }
    }
}
