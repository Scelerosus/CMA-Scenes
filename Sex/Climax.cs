using System;
using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.AI;
using UnityEngine;

namespace CMAScenes
{
    public sealed partial class ShakeController
    {
        // ---- the climax ----
        //
        // When she comes (the game says so: Lead.cs, Body.cs), or when he comes in her, the ride stops for a while and she
        // is pressed down on him as deep as she sits.
        //
        // Hers was a tremble once: her hips shaken eight times a second, dying away. Nobody comes like that. What a body
        // does is this: it tenses (her back arches, her head goes back), and the muscles round him squeeze in a row of
        // slow contractions, some eight tenths of a second apart at first, then further apart and weaker, each one
        // tucking her pelvis under and pressing her down on him a little; under it all a fine, uneven quiver. Then she
        // lets go and sinks forward, out of breath. The game has the numbers for her: how many contractions her climax
        // has and how long it lasts (ContraccionesDeOrgasmo, DuracionDeOrgasmo, by her personality); they are hers here.
        //
        // His, when she has not come herself: she holds him in her, tense and still.

        private float m_peakWet;
        private float m_climaxAt = -100f, m_cameAt = -100f, m_herClimaxAt = -100f;
        // How long this one lasts in all, and whether it is hers.
        private float m_climaxLasts = 6.5f;
        private bool m_climaxHers;
        private float ClimaxLasts => m_climaxLasts;
        // Her contractions: when each begins (seconds into the climax), how long it takes and how strong it is.
        private const int MostSqueezes = 16;
        private readonly float[] m_squeezeAt = new float[MostSqueezes], m_squeezeFor = new float[MostSqueezes], m_squeezeBy = new float[MostSqueezes];
        private int m_squeezes;
        // Now: how hard she squeezes him (0 to 1), how tense she is, how much she has let go again after it, and how far
        // her head is back (degrees).
        private float m_squeezeNow, m_tenseNow, m_letGoNow, m_headBack;
        // The game's own numbers of her climax (looked for once for each woman).
        private ContraccionesDeOrgasmo m_gameSqueezes;
        private DuracionDeOrgasmo m_gameLasts;
        private Woman m_climaxWoman;

        // How many contractions her climax has and how long it lasts, as the game has it for her.
        private bool ClimaxOfGame(out int squeezes, out float lasts)
        {
            squeezes = 0;
            lasts = 0f;
            Woman w = m_rideWoman;
            if (w == null || w.character == null)
            {
                return false;
            }
            try
            {
                if (m_climaxWoman != w)
                {
                    m_climaxWoman = w;
                    m_gameSqueezes = w.character.GetComponentInChildren<ContraccionesDeOrgasmo>(true);
                    m_gameLasts = w.character.GetComponentInChildren<DuracionDeOrgasmo>(true);
                }
                if (m_gameSqueezes != null)
                {
                    squeezes = m_gameSqueezes.currentContraccionesDeOrgasmo;
                }
                if (m_gameLasts != null)
                {
                    lasts = m_gameLasts.currentDuracionTotalDeOrgasmo;
                }
                return squeezes > 0 || lasts > 0f;
            }
            catch (Exception)
            {
                m_gameSqueezes = null;
                m_gameLasts = null;
                return false;
            }
        }

        private void ClimaxBegin(string who, bool hers)
        {
            float now = Time.unscaledTime;
            if (!hers && m_climaxHers && ClimaxNow() > 0.5f)
            {
                // He comes while she is coming: hers goes on, a little longer.
                m_climaxLasts = Mathf.Max(m_climaxLasts, now - m_climaxAt + 3.5f);
                Plugin.ModLog.LogInfo("Ride: climax (" + who + "): she is coming herself; hers goes on.");
                return;
            }
            m_climaxAt = now;
            m_climaxHers = hers;
            m_squeezes = 0;
            m_climaxLasts = 6.5f;
            string how = "she holds him in her, tense and still, for a few seconds";
            if (hers)
            {
                bool byGame = ClimaxOfGame(out int n, out float lasts);
                int asked = n;
                n = Mathf.Clamp(n > 0 ? n : 8, 4, 14);
                // (The first comes once she has tensed; each next one a little later and weaker than the one before.)
                float t = 0.45f;
                for (int k = 0; k < n; k++)
                {
                    m_squeezeAt[k] = t;
                    m_squeezeFor[k] = 0.5f + 0.03f * k;
                    m_squeezeBy[k] = Mathf.Lerp(1f, 0.38f, n > 1 ? k / (n - 1f) : 0f) * UnityEngine.Random.Range(0.88f, 1.08f);
                    t += 0.78f + 0.07f * k + UnityEngine.Random.Range(-0.04f, 0.07f);
                }
                float end = m_squeezeAt[n - 1] + m_squeezeFor[n - 1];
                if (lasts > 0f)
                {
                    // (Drawn out or pressed together to take the time the game gives her, within reason.)
                    float fit = Mathf.Clamp((Mathf.Clamp(lasts, 4f, 16f) - 1.2f) / end, 0.75f, 1.4f);
                    for (int k = 0; k < n; k++)
                    {
                        m_squeezeAt[k] *= fit;
                        m_squeezeFor[k] *= Mathf.Min(fit, 1.15f);
                    }
                    end = m_squeezeAt[n - 1] + m_squeezeFor[n - 1];
                }
                m_squeezes = n;
                m_climaxLasts = end + 2.2f;
                how = $"{n} contractions over {end:F1} s, further apart and weaker as they go ({(byGame ? $"the game has {asked} of them and {lasts:F1} s for her" : "the game's numbers for her were not to be had")}), then she lets go";
            }
            Plugin.ModLog.LogInfo("Ride: climax (" + who + "): " + how + ".");
        }

        // Once a frame (Body.cs): where she is in it.
        private void ClimaxStep(float dt)
        {
            float t = Time.unscaledTime - m_climaxAt;
            float on = ClimaxNow();
            float squeeze = 0f, tense = 0f, letGo = 0f;
            if (on > 0f)
            {
                if (m_climaxHers && m_squeezes > 0)
                {
                    float last = m_squeezeAt[m_squeezes - 1] + m_squeezeFor[m_squeezes - 1];
                    // The game still has her coming as the last one ends: another comes, weaker again. (Its climax and
                    // hers here begin together, and it is the game's that says when it is over.)
                    if (m_gOrgasm && m_gFresh && t > last - 0.1f && t < 28f && m_squeezes < MostSqueezes)
                    {
                        int k = m_squeezes;
                        m_squeezeAt[k] = Mathf.Max(t, last) + 0.45f + 0.07f * k;
                        m_squeezeFor[k] = Mathf.Min(0.95f, m_squeezeFor[k - 1] + 0.03f);
                        m_squeezeBy[k] = Mathf.Max(0.3f, m_squeezeBy[k - 1] * 0.92f);
                        m_squeezes++;
                        last = m_squeezeAt[k] + m_squeezeFor[k];
                        m_climaxLasts = last + 2.2f;
                    }
                    for (int k = 0; k < m_squeezes; k++)
                    {
                        float x = (t - m_squeezeAt[k]) / m_squeezeFor[k];
                        if (x > 0f && x < 1f)
                        {
                            // (Quick to grip, slower to let go.)
                            float wave = Mathf.Sin(Mathf.PI * Mathf.Pow(x, 0.55f));
                            squeeze = Mathf.Max(squeeze, m_squeezeBy[k] * wave * wave);
                        }
                    }
                    float gone = Mathf.SmoothStep(0f, 1f, (t - last) / 1.8f);
                    tense = Mathf.SmoothStep(0f, 1f, t / 0.5f) * (1f - gone);
                    letGo = gone;
                }
                else
                {
                    tense = 0.35f;
                }
            }
            m_squeezeNow = squeeze * on;
            m_tenseNow = Mathf.Lerp(m_tenseNow, tense * on, 1f - Mathf.Exp(-dt / 0.12f));
            m_letGoNow = Mathf.Lerp(m_letGoNow, letGo * on, 1f - Mathf.Exp(-dt / 0.3f));
            m_headBack = Mathf.Lerp(m_headBack, m_climaxHers ? 13f * m_tenseNow : 0f, 1f - Mathf.Exp(-dt / 0.35f));
        }

        // What it does to her as she is posed (Ride.cs, after the smoothing of the pose, which would take a contraction
        // out again): her pelvis tucks under and presses down with each one, her back is arched and she leans back a
        // little for as long as she is tense, and she sinks forward as she lets go.
        private void ClimaxPose(float poseW, Vector3 fwd, ref Vector3 stroke, ref float pitch, ref float roll, ref float lean, out float arch)
        {
            float squeeze = m_squeezeNow * poseW, tense = m_tenseNow * poseW;
            float t = Time.unscaledTime;
            // (The quiver: small, uneven, never a steady shake.)
            float quiver = tense * (m_climaxHers ? 1f : 0.3f);
            float q1 = Mathf.PerlinNoise(t * 9.5f, m_seed + 3f) * 2f - 1f;
            float q2 = Mathf.PerlinNoise(t * 7.3f, m_seed + 13f) * 2f - 1f;
            stroke += Vector3.down * (0.004f * squeeze) + fwd * (0.003f * squeeze);
            pitch += -3.2f * squeeze + 0.32f * q1 * quiver;
            roll += 0.22f * q2 * quiver;
            lean += -2.5f * tense + 4f * m_letGoNow * poseW;
            arch = 5.5f * tense - 1.6f * squeeze;
        }

        // The ride in waves: a slow swell of the speed (about every half minute), and after a climax she hardly moves for
        // a while, her speed coming back over the next ten seconds or so.
        private float RideWave(float t, bool swell)
        {
            float wave = swell ? 1f + 0.16f * Mathf.Sin(t * (2f * Mathf.PI / 28f) + m_seed) : 1f;
            float since = Time.unscaledTime - m_climaxAt;
            float calm = since < 0f || since > 16f ? 0f : Mathf.Clamp01(Mathf.Min(1f, (16f - since) / 10f));
            return wave * (1f - 0.8f * calm);
        }

        // With the pose flow on: a pose for how aroused she is. Cowgirl to begin with, Astride when she warms up, Over him
        // when she is nearly there, and back to Cowgirl once a climax is over. Never within half a minute of the last change.
        private float m_flowAt = -100f;
        private void PoseFlowStep()
        {
            float now = Time.unscaledTime;
            if (!Plugin.RideFlow.Value || m_mount != 3 || !m_in || m_holeAnal || now - m_flowAt < 30f || now - m_climaxAt < 10f)
            {
                return;
            }
            int cur = Poses.IndexOf(Poses.Ride, Plugin.RidePoseType.Value);
            if (cur < 0 || cur > 2)
            {
                return;
            }
            int next = cur;
            if (m_climaxAt > m_flowAt && cur != 0)
            {
                next = 0;
            }
            else if (cur == 0 && m_herWet > 0.35f)
            {
                next = 1;
            }
            else if (cur == 1 && m_herWet > 0.8f)
            {
                next = 2;
            }
            if (next == cur)
            {
                return;
            }
            m_flowAt = now;
            Poses.Set(Poses.Ride[next], Plugin.RidePoseType);
            Plugin.ModLog.LogInfo("Ride: pose flow: " + Poses.Ride[cur].name + " to " + Poses.Ride[next].name + " (aroused " + m_herWet.ToString("F2") + ").");
        }

        // How much of the climax there is now: in within half a second, out over a second and a half.
        private float ClimaxNow()
        {
            float t = Time.unscaledTime - m_climaxAt;
            if (t < 0f || t > m_climaxLasts || !Plugin.Alive(Plugin.RideClimax))
            {
                return 0f;
            }
            return Mathf.SmoothStep(0f, 1f, Mathf.Min(t / 0.4f, (m_climaxLasts - t) / 1.5f));
        }

        // The game is about to make a penis come (called from the patch on its own method, Semen.cs).
        internal void HeroCame(IntPtr penis)
        {
            if (!m_ride || !m_heroReady || m_heroPen == null || penis == IntPtr.Zero || m_heroPen.Pointer != penis)
            {
                return;
            }
            float now = Time.unscaledTime;
            if (now - m_cameAt < 12f)
            {
                return;
            }
            m_cameAt = now;
            bool inHer = m_in && m_mount >= 2;
            BodyHeCame(inHer);
            XRayHeCame(inHer);
            // (Nothing of it is put on him: what he came is in her, as the game has it, and in the X-ray. He keeps only
            // the wet look of her. In a condom, the condom says so itself, with how much: Condom.cs CondomCatches.)
            if (inHer && m_condom != 4)
            {
                Notice(Loc.T("He came inside her.", "Он кончил в неё."));
            }
            if (Plugin.Alive(Plugin.RideClimax) && inHer)
            {
                ClimaxBegin("he came", false);
            }
        }

        // She comes when the game's own pleasure of her reaches its top (Body.cs; where that is not to be had, when she
        // is as wet as the game has her get), not more often than every so often. Counted, and it floods her, whether or
        // not the ride is set to stop for it.
        private void HerClimaxStep()
        {
            float now = Time.unscaledTime;
            if (m_mount != 3 || !m_in || !HerPeakNow()
                || now - m_herClimaxAt < (m_herKnown ? 20f : 75f) || (m_climaxHers && now - m_climaxAt < (m_herKnown ? 8f : 20f)))
            {
                return;
            }
            m_herClimaxAt = now;
            m_peakWet = 1f;
            BodySheCame();
            Notice(Loc.T("She is coming.", "Она кончает."));
            ClimaxBegin(m_gOrgasm && m_gFresh ? "she comes, by the game's rhythm of her" : m_herKnown ? "her pleasure is at its top, by the game" : "she is as wet as she gets", true);
        }
    }
}
