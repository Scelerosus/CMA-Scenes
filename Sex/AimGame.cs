using System;
using System.Text;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;

namespace CMAScenes
{
    // Aiming by the game's own entrance, measured on her as she is drawn.
    //
    // Everything the aim went by was read before she is posed in the frame (MountStep runs inside the posing): her hips
    // are then where the game's animation has them (standing, some 50 cm above where she is drawn squatting), while the
    // ring of her hole is a set of physics points left by the last physics step where she was drawn. So the old
    // self-correcting aim (the ring put on the bone of hers it belongs to) compared a place on her squatting with a bone of
    // her standing, and the tip of her hole's axis (worldOutHoleDirection) was compared, standing, with his shaft: "his tip
    // is -49 cm in past her hole's root and 14 cm off its axis, 14 deg between him and it" was never true of her as drawn.
    //
    // Here it is all measured at the end of the frame, once she has been posed and drawn: where the game's entrance is
    // (the entrance its own look for a penis at her hole casts from: CalculadorDePenetracion's ray, from its entrance to its
    // bottom, or the hole's own entrance where that is not found), which way her canal goes in, and where his tip is
    // against them. From that, closed loop, frame after frame:
    //  - the place on her that is brought onto his tip (w.vulvaLocal) is the game's entrance;
    //  - her pelvis is tipped (m_holeTilt) until her canal, as drawn, lies along his shaft.
    public sealed partial class ShakeController
    {
        private CalculadorDePenetracion m_calc;
        private bool m_calcLogged;

        // The last measurement, of her as drawn.
        private bool m_gameAimValid;
        private int m_gameAimFrame = -10;
        private Vector3 m_gameEntrance, m_gameAxisIn, m_gameTipNow;
        private float m_gameTipIn, m_gameTipOff, m_gameTipAngle, m_gameAimGap, m_gameCanalLen;
        private bool m_gameCercano;
        // How far her canal is off his shaft (deg, about the axis across her), still to be taken up by the tilt.
        private float m_tiltErr;
        private bool m_tiltErrFresh;
        private Vector3 m_tiltRight = Vector3.right;
        private float m_aimProbeAt;
        private string m_holesTrying = "";

        // Finds the game's own look for a penis at her hole (called once the hole is found).
        private void CalcCapture()
        {
            m_calc = null;
            m_calcLogged = false;
            m_gameAimValid = false;
            m_tiltErrFresh = false;
            if (m_vagHole == null)
            {
                return;
            }
            try
            {
                var all = Resources.FindObjectsOfTypeAll<CalculadorDePenetracion>();
                for (int i = 0; all != null && i < all.Length; i++)
                {
                    CalculadorDePenetracion c = all[i];
                    if (c == null || !c.gameObject.scene.IsValid())
                    {
                        continue;
                    }
                    var chain = c.m_Circular8BoneChain;
                    if (chain != null && chain.Pointer == m_vagHole.Pointer)
                    {
                        m_calc = c;
                        break;
                    }
                }
                if (m_calc == null)
                {
                    // (Or the one on the hole itself, or under it.)
                    m_calc = m_vagHole.GetComponentInChildren<CalculadorDePenetracion>(true);
                }
                string what;
                if (m_calc != null)
                {
                    var rt = m_calc.rayTransformConfig;
                    Transform e = rt != null ? rt.entrada : null;
                    Transform f = rt != null ? rt.fondo : null;
                    Vector3 root = m_vagHole.entrada.position;
                    float ancho = 0f;
                    try
                    {
                        ancho = m_calc.rayConfig != null ? m_calc.rayConfig.ancho : 0f;
                    }
                    catch (Exception)
                    {
                    }
                    what = $"its look for a penis at her hole was found on '{m_calc.gameObject.name}': it casts from '{(e != null ? e.name : "-")}' ({(e != null ? ((e.position - root).magnitude * 100f).ToString("F1") : "?")} cm from the hole's entrance) "
                        + $"to '{(f != null ? f.name : "-")}' ({(e != null && f != null ? ((f.position - e.position).magnitude * 100f).ToString("F1") : "?")} cm long), {ancho * 100f:F1} cm wide; the hole's own cast is {m_vagHole.castProfundidad * 100f:F1} cm deep"
                        + $", its bottom '{(m_vagHole.fondo != null ? m_vagHole.fondo.name : "-")}' is {(m_vagHole.fondo != null ? ((m_vagHole.fondo.position - root).magnitude * 100f).ToString("F1") : "?")} cm from its entrance";
                }
                else
                {
                    what = "its look for a penis at her hole was not found; her hole's own entrance and axis are aimed by";
                }
                Plugin.ModLog.LogInfo("Ride: aim: " + what + ".");
            }
            catch (Exception ex)
            {
                m_calc = null;
                Plugin.ModLog.LogWarning("Ride: aim: the game's look for a penis at her hole could not be read: " + ex.Message);
            }
        }

        // Where the game's entrance is and which way her canal goes in, as she is now.
        private bool GameEntrance(out Vector3 entrance, out Vector3 axisIn, out float canalLen)
        {
            entrance = Vector3.zero;
            axisIn = Vector3.down;
            canalLen = 0f;
            if (m_vagHole == null || m_vagHole.entrada == null)
            {
                return false;
            }
            Vector3 root = m_vagHole.entrada.position;
            Vector3 holeIn = -m_vagHole.worldOutHoleDirection;
            holeIn = holeIn.sqrMagnitude > 1e-6f ? holeIn.normalized : Vector3.up;
            entrance = root;
            axisIn = holeIn;
            if (m_calc != null)
            {
                try
                {
                    var rt = m_calc.rayTransformConfig;
                    Transform e = rt != null ? rt.entrada : null;
                    Transform f = rt != null ? rt.fondo : null;
                    if (e != null && (e.position - root).magnitude < 0.06f)
                    {
                        entrance = e.position;
                        if (f != null)
                        {
                            Vector3 d = f.position - e.position;
                            canalLen = d.magnitude;
                            if (canalLen > 0.01f && Vector3.Angle(d, holeIn) < 60f)
                            {
                                axisIn = d / canalLen;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    m_calc = null;
                }
            }
            return true;
        }

        // At the end of the frame, with her posed and drawn: the measurement, and the aim moved by it.
        internal void AimMeasure()
        {
            m_gameAimValid = false;
            try
            {
                Woman w = m_rideWoman;
                if (!m_ride || !m_toggled || m_switching || w == null || m_vagHole == null || m_mount < 1 || !PenisNow(out Vector3 b, out Vector3 t))
                {
                    m_tiltErrFresh = false;
                    return;
                }
                if (!GameEntrance(out Vector3 e, out Vector3 axisIn, out float canalLen))
                {
                    return;
                }
                Vector3 tip = t;
                Transform gameTip = m_heroPen != null ? m_heroPen.tipPhysics : null;
                if (gameTip != null && (gameTip.position - t).magnitude < 0.05f)
                {
                    // (The tip the game's own look goes by.)
                    tip = gameTip.position;
                }
                m_gameEntrance = e;
                m_gameAxisIn = axisIn;
                m_gameTipNow = tip;
                m_gameCanalLen = canalLen;
                m_gameTipIn = Vector3.Dot(tip - e, axisIn);
                m_gameTipOff = Vector3.ProjectOnPlane(tip - e, axisIn).magnitude;
                m_gameTipAngle = Vector3.Angle(t - b, axisIn);
                Vector3 aimNow = VulvaOf(w);
                m_gameAimGap = (aimNow - e).magnitude;
                m_gameAimValid = true;
                m_gameAimFrame = Time.frameCount;

                bool aiming = Plugin.RideAutoAim.Value && !m_in && m_mount <= 2;
                // The place on her that is brought onto his tip: the game's entrance, as a fixed place on her bone (both
                // read off her as she is drawn, so they belong together).
                if (aiming && w.vulvaLocalSet && w.holeAnchor != null)
                {
                    Vector3 want = w.holeAnchor.InverseTransformPoint(e) + m_pastLocal;
                    float moved = w.holeAnchor.TransformVector(want - w.vulvaLocal).magnitude;
                    if (moved < 0.15f)
                    {
                        float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.004f, 0.1f);
                        w.vulvaLocal = Vector3.Lerp(w.vulvaLocal, want, 1f - Mathf.Exp(-dt / 0.15f));
                        if (moved > 0.01f && Time.unscaledTime >= m_aimLogAt)
                        {
                            m_aimLogAt = Time.unscaledTime + 3f;
                            MountLog($"aim: the place brought onto his tip is moved {moved * 100f:F1} cm onto the game's own entrance (measured on her as she is drawn).");
                        }
                    }
                }
                // How far her canal is from lying along his shaft, about the axis across her: the tilt takes it up.
                if (aiming)
                {
                    Vector3 r = m_tiltRight;
                    Vector3 opens = Vector3.ProjectOnPlane(-axisIn, r);
                    Vector3 shaftDown = Vector3.ProjectOnPlane(b - t, r);
                    if (opens.sqrMagnitude > 1e-4f && shaftDown.sqrMagnitude > 1e-4f)
                    {
                        m_tiltErr = Vector3.SignedAngle(opens, shaftDown, r);
                        m_tiltErrFresh = true;
                    }
                }
                else
                {
                    m_tiltErrFresh = false;
                }

                // Now and then (and only for the log): does the game's own look find him at her entrance as she is drawn,
                // and is his penis trying any hole by its own record.
                if (m_mount == 2 && Time.unscaledTime >= m_aimProbeAt)
                {
                    m_aimProbeAt = Time.unscaledTime + 1f;
                    m_gameCercano = false;
                    if (m_calc != null)
                    {
                        try
                        {
                            m_gameCercano = m_calc.TryEncontrarCercano(out Assets.TValle.BeachGirl.IPene p) && p != null;
                        }
                        catch (Exception)
                        {
                        }
                    }
                    m_holesTrying = HolesTrying();
                }
            }
            catch (Exception ex)
            {
                m_gameAimValid = false;
                m_tiltErrFresh = false;
                if (!m_calcLogged)
                {
                    m_calcLogged = true;
                    Plugin.ModLog.LogWarning("Ride: aim: measuring her as drawn failed: " + ex.Message);
                }
            }
        }

        // The holes his penis is trying to get into, by its own record, and how far it has them.
        private string HolesTrying()
        {
            try
            {
                if (m_heroPen == null)
                {
                    return "";
                }
                var list = m_heroPen.m_holesIntentando;
                if (list == null)
                {
                    return "his penis keeps no record of holes it tries";
                }
                var sb = new StringBuilder();
                sb.Append("his penis tries ").Append(list.Count).Append(" hole(s)");
                for (int i = 0; i < list.Count && i < 3; i++)
                {
                    var h = list[i];
                    if (h != null)
                    {
                        sb.Append(i == 0 ? ": " : ", ").Append((h.distance * 100f).ToString("F1")).Append(" cm (frame ").Append(Time.frameCount - h.frameID).Append(" ago)");
                    }
                }
                sb.Append(", its own stage ").Append(m_heroPen.m_UpdateEvent.ToString());
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "his penis's record could not be read (" + ex.Message + ")";
            }
        }

        // The tilt, closed loop: what is left of her canal being off his shaft, as drawn, taken up a little each frame.
        // False when there is no fresh measurement (the old way, from the game's pose, is used then).
        private bool TiltByMeasure(Vector3 right, float dt)
        {
            m_tiltRight = right;
            if (!m_tiltErrFresh || Time.frameCount - m_gameAimFrame > 2)
            {
                return false;
            }
            m_tiltErrFresh = false;
            // (Not chased to the last degree, and not fast: her canal follows her pelvis a moment late, and taken up
            // quickly the tilt swung to and fro, 13 to 32 degrees within a second in one run.)
            float err = Mathf.Abs(m_tiltErr) < 2f ? 0f : m_tiltErr - Mathf.Sign(m_tiltErr) * 2f;
            float step = Mathf.Clamp(err, -20f, 20f) * (1f - Mathf.Exp(-dt / 0.55f));
            m_holeTilt = Mathf.Clamp(m_holeTilt + step, -32f, 32f);
            return true;
        }

        // For the log: her as drawn against the game's entrance.
        private string AimState()
        {
            if (!m_gameAimValid || Time.frameCount - m_gameAimFrame > 3)
            {
                return "; as drawn: not measured";
            }
            return $"; as drawn: his tip is {m_gameTipIn * 100f:F1} cm in past the game's entrance ({(m_calc != null ? "its look's" : "the hole's")}, canal {m_gameCanalLen * 100f:F1} cm) and {m_gameTipOff * 100f:F1} cm off its canal, "
                + $"{m_gameTipAngle:F0} deg between him and it (tilt still to take up {m_tiltErr:F0} deg); the place brought onto his tip is {m_gameAimGap * 100f:F1} cm from the game's entrance; "
                + $"the game's look finds him: {m_gameCercano}{(m_holesTrying.Length > 0 ? "; " + m_holesTrying : "")}"
                + $"; at {m_gameEntrance.ToString("F3")}, canal {m_gameAxisIn.ToString("F2")}, tip {m_gameTipNow.ToString("F3")}";
        }
    }
}
