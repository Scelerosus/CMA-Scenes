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
    // Puts her in a bikini built from clothing mods that are already installed: it hides what she is wearing on the
    // body, adds a bikini top and bottom through the game's own wardrobe, and tints them to one colour.
    internal static class Bikini
    {
        private sealed class State
        {
            public Character woman;
            public IRopaManager manager;
            public readonly List<string> hidden = new List<string>();
            public readonly List<string> added = new List<string>();
            // The materials of the bikini pieces, kept so the colour can be changed while she wears them.
            public readonly List<Material> mats = new List<Material>();
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

        // The item a Bikini/Top or Bikini/Bottom setting stands for, or null.
        internal static ClothingItemMap Resolve(List<ClothingItemMap> maps, string setting, bool bottom)
        {
            return Find(maps, Keys(setting), bottom ? "half" : "");
        }

        internal static List<ClothingItemMap> LoadMaps()
        {
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
            return maps;
        }

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
                    States.Remove(key);
                }
            }
        }

        internal static bool AnyOn()
        {
            Prune();
            foreach (State s in States.Values)
            {
                if (s.on)
                {
                    return true;
                }
            }
            return false;
        }

        // The two halves of the body a bikini stands in for. Shoes, socks, gloves, hats and jewellery cover neither.
        private const RopaCubre UpperBody = RopaCubre.pectorales | RopaCubre.pezones | RopaCubre.torzo | RopaCubre.espalda;
        private const RopaCubre LowerBody = RopaCubre.labiosVaginales | RopaCubre.vaginaHole | RopaCubre.nalgas | RopaCubre.ano | RopaCubre.vientreBajo;

        internal static void Set(ShakeController host, bool on)
        {
            List<ClothingItemMap> maps = LoadMaps();
            if (on && maps.Count == 0)
            {
                Plugin.ModLog.LogWarning("Bikini: no clothing maps are loaded yet.");
                return;
            }

            Prune();
            var characters = Resources.FindObjectsOfTypeAll<Character>();
            if (characters == null)
            {
                return;
            }
            foreach (Character c in characters)
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
                if (!States.TryGetValue(key, out State state))
                {
                    state = new State();
                    States[key] = state;
                }
                state.woman = c;
                state.manager = admin.manager;
                try
                {
                    if (on && !state.on)
                    {
                        PutOn(host, c, state, maps);
                    }
                    else if (!on && state.on)
                    {
                        TakeOff(host, c, state);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.ModLog.LogWarning($"Bikini failed for '{c.name}': {ex}");
                }
            }
        }

        private static void PutOn(ShakeController host, Character woman, State state, List<ClothingItemMap> maps)
        {
            IRopaManager mgr = state.manager;
            ClothingItemMap top = Resolve(maps, Plugin.BikiniTop.Value, false);
            ClothingItemMap bottom = Resolve(maps, Plugin.BikiniBottom.Value, true);
            Plugin.ModLog.LogInfo($"Bikini pieces for '{woman.name}': top={(top != null ? top.id : "NOT FOUND")} bottom={(bottom != null ? bottom.id : "NOT FOUND")}");
            if (top == null && bottom == null)
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
                return;
            }

            // Hide what she wears on the body; shoes, socks and jewellery stay.
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
            bool replaceUpper = top != null;
            bool replaceLower = bottom != null;
            var worn = new Il2CppSystem.Collections.Generic.List<string>();
            mgr.ObtenerPiezasIDs(worn.Cast<Il2CppSystem.Collections.Generic.ICollection<string>>(), false);
            for (int i = 0; i < worn.Count; i++)
            {
                string id = worn[i];
                if ((top != null && id == top.id) || (bottom != null && id == bottom.id))
                {
                    continue;
                }
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
                bool hide = (upper || lower) && (!upper || replaceUpper) && (!lower || replaceLower);
                bool done = hide && mgr.OcultarPieza(id, true, host);
                if (done)
                {
                    state.hidden.Add(id);
                }
                Plugin.ModLog.LogInfo($"Bikini: worn '{id}' covers [{covers}] -> {(done ? "hidden" : hide ? "could not hide" : "kept")}");
            }

            ColorOf(out Color color);
            foreach (ClothingItemMap piece in new[] { top, bottom })
            {
                if (piece == null || string.IsNullOrEmpty(piece.id))
                {
                    continue;
                }
                string id = piece.id;
                var materials = new Il2CppSystem.Collections.Generic.List<SlotDeMaterialDeRopa>();
                var done = DelegateSupportAction(piece2 => Tint(piece2, color, state));
                host.StartCoroutine(mgr.AddPiezaAsync<PiezaDeRopa>(id, materials.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<SlotDeMaterialDeRopa>>(), done, false));
                state.added.Add(id);
            }
            state.on = true;
            Plugin.ModLog.LogInfo($"Bikini on: hid {state.hidden.Count} piece(s), adding {state.added.Count}.");
        }

        private static void TakeOff(ShakeController host, Character woman, State state)
        {
            IRopaManager mgr = state.manager;
            foreach (string id in state.added)
            {
                mgr.RemovePieza(id, true, host);
            }
            foreach (string id in state.hidden)
            {
                mgr.OcultarPieza(id, false, host);
            }
            Plugin.ModLog.LogInfo($"Bikini off: removed {state.added.Count} piece(s), restored {state.hidden.Count}.");
            state.added.Clear();
            state.hidden.Clear();
            state.mats.Clear();
            state.on = false;
        }

        // Changes the colour of the bikini she is wearing right now, without taking it off.
        internal static void Retint()
        {
            ColorOf(out Color color);
            foreach (State state in States.Values)
            {
                if (!state.on)
                {
                    continue;
                }
                foreach (Material mat in state.mats)
                {
                    if (mat == null)
                    {
                        continue;
                    }
                    if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", color);
                    }
                    else if (mat.HasProperty("_Color"))
                    {
                        mat.SetColor("_Color", color);
                    }
                }
            }
        }

        private static Il2CppSystem.Action<PiezaDeRopa> DelegateSupportAction(Action<PiezaDeRopa> managed)
        {
            return Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<PiezaDeRopa>>(managed);
        }

        private static void ColorOf(out Color color)
        {
            if (!ColorUtility.TryParseHtmlString(Plugin.BikiniColor.Value, out color))
            {
                color = new Color(0.85f, 0.1f, 0.35f, 1f);
            }
        }

        // The piece is in the scene by the time the game calls back: tint its materials to the chosen colour.
        private static void Tint(PiezaDeRopa piece, Color color, State state)
        {
            try
            {
                if (piece == null)
                {
                    return;
                }
                foreach (Renderer r in piece.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null)
                    {
                        continue;
                    }
                    foreach (Material mat in r.materials)
                    {
                        if (mat == null)
                        {
                            continue;
                        }
                        state.mats.Add(mat);
                        if (mat.HasProperty("_BaseColor"))
                        {
                            mat.SetColor("_BaseColor", color);
                        }
                        else if (mat.HasProperty("_Color"))
                        {
                            mat.SetColor("_Color", color);
                        }
                        if (Plugin.BikiniSolid.Value && mat.HasProperty("_BaseColorMap"))
                        {
                            mat.SetTexture("_BaseColorMap", null);
                        }
                    }
                }
                Plugin.ModLog.LogInfo($"Bikini piece '{piece.name}' tinted.");
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Bikini tint failed: " + ex.Message);
            }
        }
    }
}
