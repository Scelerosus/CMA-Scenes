using System;
using System.Collections.Generic;
using Assets.TValle.BeachGirl;
using Assets.TValle.BeachGirl.Runtime.Semens;
using UnityEngine;
using GameSemen = Assets._ReusableScripts.CuchiCuchi.Skins.Semen.SemenParaPene;

namespace CMAScenes
{
    // The condom: a thing of the plugin's own that the hero puts on himself during the ride.
    //
    // Asked for (the key, or the button in the window), she comes up off him and a rolled condom lies on his belly. He
    // takes it with his hand (the game's own control of his hands) and brings it to his tip; there it rolls down him by
    // itself, and she takes him in again. Asked for again, she comes up, it rolls back off and is gone.
    //
    // What it is made of: while it lies or is carried, three small shapes (the rolled ring, the skin across it and the
    // teat). On him, the ring (at the place it has rolled down to) and the teat (on his tip) stay as shapes, and the
    // latex itself is drawn on the skin of his penis, from his tip down to the ring, with the maps the wet look draws
    // (Wet.cs): tinted and smooth. So it bends with him and her fluids show on it as they would on his skin.
    //
    // When he comes in it, the teat fills with what the game was about to let out of him: its own numbers for this
    // ejaculation (how many spurts, how many drops of semen in each, how much a drop is, how far apart the spurts are),
    // read in the patch on its method before the amount is cut (Semen.cs). Spurt by spurt, at the game's own pace, the
    // teat swells into a bulb that holds that much (a millilitre is a cubic centimetre), white with the game's semen
    // through the latex, sagging the way down is and quivering as each spurt reaches it. It stays full for as long as he
    // wears it; coming again adds to it.
    public sealed partial class ShakeController
    {
        // 0 none. 1 lying on his belly. 2 in his hand. 3 rolling on. 4 on him. 5 coming off.
        private int m_condom;
        private float m_condomAt;
        private float m_condomRoll;
        private int m_condomHand = -1;
        private GameObject m_condomRing, m_condomSkin, m_condomTeat;
        private Material m_condomMat;
        private string m_condomTint = "";
        // The teat: its size empty, what draws it (its shape is made anew as it fills, TeatShape), and his semen in it
        // (the condom does not let it out).
        private const float TeatSize = 0.0055f;
        private MeshRenderer m_condomTeatDraws;
        private Mesh m_condomTeatMesh;
        private MaterialPropertyBlock m_condomTeatBlock;
        private bool m_condomFull;
        // How much of his semen is in it (ml), what is still to come of the ejaculation under way (x: when it reaches the
        // teat, unscaled seconds; y: ml), and the quiver of the teat each spurt sets off.
        private float m_condomMl;
        // Until when (unscaled seconds) the game's emitter is to make nothing for his penis: the ejaculation the condom caught.
        private float m_condomShotUntil;

        internal bool CondomBlocks(IntPtr owner)
        {
            return m_condom == 4 && Time.unscaledTime < m_condomShotUntil && m_heroPen != null && (owner == IntPtr.Zero || owner == m_heroPen.Pointer);
        }
        private readonly List<Vector2> m_condomSpurts = new List<Vector2>();
        private float m_condomWob, m_condomWobV;
        // What the teat's shape was made for last (ml, the way down in the teat's frame, the quiver), and its colour.
        private float m_teatMl = -1f, m_teatWob;
        private Vector3 m_teatDown;
        private float m_teatTintMl = -1f;

        // How full the condom looks, 0..1 (four millilitres and more: full), for the latex round his tip and the X-ray.
        private float CondomFullK => m_condomFull ? Mathf.Clamp01(m_condomMl / 4f) : 0f;

        // How much of his semen is in it (ml), for the window.
        internal float CondomMl => m_condomMl;

        // He is coming (the game asks his penis to): with the condom on him nothing comes out of it, it fills. Called
        // from the patch on the game's own method (Semen.cs) with the game's own ejaculation and the amount it was asked
        // for. True when the game is not to let any semen out.
        internal bool CondomCatches(IntPtr penis, GameSemen shot, float amount)
        {
            if (m_condom != 4 || m_heroPen == null || penis == IntPtr.Zero || m_heroPen.Pointer != penis)
            {
                return false;
            }
            float ml = CondomShot(shot, amount, out string how);
            m_condomFull = true;
            Notice(Loc.T($"He came in the condom: {ml:0.0} ml.", $"Он кончил в презерватив: {ml:0.0} мл."));
            Plugin.ModLog.LogInfo($"Ride: condom: he came; {how}; {ml:F1} ml go into its teat spurt by spurt (the game is kept from letting any out); {m_condomMl + ml:F1} ml in it in all.");
            return true;
        }

        // What the game was about to let out of him this time, put in the teat at the game's own pace. Gives back how
        // many millilitres, and what it was worked out from (for the log).
        private float CondomShot(GameSemen shot, float amount, out string how)
        {
            how = "the game's numbers could not be read";
            int spurts = 6;
            float gap = 0.6f, gapMore = 0f, total = 0f;
            var each = new List<float>();
            try
            {
                if (shot != null)
                {
                    // How many spurts and how much in each: the game's settings for him, through its own modifiers.
                    float nMod = 1f, qMod = 1f;
                    try
                    {
                        if (shot.m_contraccionesModificable != null) nMod = shot.m_contraccionesModificable.ModificarValor(1f);
                        if (shot.m_cantidadModificable != null) qMod = shot.m_cantidadModificable.ModificarValor(1f);
                    }
                    catch (Exception)
                    {
                    }
                    int n = shot.cantidadDeContracciones;
                    spurts = Mathf.Clamp(Mathf.RoundToInt((n > 0 ? n : 6) * (nMod > 0.05f ? nMod : 1f)), 1, 16);
                    EmisorDeSemenChain.NextEmisionConfig cfg = shot.m_defaultEmisionConfig;
                    EmisorDeSemenChain emitter = shot.m_emisor;
                    if (cfg.count <= 0 && emitter != null)
                    {
                        cfg = emitter.next;
                    }
                    int drops = cfg.count;
                    // How much one drop is, as the game reckons it for his penis (else its default).
                    float per = 0f;
                    string perFrom = "his penis";
                    try
                    {
                        IPeneSimple pene = shot.m_pene != null ? shot.m_pene.TryCast<IPeneSimple>() : null;
                        if (pene != null && emitter != null)
                        {
                            per = EmisorDeSemenChain.GetParticleVolumenAproxV2(pene, ref cfg, emitter);
                        }
                    }
                    catch (Exception)
                    {
                    }
                    if (!(per > 0f))
                    {
                        perFrom = "the game's default";
                        try
                        {
                            per = EmisorDeSemenChain.GetParticleDefaultVolumenAproxV2();
                        }
                        catch (Exception)
                        {
                        }
                    }
                    // (Its unit is not known: taken as millilitres, unless it is so small it can only be cubic metres,
                    // or so large it can only be cubic millimetres.)
                    float perMl = per > 0f && per < 1e-4f ? per * 1e6f : per > 20f ? per * 0.001f : per;
                    float less = shot.eyaculacionDisminicionDeParticulasPorCadaContraccion;
                    float q = Mathf.Max(0.05f, amount) * (qMod > 0.01f ? qMod : 1f);
                    for (int k = 0; k < spurts; k++)
                    {
                        // (Each spurt less than the one before: by that part of it, or by that many drops.)
                        float c = less < 1f ? drops * Mathf.Max(0.15f, 1f - Mathf.Max(0f, less) * k) : Mathf.Max(1f, drops - less * k);
                        each.Add(c * q * perMl);
                        total += c * q * perMl;
                    }
                    if (shot.eyaculacionContraccionCoolDown > 0.05f && shot.eyaculacionContraccionCoolDown < 3f)
                    {
                        gap = shot.eyaculacionContraccionCoolDown;
                    }
                    gapMore = Mathf.Clamp(shot.eyaculacionAumentoCoolDownPorCadaContraccion, 0f, 1f);
                    how = $"the game has {spurts} spurt(s) of {drops} drop(s) (each spurt less by {less:0.###}), {perMl:0.####} ml a drop ({perFrom}, read as {per:0.######}), the amount x{q:F2}, {gap:F2} s apart (+{gapMore:F2} s each)";
                }
            }
            catch (Exception ex)
            {
                how = "the game's numbers could not be read (" + ex.Message + ")";
            }
            // (Nothing that makes sense came of it: a man's usual, some three and a half millilitres.)
            if (!(total > 0.2f && total < 40f) || each.Count == 0)
            {
                how += "; taken as 3.5 ml instead";
                total = 3.5f * Mathf.Clamp(amount, 0.3f, 2f);
                each.Clear();
                for (int k = 0; k < spurts; k++)
                {
                    each.Add(Mathf.Lerp(1f, 0.35f, spurts > 1 ? k / (spurts - 1f) : 0f));
                }
            }
            float sum = 0f;
            foreach (float e in each)
            {
                sum += e;
            }
            float ml = Mathf.Clamp(total, 0.5f, 12f);
            float at = Time.unscaledTime + 0.12f;
            for (int k = 0; k < each.Count; k++)
            {
                m_condomSpurts.Add(new Vector2(at, ml * each[k] / Mathf.Max(1e-6f, sum)));
                at += gap + gapMore * k;
            }
            m_condomShotUntil = at + 2f;
            return ml;
        }

        // Spurts reach the teat as their time comes; the quiver they set off dies away. Every frame he wears it.
        private void CondomFillStep(float dt)
        {
            float now = Time.unscaledTime;
            for (int k = m_condomSpurts.Count - 1; k >= 0; k--)
            {
                if (now >= m_condomSpurts[k].x)
                {
                    m_condomMl = Mathf.Min(30f, m_condomMl + m_condomSpurts[k].y);
                    m_condomWobV += 3f * Mathf.Sqrt(Mathf.Max(0f, m_condomSpurts[k].y));
                    m_condomSpurts.RemoveAt(k);
                }
            }
            // (A damped spring, some six times a second, in short steps.)
            const float w = 2f * Mathf.PI * 6f;
            for (float left = dt; left > 1e-5f; left -= 0.01f)
            {
                float h = Mathf.Min(0.01f, left);
                m_condomWobV += (-w * w * m_condomWob - 2f * 0.22f * w * m_condomWobV) * h;
                m_condomWob += m_condomWobV * h;
            }
        }

        // Nothing in it any more (it is off him, or gone).
        private void CondomEmpty()
        {
            m_condomFull = false;
            m_condomMl = 0f;
            m_condomSpurts.Clear();
            m_condomWob = m_condomWobV = 0f;
        }

        private readonly Transform[] m_heroHands = new Transform[2];
        private bool m_heroHandsSearched;
        private bool m_condomHintLogged;
        private int m_condomFrame;
        private bool m_condomBall;

        // How far down him it goes: to this place along him (0 his base, 1 his tip).
        // (No lower than a little above where his shaft comes out of his skin: its root is some centimetres inside him.)
        private float m_condomTo = 0.1f;
        private const float CondomOnTime = 1.6f, CondomOffTime = 1.1f;

        // She is kept up off him while it is put on or taken off.
        private bool CondomWantsHerUp => m_condom == 1 || m_condom == 2 || m_condom == 3 || m_condom == 5;

        // Where along him the latex ends (it is drawn from there to his tip); above 1 when there is none on him.
        private float CondomEdge
        {
            get
            {
                switch (m_condom)
                {
                    case 3: return Mathf.Lerp(1.02f, m_condomTo, Soft(m_condomRoll));
                    case 4: return m_condomTo;
                    case 5: return Mathf.Lerp(m_condomTo, 1.02f, Soft(m_condomRoll));
                    default: return 2f;
                }
            }
        }

        internal int CondomState => m_condom;

        // The colour of the latex and how much of his skin shows through it.
        private static void CondomTint(out Color32 tint, out float cover)
        {
            switch ((Plugin.CondomColor.Value ?? "").ToLowerInvariant())
            {
                case "pink": tint = new Color32(240, 120, 165, 255); cover = 0.7f; break;
                case "red": tint = new Color32(200, 32, 44, 255); cover = 0.78f; break;
                case "blue": tint = new Color32(60, 112, 222, 255); cover = 0.78f; break;
                case "black": tint = new Color32(24, 24, 28, 255); cover = 0.88f; break;
                default: tint = new Color32(228, 224, 212, 255); cover = 0.32f; break;
            }
        }

        // The key, or the button in the window: gets one out, puts it away, or takes off the one he wears.
        internal void ToggleCondom()
        {
            // Out of the ride (he stands, or she has him in her mouth): it goes straight onto him, and comes straight off.
            if (!(m_ride && m_toggled && m_heroReady && m_hero != null))
            {
                if (!m_penisValid || m_wetParts.Count == 0)
                {
                    Notice(Loc.T("The hero's penis was not found yet: the condom cannot be put on.", "Член героя пока не найден: надеть презерватив не на что."));
                    return;
                }
                if (m_condom == 0)
                {
                    CondomBeginRoll(Time.unscaledTime);
                    Notice(Loc.T("The condom goes on.", "Презерватив надевается."));
                }
                else if (m_condom == 4)
                {
                    m_condom = 5;
                    m_condomAt = Time.unscaledTime - 1f;
                    m_condomRoll = 0f;
                    Notice(Loc.T("The condom comes off.", "Презерватив снимается."));
                }
                return;
            }
            float now = Time.unscaledTime;
            switch (m_condom)
            {
                case 0:
                    m_condom = 1;
                    m_condomAt = now;
                    m_condomHand = -1;
                    m_condomHintLogged = false;
                    Notice(Plugin.CondomAuto.Value
                        ? Loc.T("She comes up: the condom goes on by itself.", "Она приподнимается: презерватив наденется сам.")
                        : Loc.T("The condom lies on his belly: take it with his hand and bring it to his tip.", "Презерватив лежит у него на животе: возьмите его рукой и поднесите к головке."));
                    Plugin.ModLog.LogInfo("Ride: condom: one is got out; she comes up off him.");
                    break;
                case 1:
                case 2:
                    m_condom = 0;
                    Notice(Loc.T("The condom is put away.", "Презерватив убран."));
                    Plugin.ModLog.LogInfo("Ride: condom: put away.");
                    break;
                case 4:
                    m_condom = 5;
                    m_condomAt = now;
                    m_condomRoll = 0f;
                    Notice(Loc.T("She comes up: the condom comes off.", "Она приподнимается: презерватив снимается."));
                    Plugin.ModLog.LogInfo("Ride: condom: it is to come off; she comes up off him.");
                    break;
            }
        }

        // The ride is over: a condom he wears stays on him; one that was only got out, or was half on or half off, is
        // settled one way or the other.
        private void CondomRideOver()
        {
            if (m_condom == 1 || m_condom == 2 || m_condom == 5)
            {
                m_condom = 0;
                CondomEmpty();
            }
            else if (m_condom == 3)
            {
                m_condom = 4;
            }
            m_heroHandsSearched = false;
            m_heroHands[0] = m_heroHands[1] = null;
        }

        // There is no hero any more (another scene, another man): nothing of it is left.
        private void CondomReset()
        {
            m_condom = 0;
            m_condomRoll = 0f;
            m_condomHand = -1;
            m_heroHandsSearched = false;
            m_heroHands[0] = m_heroHands[1] = null;
            try
            {
                if (m_condomRing != null) UnityEngine.Object.Destroy(m_condomRing);
                if (m_condomSkin != null) UnityEngine.Object.Destroy(m_condomSkin);
                if (m_condomTeat != null) UnityEngine.Object.Destroy(m_condomTeat);
                if (m_condomTeatMesh != null) UnityEngine.Object.Destroy(m_condomTeatMesh);
                if (m_condomMat != null) UnityEngine.Object.Destroy(m_condomMat);
            }
            catch (Exception)
            {
            }
            m_condomRing = m_condomSkin = m_condomTeat = null;
            m_condomMat = null;
            m_condomTint = "";
            m_condomTeatDraws = null;
            m_condomTeatMesh = null;
            m_teatMl = -1f;
            m_teatTintMl = -1f;
            CondomEmpty();
        }

        // Runs at the end of every frame he lies, after his penis has been looked at as it is drawn.
        private void CondomStep(float dt)
        {
            // (Once a frame, though the end of the frame may come twice.)
            if (m_condomFrame == Time.frameCount)
            {
                return;
            }
            m_condomFrame = Time.frameCount;
            try
            {
                if (m_condom == 0 || !m_penisValid)
                {
                    CondomShow(false, false, false);
                    return;
                }
                // (Lying on his belly or in his hand it is a thing of the ride: out of the ride it is put away.)
                if ((m_condom == 1 || m_condom == 2) && !m_heroReady)
                {
                    m_condom = 0;
                    CondomShow(false, false, false);
                    return;
                }
                // His penis is not shown (he is dressed): neither is it.
                bool hidden = false;
                try
                {
                    hidden = m_heroPen != null && m_heroPen.hidden;
                }
                catch (Exception)
                {
                }
                if (hidden)
                {
                    CondomShow(false, false, false);
                    return;
                }
                float now = Time.unscaledTime;
                if (!PenisNow(out Vector3 b, out Vector3 t))
                {
                    CondomShow(false, false, false);
                    return;
                }
                Vector3 dir = (t - b).normalized;
                float tipR = PenisRadiusAt(0.9f);
                bool herUp = !m_in && m_mount <= 1 && m_inW <= 0.001f;
                CondomMake();
                switch (m_condom)
                {
                    case 1:
                    {
                        // On his belly, a hand's breadth toward his head from his penis, lying flat on his skin.
                        Vector3 spot = b + m_heroFwd * 0.24f;
                        float top = HeroTopAt(spot);
                        spot.y = (float.IsNegativeInfinity(top) ? b.y + 0.02f : top) + 0.008f;
                        CondomPlaceItem(spot, Vector3.up, tipR);
                        if (Plugin.CondomAuto.Value)
                        {
                            if (herUp && now - m_condomAt > 1.2f)
                            {
                                CondomBeginRoll(now);
                            }
                            break;
                        }
                        HeroHandsFind();
                        for (int i = 0; i < 2; i++)
                        {
                            if (m_heroHands[i] != null && (m_heroHands[i].position - spot).magnitude < 0.13f)
                            {
                                m_condom = 2;
                                m_condomHand = i;
                                m_condomAt = now;
                                Notice(Loc.T("He has the condom: bring it to his tip.", "Презерватив в руке: поднесите его к головке."));
                                Plugin.ModLog.LogInfo($"Ride: condom: taken with his {(i == 0 ? "left" : "right")} hand.");
                                break;
                            }
                        }
                        if (m_condom == 1 && !m_condomHintLogged && now - m_condomAt > 6f)
                        {
                            m_condomHintLogged = true;
                            float l = m_heroHands[0] != null ? (m_heroHands[0].position - spot).magnitude : -1f;
                            float r = m_heroHands[1] != null ? (m_heroHands[1].position - spot).magnitude : -1f;
                            Plugin.ModLog.LogInfo($"Ride: condom: still lying on his belly after 6 s (at {spot.ToString("F2")}); his hands are {l * 100f:F0} and {r * 100f:F0} cm from it.");
                        }
                        break;
                    }
                    case 2:
                    {
                        Transform hand = m_condomHand >= 0 ? m_heroHands[m_condomHand] : null;
                        if (hand == null)
                        {
                            m_condom = 1;
                            break;
                        }
                        // Held at his fingers: a little way from his wrist toward his tip.
                        Vector3 to = t - hand.position;
                        Vector3 at = hand.position + Vector3.ClampMagnitude(to, 0.09f);
                        CondomPlaceItem(at, dir, tipR);
                        if (herUp && (at - t).magnitude < 0.06f)
                        {
                            CondomBeginRoll(now);
                        }
                        break;
                    }
                    case 3:
                    case 4:
                    case 5:
                    {
                        if (m_condom == 3)
                        {
                            m_condomRoll = Mathf.Clamp01(m_condomRoll + dt / CondomOnTime);
                            if (m_condomRoll >= 1f)
                            {
                                m_condom = 4;
                                Notice(Loc.T("The condom is on.", "Презерватив надет."));
                                Plugin.ModLog.LogInfo("Ride: condom: it is on him; she takes him in again.");
                            }
                        }
                        else if (m_condom == 5 && herUp && now - m_condomAt > 0.8f)
                        {
                            m_condomRoll = Mathf.Clamp01(m_condomRoll + dt / CondomOffTime);
                            if (m_condomRoll >= 1f)
                            {
                                m_condom = 0;
                                CondomEmpty();
                                Notice(Loc.T("The condom is off.", "Презерватив снят."));
                                Plugin.ModLog.LogInfo("Ride: condom: it is off him.");
                                CondomShow(false, false, false);
                                break;
                            }
                        }
                        // The ring where it has rolled down to, the teat on his tip.
                        float lowest = 0.1f;
                        try
                        {
                            Transform skin = m_heroPen != null ? m_heroPen.skinSurfaceTransform : null;
                            float len = (t - b).magnitude;
                            if (skin != null && len > 0.03f)
                            {
                                lowest = Mathf.Clamp(Vector3.Dot(skin.position - b, dir) / len + 0.05f, 0.1f, 0.5f);
                            }
                        }
                        catch (Exception)
                        {
                        }
                        m_condomTo = Mathf.Lerp(m_condomTo, lowest, 1f - Mathf.Exp(-dt / 0.3f));
                        float edge = Mathf.Clamp01(CondomEdge);
                        float ringR = PenisRadiusAt(edge) * 1.04f;
                        // (The more of it is still rolled up, the thicker the ring.)
                        float thick = Mathf.Lerp(1f, 0.5f, Mathf.InverseLerp(1f, m_condomTo, edge));
                        Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir);
                        // (The ring is not shown on him: at the root of his shaft it read as a red band round it.)
                        CondomShow(false, false, true);
                        m_condomRing.transform.SetPositionAndRotation(Vector3.Lerp(b, t, edge), rot);
                        m_condomRing.transform.localScale = new Vector3(ringR, ringR * thick, ringR);
                        // The teat on his very tip: as full as what he came in it, sagging the way down is.
                        CondomFillStep(dt);
                        m_condomTeat.transform.SetPositionAndRotation(t - dir * 0.009f, rot);
                        m_condomTeat.transform.localScale = Vector3.one;
                        TeatShape(m_condomMl, Quaternion.Inverse(rot) * Vector3.down, m_condomWob);
                        TeatTint();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("The condom failed and is put away: " + ex.Message);
                CondomReset();
            }
        }

        private void CondomBeginRoll(float now)
        {
            m_condom = 3;
            m_condomAt = now;
            m_condomRoll = 0f;
            Plugin.ModLog.LogInfo("Ride: condom: at his tip; it rolls down him.");
        }

        // The rolled condom as a thing by itself: the ring, the skin across it and the teat in its middle.
        private void CondomPlaceItem(Vector3 at, Vector3 axis, float radius)
        {
            CondomShow(true, true, true);
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis);
            float r = Mathf.Clamp(radius, 0.012f, 0.03f) * 1.04f;
            m_condomRing.transform.SetPositionAndRotation(at, rot);
            m_condomRing.transform.localScale = Vector3.one * r;
            m_condomSkin.transform.SetPositionAndRotation(at, rot);
            m_condomSkin.transform.localScale = Vector3.one * r;
            // (The teat is made in metres and stands on the place his tip will be: let into the rolled ring a little.)
            m_condomTeat.transform.SetPositionAndRotation(at - rot * Vector3.up * 0.006f, rot);
            m_condomTeat.transform.localScale = Vector3.one;
            TeatShape(0f, Vector3.zero, 0f);
            TeatTint();
        }

        // The teat's shape, made anew when what is in it, the way down or the quiver has moved on: a revolved outline
        // round his tip's axis (y), from 9 mm below his tip (inside his glans, where the latex drawn on his skin takes
        // over) out to the end of the teat. Empty, it is a small teat; with his semen in it, a round bulb that holds
        // that much (a millilitre is a cubic centimetre), its latex round it, hanging the way down is and a little
        // flattened onto his tip when down is along him.
        private static readonly List<Vector2> s_teatLine = new List<Vector2>(16);
        private Vector3[] m_teatVerts;
        private bool m_teatTris;
        private const int TeatAround = 16, TeatRows = 13;

        private void TeatShape(float ml, Vector3 down, float wob)
        {
            if (m_condomTeatMesh == null)
            {
                return;
            }
            bool quiet = Mathf.Abs(wob) < 0.002f && Mathf.Abs(m_teatWob) < 0.002f;
            if (m_teatVerts != null && Mathf.Abs(ml - m_teatMl) < 0.02f && quiet && (down - m_teatDown).sqrMagnitude < 0.004f)
            {
                return;
            }
            m_teatMl = ml;
            m_teatWob = wob;
            m_teatDown = down;
            float vol = Mathf.Max(0f, ml) * 1e-6f;
            float rn = TeatSize;
            // (The bulb a little over what it holds: the latex round it has a thickness, and it is not quite a sphere.)
            float r = Mathf.Max(0.0042f, Mathf.Pow(3f * vol / (4f * Mathf.PI), 1f / 3f) * 1.08f) * (1f + 0.12f * Mathf.Clamp(wob, -1f, 1f));
            float yc = 0.009f + Mathf.Max(0.004f, r * 0.85f);
            s_teatLine.Clear();
            s_teatLine.Add(new Vector2(rn, 0f));
            float from = 0f;
            float yStart = yc;
            if (r > rn)
            {
                from = -Mathf.Acos(rn / r);
                yStart = yc + r * Mathf.Sin(from);
            }
            s_teatLine.Add(new Vector2(rn, Mathf.Clamp(yStart - 0.0005f, 0.001f, 0.0075f)));
            for (int i = 0; i < TeatRows - 2; i++)
            {
                float a = Mathf.Lerp(from, Mathf.PI * 0.5f, i / (TeatRows - 3f));
                s_teatLine.Add(new Vector2(r * Mathf.Cos(a), yc + r * Mathf.Sin(a)));
            }
            int cols = TeatAround + 1;
            if (m_teatVerts == null)
            {
                m_teatVerts = new Vector3[TeatRows * cols];
            }
            // The way down across his axis, and along it (below 0: down is toward his root, as when he points up).
            float fill = Mathf.Clamp01(ml / 3f);
            Vector3 across = new Vector3(down.x, 0f, down.z);
            float along = down.y;
            float top = yc + r - 0.009f;
            for (int i = 0; i < TeatRows; i++)
            {
                Vector2 p = s_teatLine[i];
                float h = Mathf.Max(0f, p.y - 0.009f);
                float s = top > 1e-5f ? h / top : 0f;
                float y = 0.009f + (p.y - 0.009f) * (1f - 0.18f * along * fill);
                float rr = p.x * (1f + 0.08f * along * fill);
                Vector3 sag = across * (0.55f * r * fill * s * s);
                if (p.y < 0.009f)
                {
                    y = p.y;
                    rr = p.x;
                    sag = Vector3.zero;
                }
                for (int j = 0; j < cols; j++)
                {
                    float a = j / (float)TeatAround * Mathf.PI * 2f;
                    m_teatVerts[i * cols + j] = new Vector3(rr * Mathf.Cos(a), y, rr * Mathf.Sin(a)) + sag;
                }
            }
            m_condomTeatMesh.vertices = m_teatVerts;
            if (!m_teatTris)
            {
                m_teatTris = true;
                var tris = new int[(TeatRows - 1) * TeatAround * 6];
                int k = 0;
                for (int i = 0; i + 1 < TeatRows; i++)
                {
                    for (int j = 0; j < TeatAround; j++)
                    {
                        int a0 = i * cols + j, a1 = (i + 1) * cols + j, b0 = a0 + 1, b1 = a1 + 1;
                        tris[k++] = a0; tris[k++] = a1; tris[k++] = b0;
                        tris[k++] = a1; tris[k++] = b1; tris[k++] = b0;
                    }
                }
                m_condomTeatMesh.triangles = tris;
            }
            m_condomTeatMesh.RecalculateNormals();
            m_condomTeatMesh.RecalculateBounds();
        }

        // The teat's colour: the latex's, and white with the game's semen through it as it fills.
        private void TeatTint()
        {
            if (m_condomTeatDraws == null || m_condomMat == null || Mathf.Abs(m_condomMl - m_teatTintMl) < 0.05f || !m_condomMat.HasProperty("_BaseColor"))
            {
                return;
            }
            m_teatTintMl = m_condomMl;
            if (m_condomTeatBlock == null)
            {
                m_condomTeatBlock = new MaterialPropertyBlock();
            }
            Color latex = m_condomMat.GetColor("_BaseColor");
            Color semen = new Color(s_semenColour.r / 255f, s_semenColour.g / 255f, s_semenColour.b / 255f, 1f);
            float k = 0.85f * Soft(m_condomMl / 1.2f);
            Color c = Color.Lerp(latex, Color.Lerp(semen, new Color(0.94f, 0.92f, 0.86f, 1f), 0.4f), k);
            c.a = Mathf.Max(latex.a, k);
            m_condomTeatDraws.GetPropertyBlock(m_condomTeatBlock);
            m_condomTeatBlock.SetColor("_BaseColor", c);
            if (m_condomMat.HasProperty("_Smoothness"))
            {
                m_condomTeatBlock.SetFloat("_Smoothness", Mathf.Lerp(0.82f, s_semenSmooth, k));
            }
            m_condomTeatDraws.SetPropertyBlock(m_condomTeatBlock);
        }

        private void CondomShow(bool ring, bool skin, bool teat)
        {
            if (m_condomRing != null && m_condomRing.activeSelf != ring) m_condomRing.SetActive(ring);
            if (m_condomSkin != null && m_condomSkin.activeSelf != skin) m_condomSkin.SetActive(skin);
            if (m_condomTeat != null && m_condomTeat.activeSelf != teat) m_condomTeat.SetActive(teat);
        }

        private void HeroHandsFind()
        {
            if (m_heroHandsSearched)
            {
                return;
            }
            m_heroHandsSearched = true;
            Animator anim = m_hero.bodyAnimator;
            if (anim != null && anim.isHuman)
            {
                m_heroHands[0] = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                m_heroHands[1] = anim.GetBoneTransform(HumanBodyBones.RightHand);
            }
            Plugin.ModLog.LogInfo($"Ride: condom: his hands: left {(m_heroHands[0] != null ? m_heroHands[0].name : "not found")}, right {(m_heroHands[1] != null ? m_heroHands[1].name : "not found")}.");
        }

        // The shapes and their material, made once for a ride; the colour is kept as set.
        private void CondomMake()
        {
            if (m_condomRing == null)
            {
                m_condomMat = CondomMaterial();
                m_condomBall = m_condomMat != null && m_condomMat.shader != null && !string.Equals(m_condomMat.shader.name, "HDRP/Lit", StringComparison.Ordinal) && m_wetParts.Count > 0 && m_wetParts[0].material != null && m_condomMat.shader != m_wetParts[0].material.shader;
                int layer = 0;
                foreach (WetPart p in m_wetParts)
                {
                    if (p.renderer != null)
                    {
                        layer = p.renderer.gameObject.layer;
                        break;
                    }
                }
                // The ring: a tube bent round, of radius 1 with a tube of 0.13.
                var ring = new List<Vector2>();
                for (int i = 0; i <= 10; i++)
                {
                    float a = i / 10f * Mathf.PI * 2f;
                    ring.Add(new Vector2(1f + 0.13f * Mathf.Cos(a), 0.13f * Mathf.Sin(a)));
                }
                m_condomRing = CondomShape("CMAScenes.CondomRing", Revolve(ring, 28), layer);
                // The skin across the ring, while it is rolled up.
                var skin = new List<Vector2> { new Vector2(0f, -0.03f), new Vector2(0.95f, -0.03f), new Vector2(0.95f, 0.03f), new Vector2(0f, 0.03f) };
                m_condomSkin = CondomShape("CMAScenes.CondomSkin", Revolve(skin, 28), layer);
                // The teat: a shape of its own made in metres and made anew as it fills (TeatShape).
                // (The teat alone: the latex over his glans is drawn on his skin, and a shape over it looked wrong.)
                m_condomTeatMesh = new Mesh { name = "CMAScenes.CondomTeat", hideFlags = HideFlags.HideAndDontSave };
                m_condomTeatMesh.MarkDynamic();
                m_teatVerts = null;
                m_teatTris = false;
                m_teatMl = -1f;
                m_teatTintMl = -1f;
                m_condomTeat = CondomShape("CMAScenes.CondomTeat", m_condomTeatMesh, layer);
                m_condomTeatDraws = m_condomTeat.GetComponent<MeshRenderer>();
                TeatShape(0f, Vector3.zero, 0f);
                Plugin.ModLog.LogInfo($"Ride: condom: its shapes were made (material from {(m_condomMat != null ? "'" + m_condomMat.shader.name + "'" : "nothing: they may not show")}, layer {layer}).");
            }
            string tintName = Plugin.CondomColor.Value ?? "";
            if (m_condomMat != null && tintName != m_condomTint && !m_condomBall)
            {
                m_condomTint = tintName;
                CondomTint(out Color32 tint, out float _);
                Color c = tint;
                if (m_condomMat.HasProperty("_BaseColor")) m_condomMat.SetColor("_BaseColor", c);
                if (m_condomMat.HasProperty("_Color")) m_condomMat.SetColor("_Color", c);
                // (The teat's own colour is made anew from it.)
                m_teatTintMl = -1f;
            }
        }

        private GameObject CondomShape(string name, Mesh mesh, int layer)
        {
            var go = new GameObject(name) { layer = layer, hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            if (m_condomMat != null)
            {
                mr.sharedMaterial = m_condomMat;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return go;
        }

        // Latex: the material of his own skin (it is known to draw in the game's view) with nothing of the skin left in
        // it: plain, smooth, drawn from both sides.
        private Material CondomMaterial()
        {
            Material src = null;
            foreach (WetPart p in m_wetParts)
            {
                if (p.material != null)
                {
                    src = p.material;
                    break;
                }
            }
            Material m;
            Material ball = GameBallMaterial();
            if (ball != null)
            {
                // The game's own ball: its look is taken as it is.
                m = new Material(ball) { name = "CMAScenes.Condom", hideFlags = HideFlags.HideAndDontSave };
                if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", 0f);
                if (m.HasProperty("_CullModeForward")) m.SetFloat("_CullModeForward", 0f);
                return m;
            }
            if (src != null)
            {
                m = new Material(src);
            }
            else
            {
                Shader lit = Shader.Find("HDRP/Lit");
                if (lit == null)
                {
                    return null;
                }
                m = new Material(lit);
            }
            m.name = "CMAScenes.Condom";
            m.hideFlags = HideFlags.HideAndDontSave;
            Texture2D white = Texture2D.whiteTexture;
            foreach (string map in new[] { "_BaseColorMap", "_MainTex" })
            {
                if (m.HasProperty(map)) m.SetTexture(map, white);
            }
            foreach (string map in new[] { "_MaskMap", "_NormalMap", "_DetailMap", "_ThicknessMap", "_SubsurfaceMaskMap" })
            {
                if (m.HasProperty(map)) m.SetTexture(map, null);
            }
            m.DisableKeyword("_MASKMAP");
            m.DisableKeyword("_NORMALMAP");
            m.DisableKeyword("_DETAIL_MAP");
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.82f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", 0f);
            if (m.HasProperty("_CullModeForward")) m.SetFloat("_CullModeForward", 0f);
            if (m.HasProperty("_DoubleSidedEnable")) m.SetFloat("_DoubleSidedEnable", 1f);
            return m;
        }

        // The ball the game itself shows (the one of its colliders): its material, if there is one in the scene. What was
        // found is written to the log, so that the right one can be told from the others.
        private static readonly System.Text.RegularExpressions.Regex BallName = new System.Text.RegularExpressions.Regex("collid|ball|bola|esfera|grab|touch|cursor|mano|hand|proxy", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private Material GameBallMaterial()
        {
            try
            {
                Material pick = null;
                int shown = 0;
                var seen = new HashSet<int>();
                var all = Resources.FindObjectsOfTypeAll<MeshFilter>();
                for (int i = 0; all != null && i < all.Length; i++)
                {
                    MeshFilter mf = all[i];
                    if (mf == null || mf.sharedMesh == null || !mf.gameObject.scene.IsValid() || mf.name.StartsWith("CMAScenes", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    string mesh = mf.sharedMesh.name ?? "";
                    if (mesh.IndexOf("sphere", StringComparison.OrdinalIgnoreCase) < 0 && mesh.IndexOf("ball", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                    MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                    Material mat = mr != null ? mr.sharedMaterial : null;
                    if (mat == null || !seen.Add(mat.GetInstanceID()))
                    {
                        continue;
                    }
                    string parent = mf.transform.parent != null ? mf.transform.parent.name : "";
                    bool named = BallName.IsMatch(mf.name) || BallName.IsMatch(parent) || BallName.IsMatch(mat.name);
                    if (shown < 12)
                    {
                        shown++;
                        Plugin.ModLog.LogInfo($"Ride: condom: a ball of the game's: '{parent}/{mf.name}' (mesh '{mesh}', shown {mr.enabled && mf.gameObject.activeInHierarchy}), material '{mat.name}', shader '{(mat.shader != null ? mat.shader.name : "?")}'{(named ? " - taken by its name" : "")}.");
                    }
                    if (named && pick == null)
                    {
                        pick = mat;
                    }
                }
                if (shown == 0)
                {
                    Plugin.ModLog.LogInfo("Ride: condom: no ball of the game's was found in the scene; it is made of the plugin's own latex.");
                }
                return pick;
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogInfo("Ride: condom: the game's balls could not be looked for (" + ex.Message + ").");
                return null;
            }
        }

        // A shape turned round the Y axis from its outline (how far out, how high), the outline going round the shape
        // anticlockwise so that it faces outward.
        private static Mesh Revolve(List<Vector2> outline, int around)
        {
            int rows = outline.Count, cols = around + 1;
            var vs = new Vector3[rows * cols];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    float a = j / (float)around * Mathf.PI * 2f;
                    vs[i * cols + j] = new Vector3(outline[i].x * Mathf.Cos(a), outline[i].y, outline[i].x * Mathf.Sin(a));
                }
            }
            var tris = new int[(rows - 1) * around * 6];
            int k = 0;
            for (int i = 0; i + 1 < rows; i++)
            {
                for (int j = 0; j < around; j++)
                {
                    int a0 = i * cols + j, a1 = (i + 1) * cols + j, b0 = a0 + 1, b1 = a1 + 1;
                    tris[k++] = a0; tris[k++] = a1; tris[k++] = b0;
                    tris[k++] = a1; tris[k++] = b1; tris[k++] = b0;
                }
            }
            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
