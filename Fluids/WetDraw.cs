using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using HerShine = Assets._ReusableScripts.CuchiCuchi.AI.Controladores.ControlladorBrilloPorPlacer;

namespace CMAScenes
{
    public sealed partial class ShakeController
    {
        // Puts pixels up on a texture without making a new copy of them for the game each time: the game's own array for
        // the texture is made once and filled in place. (Handing it ours made a megabyte of garbage in the game's memory
        // for every map drawn, several maps several times a second while he is wet: its collector then stalled a frame
        // now and then.) Should that not work here, they are put up the plain way.
        private static bool s_putPlain;

        private static void PutPixels(Texture2D tex, Color32[] pixels, ref Il2CppStructArray<Color32> buf)
        {
            if (!s_putPlain)
            {
                try
                {
                    if (buf == null || buf.Length != pixels.Length)
                    {
                        buf = new Il2CppStructArray<Color32>(pixels.Length);
                    }
                    pixels.AsSpan().CopyTo(buf.AsSpan());
                    tex.SetPixels32(buf);
                    tex.Apply(true, false);
                    return;
                }
                catch (Exception ex)
                {
                    s_putPlain = true;
                    buf = null;
                    Plugin.ModLog.LogInfo("Ride: wet: the maps are put up the plain way (" + ex.Message + ").");
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
        }

        private float m_wetDrawAt;
        private float m_wetDrawCost;
        private int m_upNext;

        private static float Soft(float x)
        {
            x = x <= 0f ? 0f : x >= 1f ? 1f : x;
            return x * x * (3f - 2f * x);
        }

        // The maps are drawn again when how he looks has changed (how wet he is somewhere, how far the condom has
        // rolled, how full it is), a few times a second at most. At every texel of his skin: the latex, if the condom
        // covers that place (with what he came in it showing white through it round his tip); then the clear film by
        // how wet he is there (gloss, and in relief a smooth film that levels his skin).
        private static bool[] s_touch = new bool[8];

        private void DrawMasks()
        {
            FindSemenLook();
            float shine = Fast.Clamp01(Plugin.RideWetShine.Value);
            float edge = CondomEdge;
            // The colour of his skin is drawn over only when something with a colour of its own is on him (the condom):
            // clear fluid is gloss alone, and his skin keeps the game's own colours.
            bool needColour = edge < 1.5f;
            bool inRelief = Plugin.RideWetRelief.Value;
            // (How full the condom is, in steps: what he came in it shows round his tip, more of it the fuller.)
            int full = m_condomFull ? Mathf.Clamp(Mathf.RoundToInt(CondomFullK * 4f), 1, 4) : 0;
            string look = (Plugin.CondomColor.Value ?? "") + (needColour ? "|c" : "") + "|f" + full + (inRelief ? "|r" : "");
            float diff = Fast.Abs(shine - m_wetShineDrawn);
            for (int i = 0; i < WetBins; i++)
            {
                diff = Fast.Max(diff, Fast.Abs(m_wetAlong[i] - m_wetDrawn[i]));
                diff = Fast.Max(diff, Fast.Abs(m_spitAlong[i] - m_spitDrawn[i]));
            }
            bool rolling = Fast.Abs(edge - m_edgeDrawn) > 0.004f;
            if ((diff <= 0.02f && !rolling && look == m_lookDrawn) || Time.unscaledTime < m_wetDrawAt)
            {
                return;
            }
            long drawStart = System.Diagnostics.Stopwatch.GetTimestamp();
            m_wetDrawAt = Time.unscaledTime + (rolling ? 0.12f : 0.25f);
            // Which parts of his skin have something new on them: only those are worked out and put up again. (The look as
            // a whole changed: all of them. Else those his wet bins, or the edge of the condom, have moved on.)
            bool lookNew = look != m_lookDrawn;
            bool lookMoved = lookNew || Fast.Abs(shine - m_wetShineDrawn) > 0.001f;
            float edgeLo = Fast.Min(edge, m_edgeDrawn) - 0.02f, edgeHi = Fast.Max(edge, m_edgeDrawn) + 0.02f;
            var touch = s_touch.Length >= m_wetParts.Count ? s_touch : (s_touch = new bool[m_wetParts.Count + 4]);
            for (int pi = 0; pi < m_wetParts.Count; pi++)
            {
                WetPart tp = m_wetParts[pi];
                bool t = !tp.drawn || lookMoved || (rolling && edgeHi >= tp.aLo && edgeLo <= tp.aHi);
                if (!t)
                {
                    int lo = Fast.Max(0, Mathf.FloorToInt((tp.aLo - 0.12f) * WetBins));
                    int hi = Fast.Min(WetBins - 1, Mathf.CeilToInt((tp.aHi + 0.05f) * WetBins));
                    for (int bi = lo; bi <= hi && !t; bi++)
                    {
                        t = Fast.Abs(m_wetAlong[bi] - m_wetDrawn[bi]) > 0.002f || Fast.Abs(m_spitAlong[bi] - m_spitDrawn[bi]) > 0.002f;
                    }
                }
                touch[pi] = t;
            }
            m_wetShineDrawn = shine;
            m_edgeDrawn = edge;
            m_lookDrawn = look;
            Array.Copy(m_wetAlong, m_wetDrawn, WetBins);
            Array.Copy(m_spitAlong, m_spitDrawn, WetBins);
            int last = WetBins * 4;
            var wet = new float[last + 1];
            // For the relief: how thick the film of liquid on him is (metres) and how far it levels his skin (0..1).
            var film = new float[last + 1];
            var level = new float[last + 1];
            for (int i = 0; i <= last; i++)
            {
                float a = i / (float)last;
                // (Half wet already shines in full: the rest of the way only keeps it wet for longer.)
                float here = Fast.Clamp01(WetAt(a) * 2f);
                // (Her saliva shines as her other fluids do.)
                float wetS = Soft(Fast.Max(here, Fast.Clamp01(SpitAt(a) * 2f)));
                wet[i] = wetS * shine;
                // A thin clear film: thickest just after it was laid on (as the place comes out of her) and draining
                // thinner, as a viscous film on a slope does; and a little thicker where it ends, a soft swell along its
                // edge (the meniscus where wet meets dry), as wide as the edge is soft: a line of light, not a bump.
                float fresh = FreshAt(a);
                film[i] = wet[i] * (0.00005f + 0.0002f * fresh) + 0.00018f * shine * 4f * wetS * (1f - wetS);
                // Liquid fills the fine relief of the skin: under a fresh film it is as smooth as a mirror.
                level[i] = Fast.Clamp01(wetS * shine * (0.7f + 0.3f * fresh));
            }
            CondomTint(out Color32 tint, out float tintK);
            // (The edge of the film is not a ruled line round him: along the bands where it ran (WetPart.streak) it
            // reaches down as far as he is wet; between them it stands back toward his tip by up to some 6 mm. So it
            // never reaches further down him than he has been wet.)
            float runBack = 0.006f / Fast.Max(0.06f, m_penisLength) * last;
            for (int pi = 0; pi < m_wetParts.Count; pi++)
            {
                if (!touch[pi])
                {
                    continue;
                }
                WetPart p = m_wetParts[pi];
                p.drawn = true;
                bool paint = p.skin != null && needColour;
                bool heights = inRelief && p.reliefOk;
                // (Only the texels that show some of him are gone through: the rest of each map is as it was made, his
                // dry skin, and never changes. They are a sixth of it.)
                int[] live = p.live;
                for (int n = 0; n < live.Length; n++)
                {
                    int i = live[n];
                    Color32 c = p.dry[i];
                    float a = p.along[i];
                    int k = Fast.Clamp((int)(a * last + 0.5f), 0, last);
                    float st = p.streak != null ? p.streak[i] : 0.5f;
                    int kf = Fast.Clamp(k - (int)((1f - st) * runBack + 0.5f), 0, last);
                    float alpha = c.a;
                    float sr = 0f, sg = 0f, sb = 0f;
                    if (paint)
                    {
                        Color32 s = p.skin[i];
                        sr = s.r;
                        sg = s.g;
                        sb = s.b;
                    }
                    bool latex = a >= edge;
                    if (latex)
                    {
                        alpha = Fast.Max(alpha, 205f);
                        sr += (tint.r - sr) * tintK;
                        sg += (tint.g - sg) * tintK;
                        sb += (tint.b - sb) * tintK;
                        if (full > 0 && a > 0.84f)
                        {
                            // He has come in it: what does not fit in its teat lies under the latex round his tip, white
                            // through it (further down him and whiter the more of it there is).
                            float white = Soft((a - (0.92f - 0.02f * full)) / 0.1f) * (0.3f + 0.12f * full);
                            int cx = i % MaskSize, cy = i / MaskSize;
                            int ci = ((cy * SemenTile) % MaskSize) * MaskSize + (cx * SemenTile) % MaskSize;
                            Color32 under = s_semenCol != null ? SemenRecoloured(s_semenCol[ci], 0.65f) : s_semenColour;
                            sr += (under.r - sr) * white;
                            sg += (under.g - sg) * white;
                            sb += (under.b - sb) * white;
                        }
                    }
                    float g = wet[kf];
                    if (g > 0.003f)
                    {
                        // Clear fluid: gloss alone, even all over the wet part (on the latex a little less: it is wetted
                        // less). Its colour is his skin's (or the latex's) as it is.
                        alpha += (255f - alpha) * g * (latex ? 0.9f : 1f);
                    }
                    c.a = (byte)Fast.Clamp(alpha, 0f, 255f);
                    p.pixels[i] = c;
                    if (paint)
                    {
                        p.skinPixels[i] = new Color32((byte)Fast.Clamp(sr, 0f, 255f), (byte)Fast.Clamp(sg, 0f, 255f), (byte)Fast.Clamp(sb, 0f, 255f), p.skin[i].a);
                    }
                    if (heights)
                    {
                        // How high the film stands here (in texels; a little thicker along the bands where it ran) and
                        // how far it levels his skin; under the latex the skin is levelled by the latex itself.
                        p.hStatic[i] = film[kf] * (0.75f + 0.5f * st) * p.density;
                        p.lvStatic[i] = Fast.Max(latex ? 0.4f : 0f, level[kf]);
                    }
                }
                if (heights)
                {
                    // The mask goes up together with the normal map (FluidPass); only the very first is put up here, so
                    // that there is never a mask of nothing on him.
                    p.dirty = true;
                    if (!p.maskReady)
                    {
                        PutPixels(p.mask, p.pixels, ref p.maskBuf);
                        p.maskReady = true;
                    }
                }
                else if (!p.maskReady || lookNew)
                {
                    PutPixels(p.mask, p.pixels, ref p.maskBuf);
                    p.maskReady = true;
                    p.upMask = false;
                }
                else
                {
                    p.upMask = true;
                }
                if (paint)
                {
                    if (!p.colourReady || lookNew)
                    {
                        // (At once: what was there is another look, and is shown for as long as this takes otherwise.)
                        PutPixels(p.colour, p.skinPixels, ref p.colourBuf);
                        p.colourReady = true;
                        p.upColour = false;
                    }
                    else
                    {
                        p.upColour = true;
                    }
                }
            }
            // Paced by what it cost: no more than about an eighth of the time goes on this, however slow the machine.
            float costMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - drawStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            m_wetDrawCost = m_wetDrawCost <= 0f ? costMs : Fast.Lerp(m_wetDrawCost, costMs, 0.2f);
            m_wetDrawAt = Time.unscaledTime + Fast.Max(rolling ? 0.12f : 0.25f, m_wetDrawCost * 0.008f);
            if (needColour != m_colourNeeded)
            {
                m_colourNeeded = needColour;
                if (m_wetShown)
                {
                    WetShow(true);
                }
            }
        }

        // One map goes up to the graphics card in a frame, not all of them at once: each upload (with its mip maps) costs
        // a few milliseconds and the six to nine of a round, all in one frame, were the stalls of the wet look and of the
        // condom rolling on. Called every frame.
        private void UploadStep()
        {
            int n = m_wetParts.Count;
            if (n == 0)
            {
                return;
            }
            Prof.B("Upload");
            try
            {
                for (int k = 0; k < n; k++)
                {
                    int at = (m_upNext + k) % n;
                    WetPart p = m_wetParts[at];
                    if (p.upMask && p.mask != null)
                    {
                        p.upMask = false;
                        PutPixels(p.mask, p.pixels, ref p.maskBuf);
                        m_upNext = at;
                        return;
                    }
                    if (p.upNormal && p.normalTex != null)
                    {
                        p.upNormal = false;
                        PutPixels(p.normalTex, p.normalPixels, ref p.normalBuf);
                        m_upNext = at;
                        return;
                    }
                    if (p.upColour && p.colour != null)
                    {
                        p.upColour = false;
                        PutPixels(p.colour, p.skinPixels, ref p.colourBuf);
                        m_upNext = at;
                        return;
                    }
                }
            }
            finally
            {
                Prof.E("Upload");
            }
        }
    }
}
