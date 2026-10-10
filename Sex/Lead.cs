using System;
using Assets._ReusableScripts.CuchiCuchi.AI.Emociones.Handlers;
using Assets._ReusableScripts.CuchiCuchi.Upgrades;
using UnityEngine;

namespace CMAScenes
{
    // She leads: her own rhythm and her own depth, by what the game itself has of her body.
    //
    // The game keeps a rhythm for every woman (Upgrades.Tempo): a speed of him in her that she likes best (her "sweet"
    // speed, which wanders slowly and is another one cold, near her climax and tender after it), a window about it, and
    // the speed he is going at in her now. Her pleasure from being taken comes by that: in her window it rises, out of
    // it it hardly does. While it is the player who thrusts, that speed is his to find. Here it is she who moves, so she
    // finds it herself.
    //
    // What the speed is, a run has shown: metres a second of him going in and out of her, near enough her strokes a
    // second times twice their length (1.37 strokes of 12.5 cm: 0.34 by that, 0.36 and 0.38 by the game; 0.95 strokes:
    // 0.24 and 0.22). She likes some 0.4 to 0.5 of it in her vagina and 0.1 to 0.25 in her anus. The game's own measure
    // is of the moment, though (at half a stroke a second it read anything from 0.04 to 0.28), and turning her tempo
    // after it, as was done first, ran her tempo to its end and stayed there. So she does not chase it: she works out
    // how many strokes a second, of the length her strokes have, make the speed she likes, and rides at that; where it
    // would take more strokes than she makes gladly, her strokes get longer first. The game's measure only trims it,
    // slowly and by little (should the game count him a little slower or faster in her than she does herself).
    // The mood and the tempo that are set say which side of her window she keeps to.
    //
    // Her depth: how deep she takes him is brought to just short of where it begins to hurt her. That is learned from
    // the game's pain of her (it rises while she is taken too deep), and let out again slowly once it has passed, so
    // that she goes deeper as she warms up.
    //
    // Her climax and how long it lasts are the game's own too, where its rhythm keeps them.
    //
    // Nothing of the game's is set here: it is all read. With the game's rhythm not to be had (another version of the
    // game, the system switched off), the ride goes by the speed and depth that are set, as before.
    public sealed partial class ShakeController
    {
        private Tempo.State m_tempoState;
        private Woman m_tempoWoman;
        private float m_tempoSeekAt, m_leadReadAt, m_leadLogAt, m_leadStaleSince;
        private int m_tempoFails;
        private bool m_tempoTold;
        // The game's rhythm of her, as last read: the speed he is going at in her, the one she likes best, her window,
        // whether he is in it, whether the reading is fresh, and whether she is coming or tender.
        private float m_gSpeed, m_gSweet, m_gMin, m_gMax, m_gTenderUntil;
        private bool m_gInBand, m_gFresh, m_gOrgasm, m_gOrgasmWas;
        // What she makes of it: how many strokes a second she rides at by her rhythm (none: she is not led), how much
        // longer than set her strokes are for it, and the deepest she takes him (of his length) without it hurting.
        private float m_leadRate, m_leadStroke = 1f, m_depthLimit = 1f;
        // When the game's rhythm of her was last read fresh, the speed she aims at, and how the game's measure of him
        // in her stands to her own count of it (averaged long: what she aims at is trimmed by that).
        private float m_leadKnownAt = -100f, m_leadWant, m_leadCalib = 1f, m_ownAvg, m_gameAvg, m_avgFor;
        // The strokes as she is posed (Ride.cs): how long one is set to be before there is no more room on him, how
        // much room there is, and how long they are right now with everything that shortens them.
        private float m_strokeSet = 0.09f, m_strokeRoom = 0.1f, m_strokeNow;
        // The deepest he has been in her in the last second or so, and her pain as it was at the reading before.
        private float m_depthPeak, m_painRaw, m_painRise;
        private bool m_painFresh, m_limitTold;
        // She is finding her own rhythm right now (the game's rhythm of her is being read, and it is hers to lead).
        // (Her rhythm is kept for a while after the game last noted it: it stops noting while she holds still, in a
        // rest or a climax, and she is not to fall back to the tempo that is set for that.)
        internal bool Leading => Plugin.RideLead.Value && m_leadRate > 0f && Time.unscaledTime - m_leadKnownAt < 15f;

        private void LeadReset()
        {
            m_tempoState = null;
            m_tempoWoman = null;
            m_tempoSeekAt = 0f;
            m_tempoFails = 0;
            m_leadStaleSince = 0f;
            m_gFresh = false;
            m_gOrgasm = false;
            m_gOrgasmWas = false;
            m_leadRate = 0f;
            m_leadKnownAt = -100f;
            m_leadWant = 0f;
            m_leadCalib = 1f;
            m_ownAvg = 0f;
            m_gameAvg = 0f;
            m_avgFor = 0f;
            m_leadStroke = 1f;
            m_depthLimit = 1f;
            m_depthPeak = 0f;
            m_painFresh = false;
            m_limitTold = false;
        }

        // Which side of her window she keeps to: the mood and the tempo that are set. (Her window is some half of the
        // speed she likes best below it and a sixth above.)
        private static float LeadBias()
        {
            float mood;
            switch ((Plugin.PresetName.Value ?? "Mine").ToLowerInvariant())
            {
                case "slow": mood = 0.8f; break;
                case "soft": mood = 0.9f; break;
                case "fast": mood = 1.12f; break;
                default: mood = 1f; break;
            }
            return mood * Mathf.Clamp(Mathf.Pow(Mathf.Max(0.1f, Plugin.RideFrequency.Value) / 1.1f, 0.4f), 0.75f, 1.3f);
        }

        // The game's record of her rhythm: found by her feelings (or by her character), else among all of them by which
        // one is hers.
        private Tempo.State TempoStateOf()
        {
            Woman w = m_rideWoman;
            float now = Time.unscaledTime;
            if (w == null || w.character == null)
            {
                return null;
            }
            if (m_tempoWoman != w)
            {
                m_tempoWoman = w;
                m_tempoState = null;
                m_tempoSeekAt = 0f;
                m_tempoFails = 0;
            }
            if (m_tempoState != null)
            {
                return m_tempoState;
            }
            if (now < m_tempoSeekAt || m_tempoFails > 4)
            {
                return null;
            }
            m_tempoSeekAt = now + 3f;
            try
            {
                if (!Tempo.Enabled)
                {
                    if (!m_tempoTold)
                    {
                        m_tempoTold = true;
                        Plugin.ModLog.LogInfo("Ride: lead: the game's rhythm (Tempo) is switched off in this game: she rides at the speed that is set.");
                    }
                    return null;
                }
                Tempo.State s = null;
                Component by = m_feel != null ? m_feel : (Component)w.character;
                s = Tempo.StateFor(by);
                string how = "by her feelings";
                if (s == null)
                {
                    var all = Tempo.s_states;
                    if (all != null)
                    {
                        var e = all.GetEnumerator();
                        while (e.MoveNext())
                        {
                            var pair = e.Current;
                            Transform root = pair.Key;
                            if (root != null && (w.character.transform == root || w.character.transform.IsChildOf(root) || root.IsChildOf(w.character.transform)))
                            {
                                s = pair.Value;
                                how = "by her place among the game's records ('" + root.name + "')";
                                break;
                            }
                        }
                    }
                }
                m_tempoState = s;
                if (s != null)
                {
                    Plugin.ModLog.LogInfo($"Ride: lead: the game's rhythm of her was found {how}: she likes {s.sweet:F3} best, window {s.winMin:F3} to {s.winMax:F3}, he goes at {s.speed:F3} now (in it {s.inBand}); coming {s.enOrgasmo}; ride {s.ride:F2} of {s.rideCap:F2}.");
                    LogHerDepths(w);
                }
                else if (!m_tempoTold)
                {
                    m_tempoTold = true;
                    Plugin.ModLog.LogInfo("Ride: lead: the game keeps no rhythm for her (yet); looked for again every few seconds.");
                }
                return s;
            }
            catch (Exception ex)
            {
                m_tempoFails++;
                Plugin.ModLog.LogInfo("Ride: lead: the game's rhythm of her could not be had: " + ex.Message);
                return null;
            }
        }

        // For the log, once: the depths the game itself has pleasure and pain come from for her (its own units), to hold
        // against what is learned here from her pain.
        private void LogHerDepths(Woman w)
        {
            try
            {
                var pleasure = w.character.GetComponentInChildren<PlacerPorPenetraciones>(true);
                var pain = w.character.GetComponentInChildren<DolorPorPenetracion>(true);
                if (pleasure == null && pain == null)
                {
                    return;
                }
                bool anal = m_holeAnal;
                string p = pleasure != null ? (anal ? pleasure.intervaloProfundidad_Anal : pleasure.intervaloProfundidad_Vaginal).min.ToString("F3") + " to " + (anal ? pleasure.intervaloProfundidad_Anal : pleasure.intervaloProfundidad_Vaginal).max.ToString("F3") : "?";
                string d = pain != null ? (anal ? pain.intervaloProfundidad_Anal : pain.intervaloProfundidad_Vaginal).min.ToString("F3") + " to " + (anal ? pain.intervaloProfundidad_Anal : pain.intervaloProfundidad_Vaginal).max.ToString("F3") : "?";
                Plugin.ModLog.LogInfo($"Ride: lead: by the game, depth gives her pleasure from {p} and pain from {d} (its own units; her {(anal ? "anus" : "vagina")}); he is {m_gameDepth:F3} deep by its record now.");
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogInfo("Ride: lead: the game's depths of her could not be read (" + ex.Message + ").");
            }
        }

        // Her pain as just read from the game (Body.cs, FeelStep): kept with how much it rose since the reading before.
        private void LeadNotePain(float pain)
        {
            m_painRise = pain - m_painRaw;
            m_painRaw = pain;
            m_painFresh = true;
        }

        // Once a frame while she rides (Body.cs): the game's rhythm of her is read, and her tempo, the length of her
        // strokes and her depth are set by it.
        private void LeadStep(float dt, bool riding)
        {
            float now = Time.unscaledTime;
            bool on = Plugin.RideLead.Value;
            if (!on)
            {
                m_gFresh = false;
                m_leadRate = 0f;
                m_leadStroke = Mathf.Lerp(m_leadStroke, 1f, 1f - Mathf.Exp(-dt / 1f));
                m_depthLimit = Mathf.Min(1f, m_depthLimit + dt / 10f);
                return;
            }

            // The game's rhythm of her, ten times a second.
            if (now >= m_leadReadAt)
            {
                m_leadReadAt = now + 0.1f;
                try
                {
                    Tempo.State s = TempoStateOf();
                    if (s != null)
                    {
                        m_gFresh = s.NoteFresh(0.6f);
                        m_gSpeed = s.speed;
                        m_gSweet = s.sweet;
                        m_gMin = s.winMin;
                        m_gMax = s.winMax;
                        m_gInBand = s.inBand;
                        m_gOrgasmWas = m_gOrgasm;
                        m_gOrgasm = s.enOrgasmo;
                        m_gTenderUntil = s.tenderUntil;
                    }
                    else
                    {
                        m_gFresh = false;
                    }
                }
                catch (Exception)
                {
                    m_gFresh = false;
                    m_tempoState = null;
                    m_tempoFails++;
                }
                // (Nothing noted by the game for a good while though he is in her: its record of her may be another one
                // by now. It is looked for again.)
                if (riding && !m_gFresh && m_tempoState != null)
                {
                    if (m_leadStaleSince <= 0f)
                    {
                        m_leadStaleSince = now;
                    }
                    else if (now - m_leadStaleSince > 6f)
                    {
                        m_leadStaleSince = 0f;
                        m_tempoState = null;
                        m_tempoSeekAt = 0f;
                    }
                }
                else
                {
                    m_leadStaleSince = 0f;
                }
            }

            // Her rhythm: so many strokes a second, of the length her strokes have, as make the speed of him in her that
            // she likes (a little under the one she likes best, well inside her window: the speed of a stroke rises and
            // falls about what it is on average).
            if (m_gFresh && m_gSweet > 1e-4f)
            {
                m_leadKnownAt = now;
            }
            if (riding && m_gSweet > 1e-4f && now - m_leadKnownAt < 15f)
            {
                float cap = Mathf.Clamp(Plugin.RideMaxSpeed.Value, 0.5f, 3f);
                float want = m_gSweet * 0.85f * LeadBias() * m_leadCalib;
                if (m_gMax > m_gMin && m_gMin > 0f)
                {
                    want = Mathf.Clamp(want, Mathf.Min(m_gMin * 1.12f, m_gSweet), Mathf.Max(m_gMax * 0.94f, m_gSweet * 0.9f));
                }
                m_leadWant = want;
                // (The most strokes a second she makes gladly: where it takes more than that, her strokes get longer
                // first, as far as there is room on him.)
                float gladly = Mathf.Min(1.35f, 0.85f * cap);
                float set = Mathf.Max(0.02f, m_strokeSet);
                float room = Mathf.Max(0.02f, m_strokeRoom);
                float need = want / (2f * gladly);
                float longer = Mathf.Min(set, room) >= need ? 1f : Mathf.Clamp(need / set, 1f, 1.6f);
                m_leadStroke = Mathf.Lerp(m_leadStroke, longer, 1f - Mathf.Exp(-dt / 2f));
                float len = Mathf.Max(0.025f, Mathf.Min(set * m_leadStroke, room));
                float rate = want / (2f * len);
                // (Gently while she warms up, however short her strokes are then.)
                float warm = Plugin.Alive(Plugin.RideWarmUp) ? Soft(m_warm) : 1f;
                rate = Mathf.Clamp(rate, 0.2f, Mathf.Lerp(Mathf.Min(0.7f, cap), cap, warm));
                m_leadRate = m_leadRate <= 0f ? rate : Mathf.Lerp(m_leadRate, rate, 1f - Mathf.Exp(-dt / 1.2f));

                // How the game's measure of him in her stands to her own count, over a good while of plain riding: what
                // she aims at is trimmed by that, within a quarter or so.
                if (m_gFresh && !m_resting && !m_edging && ClimaxNow() < 0.01f && m_strokeNow > 0.02f && m_speedNow > 0.15f)
                {
                    float k = 1f - Mathf.Exp(-dt / 6f);
                    float own = 2f * m_strokeNow * m_speedNow;
                    m_ownAvg = m_avgFor <= 0f ? own : Mathf.Lerp(m_ownAvg, own, k);
                    m_gameAvg = m_avgFor <= 0f ? m_gSpeed : Mathf.Lerp(m_gameAvg, m_gSpeed, k);
                    m_avgFor += dt;
                    if (m_avgFor > 12f && m_gameAvg > 0.02f && m_ownAvg > 0.02f)
                    {
                        m_leadCalib = Mathf.Lerp(m_leadCalib, Mathf.Clamp(m_ownAvg / m_gameAvg, 0.75f, 1.35f), 1f - Mathf.Exp(-dt / 10f));
                    }
                }
            }
            else
            {
                if (!riding)
                {
                    m_avgFor = 0f;
                }
                if (now - m_leadKnownAt >= 15f)
                {
                    m_leadRate = 0f;
                }
                m_leadStroke = Mathf.Lerp(m_leadStroke, 1f, 1f - Mathf.Exp(-dt / 1.5f));
            }

            // Her depth: just short of where it begins to hurt.
            if (riding)
            {
                m_depthPeak = Mathf.Max(m_rideIn, m_depthPeak - dt * 0.5f);
                if (m_painFresh && m_herKnown)
                {
                    m_painFresh = false;
                    // (Rising by more than two points of the game's hundred in a second, with him well in her.)
                    if (m_painRise > 0.004f && m_depthPeak > 0.25f)
                    {
                        float limit = Mathf.Max(0.25f, m_depthPeak - 0.05f);
                        if (limit < m_depthLimit)
                        {
                            m_depthLimit = limit;
                            if (!m_limitTold)
                            {
                                m_limitTold = true;
                                Notice(Loc.T("That deep hurts her: she takes a little less of him.", "Так глубоко ей больно — она берёт чуть меньше."));
                            }
                            Plugin.ModLog.LogInfo($"Ride: lead: her pain rose (to {m_painRaw:F2}) with him {m_depthPeak:F2} of his length in her: she takes no more than {m_depthLimit:F2} of him for now.");
                        }
                    }
                }
                if (m_herKnown && m_painRaw < 0.05f)
                {
                    // (Let out again: all of him in some forty seconds without pain.)
                    m_depthLimit = Mathf.Min(1f, m_depthLimit + dt / 40f);
                }
            }

            if (riding && now >= m_leadLogAt)
            {
                m_leadLogAt = now + 15f;
                Plugin.ModLog.LogInfo(Leading
                    ? $"Ride: lead: she likes {m_gSweet:F3} best (window {m_gMin:F3} to {m_gMax:F3}) and aims at {m_leadWant:F3}: {m_leadRate:F2} strokes a second of {Mathf.Min(m_strokeSet * m_leadStroke, m_strokeRoom) * 100f:F1} cm (set {m_strokeSet * 100f:F1}, room {m_strokeRoom * 100f:F1}, strokes x{m_leadStroke:F2}); she makes {m_speedNow:F2} a second of {m_strokeNow * 100f:F1} cm now, {2f * m_strokeNow * m_speedNow:F3} by her own count; the game has him at {m_gSpeed:F3} (in her window {m_gInBand}, noted {m_gFresh}); the game's against her own over a while x{m_leadCalib:F2}; she takes up to {m_depthLimit:F2} of him (pain {m_painRaw:F2}); coming {m_gOrgasm}, tender {(m_gTenderUntil > Time.time ? "yes" : "no")}."
                    : $"Ride: lead: the game's rhythm of her is {(m_tempoState != null ? "not being noted now" : "not to be had")}: she rides at the speed that is set; she takes up to {m_depthLimit:F2} of him (pain {m_painRaw:F2}).");
            }
        }

        // Did she begin to come just now, by the game's own rhythm of her? (False where that is not to be had: then her
        // feelings say, Body.cs.)
        private bool LeadSaysSheComes(out bool known)
        {
            known = Plugin.RideLead.Value && m_tempoState != null && m_gFresh;
            return known && m_gOrgasm && !m_gOrgasmWas;
        }

        // What she is doing about her rhythm, for the line on the screen (empty when she is not leading).
        private string LeadText()
        {
            if (!Leading || m_mount != 3)
            {
                return "";
            }
            if (m_gOrgasm)
            {
                return Loc.T("coming", "кончает");
            }
            string tender = m_gTenderUntil > Time.time ? Loc.T(", tender", ", нежная после оргазма") : "";
            return (m_gInBand ? Loc.T("in her rhythm", "в своём ритме") : m_gSpeed < m_gSweet ? Loc.T("speeding up to her rhythm", "разгоняется до своего ритма") : Loc.T("slowing down to her rhythm", "сбавляет до своего ритма")) + tender;
        }
    }
}
