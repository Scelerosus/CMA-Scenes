using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.Holes.Internals;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace CMAScenes
{
    // The X-ray through her body: her insides, and what of him is in her, drawn over her skin, half see-through.
    //
    // HDRP/Lit draws a see-through surface in one pass, and what that pass is tested against is a number on the
    // material (_ZTestDepthEqualForOpaque, which the editor fills from _ZTestTransparent): set to "always", it is drawn
    // whatever is before it. The materials of his penis are see-through surfaces already, so copies of them are what
    // everything here is drawn with.
    //
    // (At first his own materials were set to "always". Then all of him showed through anything: his root through his
    // own belly, and he lay over her as solid as ever, as if he merely went through her. So:)
    //
    //  - His penis itself is left alone. A second drawing of it is made (the same mesh on the same bones), half
    //    see-through and drawn "always", of which only what is in her is kept: each texel of its picture carries in its
    //    alpha where along him it lies (the map the wetness is drawn by, Wet.cs), and the material cuts away everything
    //    below the place her entrance is at in that frame (and never anything below where his shaft comes out of his own
    //    skin: the root of the mesh lies inside his body). It is fainter toward her entrance and solid at his tip.
    //  - With the condom on him, its teat (and what he came in it) is drawn the same way while it is in her.
    //  - Her insides are given see-through materials while the X-ray is on (their own are put back after), drawn twice:
    //    once before him, and once more after him as a thin veil, so that he is seen inside them and not on them.
    //  - Her womb is a shape of the plugin's own (XRayWomb.cs), hung between the bones the game has for its neck and
    //    its top.
    public sealed partial class ShakeController
    {
        private const float ZAlways = 8f;
        // (After his own penis, which is drawn at 2750 or so, and before the threads of her wetness.)
        private const int XQueue = 2756;
        private const int HimTex = 256;
        // The alpha of a texel of his: this at his base, 255 at his tip.
        private const float HimAlphaLow = 110f;

        private sealed class XGhost
        {
            public Renderer renderer;
            public Il2CppReferenceArray<Material> was;
            public Il2CppReferenceArray<Material> ghosts;
            public Material first;
            public bool wasEnabled;
            public int putBack;
            public int layerWas = -1;
            // For his semen in it: its own colour, how solid it is, whether it is her rectum, and how full it is drawn.
            public Color tint;
            public float alpha, fill;
            public bool anal;
        }

        // The second drawing of a mesh of his penis.
        private sealed class XHim
        {
            public SkinnedMeshRenderer src, draws;
            public GameObject obj;
            public readonly List<Material> mats = new List<Material>();
            public readonly List<Texture2D> texs = new List<Texture2D>();
            public int shapes;
        }

        private readonly List<XGhost> m_xGhosts = new List<XGhost>();
        private readonly List<XHim> m_xHim = new List<XHim>();
        private bool m_xGhostOn, m_xGhostTold, m_xHimLatex;
        private int m_xGhostFails, m_xHimParts;
        private float m_xGhostAlpha;
        private Material m_xGhostFrom;
        // The layer what the plugin draws for the X-ray is put on: his penis's (the view draws it). Kept once found, for
        // after the sex, when his skin may have been let go.
        private int m_xLayer = -1;
        // Where along him her entrance is this frame (XRayCut, worked out once a frame).
        private float m_xCutNow = 2f;

        // "In her body" (0.53.5). The second drawing of him is shown only where something is before it. That needs her
        // skin to have written its depth by the time he is drawn; whether it does is read off her skin's material
        // (SkinWritesDepth, once for each woman). It is no longer instead of the cut at her entrance but besides it: by
        // depth alone, with the cut set seven centimetres outside her, everything of him that his own body hid from his
        // view was drawn too (the root of his penis inside his belly, under her), and it read as his belly and his balls
        // seen through (the user's picture of 2026-10-10).
        private bool m_xInBody;
        private Woman m_xInBodyFor;
        private bool m_xInBodyWas;

        private bool SkinWritesDepth(Woman w)
        {
            if (w == m_xInBodyFor)
            {
                return m_xInBodyWas;
            }
            m_xInBodyFor = w;
            m_xInBodyWas = SkinDepthRead(w);
            return m_xInBodyWas;
        }

        private bool SkinDepthRead(Woman w)
        {
            try
            {
                SkinnedMeshRenderer best = null;
                Material skin = null;
                int most = 0;
                var all = w.character.transform.GetComponentsInChildren<SkinnedMeshRenderer>(false);
                for (int i = 0; all != null && i < all.Length; i++)
                {
                    SkinnedMeshRenderer r = all[i];
                    if (r == null || !r.enabled || r.sharedMesh == null)
                    {
                        continue;
                    }
                    var mats = r.sharedMaterials;
                    for (int k = 0; mats != null && k < mats.Length; k++)
                    {
                        Material m = mats[k];
                        if (m != null && m.name.IndexOf("Skin_Body", StringComparison.OrdinalIgnoreCase) >= 0 && r.sharedMesh.vertexCount > most)
                        {
                            most = r.sharedMesh.vertexCount;
                            best = r;
                            skin = m;
                        }
                    }
                }
                if (skin == null)
                {
                    Plugin.ModLog.LogInfo("X-ray: in her body: the skin of her body was not found among her meshes; all of him past her entrance is shown.");
                    return false;
                }
                float surface = skin.HasProperty("_SurfaceType") ? skin.GetFloat("_SurfaceType") : -1f;
                float zwrite = skin.HasProperty("_ZWrite") ? skin.GetFloat("_ZWrite") : -1f;
                float tzwrite = skin.HasProperty("_TransparentZWrite") ? skin.GetFloat("_TransparentZWrite") : -1f;
                float prepass = skin.HasProperty("_TransparentDepthPrepassEnable") ? skin.GetFloat("_TransparentDepthPrepassEnable") : -1f;
                bool opaque = skin.renderQueue < 2500 || surface == 0f;
                bool depth = opaque || tzwrite > 0.5f || prepass > 0.5f;
                // (And before him: an opaque skin always is; a see-through one only when it is drawn earlier.)
                bool before = opaque || skin.renderQueue < XQueue + 2;
                Plugin.ModLog.LogInfo($"X-ray: in her body: her skin is '{skin.name}' on '{best.name}' ({(skin.shader != null ? skin.shader.name : "?")}), queue {skin.renderQueue}, surface type {surface:F0}, depth write {zwrite:F0}, see-through depth write {tzwrite:F0}, depth before colour {prepass:F0}: {(depth && before ? "it has written its depth before he is drawn; of what of him is past her entrance, only what is hidden is shown" : "its depth is not there when he is drawn; all of him past her entrance is shown")}.");
                return depth && before;
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogInfo("X-ray: in her body: her skin could not be read (" + ex.Message + "); all of him past her entrance is shown.");
                return false;
            }
        }

        private static void DrawnAlways(Material m, int queue)
        {
            if (m.HasProperty("_ZTestDepthEqualForOpaque")) m.SetFloat("_ZTestDepthEqualForOpaque", ZAlways);
            if (m.HasProperty("_ZTestTransparent")) m.SetFloat("_ZTestTransparent", ZAlways);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_TransparentZWrite")) m.SetFloat("_TransparentZWrite", 0f);
            // (Lit from within by as much whatever the light and the camera's exposure: inside her it is in her shadow.)
            if (m.HasProperty("_EmissiveExposureWeight")) m.SetFloat("_EmissiveExposureWeight", 0f);
            m.renderQueue = queue;
        }

        // A plain see-through material of one colour: for her insides and her womb.
        private Material GhostOf(Material from, Color tint, float alpha, int queue)
        {
            Material m = new Material(from) { name = "CMAScenes.XRay", hideFlags = HideFlags.HideAndDontSave };
            foreach (string map in new[] { "_BaseColorMap", "_MainTex", "_MaskMap", "_NormalMap", "_DetailMap", "_ThicknessMap", "_SubsurfaceMaskMap", "_CoatMaskMap", "_EmissiveColorMap" })
            {
                if (m.HasProperty(map)) m.SetTexture(map, null);
            }
            m.DisableKeyword("_MASKMAP");
            m.DisableKeyword("_NORMALMAP");
            m.DisableKeyword("_DETAIL_MAP");
            m.DisableKeyword("_EMISSIVE_COLOR_MAP");
            Color c = new Color(tint.r, tint.g, tint.b, alpha);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.7f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            // (Nothing of it is cut away for being thin: his material is one that is cut by its alpha.)
            if (m.HasProperty("_AlphaCutoff")) m.SetFloat("_AlphaCutoff", 0.01f);
            // (Both sides of it: her canal is seen from within by the game, and from without here.)
            if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", 0f);
            if (m.HasProperty("_CullModeForward")) m.SetFloat("_CullModeForward", 0f);
            if (m.HasProperty("_EmissiveColor")) m.SetColor("_EmissiveColor", new Color(tint.r * 0.35f, tint.g * 0.35f, tint.b * 0.35f, 1f));
            if (m.HasProperty("_AlbedoAffectEmissive")) m.SetFloat("_AlbedoAffectEmissive", 0f);
            DrawnAlways(m, queue);
            return m;
        }

        private static readonly Color SemenTint = new Color(0.97f, 0.94f, 0.84f);

        // A see-through material as full of his semen as told: paler, more solid, lit more from within.
        private static void Filled(Material m, Color tint, float alpha, float fill)
        {
            if (m == null)
            {
                return;
            }
            Color c = Color.Lerp(tint, SemenTint, fill);
            c.a = Mathf.Lerp(alpha, Mathf.Min(1f, alpha + 0.3f), fill);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            float lit = Mathf.Lerp(0.35f, 0.6f, fill);
            if (m.HasProperty("_EmissiveColor")) m.SetColor("_EmissiveColor", new Color(c.r * lit, c.g * lit, c.b * lit, 1f));
        }

        private void GhostAdd(HoleInternal hi, Material from, Color tint, float alpha, bool anal, string what)
        {
            if (hi == null)
            {
                return;
            }
            SkinnedMeshRenderer smr = hi.skinnedMeshRenderer;
            if (smr == null)
            {
                smr = hi.m_SkinnedMeshRenderer;
            }
            if (smr == null)
            {
                Plugin.ModLog.LogInfo($"X-ray: through her body: {what} has no mesh of its own to show.");
                return;
            }
            Il2CppReferenceArray<Material> was = smr.sharedMaterials;
            if (was == null || was.Length == 0)
            {
                Plugin.ModLog.LogInfo($"X-ray: through her body: {what} ('{smr.name}') has no materials.");
                return;
            }
            // Each part of the mesh behind him; and one more material, which draws the last part once again, before him:
            // the veil.
            var ghosts = new Il2CppReferenceArray<Material>(was.Length + 1);
            for (int i = 0; i < was.Length; i++)
            {
                ghosts[i] = GhostOf(from, tint, alpha, XQueue);
            }
            ghosts[was.Length] = GhostOf(from, Color.Lerp(tint, Color.white, 0.25f), alpha * 0.4f, XQueue + 4);
            var g = new XGhost { renderer = smr, was = was, ghosts = ghosts, first = ghosts[0], wasEnabled = smr.enabled, tint = tint, alpha = alpha, anal = anal };
            if (!smr.enabled)
            {
                smr.enabled = true;
            }
            // (The game may keep her insides on a layer that only its own X-ray cameras draw. Then the view of the
            // player never draws them, whatever their material: for as long as the X-ray is on they are put on the layer
            // of his penis, which it does draw. Not when the object has a collider on it: its layer is the physics' too.)
            Camera view = Camera.main;
            int layer = smr.gameObject.layer;
            bool drawn = view == null || (view.cullingMask & (1 << layer)) != 0;
            string layerSaid = drawn ? "which the view draws" : "which the view does not draw";
            if (!drawn && m_xLayer >= 0)
            {
                if (smr.GetComponent<Collider>() == null)
                {
                    g.layerWas = layer;
                    smr.gameObject.layer = m_xLayer;
                    layerSaid += ": put on layer " + smr.gameObject.layer + " (his penis's) while the X-ray is on";
                }
                else
                {
                    layerSaid += ", and it has a collider on it: left there (nothing of it will show through her)";
                }
            }
            smr.sharedMaterials = ghosts;
            m_xGhosts.Add(g);
            if (!m_xGhostTold)
            {
                Plugin.ModLog.LogInfo($"X-ray: through her body: {what} is the mesh '{smr.name}' ({(smr.sharedMesh != null ? smr.sharedMesh.vertexCount : 0)} points, {was.Length} part(s), on layer {layer}, {layerSaid}); drawn see-through at {alpha:F2}, with a veil of it before him.");
            }
        }

        // The picture of a part of his skin, with where along him each texel lies in its alpha (nothing where the map
        // shows nothing of him).
        private static Texture2D HimPicture(WetPart p)
        {
            if (p == null || p.along == null || p.along.Length != MaskSize * MaskSize)
            {
                return null;
            }
            int step = MaskSize / HimTex;
            var px = new Il2CppStructArray<Color32>(HimTex * HimTex);
            bool skin = p.skin != null && p.skin.Length == p.along.Length;
            for (int y = 0; y < HimTex; y++)
            {
                for (int x = 0; x < HimTex; x++)
                {
                    // (Of the texels of the map that fall on this one, the one furthest up him: none is lost at the
                    // edges of the islands of the map.)
                    int at = -1;
                    float a = -1f;
                    for (int k = 0; k < step * step; k++)
                    {
                        int i = (y * step + k / step) * MaskSize + x * step + k % step;
                        if (p.along[i] > a)
                        {
                            a = p.along[i];
                            at = i;
                        }
                    }
                    Color32 c = skin && at >= 0 ? p.skin[at] : new Color32(214, 160, 140, 255);
                    c.a = a < 0f ? (byte)0 : (byte)Mathf.RoundToInt(HimAlphaLow + (255f - HimAlphaLow) * Mathf.Clamp01(a));
                    px[y * HimTex + x] = c;
                }
            }
            var tex = new Texture2D(HimTex, HimTex, TextureFormat.RGBA32, false) { name = "CMAScenes.XRayHim", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        // The material a part of the second drawing of him is drawn with. With no picture (a part of the mesh that is
        // not his skin, or one whose map could not be read) nothing of that part is drawn at all.
        private static Material HimGhostOf(Material from, Texture2D picture, Color tint)
        {
            Material m = new Material(from) { name = "CMAScenes.XRayHim", hideFlags = HideFlags.HideAndDontSave };
            if (picture != null)
            {
                if (m.HasProperty("_BaseColorMap")) m.SetTexture("_BaseColorMap", picture);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", picture);
            }
            Color c = new Color(tint.r, tint.g, tint.b, picture != null ? 1f : 0f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_AlphaCutoffEnable")) m.SetFloat("_AlphaCutoffEnable", 1f);
            m.EnableKeyword("_ALPHATEST_ON");
            if (m.HasProperty("_AlphaCutoff")) m.SetFloat("_AlphaCutoff", 0.999f);
            if (m.HasProperty("_EmissiveColor")) m.SetColor("_EmissiveColor", new Color(0.3f, 0.3f, 0.3f, 1f));
            if (m.HasProperty("_AlbedoAffectEmissive")) m.SetFloat("_AlbedoAffectEmissive", 1f);
            DrawnAlways(m, XQueue + 2);
            return m;
        }

        // (5: where what is there already is nearer than this.)
        private static void DrawnHidden(Material m)
        {
            if (m.HasProperty("_ZTestDepthEqualForOpaque")) m.SetFloat("_ZTestDepthEqualForOpaque", 5f);
            if (m.HasProperty("_ZTestTransparent")) m.SetFloat("_ZTestTransparent", 5f);
        }

        private void HimAdd(SkinnedMeshRenderer src, float alpha)
        {
            Il2CppReferenceArray<Material> its = src.sharedMaterials;
            if (src.sharedMesh == null || its == null || its.Length == 0)
            {
                return;
            }
            var h = new XHim { src = src };
            var mats = new Il2CppReferenceArray<Material>(its.Length);
            int shown = 0;
            for (int slot = 0; slot < its.Length; slot++)
            {
                WetPart part = null;
                foreach (WetPart p in m_wetParts)
                {
                    if (p.renderer != null && p.renderer.Pointer == src.Pointer && p.slot == slot)
                    {
                        part = p;
                        break;
                    }
                }
                Texture2D picture = HimPicture(part);
                Color tint = Color.white;
                if (part != null && part.material != null && part.material.HasProperty("_BaseColor"))
                {
                    tint = part.material.GetColor("_BaseColor");
                    if (tint.maxColorComponent < 0.05f)
                    {
                        tint = Color.white;
                    }
                }
                Material m = HimGhostOf(part != null && part.material != null ? part.material : m_wetParts[0].material, picture, tint);
                if (m_xInBody)
                {
                    DrawnHidden(m);
                }
                if (picture != null)
                {
                    Color c = m.GetColor("_BaseColor");
                    c.a = alpha;
                    m.SetColor("_BaseColor", c);
                    h.texs.Add(picture);
                    shown++;
                }
                h.mats.Add(m);
                mats[slot] = m;
            }
            // (Its name must not read as his penis: he is looked for by the names of his meshes.)
            h.obj = new GameObject("CMAScenes.XRayHim") { layer = src.gameObject.layer, hideFlags = HideFlags.HideAndDontSave };
            h.obj.transform.SetParent(src.transform.parent, false);
            h.obj.transform.localPosition = src.transform.localPosition;
            h.obj.transform.localRotation = src.transform.localRotation;
            h.obj.transform.localScale = src.transform.localScale;
            h.draws = h.obj.AddComponent<SkinnedMeshRenderer>();
            h.draws.sharedMesh = src.sharedMesh;
            h.draws.bones = src.bones;
            h.draws.rootBone = src.rootBone;
            h.draws.localBounds = src.localBounds;
            h.draws.quality = src.quality;
            h.draws.updateWhenOffscreen = src.updateWhenOffscreen;
            h.draws.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            h.draws.receiveShadows = false;
            h.draws.sharedMaterials = mats;
            h.draws.enabled = false;
            h.shapes = src.sharedMesh.blendShapeCount;
            m_xHim.Add(h);
            if (!m_xGhostTold)
            {
                Plugin.ModLog.LogInfo($"X-ray: through her body: his penis is drawn a second time for what of it is in her (mesh '{src.name}', {its.Length} part(s), {shown} of them with a map of where along him they lie, {h.shapes} shape(s) kept the same); his own drawing is left as it is.");
            }
        }

        // Where along him her entrance is, 0 at his base and 1 at his tip; above 1 when he is not in her.
        private float XRayCut()
        {
            if (!m_in || m_mount < 2 || m_vagHole == null || m_vagHole.entrada == null || !PenisLive(out Vector3 root, out Vector3 tip))
            {
                return 2f;
            }
            Vector3 axis = tip - root;
            float len2 = axis.sqrMagnitude;
            if (len2 < 1e-4f)
            {
                return 2f;
            }
            float len = Mathf.Sqrt(len2);
            // (From a few millimetres outside the place the game calls her entrance: XRay/CutBelowEntrance. It was 3 cm
            // once, for a gap seen at her lips (2026-10-09), and 7 cm with "in her body": then his penis was drawn down
            // onto his own belly, root and all (2026-10-10). Now never more than 3 cm.)
            float below = Mathf.Clamp(Plugin.XRayBelow.Value, -1f, 3f) * 0.01f;
            float cut = Vector3.Dot(HoleOpening(m_vagHole) - root, axis) / len2 - below / len;
            // (And never below where his shaft comes out of his skin, the game's own place for it, and half a centimetre
            // more: the root of the mesh, and what of it is drawn in his belly, are nothing of his that is in her.)
            float skin = 0.1f;
            try
            {
                Transform surface = m_heroPen != null ? m_heroPen.skinSurfaceTransform : null;
                if (surface != null)
                {
                    skin = Mathf.Clamp(Vector3.Dot(surface.position - root, axis) / len2, 0f, 0.6f);
                }
            }
            catch (Exception)
            {
            }
            return Mathf.Max(0f, Mathf.Max(cut, skin + 0.005f / len));
        }

        private void HimStep(float alpha)
        {
            float cut = XRayCut();
            m_xCutNow = cut;
            bool latex = m_condom == 4;
            Color32 tint = default;
            bool any = false;
            if (latex != m_xHimLatex && latex)
            {
                CondomTint(out tint, out float _);
            }
            foreach (XHim h in m_xHim)
            {
                if (h.src == null || h.draws == null)
                {
                    continue;
                }
                bool shown = cut < 0.97f && h.src.enabled && h.src.gameObject.activeInHierarchy && (m_heroPen == null || !m_heroPen.hidden);
                if (h.draws.enabled != shown)
                {
                    h.draws.enabled = shown;
                }
                if (!shown)
                {
                    continue;
                }
                any = true;
                float cutoff = alpha * (HimAlphaLow + (255f - HimAlphaLow) * Mathf.Clamp01(cut)) / 255f;
                for (int i = 0; i < h.mats.Count; i++)
                {
                    Material m = h.mats[i];
                    if (m == null || m.GetColor("_BaseColor").a <= 0f)
                    {
                        continue;
                    }
                    m.SetFloat("_AlphaCutoff", cutoff);
                    if (latex != m_xHimLatex)
                    {
                        // (In a condom: the colour of its latex over him.)
                        Color c = latex ? Color.Lerp(Color.white, new Color(tint.r / 255f, tint.g / 255f, tint.b / 255f), 0.6f) : Color.white;
                        m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, alpha));
                    }
                }
                for (int i = 0; i < h.shapes && i < 48; i++)
                {
                    float wgt = h.src.GetBlendShapeWeight(i);
                    if (Mathf.Abs(h.draws.GetBlendShapeWeight(i) - wgt) > 0.01f)
                    {
                        h.draws.SetBlendShapeWeight(i, wgt);
                    }
                }
            }
            if (any)
            {
                m_xHimLatex = latex;
            }
        }

        // Kept up every frame.
        private void GhostStep(Woman w)
        {
            if (m_xGhostFails > 3)
            {
                return;
            }
            try
            {
                float alpha = Mathf.Clamp(Plugin.XRayOpacity.Value, 0.15f, 1f);
                // (Made anew when his skin was read anew. After the sex his skin may be let go: what was made stays.)
                float dim = 0.49f;
                if (m_xGhostOn && (Mathf.Abs(dim - m_xShadeDim) > 0.005f || (m_wetParts.Count > 0 && m_xHimParts != m_wetParts.Count)))
                {
                    GhostOff();
                }
                // (The slider for how solid it is only colours anew what there is: made anew each frame it was dragged,
                // it cost tens of milliseconds a frame.)
                if (m_xGhostOn && Mathf.Abs(alpha - m_xGhostAlpha) > 0.005f)
                {
                    GhostAlpha(alpha);
                }
                if (!m_xGhostOn)
                {
                    m_xGhostOn = true;
                    m_xGhostAlpha = alpha;
                    m_xHimParts = m_wetParts.Count;
                    m_xHimLatex = false;
                    Material from = m_wetParts.Count > 0 ? m_wetParts[0].material : null;
                    if (from == null)
                    {
                        from = m_xGhostFrom;
                    }
                    m_xGhostFrom = from;
                    if (m_wetParts.Count > 0 && m_wetParts[0].renderer != null)
                    {
                        m_xLayer = m_wetParts[0].renderer.gameObject.layer;
                    }
                    m_xShadeDim = dim;
                    if (from == null || m_xLayer < 0)
                    {
                        if (!m_xGhostTold)
                        {
                            Plugin.ModLog.LogInfo("X-ray: through her body: no material of his was found to draw with; nothing is shown through her.");
                        }
                    }
                    else
                    {
                        GhostAdd(m_xVag, from, new Color(0.96f, 0.5f, 0.58f), alpha, false, "her vagina");
                        GhostAdd(m_xAnus, from, new Color(0.88f, 0.5f, 0.44f), alpha, true, "her rectum");
                        WombMake(from, Mathf.Min(1f, alpha + 0.2f));
                        ShadeMake(from, dim);
                        FlowMake(from);
                        m_xInBody = Plugin.XRayInBody.Value && SkinWritesDepth(w);
                        var done = new List<IntPtr>();
                        foreach (WetPart p in m_wetParts)
                        {
                            SkinnedMeshRenderer src = p.renderer != null ? p.renderer.TryCast<SkinnedMeshRenderer>() : null;
                            if (src != null && !done.Contains(src.Pointer))
                            {
                                done.Add(src.Pointer);
                                HimAdd(src, Mathf.Min(1f, alpha + 0.3f));
                            }
                        }
                    }
                    m_xGhostTold = true;
                }
                for (int i = m_xGhosts.Count - 1; i >= 0; i--)
                {
                    XGhost g = m_xGhosts[i];
                    if (g.renderer == null)
                    {
                        m_xGhosts.RemoveAt(i);
                        continue;
                    }
                    // (His semen in it: her canal is drawn half as full as her womb, her rectum fully.)
                    float fill = g.anal ? m_xFillA * 0.6f : m_xFillV * 0.35f;
                    if (Mathf.Abs(fill - g.fill) > 0.02f || (fill <= 0f && g.fill > 0f))
                    {
                        g.fill = fill;
                        for (int k = 0; k + 1 < g.ghosts.Length; k++)
                        {
                            Filled(g.ghosts[k], g.tint, g.alpha, fill);
                        }
                    }
                    Material now = g.renderer.sharedMaterial;
                    if (now != null && g.first != null && now.Pointer == g.first.Pointer)
                    {
                        continue;
                    }
                    // The game has put materials of its own on them: those are the ones to give back, and the
                    // see-through ones go on again. (Not for ever, should it do so every frame.)
                    if (++g.putBack > 40)
                    {
                        Plugin.ModLog.LogInfo($"X-ray: through her body: the game keeps putting its own materials back on '{g.renderer.name}'; it is left to it.");
                        m_xGhosts.RemoveAt(i);
                        continue;
                    }
                    Il2CppReferenceArray<Material> its = g.renderer.sharedMaterials;
                    if (its != null && its.Length == g.was.Length)
                    {
                        g.was = its;
                        g.renderer.sharedMaterials = g.ghosts;
                    }
                }
                float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.004f, 0.05f);
                SemenStep(m_xGhostFrom, dt);
                HimStep(Mathf.Min(1f, alpha + 0.3f));
                TeatGhostStep(Mathf.Min(1f, alpha + 0.3f));
                WombStep(w);
                ShadeStep(w);
                FlowStep(dt);
            }
            catch (Exception ex)
            {
                m_xGhostFails++;
                Plugin.ModLog.LogWarning("X-ray: through her body: failed (" + ex.Message + ")" + (m_xGhostFails > 3 ? "; no longer tried." : "."));
                GhostOff();
            }
        }

        // How solid it is all drawn, set anew from the slider.
        private void GhostAlpha(float alpha)
        {
            m_xGhostAlpha = alpha;
            foreach (XGhost g in m_xGhosts)
            {
                g.alpha = alpha;
                // (Coloured anew below, by how full of his semen it is.)
                g.fill = -1f;
                Material veil = g.ghosts != null && g.ghosts.Length > 0 ? g.ghosts[g.ghosts.Length - 1] : null;
                if (veil != null && veil.HasProperty("_BaseColor"))
                {
                    Color c = Color.Lerp(g.tint, Color.white, 0.25f);
                    veil.SetColor("_BaseColor", new Color(c.r, c.g, c.b, alpha * 0.4f));
                }
            }
            float him = Mathf.Min(1f, alpha + 0.3f);
            foreach (XHim h in m_xHim)
            {
                foreach (Material m in h.mats)
                {
                    if (m != null && m.HasProperty("_BaseColor"))
                    {
                        Color c = m.GetColor("_BaseColor");
                        if (c.a > 0f)
                        {
                            c.a = him;
                            m.SetColor("_BaseColor", c);
                        }
                    }
                }
            }
            m_xWombAlpha = Mathf.Min(1f, alpha + 0.2f);
            m_xWombFill = -1f;
            m_xTeatTint = -1f;
        }

        // The teat of the condom while it is in her, with what he came in it: drawn as he is, over her.
        private GameObject m_xTeat;
        private Material m_xTeatMat;
        private float m_xTeatTint = -1f;

        private void TeatGhostStep(float alpha)
        {
            bool want = m_condom == 4 && m_condomTeat != null && m_condomTeat.activeInHierarchy && m_condomTeatMesh != null && m_xGhostFrom != null && m_xLayer >= 0 && m_xCutNow < 0.97f;
            if (!want)
            {
                if (m_xTeat != null && m_xTeat.activeSelf)
                {
                    m_xTeat.SetActive(false);
                }
                return;
            }
            if (m_xTeat == null)
            {
                m_xTeatMat = GhostOf(m_xGhostFrom, SemenTint, alpha, XQueue + 3);
                if (m_xInBody)
                {
                    DrawnHidden(m_xTeatMat);
                }
                m_xTeat = new GameObject("CMAScenes.XRayTeat") { layer = m_xLayer, hideFlags = HideFlags.HideAndDontSave };
                m_xTeat.AddComponent<MeshFilter>().sharedMesh = m_condomTeatMesh;
                MeshRenderer r = m_xTeat.AddComponent<MeshRenderer>();
                r.sharedMaterial = m_xTeatMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                m_xTeatTint = -1f;
            }
            if (!m_xTeat.activeSelf)
            {
                m_xTeat.SetActive(true);
            }
            Transform from = m_condomTeat.transform, to = m_xTeat.transform;
            to.SetPositionAndRotation(from.position, from.rotation);
            to.localScale = from.lossyScale;
            // The latex's colour, white with his semen as it fills.
            if (Mathf.Abs(m_condomMl - m_xTeatTint) > 0.05f)
            {
                m_xTeatTint = m_condomMl;
                CondomTint(out Color32 tint, out float _);
                Color latex = new Color(tint.r / 255f, tint.g / 255f, tint.b / 255f);
                Filled(m_xTeatMat, Color.Lerp(Color.white, latex, 0.6f), alpha, Soft(m_condomMl / 1.2f));
            }
        }

        // Everything as it was: her insides get their own materials back, and what was made is taken away.
        private void GhostOff()
        {
            if (!m_xGhostOn && m_xGhosts.Count == 0 && m_xHim.Count == 0)
            {
                return;
            }
            m_xGhostOn = false;
            foreach (XGhost g in m_xGhosts)
            {
                try
                {
                    if (g.renderer != null)
                    {
                        Material now = g.renderer.sharedMaterial;
                        if (now != null && g.first != null && now.Pointer == g.first.Pointer && g.was != null)
                        {
                            g.renderer.sharedMaterials = g.was;
                        }
                        if (!g.wasEnabled)
                        {
                            g.renderer.enabled = false;
                        }
                        else if (!g.renderer.enabled)
                        {
                            g.renderer.enabled = true;
                        }
                        if (g.layerWas >= 0)
                        {
                            g.renderer.gameObject.layer = g.layerWas;
                        }
                    }
                    for (int i = 0; g.ghosts != null && i < g.ghosts.Length; i++)
                    {
                        if (g.ghosts[i] != null)
                        {
                            UnityEngine.Object.Destroy(g.ghosts[i]);
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
            m_xGhosts.Clear();
            foreach (XHim h in m_xHim)
            {
                try
                {
                    if (h.obj != null)
                    {
                        UnityEngine.Object.Destroy(h.obj);
                    }
                    foreach (Material m in h.mats)
                    {
                        if (m != null)
                        {
                            UnityEngine.Object.Destroy(m);
                        }
                    }
                    foreach (Texture2D t in h.texs)
                    {
                        if (t != null)
                        {
                            UnityEngine.Object.Destroy(t);
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
            m_xHim.Clear();
            WombDrop();
            ShadeDrop();
            FlowDrop();
            SemenOff();
            try
            {
                if (m_xTeat != null) UnityEngine.Object.Destroy(m_xTeat);
                if (m_xTeatMat != null) UnityEngine.Object.Destroy(m_xTeatMat);
            }
            catch (Exception)
            {
            }
            m_xTeat = null;
            m_xTeatMat = null;
            m_xCutNow = 2f;
        }
    }
}
