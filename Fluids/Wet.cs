using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using HerShine = Assets._ReusableScripts.CuchiCuchi.AI.Controladores.ControlladorBrilloPorPlacer;

namespace CMAScenes
{
    // The hero's penis gets wet from her while she rides him.
    //
    // How wet: each place along him by itself. A place gets wetter only while it is in her, by the time it is there and
    // by how aroused she is (the game's own record of her vagina, which it raises with her pleasure); out of her it dries
    // slowly. And never further down him than the deepest she has taken him this ride (m_reachVag, with a soft edge of a
    // centimetre or so): he is wet from his tip down to there and dry below. The same for her mouth (m_reachMouth).
    //
    // What it looks like: one kind of fluid, a thin clear film. It is the gloss of his own skin: each material of his
    // penis has a mask map whose alpha is how smooth the skin is; while he is wet it is given a copy of that map in which
    // the wet places are as smooth as skin gets, and the dry ones are as they were. (It used to be a see-through film
    // laid over him: that came out as a black stain or as nothing at all, for a see-through surface shines by only as
    // much as it is seen.) For that, every texel of the map is told once where on the shaft it lies: the mesh is laid out
    // flat in its UVs. Clear fluid leaves the colour of his skin alone. (Beads, drops and creamy lumps were tried and
    // read as a rash on him: they are gone.)
    //
    // What has a colour of its own (the condom, Condom.cs) is drawn on a copy of the map of his skin's colour in the same
    // way: from the place the condom has rolled down to, up to his tip, his skin is tinted and smooth, and her fluids lie
    // on top of that. (His own semen on him after he came in her was drawn so once; the user asked for it to go: what
    // he came stays in her, as the game has it, and is seen there in the X-ray. He keeps only the wet look.)
    //
    // The wet look is always on (the switch for it is gone from the window, and the setting with it).
    //
    // A gloss alone reads as a shiny texture, not as something lying on him: the film is given a gentle shape as well,
    // by a normal map of its own (WetFlow.cs) that smooths his skin under it; and threads of it are drawn out between
    // her and his tip as she comes off him (WetStrings.cs).
    public sealed partial class ShakeController
    {
        private bool m_reliefLogged;
        private sealed class WetPart
        {
            public Renderer renderer;
            public int slot;
            public string name;
            public Material material;
            public bool hadBlock;
            public bool keywordAdded;
            // The range of smoothness the material had, and its mask map as it was (null when it had none).
            public float min0, max0;
            public Texture maskWas;
            // For each texel of the mask: where on the shaft it is (0 the base, 1 the tip, below 0 nothing of him there),
            // the mask as it is while dry, and the one that is drawn.
            public float[] along;
            public Color32[] dry;
            public Color32[] pixels;
            public Texture2D mask;
            // The colour of his skin: its map as it was, a copy of it (null when it could not be had: then only the
            // gloss is drawn), and the one that is drawn.
            public Texture baseWas;
            public Color32[] skin;
            public Color32[] skinPixels;
            public Texture2D colour;
            // For each texel, a smooth pattern that runs along the shaft (0..1: long soft bands, a few millimetres wide
            // round him and centimetres long): where the film runs down a little further and lies a little thicker, so
            // that its edge is not a ruled line and it reads as liquid that ran, not as paint.
            public float[] streak;
            // Texels of the map to a metre of his skin, and for each texel the place on his mesh it shows (metres, in
            // the mesh's own frame, three numbers to a texel).
            public float density;
            public float[] pos;
            // The texels that show some of him (where along is not below 0): the only ones the liquid is worked out on.
            public int[] live;
            // The film in relief (WetFlow.cs): the normal map of his skin as it was, a copy of it in the encoding that
            // is drawn, and the one drawn; how high the film stands at each texel (in texels) and how far it levels the
            // skin under it.
            public bool reliefOk;
            public Texture normalWas;
            public bool normalKeywordAdded;
            public float normalScale0 = 1f;
            public Color32[] normalDry;
            public Color32[] normalPixels;
            public Texture2D normalTex;
            public float[] hStatic, lvStatic;
            public bool maskReady, dirty;
            // The game's own arrays the maps are put up through, made once for each (PutPixels).
            public Il2CppStructArray<Color32> maskBuf, colourBuf, normalBuf;
            // Where along him this part's texels lie (lowest, highest), whether it was drawn at all yet, and which of its
            // maps are drawn and still to be put up (UploadStep: one a frame).
            public float aLo, aHi;
            public bool drawn, colourReady, upMask, upColour, upNormal;
        }

        private static readonly Regex WetMaterialName = new Regex("penis|(?<![a-z])pene|genit|(?<![a-z])dick|glans|(?<![a-z])shaft|scrot|testic", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex WetNotSkin = new Regex("x-?ray|internal", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly int MaskMapId = Shader.PropertyToID("_MaskMap");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseColorMap");
        private static readonly int SmoothMinId = Shader.PropertyToID("_SmoothnessRemapMin");
        private static readonly int SmoothMaxId = Shader.PropertyToID("_SmoothnessRemapMax");
        private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
        private static readonly int NormalScaleId = Shader.PropertyToID("_NormalScale");
        private const string MaskKeyword = "_MASKMAP";
        private const string NormalKeyword = "_NORMALMAP";
        private const int MaskSize = 512;
        private const int MaskShift = 9;
        private const float DefaultPenisLength = 0.16f;

        private readonly List<WetPart> m_wetParts = new List<WetPart>();
        // How wet he is at each place along the shaft, from the base (the first) to the tip (the last), what of it is
        // drawn, and the most of it anywhere.
        private const int WetBins = 48;
        private readonly float[] m_wetAlong = new float[WetBins];
        private readonly float[] m_wetDrawn = new float[WetBins];
        private float m_wet;
        // How far down from the tip he is wet (0..1 of his length), for the log.
        private float m_wetReach;
        // The deepest she has taken him this ride, in her vagina and in her mouth (0..1 of his length from the tip): no
        // place further down him than that gets wet. It follows how deep he is in her, but goes up only at a limited
        // pace (a few strokes to the bottom), so that a single frame in which the depth was read too deep (a jump as
        // she mounts) does not wet him to the root; it never goes down while she rides, and starts again once he is dry.
        private float m_reachVag, m_reachMouth;
        private float m_reachLogged;
        private float m_wetShineDrawn = -1f;
        // What of the look was drawn last: how far the condom had rolled, the settings.
        private float m_edgeDrawn = 2f;
        private string m_lookDrawn = "";
        private bool m_colourNeeded;
        // How thick he is along the shaft (metres from his middle line), from the base to the tip.
        private readonly float[] m_penisRadius = new float[RadiusBins];
        private const int RadiusBins = 16;
        private bool m_wetShown;
        private bool m_wetReported;
        // His length, from the mesh: used when his penis was not found by its bones.
        private float m_penisLength = DefaultPenisLength;
        private MaterialPropertyBlock m_wetBlock;

        // Her side: the game's own record of how wet she is (it raises it with her pleasure), read as how aroused she is.
        private HerShine m_herShine;
        private float m_herWet;
        private float m_herReadAt, m_herLogAt;

        // Finds the skin of his penis and lays out its masks, as he is taken up. Called once for each ride.
        private void WetCapture(Character hero)
        {
            WetRestore();
            try
            {
                var seen = new HashSet<int>();
                Component[] places = { hero, hero.bodyAnimator };
                foreach (Component place in places)
                {
                    if (place == null)
                    {
                        continue;
                    }
                    var all = place.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    for (int k = 0; all != null && k < all.Length; k++)
                    {
                        SkinnedMeshRenderer r = all[k];
                        // His penis, and not the see-through copies of it the game keeps for its x-ray view.
                        if (r == null || r.sharedMesh == null || !seen.Add(r.GetInstanceID()) || !WetMaterialName.IsMatch(r.name) || WetNotSkin.IsMatch(r.name))
                        {
                            continue;
                        }
                        AddWetParts(r);
                    }
                }
                if (m_wetParts.Count == 0)
                {
                    Plugin.ModLog.LogInfo("Ride: wet: no skin of his penis was found that can be made wet.");
                }
            }
            catch (Exception ex)
            {
                WetRestore();
                Plugin.ModLog.LogWarning("Wet look could not be prepared: " + ex.Message);
            }
            FindHerWetness();
            FindSemenLook();
        }

        // Her saliva on him (from her mouth, in or out of the ride): at each place along him, as her other fluids are
        // kept, and drawn the same way.
        private readonly float[] m_spitAlong = new float[WetBins];
        private readonly float[] m_spitDrawn = new float[WetBins];

        private float SpitAt(float a)
        {
            float x = Fast.Clamp(a * WetBins - 0.5f, 0f, WetBins - 1f);
            int i = Fast.Min((int)x, WetBins - 2);
            return Fast.Lerp(m_spitAlong[i], m_spitAlong[i + 1], x - i);
        }

        // How wet he is with her fluids at a place along the shaft (0 the base, 1 the tip).
        private float WetAt(float a)
        {
            float x = Fast.Clamp(a * WetBins - 0.5f, 0f, WetBins - 1f);
            int i = Fast.Min((int)x, WetBins - 2);
            return Fast.Lerp(m_wetAlong[i], m_wetAlong[i + 1], x - i);
        }

        // How thick he is at a place along the shaft (0 the base, 1 the tip), in metres from his middle line.
        private float PenisRadiusAt(float a)
        {
            return m_penisRadius[Fast.Clamp((int)(a * RadiusBins), 0, RadiusBins - 1)];
        }

        // Puts the drawn masks on his skin, or takes them off: as a per-renderer override, so the material itself is not
        // changed (but for the switch that makes it read a mask at all, where it had none).
        private void WetShow(bool show)
        {
            if (m_wetBlock == null)
            {
                m_wetBlock = new MaterialPropertyBlock();
            }
            bool relief = Plugin.RideWetRelief.Value, reliefOn = false;
            foreach (WetPart p in m_wetParts)
            {
                if (p.renderer == null || p.material == null)
                {
                    continue;
                }
                if (show)
                {
                    if (!p.material.IsKeywordEnabled(MaskKeyword))
                    {
                        p.material.EnableKeyword(MaskKeyword);
                        p.keywordAdded = true;
                    }
                    p.renderer.GetPropertyBlock(m_wetBlock, p.slot);
                    m_wetBlock.SetTexture(MaskMapId, p.mask);
                    if (p.colour != null && (m_colourNeeded || p.baseWas != null))
                    {
                        m_wetBlock.SetTexture(BaseMapId, m_colourNeeded ? p.colour : p.baseWas);
                    }
                    m_wetBlock.SetFloat(SmoothMinId, p.min0);
                    m_wetBlock.SetFloat(SmoothMaxId, 1f);
                    if (relief && p.reliefOk && p.normalTex != null)
                    {
                        // The liquid in relief: its normal map in place of his skin's (his skin's own relief is in it).
                        try
                        {
                            if (!p.material.IsKeywordEnabled(NormalKeyword))
                            {
                                p.material.EnableKeyword(NormalKeyword);
                                p.normalKeywordAdded = true;
                            }
                            m_wetBlock.SetTexture(NormalMapId, p.normalTex);
                            m_wetBlock.SetFloat(NormalScaleId, 1f);
                            reliefOn = true;
                        }
                        catch (Exception ex)
                        {
                            p.reliefOk = false;
                            Plugin.ModLog.LogInfo($"Ride: wet: relief: '{p.name}' could not be given its normal map ({ex.Message}); only gloss is drawn on it.");
                        }
                    }
                    p.renderer.SetPropertyBlock(m_wetBlock, p.slot);
                    continue;
                }
                if (p.keywordAdded)
                {
                    p.material.DisableKeyword(MaskKeyword);
                    p.keywordAdded = false;
                }
                if (p.normalKeywordAdded)
                {
                    p.material.DisableKeyword(NormalKeyword);
                    p.normalKeywordAdded = false;
                }
                if (!p.hadBlock)
                {
                    p.renderer.SetPropertyBlock(null, p.slot);
                    continue;
                }
                p.renderer.GetPropertyBlock(m_wetBlock, p.slot);
                if (p.maskWas != null)
                {
                    m_wetBlock.SetTexture(MaskMapId, p.maskWas);
                }
                if (p.baseWas != null)
                {
                    m_wetBlock.SetTexture(BaseMapId, p.baseWas);
                }
                if (p.normalWas != null)
                {
                    m_wetBlock.SetTexture(NormalMapId, p.normalWas);
                }
                if (p.material.HasProperty(NormalScaleId))
                {
                    m_wetBlock.SetFloat(NormalScaleId, p.material.GetFloat(NormalScaleId));
                }
                m_wetBlock.SetFloat(SmoothMinId, p.material.GetFloat(SmoothMinId));
                m_wetBlock.SetFloat(SmoothMaxId, p.material.GetFloat(SmoothMaxId));
                p.renderer.SetPropertyBlock(m_wetBlock, p.slot);
            }
            m_wetShown = show;
            m_reliefShown = show && reliefOn;
            if (m_reliefShown && !m_reliefLogged)
            {
                m_reliefLogged = true;
                int parts = 0;
                foreach (WetPart p in m_wetParts)
                {
                    parts += p.reliefOk ? 1 : 0;
                }
                Plugin.ModLog.LogInfo($"Ride: wet: relief: the film is shown in relief on {parts} of {m_wetParts.Count} part(s) of his skin (depth {Plugin.RideWetDepth.Value:F2}, threads {(Plugin.RideWetStrings.Value ? "on" : "off")}).");
            }
        }

        // Takes the wet look off for good: his skin is as the game has it, and the masks are thrown away.
        private void WetRestore()
        {
            try
            {
                if (m_wetShown)
                {
                    WetShow(false);
                }
                foreach (WetPart p in m_wetParts)
                {
                    p.upMask = p.upColour = p.upNormal = false;
                    if (p.mask != null)
                    {
                        UnityEngine.Object.Destroy(p.mask);
                    }
                    if (p.colour != null)
                    {
                        UnityEngine.Object.Destroy(p.colour);
                    }
                    if (p.normalTex != null)
                    {
                        UnityEngine.Object.Destroy(p.normalTex);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Wet look could not be taken off: " + ex.Message);
            }
            FlowForget();
            StrandsDrop();
            m_wetParts.Clear();
            m_wetShown = false;
            m_wetReported = false;
            m_reliefLogged = false;
            m_wetShineDrawn = -1f;
            m_reachVag = 0f;
            m_reachMouth = 0f;
            m_reachLogged = 0f;
            m_reachMouthLogged = 0f;
            m_reachLogs = 0;
            m_edgeDrawn = 2f;
            m_lookDrawn = "";
            m_colourNeeded = false;
            Array.Clear(m_wetDrawn, 0, WetBins);
            Array.Clear(m_spitDrawn, 0, WetBins);
            m_penisLength = DefaultPenisLength;
        }

        // ---- her side ----

        private void FindHerWetness()
        {
            m_herShine = null;
            m_herWet = 0f;
            m_herLogAt = 0f;
            try
            {
                if (m_rideWoman == null || m_rideWoman.character == null)
                {
                    return;
                }
                m_herShine = m_rideWoman.character.GetComponentInChildren<HerShine>(true);
                if (m_herShine == null)
                {
                    Plugin.ModLog.LogInfo("Ride: wet: the game's record of her wetness was not found on her; he gets wet at a steady pace instead.");
                    return;
                }
                var now = m_herShine.m_currentBrillos;
                Plugin.ModLog.LogInfo($"Ride: wet: how aroused she is is read from the game (now: vagina {now.vag:F2}, skin {now.main:F2}).");
            }
            catch (Exception ex)
            {
                m_herShine = null;
                Plugin.ModLog.LogWarning("Her wetness could not be found: " + ex.Message);
            }
        }

        // How aroused she is, by the game, read a few times a second.
        private void HerWetStep(float dt, bool contact)
        {
            if (m_herShine == null)
            {
                // Nothing to read: taken as aroused while she rides.
                m_herWet = contact ? 1f : 0f;
                return;
            }
            try
            {
                if (Time.unscaledTime < m_herReadAt)
                {
                    return;
                }
                m_herReadAt = Time.unscaledTime + 0.25f;
                var now = m_herShine.m_currentBrillos;
                var config = m_herShine.config;
                float maxVag = config != null && config.maxBrilloVag > 0.01f ? config.maxBrilloVag : 1f;
                // Only her vagina: that is what he is in. (The game keeps it at 1 when she is not aroused at all, and
                // raises it with her pleasure.)
                m_herWet = Fast.Clamp01((now.vag - 1f) / Fast.Max(0.1f, maxVag - 1f));
                if (contact && m_herLogAt == 0f)
                {
                    m_herLogAt = Time.unscaledTime + 15f;
                }
                if (m_herLogAt > 0f && Time.unscaledTime >= m_herLogAt)
                {
                    m_herLogAt = -1f;
                    Plugin.ModLog.LogInfo($"Ride: wet: after 15 s on him: her vagina {now.vag:F2} of {maxVag:F2} at most, taken as {m_herWet:F2} aroused; he is {m_wet:F2} wet (down to {m_wetReach:F2} of him, taken {m_reachVag:F2} of him deep at most, {m_rideIn:F2} in her now).");
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Her wetness could not be read: " + ex.Message);
                m_herShine = null;
            }
        }

        // How far down him she is this frame.
        private float m_wetEdge = 1f;

        // Runs every frame: while he is in her he gets wet, as far down as he is in her and never further down than the
        // deepest she has taken him; out of her he dries.
        private int m_wetFrame = -1;
        private float m_reachMouthLogged;
        private int m_reachLogs;

        // How wet a place along him may get (0..1), by how far down from his tip it is against how deep he has been
        // taken (0..1 of his length): 1 well above that depth, 0 at it and below, and a soft edge of some 1.5 cm between.
        // (The edge lies inside the part that was in her, so the wet never reaches past the deepest point.)
        private float ReachCap(float at, float reach)
        {
            if (reach <= 0.001f)
            {
                return 0f;
            }
            float soft = Fast.Clamp(0.015f / Fast.Max(0.06f, m_penisLength), 0.03f, 0.2f);
            return Soft((reach - (1f - at)) / soft);
        }

        private void UpdateWet(float dt)
        {
            m_wetFrame = Time.frameCount;
            try
            {
                // How deep he is in her this frame, as the ride worked it out (nothing if it did not run just now).
                float inHer = m_heroReady && m_ride && m_toggled && !m_switching && m_heroW > 0.95f
                    && Time.frameCount - m_rideInFrame <= 1 ? m_rideIn : 0f;
                bool contact = inHer > 0.01f;
                HerWetStep(dt, contact);
                // Each place along him by itself: what is in her now (from the tip down to her entrance) gets wetter, up
                // to how far the deepest she has taken him allows; what is out of her dries slowly. The tip, in her all
                // the time, is wet first; further down he is in her only at the bottom of each stroke and gets wet more
                // slowly; below the deepest she has gone he stays dry.
                float edge = 1f - Fast.Clamp01(inHer);
                m_wetEdge = edge;
                // The deepest she has taken him: it follows the depth up, but at no more than 0.4 of his length a second,
                // so that it takes the bottom of a few strokes to get there and a depth read wrong for a frame or two
                // counts for next to nothing.
                if (contact && !m_holeAnal && inHer > m_reachVag)
                {
                    m_reachVag = Fast.Min(inHer, m_reachVag + dt * 0.4f);
                }
                HerClimaxStep();
                PoseFlowStep();
                PlaceTourStep();
                m_peakWet = Fast.Max(0f, m_peakWet - dt / 15f);
                // By the time he is in her and by how aroused she is: in some twenty seconds in her while she is not
                // aroused, in some six when she is as aroused as she gets. What is out of her dries far more slowly than
                // that, and more slowly still while she rides (it is back in her at the bottom of the next stroke).
                // (Her own climax floods her for a while: wetter.)
                float wetter = dt * Fast.Lerp(0.3f, 1f, m_herWet) * (1f + 2f * m_peakWet) / 6f;
                float drier = dt / (contact ? 300f : 90f);
                // (While she rides him her mouth is not on him: it is not looked for then.)
                float inMouth = !(m_ride && m_toggled && m_heroReady) ? MouthDepth() : 0f;
                bool mouthOn = inMouth > 0.02f;
                float mouthEdge = 1f - Fast.Clamp01(inMouth);
                if (mouthOn && inMouth > m_reachMouth)
                {
                    m_reachMouth = Fast.Min(inMouth, m_reachMouth + dt * 0.4f);
                }
                if (mouthOn && !m_spitReported)
                {
                    m_spitReported = true;
                    Plugin.ModLog.LogInfo($"Wet: he is in her mouth ({inMouth:F2} of him): he gets wet with her saliva as far as he is in it.");
                }
                m_wet = 0f;
                m_wetReach = 0f;
                float most = 0f, mostSpit = 0f;
                for (int i = 0; i < WetBins; i++)
                {
                    float at = (i + 0.5f) / WetBins;
                    float w = m_wetAlong[i];
                    float capV = ReachCap(at, m_reachVag);
                    if (contact && at >= edge)
                    {
                        // (Her fluids are her vagina's: in her anus he gets no wetter, and what is on him stays.)
                        if (!m_holeAnal && w < capV)
                        {
                            w = Fast.Min(capV, w + wetter);
                        }
                    }
                    else
                    {
                        w = Fast.Max(0f, w - drier);
                    }
                    m_wetAlong[i] = w;
                    // Her saliva: what of him is in her mouth gets wet in a few seconds (no further down than the deepest
                    // she has had him in it), and dries in a minute.
                    float spit = m_spitAlong[i];
                    float capM = ReachCap(at, m_reachMouth);
                    if (mouthOn && at >= mouthEdge)
                    {
                        if (spit < capM)
                        {
                            spit = Fast.Min(capM, spit + dt / 3f);
                        }
                    }
                    else
                    {
                        spit = Fast.Max(0f, spit - dt / 60f);
                    }
                    m_spitAlong[i] = spit;
                    // How fresh the film is here: laid on anew while in her (or her mouth), draining thinner out of it.
                    // (A film draining down a slope thins as one over the root of the time: df/dt = -f^3 / 2T, T = 4 s.)
                    float fr = m_freshAlong[i];
                    bool coated = (contact && at >= edge && !m_holeAnal && capV > 0.05f) || (mouthOn && at >= mouthEdge && capM > 0.05f);
                    m_freshAlong[i] = coated ? 1f : Fast.Max(0f, fr - dt * fr * fr * fr / 8f);
                    most = Fast.Max(most, w);
                    mostSpit = Fast.Max(mostSpit, spit);
                    m_wet = Fast.Max(m_wet, Fast.Max(w, spit));
                    if (w > 0.1f)
                    {
                        m_wetReach = Fast.Max(m_wetReach, 1f - i / (float)WetBins);
                    }
                }
                // Dry again: the deepest she has taken him starts again from nothing.
                if (most <= 0.001f && !contact)
                {
                    m_reachVag = 0f;
                    m_reachLogged = 0f;
                }
                if (mostSpit <= 0.001f && !mouthOn)
                {
                    m_reachMouth = 0f;
                    m_reachMouthLogged = 0f;
                }
                if (m_reachLogs < 24 && (m_reachVag >= m_reachLogged + 0.1f || m_reachMouth >= m_reachMouthLogged + 0.1f))
                {
                    m_reachLogs++;
                    m_reachLogged = Fast.Max(m_reachLogged, Mathf.Floor(m_reachVag * 10f) / 10f);
                    m_reachMouthLogged = Fast.Max(m_reachMouthLogged, Mathf.Floor(m_reachMouth * 10f) / 10f);
                    Plugin.ModLog.LogInfo($"Ride: wet: reach: the deepest he has been taken: vagina {m_reachVag:F2} of him ({m_reachVag * m_penisLength * 100f:F1} cm), mouth {m_reachMouth:F2} ({m_reachMouth * m_penisLength * 100f:F1} cm); in her now {inHer:F2}, in her mouth {inMouth:F2}; wet from his tip down to {m_wetReach:F2} of him.");
                }
                if (m_wetParts.Count == 0)
                {
                    return;
                }
                // Dry, the ride over and no hero about: everything is taken off and thrown away.
                if (m_wet <= 0.001f && !m_heroReady && m_watchHero == null)
                {
                    WetRestore();
                    return;
                }
                bool show = m_wet > 0.003f || CondomEdge < 1.5f;
                if (show)
                {
                    Prof.B("DrawMasks");
                    DrawMasks();
                    Prof.E("DrawMasks");
                }
                if (show != m_wetShown)
                {
                    WetShow(show);
                }
                // The film in relief (WetFlow.cs).
                bool relief = show && Plugin.RideWetRelief.Value;
                if (show && m_wetShown && (relief && ReliefAny()) != m_reliefShown && Time.unscaledTime >= m_reliefSwitchAt)
                {
                    // Switched on or off in the settings: his skin is given back and the look put on again as it is now.
                    m_reliefSwitchAt = Time.unscaledTime + 1f;
                    WetShow(false);
                    WetShow(true);
                }
                if (relief)
                {
                    FlowStep();
                }
                UploadStep();
                if (show && !m_wetReported && m_wet > 0.4f)
                {
                    m_wetReported = true;
                    Plugin.ModLog.LogInfo($"Ride: wet now along him: tip {WetAt(0.95f):F2}, three quarters {WetAt(0.75f):F2}, middle {WetAt(0.5f):F2}, a quarter {WetAt(0.25f):F2}, root {WetAt(0.05f):F2} (taken {m_reachVag:F2} of him deep at most); drawn on {m_wetParts.Count} part(s) of his skin, shine {Plugin.RideWetShine.Value:F2}.");
                }
            }
            catch (Exception ex)
            {
                if (WarnOnce(ref m_wetWarns, ref m_wetQuiet))
                {
                    Plugin.ModLog.LogWarning("Wet look failed: " + ex.Message);
                }
                m_wet = 0f;
                Array.Clear(m_wetAlong, 0, WetBins);
                Array.Clear(m_spitAlong, 0, WetBins);
                WetRestore();
            }
        }

        private bool m_spitReported;
        private int m_wetWarns;
        private float m_wetQuiet;
    }
}
