using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UpdateType = Assets._ReusableScripts.Globales.Updater.GlobalUpdater.UpdateType;

namespace CMAScenes
{
    // Her hips where she is drawn, for the rest of the game too.
    //
    // She is posed late in the frame, after the game has animated her. Everything the game does earlier in the frame (its
    // physics step, and its own look for a penis at her entrance, which goes by rays against the colliders of her hole) saw
    // her where the animation has her: standing, her hole some 50 cm above where she is drawn squatting on his tip. So the
    // game never once noticed him at her entrance however far down him she was drawn (seen in a run: 17 tries, "a penis near
    // her entrance: False", his tip "-49 cm past her hole's root" by the game's record of it).
    //
    // So her hips (and the bones from them to her hole) are put back where she was drawn at every stage of the game's frame
    // before she is posed again, and the physics is told of it. Whatever the game's animation writes over them is its own;
    // where nothing wrote, they are given back the values they had, before she is posed again.
    public sealed partial class ShakeController
    {
        private sealed class HeldBone
        {
            public Transform t;
            public Vector3 pos, gamePos;
            public Quaternion rot, gameRot;
            public bool restored;
        }

        private readonly List<HeldBone> m_heldBones = new List<HeldBone>();
        private Woman m_heldWoman;
        private bool m_holdValid;
        private int m_holdLogs;

        // (Diagnostics: how far her hole is from his tip as each stage of the game's frame sees it.)
        private readonly StringBuilder m_stageProbe = new StringBuilder();
        private float m_stageProbeAt;
        private bool m_stageProbing;
        private int m_stageProbeLogs;
        // (A long line: written a few times in a session, while she takes him in, and once in a while as she rides.)
        private const int StageProbeMost = 6;

        private bool HoldWanted(out Woman w)
        {
            w = null;
            if (!Plugin.RideHoldHips.Value || !m_toggled || !m_ride || m_switching || m_poseW < 0.02f)
            {
                return false;
            }
            w = m_rideWoman;
            return w != null && w.hip != null;
        }

        // At the end of the frame, once she has been posed and the game has done everything it does after that.
        internal void HoldCapture()
        {
            try
            {
                if (!HoldWanted(out Woman w))
                {
                    m_holdValid = false;
                    return;
                }
                if (w != m_heldWoman || m_heldBones.Count == 0)
                {
                    m_heldWoman = w;
                    m_heldBones.Clear();
                    var seen = new HashSet<IntPtr>();
                    foreach (Transform t in new[] { w.hip, w.sPelvis?.t, w.sWaist?.t, w.sSp1?.t, w.sSp2?.t })
                    {
                        if (t != null && seen.Add(t.Pointer))
                        {
                            m_heldBones.Add(new HeldBone { t = t });
                        }
                    }
                    if (m_holdLogs < 3)
                    {
                        m_holdLogs++;
                        var names = new StringBuilder();
                        foreach (HeldBone h in m_heldBones)
                        {
                            names.Append(names.Length > 0 ? ", " : "").Append(h.t.name);
                        }
                        Plugin.ModLog.LogInfo($"Ride: her hips are kept where she is drawn for the rest of the game's frame ({names}; the physics is told: {Physics.autoSyncTransforms}->sync).");
                    }
                }
                foreach (HeldBone h in m_heldBones)
                {
                    if (h.t == null)
                    {
                        m_heldBones.Clear();
                        m_holdValid = false;
                        return;
                    }
                    h.pos = h.t.localPosition;
                    h.rot = h.t.localRotation;
                    h.restored = false;
                }
                m_holdValid = true;
            }
            catch (Exception)
            {
                m_holdValid = false;
            }
        }

        // At each stage of the game's frame before she is posed: her hips back where she was drawn, if anything moved them.
        private void HoldApply()
        {
            if (!m_holdValid)
            {
                return;
            }
            try
            {
                bool moved = false;
                foreach (HeldBone h in m_heldBones)
                {
                    if (h.t == null)
                    {
                        m_holdValid = false;
                        return;
                    }
                    Vector3 lp = h.t.localPosition;
                    Quaternion lr = h.t.localRotation;
                    if ((lp - h.pos).sqrMagnitude < 1e-12f && Quaternion.Angle(lr, h.rot) < 0.001f)
                    {
                        continue;
                    }
                    h.gamePos = lp;
                    h.gameRot = lr;
                    h.restored = true;
                    h.t.localPosition = h.pos;
                    h.t.localRotation = h.rot;
                    moved = true;
                }
                if (moved)
                {
                    Physics.SyncTransforms();
                }
            }
            catch (Exception)
            {
                m_holdValid = false;
            }
        }

        // Just before she is posed again: where nothing of the game's wrote over her hips, they get back what they had.
        private void HoldUndo()
        {
            if (!m_holdValid)
            {
                return;
            }
            m_holdValid = false;
            try
            {
                foreach (HeldBone h in m_heldBones)
                {
                    if (h.t == null || !h.restored)
                    {
                        continue;
                    }
                    h.restored = false;
                    if ((h.t.localPosition - h.pos).sqrMagnitude < 1e-12f && Quaternion.Angle(h.t.localRotation, h.rot) < 0.001f)
                    {
                        h.t.localPosition = h.gamePos;
                        h.t.localRotation = h.gameRot;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        // Called for every stage of the game's frame: early stages keep her hips, the first late one gives them back.
        private void HoldStage(UpdateType stage, bool late)
        {
            if (late)
            {
                HoldUndo();
            }
            else if (stage != UpdateType.lateUpdateAfterCameraController)
            {
                HoldApply();
            }
            StageProbe(stage);
        }

        // Every few seconds while she takes him in, for one frame: at each stage, how far her hole is from his tip, and
        // whether the game sees him at it.
        private void StageProbe(UpdateType stage)
        {
            try
            {
                if (stage == UpdateType.lateUpdateAfterCameraController)
                {
                    if (m_stageProbing)
                    {
                        m_stageProbing = false;
                        if (m_stageProbeLogs < StageProbeMost)
                        {
                            m_stageProbeLogs++;
                            Plugin.ModLog.LogInfo("Ride: stages (hole to his tip, cm / trying / near): " + m_stageProbe);
                        }
                    }
                    else if (m_mount >= 1 && m_mount <= 3 && m_vagHole != null && Time.unscaledTime >= m_stageProbeAt && m_stageProbeLogs < StageProbeMost)
                    {
                        m_stageProbeAt = Time.unscaledTime + (m_mount == 3 ? 15f : 4f);
                        m_stageProbing = true;
                        m_stageProbe.Clear();
                    }
                    return;
                }
                if (!m_stageProbing || m_vagHole == null || !PenisNow(out Vector3 b, out Vector3 tip))
                {
                    return;
                }
                float d = (HoleOpening(m_vagHole) - tip).magnitude * 100f;
                bool near = false;
                try
                {
                    near = m_vagHole.Cercano() != null;
                }
                catch (Exception)
                {
                }
                m_stageProbe.Append(stage.ToString()).Append(' ').Append(d.ToString("F0")).Append(GameTrying() ? "/T" : "").Append(near ? "/N" : "").Append("; ");
            }
            catch (Exception)
            {
            }
        }
    }
}
