using System;
using UnityEngine;

namespace CMAScenes
{
    // What the game does to her after she has been posed, and her put back for it in the same frame.
    //
    // She is posed at the first late stage of the game's frame. After that the game's own inverse kinematics run (FinalIK,
    // between the stages lateUpdateOnFinalIK and lateUpdateAfterFinalIK: the grounder, and the high heel system, which
    // stands her on her toes and lifts her pelvis by the height of her heels). So she is drawn somewhere else than she
    // was put: in heels some 7.7 cm higher, and she sat down on the air above him.
    //
    // That was first taken up a frame late: the lift was measured at the end of a frame, smoothed, and taken off where she
    // was put in the next. But the lift is not steady. In a run it went from 7.6 cm to 10.9 and to 19.2 cm within a few
    // strokes as she rode faster ("after she is posed the game moves her up by 19,2 cm"), the smoothed value lagged, the
    // top of her stroke went past his tip by what was missing, and he slipped out of her: three times in one ride of her
    // anus. Nothing that follows from behind can hold the top of a stroke to a centimetre.
    //
    // So she is put back in the same frame instead, right after the game's inverse kinematics have run and before
    // anything else of the game's looks at her (its puppets, its colliders, its physics step and its look for a penis at
    // her hole all come later in the frame): her hips go back to where they were put, exactly; her feet stay where the
    // game has drawn them (on her heels, on the floor; on her knees, where they were put) and her legs are bent anew to
    // reach them; her hands go back to where they were put. There is nothing to settle and nothing to lag.
    //
    // At the end of the frame it is only measured, for the log: what the game moved her by, and what was left of it.
    public sealed partial class ShakeController
    {
        // Where her hips (her pelvis bone: it is rigid, and the one her holes hang from) were put this frame, and how much
        // of that is hers to keep (the weight of the pose, with him under her).
        private Vector3 m_hipPosed;
        private int m_hipPosedFrame = -1;
        private float m_posedW;
        // Her hands as they were put this frame.
        // Her feet as they were put this frame, and which way her knees were to stand out.
        private readonly Vector3[] m_footPosed = new Vector3[2], m_footPole = new Vector3[2];
        private readonly Quaternion[] m_footPosedRot = new Quaternion[2];
        private int m_footPosedFrame = -1, m_footDragLogs;
        private readonly Vector3[] m_handPosed = new Vector3[2];
        private readonly Quaternion[] m_handPosedRot = new Quaternion[2];
        private int m_handPosedFrame = -1;
        // What the game moved her hips by after she was posed, this frame; and over the time since it was last told:
        // the least and the most of it upward, and the most that was left over at the end of a frame.
        private Vector3 m_gameMoved;
        private int m_gameMovedFrame = -1;
        private float m_movedLow = float.MaxValue, m_movedHigh = float.MinValue, m_leftMost;
        private int m_movedFrames;
        private float m_poseFixLogAt;
        private int m_poseBackFails;

        private void PoseFixReset()
        {
            m_hipPosedFrame = -1;
            m_handPosedFrame = -1;
            m_gameMovedFrame = -1;
            m_gameMoved = Vector3.zero;
            m_movedLow = float.MaxValue;
            m_movedHigh = float.MinValue;
            m_leftMost = 0f;
            m_movedFrames = 0;
            m_poseFixLogAt = 0f;
        }

        // Right after the game's inverse kinematics (the stage lateUpdateAfterFinalIK): her hips back where they were put.
        internal void PoseBack()
        {
            if (m_poseBackFails > 3)
            {
                return;
            }
            try
            {
                Woman w = m_rideWoman;
                if (w == null || w.hip == null || w.sHip == null || w.sPelvis == null || m_hipPosedFrame != Time.frameCount || !m_ride || m_posedW < 0.02f)
                {
                    return;
                }
                Vector3 moved = w.sPelvis.t.position - m_hipPosed;
                if (moved.sqrMagnitude > 0.25f)
                {
                    // (Half a metre: she was put elsewhere this frame, a cut. Nothing to take back.)
                    return;
                }
                m_gameMoved = moved;
                m_gameMovedFrame = Time.frameCount;
                // (As much of it as she is posed: coming into the pose and out of it she is the game's own by the rest, and
                // standing in her heels she is to stay lifted by them.)
                Vector3 back = -moved * Mathf.Clamp01(m_posedW);
                // (On her knees her feet are kept where they were put: the game's heel system turns them as for standing in
                // heels, and a foot laid top down on the cushion came up into the air. Feet.cs.)
                float kneelKeep = m_footPosedFrame == Time.frameCount ? Mathf.Clamp01(m_kneelPosed) : 0f;
                if (back.sqrMagnitude < 1e-8f && kneelKeep < 0.01f)
                {
                    return;
                }
                // Her feet as the game has drawn them, and which way her knees stand out from the line of her legs.
                Vector3[] foot = s_backFoot;
                Vector3[] pole = s_backPole;
                Quaternion[] footRot = s_backFootRot;
                for (int i = 0; i < 2; i++)
                {
                    Limb leg = w.legs[i];
                    if (leg == null)
                    {
                        return;
                    }
                    foot[i] = leg.end.t.position;
                    footRot[i] = leg.end.t.rotation;
                    pole[i] = leg.fore.t.position - 0.5f * (leg.upper.t.position + foot[i]);
                    // (Carried to a place: should the game have dragged her foot off toward where it has her standing,
                    // by more than a hand's width past what it moved her hips, her foot goes back where it was put.)
                    if (m_carryOn && m_footPosedFrame == Time.frameCount && (foot[i] - m_footPosed[i] - moved).sqrMagnitude > 0.0225f)
                    {
                        if (m_footDragLogs < 3)
                        {
                            m_footDragLogs++;
                            Plugin.ModLog.LogInfo($"Place: the game moved her {(i == 0 ? "left" : "right")} foot {(foot[i] - m_footPosed[i]).magnitude * 100f:F0} cm from where it was put; it is put back.");
                        }
                        foot[i] = m_footPosed[i];
                        footRot[i] = m_footPosedRot[i];
                        pole[i] = m_footPole[i];
                    }
                    else if (kneelKeep > 0.01f)
                    {
                        foot[i] = Vector3.Lerp(foot[i], m_footPosed[i], kneelKeep);
                        footRot[i] = Quaternion.Slerp(footRot[i], m_footPosedRot[i], kneelKeep);
                        pole[i] = Vector3.Slerp(pole[i], m_footPole[i], kneelKeep);
                    }
                }
                w.hip.position += back;
                w.sHip.Seal();
                for (int i = 0; i < 2; i++)
                {
                    Limb leg = w.legs[i];
                    Ik(leg, foot[i], pole[i], 1f);
                    leg.end.t.rotation = footRot[i];
                    leg.end.Commit();
                }
                // Her hands: with her hips back they are where they were put, unless the game has moved them about her
                // body as well. Then they are brought back too (only once she is fully in the pose: until then they are
                // on their way there).
                if (m_handPosedFrame == Time.frameCount && m_posedW > 0.98f)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        Limb arm = w.arms[i];
                        if (arm == null)
                        {
                            continue;
                        }
                        Vector3 hand = arm.end.t.position;
                        if ((hand - m_handPosed[i]).sqrMagnitude < 9e-6f)
                        {
                            continue;
                        }
                        Vector3 elbowOut = arm.fore.t.position - 0.5f * (arm.upper.t.position + hand);
                        Ik(arm, m_handPosed[i], elbowOut, 1f, true);
                        arm.end.t.rotation = m_handPosedRot[i];
                        arm.end.Commit();
                    }
                }
                // (The game's physics step comes later in this same frame: it is told where she is now.)
                Physics.SyncTransforms();
            }
            catch (Exception ex)
            {
                m_poseBackFails++;
                Plugin.ModLog.LogWarning("Ride: posed and drawn: she could not be put back after the game's inverse kinematics (" + ex.Message + ")" + (m_poseBackFails > 3 ? "; no longer tried." : "."));
            }
        }

        private static readonly Vector3[] s_backFoot = new Vector3[2], s_backPole = new Vector3[2];
        private static readonly Quaternion[] s_backFootRot = new Quaternion[2];

        // At the end of the frame, once the game has done all it does to her: for the log, what it moved her by after
        // she was posed and how far from where she was put she is drawn all the same.
        internal void PoseFixMeasure()
        {
            try
            {
                Woman w = m_rideWoman;
                if (w == null || w.sPelvis == null || m_hipPosedFrame != Time.frameCount || m_gameMovedFrame != Time.frameCount || !m_ride || !m_toggled || m_switching || m_posedW < 0.98f)
                {
                    return;
                }
                float up = m_gameMoved.y;
                m_movedLow = Mathf.Min(m_movedLow, up);
                m_movedHigh = Mathf.Max(m_movedHigh, up);
                m_leftMost = Mathf.Max(m_leftMost, (w.sPelvis.t.position - m_hipPosed).magnitude);
                m_movedFrames++;
                float now = Time.unscaledTime;
                if (m_poseFixLogAt <= 0f)
                {
                    m_poseFixLogAt = now + 3f;
                    return;
                }
                if (now < m_poseFixLogAt || m_movedFrames < 30)
                {
                    return;
                }
                m_poseFixLogAt = now + 30f;
                string heels = HeelsText();
                Plugin.ModLog.LogInfo($"Ride: posed and drawn: after she is posed the game moves her hips by {m_movedLow * 100f:F1} to {m_movedHigh * 100f:F1} cm upward (over {m_movedFrames} frames; now {new Vector2(m_gameMoved.x, m_gameMoved.z).magnitude * 100f:F1} cm across the floor){(heels.Length > 0 ? "; " + heels : "")}. She is put back in the same frame; drawn at most {m_leftMost * 1000f:F1} mm from where she was put.");
                if (m_movedHigh > 0.03f && !m_heelsTold)
                {
                    m_heelsTold = true;
                    Notice(Loc.T($"She is on heels ({m_movedHigh * 100f:F0} cm): she squats that much lower to reach him.", $"Она на каблуках ({m_movedHigh * 100f:F0} см): приседает на столько же ниже, чтобы достать до него."));
                }
                m_movedLow = float.MaxValue;
                m_movedHigh = float.MinValue;
                m_leftMost = 0f;
                m_movedFrames = 0;
            }
            catch (Exception)
            {
            }
        }
    }
}
