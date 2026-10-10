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
        // How she sits on him, for the log: a few seconds into the riding (and again when the pose is changed), at the
        // bottom of a stroke.
        private string m_reportPose = "";
        private float m_reportAt;

        private void RideReport(Woman w, Vector3 vulva, float inHer, float lean, float[] handOff, bool onHim)
        {
            string pose = Plugin.RidePoseType.Value + "|" + Plugin.RideFacing.Value;
            float now = Time.unscaledTime;
            if (pose != m_reportPose)
            {
                m_reportPose = pose;
                m_reportAt = now + 6f;
                return;
            }
            if (m_reportAt <= 0f || now < m_reportAt || m_poseW < 0.99f || (onHim && m_heroW < 0.99f)
                || (now < m_reportAt + 4f && Mathf.Sin(m_motion.phase) > -0.9f))
            {
                return;
            }
            m_reportAt = 0f;
            string penis = "his penis was not found";
            if (PenisNow(out Vector3 pb, out Vector3 pt))
            {
                Vector3 dir = (pt - pb).normalized;
                float along = Vector3.Dot(vulva - pb, dir);
                Vector3 off = vulva - pb - dir * along;
                penis = $"her vagina is {along * 100f:F1} cm up his penis from its root ({(pt - pb).magnitude * 100f:F0} cm long, pointing {dir.ToString("F2")}) and {off.magnitude * 100f:F1} cm off its line";
            }
            string game = "the game's record of her vagina is not there";
            try
            {
                if (m_vagInformer != null)
                {
                    var st = m_vagInformer.estadoActual;
                    game = $"the game says entered {m_vagInformer.isPenetrated} (his depth {st.profundidadPeneLocal:F2}, her opening {st.aperturaLocal:F2})";
                }
            }
            catch (Exception)
            {
            }
            string[] stage = { "not over him", "over his tip", "taking him in", "riding" };
            Plugin.ModLog.LogInfo($"Ride: pose '{Plugin.RidePoseType.Value}' (facing {Plugin.RideFacing.Value}), {stage[Mathf.Clamp(m_mount, 0, 3)]}, at the bottom of a stroke: {penis}; depth {inHer:F2}; {game}; "
                + $"she sits {m_sSeat * 100f:F1} cm above his root and rises to {m_sTop * 100f:F1} cm, moved {m_mountCorr.magnitude * 100f:F1} cm across the floor to be on him, his root found {m_penisFix.magnitude * 100f:F1} cm from where it stood; "
                + $"lean {lean:F0} deg; her hands are {handOff[0] * 100f:F1} and {handOff[1] * 100f:F1} cm from her thighs.");
        }
    }
}
