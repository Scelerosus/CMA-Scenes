using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers.Interacciones;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace CMAScenes
{
    public sealed partial class ShakeController
    {
        // Decides what she does: which move, how fast, when she eases off, and how deep she leans.
        private readonly Motion m_motion = new Motion();
        private readonly float m_seed = UnityEngine.Random.value * 100f;
        private int m_move = -1;
        private float m_moveEnd;
        private float m_nextPause;
        private float m_pauseEnd;
        private float m_leanTarget;

        // The rhythm. Steady is one bounce. Random picks a move now and then. Scenario is a fixed sequence that loops: a slow
        // circle, bounces, a pause, a faster stretch, side to side, and round again the other way. Moves melt into each other,
        // and the speed and depth of everything follow the preset.
        private struct Step
        {
            public float seconds;
            public int move;
            public float tempo;
            public float intensity;
            public int dir;
        }

        private static readonly Step[] Scenario =
        {
            new Step { seconds = 10f, move = 2, tempo = 0.65f, intensity = 1f, dir = 1 },
            new Step { seconds = 8f, move = 0, tempo = 1.0f, intensity = 1f, dir = 1 },
            new Step { seconds = 2.2f, move = 0, tempo = 0.9f, intensity = 0.12f, dir = 1 },
            new Step { seconds = 9f, move = 0, tempo = 1.45f, intensity = 1f, dir = 1 },
            new Step { seconds = 7f, move = 1, tempo = 0.9f, intensity = 1f, dir = 1 },
            new Step { seconds = 6f, move = 3, tempo = 1.1f, intensity = 1f, dir = 1 },
            new Step { seconds = 2f, move = 3, tempo = 0.8f, intensity = 0.15f, dir = 1 },
            new Step { seconds = 9f, move = 2, tempo = 0.7f, intensity = 1f, dir = -1 }
        };

        private int m_step = -1;
        // How many strokes (or bounces) a second she makes right now, all told: for the line on the screen.
        private float m_speedNow;
        private float m_tempoGoal = 1f;
        private float m_tempoNow = 1f;
        private float m_rateNow = -1f;
        // How long one move takes to melt into the next, in seconds.
        private const float MoveMelt = 0.9f;
        // How long the bounce takes to fade in and out, in seconds.
        private const float ShakeRamp = 1f;
        private float m_intensityGoal = 1f;

        // What a preset does to the speed and the depth of the motion. Mine leaves the settings exactly as they are.
        private static void PresetFactors(out float speed, out float amp)
        {
            switch ((Plugin.PresetName.Value ?? "Mine").ToLowerInvariant())
            {
                case "slow":
                    speed = 0.45f;
                    amp = 1f;
                    break;
                case "soft":
                    speed = 0.7f;
                    amp = 0.8f;
                    break;
                case "fast":
                    speed = 1.4f;
                    amp = 0.9f;
                    break;
                default:
                    speed = 1f;
                    amp = 1f;
                    break;
            }
        }

        internal void CyclePreset()
        {
            string[] names = { "Slow", "Soft", "Fast", "Mine" };
            int i = Array.FindIndex(names, n => string.Equals(n, Plugin.PresetName.Value, StringComparison.OrdinalIgnoreCase));
            string next = names[(i + 1) % names.Length];
            Plugin.PresetName.Value = next;
            try
            {
                Plugin.Cfg.Save();
            }
            catch (Exception)
            {
            }
            Notice(Loc.T("Mood: ", "Настрой: ") + Loc.Preset(next));
        }

        private void ResetDirector()
        {
            m_move = -1;
            m_step = -1;
            m_moveEnd = 0f;
            m_tempoGoal = 1f;
            m_intensityGoal = 1f;
            m_nextPause = Time.time + UnityEngine.Random.Range(9f, 15f);
            m_pauseEnd = 0f;
            if (m_shakeW > 0.001f)
            {
                // Turned on again while she is still moving: what she is doing is left to melt into the new start.
                return;
            }
            m_tempoNow = 1f;
            m_rateNow = -1f;
            m_motion.bounce = 1f;
            m_motion.sway = 0f;
            m_motion.circle = 0f;
            m_motion.intensity = 1f;
            m_motion.leanExtra = 0f;
        }

        private void StepDirector(float dt, float shakeW)
        {
            Motion mo = m_motion;
            float t = Time.time;
            string rhythm = Plugin.Rhythm.Value ?? "Scenario";
            bool scenario = string.Equals(rhythm, "Scenario", StringComparison.OrdinalIgnoreCase);
            bool random = string.Equals(rhythm, "Random", StringComparison.OrdinalIgnoreCase);

            // The preset eases in, so that changing it never makes a jump.
            PresetFactors(out float speedGoal, out float ampGoal);
            float presetBlend = 1f - Mathf.Exp(-dt / 0.8f);
            mo.speed = Mathf.Lerp(mo.speed, speedGoal, presetBlend);
            mo.amp = Mathf.Lerp(mo.amp, ampGoal, presetBlend);

            if (scenario || random)
            {
                if (shakeW > 0.5f && t >= m_moveEnd)
                {
                    if (scenario)
                    {
                        m_step = (m_step + 1) % Scenario.Length;
                        Step s = Scenario[m_step];
                        m_move = s.move;
                        m_tempoGoal = s.tempo;
                        m_intensityGoal = s.intensity;
                        if (s.move == 2)
                        {
                            mo.circleDir = s.dir;
                        }
                        m_moveEnd = t + s.seconds * Mathf.Max(0.3f, Plugin.MoveSeconds.Value / 6f);
                    }
                    else
                    {
                        int next;
                        do { next = UnityEngine.Random.Range(0, 4); } while (next == m_move);
                        m_move = next;
                        float len = Plugin.MoveSeconds.Value;
                        m_moveEnd = t + UnityEngine.Random.Range(len * 0.7f, len * 1.4f);
                        if (m_move == 2 && UnityEngine.Random.value > 0.5f)
                        {
                            mo.circleDir = -mo.circleDir;
                        }
                        m_tempoGoal = 1f;
                        m_intensityGoal = 1f;
                    }
                }

                float tb, ts, tc;
                switch (m_move)
                {
                    case 1: tb = 0.3f; ts = 1f; tc = 0f; break;
                    case 2: tb = 0.2f; ts = 0f; tc = 1f; break;
                    case 3: tb = 0.7f; ts = 0.6f; tc = 0.4f; break;
                    default: tb = 1f; ts = 0f; tc = 0f; break;
                }
                if (GrindingNow)
                {
                    // Out of breath, or holding him back: she sits on him and grinds, whatever the rhythm has her do.
                    tb = 0.1f;
                    ts = 1f;
                    tc = 0.55f;
                }
                // One move melts into the next instead of changing at a constant rate and stopping with a corner.
                float blend = 1f - Mathf.Exp(-dt / MoveMelt);
                mo.bounce = Mathf.Lerp(mo.bounce, tb, blend);
                mo.sway = Mathf.Lerp(mo.sway, ts, blend);
                mo.circle = Mathf.Lerp(mo.circle, tc, blend);

                // Eases off now and then and settles a little deeper: by the script in Scenario, at random in Random.
                bool pausing = false;
                if (scenario)
                {
                    pausing = m_intensityGoal < 0.5f;
                }
                else if (shakeW > 0.99f)
                {
                    if (t >= m_nextPause)
                    {
                        m_pauseEnd = t + UnityEngine.Random.Range(1.2f, 2.2f);
                        m_nextPause = m_pauseEnd + UnityEngine.Random.Range(12f, 24f);
                    }
                    pausing = t < m_pauseEnd;
                }
                float intensityGoal = scenario ? m_intensityGoal : (pausing ? 0.12f : 1f);
                mo.intensity = Mathf.Lerp(mo.intensity, intensityGoal, 1f - Mathf.Exp(-dt / 0.7f));

                float wantLean = Mathf.PerlinNoise(t * 0.1f, m_seed + 7f);
                m_leanTarget = pausing ? Mathf.Max(wantLean, 0.8f) : wantLean;
                mo.leanExtra = Mathf.Lerp(mo.leanExtra, Plugin.LeanExtra.Value * m_leanTarget, 1f - Mathf.Exp(-dt / 1f));
            }
            else
            {
                // Steady: one plain bounce. Coming from another rhythm, whatever she was doing melts into it.
                float blend = 1f - Mathf.Exp(-dt / MoveMelt);
                // (Out of breath, or holding him back, she sits on him and grinds: Body.cs.)
                bool grind = GrindingNow;
                mo.bounce = Mathf.Lerp(mo.bounce, grind ? 0.1f : 1f, blend);
                mo.sway = Mathf.Lerp(mo.sway, grind ? 1f : 0f, blend);
                mo.circle = Mathf.Lerp(mo.circle, grind ? 0.55f : 0f, blend);
                mo.intensity = Mathf.Lerp(mo.intensity, 1f, blend);
                mo.leanExtra = Mathf.Lerp(mo.leanExtra, 0f, blend);
                m_tempoGoal = 1f;
            }

            // The tempo glides to the one the step wants, and so does the speed that is set: neither changes at once.
            m_tempoNow = Mathf.Lerp(m_tempoNow, m_tempoGoal, 1f - Mathf.Exp(-dt / 1.8f));
            float tempo = m_tempoNow;
            float rate = Plugin.RideFrequency.Value;
            // (While she leads, how fast she goes is hers: the strokes a second that make the speed of him in her that
            // she likes, Lead.cs. The rhythm's moves, her body and the quiet after a climax still have their say on it.)
            bool led = m_ride && Leading;
            if (led)
            {
                rate = m_leadRate;
            }
            else if (m_ride && Plugin.Alive(Plugin.RideBuild) && m_heroReady)
            {
                // (The more aroused she is, the faster she rides.)
                rate *= 1f + 0.2f * m_herWet;
            }
            m_rateNow = m_rateNow < 0f ? rate : Mathf.Lerp(m_rateNow, rate, 1f - Mathf.Exp(-dt / 0.6f));

            // Nobody keeps the same depth and speed for every bounce: both wander a little, slowly, and her weight moves from
            // one leg to the other.
            mo.jitter = 1f + 0.14f * (Mathf.PerlinNoise(t * 0.55f, m_seed + 21f) * 2f - 1f);
            float lean = Mathf.Sin(t * 0.5f + m_seed) * 0.7f + (Mathf.PerlinNoise(t * 0.25f, m_seed + 31f) * 2f - 1f) * 0.3f;
            mo.weight = Mathf.Lerp(mo.weight, lean, 1f - Mathf.Exp(-dt * 3f));
            tempo *= 1f + 0.05f * (Mathf.PerlinNoise(t * 0.9f, m_seed + 41f) * 2f - 1f) + 0.1f * (Mathf.PerlinNoise(t * 0.12f, m_seed) * 2f - 1f);
            if (m_ride && Plugin.Alive(Plugin.RideWaves))
            {
                // (While she leads, only the quiet after a climax: the swell is her own rhythm's then.)
                tempo *= RideWave(t, !Leading);
            }
            if (m_ride)
            {
                // (Her body's say: the warm-up, how tired she is, whether it hurts, and his finish. Body.cs.)
                tempo *= m_bodyTempo;
            }

            // How fast she goes in the end, in strokes a second: what everything asks for, eased down toward the most a
            // woman on top really keeps up (so that nothing, her arousal, the climax or the preset, ever makes her race).
            // (The mood does not slow or hurry her rhythm when she leads: it says which side of her window she keeps to.)
            float asked = m_rateNow * tempo * (led ? 1f : mo.speed);
            if (m_ride)
            {
                float cap = Mathf.Clamp(Plugin.RideMaxSpeed.Value, 0.5f, 3f);
                // (Led, the most she goes is taken into the speed she rides at already: only held to it here.)
                asked = led ? Mathf.Min(asked, cap) : cap * (float)System.Math.Tanh(asked / cap);
            }
            mo.phase += dt * asked * 2f * Mathf.PI;
            m_speedNow = asked;
            if (mo.phase > 2000f * Mathf.PI)
            {
                mo.phase -= 2000f * Mathf.PI;
            }
        }
    }
}
