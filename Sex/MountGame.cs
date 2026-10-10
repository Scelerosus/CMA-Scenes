using System;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;
using GameHole = Assets._ReusableScripts.CuchiCuchi.PhysicsAndBonesScripts.BoneStretchedChain;
using GamePenis = Assets._ReusableScripts.CuchiCuchi.Penis;
using VagInformer = Assets._ReusableScripts.CuchiCuchi.Dependentes.Characters.FemaleCharPenetrationInformer;

namespace CMAScenes
{
    public sealed partial class ShakeController
    {
        // Her other hole, as the game has it: he is not to end up in that one. How many times he did, and since when.
        private VagInformer.BaseInformer m_otherInformer;
        private int m_wrongTries;
        private float m_wrongSince;
        private bool m_wrongNoticed;
        private GameHole m_otherHole;
        private VagInformer.BaseInformer m_vagInformer;

        // Her mouth, as the game has it: how much of him is in it (0 none, 1 all of him), for the wet look. Any woman's:
        // their records are looked for again every couple of seconds.
        private VagInformer[] m_mouths;
        private float m_mouthsAt;

        // How deep he is in her and how wide she is opened, as the game has it (its own units, near enough metres).
        private float m_gameDepth, m_gameOpen;
        private const float HalfGlans = 0.015f, OpenByGlans = 0.03f;

        // Where a hole of hers really opens: the middle of the ring of points the game stretches round whatever goes in
        // (its "entrance" is the root of the hole, some way inside her).
        private static Vector3 HoleOpening(GameHole hole)
        {
            try
            {
                Vector3 ring = hole.ObtenerCentroDePuntosGlobal();
                if ((ring - hole.entrada.position).sqrMagnitude < 0.01f)
                {
                    return ring;
                }
            }
            catch (Exception)
            {
            }
            return hole.entrada.position;
        }

        // Is the game opening her to him (his tip is at her entrance and it has taken that for trying to get in)?
        private bool GameTrying()
        {
            try
            {
                var hc = m_vagInformer != null ? m_vagInformer.m_holeController : null;
                return hc != null && hc.penisIsTryingPenetrate;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Is he in her other hole, by the game's own record?
        private bool OtherEntered()
        {
            if (m_otherInformer == null)
            {
                return false;
            }
            try
            {
                if (!m_otherInformer.isPenetrated)
                {
                    return false;
                }
                var by = m_otherInformer.penetradoPor;
                return m_heroPen == null || by == null || by.Pointer == m_heroPen.Pointer;
            }
            catch (Exception)
            {
                m_otherInformer = null;
                return false;
            }
        }

        // What the game itself makes of his penis at her entrance, for the log.
        private string TryState()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                if (m_vagInformer != null)
                {
                    sb.Append("the game: entered ").Append(m_vagInformer.isPenetrated).Append(" (depth ").Append((m_gameDepth * 100f).ToString("F1")).Append(", opened ").Append((m_gameOpen * 100f).ToString("F1")).Append(')');
                    var hc = m_vagInformer.m_holeController;
                    if (hc != null)
                    {
                        sb.Append(", a penis is trying ").Append(hc.penisIsTryingPenetrate).Append(", closed or blocked ").Append(hc.cerradoOBlockeado);
                    }
                }
                if (m_vagHole != null)
                {
                    sb.Append(", a penis near her entrance: ").Append(m_vagHole.Cercano() != null);
                    Vector3 root = m_vagHole.entrada.position;
                    Vector3 ring = HoleOpening(m_vagHole);
                    Vector3 axis = m_vagHole.worldOutHoleDirection;
                    m_vagHole.ObtenerAperturaEntreColliderDePuntosUnidadesGlobales(out float widest, out float narrowest);
                    sb.Append("; her ring is ").Append(((ring - root).magnitude * 100f).ToString("F1")).Append(" cm from her hole's root, ")
                        .Append((narrowest * 100f).ToString("F1")).Append(" to ").Append((widest * 100f).ToString("F1")).Append(" cm wide");
                    sb.Append("; her pelvis is tipped ").Append(m_holeTilt.ToString("F0")).Append(" deg to bring it in line with him");
                }
                if (m_heroPen != null)
                {
                    sb.Append("; his penis in something ").Append(m_heroPen.isPenetrating).Append(", blocked ").Append(m_heroPen.IsBlocked());
                    // What the game needs of him before it looks for him in her at all: hard enough not to be hidden (a
                    // hidden penis has its colliders put away, and nothing of it is found at her entrance).
                    sb.Append(", hidden ").Append(m_heroPen.hidden).Append(", erection ").Append(m_heroPen.erection.ToString("F1"))
                        .Append(" (aimed at ").Append(m_heroPen.m_ErectionTarget.ToString("F1")).Append(", hidden below ").Append(Penetrador.minErectionBeforeHide.ToString("F1")).Append(')');
                    PenisPart tipPart = m_heroPen.punta;
                    if (tipPart != null)
                    {
                        sb.Append(", his tip as the game has it: hidden ").Append(tipPart.hidden).Append(", on ").Append(tipPart.activado)
                            .Append(", ").Append(tipPart.penetrationState.ToString()).Append(", ").Append((tipPart.worldDistanceToHole * 100f).ToString("F1")).Append(" cm from a hole");
                    }
                }
                if (m_vagHole != null)
                {
                    sb.Append("; may be entered ").Append(m_vagHole.canBePenetrated);
                    // Where his tip is against the hole as the game lays it out (its root and the way it opens): how far in
                    // past its root, how far off its axis, and the angle between him and it. And how far the tip the game
                    // has (its physics) is from the tip that is drawn, which is the one she is put on.
                    if (PenisNow(out Vector3 pb, out Vector3 pt))
                    {
                        Vector3 root = m_vagHole.entrada.position;
                        Vector3 inward = -m_vagHole.worldOutHoleDirection.normalized;
                        float inPast = Vector3.Dot(pt - root, inward);
                        float offAxis = Vector3.ProjectOnPlane(pt - root, inward).magnitude;
                        sb.Append("; his tip is ").Append((inPast * 100f).ToString("F1")).Append(" cm in past her hole's root and ").Append((offAxis * 100f).ToString("F1"))
                            .Append(" cm off its axis, ").Append(Vector3.Angle(pt - pb, inward).ToString("F0")).Append(" deg between him and it");
                        Transform gameTip = m_heroPen != null ? m_heroPen.tipPhysics : null;
                        if (gameTip != null)
                        {
                            sb.Append("; the game's tip is ").Append(((gameTip.position - pt).magnitude * 100f).ToString("F1")).Append(" cm from the drawn one");
                        }
                    }
                }
                sb.Append(AimState());
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "the game could not be asked (" + ex.Message + ")";
            }
        }

        // Whether the game itself would let his penis into her now. His penis may stand up as drawn while the game still
        // has it too soft: below the game's own erection for it, it is "hidden" (its colliders are put away) and nothing
        // of it is found at her entrance however far she comes down on it. (Seen in a run: fourteen tries of sinking 5 cm
        // past his tip with the game never once noticing a penis near her, then in at once the moment he got hard enough.)
        // Blocked is the game's other refusal (after he came, until he is hard again).
        private bool GamePenisReady(out string why)
        {
            why = "";
            if (m_heroPen == null)
            {
                return true;
            }
            try
            {
                bool hidden = m_heroPen.hidden;
                bool blocked = m_heroPen.IsBlocked();
                if (!hidden && !blocked)
                {
                    return true;
                }
                why = $"erection {m_heroPen.erection:F1} (aimed at {m_heroPen.m_ErectionTarget:F1}, hidden below {Penetrador.minErectionBeforeHide:F1}), hidden {hidden}, blocked {blocked}";
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        // Finds the game's record of her vagina and his penis as the game has it. Called when he is taken up.
        private void MountCapture(Character hero)
        {
            bool anal = AnalNow();
            m_holeAnal = anal;
            string holeName = anal ? "anus" : "vagina";
            m_vagInformer = null;
            m_otherInformer = null;
            m_vagHole = null;
            m_heroPen = null;
            Woman w = m_rideWoman;
            m_otherHole = null;
            m_pastLocal = Vector3.zero;
            try
            {
                if (w != null && w.character != null && w.legs[0] != null && w.legs[1] != null)
                {
                    w.vulvaLocalSet = false;
                    w.holeAnchor = null;
                    // (Her crotch, without the fine-tuning of the aim.)
                    VulvaOf(w);
                    Vector3 at = w.vulva != null ? w.vulva.position : 0.5f * (w.legs[0].upper.t.position + w.legs[1].upper.t.position) + Vector3.down * VulvaBelowHips;
                    // Hers is the one whose entrance is at her crotch.
                    float best = 0.25f;
                    var all = Resources.FindObjectsOfTypeAll<VagInformer>();
                    for (int i = 0; all != null && i < all.Length; i++)
                    {
                        VagInformer inf = all[i];
                        if (inf == null || !inf.gameObject.scene.IsValid() || !inf.gameObject.activeInHierarchy)
                        {
                            continue;
                        }
                        VagInformer.BaseInformer vag = anal ? inf.m_anusInformer : inf.m_vagInformer;
                        GameHole hole = vag != null ? vag.hole : null;
                        Transform entrance = hole != null ? hole.entrada : null;
                        if (entrance == null)
                        {
                            continue;
                        }
                        float d = (entrance.position - at).magnitude;
                        if (d < best)
                        {
                            best = d;
                            m_vagInformer = vag;
                            m_vagHole = hole;
                            m_otherInformer = anal ? inf.m_vagInformer : inf.m_anusInformer;
                            m_otherHole = m_otherInformer != null ? m_otherInformer.hole : null;
                        }
                    }
                    if (m_vagInformer != null)
                    {
                        Transform entrance = m_vagHole.entrada;
                        Vector3 open = HoleOpening(m_vagHole);
                        Vector3 off = open - at;
                        string taken = "her entrance is still taken from her bone";
                        // The entrance the game goes by, kept as a fixed place on the bone of hers it is next to: the entrance
                        // itself may be moved about by the game as it is stretched.
                        Transform anchor = w.vulva != null ? w.vulva : w.legs[0].upper.t.parent;
                        if (anchor != null && off.magnitude < 0.2f)
                        {
                            w.vulvaLocal = anchor.InverseTransformPoint(open);
                            w.holeAnchor = anchor;
                            w.vulvaLocalSet = true;
                            taken = $"her entrance is taken to be at the ring it opens by, {(open - entrance.position).magnitude * 100f:F1} cm from that";
                            // Each time he has ended up in her other hole the aim goes a centimetre further from it.
                            float past = 0.01f * m_wrongTries;
                            if (m_otherHole != null && m_otherHole.entrada != null && past > 0f)
                            {
                                Vector3 away = open - HoleOpening(m_otherHole);
                                if (away.sqrMagnitude > 1e-6f)
                                {
                                    m_pastLocal = anchor.InverseTransformVector(away.normalized * past);
                                    w.vulvaLocal += m_pastLocal;
                                    taken += $" (aimed {past * 100f:F1} cm past it, away from her other hole, which is {away.magnitude * 100f:F1} cm from it)";
                                }
                            }
                        }
                        Vector3 outDir = m_vagHole.worldOutHoleDirection;
                        Plugin.ModLog.LogInfo($"Ride: the game's record of her {holeName} was found: its entrance '{entrance.name}' is {off.magnitude * 100f:F1} cm from her crotch bone (up {off.y * 100f:F1} cm), {taken}; it opens toward {outDir.ToString("F2")}; entered now: {m_vagInformer.isPenetrated}, may be entered: {m_vagHole.canBePenetrated}.");
                    }
                    else
                    {
                        Plugin.ModLog.LogInfo($"Ride: the game's record of her {holeName} was not found; {(anal ? "her vagina's place stands for it, and " : "")}whether he is in her is told from where his penis is.");
                    }
                }
            }
            catch (Exception ex)
            {
                m_vagInformer = null;
                m_vagHole = null;
                Plugin.ModLog.LogWarning("The game's record of her vagina could not be read: " + ex.Message);
            }
            // (And the game's own look for a penis at that hole: its entrance is what she is aimed by, AimGame.cs.)
            CalcCapture();
            try
            {
                m_heroPen = hero.GetComponentInChildren<GamePenis>(true);
                if (m_heroPen == null && hero.transform.root != null)
                {
                    m_heroPen = hero.transform.root.GetComponentInChildren<GamePenis>(true);
                }
                if (m_heroPen != null)
                {
                    Plugin.ModLog.LogInfo($"Ride: his penis as the game has it: erection {m_heroPen.erection:F2} (aimed at {m_heroPen.m_ErectionTarget:F2}), {m_heroPen.worldLength * 100f:F0} cm long, hidden {m_heroPen.hidden}, blocked {m_heroPen.IsBlocked()}, in something now: {m_heroPen.isPenetrating}.");
                }
                else
                {
                    Plugin.ModLog.LogInfo("Ride: his penis was not found among the game's own; it cannot be asked to stay hard.");
                }
            }
            catch (Exception ex)
            {
                m_heroPen = null;
                Plugin.ModLog.LogWarning("His penis could not be read from the game: " + ex.Message);
            }
        }

        private float MouthDepth()
        {
            if (m_heroPen == null || !m_penisValid)
            {
                return 0f;
            }
            try
            {
                if (m_mouths == null || Time.unscaledTime >= m_mouthsAt)
                {
                    m_mouthsAt = Time.unscaledTime + 4f;
                    m_mouths = Resources.FindObjectsOfTypeAll<VagInformer>();
                }
                if (!PenisNow(out Vector3 b, out Vector3 t))
                {
                    return 0f;
                }
                Vector3 shaft = t - b;
                float len = shaft.magnitude;
                if (len < 0.03f)
                {
                    return 0f;
                }
                Vector3 dir = shaft / len;
                float most = 0f;
                for (int i = 0; m_mouths != null && i < m_mouths.Length; i++)
                {
                    VagInformer inf = m_mouths[i];
                    if (inf == null || !inf.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    VagInformer.BaseInformer mouth = inf.m_bocaInformer;
                    GameHole hole = mouth != null ? mouth.hole : null;
                    if (hole == null || hole.entrada == null || !mouth.isPenetrated)
                    {
                        continue;
                    }
                    var by = mouth.penetradoPor;
                    if (by != null && by.Pointer != m_heroPen.Pointer)
                    {
                        continue;
                    }
                    // How far down him her lips are.
                    Vector3 lips = HoleOpening(hole);
                    float along = Vector3.Dot(lips - b, dir);
                    if (along > -0.03f && along < len && (lips - b - dir * along).magnitude < 0.06f)
                    {
                        most = Mathf.Max(most, Mathf.Clamp01((len - along) / len));
                        // (Kept for the threads of saliva between her lips and him as she lets him out: WetStrings.cs.)
                        m_mouthSeen = hole;
                        m_mouthSeenAt = Time.unscaledTime;
                    }
                }
                return most;
            }
            catch (Exception)
            {
                m_mouths = null;
                return 0f;
            }
        }

        private void MountForget()
        {
            CondomRideOver();
            m_vagInformer = null;
            m_otherInformer = null;
            m_otherHole = null;
            m_vagHole = null;
            m_heroPen = null;
            m_penisGoalValid = false;
        }

        // Is he in her? By the game's own record of her vagina when there is one (and it is his penis that is in it), else
        // by where his penis is: her vagina on its line, between its root and a little below its tip.
        private bool PenisInHer(Vector3 vulva, bool have, Vector3 b, Vector3 t, ref bool game, ref bool geo)
        {
            geo = false;
            game = false;
            if (have)
            {
                Vector3 shaft = t - b;
                float len = shaft.magnitude;
                if (len > 0.03f)
                {
                    Vector3 dir = shaft / len;
                    float along = Vector3.Dot(vulva - b, dir);
                    float off = (vulva - b - dir * along).magnitude;
                    geo = along > -0.03f && along < len - 0.012f && off < 0.035f;
                }
            }
            m_gameDepth = 0f;
            m_gameOpen = 0f;
            if (m_vagInformer != null)
            {
                try
                {
                    game = m_vagInformer.isPenetrated;
                    if (game && m_heroPen != null)
                    {
                        // By his penis, not by a finger or a toy.
                        var by = m_vagInformer.penetradoPor;
                        if (by != null && by.Pointer != m_heroPen.Pointer)
                        {
                            game = false;
                        }
                    }
                    var state = m_vagInformer.estadoActual;
                    m_gameDepth = state.profundidadPeneLocal;
                    m_gameOpen = state.aperturaLocal;
                    // Where his penis is against her is not enough: it goes through her skin beside her hole just as
                    // well as into it. He is in her when the game itself has her hole round him: opened by his glans, or
                    // with him half a glans deep in it at least. (Once she rides, for as long as the game has him in it.)
                    if (!game)
                    {
                        return false;
                    }
                    return m_mount >= 3 || m_gameDepth >= HalfGlans || m_gameOpen >= OpenByGlans;
                }
                catch (Exception ex)
                {
                    m_vagInformer = null;
                    Plugin.ModLog.LogWarning("The game's record of her hole failed, going by where his penis is: " + ex.Message);
                }
            }
            return geo;
        }
    }
}
