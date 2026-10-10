using System;
using System.Collections.Generic;
using UnityEngine;

namespace CMAScenes
{
    // Her fluids on him as a liquid film, not as a gloss alone (the setting "WetRelief").
    //
    // What makes a thin clear film read as liquid is not bumps on it but what it does to the skin under it: it fills in
    // the fine relief of the skin (wet skin is smooth as a mirror where dry skin is matt and textured), so the highlights
    // on him become long, clean and unbroken. On top of that the film itself has a gentle shape: a little thicker where it
    // was laid on last and where it runs down in long soft rivulets along the shaft, and a soft swell at its edge where
    // wet meets dry (the meniscus). Nothing on it is round or discrete: drops and beads drawn as caps on the skin read as
    // a rash, not as liquid, and were taken out.
    //
    // So the wet look (Wet.cs) is given a normal map of its own over his skin's: his skin's own normal map, levelled where
    // the film lies on it, with the slopes of the film's own height added. His skin's relief is copied in, so nothing of
    // it is lost where there is no liquid.
    //
    // It costs: the maps are drawn again one material at a time, no more often than the time they took allows.
    public sealed partial class ShakeController
    {
        // How fresh the film is along him (1 just laid on, falling as it drains), like m_wetAlong.
        private readonly float[] m_freshAlong = new float[WetBins];
        // From his base to his tip in the mesh's own frame (for the rivulets, which run along him).
        private bool m_reliefShown;
        private float m_reliefSwitchAt;
        // When the next map may be drawn, which material is next, and how long one took (ms, smoothed).
        private float m_liquidAt;
        private int m_flowNext;
        private float m_flowCost = 4f;
        private int m_flowPasses;
        private static readonly HashSet<string> s_shadersLogged = new HashSet<string>();
        private static readonly System.Text.RegularExpressions.Regex ShaderPropWorth =
            new System.Text.RegularExpressions.Regex("normal|coat|detail|bent|height|smooth|mask|material|surface|uv|spec|iridesc|aniso", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        private float FreshAt(float a)
        {
            float x = Fast.Clamp(a * WetBins - 0.5f, 0f, WetBins - 1f);
            int i = Fast.Min((int)x, WetBins - 2);
            return Fast.Lerp(m_freshAlong[i], m_freshAlong[i + 1], x - i);
        }

        private bool ReliefAny()
        {
            foreach (WetPart p in m_wetParts)
            {
                if (p.reliefOk)
                {
                    return true;
                }
            }
            return false;
        }

        // Once for each shader: what it has that the liquid could use, and the switches set on this material, for the log.
        private static void LogShaderOnce(Material m)
        {
            try
            {
                Shader sh = m.shader;
                string key = (sh != null ? sh.name : "?") + "|" + m.name;
                if (sh == null || !s_shadersLogged.Add(key))
                {
                    return;
                }
                var sb = new System.Text.StringBuilder();
                int count = sh.GetPropertyCount();
                for (int i = 0; i < count; i++)
                {
                    string name = sh.GetPropertyName(i);
                    if (name != null && ShaderPropWorth.IsMatch(name))
                    {
                        sb.Append(name).Append(' ');
                    }
                }
                var kws = m.shaderKeywords;
                var kb = new System.Text.StringBuilder();
                for (int i = 0; kws != null && i < kws.Length; i++)
                {
                    kb.Append(kws[i]).Append(' ');
                }
                Plugin.ModLog.LogInfo($"Ride: wet: relief: '{m.name}' is drawn with '{sh.name}' ({count} properties); those of note: {sb}");
                Plugin.ModLog.LogInfo($"Ride: wet: relief: '{m.name}' has the switches: {kb}");
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogInfo("Ride: wet: relief: the shader could not be looked at (" + ex.Message + ").");
            }
        }

        // Gets a part ready for the liquid in relief: its skin's normal map is read (in whatever way the game packed it)
        // and kept in the plain encoding that is drawn, already at the strength the material gives it.
        private void CaptureRelief(WetPart p, Material m)
        {
            LogShaderOnce(m);
            p.reliefOk = false;
            if (!m.HasProperty(NormalMapId))
            {
                Plugin.ModLog.LogInfo($"Ride: wet: relief: '{m.name}' has no normal map to draw on; only gloss is drawn on it.");
                return;
            }
            if (m.HasProperty("_NormalMapSpace") && m.GetFloat("_NormalMapSpace") > 0.5f)
            {
                Plugin.ModLog.LogInfo($"Ride: wet: relief: '{m.name}' has its normal map in object space; only gloss is drawn on it.");
                return;
            }
            p.normalWas = m.GetTexture(NormalMapId);
            bool used = m.IsKeywordEnabled(NormalKeyword);
            p.normalScale0 = m.HasProperty(NormalScaleId) ? m.GetFloat(NormalScaleId) : 1f;
            Color32[] src = null;
            if (p.normalWas != null && used)
            {
                src = CopyMask(p.normalWas, false);
                if (src == null)
                {
                    Plugin.ModLog.LogInfo($"Ride: wet: relief: the normal map of '{m.name}' could not be read; only gloss is drawn on it.");
                    return;
                }
            }
            const int n = MaskSize * MaskSize;
            p.normalDry = new Color32[n];
            float sumZ = 0f;
            for (int i = 0; i < n; i++)
            {
                if (src == null)
                {
                    p.normalDry[i] = new Color32(128, 128, 255, 255);
                    sumZ += 1f;
                    continue;
                }
                Color32 c = src[i];
                // (Packed as the game's shader unpacks it: x in red times alpha, which is right both for maps that keep
                // x in alpha with red at 1 and for those that keep it in red with alpha at 1; y in green.)
                float x = (c.r / 255f) * (c.a / 255f) * 2f - 1f;
                float y = c.g / 255f * 2f - 1f;
                x *= p.normalScale0;
                y *= p.normalScale0;
                float z = Fast.Sqrt(Fast.Max(0.0025f, 1f - x * x - y * y));
                float inv = 1f / Fast.Sqrt(x * x + y * y + z * z);
                x *= inv;
                y *= inv;
                z *= inv;
                sumZ += z;
                p.normalDry[i] = new Color32((byte)((x * 0.5f + 0.5f) * 255f + 0.5f), (byte)((y * 0.5f + 0.5f) * 255f + 0.5f), (byte)((z * 0.5f + 0.5f) * 255f + 0.5f), 255);
            }
            p.normalPixels = new Color32[n];
            p.hStatic = new float[n];
            p.lvStatic = new float[n];
            p.normalTex = new Texture2D(MaskSize, MaskSize, TextureFormat.RGBA32, true, true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 4,
                hideFlags = HideFlags.HideAndDontSave
            };
            // (Put up at once as his skin is, so that it is never shown empty.)
            PutPixels(p.normalTex, p.normalDry, ref p.normalBuf);
            p.reliefOk = true;
            string was = p.normalWas != null ? $"'{p.normalWas.name}' {p.normalWas.width}x{p.normalWas.height}" : "none";
            Plugin.ModLog.LogInfo($"Ride: wet: relief: '{m.name}': its normal map {was} ({(used ? "used, switch " + NormalKeyword + " on" : "not used: switch " + NormalKeyword + " off, it will be switched on while he is wet")}), strength {p.normalScale0:F2}, {(src != null ? "copied" : "taken as flat")} (mean tilt {Mathf.Acos(Fast.Clamp01(sumZ / n)) * Mathf.Rad2Deg:F1} deg); {p.live.Length} texels of him.");
        }

        // Forgets the state of the film (a new hero, the look taken off).
        private void FlowForget()
        {
            m_reliefShown = false;
            m_flowPasses = 0;
            Array.Clear(m_freshAlong, 0, WetBins);
        }

        // Every frame while the liquid is shown in relief: one map is drawn again when it is due.
        private void FlowStep()
        {
            try
            {
                if (Time.unscaledTime < m_liquidAt || m_wetParts.Count == 0)
                {
                    return;
                }
                // The next material that has something new on it, in turn.
                for (int k = 0; k < m_wetParts.Count; k++)
                {
                    WetPart p = m_wetParts[(m_flowNext + k) % m_wetParts.Count];
                    if (!p.reliefOk || p.normalTex == null || !p.dirty)
                    {
                        continue;
                    }
                    m_flowNext = (m_flowNext + k + 1) % m_wetParts.Count;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    Prof.B("FluidPass");
                    FluidPass(p);
                    Prof.E("FluidPass");
                    sw.Stop();
                    float ms = (float)sw.Elapsed.TotalMilliseconds;
                    m_flowCost = m_flowPasses == 0 ? ms : Fast.Lerp(m_flowCost, ms, 0.1f);
                    m_flowPasses++;
                    // (No more than about a sixth of the time goes on it, however slow the machine.)
                    m_liquidAt = Time.unscaledTime + Fast.Clamp(m_flowCost * 0.006f, 0.035f, 0.3f);
                    if (m_flowPasses == 1)
                    {
                        Plugin.ModLog.LogInfo($"Ride: wet: relief: the first map of the film was drawn on '{p.name}' in {ms:F1} ms.");
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Ride: wet: the film in relief failed and is stopped: " + ex.Message);
                foreach (WetPart p in m_wetParts)
                {
                    p.reliefOk = false;
                }
                if (m_wetShown)
                {
                    WetShow(false);
                    WetShow(true);
                }
            }
        }

        // Draws one material's maps again: from the height of the film on him, the normal map (his skin's own relief
        // levelled under the film, the film's own gentle slopes on top), and the mask as it was drawn.
        private void FluidPass(WetPart p)
        {
            p.dirty = false;
            Array.Copy(p.normalDry, p.normalPixels, p.normalDry.Length);
            float relief = Fast.Clamp(Plugin.RideWetDepth.Value, 0f, 3f);
            const int wrap = MaskSize - 1;
            int[] live = p.live;
            float[] hs = p.hStatic;
            for (int n = 0; n < live.Length; n++)
            {
                int i = live[n];
                int x = i & wrap, y = i >> MaskShift;
                int iL = (y << MaskShift) | ((x - 1) & wrap), iR = (y << MaskShift) | ((x + 1) & wrap);
                int iD = (((y - 1) & wrap) << MaskShift) | x, iU = (((y + 1) & wrap) << MaskShift) | x;
                float hL = hs[iL], hR = hs[iR], hDn = hs[iD], hUp = hs[iU];
                float lv = p.lvStatic[i];
                if (lv < 0.003f && hL + hR + hDn + hUp < 1e-4f)
                {
                    continue;
                }
                Color32 bn = p.normalDry[i];
                float bx = bn.r / 127.5f - 1f, by = bn.g / 127.5f - 1f;
                float bz = Fast.Sqrt(Fast.Max(0.05f, 1f - bx * bx - by * by));
                // His skin's own bumps, filled in by the liquid (almost all of them under a fresh film); on top of them
                // the slopes of the film itself, which are gentle and long.
                float flat = 1f - 0.9f * lv;
                float nx = bx / bz * flat - (hR - hL) * 0.5f * relief;
                float ny = by / bz * flat - (hUp - hDn) * 0.5f * relief;
                float inv = 1f / Fast.Sqrt(nx * nx + ny * ny + 1f);
                p.normalPixels[i] = new Color32((byte)((nx * inv * 0.5f + 0.5f) * 255f + 0.5f), (byte)((ny * inv * 0.5f + 0.5f) * 255f + 0.5f), (byte)((inv * 0.5f + 0.5f) * 255f + 0.5f), 255);
            }
            // (Worked out here, put up one a frame by UploadStep.)
            p.maskReady = true;
            p.upMask = true;
            p.upNormal = true;
        }
    }
}
