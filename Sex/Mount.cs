using System;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;
using GameHole = Assets._ReusableScripts.CuchiCuchi.PhysicsAndBonesScripts.BoneStretchedChain;
using GamePenis = Assets._ReusableScripts.CuchiCuchi.Penis;
using VagInformer = Assets._ReusableScripts.CuchiCuchi.Dependentes.Characters.FemaleCharPenetrationInformer;

namespace CMAScenes
{
    // How she gets onto him and stays on him.
    //
    // She is placed by her vagina on his penis, not by a squat of so many centimetres: her feet stay planted on the floor
    // either side of him, and her hips are carried so that her vagina is on the line of his penis at the height the ride
    // asks for. She comes over him above his tip, lowers herself onto it, and only when he is in her does she sit down
    // on him and begin to move. If he slips out she stops, comes up over his tip and takes him in again.
    //
    // Whether he is in her is the game's own say: its record of her vagina (the one its moans and its "too deep" go by)
    // tells when it is entered, and by what. Where that record is not found, or never says so while he plainly is in her,
    // it is told from where his penis is against her vagina.
    public sealed partial class ShakeController
    {
        // 0: not over him. 1: over him, above his tip. 2: taking him in. 3: riding.
        private int m_mount;
        private float m_mountAt;
        // How high her vagina is above his root, the stroke left out: where it is, where it is going, and how fast.
        private float m_sBase, m_sGoalNow, m_sVel;
        // How much of the stroke there is: none until he is in her and she has sat down.
        private float m_rideW;
        // How surely he is in her: it takes a moment to say so and a longer one to say he is out.
        private float m_inW;
        private bool m_in;
        private float m_outSince;
        private int m_mountTries;
        // The lowest she sits and the highest she rises, above his root: she does not come off his tip.
        private float m_sSeat = 0.02f, m_sTop = 0.15f;
        // How much lower than that the top of her stroke is kept for this hole of hers: learned, from his having slipped
        // out of it and from how little of him was left in it at the top of a stroke. And the least of him that was in
        // her, by the game's own entrance as she is drawn, since that was last looked at.
        private float m_topCut, m_tipInLow = float.MaxValue, m_tipInAt;
        private int m_slips;
        // His length as he is while free, and how far his root (as it was found) is under his skin.
        private float m_penisLenFree;
        private float m_penisSkin = 0.015f;
        private float m_skinAt;
        // How far her vagina was from the line of his penis, across the floor, the frame before.
        private float m_alignErr = 1f;
        private Vector3 m_vulvaLast;
        private bool m_vulvaLastValid;
        // Where her vagina is without the stroke, followed slowly: her feet are planted about it and he is laid under it.
        private Vector3 m_vulvaHome;
        private bool m_vulvaHomeValid;
        // How far she moves across the floor to be on his line exactly.
        private Vector3 m_mountCorr;
        // Where the root of his penis rests once he lies (known while he is still being laid down).
        private Vector3 m_penisGoalBase;
        private bool m_penisGoalValid;

        // The game's own record of her entrance (her vagina, or her anus when the ride is set to that), the hole itself,
        // and his penis as the game has it.
        private bool m_holeAnal;

        // How far past her entrance the aim is, away from her other hole (on the bone the entrance is kept on).
        private Vector3 m_pastLocal;
        private float m_aimLogAt;

        private GameHole m_vagHole;
        private GamePenis m_heroPen;
        private int m_mountLogs;
        private float m_hardAt, m_hardLogAt;
        private bool m_hardLogged, m_hardNoticed;
        private bool m_waitLogged;
        private bool m_closedNoticed;
        private float m_tryLogAt;
        // When the game was last opening her to him (its "a penis is trying").
        private float m_tryingAt = -10f;

        // The little circle of her hips while she feels for the way in (across the floor; added where she is placed).
        internal Vector3 m_searchOff;
        private float m_searchAmp, m_searchPhase;
        private float m_notReadySince;
        private bool m_tryAnyway, m_notReadyLogged, m_notReadyNoticed;
        private const float NotReadyPatience = 20f;

        private void MountReset()
        {
            m_mount = 0;
            m_notReadySince = 0f;
            m_tryAnyway = false;
            m_notReadyLogged = false;
            m_notReadyNoticed = false;
            m_searchOff = Vector3.zero;
            m_mountAt = 0f;
            m_rideW = 0f;
            m_inW = 0f;
            m_in = false;
            m_outSince = 0f;
            m_topCut = 0f;
            m_slips = 0;
            m_tipInLow = float.MaxValue;
            m_tipInAt = 0f;
            m_mountTries = 0;
            m_wrongTries = 0;
            m_wrongSince = 0f;
            m_wrongNoticed = false;
            m_alignErr = 1f;
            m_vulvaLastValid = false;
            m_vulvaHomeValid = false;
            m_mountCorr = Vector3.zero;
            if (!m_heroReady)
            {
                // (Started again with him still under her, she keeps the height she has and goes on from there.)
                m_sVel = 0f;
                m_sBase = m_sGoalNow = 0f;
            }
            m_penisLenFree = 0f;
            m_mountLogs = 0;
            m_hardLogged = false;
            m_hardNoticed = false;
            m_waitLogged = false;
            m_closedNoticed = false;
            m_triesNoticed = false;
            m_failsInRow = 0;
            m_tryLogs = 0;
            m_tryingAt = -10f;
            m_hardLogAt = 0f;
            m_reportAt = 0f;
        }

        // What is going on in the ride, in a few words, for the window.
        internal string RideStatusText()
        {
            if (!m_heroReady)
            {
                return Loc.T("he is lying down", "он ложится");
            }
            string hole = m_holeAnal ? Loc.T("anal", "анал") : Loc.T("vaginal", "вагина");
            string what;
            if (CondomWantsHerUp)
            {
                what = m_condom == 1 ? Loc.T("she waits over him: the condom lies on his belly", "она ждёт над ним: презерватив лежит у него на животе")
                    : m_condom == 2 ? Loc.T("she waits over him: the condom is in his hand", "она ждёт над ним: презерватив у него в руке")
                    : m_condom == 3 ? Loc.T("the condom is going on", "презерватив надевается") : Loc.T("the condom is coming off", "презерватив снимается");
            }
            else
            {
                what = m_mount == 3 ? (GrindingNow ? Loc.T("she sits on him and grinds", "она сидит на нём и трётся") : Loc.T("she is riding him", "она скачет на нём"))
                    : m_mount == 2 ? Loc.T("she is taking him in", "она насаживается")
                    : Loc.T("she is over him, getting ready", "она над ним, готовится");
            }
            return what + "  ·  " + hole + (m_condom == 4 ? "  ·  " + Loc.T("condom on", "в презервативе") : "");
        }

        private void MountLog(string text)
        {
            if (m_mountLogs < 80)
            {
                m_mountLogs++;
                Plugin.ModLog.LogInfo("Ride: " + text);
            }
        }

        // How long she holds still, as far down as she goes, before she comes up and tries again (it was a setting, which
        // was cut to this whatever it said).
        private const float EnterPatience = 3f;
        // Every woman's hole is its own: some give at a touch, some want her pressed on them for longer and a little
        // further down, and his penis gives under it (it bends away) by its own elasticity. What each took is kept for the
        // session by woman and hole: the next time it is begun with that, and every try that did not work asks for a little
        // more patience and a little more pressure, while one that worked brings it back toward what it took.
        private sealed class EntryLearn
        {
            public float patience = EnterPatience, extraSink;
            public int fails, wins;
        }
        private static readonly System.Collections.Generic.Dictionary<string, EntryLearn> s_entry = new System.Collections.Generic.Dictionary<string, EntryLearn>();

        private EntryLearn EntryOf()
        {
            string key = (m_rideWoman != null && m_rideWoman.name != null ? m_rideWoman.name : "?") + "|" + (m_holeAnal ? "anus" : "vagina");
            if (!s_entry.TryGetValue(key, out EntryLearn e))
            {
                e = new EntryLearn();
                s_entry[key] = e;
            }
            return e;
        }

        // The long line about taking him in is written every second only for the first few seconds of the first tries.
        private int m_tryLogs;
        private bool m_triesNoticed;
        // Tries that did not work since he was last in her (for the hint above).
        private int m_failsInRow;
        // Taking him in: how far past his tip she has come down, how high his tip stood before she touched it, and since
        // when she has been as far down as she goes.
        private float m_sink, m_tipFree, m_sinkFullAt;
        private const float MaxSink = 0.055f;
        // How far her pelvis is tipped to bring her hole in line with him (degrees, about the axis across her).
        private float m_holeTilt;

        // Her pelvis is tipped so that her hole opens straight down his shaft: its axis, as the game has it, is some way
        // off the upright (a quarter of a right angle, for her anus), and he goes in along it or not at all. Worked out
        // from her pelvis as the game has it this frame (before she is posed) and what the pose turns it by.
        private float HoleTilt(Vector3 right, float posed, float dt)
        {
            bool aiming = Plugin.RideAutoAim.Value && m_vagHole != null && m_heroReady && m_mount >= 1;
            if (!aiming)
            {
                m_holeTilt = Mathf.MoveTowards(m_holeTilt, 0f, 40f * dt);
                return m_holeTilt;
            }
            if (!m_in && m_mount <= 2 && TiltByMeasure(right, dt))
            {
                // (Closed loop on her canal as drawn, against his shaft: AimGame.cs.)
                return m_holeTilt;
            }
            if (Time.frameCount - m_gameAimFrame < 30)
            {
                // (Measured a moment ago: kept as it is until the next measurement.)
                return m_holeTilt;
            }
            if (!m_in && m_mount <= 2 && PenisNow(out Vector3 b, out Vector3 t))
            {
                try
                {
                    Vector3 opens = Vector3.ProjectOnPlane(m_vagHole.worldOutHoleDirection, right);
                    Vector3 shaftDown = Vector3.ProjectOnPlane(b - t, right);
                    if (opens.sqrMagnitude > 1e-4f && shaftDown.sqrMagnitude > 1e-4f)
                    {
                        float goal = Mathf.Clamp(Vector3.SignedAngle(opens, shaftDown, right) - posed, -28f, 28f);
                        m_holeTilt = Mathf.Lerp(m_holeTilt, goal, 1f - Mathf.Exp(-dt / 0.5f));
                    }
                }
                catch (Exception)
                {
                }
            }
            return m_holeTilt;
        }

        // Where the line of his penis is, across the floor, at a height above its root: as he really is while he is not in
        // her (she comes down onto his tip, wherever it points), and straight up from where his root rests once he is.
        private Vector3 MountLine(float s)
        {
            Vector3 rest = m_penisGoalBase;
            if (m_inW >= 0.999f || !PenisNow(out Vector3 b, out Vector3 t))
            {
                return rest;
            }
            float span = t.y - b.y;
            if (span < 0.03f)
            {
                // Not up at all: there is nothing to come down onto.
                return rest;
            }
            Vector3 free = Vector3.Lerp(b, t, Mathf.Clamp01((m_penisGoalBase.y + s - b.y) / span));
            return Vector3.Lerp(free, rest, m_inW);
        }

        // He has to be hard for her to take him in. The game is asked to keep him so while the ride lasts (its own setting
        // of how hard he is aimed to be), if that is switched on.
        private void KeepHard(float now)
        {
            if (m_heroPen == null || !Plugin.RideKeepHard.Value || now < m_hardAt)
            {
                return;
            }
            m_hardAt = now + 0.5f;
            try
            {
                float e = m_heroPen.erection;
                float aim = m_heroPen.m_ErectionTarget;
                if (!m_hardLogged)
                {
                    m_hardLogged = true;
                    m_hardLogAt = now + 4f;
                    Plugin.ModLog.LogInfo($"Ride: he is asked to stay hard for the ride (erection {e:F2}, aimed at {aim:F2}).");
                }
                else if (m_hardLogAt > 0f && now >= m_hardLogAt)
                {
                    m_hardLogAt = 0f;
                    Plugin.ModLog.LogInfo($"Ride: four seconds after asking: erection {e:F2}, aimed at {aim:F2}.");
                }
                // (The game counts it in per cent.)
                if (aim < 97f)
                {
                    m_heroPen.SetErectionTarget(100f);
                }
            }
            catch (Exception ex)
            {
                m_heroPen = null;
                Plugin.ModLog.LogWarning("He could not be asked to stay hard: " + ex.Message);
            }
        }

        // Runs once a frame, before she is placed: where she is in getting onto him, and from that how high her vagina is
        // to be above his root and how much of the stroke there is.
        private void MountStep(bool onHim, bool posing, float dt)
        {
            float now = Time.unscaledTime;
            if (m_heroReady && m_hero != null && m_holeAnal != AnalNow())
            {
                // The other hole was picked in the middle of the ride: she comes up off him and takes him in there.
                m_topCut = 0f;
                m_slips = 0;
                m_tipInLow = float.MaxValue;
                m_wrongTries = 0;
                m_wrongNoticed = false;
                MountCapture(m_hero);
                m_vulvaLastValid = false;
                m_mountTries = 0;
                if (m_mount >= 2)
                {
                    m_mount = 1;
                    m_mountAt = now;
                }
                MountLog($"the ride is set to her {(m_holeAnal ? "anus" : "vagina")} now: she comes up and takes him in there.");
            }
            // (The aim corrects itself at the end of the frame, on her as she is drawn: AimMeasure, AimGame.cs. Read here,
            // before she is posed, her bones are where the game's animation has her and her hole's ring is where the
            // physics left it: the two do not belong together.)
            bool have = PenisNow(out Vector3 b, out Vector3 t);
            float lenHome = (m_penisHomeTip - m_penisHomeBase).magnitude;
            if (m_penisLenFree < 0.05f)
            {
                m_penisLenFree = lenHome;
            }
            bool game = false, geo = false;
            bool inNow = onHim && m_vulvaLastValid && PenisInHer(m_vulvaLast, have, b, t, ref game, ref geo);
            m_inW = Mathf.MoveTowards(m_inW, inNow ? 1f : 0f, dt / (inNow ? 0.1f : 0.3f));
            if (m_inW >= 0.999f)
            {
                m_in = true;
            }
            else if (m_inW <= 0.001f)
            {
                m_in = false;
            }

            Vector3 dirNow = have ? (t - b).normalized : Vector3.up;
            float lenNow = have ? (t - b).magnitude : lenHome;
            // Standing up from him, and not shrunk: hard enough to be taken in.
            bool erect = have && dirNow.y > 0.8f && lenNow > 0.6f * Mathf.Max(lenHome, m_penisLenFree);
            if (erect && !m_in && m_mount <= 1)
            {
                m_penisLenFree = Mathf.Lerp(m_penisLenFree, lenNow, 1f - Mathf.Exp(-dt / 0.25f));
            }
            float len = Mathf.Clamp(m_penisLenFree, 0.08f, 0.35f);
            // (Less of him than is set while she warms up, and while it hurts: Body.cs.)
            float depthSet = Mathf.Clamp(Plugin.RideDepth.Value * m_bodyDepth, 0.2f, 1f);
            float seat = Mathf.Max(Mathf.Max(0.02f, m_penisSkin + 0.005f), len * (1f - depthSet));
            // The top of her stroke: his glans stays in her. More of him than that in her anus, whose ring lets go of
            // him sooner than her vagina does; and more again for a hole he has slipped out of (m_topCut).
            float top = Mathf.Max(seat, len - (m_holeAnal ? 0.055f : 0.035f) - m_topCut);
            // Riding, she sits higher by the room his hips come up into as he meets her (Body.cs, HeroMeetsRoom): at the
            // bottom of her stroke his thrust brings him the rest of the way into her. No more than half of what there is
            // of him to ride: the rest of the stroke is hers.
            float room = m_mount == 3 ? Mathf.Min(HeroMeetsRoom(), 0.5f * (top - seat)) : 0f;
            m_sSeat = seat + room;
            m_sTop = Mathf.Max(m_sSeat, top);
            // How high his tip is above where his root rests, as he is now.
            float tipS = have && m_penisGoalValid ? Mathf.Clamp(t.y - m_penisGoalBase.y, 0.03f, len + 0.03f) : len;
            float hover = len + 0.035f;

            // Turned round on him (the reverse, or back), he swings round under her: she comes up off him for that, and
            // goes down on him again when he lies the new way.
            float swing = Vector3.Angle(m_heroFwd, HeroHeadGoal());
            if (m_mount >= 2 && swing > 12f)
            {
                m_mount = 1;
                m_mountAt = now;
                MountLog("she is turned round on him: she comes up while he swings round under her.");
            }

            // He has gone into her other hole instead (the game says so): she comes up off him and comes down again aimed
            // further from it. After a few tries it is left at that.
            bool otherIn = OtherEntered();
            if (otherIn && m_mount >= 2 && m_wrongTries < 5)
            {
                if (m_wrongSince <= 0f)
                {
                    m_wrongSince = now;
                }
                else if (now - m_wrongSince > 0.4f)
                {
                    m_wrongSince = 0f;
                    m_wrongTries++;
                    MountCapture(m_hero);
                    m_vulvaLastValid = false;
                    m_mount = 1;
                    m_mountAt = now;
                    MountLog($"he went into her {(m_holeAnal ? "vagina" : "anus")} instead of her {(m_holeAnal ? "anus" : "vagina")} (wrong hole {m_wrongTries}); she comes up and comes down again aimed further from it.");
                    if (!m_wrongNoticed)
                    {
                        m_wrongNoticed = true;
                        Notice(Loc.T("Wrong hole: she comes up and tries again.", "Не туда: она приподнимается и пробует ещё раз."));
                    }
                }
            }
            else
            {
                m_wrongSince = 0f;
            }

            if (m_mount >= 2 && CondomWantsHerUp)
            {
                m_mount = 1;
                m_mountAt = now;
                MountLog("she comes up off him for the condom.");
            }

            if (!onHim || !posing)
            {
                if (m_mount != 0)
                {
                    m_mount = 0;
                    m_mountAt = now;
                }
                m_rideW = Mathf.MoveTowards(m_rideW, 0f, dt / 0.25f);
                m_searchOff = Vector3.zero;
                m_searchAmp = 0f;
                if (!onHim)
                {
                    // Nothing to be on yet: kept ready over where his tip will be.
                    m_sBase = m_sGoalNow = hover;
                    m_sVel = 0f;
                }
                // (Coming out of the pose she is lifted off him by the pose itself.)
                return;
            }
            KeepHard(now);

            // Does the game have his penis ready to go in (GamePenisReady)? While he looks hard and it does not, she waits
            // over him instead of sinking onto him in vain; should that go on too long (the game's word may be wrong for
            // some penis), she tries all the same.
            bool gameReady = GamePenisReady(out string notReady);
            if (gameReady)
            {
                if (m_notReadySince > 0f)
                {
                    MountLog($"the game has his penis ready to go in now, after {now - m_notReadySince:F0} s of waiting for it.");
                }
                m_notReadySince = 0f;
                m_tryAnyway = false;
                m_notReadyLogged = false;
            }
            else if (erect && m_mount <= 2)
            {
                if (m_notReadySince <= 0f)
                {
                    m_notReadySince = now;
                }
                if (!m_tryAnyway && now - m_notReadySince > NotReadyPatience)
                {
                    m_tryAnyway = true;
                    MountLog($"the game still does not have his penis ready after {NotReadyPatience:F0} s ({notReady}); she tries to take him in all the same.");
                }
            }
            bool readyEnough = gameReady || m_tryAnyway;

            float sGoal = hover;
            float pace = 0.5f;
            switch (m_mount)
            {
                case 0:
                    m_mount = 1;
                    m_mountAt = now;
                    if (m_sGoalNow <= 0.001f)
                    {
                        m_sBase = m_sGoalNow = hover;
                        m_sVel = 0f;
                    }
                    goto case 1;
                case 1:
                {
                    // Over him: above his tip, on his line.
                    // (Higher while a condom is put on him or taken off: there is room for his hand.)
                    bool condom = CondomWantsHerUp;
                    // (And higher while he is still in her other hole: he has to come out of it first.)
                    sGoal = (erect ? Mathf.Max(tipS, len * 0.7f) : len) + (condom ? 0.16f : otherIn ? 0.1f : 0.035f);
                    if (erect && now >= m_skinAt && m_heroPen != null)
                    {
                        // How far under his skin the root that was found is: she sits down to his skin, not into it.
                        m_skinAt = now + 0.25f;
                        try
                        {
                            Transform skin = m_heroPen.skinSurfaceTransform;
                            if (skin != null)
                            {
                                m_penisSkin = Mathf.Lerp(m_penisSkin, Mathf.Clamp(Vector3.Dot(skin.position - b, dirNow), 0f, 0.06f), 0.5f);
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                    bool there = m_penisRestValid && m_heroW >= 0.99f && m_poseW >= 0.97f && Mathf.Abs(m_sBase - sGoal) < 0.012f && m_alignErr < 0.015f && swing < 4f;
                    // The first time a moment longer: where his root really is, lying, is measured meanwhile.
                    float wait = m_mountTries == 0 ? 1.0f : 0.5f;
                    if (erect && !readyEnough && !condom && !otherIn)
                    {
                        if (!m_notReadyLogged)
                        {
                            m_notReadyLogged = true;
                            MountLog($"his penis stands up as drawn, but the game does not have it ready to go in ({notReady}); she waits over him (up to {NotReadyPatience:F0} s, then she tries all the same).");
                        }
                        if (!m_notReadyNoticed && now - m_notReadySince > 1.5f)
                        {
                            m_notReadyNoticed = true;
                            Notice(Plugin.RideKeepHard.Value
                                ? Loc.T("He is not hard enough for the game yet: she waits over him.", "Он ещё недостаточно твёрд для игры — она ждёт над ним.")
                                : Loc.T("He is not hard enough for the game yet: she waits over him (\"Keeps him hard\" makes him).", "Он ещё недостаточно твёрд для игры — она ждёт над ним (включите «Не даёт ему опасть»)."));
                        }
                    }
                    if (there && erect && readyEnough && !condom && !otherIn && now - m_mountAt >= wait)
                    {
                        m_mount = 2;
                        m_mountAt = now;
                        m_waitLogged = false;
                        m_tryLogs = 0;
                        m_sink = 0f;
                        m_sinkFullAt = 0f;
                        m_tipFree = tipS;
                        MountLog($"she is over his tip ({len * 100f:F0} cm long, his root {m_penisSkin * 100f:F1} cm under his skin, {m_alignErr * 100f:F1} cm off his line) and takes him in, try {m_mountTries + 1}.");
                    }
                    else if (now - m_mountAt > 4f && !m_waitLogged)
                    {
                        // What she is waiting for, once, should it take long.
                        m_waitLogged = true;
                        MountLog($"still over him after 4 s: he lies {m_heroW:F2} (his place known {m_penisRestValid}), her pose {m_poseW:F2}, her height {(m_sBase - sGoal) * 100f:F1} cm from where it is going, {m_alignErr * 100f:F1} cm off his line (she moved {m_mountCorr.magnitude * 100f:F1} cm), his penis up {erect} (pointing {dirNow.ToString("F2")}, {lenNow * 100f:F0} cm).");
                    }
                    if (!erect && now - m_mountAt > 1.5f && !m_hardNoticed)
                    {
                        m_hardNoticed = true;
                        Notice(Loc.T("He is not hard yet: she waits over him.", "Он ещё не возбуждён — она ждёт над ним."));
                        MountLog($"his penis is not up (pointing {dirNow.ToString("F2")}, {lenNow * 100f:F0} cm of {lenHome * 100f:F0}); she waits over him.");
                    }
                    break;
                }
                case 2:
                {
                    // Taking him in: down onto his tip, and from there down to where she sits once he is in her.
                    pace = Mathf.Clamp(Plugin.RideEnterSpeed.Value, 0.02f, 0.4f);
                    float held = now - m_mountAt;
                    if (m_in)
                    {
                        sGoal = m_sSeat;
                        if (m_sBase - m_sSeat < 0.008f)
                        {
                            EntryLearn won = EntryOf();
                            won.wins++;
                            // (What it took, with some to spare: the next time she waits for about that, and presses for about that.)
                            won.patience = Mathf.Clamp(Mathf.Lerp(won.patience, held * 1.8f + 1.5f, 0.5f), EnterPatience, 14f);
                            won.extraSink = Mathf.Clamp(Mathf.Lerp(won.extraSink, Mathf.Max(0f, m_sink - 0.03f), 0.5f), 0f, 0.03f);
                            m_mount = 3;
                            m_mountAt = now;
                            m_failsInRow = 0;
                            m_reportAt = now + 5f;
                            MountLog($"he is in her ({(m_vagInformer != null ? $"by the game's own record: {m_gameDepth * 100f:F1} deep, opened {m_gameOpen * 100f:F1}" : "by where his penis is")}) and she has sat down on him after {held:F1} s; the ride begins. She sits {m_sSeat * 100f:F1} cm above his root and rises to {m_sTop * 100f:F1} cm.");
                        }
                    }
                    else if (!readyEnough)
                    {
                        // The game has put his penis out of reach (gone soft, or blocked): she comes up over his tip and
                        // waits there for it. Not a failed try: nothing she does would get him in.
                        m_mount = 1;
                        m_mountAt = now;
                        MountLog($"the game no longer has his penis ready to go in ({notReady}); she comes up over his tip and waits (not counted as a try).");
                    }
                    else
                    {
                        // She comes down onto his tip and goes on down, slowly, until he is really in her: until the game
                        // itself has her opened round him, by half his glans at least. No further than his glans is long:
                        // if he is not in her by then he has gone past her hole, and she comes up to try again. While the
                        // game is opening her to him she holds still. (His tip is taken where it stood before she
                        // touched it: pressed on, he bends, and she would only follow him down.)
                        // (No further than his glans is long past where his tip is taken to meet her entrance; but while, as
                        // she is drawn, his tip is not yet in past the game's own entrance and is on its canal, she goes on
                        // down to it, a few centimetres more at most.)
                        EntryLearn learn = EntryOf();
                        float maxSink = MaxSink + learn.extraSink;
                        if (m_gameAimValid && Time.frameCount - m_gameAimFrame <= 3 && m_gameTipIn < 0.015f && m_gameTipOff < 0.025f)
                        {
                            maxSink = MaxSink + learn.extraSink + Mathf.Clamp(0.015f - m_gameTipIn, 0f, 0.04f);
                        }
                        bool trying = GameTrying();
                        if (trying)
                        {
                            m_tryingAt = now;
                        }
                        if (m_sBase - (m_tipFree + 0.01f) < 0.012f && !trying)
                        {
                            m_sink = Mathf.Min(Mathf.Max(maxSink, m_sink), m_sink + pace * dt);
                            if (m_sink >= maxSink - 0.0005f && m_sinkFullAt <= 0f)
                            {
                                m_sinkFullAt = now;
                            }
                        }
                        sGoal = Mathf.Max(m_sSeat, m_tipFree + 0.01f - m_sink);
                        if (now >= m_tryLogAt && m_tryLogs < 4 && m_mountTries < 3)
                        {
                            m_tryLogAt = now + 1f;
                            m_tryLogs++;
                            MountLog($"taking him in, {held:F0} s: her entrance is {(m_sBase - tipS) * 100f:F1} cm above his tip; {TryState()}.");
                        }
                        // (While the game is opening her to him, or was a moment ago, she is not taken off him for his
                        // penis bending under her or for being a little off his line: he is on his way in. Seen in a
                        // run: "a penis is trying True", and the try given up the same second for "his penis went down
                        // or aside", which is only what a penis does when it is pressed on. Not for ever, though.)
                        bool opening = (trying || now - m_tryingAt < 0.6f) && held < 12f;
                        // (His penis gives under her: pressed on it bends away from the upright, which is only its elasticity and no
                        // reason to give up. Seen in a run: tries given up over and over for "his penis went down or aside" with
                        // her on his tip and the game not yet opening her, and in at the fifth or the seventh. It is a miss only
                        // when it has gone a long way down, or gone soft.)
                        bool bent = !have || dirNow.y < 0.45f || lenNow < 0.4f * Mathf.Max(lenHome, m_penisLenFree);
                        bool aside = (bent || m_alignErr > 0.06f) && !opening;
                        // (While the game is opening her to him she holds still on his tip for as long as that takes.)
                        bool spent = m_sinkFullAt > 0f && now - m_sinkFullAt > (trying ? 16f : learn.patience);
                        if ((aside && held > 0.6f) || spent || held > 40f)
                        {
                            string why = aside ? (bent ? "his penis went right down or aside" : $"she was {m_alignErr * 100f:F1} cm off his line") : $"he was not in her after {held:F0} s, {m_sink * 100f:F1} cm past his tip";
                            if (!aside && m_mountTries >= 1 && !m_closedNoticed)
                            {
                                m_closedNoticed = true;
                                Notice(Loc.T("He does not go in: the game is not letting him into her yet.", "Он не входит: игра пока не пускает его в неё."));
                            }
                            string hole = "";
                            try
                            {
                                if (m_vagInformer != null && m_vagInformer.m_holeController != null)
                                {
                                    var hc = m_vagInformer.m_holeController;
                                    hole = $" (the game: closed or blocked {hc.cerradoOBlockeado}, a penis is trying {hc.penisIsTryingPenetrate}, time to open {hc.timeTryingToOpenHole:F1} s)";
                                }
                            }
                            catch (Exception)
                            {
                            }
                            m_mountTries++;
                            if (!aside)
                            {
                                // (Pressed as far as she goes and held as long as she does, and he did not go in: next time further and longer.)
                                learn.fails++;
                                learn.patience = Mathf.Min(14f, learn.patience + 1.5f);
                                learn.extraSink = Mathf.Min(0.03f, learn.extraSink + 0.005f);
                                MountLog($"this hole asks for more: next time she waits {learn.patience:F1} s and presses {(MaxSink + learn.extraSink) * 100f:F1} cm past his tip (try {learn.fails} that did not work for {(m_rideWoman != null ? m_rideWoman.name : "her")}).");
                            }
                            m_mount = 1;
                            m_mountAt = now;
                            if (++m_failsInRow >= 6 && !m_triesNoticed)
                            {
                                // Once, after a good many tries: what could be in the way, by what the tries ran into.
                                m_triesNoticed = true;
                                Notice(aside && erect
                                    ? (Plugin.RideAutoAim.Value
                                        ? Loc.T("She keeps missing him: try another position, or stop and start the sex again.", "Она всё время промахивается: попробуйте другую позу или закончите и начните секс заново.")
                                        : Loc.T("She keeps missing him: turn on \"Finds the way in by herself\" (Sex page, Fine tuning).", "Она всё время промахивается: включите «Сама находит вход» (страница «Секс», «Тонкая настройка»)."))
                                    : !erect
                                        ? (Plugin.RideKeepHard.Value
                                            ? Loc.T("His penis keeps bending away from her: start the sex over, or try another position.", "Член всё время уходит в сторону: начните секс заново или попробуйте другую позу.")
                                            : Loc.T("He keeps going soft: turn on \"Keeps him hard\" (Sex page, Orgasm).", "Он всё время опадает: включите «Не даёт ему опасть» (страница «Секс», «Оргазм»)."))
                                        : Loc.T("The game does not let him in: try the other hole, or start the sex over.", "Игра его не пускает: попробуйте другое отверстие или начните секс заново."));
                            }
                            MountLog($"try {m_mountTries} to take him in did not work: {why}{hole}; by where he is: {(geo ? "in" : "out")}, by the game: {(game ? "in" : "out")}. She comes up and tries again.");
                        }
                    }
                    break;
                }
                default:
                {
                    // Riding: she sits on him and the stroke comes in. Should he slip out, she stops and takes him in again.
                    sGoal = m_sSeat;
                    if (!m_in)
                    {
                        if (m_outSince <= 0f)
                        {
                            m_outSince = now;
                        }
                        // A lost game penetration flag with no upward travel is not evidence that the top of
                        // her stroke was too high. Give the game's contact a little longer to settle there.
                        bool movedUp = m_sTop - m_sSeat > 0.015f && m_strokeNow > 0.012f;
                        if (now - m_outSince > (movedUp ? 0.25f : 0.75f))
                        {
                            m_outSince = 0f;
                            m_mount = 1;
                            m_mountAt = now;
                            // (She has learned how far up him this hole of hers goes: the next strokes stop short of it.)
                            m_slips++;
                            if (movedUp)
                            {
                                m_topCut = Mathf.Min(0.08f, m_topCut + 0.02f);
                            }
                            m_tipInLow = float.MaxValue;
                            MountLog($"he slipped out of her (slip {m_slips} of this ride; {m_speedNow:F2} strokes a second, the stroke {(m_sTop - m_sSeat) * 100f:F1} cm long, upward travel {(m_strokeNow * 100f):F1} cm, game entered {game}, geometry in {geo}); she stops and takes him in again. The top margin is {(((m_holeAnal ? 0.055f : 0.035f) + m_topCut) * 100f):F1} cm.");
                        }
                    }
                    else
                    {
                        m_outSince = 0f;
                        // How little of him is left in her at the top of her strokes, by the game's own entrance and canal
                        // as she is drawn (AimGame.cs): when that gets down to a centimetre or two, the top comes down
                        // before he is out.
                        if (m_gameAimValid && Time.frameCount - m_gameAimFrame <= 2 && m_gameTipOff < 0.04f)
                        {
                            m_tipInLow = Mathf.Min(m_tipInLow, m_gameTipIn);
                        }
                        if (now >= m_tipInAt)
                        {
                            m_tipInAt = now + Mathf.Clamp(1.5f / Mathf.Max(0.2f, m_speedNow), 1.2f, 6f);
                            float least = m_holeAnal ? 0.03f : 0.02f;
                            if (m_tipInLow < least && m_rideW > 0.9f)
                            {
                                float more = Mathf.Min(0.02f, least - m_tipInLow + 0.005f);
                                if (m_topCut + more <= 0.08f)
                                {
                                    m_topCut += more;
                                    MountLog($"at the top of her strokes only {m_tipInLow * 100f:F1} cm of him was left in her (by the game's entrance, as she is drawn): her strokes stop {more * 100f:F1} cm sooner, {(((m_holeAnal ? 0.055f : 0.035f) + m_topCut) * 100f):F1} cm short of his tip.");
                                }
                            }
                            else if (m_tipInLow < float.MaxValue && m_tipInLow > least + 0.025f
                                && m_topCut > 0f && now - m_mountAt > 12f && m_herPain < 0.2f)
                            {
                                // The earlier safety cut is allowed back only after the game's entrance has held a
                                // generous margin for several strokes. Never force extra depth through pain.
                                m_topCut = Mathf.Max(0f, m_topCut - 0.005f);
                            }
                            m_tipInLow = float.MaxValue;
                        }
                    }
                    break;
                }
            }
            // Come down past his tip with the game ready for him and yet not noticing him at her entrance: she circles her
            // hips a little, well within his line, so that his tip finds its way in rather than resting against her beside
            // it. (Out of that it fades away again.)
            bool searching = m_mount == 2 && !m_in && readyEnough && m_sink > 0.015f && !GameTrying();
            m_searchAmp = Mathf.MoveTowards(m_searchAmp, searching ? 0.006f : 0f, 0.012f * dt);
            if (m_searchAmp > 1e-5f)
            {
                m_searchPhase += dt * Mathf.PI * 2f / 1.6f;
                Vector3 fwdS = Vector3.ProjectOnPlane(m_rideFwd, Vector3.up);
                fwdS = fwdS.sqrMagnitude > 1e-6f ? fwdS.normalized : Vector3.forward;
                m_searchOff = (fwdS * Mathf.Cos(m_searchPhase) + Vector3.Cross(Vector3.up, fwdS) * Mathf.Sin(m_searchPhase)) * m_searchAmp;
            }
            else
            {
                m_searchOff = Vector3.zero;
            }
            // Her height goes there at a pace (slower while she takes him in), and starts and ends gently.
            m_sGoalNow = Mathf.MoveTowards(m_sGoalNow, sGoal, pace * dt);
            m_sBase = Damp(m_sBase, m_sGoalNow, ref m_sVel, 0.2f, dt);
            m_rideW = Mathf.MoveTowards(m_rideW, m_mount == 3 ? 1f : 0f, dt / (m_mount == 3 ? 0.9f : 0.25f));
        }
    }
}
