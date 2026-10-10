using System;
using System.Collections.Generic;
using Assets;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using GameHoleSemen = Assets._ReusableScripts.CuchiCuchi.Characters.Skins.Semen.Abstracts.SemenParaHole;
using GameSemenHoles = Assets._ReusableScripts.CuchiCuchi.Dependentes.Skins.Semen.ControlladorDeSkinDeAcumulacionDeSemenEnHoles;
using GameSemenIn = Assets._ReusableScripts.CuchiCuchi.Holes.Internals.Controlladores.Pieles.ControlladorDeSemenParaInternalHole;
using SemenAnus = Assets._ReusableScripts.CuchiCuchi.Characters.Skins.Semen.SemenParaAnus;
using SemenVag = Assets._ReusableScripts.CuchiCuchi.Characters.Skins.Semen.SemenParaVag;

namespace CMAScenes
{
    // His semen in her, as the game itself has it, for the X-ray; and the shade the X-ray is seen on.
    //
    // The game keeps two things of it for each of her holes:
    //  - how many millilitres of his semen are in it (SemenParaHole: SemenParaVag, SemenParaAnus), raised as the drops
    //    he lets out in her reach her insides, and lowered as it lets them out of her again: then it is "expulsando",
    //    and the drops that come out of her entrance are its own (strings of drops, or the falling drops of the
    //    Corrupted mod), drawn outside her like anything else;
    //  - a second drawing of her insides with a material of semen on it (ControlladorDeSemenParaInternalHole: m_instance,
    //    m_semenMaterialInstance), shown as full as a weight of its own (m_currentW) says: the game cuts away less of its
    //    picture of the semen the more there is. Nobody sees it: it is inside her.
    // While the X-ray is on, that drawing is given a see-through material with the game's own picture, cut exactly as
    // the game cuts its own; her womb (or her rectum) is drawn as full as that weight; and while the game lets the semen
    // out of her, a stream runs down her canal to her entrance (XRayFlow.cs). So what is shown is the semen the game has
    // in her, not a guess of the plugin's: none in a condom, none when he came outside her.
    //
    // (Up to 0.2.0 nothing of it showed: the game's picture of the semen has its shape in an alpha of 0 to 0.46, and the
    // plugin cut it at 0.47 while there was less than two thirds of the most in her. The picture is now stretched to the
    // whole of 0..1 and cut where the game cuts it.)
    public sealed partial class ShakeController
    {
        private sealed class XSemen
        {
            public GameSemenIn ctl;
            public bool anal;
            public Renderer draws;
            public Il2CppReferenceArray<Material> was, ghosts;
            public Material ghost;
            public bool hasMap;
            public int putBack;
            // How far up the game's picture's alpha goes (it is cut against that), and whether the number the game sets on
            // its material is the cut itself.
            public float top = 1f;
            public bool cutIsGame;
        }

        private readonly List<XSemen> m_xSemenIn = new List<XSemen>();
        private readonly HashSet<IntPtr> m_xSemenToldFor = new HashSet<IntPtr>();
        // How full of it her vagina and her rectum are drawn (eased), and whether the game's own record was found.
        private float m_xFillV, m_xFillA;
        private bool m_xSemenTold;
        // The game's own count of it in her (ml, and the most it has room for), what leaves her (ml a second, by that
        // count going down), whether the game says it is letting it out now, and how much of a stream is drawn (eased).
        private GameHoleSemen m_xHoleV, m_xHoleA;
        private float m_xMlV, m_xMlA, m_xMaxV, m_xMaxA, m_xOutV, m_xOutA, m_xFlowV, m_xFlowA;
        private bool m_xLetsV, m_xLetsA;
        private float m_xMlAt, m_xMlWasV = -1f, m_xMlWasA = -1f;
        private int m_xLetsLogs;

        // The game's picture of the semen, stretched to the whole of 0..1 in its alpha (made once for each picture).
        private static Texture2D s_xSemenPic;
        private static int s_xSemenPicOf;
        private static float s_xSemenPicTop = 1f;

        private void SemenFind()
        {
            m_xSemenIn.Clear();
            m_xHoleV = m_xHoleA = null;
            // (What was read of her last is kept until it is read again: found anew after the sex, she would otherwise
            // have none in her for a frame, and the X-ray that stays for it would go.)
            m_xMlWasV = m_xMlWasA = -1f;
            var all = Resources.FindObjectsOfTypeAll<GameSemenIn>();
            for (int i = 0; all != null && i < all.Length; i++)
            {
                GameSemenIn c = all[i];
                if (c == null || !c.gameObject.scene.IsValid() || c.m_Internals == null)
                {
                    continue;
                }
                IntPtr of = c.m_Internals.Pointer;
                bool vag = m_xVag != null && of == m_xVag.Pointer, anus = m_xAnus != null && of == m_xAnus.Pointer;
                if (vag || anus)
                {
                    m_xSemenIn.Add(new XSemen { ctl = c, anal = anus });
                }
            }
            HoleSemenFind();
            if (!m_xSemenTold)
            {
                m_xSemenTold = true;
                Plugin.ModLog.LogInfo($"X-ray: his semen in her: the game's own drawing of it was found for {m_xSemenIn.Count} of her holes{(m_xSemenIn.Count == 0 ? " (her womb fills by the plugin's own count then)" : "")}; its count of it in her, in millilitres: {(m_xHoleV != null ? "her vagina's" : "not for her vagina")}, {(m_xHoleA != null ? "her anus's" : "not for her anus")}.");
            }
        }

        // The game's count of his semen in each of her holes: by the object that ties it to the drawings found above,
        // else by being hers.
        private void HoleSemenFind()
        {
            try
            {
                var ties = Resources.FindObjectsOfTypeAll<GameSemenHoles>();
                for (int i = 0; ties != null && i < ties.Length; i++)
                {
                    GameSemenHoles t = ties[i];
                    if (t == null || !t.gameObject.scene.IsValid())
                    {
                        continue;
                    }
                    foreach (XSemen s in m_xSemenIn)
                    {
                        if (s.ctl == null)
                        {
                            continue;
                        }
                        if (!s.anal && t.m_ControlladorDeSemenParaVagInternal != null && t.m_ControlladorDeSemenParaVagInternal.Pointer == s.ctl.Pointer && t.m_SemenParaVag != null)
                        {
                            m_xHoleV = t.m_SemenParaVag;
                        }
                        if (s.anal && t.m_ControlladorDeSemenParaAnusInternal != null && t.m_ControlladorDeSemenParaAnusInternal.Pointer == s.ctl.Pointer && t.m_SemenParaAnus != null)
                        {
                            m_xHoleA = t.m_SemenParaAnus;
                        }
                    }
                }
                if ((m_xHoleV == null || m_xHoleA == null) && m_xWoman != null && m_xWoman.character != null)
                {
                    Transform her = m_xWoman.character.transform;
                    Transform root = m_xWoman.hip != null ? m_xWoman.hip.root : null;
                    var holes = Resources.FindObjectsOfTypeAll<GameHoleSemen>();
                    for (int i = 0; holes != null && i < holes.Length; i++)
                    {
                        GameHoleSemen h = holes[i];
                        if (h == null || !h.gameObject.scene.IsValid() || !(h.transform.IsChildOf(her) || (root != null && h.transform.root == root)))
                        {
                            continue;
                        }
                        if (m_xHoleV == null && h.TryCast<SemenVag>() != null)
                        {
                            m_xHoleV = h;
                        }
                        else if (m_xHoleA == null && h.TryCast<SemenAnus>() != null)
                        {
                            m_xHoleA = h;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogInfo("X-ray: his semen in her: the game's count of it could not be found (" + ex.Message + ").");
            }
        }

        // How much of his semen the game's drawing has in a hole of hers, 0 to 1; below 0 when it keeps no record of it.
        private float SemenOfGame(bool anal)
        {
            foreach (XSemen s in m_xSemenIn)
            {
                if (s.anal == anal && s.ctl != null)
                {
                    return Mathf.Clamp01(s.ctl.m_currentW);
                }
            }
            return -1f;
        }

        // The game's count of his semen in one of her holes (ml), how much it has room for, and whether it lets it out.
        private static float HoleMl(GameHoleSemen h, out float most, out bool letting)
        {
            most = 0f;
            letting = false;
            if (h == null)
            {
                return 0f;
            }
            float ml;
            try
            {
                ml = h.MililitrosAcumulados(TipoDeSemen.semen, out most, out float _);
            }
            catch (Exception)
            {
                ml = h.m_acumuladoSemenMililitros;
            }
            try
            {
                letting = h.expulsando;
            }
            catch (Exception)
            {
            }
            return float.IsNaN(ml) ? 0f : Mathf.Max(0f, ml);
        }

        // Every frame the X-ray is on, whatever it shows: the game's count of his semen in her, what of it leaves her,
        // and how full she is drawn.
        private void SemenRead(float dt)
        {
            float now = Time.unscaledTime;
            if (now >= m_xMlAt)
            {
                float span = m_xMlAt > 0f ? Mathf.Clamp(now - m_xMlAt + 0.1f, 0.05f, 1f) : 0.1f;
                m_xMlAt = now + 0.1f;
                bool wasV = m_xLetsV, wasA = m_xLetsA;
                m_xMlV = HoleMl(m_xHoleV, out m_xMaxV, out m_xLetsV);
                m_xMlA = HoleMl(m_xHoleA, out m_xMaxA, out m_xLetsA);
                // (What leaves her: the count going down, a second's worth, smoothed. It goes up when he comes in her.)
                float outV = m_xMlWasV >= 0f ? Mathf.Max(0f, m_xMlWasV - m_xMlV) / span : 0f;
                float outA = m_xMlWasA >= 0f ? Mathf.Max(0f, m_xMlWasA - m_xMlA) / span : 0f;
                m_xMlWasV = m_xMlV;
                m_xMlWasA = m_xMlA;
                m_xOutV = Mathf.Lerp(m_xOutV, outV, 0.35f);
                m_xOutA = Mathf.Lerp(m_xOutA, outA, 0.35f);
                if ((m_xLetsV != wasV || m_xLetsA != wasA) && m_xLetsLogs < 24)
                {
                    m_xLetsLogs++;
                    bool v = m_xLetsV != wasV;
                    bool on = v ? m_xLetsV : m_xLetsA;
                    Plugin.ModLog.LogInfo($"X-ray: the game {(on ? "lets his semen out of" : "stops letting his semen out of")} her {(v ? "vagina" : "anus")} ({(v ? m_xMlV : m_xMlA):F1} of {(v ? m_xMaxV : m_xMaxA):F1} ml in it{(m_in ? ", he is in her" : "")}).");
                }
            }
            // A stream while the game lets it out, or while its count of it goes down: as thick as what leaves her.
            float wantV = m_xLetsV || m_xOutV > 0.03f ? Mathf.Clamp(0.35f + m_xOutV / 1.5f, 0.35f, 1f) : 0f;
            float wantA = m_xLetsA || m_xOutA > 0.03f ? Mathf.Clamp(0.35f + m_xOutA / 1.5f, 0.35f, 1f) : 0f;
            if (m_xMlV <= 0.02f && m_xOutV <= 0.03f) wantV = 0f;
            if (m_xMlA <= 0.02f && m_xOutA <= 0.03f) wantA = 0f;
            m_xFlowV = Mathf.MoveTowards(m_xFlowV, wantV, dt / 0.4f);
            m_xFlowA = Mathf.MoveTowards(m_xFlowA, wantA, dt / 0.4f);
            // How full she is drawn: as the game's own drawing has it; else as its count has it; else by the plugin's own
            // count since he came in her bare.
            float gv = SemenOfGame(false), ga = SemenOfGame(true);
            if (gv < 0f && m_xHoleV != null) gv = m_xMaxV > 0.01f ? Mathf.Clamp01(m_xMlV / m_xMaxV) : 0f;
            if (ga < 0f && m_xHoleA != null) ga = m_xMaxA > 0.01f ? Mathf.Clamp01(m_xMlA / m_xMaxA) : 0f;
            float own = m_xFillBare ? m_xSemen : 0f;
            m_xFillV = Mathf.MoveTowards(m_xFillV, gv >= 0f ? gv : m_xFillAnal ? 0f : own, dt / 3f);
            m_xFillA = Mathf.MoveTowards(m_xFillA, ga >= 0f ? ga : m_xFillAnal ? own : 0f, dt / 3f);
        }

        // Whether the game still has some of his semen in her (or lets it out): the X-ray stays after the sex for that.
        private bool SemenStillInHer => m_xMlV > 0.15f || m_xMlA > 0.15f || m_xFlowV > 0.02f || m_xFlowA > 0.02f;

        // The game's picture of the semen with its alpha stretched to the whole of 0..1: cut where the game cuts its own
        // (against how far its alpha goes), every bit of it that the game shows is shown.
        private static Texture2D SemenPicture(Texture map, out float top)
        {
            top = 1f;
            if (map == null)
            {
                return null;
            }
            if (s_xSemenPic != null && s_xSemenPicOf == map.GetInstanceID())
            {
                top = s_xSemenPicTop;
                return s_xSemenPic;
            }
            Color32[] read = CopyMask(map, true);
            if (read == null)
            {
                return null;
            }
            int most = 0;
            for (int i = 0; i < read.Length; i++)
            {
                most = Math.Max(most, read[i].a);
            }
            top = Mathf.Max(0.02f, most / 255f);
            const int n = 256;
            int step = MaskSize / n;
            var px = new Il2CppStructArray<Color32>(n * n);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    Color32 c = read[(y * step) * MaskSize + x * step];
                    c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(c.a / top), 0, 255);
                    px[y * n + x] = c;
                }
            }
            if (s_xSemenPic != null)
            {
                UnityEngine.Object.Destroy(s_xSemenPic);
            }
            s_xSemenPic = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "CMAScenes.XRaySemen", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Repeat };
            s_xSemenPic.SetPixels32(px);
            s_xSemenPic.Apply(true, false);
            s_xSemenPicOf = map.GetInstanceID();
            s_xSemenPicTop = top;
            return s_xSemenPic;
        }

        private static readonly int AlphaCutoffId = Shader.PropertyToID("_AlphaCutoff");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");

        // Each frame the X-ray shows her through her body: the game's drawing of the semen in her, see-through.
        private void SemenStep(Material from, float dt)
        {
            foreach (XSemen s in m_xSemenIn)
            {
                if (s.ctl == null || from == null)
                {
                    continue;
                }
                float w = Mathf.Clamp01(s.ctl.m_currentW);
                Material own0 = s.ctl.m_semenMaterialInstance;
                if (own0 == null && s.was != null && s.was.Length > 0)
                {
                    own0 = s.was[0];
                }
                if (s.draws == null)
                {
                    // (The game makes its drawing when it is first needed.)
                    SkinnedMeshRenderer inst = s.ctl.m_instance;
                    if (inst == null)
                    {
                        continue;
                    }
                    Il2CppReferenceArray<Material> its = inst.sharedMaterials;
                    if (its == null || its.Length == 0)
                    {
                        continue;
                    }
                    if (own0 == null)
                    {
                        own0 = its[0];
                    }
                    Texture map = null;
                    string mapName = "";
                    foreach (string name in new[] { "_BaseColorMap", "_MainTex", "_BaseColorMap0", "_BaseMap" })
                    {
                        if (own0 != null && own0.HasProperty(name) && own0.GetTexture(name) != null)
                        {
                            map = own0.GetTexture(name);
                            mapName = name;
                            break;
                        }
                    }
                    s.ghost = GhostOf(from, SemenTint, 0.95f, XQueue + 3);
                    Texture2D pic = null;
                    try
                    {
                        pic = SemenPicture(map, out s.top);
                    }
                    catch (Exception)
                    {
                        pic = null;
                    }
                    if (pic != null)
                    {
                        if (s.ghost.HasProperty("_BaseColorMap")) s.ghost.SetTexture("_BaseColorMap", pic);
                        if (s.ghost.HasProperty("_MainTex")) s.ghost.SetTexture("_MainTex", pic);
                        if (s.ghost.HasProperty("_AlphaCutoffEnable")) s.ghost.SetFloat("_AlphaCutoffEnable", 1f);
                        s.ghost.EnableKeyword("_ALPHATEST_ON");
                        s.hasMap = true;
                    }
                    if (s.ghost.HasProperty("_EmissiveColor")) s.ghost.SetColor("_EmissiveColor", new Color(0.62f, 0.6f, 0.54f, 1f));
                    int id = s.ctl.m_ShaderID;
                    s.cutIsGame = id == AlphaCutoffId || id == CutoffId;
                    s.ghosts = new Il2CppReferenceArray<Material>(its.Length);
                    for (int i = 0; i < its.Length; i++)
                    {
                        s.ghosts[i] = s.ghost;
                    }
                    s.was = its;
                    s.draws = inst;
                    inst.sharedMaterials = s.ghosts;
                    if (m_xSemenToldFor.Add(s.ctl.Pointer))
                    {
                        float its0 = float.NaN;
                        try
                        {
                            if (own0 != null && own0.HasProperty(id)) its0 = own0.GetFloat(id);
                        }
                        catch (Exception)
                        {
                        }
                        Plugin.ModLog.LogInfo($"X-ray: his semen in her {(s.anal ? "rectum" : "vagina")}: the game draws it with the mesh '{inst.name}' (shown {inst.enabled}, layer {inst.gameObject.layer}), material '{(own0 != null ? own0.name : "none")}' ({(own0 != null && own0.shader != null ? own0.shader.name : "?")}, queue {(own0 != null ? own0.renderQueue : 0)}), {(map != null ? "picture '" + map.name + "' (" + mapName + "), its alpha up to " + s.top.ToString("F2") + (pic != null ? ", stretched to 1" : ", could not be copied") : "no picture: it is shown all over, as solid as there is of it")}; the game has {w:F2} of it now, the number it sets on its material is {its0:F2} ({(s.cutIsGame ? "its alpha cut: the same cut is used here" : "not its alpha cut: cut here by how much there is")}), its most cut {(s.ctl.config != null ? s.ctl.config.maxAlphaCut : float.NaN):F2}.");
                    }
                }
                else
                {
                    Material now = s.draws.sharedMaterial;
                    if ((now == null || now.Pointer != s.ghost.Pointer) && s.putBack < 60)
                    {
                        s.putBack++;
                        Il2CppReferenceArray<Material> its = s.draws.sharedMaterials;
                        if (its != null && its.Length == s.ghosts.Length)
                        {
                            s.was = its;
                            s.draws.sharedMaterials = s.ghosts;
                        }
                    }
                }
                // Where the game cuts its own picture, against how far that picture's alpha goes; else the more of it
                // there is, the more of the picture is let through. With no picture: the more of it, the more solid.
                if (s.hasMap)
                {
                    float cut;
                    if (w <= 0.005f)
                    {
                        cut = 1.01f;
                    }
                    else if (s.cutIsGame && own0 != null)
                    {
                        cut = Mathf.Clamp(own0.GetFloat(s.ctl.m_ShaderID) / Mathf.Max(0.02f, s.top), 0.02f, 0.95f);
                    }
                    else
                    {
                        cut = Mathf.Lerp(0.85f, 0.05f, Mathf.Sqrt(w));
                    }
                    s.ghost.SetFloat("_AlphaCutoff", cut);
                }
                else
                {
                    s.ghost.SetColor("_BaseColor", new Color(SemenTint.r, SemenTint.g, SemenTint.b, w <= 0.005f ? 0f : Mathf.Lerp(0.25f, 0.95f, w)));
                    s.ghost.SetFloat("_AlphaCutoff", 0.01f);
                }
            }
        }

        // The game's drawings of the semen in her get their own materials back (what was found of them is kept).
        private void SemenOff()
        {
            foreach (XSemen s in m_xSemenIn)
            {
                try
                {
                    if (s.draws != null && s.ghost != null && s.was != null)
                    {
                        Material now = s.draws.sharedMaterial;
                        if (now != null && now.Pointer == s.ghost.Pointer)
                        {
                            s.draws.sharedMaterials = s.was;
                        }
                    }
                    if (s.ghost != null)
                    {
                        UnityEngine.Object.Destroy(s.ghost);
                    }
                }
                catch (Exception)
                {
                }
                s.draws = null;
                s.ghost = null;
                s.ghosts = null;
                s.was = null;
                s.hasMap = false;
                s.putBack = 0;
            }
        }

        // ---- the shade ----
        //
        // Her skin cannot be made see-through. What is drawn over it reads as an X-ray once the place is darkened: a
        // soft dark round, always turned to the view, drawn over her before everything else of the X-ray, in which her
        // insides glow.
        private GameObject m_xShade;
        private Mesh m_xShadeMesh;
        private Material m_xShadeMat;
        private Texture2D m_xShadeTex;
        private float m_xShadeDim;

        private void ShadeMake(Material from, float dim)
        {
            ShadeDrop();
            m_xShadeDim = dim;
            if (dim < 0.02f || m_xLayer < 0)
            {
                return;
            }
            const int n = 64;
            var px = new Il2CppStructArray<Color32>(n * n);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // (Even in its middle, fading out over its outer half.)
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - r) / 0.55f));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            m_xShadeTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "CMAScenes.XRayShade", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            m_xShadeTex.SetPixels32(px);
            m_xShadeTex.Apply(false, true);
            m_xShadeMat = GhostOf(from, new Color(0.012f, 0.014f, 0.03f), dim, XQueue - 2);
            if (m_xShadeMat.HasProperty("_BaseColorMap")) m_xShadeMat.SetTexture("_BaseColorMap", m_xShadeTex);
            if (m_xShadeMat.HasProperty("_MainTex")) m_xShadeMat.SetTexture("_MainTex", m_xShadeTex);
            // (Not "always", as the rest of the X-ray: it is a flat round in the middle of her pelvis, and drawn always it
            // lay over everything round her as well, the space under her and him there. It is drawn only where something
            // is before it: that is her own body, from wherever one looks.)
            if (m_xShadeMat.HasProperty("_ZTestDepthEqualForOpaque")) m_xShadeMat.SetFloat("_ZTestDepthEqualForOpaque", 5f);
            if (m_xShadeMat.HasProperty("_ZTestTransparent")) m_xShadeMat.SetFloat("_ZTestTransparent", 5f);
            if (m_xShadeMat.HasProperty("_Smoothness")) m_xShadeMat.SetFloat("_Smoothness", 0f);
            if (m_xShadeMat.HasProperty("_EmissiveColor")) m_xShadeMat.SetColor("_EmissiveColor", Color.black);
            m_xShadeMesh = new Mesh { name = "CMAScenes.XRayShade", hideFlags = HideFlags.HideAndDontSave };
            m_xShadeMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) };
            m_xShadeMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            m_xShadeMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m_xShadeMesh.RecalculateNormals();
            m_xShadeMesh.RecalculateBounds();
            m_xShade = new GameObject("CMAScenes.XRayShade") { layer = m_xLayer, hideFlags = HideFlags.HideAndDontSave };
            m_xShade.AddComponent<MeshFilter>().sharedMesh = m_xShadeMesh;
            MeshRenderer r2 = m_xShade.AddComponent<MeshRenderer>();
            r2.sharedMaterial = m_xShadeMat;
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r2.receiveShadows = false;
            m_xShade.SetActive(false);
        }

        // Each frame: over the middle of her insides, turned to the view.
        private void ShadeStep(Woman w)
        {
            if (m_xShade == null)
            {
                return;
            }
            Camera view = Camera.main;
            bool can = view != null && w != null && w.sPelvis != null;
            if (m_xShade.activeSelf != can)
            {
                m_xShade.SetActive(can);
            }
            if (!can)
            {
                return;
            }
            Vector3 mid = w.sPelvis.t.position;
            if (m_xVagBones != null && m_xVagBones.Length > 1 && m_xVagBones[0] != null && m_xVagBones[m_xVagBones.Length - 1] != null)
            {
                Vector3 a = m_xVagBones[0].position, b = m_xVagBones[m_xVagBones.Length - 1].position;
                mid = Vector3.Lerp(a, b, 0.75f);
            }
            Transform t = m_xShade.transform;
            t.position = mid;
            t.rotation = view.transform.rotation;
            t.localScale = Vector3.one * 0.4f;
        }

        private void ShadeDrop()
        {
            try
            {
                if (m_xShade != null) UnityEngine.Object.Destroy(m_xShade);
                if (m_xShadeMesh != null) UnityEngine.Object.Destroy(m_xShadeMesh);
                if (m_xShadeMat != null) UnityEngine.Object.Destroy(m_xShadeMat);
                if (m_xShadeTex != null) UnityEngine.Object.Destroy(m_xShadeTex);
            }
            catch (Exception)
            {
            }
            m_xShade = null;
            m_xShadeMesh = null;
            m_xShadeMat = null;
            m_xShadeTex = null;
        }
    }
}
