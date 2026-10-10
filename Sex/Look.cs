using System;
using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;

namespace CMAScenes
{
    public sealed partial class ShakeController
    {
        private HeadFrame m_heroFace;
        private Transform m_heroFaceOf;

        // How her face and his lie on the bones of their heads: found once each.
        private void FacesReady(Woman w, Vector3 fwd)
        {
            if (w.face == null)
            {
                w.face = HeadFrame.Of(w.sHead.t, fwd);
                bool eyesOk = w.face.eyeA != null && w.face.eyeAxisA != Vector3.zero && w.face.eyeAxisB != Vector3.zero;
                Plugin.ModLog.LogInfo($"Ride: her face is found {(w.face.byEyes ? "by her eyes" : "by the way she faces (her eyes were not found)")}; her mouth is {w.face.mouth.magnitude * 100f:F1} cm from the bone of her head; her eyes {(eyesOk ? $"('{w.face.eyeA.name}', '{w.face.eyeB.name}') can be turned" : "cannot be turned by the plugin (which way they look along their bones could not be told)")}; her back hangs from her {(w.sWaist.t.IsChildOf(w.sPelvis.t) ? "pelvis" : "hips, beside her pelvis")}.");
            }
            if (m_heroHead != null && (m_heroFace == null || m_heroFaceOf != m_heroHead))
            {
                m_heroFaceOf = m_heroHead;
                m_heroFace = HeadFrame.Of(m_heroHead, Vector3.up);
                Plugin.ModLog.LogInfo($"Ride: his face is found {(m_heroFace.byEyes ? "by his eyes" : "as looking straight up (his eyes were not found)")}.");
            }
        }

        // ---- she looks at him ----

        // (Failures that could come back every frame are told a few times, then now and then: WarnOnce.)
        private int m_lookWarns, m_finalWarns;
        private float m_lookQuiet, m_finalQuiet;
        private Woman m_lookWoman;
        private int m_lookFrame = -1, m_headDoneFrame = -1, m_eyesDoneFrame = -1;
        private float m_lookW;
        private Vector3 m_headLooks = Vector3.forward;
        private float m_lookLogAt;

        // At the end of posing her: what her head is to do later in the frame, and how her back and neck stand now.
        private void LookKeep(Woman w, float poseW)
        {
            m_lookWoman = w;
            m_lookFrame = Time.frameCount;
            m_lookW = poseW;
            if (w.sHead == null)
            {
                return;
            }
            if (w.neck == null)
            {
                var chain = new List<Transform>();
                for (Transform t = w.sHead.t.parent; t != null && t != w.sSp2.t && chain.Count < 6; t = t.parent)
                {
                    chain.Add(t);
                }
                w.neck = chain.ToArray();
                w.neckLocal = new Quaternion[w.neck.Length];
            }
            for (int i = 0; i < w.neck.Length; i++)
            {
                w.neckLocal[i] = w.neck[i].localRotation;
            }
            w.backLocal[0] = w.sWaist.t.localRotation;
            w.backLocal[1] = w.sSp1.t.localRotation;
            w.backLocal[2] = w.sSp2.t.localRotation;
        }

        // Runs once the game's own look-at has turned her back, neck and head toward what it has her look at (the
        // camera): that is taken back, and her head is turned to his face instead (as far as a neck turns).
        private void HeadLate()
        {
            Woman w = m_lookWoman;
            if (w == null || w.sHead == null || m_lookFrame != Time.frameCount || m_headDoneFrame == Time.frameCount)
            {
                return;
            }
            m_headDoneFrame = Time.frameCount;
            bool look = Plugin.RideLookAtHim.Value && m_heroReady && m_heroHead != null;
            float weight = look ? m_lookW : 0f;
            if (weight <= 0.001f || m_heroHead == null)
            {
                return;
            }
            try
            {
                FacesReady(w, m_rideFwd);
                // (Her waist and spine as they were once she was posed: no array is made for it every frame.)
                w.sWaist.t.localRotation = Quaternion.Slerp(w.sWaist.t.localRotation, w.backLocal[0], weight);
                w.sSp1.t.localRotation = Quaternion.Slerp(w.sSp1.t.localRotation, w.backLocal[1], weight);
                w.sSp2.t.localRotation = Quaternion.Slerp(w.sSp2.t.localRotation, w.backLocal[2], weight);
                // (From her spine up: each bone is turned on the one below it.)
                for (int i = w.neck.Length - 1; i >= 0; i--)
                {
                    w.neck[i].localRotation = Quaternion.Slerp(w.neck[i].localRotation, w.neckLocal[i], weight);
                }
                Transform head = w.sHead.t;
                Quaternion plain = (head.parent != null ? head.parent.rotation : Quaternion.identity) * w.sHead.BaseLocal;
                Quaternion result = Quaternion.Slerp(head.rotation, plain, weight);
                float turned = 0f;
                if (look)
                {
                    Vector3 hisEyes = m_heroHead.position + m_heroHead.rotation * m_heroFace.eyes;
                    Vector3 want = hisEyes - (head.position + plain * w.face.eyes);
                    Vector3 now = plain * w.face.looks;
                    turned = Vector3.Angle(now, want);
                    // (Behind her he cannot be looked at: then her head stays as her pose has it.)
                    if (want.sqrMagnitude > 1e-4f && turned < 125f)
                    {
                        Quaternion full = Quaternion.FromToRotation(now, want) * plain;
                        result = Quaternion.Slerp(result, Quaternion.RotateTowards(plain, full, 55f), m_lookW);
                    }
                }
                if (m_headBack > 0.05f)
                {
                    // (Her head goes back as she comes: Climax.cs.)
                    result = Quaternion.AngleAxis(-m_headBack * m_lookW, Vector3.Cross(Vector3.up, m_rideFwd)) * result;
                }
                head.rotation = result;
                w.sHead.Commit();
                m_headLooks = result * w.face.looks;
                if (Time.unscaledTime >= m_lookLogAt)
                {
                    m_lookLogAt = Time.unscaledTime + 30f;
                    Plugin.ModLog.LogInfo($"Ride: she looks at him: his face is {turned:F0} deg from the way her head is posed ({(turned < 125f ? (turned > 55f ? "turned 55 deg toward him, her eyes do the rest" : "turned to him") : "behind her: left as posed")}).");
                }
            }
            catch (Exception ex)
            {
                m_lookWoman = null;
                if (WarnOnce(ref m_lookWarns, ref m_lookQuiet))
                {
                    Plugin.ModLog.LogWarning("Her head could not be turned to him: " + ex.Message);
                }
            }
        }

        // Runs once the game has turned her eyes: to his eyes instead, as far as eyes turn in a head.
        private void EyesLate()
        {
            Woman w = m_lookWoman;
            if (w == null || w.face == null || m_heroFace == null || m_lookFrame != Time.frameCount || m_eyesDoneFrame == Time.frameCount)
            {
                return;
            }
            m_eyesDoneFrame = Time.frameCount;
            if (!Plugin.RideLookAtHim.Value || !m_heroReady || m_heroHead == null || m_lookW <= 0.001f)
            {
                return;
            }
            try
            {
                Vector3 hisEyes = m_heroHead.position + m_heroHead.rotation * m_heroFace.eyes;
                for (int i = 0; i < 2; i++)
                {
                    Transform eye = i == 0 ? w.face.eyeA : w.face.eyeB;
                    Vector3 axis = i == 0 ? w.face.eyeAxisA : w.face.eyeAxisB;
                    if (eye == null || axis == Vector3.zero)
                    {
                        continue;
                    }
                    Vector3 want = hisEyes - eye.position;
                    if (want.sqrMagnitude < 1e-6f)
                    {
                        continue;
                    }
                    want = Vector3.RotateTowards(m_headLooks, want.normalized, 32f * Mathf.Deg2Rad, 0f);
                    Quaternion turned = Quaternion.FromToRotation(eye.rotation * axis, want) * eye.rotation;
                    eye.rotation = Quaternion.Slerp(eye.rotation, turned, m_lookW);
                }
            }
            catch (Exception ex)
            {
                m_lookWoman = null;
                if (WarnOnce(ref m_lookWarns, ref m_lookQuiet))
                {
                    Plugin.ModLog.LogWarning("Her eyes could not be turned to him: " + ex.Message);
                }
            }
        }
    }
}
