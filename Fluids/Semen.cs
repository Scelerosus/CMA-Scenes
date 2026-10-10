using System;
using HarmonyLib;
using UnityEngine;
using Assets.TValle.BeachGirl;
using Assets.TValle.BeachGirl.Runtime.Semens;
using GameSemen = Assets._ReusableScripts.CuchiCuchi.Skins.Semen.SemenParaPene;
using SemenSkin = Assets._ReusableScripts.CuchiCuchi.Dependentes.Skins.Semen.SemenSkinController;

namespace CMAScenes
{
    // What the plugin has to do with the hero's semen.
    //
    // The condom does not let it through: the game's own method that makes his penis come is told to let out next to
    // nothing while he wears one, and the teat of the condom fills instead with as much as the game was about to let out
    // (its own numbers for this ejaculation, read here before they are cut: Condom.cs). The same method is how the ride
    // learns that he came (the climax, Climax.cs).
    //
    // And what the semen in a full condom looks like is taken from the game's own semen: its colour and its map.
    [HarmonyPatch(typeof(GameSemen), nameof(GameSemen.TryEyacular))]
    internal static class CondomHoldsSemen
    {
        private static bool Prefix(GameSemen __instance, ref float cantidadMod, ref float velMod)
        {
            try
            {
                ShakeController me = ShakeController.Instance;
                var penis = __instance != null ? __instance.penis : null;
                if (me != null && penis != null)
                {
                    me.HeroCame(penis.Pointer);
                }
                if (me != null && penis != null && me.CondomCatches(penis.Pointer, __instance, cantidadMod))
                {
                    // He comes as the game has him come (stopped outright, his arousal stayed where it was and nothing
                    // of what follows his coming happened), only with no semen to speak of and no force behind it:
                    // what there is of it stays in the condom.
                    cantidadMod = 0.0001f;
                    velMod = 0.02f;
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Condom: the check on his coming failed: " + ex.Message);
            }
            return true;
        }

        internal static void Install()
        {
            try
            {
                var h = new Harmony(Plugin.PluginGuid + ".semen");
                h.PatchAll(typeof(CondomHoldsSemen));
                try
                {
                    h.PatchAll(typeof(CondomHoldsChain));
                    h.PatchAll(typeof(CondomHoldsOverflow));
                }
                catch (Exception ex2)
                {
                    Plugin.ModLog.LogWarning("Condom: the game's semen emitter could not be watched: " + ex2.Message);
                }
                Plugin.ModLog.LogInfo("Condom: the game's own ejaculation is watched (no semen gets out of a condom).");
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Condom: the game's own ejaculation could not be watched; semen will get out of a condom: " + ex.Message);
            }
        }
    }

    // The game also lets the semen of a condom out of him, in drops that fly from his tip into her (they only shrink with
    // the amount asked for; the count stays), and what is in her it lets out of her after. So for as long as the ejaculation
    // the condom caught goes on, the emitter makes nothing for his penis (the condom has the amount: Condom.cs).
    [HarmonyPatch(typeof(EmisorDeSemenChain), nameof(EmisorDeSemenChain.EmitirDesdePene))]
    internal static class CondomHoldsChain
    {
        private static bool Prefix(IPeneSimple Owner, ref SemenChain __result)
        {
            try
            {
                ShakeController me = ShakeController.Instance;
                if (me != null && me.CondomBlocks(Owner != null ? Owner.Pointer : IntPtr.Zero))
                {
                    __result = null;
                    return false;
                }
            }
            catch (Exception)
            {
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(EmisorDeSemenChain), nameof(EmisorDeSemenChain.EmitirDesdePeneConOverflow))]
    internal static class CondomHoldsOverflow
    {
        private static bool Prefix(IPeneSimple Owner)
        {
            try
            {
                ShakeController me = ShakeController.Instance;
                if (me != null && me.CondomBlocks(Owner != null ? Owner.Pointer : IntPtr.Zero))
                {
                    return false;
                }
            }
            catch (Exception)
            {
            }
            return true;
        }
    }

    public sealed partial class ShakeController
    {
        // The look of the game's own semen, found among its materials and read once: its colour map (as bytes, as it is
        // painted), its colour on average, and how smooth it is. What fills a condom (in its teat, and under the latex
        // round his tip) is drawn from these: the game's semen recoloured. Null / the defaults while none was found (the
        // game makes its semen only once it has been needed).
        private static Color32 s_semenColour = new Color32(238, 236, 226, 255);
        private static float s_semenSmooth = 0.92f;
        private static Color32[] s_semenCol;
        private static bool s_semenSearched;
        private static float s_semenNextAt;
        private static int s_semenLooks;
        private const int SemenTile = 2;

        private static readonly System.Text.RegularExpressions.Regex SemenMaterialName =
            new System.Text.RegularExpressions.Regex("semen|(?<![a-z])cum(?![a-z])|sperm|esperma|(?<![a-z])jizz|leche", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
        private static readonly System.Text.RegularExpressions.Regex ColourMapName =
            new System.Text.RegularExpressions.Regex("base|main|albedo|diffuse|color|colour", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
        private static readonly System.Text.RegularExpressions.Regex NotColourMap =
            new System.Text.RegularExpressions.Regex("normal|bump|height|noise|mask|detail|emiss|metal|occl|thick", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        // Looks for the game's semen among its materials: those of its semen skins first, then any material of the scene
        // named for it. Tried again every so often while none has a colour map (the game makes its own at its first use).
        private static void FindSemenLook()
        {
            if (s_semenSearched || Time.unscaledTime < s_semenNextAt)
            {
                return;
            }
            s_semenNextAt = Time.unscaledTime + 15f;
            try
            {
                var candidates = new System.Collections.Generic.List<Material>();
                var seen = new System.Collections.Generic.HashSet<int>();
                var skins = Resources.FindObjectsOfTypeAll<SemenSkin>();
                for (int i = 0; skins != null && i < skins.Length; i++)
                {
                    var list = skins[i] != null ? skins[i].m_SemenSkins : null;
                    for (int j = 0; list != null && j < list.Count; j++)
                    {
                        Material m = list[j] != null ? list[j].m_material : null;
                        if (m != null && seen.Add(m.GetInstanceID()))
                        {
                            candidates.Add(m);
                        }
                    }
                }
                var all = Resources.FindObjectsOfTypeAll<Material>();
                for (int i = 0; all != null && i < all.Length; i++)
                {
                    Material m = all[i];
                    if (m != null && m.name != null && !m.name.StartsWith("CMAScenes", StringComparison.Ordinal) && SemenMaterialName.IsMatch(m.name) && seen.Add(m.GetInstanceID()))
                    {
                        candidates.Add(m);
                    }
                }
                // (Listed in full the first two times only: it is looked for again at every ride while it is not had.)
                bool listing = s_semenLooks++ < 2;
                if (listing)
                {
                    Plugin.ModLog.LogInfo($"Wet: {candidates.Count} material(s) of the game's semen found" + (candidates.Count > 0 ? ":" : "; tried again in 15 s."));
                }
                Material pick = null;
                Texture pickMap = null;
                int shown = 0;
                foreach (Material m in candidates)
                {
                    var names = m.GetTexturePropertyNames();
                    var sb = new System.Text.StringBuilder();
                    Texture map = null;
                    for (int k = 0; names != null && k < names.Length; k++)
                    {
                        Texture t = m.GetTexture(names[k]);
                        sb.Append(names[k]).Append(t != null ? "=" + t.name + " " + t.width + "x" + t.height : "=none").Append(' ');
                        if (t != null && map == null && ColourMapName.IsMatch(names[k]) && !NotColourMap.IsMatch(names[k]))
                        {
                            map = t;
                        }
                    }
                    if (listing && shown++ < 10)
                    {
                        Plugin.ModLog.LogInfo($"Wet:   '{m.name}', shader '{(m.shader != null ? m.shader.name : "?")}': {sb}");
                    }
                    if (pick == null || (pickMap == null && map != null))
                    {
                        pick = m;
                        pickMap = map;
                    }
                }
                if (pick == null)
                {
                    return;
                }
                // Its colour on average: from its map, else from its colour property.
                Color32 colour = s_semenColour;
                foreach (string name in new[] { "_BaseColor", "_Color", "_SemenColor", "_MainColor", "_Tint" })
                {
                    if (pick.HasProperty(name))
                    {
                        Color c = pick.GetColor(name);
                        if (c.maxColorComponent > 0.5f)
                        {
                            colour = new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), 255);
                        }
                        break;
                    }
                }
                foreach (string name in new[] { "_Smoothness", "_Glossiness" })
                {
                    if (pick.HasProperty(name))
                    {
                        s_semenSmooth = Mathf.Clamp(pick.GetFloat(name), 0.7f, 1f);
                        break;
                    }
                }
                if (pickMap != null)
                {
                    Color32[] read = CopyMask(pickMap, true);
                    if (read != null)
                    {
                        long r = 0, g = 0, b = 0, n = 0;
                        float lo = 1f, hi = 0f, aLo = 1f, aHi = 0f;
                        for (int i = 0; i < read.Length; i++)
                        {
                            Color32 c = read[i];
                            float l = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                            aLo = Mathf.Min(aLo, c.a / 255f);
                            aHi = Mathf.Max(aHi, c.a / 255f);
                            if (c.a > 20)
                            {
                                r += c.r;
                                g += c.g;
                                b += c.b;
                                n++;
                                lo = Mathf.Min(lo, l);
                                hi = Mathf.Max(hi, l);
                            }
                        }
                        if (n > 100)
                        {
                            colour = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
                            s_semenCol = read;
                            Plugin.ModLog.LogInfo($"Wet: the semen map: alpha {aLo:F2} to {aHi:F2}, light {lo:F2} to {hi:F2}.");
                        }
                    }
                }
                s_semenColour = colour;
                s_semenSearched = s_semenCol != null;
                Plugin.ModLog.LogInfo($"Wet: what fills a condom is drawn from the game's own semen: '{pick.name}' (shader '{(pick.shader != null ? pick.shader.name : "?")}'), colour {colour.r},{colour.g},{colour.b}, smoothness {s_semenSmooth:F2}, " + (s_semenCol != null ? "its colour map copied." : "no colour map could be copied: its colour and gloss only."));
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogInfo("Wet: the game's own semen could not be looked at (" + ex.Message + "); what fills a condom is a plain glossy white.");
            }
        }

        // The semen recoloured: its own colour pulled toward a creamy white, by how much of that is asked.
        private static Color32 SemenRecoloured(Color32 texel, float amount)
        {
            // (Its map is pure white: recoloured to the warm white of the real thing, the more the higher amount is.)
            return new Color32(
                (byte)Mathf.Lerp(texel.r, 238f, amount),
                (byte)Mathf.Lerp(texel.g, 230f, amount),
                (byte)Mathf.Lerp(texel.b, 208f, amount), 255);
        }
    }
}
