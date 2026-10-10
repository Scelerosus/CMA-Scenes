using System;
using System.Collections.Generic;
using UnityEngine;

namespace CMAScenes
{
    // Her womb, for the X-ray through her body.
    //
    // What the game draws at the far end of her canal does not read as a womb. So a shape of the plugin's own is put
    // there (WombStep): the body of the womb (a pear, flat from front to back, its neck down), the two tubes
    // out of its upper corners and an ovary at the end of each. It is as long as the two bones are apart, and moves and
    // tips with them.
    public sealed partial class ShakeController
    {
        private GameObject m_xWomb;
        private Mesh m_xWombMesh;
        private Material m_xWombMat;
        private float m_xWombLen, m_xWombAlpha, m_xWombFill = -1f;
        private static readonly Color WombTint = new Color(0.9f, 0.34f, 0.42f);
        private bool m_xWombTold;

        // A tube along a line of points, as thick at each as told, and flat by that much across "side". Closed where it
        // is given no thickness. Faces outward.
        private static void WombTube(List<Vector3> vs, List<int> tris, Vector3[] line, float[] thick, Vector3 side, float flat, int around)
        {
            int first = vs.Count;
            for (int i = 0; i < line.Length; i++)
            {
                Vector3 along = (line[Mathf.Min(i + 1, line.Length - 1)] - line[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 x = Vector3.ProjectOnPlane(side, along);
                x = x.sqrMagnitude > 1e-6f ? x.normalized : Vector3.Cross(along, Vector3.forward).normalized;
                Vector3 z = Vector3.Cross(along, x);
                for (int j = 0; j < around; j++)
                {
                    float a = j / (float)around * Mathf.PI * 2f;
                    vs.Add(line[i] + (x * Mathf.Cos(a) + z * (Mathf.Sin(a) * flat)) * thick[i]);
                }
            }
            for (int i = 0; i + 1 < line.Length; i++)
            {
                for (int j = 0; j < around; j++)
                {
                    int j1 = (j + 1) % around;
                    int a0 = first + i * around + j, b0 = first + i * around + j1, a1 = a0 + around, b1 = b0 + around;
                    tris.Add(a0); tris.Add(b0); tris.Add(a1);
                    tris.Add(b0); tris.Add(b1); tris.Add(a1);
                }
            }
        }

        // The shape, one unit long from the neck of her womb (the origin) to its top (up); her left and right are
        // across (x), her front and back are z.
        private static Mesh WombMesh()
        {
            var vs = new List<Vector3>();
            var tris = new List<int>();
            // Its body: the mouth of its neck, the neck, the narrow place above it, the body widening to its top.
            float[] ys = { -0.05f, -0.03f, 0.05f, 0.16f, 0.27f, 0.36f, 0.48f, 0.6f, 0.72f, 0.83f, 0.92f, 0.98f, 1.01f };
            float[] rs = { 0f, 0.1f, 0.15f, 0.16f, 0.14f, 0.15f, 0.21f, 0.29f, 0.35f, 0.37f, 0.31f, 0.18f, 0f };
            var line = new Vector3[ys.Length];
            for (int i = 0; i < ys.Length; i++)
            {
                line[i] = new Vector3(0f, ys[i], 0f);
            }
            WombTube(vs, tris, line, rs, Vector3.right, 0.62f, 20);
            for (int s = -1; s <= 1; s += 2)
            {
                // A tube: out of the upper corner, sideways, and round and down to the ovary, widening at its end.
                WombTube(vs, tris, new[]
                {
                    new Vector3(s * 0.26f, 0.84f, 0f), new Vector3(s * 0.42f, 0.9f, 0.01f), new Vector3(s * 0.6f, 0.95f, 0.03f), new Vector3(s * 0.78f, 0.95f, 0.05f),
                    new Vector3(s * 0.93f, 0.88f, 0.06f), new Vector3(s * 1.02f, 0.76f, 0.05f), new Vector3(s * 1.04f, 0.64f, 0.03f), new Vector3(s * 1.02f, 0.57f, 0.02f)
                }, new[] { 0.05f, 0.04f, 0.033f, 0.033f, 0.04f, 0.055f, 0.075f, 0f }, Vector3.forward, 1f, 8);
                // An ovary: an egg lying under the end of the tube, held to the womb by a cord.
                Vector3 mid = new Vector3(s * 0.8f, 0.55f, 0.02f), lie = new Vector3(s * 0.94f, -0.34f, 0f).normalized;
                var egg = new Vector3[9];
                var eggR = new float[9];
                for (int i = 0; i < 9; i++)
                {
                    float t = i / 8f * Mathf.PI;
                    egg[i] = mid - lie * (Mathf.Cos(t) * 0.19f);
                    eggR[i] = Mathf.Sin(t) * 0.12f;
                }
                WombTube(vs, tris, egg, eggR, Vector3.forward, 0.85f, 10);
                WombTube(vs, tris, new[] { new Vector3(s * 0.3f, 0.7f, 0f), new Vector3(s * 0.48f, 0.64f, 0.01f), new Vector3(s * 0.64f, 0.6f, 0.02f) }, new[] { 0.02f, 0.016f, 0.016f }, Vector3.forward, 1f, 6);
            }
            var mesh = new Mesh { name = "CMAScenes.Womb", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void WombMake(Material from, float alpha)
        {
            WombDrop();
            if (m_xVagBones == null || m_xVagBones.Length < 2 || m_xLayer < 0)
            {
                return;
            }
            m_xWombMesh = WombMesh();
            m_xWombMat = GhostOf(from, WombTint, alpha, XQueue);
            m_xWombAlpha = alpha;
            m_xWombFill = -1f;
            m_xWomb = new GameObject("CMAScenes.Womb") { layer = m_xLayer, hideFlags = HideFlags.HideAndDontSave };
            m_xWomb.AddComponent<MeshFilter>().sharedMesh = m_xWombMesh;
            MeshRenderer r = m_xWomb.AddComponent<MeshRenderer>();
            r.sharedMaterial = m_xWombMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            m_xWomb.SetActive(false);
            m_xWombLen = 0f;
        }

        // Each frame: put at the far end of her canal, going on the way the canal runs and tipped toward her front, as a
        // womb lies. (It was hung between the game's two bones for it at first: those are under three centimetres
        // apart and do not point the way a womb lies, and it came out in the wrong place.) Its neck is the game's bone
        // for it when that is at the end of her canal, so that it gives when he pushes at it.
        private void WombStep(Woman w)
        {
            if (m_xWomb == null)
            {
                return;
            }
            Transform[] canal = m_xVagBones;
            bool can = canal != null && canal.Length > 1 && canal[0] != null && canal[canal.Length - 1] != null && w != null && w.legs[0] != null && w.legs[1] != null;
            Vector3 end = can ? canal[canal.Length - 1].position : Vector3.zero;
            Vector3 up = can ? end - canal[0].position : Vector3.up;
            float runs = up.magnitude;
            can = can && runs > 0.02f && runs < 0.3f;
            if (m_xWomb.activeSelf != can)
            {
                m_xWomb.SetActive(can);
            }
            if (!can)
            {
                return;
            }
            up /= runs;
            Vector3 canalUp = up;
            Vector3 neck = end;
            bool onBone = false;
            if (m_xCervix != null)
            {
                Vector3 c = m_xCervix.position - end;
                if (c.sqrMagnitude < 0.04f * 0.04f && Vector3.Dot(c, up) > -0.012f)
                {
                    neck = m_xCervix.position;
                    onBone = true;
                }
            }
            // Her womb goes on from her canal: his head, where it is past the end of the canal, goes in through the neck of
            // the womb, which draws back round it (the womb is put that much further along).
            // (Only while he is in her: after the sex he stands somewhere, and his tip "past the end of her canal" meant
            // nothing.)
            if (m_in && PenisNow(out Vector3 penRoot, out Vector3 penTip))
            {
                float past = Vector3.Dot(penTip - end, canalUp) - 0.012f;
                if (past > 0f)
                {
                    neck += canalUp * Mathf.Min(past, 0.07f);
                }
            }
            // (Her front: the way from her anus to her vagina.)
            float tipped = 0f;
            try
            {
                if (m_xVag != null && m_xAnus != null && m_xVag.m_hole != null && m_xAnus.m_hole != null && m_xVag.m_hole.entrada != null && m_xAnus.m_hole.entrada != null)
                {
                    Vector3 front = Vector3.ProjectOnPlane(m_xVag.m_hole.entrada.position - m_xAnus.m_hole.entrada.position, up);
                    if (front.sqrMagnitude > 1e-6f)
                    {
                        up = (up + front.normalized * 0.4f).normalized;
                        tipped = 22f;
                    }
                }
            }
            catch (Exception)
            {
            }
            m_xWombLen = 0.062f;
            float apart = runs;
            Vector3 across = Vector3.ProjectOnPlane(w.legs[1].upper.t.position - w.legs[0].upper.t.position, up);
            if (across.sqrMagnitude < 1e-6f)
            {
                return;
            }
            across.Normalize();
            Transform t = m_xWomb.transform;
            t.position = neck;
            t.rotation = Quaternion.LookRotation(Vector3.Cross(across, up), up);
            // His semen in her (he came in her vagina, bare): her womb fills with it over a few seconds, paler and
            // rounder, and empties again as it leaves her.
            float fill = m_xFillV;
            if (Mathf.Abs(fill - m_xWombFill) > 0.02f || (fill <= 0f && m_xWombFill != 0f))
            {
                m_xWombFill = fill;
                Filled(m_xWombMat, WombTint, m_xWombAlpha, fill);
            }
            float round = 1f + 0.16f * fill;
            t.localScale = new Vector3(m_xWombLen * round, m_xWombLen * (1f + 0.05f * fill), m_xWombLen * (1f + 0.4f * fill));
            if (!m_xWombTold)
            {
                m_xWombTold = true;
                Plugin.ModLog.LogInfo($"X-ray: through her body: her womb is a shape of the plugin's own, at the far end of her canal ({apart * 100f:F1} cm from its entrance), its neck {(onBone ? "on the game's bone '" + m_xCervix.name + "'" : "on the last bone of her canal")}, tipped {tipped:F0} deg toward her front; drawn {m_xWombLen * 100f:F1} cm long.");
            }
        }

        private void WombDrop()
        {
            try
            {
                if (m_xWomb != null) UnityEngine.Object.Destroy(m_xWomb);
                if (m_xWombMesh != null) UnityEngine.Object.Destroy(m_xWombMesh);
                if (m_xWombMat != null) UnityEngine.Object.Destroy(m_xWombMat);
            }
            catch (Exception)
            {
            }
            m_xWomb = null;
            m_xWombMesh = null;
            m_xWombMat = null;
        }
    }
}
