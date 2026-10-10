using System;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.AI;
using UnityEngine;
using HisFeelings = Assets.TValle.BeachGirl.Characters.Male.AI.Runtime.Emociones.EmocionesMasculinas;

namespace CMAScenes
{
    // Her body in the ride: what she feels, how warmed up and how tired she is, and what she does about it.
    //
    // What the two of them feel is the game's own: its emotions of her (pleasure and pain, the ones its moans, its "too
    // deep" and her orgasm go by) and of him (his pleasure, at the top of which the game makes him come). They are only
    // read, never set. From them, and from what the ride has her do:
    //  - the warm-up: to begin with she takes him shallow and slowly, deeper and quicker as she warms up (the game itself
    //    has her hurt when she is taken deep at once);
    //  - she listens to her body: while it hurts she eases off, shallower and slower, until it has passed;
    //  - stamina: riding tires her, sooner on her feet than on her knees and sooner the faster she goes; tired she slows,
    //    and worn out she sits down on him and grinds for a while to get her breath back;
    //  - she breathes: her chest rises and falls, harder when she is tired or has just come;
    //  - his finish: left alone, or held back (as he nears coming she all but stops until he has cooled a little), or
    //    held back until she is nearly there herself;
    //  - her climax is the game's: when its pleasure of her reaches its top;
    //  - the tally of the session: how long, how many strokes, how deep, how many times each of them came.
    //
    // All of it works through the tempo and the depth of the ride, never through new places for her limbs: nothing here
    // can put her out of her pose.
    public sealed partial class ShakeController
    {
        private EmocionesFemeninas m_feel;
        private HisFeelings m_hisFeel;
        private Woman m_feelWoman;
        private Character m_feelHero;
        private float m_feelSeekAt, m_feelReadAt, m_feelLogAt;
        private int m_feelFails;
        // What they feel, 0..1, as the game has it (read a few times a second, eased): her pleasure and pain, his
        // pleasure; whether hers is at its top now and was at the last reading; and whether each is known at all.
        private float m_herPleasure, m_herPain, m_hisPleasure;
        private bool m_herKnown, m_hisKnown, m_herAtTop, m_herTopWas;

        // How warmed up she is, how tired, and how far she is easing off because it hurts (0..1 each).
        private float m_warm, m_tired, m_ease;
        private bool m_resting, m_edging, m_painTold, m_restTold, m_edgeTold;
        private float m_finishK = 1f;
        private float m_breathPhase, m_breathHard;
        // What all that makes of the ride: its tempo and its depth, as factors (eased).
        private float m_bodyTempo = 1f, m_bodyDepth = 1f;
        // When she gets off him by herself (after he came, if that is set); 0: not at all.
        private float m_endAt;

        // The tally of the ride that is on, or of the last one.
        private float m_statRiding, m_statDeepest;
        private int m_statStrokes, m_statHer, m_statHim, m_statTurn;
        private bool m_statAny;

        // She sits on him and grinds instead of riding: out of breath, or holding him back.
        internal bool GrindingNow => m_ride && (m_resting || m_edging);

        internal float BodyTempo => m_bodyTempo;
        internal float BodyDepth => m_bodyDepth;

        // When a ride begins.
        private void BodyReset()
        {
            m_warm = 0f;
            m_tired = 0f;
            m_ease = 0f;
            m_resting = false;
            m_edging = false;
            m_painTold = false;
            m_restTold = false;
            m_edgeTold = false;
            m_finishK = 1f;
            m_bodyTempo = 1f;
            m_bodyDepth = 1f;
            m_endAt = 0f;
            m_breathHard = 0f;
            m_herTopWas = false;
            m_statRiding = 0f;
            m_statDeepest = 0f;
            m_statStrokes = 0;
            m_statHer = 0;
            m_statHim = 0;
            m_statTurn = int.MinValue;
            m_statAny = false;
            LeadReset();
        }

        private static T FeelingsOf<T>(Character c) where T : Component
        {
            if (c == null)
            {
                return null;
            }
            T found = c.GetComponentInChildren<T>(true);
            // (Not looked for from the top of the scene: another character's would be found.)
            return found != null ? found : c.GetComponentInParent<T>();
        }

        // One of the game's emotions as a number from 0 to 1, and whether it is at its top. Below 0: not to be had.
        private static float Feeling(Emocion e, out bool atTop)
        {
            atTop = false;
            if (e == null)
            {
                return -1f;
            }
            float v = e.m_clampedValor;
            if (float.IsNaN(v) || float.IsInfinity(v))
            {
                return -1f;
            }
            atTop = e.m_valueAtMax;
            // (The game counts them from 0 to 100. Its "maxValueReal" is no top to go by: it is float.MaxValue, 3.4e38, and
            // dividing by it made every feeling zero in the first run of this.)
            float top = Emocion.maxValueReal;
            if (!(top > 0.5f) || top > 1000f)
            {
                top = v > 1.5f ? 100f : 1f;
            }
            return Mathf.Clamp01(v / top);
        }

        // Reads what they feel from the game, a few times a second.
        private void FeelStep()
        {
            float now = Time.unscaledTime;
            Woman w = m_rideWoman;
            Character hero = m_heroReady ? m_hero : null;
            bool anew = w != m_feelWoman || hero != m_feelHero;
            if (anew || ((m_feel == null || (m_hisFeel == null && hero != null)) && now >= m_feelSeekAt && m_feelFails < 4))
            {
                m_feelSeekAt = now + 10f;
                m_feelWoman = w;
                m_feelHero = hero;
                if (anew)
                {
                    m_feel = null;
                    m_hisFeel = null;
                    m_feelFails = 0;
                    m_herKnown = false;
                    m_hisKnown = false;
                    m_feelLogAt = now + 12f;
                }
                try
                {
                    if (m_feel == null && w != null && w.character != null)
                    {
                        m_feel = FeelingsOf<EmocionesFemeninas>(w.character);
                    }
                    if (m_hisFeel == null && hero != null)
                    {
                        m_hisFeel = FeelingsOf<HisFeelings>(hero);
                    }
                    if (anew && (w != null || hero != null))
                    {
                        Plugin.ModLog.LogInfo($"Ride: body: what she feels is {(m_feel != null ? "read from the game's own emotions of her" : "not to be had from the game (her arousal is told from her wetness alone)")}; what he feels is {(m_hisFeel != null ? "read from the game's emotions of him" : "not to be had")}.");
                    }
                }
                catch (Exception ex)
                {
                    m_feelFails++;
                    Plugin.ModLog.LogInfo("Ride: body: the game's emotions could not be looked for: " + ex.Message);
                }
            }
            if (now < m_feelReadAt)
            {
                return;
            }
            m_feelReadAt = now + 0.2f;
            try
            {
                m_herTopWas = m_herAtTop;
                if (m_feel != null)
                {
                    float p = Feeling(m_feel.m_placer, out bool top);
                    float d = Feeling(m_feel.m_dolor, out bool _);
                    m_herKnown = p >= 0f;
                    if (m_herKnown)
                    {
                        m_herPleasure = Mathf.Lerp(m_herPleasure, p, 0.5f);
                        m_herPain = Mathf.Lerp(m_herPain, Mathf.Max(0f, d), 0.5f);
                        m_herAtTop = top;
                        LeadNotePain(Mathf.Max(0f, d));
                    }
                }
                else
                {
                    m_herKnown = false;
                }
                if (!m_herKnown)
                {
                    m_herAtTop = false;
                    m_herPain = 0f;
                }
                if (m_hisFeel != null)
                {
                    float p = Feeling(m_hisFeel.m_placer, out bool _);
                    m_hisKnown = p >= 0f;
                    if (m_hisKnown)
                    {
                        m_hisPleasure = Mathf.Lerp(m_hisPleasure, p, 0.5f);
                    }
                }
                else
                {
                    m_hisKnown = false;
                }
                if (m_feelLogAt > 0f && now >= m_feelLogAt && m_mount == 3)
                {
                    // Once, some seconds into the riding: the numbers as the game has them, to check the reading by.
                    m_feelLogAt = 0f;
                    string hers = m_feel != null && m_feel.m_placer != null
                        ? $"her pleasure {m_feel.m_placer.m_clampedValor:F1} (real {m_feel.m_placer.m_valorReal:F1}, at its top {m_feel.m_placer.m_valueAtMax}), her pain {(m_feel.m_dolor != null ? m_feel.m_dolor.m_clampedValor.ToString("F1") : "?")}, her arousal {(m_feel.m_arousal != null ? m_feel.m_arousal.m_clampedValor.ToString("F1") : "?")}"
                        : "hers not read";
                    string his = m_hisFeel != null && m_hisFeel.m_placer != null ? $"his pleasure {m_hisFeel.m_placer.m_clampedValor:F1} (real {m_hisFeel.m_placer.m_valorReal:F1})" : "his not read";
                    Plugin.ModLog.LogInfo($"Ride: body: as the game has them (0 to 100): {hers}; {his}. Taken as: her pleasure {m_herPleasure:F2}, her pain {m_herPain:F2}, his pleasure {m_hisPleasure:F2}.");
                }
            }
            catch (Exception ex)
            {
                m_feel = null;
                m_hisFeel = null;
                m_herKnown = false;
                m_hisKnown = false;
                m_herAtTop = false;
                m_feelFails++;
                Plugin.ModLog.LogInfo("Ride: body: the game's emotions could not be read: " + ex.Message);
            }
        }

        // Has she reached her climax, by the game's own pleasure of her (the moment it gets to its top)? Without that
        // to go by: when she is as wet as the game has her get.
        private bool HerPeakNow()
        {
            // (By the game's rhythm of her first; her pleasure at its top says so as well, should the rhythm not.)
            if (LeadSaysSheComes(out bool _))
            {
                return true;
            }
            if (m_herKnown)
            {
                return m_herAtTop && !m_herTopWas;
            }
            return !m_holeAnal && m_herShine != null && m_herWet >= 0.97f;
        }

        // Once a frame, before the director: what her body makes of the ride.
        private void BodyStep(float dt)
        {
            float now = Time.unscaledTime;
            if (!m_ride || !m_toggled || m_switching)
            {
                m_bodyTempo = Mathf.Lerp(m_bodyTempo, 1f, 1f - Mathf.Exp(-dt / 0.5f));
                m_bodyDepth = Mathf.Lerp(m_bodyDepth, 1f, 1f - Mathf.Exp(-dt / 0.5f));
                m_resting = false;
                m_edging = false;
                m_squeezeNow = m_tenseNow = m_letGoNow = m_headBack = 0f;
                return;
            }
            FeelStep();
            ClimaxStep(dt);
            bool riding = m_mount == 3 && m_in && m_rideW > 0.5f;
            // (The quiet after a climax, hers or his: she is getting her breath back anyway.)
            bool calm = now - m_climaxAt < 16f;

            // The tally.
            if (riding)
            {
                m_statAny = true;
                m_statRiding += dt;
                m_statDeepest = Mathf.Max(m_statDeepest, m_rideIn * Mathf.Max(0.05f, m_penisLength));
                int turn = Mathf.FloorToInt(m_motion.phase / (2f * Mathf.PI));
                if (turn != m_statTurn)
                {
                    if (m_statTurn != int.MinValue && m_motion.bounce > 0.3f)
                    {
                        m_statStrokes++;
                    }
                    m_statTurn = turn;
                }
            }

            // Her own rhythm and depth, by the game's record of her body (Lead.cs).
            LeadStep(dt, riding);

            // The warm-up: in some half a minute of him in her, sooner the more she likes it.
            if (riding)
            {
                m_warm = Mathf.Min(1f, m_warm + dt * (0.45f + 0.55f * Mathf.Max(m_herWet, m_herKnown ? m_herPleasure : 0f)) / 26f);
            }
            float warm = Plugin.Alive(Plugin.RideWarmUp) ? Soft(m_warm) : 1f;

            // It hurts: she eases off at once, and comes back to it slowly once it has passed.
            float hurt = Plugin.Alive(Plugin.RideListen) && m_herKnown ? Mathf.Clamp01((m_herPain - 0.3f) / 0.3f) : 0f;
            m_ease = Mathf.MoveTowards(m_ease, hurt, dt / (hurt > m_ease ? 0.8f : 7f));
            if (m_ease > 0.5f && riding && !m_painTold)
            {
                m_painTold = true;
                Notice(Loc.T("It hurts her: she takes him shallower and slower for a while.", "Ей больно — она берёт его мельче и медленнее, пока не пройдёт."));
                Plugin.ModLog.LogInfo($"Ride: body: it hurts her (pain {m_herPain:F2} of 1): she eases off.");
            }

            // Stamina.
            if (!Plugin.Alive(Plugin.RideTires))
            {
                m_tired = 0f;
                m_resting = false;
            }
            else if (riding && !m_resting && !calm && !m_edging)
            {
                bool kneel = string.Equals(Plugin.RideLegs.Value, "Knees", StringComparison.OrdinalIgnoreCase);
                // (The work of it: how fast she goes, how much of it is up and down, and what carries her weight.)
                float effort = Mathf.Pow(Mathf.Max(0.05f, m_speedNow), 1.5f) * (0.5f + 0.5f * Mathf.Clamp01(m_motion.bounce)) * (kneel ? 0.6f : 1f);
                m_tired = Mathf.Min(1f, m_tired + dt * effort / (75f * Mathf.Clamp(Plugin.RideStamina.Value, 0.3f, 5f)));
                if (m_tired >= 1f)
                {
                    m_resting = true;
                    Plugin.ModLog.LogInfo($"Ride: body: she is out of breath after {m_statRiding:F0} s of riding ({m_statStrokes} strokes): she sits down on him and grinds until she has it back.");
                    if (!m_restTold)
                    {
                        m_restTold = true;
                        Notice(Loc.T("She is out of breath: she sits down on him and grinds for a while.", "Она запыхалась — садится на него и трётся, пока не отдышится."));
                    }
                }
            }
            else
            {
                m_tired = Mathf.Max(0f, m_tired - dt / (m_resting || calm ? 14f : 32f));
                if (m_resting && (m_tired <= 0.35f || m_mount < 2))
                {
                    m_resting = false;
                }
            }

            // His finish.
            float finish = 1f;
            string mode = Plugin.RideFinish.Value ?? "";
            bool edge = string.Equals(mode, "Edge", StringComparison.OrdinalIgnoreCase);
            bool together = string.Equals(mode, "Together", StringComparison.OrdinalIgnoreCase);
            if (riding && m_hisKnown && !calm && (edge || together))
            {
                // Held back: as he nears coming she all but stops, sitting on him, until he has cooled a little. Together:
                // only while she is not nearly there herself; once she is, she goes all out, and he comes with her.
                bool herNear = together && m_herKnown && (m_herPleasure > 0.85f || m_herAtTop);
                if (!m_edging && m_hisPleasure > 0.86f && !herNear)
                {
                    m_edging = true;
                    Plugin.ModLog.LogInfo($"Ride: body: he is near coming (his pleasure {m_hisPleasure:F2}, hers {m_herPleasure:F2}): she holds him back.");
                    if (!m_edgeTold)
                    {
                        m_edgeTold = true;
                        Notice(together
                            ? Loc.T("He is close: she holds him back until she is there too.", "Он близко — она придерживает его, пока сама не дойдёт.")
                            : Loc.T("He is close: she holds him on the edge.", "Он близко — она держит его на грани."));
                    }
                }
                else if (m_edging && (m_hisPleasure < 0.62f || herNear))
                {
                    m_edging = false;
                }
                finish = m_edging ? (together ? 0.45f : 0.22f) : herNear && m_hisPleasure > 0.7f ? 1.2f : 1f;
            }
            else
            {
                m_edging = false;
            }
            m_finishK = Mathf.Lerp(m_finishK, finish, 1f - Mathf.Exp(-dt / 0.8f));

            // She gets off him by herself, a while after he came.
            if (m_endAt > 0f && now >= m_endAt)
            {
                m_endAt = 0f;
                if (Riding)
                {
                    Notice(Loc.T("He came: she gets off him.", "Он кончил — она слезает с него."));
                    SetActive(false, true);
                    return;
                }
            }

            // Breathing: harder when she is tired, and hardest right after a climax.
            float after = now - m_climaxAt;
            float hard = Mathf.Max(Soft(m_tired) * 0.8f, after >= 0f && after < 25f ? 1f - after / 25f : 0f);
            m_breathHard = Mathf.Lerp(m_breathHard, hard, 1f - Mathf.Exp(-dt / 1.5f));
            m_breathPhase += dt * 2f * Mathf.PI * Mathf.Lerp(0.27f, 0.8f, m_breathHard);
            if (m_breathPhase > 200f * Mathf.PI)
            {
                m_breathPhase -= 200f * Mathf.PI;
            }

            float depth = Mathf.Min(Mathf.Lerp(0.55f, 1f, warm), Mathf.Lerp(1f, 0.6f, m_ease));
            // (No deeper than where it begins to hurt her, as she has learned it: of his length, against the depth set.)
            depth = Mathf.Min(depth, m_depthLimit / Mathf.Clamp(Plugin.RideDepth.Value, 0.2f, 1f));
            // (While she leads, how fast she goes is hers to find by her rhythm: the slow start of the warm-up is left to that.)
            float tempo = (Leading ? 1f : Mathf.Lerp(0.78f, 1f, warm)) * Mathf.Lerp(1f, 0.7f, m_ease) * Mathf.Lerp(1f, 0.74f, Soft(m_tired)) * (m_resting ? 0.6f : 1f) * m_finishK;
            m_bodyDepth = Mathf.Lerp(m_bodyDepth, depth, 1f - Mathf.Exp(-dt / 1.2f));
            m_bodyTempo = Mathf.Lerp(m_bodyTempo, tempo, 1f - Mathf.Exp(-dt / 0.8f));
        }

        // How far her chest is lifted by her breath now, in degrees (her upper back is turned by it).
        private float Breath()
        {
            if (!Plugin.Alive(Plugin.RideBreath))
            {
                return 0f;
            }
            // (In is quicker than out.)
            float s = Mathf.Sin(m_breathPhase);
            float wave = s > 0f ? Mathf.Sqrt(s) : -s * s;
            return Mathf.Lerp(0.45f, 2.1f, m_breathHard) * wave;
        }

        // How far his hips come up to meet her now, in metres: up as she comes down, in time with her stroke; and held
        // up into her for as long as a climax lasts. (HeroExtra.cs puts it on his skeleton, with the wheel's own lift,
        // and no more than about 10 cm: further, his shoulders and heels would come off what he lies on.)
        private float HeroMeets()
        {
            float amp = HeroMeetsAmp();
            if (amp <= 0.0005f || !m_ride || !m_toggled || m_mount != 3 || !m_in)
            {
                return 0f;
            }
            float climax = ClimaxNow();
            // (His hips are highest as she is lowest. Asked for a little early: they follow smoothly, a moment late.)
            float pulse = 0.5f * (1f - Mathf.Sin(m_motion.phase + 0.8f));
            pulse *= pulse;
            return amp * (pulse * HeroMeetsMoving() * (1f - climax) + 0.8f * climax);
        }

        private float HeroMeetsAmp()
        {
            float amp = Mathf.Clamp(Plugin.RideHeMeets.Value, 0f, 0.09f);
            if (m_holdW > 0.01f)
            {
                // (Holding her, it is he who moves her: his hips do the stroke.)
                amp = Mathf.Lerp(amp, Mathf.Clamp(amp * 1.5f, 0.075f, ThrustCap), m_holdW);
            }
            return amp;
        }

        private float HeroMeetsMoving()
        {
            return m_rideW * m_shakeW * Mathf.Clamp01(m_motion.bounce + 0.3f) * Mathf.Clamp01(m_motion.intensity);
        }

        // How much higher she sits on him (Mount.cs) to leave room for his hips to come up into her at the bottom of her
        // stroke: about what they come up by (as eased, they come up by some four fifths of what is asked). So his thrust
        // takes him deeper into her rather than lifting her with him, the two of them bobbing together and nothing showing.
        // Held by him, half of it: the rest of his lift carries her up, as it is he who moves her.
        internal float HeroMeetsRoom()
        {
            float amp = HeroMeetsAmp();
            if (amp <= 0.0005f || !m_ride || !m_toggled || m_mount != 3 || !m_in || !m_heroReady)
            {
                return 0f;
            }
            return 0.8f * amp * HeroMeetsMoving() * (1f - 0.5f * m_holdW);
        }

        // He came (Ride.cs, HeroCame): counted, and she gets off him after a while if that is set.
        private void BodyHeCame(bool inHer)
        {
            m_statHim++;
            if (inHer && Plugin.RideEndAfter.Value)
            {
                m_endAt = Time.unscaledTime + ClimaxLasts + 9f;
            }
        }

        private void BodySheCame()
        {
            m_statHer++;
            // (It takes it out of her, and lets her get her breath back in the quiet after.)
            m_tired = Mathf.Min(1f, m_tired + 0.15f);
        }

        // The tally of the ride, in a line (empty before the first one).
        internal string SessionText()
        {
            if (!m_statAny)
            {
                return "";
            }
            int sec = Mathf.RoundToInt(m_statRiding);
            return Loc.T("on him ", "на нём ") + (sec / 60) + ":" + (sec % 60).ToString("00")
                + "  ·  " + Loc.T("strokes: ", "толчков: ") + m_statStrokes
                + "  ·  " + Loc.T("deepest: ", "глубже всего: ") + (m_statDeepest * 100f).ToString("0") + Loc.T(" cm", " см")
                + "  ·  " + Loc.T("she came: ", "она кончила: ") + m_statHer
                + "  ·  " + Loc.T("he came: ", "он кончил: ") + m_statHim;
        }

        // What she feels and how she is, for the line on the screen and the window (empty when nothing is known).
        internal string BodyText()
        {
            var sb = new System.Text.StringBuilder(64);
            if (m_herKnown)
            {
                sb.Append(Loc.T("her pleasure ", "её удовольствие ")).Append(Mathf.RoundToInt(m_herPleasure * 100f)).Append('%');
            }
            if (m_hisKnown)
            {
                sb.Append(sb.Length > 0 ? "  ·  " : "").Append(Loc.T("his ", "его ")).Append(Mathf.RoundToInt(m_hisPleasure * 100f)).Append('%');
            }
            if (Plugin.Alive(Plugin.RideTires) && m_tired > 0.05f)
            {
                sb.Append(sb.Length > 0 ? "  ·  " : "").Append(m_resting ? Loc.T("getting her breath back", "переводит дух") : Loc.T("tired ", "устала на ") + Mathf.RoundToInt(m_tired * 100f) + "%");
            }
            string lead = LeadText();
            if (lead.Length > 0 && !m_resting && !m_edging)
            {
                sb.Append(sb.Length > 0 ? "  ·  " : "").Append(lead);
            }
            if (m_edging)
            {
                sb.Append(sb.Length > 0 ? "  ·  " : "").Append(Loc.T("holding him back", "придерживает его"));
            }
            else if (Plugin.Alive(Plugin.RideWarmUp) && m_warm < 0.95f && m_mount == 3)
            {
                sb.Append(sb.Length > 0 ? "  ·  " : "").Append(Loc.T("warming up", "разогревается"));
            }
            if (m_ease > 0.3f)
            {
                sb.Append(sb.Length > 0 ? "  ·  " : "").Append(Loc.T("it hurts: easing off", "ей больно: сбавила"));
            }
            return sb.ToString();
        }
    }
}
