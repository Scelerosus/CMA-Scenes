using System;
using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Chars.Ropa.Memorias;
using Assets._ReusableScripts.CuchiCuchi.Ropa;
using Assets.TValle.Tools.Runtime.Moddding.Clothing.Maps;
using UnityEngine;

namespace CMAScenes
{
    // Replaces equipped clothing through the game's wardrobe and retains the original material selections.
    internal static class Bikini
    {
        private sealed class OriginalPiece
        {
            public Il2CppSystem.Collections.Generic.List<SlotDeMaterialDeRopa> materials;
            public bool upper, lower;
            public ClothingItemMap.Type type;
            public bool typeRequired;
            public bool selectedOriginal;
        }
        private sealed class State
        {
            public Character woman;
            public IRopaManager manager;
            public readonly Dictionary<string, OriginalPiece> removed = new();
            public readonly HashSet<string> restoring = new(StringComparer.Ordinal);
            public readonly HashSet<string> loading = new(StringComparer.Ordinal);
            public readonly Queue<Action> additions = new();
            public readonly HashSet<string> applied = new(StringComparer.Ordinal);
            public float nextWardrobeScan, loadDeadline;
            public bool loadStalled;
            public string error;
            public bool cleanupError;
            public bool restoreRequested;
            public bool pending;
            public ClothingChoice pendingChoice;
            public ShakeController pendingHost;
            public readonly HashSet<string> added = new(StringComparer.Ordinal);
            public readonly Dictionary<string, List<Material>> materials = new(StringComparer.Ordinal);
            public readonly Dictionary<string, List<SkinnedMeshRenderer>> renderers = new(StringComparer.Ordinal);
            public bool on;
        }

        private static readonly Dictionary<long, State> States = new Dictionary<long, State>();

        private static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }
            return s.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "").Replace(".", "");
        }

        internal static string[] Keys(string csv)
        {
            var list = new List<string>();
            foreach (string k in (csv ?? "").Split(','))
            {
                string n = Norm(k.Trim());
                if (n.Length > 0)
                {
                    list.Add(n);
                }
            }
            return list.ToArray();
        }

        private static bool Matches(ClothingItemMap map, string key, string exclude)
        {
            string[] names = { Norm(map.name), Norm(map.id), Norm(map.fullName), Norm(map.displayId) };
            foreach (string n in names)
            {
                if (n.Length > 0 && n.Contains(key) && (exclude.Length == 0 || !n.Contains(exclude)))
                {
                    return true;
                }
            }
            return false;
        }

        // A name that is exactly the id (or the name) of one item is a pick from the menu: take that item, not
        // the first one whose name merely contains it ("bikinitop" is also inside "microbikinitop").
        private static bool Exact(ClothingItemMap map, string key)
        {
            return Norm(map.id) == key || Norm(map.name) == key || Norm(map.fullName) == key || Norm(map.displayId) == key;
        }

        private static ClothingItemMap Find(List<ClothingItemMap> maps, string[] keys, string exclude)
        {
            foreach (string key in keys)
            {
                foreach (ClothingItemMap m in maps)
                {
                    if (Exact(m, key))
                    {
                        return m;
                    }
                }
                foreach (ClothingItemMap m in maps)
                {
                    if (Matches(m, key, exclude))
                    {
                        return m;
                    }
                }
            }
            return null;
        }

        // Resolve a legacy Top or Bottom config value, or return null.
        internal static ClothingItemMap Resolve(List<ClothingItemMap> maps, string setting, bool bottom)
        {
            return Find(maps, Keys(setting), bottom ? "half" : "");
        }

        internal static List<ClothingItemMap> LoadMaps()
        {
            if (cachedMaps != null && Time.unscaledTime < nextMapScan) return new List<ClothingItemMap>(cachedMaps);
            var maps = new List<ClothingItemMap>();
            var all = Resources.FindObjectsOfTypeAll<ClothingItemMap>();
            if (all != null)
            {
                foreach (ClothingItemMap m in all)
                {
                    if (m != null)
                    {
                        maps.Add(m);
                    }
                }
            }
            foreach (MapaDeRopa catalog in Resources.FindObjectsOfTypeAll<MapaDeRopa>())
                if (catalog?.piezas != null)
                    for (int i = 0; i < catalog.piezas.Count; i++)
                    {
                        var data = catalog.piezas[i];
                        if (data == null || data.paraSexo == Sexo.masculino || string.IsNullOrWhiteSpace(data.stringId)) continue;
                        string name = data.enumID switch
                        {
                            MapaDeRopa.RopaPreSetId.glasesCatEye => "Cat eye glasses",
                            MapaDeRopa.RopaPreSetId.glasesSquare => "Square glasses",
                            MapaDeRopa.RopaPreSetId.glasesRound => "Round glasses",
                            _ => null
                        };
                        if (name == null || maps.Exists(m => m.id == data.stringId)) continue;
                        if (!nativeGlasses.TryGetValue(data.stringId, out var proxy) || proxy == null)
                        {
                            proxy = ScriptableObject.CreateInstance<ClothingItemMap>();
                            proxy.name = name; proxy.m_id = data.stringId; proxy.m_displayId = name;
                            proxy.fullName = name; proxy.sex = ClothingItemMap.Sex.female;
                            proxy.type = ClothingItemMap.Type.glasses;
                            nativeGlasses[data.stringId] = proxy;
                        }
                        maps.Add(proxy);
                    }
            cachedMaps = maps;
            nextMapScan = Time.unscaledTime + 2;
            return new List<ClothingItemMap>(maps);
        }

        private static List<ClothingItemMap> cachedMaps;
        private static float nextMapScan;
        private static readonly Dictionary<string, ClothingItemMap> nativeGlasses = new(StringComparer.Ordinal);

        // A woman who is gone (another scene was loaded, she was removed) takes what was remembered of her with her. Left in,
        // a bikini "still on" someone who no longer exists kept the switch stuck at worn, and it could not be put on again.
        private static void Prune()
        {
            List<long> gone = null;
            foreach (KeyValuePair<long, State> kv in States)
            {
                if (kv.Value.woman == null)
                {
                    (gone ?? (gone = new List<long>())).Add(kv.Key);
                }
            }
            if (gone != null)
            {
                foreach (long key in gone)
                {
                    ReleaseMaterials(States[key]);
                    States.Remove(key);
                }
            }
        }

        internal static bool AnyOn()
        {
            Prune();
            return IsOn(WardrobeCharacters.Selected);
        }

        internal static bool LoadingStalled() => WardrobeCharacters.Selected != null
            && States.TryGetValue(WardrobeCharacters.Selected.Pointer.ToInt64(), out State state) && state.loadStalled;

        internal static bool LoadingSelected() => WardrobeCharacters.Selected != null
            && States.TryGetValue(WardrobeCharacters.Selected.Pointer.ToInt64(), out State state)
            && (state.pending || state.restoreRequested || state.loading.Count != 0 || state.restoring.Count != 0
                || state.additions.Count != 0 || !state.on && state.added.Count != 0);

        internal static string SelectedError() => WardrobeCharacters.Selected != null
            && States.TryGetValue(WardrobeCharacters.Selected.Pointer.ToInt64(), out State state) ? state.error : null;

        internal static bool CanRestoreSelected() => WardrobeCharacters.Selected != null
            && States.TryGetValue(WardrobeCharacters.Selected.Pointer.ToInt64(), out State state)
            && (state.removed.Count != 0 || state.added.Count != 0 || state.pending || state.restoreRequested
                || state.loading.Count != 0 || state.restoring.Count != 0);

        internal static bool IsOn(Character character) => character != null && States.TryGetValue(character.Pointer.ToInt64(), out State state) && state.woman == character && (state.on || state.pending);

        private static void QueueApply(State state, ShakeController host, ClothingChoice choice)
        {
            state.pending = true;
            state.pendingHost = host;
            state.pendingChoice = choice;
        }

        internal static void Step()
        {
            Prune();
            foreach (State state in new List<State>(States.Values))
            {
                try
                {
                    if (state.loading.Count != 0 || state.restoring.Count != 0)
                    {
                        if (!state.loadStalled && Time.unscaledTime >= state.loadDeadline)
                        {
                            state.loadStalled = true;
                            state.on = false; state.pending = false; state.restoreRequested = false;
                            state.additions.Clear();
                            state.error = "Clothing loading stalled. Reload the game before retrying; the original outfit was retained.";
                            Plugin.ModLog.LogWarning("Outfit: native clothing load stalled. Reload the game before retrying; original wardrobe snapshot retained.");
                        }
                        continue;
                    }
                    state.loadStalled = false;
                    if (state.manager.isLoadingConjunto) continue;
                    if (!state.on && state.added.Count != 0)
                    {
                        if (Time.unscaledTime < state.nextWardrobeScan) continue;
                        state.nextWardrobeScan = Time.unscaledTime + .5f;
                        var owned = new List<string>(state.added);
                        bool clean = WardrobeRemoval.Cleanup(state.manager, state.added, state.loading, state.pendingHost, state.renderers);
                        foreach (string id in owned) if (!state.added.Contains(id)) ReleaseMaterials(state, id);
                        if (!clean)
                        {
                            if (string.IsNullOrEmpty(state.error))
                            {
                                state.error = "The game has not removed every garment yet. Cleanup will retry automatically.";
                                state.cleanupError = true;
                            }
                            continue;
                        }
                        if (state.cleanupError) { state.error = null; state.cleanupError = false; }
                    }
                    if (state.on && state.additions.Count != 0)
                    {
                        try { state.additions.Dequeue()(); }
                        catch (Exception ex) { state.error = "A garment could not load. Check the log for its name."; Plugin.ModLog.LogWarning("Clothing load failed: " + ex); }
                        continue;
                    }
                    if (state.on && !state.pending && state.restoring.Count == 0 && !state.manager.isLoadingConjunto && Time.unscaledTime >= state.nextWardrobeScan)
                    {
                        state.nextWardrobeScan = Time.unscaledTime + .5f;
                        foreach (string id in new List<string>(state.applied))
                        {
                            state.renderers.TryGetValue(id, out var renderers);
                            if (!WardrobeRemoval.SynchronizeRemoved(state.manager, id, state.added.Contains(id), state.pendingHost, renderers)) continue;
                            state.applied.Remove(id); state.added.Remove(id);
                            ReleaseMaterials(state, id);
                            Plugin.ModLog.LogInfo($"Outfit: '{id}' removed through the game; worn state synchronized.");
                        }
                        if (state.applied.Count == 0) state.on = false;
                    }
                    if (state.restoreRequested)
                    {
                        if (state.restoring.Count != 0) continue;
                        if (state.pendingHost != null && state.woman != null && state.removed.Count != 0)
                        {
                            foreach (string id in new List<string>(state.removed.Keys)) { Restore(state.pendingHost, state, id); break; }
                            continue;
                        }
                        state.restoreRequested = false;
                    }
                    if (!state.pending || state.restoring.Count != 0) continue;
                    var host = state.pendingHost;
                    var choice = state.pendingChoice;
                    state.pending = false;
                    if (host == null || state.woman == null || !state.woman.gameObject.activeInHierarchy) continue;
                    try { PutOn(host, state.woman, state, LoadMaps(), choice); }
                    catch (Exception ex) { state.error = "The selected outfit could not be applied. The original outfit is retained for restoration."; Plugin.ModLog.LogWarning("Clothing replacement failed: " + ex); }
                }
                catch (Exception ex)
                {
                    state.error = "The wardrobe could not finish this change. Try restoring the original outfit.";
                    Plugin.ModLog.LogWarning("Outfit update failed: " + ex);
                }
            }
        }

        // The two halves of the body a bikini stands in for. Shoes, socks, gloves, hats and jewellery cover neither.
        private const RopaCubre UpperBody = RopaCubre.pectorales | RopaCubre.pezones | RopaCubre.torzo | RopaCubre.espalda;
        private const RopaCubre LowerBody = RopaCubre.labiosVaginales | RopaCubre.vaginaHole | RopaCubre.nalgas | RopaCubre.ano | RopaCubre.vientreBajo;

        internal static void Set(ShakeController host, bool on)
        {
            SetFor(host, WardrobeCharacters.Selected, on);
        }

        internal static ClothingChoice CurrentChoice()
        {
            if (!string.IsNullOrWhiteSpace(Plugin.BikiniItems.Value))
                return ClothingChoice.FromConfig(Plugin.BikiniItems.Value, "", "", Plugin.BikiniColor.Value, Plugin.BikiniSolid.Value, Plugin.BikiniMaterials.Value, Plugin.BikiniColors.Value);
            var items = new List<string>();
            var maps = LoadMaps();
            var top = Resolve(maps, Plugin.BikiniTop.Value, false);
            var bottom = Resolve(maps, Plugin.BikiniBottom.Value, true);
            if (top != null) items.Add(top.id);
            if (bottom != null && !items.Contains(bottom.id)) items.Add(bottom.id);
            return ClothingChoice.FromConfig(string.Join("|", items), "", "", Plugin.BikiniColor.Value, Plugin.BikiniSolid.Value,
                Plugin.BikiniMaterials.Value, Plugin.BikiniColors.Value);
        }

        // Snapshot the selected character's live wardrobe on the Unity thread. Keep only managed IDs/slot data.
        internal static Dictionary<string, Dictionary<int, string>> ReadWorn(out HashSet<string> ids)
        {
            ids = new HashSet<string>(StringComparer.Ordinal);
            var variants = new Dictionary<string, Dictionary<int, string>>(StringComparer.Ordinal);
            try
            {
                Character character = WardrobeCharacters.Selected;
                if (character == null) return variants;
                RopaAdminDeCharacter admin = character.GetComponentInChildren<RopaAdminDeCharacter>(true);
                IRopaManager manager = admin?.manager;
                if (manager == null || manager.isLoadingConjunto) return variants;
                var worn = new Il2CppSystem.Collections.Generic.List<string>();
                manager.ObtenerPiezasIDs(worn.Cast<Il2CppSystem.Collections.Generic.ICollection<string>>(), false);
                var maps = LoadMaps();
                for (int i = 0; i < worn.Count; i++)
                {
                    string id = worn[i];
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    if (!manager.Usando(id, true)) continue;
                    ids.Add(id);
                    var map = Resolve(maps, id, false);
                    if (map?.materialsPerIndex == null || !manager.piezasPuestasPorId.ContainsKey(id)) continue;
                    var piece = manager.piezasPuestasPorId[id];
                    var slots = piece?.materialesData;
                    if (slots == null) continue;
                    var selected = new Dictionary<int, string>();
                    int slotCount = slots.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<SlotDeMaterialDeRopa>>().Count;
                    foreach (var group in map.materialsPerIndex)
                    {
                        if (group == null || group.materials == null) continue;
                        if (group.materialIndex < 0) continue;
                        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                        {
                            var slot = slots[slotIndex];
                            if (slot == null || slot.materialSlot != group.materialIndex) continue;
                            string materialId = slot.materialIDString;
                            if (string.IsNullOrWhiteSpace(materialId)) continue;
                            for (int j = 0; j < group.materials.Count; j++)
                                if (group.materials[j] != null && group.materials[j].id == materialId)
                                { selected[group.materialIndex] = materialId; break; }
                            if (selected.ContainsKey(group.materialIndex)) break;
                        }
                    }
                    if (selected.Count > 0) variants[id] = selected;
                }
            }
            catch (Exception ex) { Plugin.ModLog.LogWarning("Outfit: could not read the worn wardrobe: " + ex.Message); }
            return variants;
        }

        internal static ClothingChoice WornChoice()
        {
            var variants = ReadWorn(out var ids);
            return new ClothingChoice(ids, Plugin.BikiniColor.Value, Plugin.BikiniSolid.Value, variants, CurrentChoice().Colors);
        }

        internal static bool RandomizeMaterials(ClothingChoice choice)
        {
            bool changed = false;
            var maps = LoadMaps();
            foreach (string id in choice.Items)
            {
                ClothingItemMap piece = Resolve(maps, id, false);
                if (piece?.materialsPerIndex == null) continue;
                if (!choice.Materials.TryGetValue(piece.id, out var selected))
                    choice.Materials[piece.id] = selected = new Dictionary<int, string>();
                foreach (var group in piece.materialsPerIndex)
                {
                    if (group?.materials == null || group.materials.Count == 0) continue;
                    var valid = new List<string>();
                    for (int i = 0; i < group.materials.Count; i++)
                    {
                        var material = group.materials[i];
                        if (material != null && !string.IsNullOrWhiteSpace(material.id)) valid.Add(material.id);
                    }
                    if (valid.Count == 0) continue;
                    selected[group.materialIndex] = valid[UnityEngine.Random.Range(0, valid.Count)];
                    changed = true;
                }
            }
            return changed;
        }

        internal static void SetFor(ShakeController host, Character selected, bool on, ClothingChoice choice = null, bool restoreOriginal = true)
        {
            List<ClothingItemMap> maps = on ? LoadMaps() : new List<ClothingItemMap>();
            if (on && maps.Count == 0)
            {
                Plugin.ModLog.LogWarning("Bikini: no clothing maps are loaded yet.");
                return;
            }

            Prune();
            foreach (Character c in new[] { selected })
            {
                // Women only: the hero has a wardrobe too.
                if (c == null || !c.gameObject.scene.IsValid() || !c.gameObject.activeInHierarchy || c.sexo == Sexo.masculino)
                {
                    continue;
                }
                RopaAdminDeCharacter admin = c.GetComponentInChildren<RopaAdminDeCharacter>(true);
                if (admin == null || admin.manager == null)
                {
                    continue;
                }
                long key = c.Pointer.ToInt64();
                if (!States.TryGetValue(key, out State state) || state.woman != c)
                {
                    state = new State();
                    States[key] = state;
                }
                state.woman = c;
                state.manager = admin.manager;
                if (state.loadStalled) continue;
                try
                {
                    if (on && !state.on)
                    {
                        if (state.restoring.Count > 0 || state.loading.Count > 0 || state.restoreRequested || state.added.Count > 0 || state.manager.isLoadingConjunto) QueueApply(state, host, choice ?? CurrentChoice());
                        else PutOn(host, c, state, maps, choice ?? CurrentChoice());
                    }
                    else if (!on && (state.on || state.pending || state.removed.Count > 0 || state.loading.Count > 0 || state.added.Count > 0))
                    {
                        state.error = null; state.cleanupError = false;
                        TakeOff(host, c, state, restoreOriginal);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.ModLog.LogWarning($"Bikini failed for '{c.name}': {ex}");
                }
            }
        }

        private static void PutOn(ShakeController host, Character woman, State state, List<ClothingItemMap> maps, ClothingChoice choice)
        {
            IRopaManager mgr = state.manager;
            state.pendingHost = host;
            state.error = null; state.cleanupError = false;
            var pieces = new List<ClothingItemMap>();
            foreach (string selected in choice.Items)
            {
                ClothingItemMap map = Resolve(maps, selected, false);
                if (map != null && pieces.Find(p => p.id == map.id) == null) pieces.Add(map);
            }
            Plugin.ModLog.LogInfo($"Outfit pieces for '{woman.name}': {string.Join(", ", pieces.ConvertAll(p => p.id))}");
            if (pieces.Count == 0)
            {
                var names = new List<string>();
                foreach (ClothingItemMap m in maps)
                {
                    string n = Norm(m.name) + Norm(m.id);
                    if (n.Contains("bikini") || n.Contains("tanga") || n.Contains("string") || n.Contains("bra"))
                    {
                        names.Add($"{m.name} [{m.id}]");
                    }
                }
                Plugin.ModLog.LogWarning("Bikini: nothing matched. Candidates: " + string.Join("; ", names) + ". Set Bikini/Top and Bikini/Bottom in the config to part of one of these names.");
                TakeOff(host, woman, state, true);
                return;
            }

            // Replace only selected body coverage or another item of a selected non-body type.
            var byId = new Dictionary<string, ClothingItemMap>();
            foreach (ClothingItemMap m in maps)
            {
                if (!string.IsNullOrEmpty(m.id) && !byId.ContainsKey(m.id))
                {
                    byId[m.id] = m;
                }
            }
            // What the game says each worn piece covers decides it, not the list of installed clothing items: her own
            // outfit is not in that list. A piece goes only when everything it covers is replaced by the bikini, so
            // with only a top picked a dress stays rather than leaving her bare below.
            bool replaceUpper = false, replaceLower = false;
            var selectedTypes = new HashSet<ClothingItemMap.Type>();
            foreach (ClothingItemMap piece in pieces)
            {
                selectedTypes.Add(piece.type);
                RopaCubre covers = RopaCubre.None;
                try { covers = mgr.ObtenerMapa()?.ObtenerData(piece.id)?.cubreFlag ?? RopaCubre.None; }
                catch (Exception ex) { Plugin.ModLog.LogWarning($"Outfit: could not read selected coverage for '{piece.id}': {ex.Message}"); }
                if (covers != RopaCubre.None)
                {
                    replaceUpper |= (covers & UpperBody) != 0;
                    replaceLower |= (covers & LowerBody) != 0;
                    continue;
                }
                switch (piece.type)
                {
                    case ClothingItemMap.Type.upperBodyUnderwear: case ClothingItemMap.Type.upperBody: case ClothingItemMap.Type.jacket:
                        replaceUpper = true; break;
                    case ClothingItemMap.Type.lowerBodyUnderwear: case ClothingItemMap.Type.lowerBody:
                        replaceLower = true; break;
                    case ClothingItemMap.Type.swimsuit:
                        replaceUpper = replaceLower = true; break;
                }
            }
            var noLongerReplaced = new List<string>();
            foreach (var original in state.removed)
            {
                if (original.Value.selectedOriginal && pieces.Exists(p => p.id == original.Key)) continue;
                if ((!original.Value.upper || replaceUpper) && (!original.Value.lower || replaceLower)
                    && (!original.Value.typeRequired || selectedTypes.Contains(original.Value.type))) continue;
                noLongerReplaced.Add(original.Key);
            }
            if (noLongerReplaced.Count > 0)
            {
                QueueApply(state, host, choice);
                Restore(host, state, noLongerReplaced[0]);
                return;
            }
            var worn = new Il2CppSystem.Collections.Generic.List<string>();
            mgr.ObtenerPiezasIDs(worn.Cast<Il2CppSystem.Collections.Generic.ICollection<string>>(), false);
            for (int i = 0; i < worn.Count; i++)
            {
                string id = worn[i];
                bool selectedOriginal = pieces.Exists(p => p.id == id);
                RopaCubre covers = RopaCubre.None;
                try
                {
                    covers = mgr.PiezaCubreFlags(id, false);
                }
                catch (Exception ex)
                {
                    Plugin.ModLog.LogWarning($"Bikini: could not read what '{id}' covers: {ex.Message}");
                }
                bool upper = (covers & UpperBody) != 0;
                bool lower = (covers & LowerBody) != 0;
                if (!upper && !lower && byId.TryGetValue(id, out ClothingItemMap map))
                {
                    // A piece that reports nothing is judged by what kind of item it is.
                    switch (map.type)
                    {
                        case ClothingItemMap.Type.upperBodyUnderwear:
                        case ClothingItemMap.Type.upperBody:
                            upper = true;
                            break;
                        case ClothingItemMap.Type.lowerBodyUnderwear:
                        case ClothingItemMap.Type.lowerBody:
                            lower = true;
                            break;
                        case ClothingItemMap.Type.jacket:
                        case ClothingItemMap.Type.swimsuit:
                            upper = true;
                            lower = true;
                            break;
                    }
                }
                bool sameType = byId.TryGetValue(id, out ClothingItemMap wornMap) && selectedTypes.Contains(wornMap.type);
                bool replace = selectedOriginal || sameType || (upper || lower) && (!upper || replaceUpper) && (!lower || replaceLower);
                bool done = false;
                if (replace)
                {
                    // Capture independent data before RemovePieza destroys the equipped object.
                    var original = mgr.piezasPuestasPorId[id];
                    var materials = ClothingMaterials.Snapshot(original);
                    done = WardrobeRemoval.Remove(mgr, id, host);
                    if (!done) state.error = "The game could not remove a conflicting garment. Restore the original outfit and retry.";
                    if (done && !state.removed.ContainsKey(id)) state.removed[id] = new OriginalPiece { materials = materials, upper = upper, lower = lower,
                        type = wornMap != null ? wornMap.type : ClothingItemMap.Type.None, typeRequired = sameType, selectedOriginal = selectedOriginal };
                }
                Plugin.ModLog.LogInfo($"Clothing: worn '{id}' covers [{covers}] -> {(done ? "removed from equipped wardrobe" : replace ? "could not remove" : "kept")}");
            }

            var addedThisTime = new HashSet<string>(StringComparer.Ordinal);
            state.on = true;
            state.applied.Clear();
            foreach (var selected in pieces) state.applied.Add(selected.id);
            foreach (ClothingItemMap piece in pieces)
            {
                if (piece == null || string.IsNullOrEmpty(piece.id) || !addedThisTime.Add(piece.id))
                {
                    continue;
                }
                string id = piece.id;
                ColorOf(choice.ColorFor(id), out Color color);
                // Reload a selected original too: fabric and solid colour changes require fresh native material data.
                if (mgr.Usando(id, false)) continue;
                var materials = MaterialSlots(piece, choice.Materials, color);
                state.added.Add(id);
                var done = DelegateSupportAction(piece2 =>
                {
                    try
                    {
                        if ((!state.on || state.woman == null) && piece2 != null)
                        {
                            if (WardrobeRemoval.Remove(mgr, id, host)) state.added.Remove(id);
                        }
                        else if (piece2 != null) Tint(piece2, id, color, state, choice.Solid);
                        else
                        {
                            TakeOff(host, woman, state, true);
                            state.error = "A selected garment did not load. Try another item or material.";
                            Plugin.ModLog.LogWarning($"Clothing: replacement '{id}' did not load.");
                        }
                    }
                    finally { state.loading.Remove(id); }
                });
                // The native wardrobe loads one item at a time; concurrent loads can leave unregistered renderers.
                state.additions.Enqueue(() =>
                {
                    state.loading.Add(id);
                    state.loadDeadline = Time.unscaledTime + 60;
                    try { host.StartCoroutine(mgr.AddPiezaAsync<PiezaDeRopaBase>(id, materials.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<SlotDeMaterialDeRopa>>(), done, false)); }
                    catch { state.loading.Remove(id); TakeOff(host, woman, state, true); throw; }
                });
            }
            Plugin.ModLog.LogInfo($"Clothing on: replaced {state.removed.Count} piece(s), adding {state.added.Count}.");
        }

        private static Il2CppSystem.Collections.Generic.List<SlotDeMaterialDeRopa> MaterialSlots(ClothingItemMap piece, Dictionary<string, Dictionary<int, string>> variants, Color color)
        {
            var slots = new Il2CppSystem.Collections.Generic.List<SlotDeMaterialDeRopa>();
            if (piece?.materialsPerIndex == null || !variants.TryGetValue(piece.id, out var selected)) return slots;
            foreach (var group in piece.materialsPerIndex)
            {
                if (group == null || group.materialIndex < 0 || group.materials == null || !selected.TryGetValue(group.materialIndex, out string id)) continue;
                for (int i = 0; i < group.materials.Count; i++)
                {
                    var material = group.materials[i];
                    if (material == null || material.id != id) continue;
                    slots.Add(ClothingMaterials.Create(id, group.materialIndex, color));
                    break;
                }
            }
            return slots;
        }

        private static void TakeOff(ShakeController host, Character woman, State state, bool restoreOriginal)
        {
            state.pending = false;
            state.on = false;
            state.additions.Clear();
            state.applied.Clear();
            state.pendingHost = host;
            state.restoreRequested = restoreOriginal;
            IRopaManager mgr = state.manager;
            var owned = new List<string>(state.added);
            WardrobeRemoval.Cleanup(mgr, state.added, state.loading, host, state.renderers);
            foreach (string id in owned) if (!state.added.Contains(id)) ReleaseMaterials(state, id);
            Plugin.ModLog.LogInfo($"Clothing off: {state.added.Count} removal(s) pending, {(restoreOriginal ? "restoring" : "retaining original snapshot of")} {state.removed.Count}.");
        }

        private static void Restore(ShakeController host, State state, string id)
        {
            if (state.restoring.Contains(id)) return;
            if (state.manager.Usando(id, false)) { state.removed.Remove(id); return; }
            var original = state.removed[id];
            state.restoring.Add(id);
            state.loadDeadline = Time.unscaledTime + 60;
            try
            {
                var done = DelegateSupportAction(piece =>
                {
                    state.restoring.Remove(id);
                    if (piece != null)
                    {
                        state.removed.Remove(id);
                    }
                    else
                    {
                        state.pending = false;
                        state.restoreRequested = false;
                        state.error = "A garment from the original outfit could not load. Its settings are retained so restoration can be retried.";
                        Plugin.ModLog.LogWarning($"Clothing: could not restore '{id}'; original data retained for retry.");
                    }
                });
                host.StartCoroutine(state.manager.AddPiezaAsync<PiezaDeRopaBase>(id, original.materials.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<SlotDeMaterialDeRopa>>(), done, false));
            }
            catch (Exception ex)
            {
                state.restoring.Remove(id);
                state.pending = false;
                state.restoreRequested = false;
                state.error = "The original outfit could not be restored. Its settings are retained for retry.";
                Plugin.ModLog.LogWarning($"Clothing: restore '{id}' failed; original data retained: {ex}");
            }
        }

        private static void ReleaseMaterials(State state, string id = null)
        {
            foreach (string piece in id == null ? new List<string>(state.materials.Keys) : new List<string> { id })
            {
                state.renderers.Remove(piece);
                if (!state.materials.Remove(piece, out var materials)) continue;
                foreach (Material material in materials) if (material != null) UnityEngine.Object.Destroy(material);
            }
        }

        private static Il2CppSystem.Action<PiezaDeRopaBase> DelegateSupportAction(Action<PiezaDeRopaBase> managed)
        {
            return Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<PiezaDeRopaBase>>(managed);
        }

        private static void ColorOf(string value, out Color color)
        {
            if (!ColorUtility.TryParseHtmlString(value, out color))
            {
                color = new Color(0.85f, 0.1f, 0.35f, 1f);
            }
        }

        // The piece is in the scene by the time the game calls back: tint its materials to the chosen colour.
        private static void Tint(PiezaDeRopaBase piece, string id, Color color, State state, bool solid)
        {
            try
            {
                if (piece == null)
                {
                    return;
                }
                var owned = new List<Material>();
                state.materials[id] = owned;
                state.renderers[id] = WardrobeRemoval.Renderers(piece);
                var renderers = new HashSet<Renderer>();
                foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true)) if (renderer != null) renderers.Add(renderer);
                foreach (Renderer renderer in WardrobeRemoval.Renderers(piece)) renderers.Add(renderer);
                var slots = piece.materialesData;
                if (slots != null)
                {
                    int count = slots.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<SlotDeMaterialDeRopa>>().Count;
                    for (int i = 0; i < count; i++) if (slots[i] != null) piece.ChangeMainColor(slots[i].materialSlot, color);
                }
                foreach (Renderer r in renderers)
                {
                    if (r == null)
                    {
                        continue;
                    }
                    var materials = r.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == null)
                        {
                            continue;
                        }
                        var mat = new Material(materials[i]);
                        owned.Add(mat); materials[i] = mat;
                        if (mat.HasProperty("_BaseColor"))
                        {
                            mat.SetColor("_BaseColor", color);
                        }
                        else if (mat.HasProperty("_Color"))
                        {
                            mat.SetColor("_Color", color);
                        }
                        if (solid) foreach (string property in new[] { "_BaseColorMap", "_BaseMap", "_MainTex" })
                            if (mat.HasProperty(property)) mat.SetTexture(property, null);
                    }
                    r.sharedMaterials = materials;
                }
                Plugin.ModLog.LogInfo($"Bikini piece '{piece.name}' tinted.");
            }
            catch (Exception ex)
            {
                state.error = "The garment loaded, but its colour could not be applied.";
                Plugin.ModLog.LogWarning("Bikini tint failed: " + ex.Message);
            }
        }
    }
}
