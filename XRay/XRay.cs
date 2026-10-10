using System;
using Assets._ReusableScripts.CuchiCuchi.Holes.Internals;
using UnityEngine;
using GameHole = Assets._ReusableScripts.CuchiCuchi.PhysicsAndBonesScripts.BoneStretchedChain;

namespace CMAScenes
{
    // The X-ray: seeing him move in her.
    //
    // The game has her insides: for each of her holes a skinned mesh of its own (HoleInternal: VagInternals with her
    // canal, the neck of her womb and her womb; AnusInternals with her anus, her rectum and her gut), whose bones the
    // game moves and stretches round whatever is in her, with springs, every frame. They are drawn like anything else,
    // inside her body, where nobody sees them.
    //
    // The X-ray shows them, two ways at once (either can be had alone, by the setting "View"):
    //  - through her body (XRayGhost.cs, XRayWomb.cs): her insides, her womb and what of his penis is in her are drawn
    //    over her skin, half see-through, wherever one looks from;
    //  - an inset on the screen (XRayInset.cs): a cut through her from front to back, painted from where the bones of
    //    her insides and of his penis are in that frame, with how deep he is in her.
    // And there is a third view, "Game": the game has an X-ray of its own (a camera on her pelvis, in a frame of its
    // interface), which is the player's to switch on once the agency has reached a level. With that view it is the
    // game's that is switched on for the sex, and the two above stand back (XRayGame.cs).
    //
    // It is there for the sex of this plugin (while she is on him), on a key and a switch in the window; and after it, on
    // her, for as long as the game still has his semen in her (XRay/AfterSex): the game lets it out of her once nothing
    // is in her any more, and that is what is to be watched (XRaySemen.cs, XRayFlow.cs). Nothing of the game's is
    // changed for good: the materials of her insides are given back when it is switched off.
    public sealed partial class ShakeController
    {
        // Her insides as the game has them (either may be missing), and whose they are.
        private HoleInternal m_xVag, m_xAnus;
        private VagInternals m_xVagIn;
        private AnusInternals m_xAnusIn;
        private Woman m_xWoman;
        private GameHole m_xFor;
        private float m_xSeekAt;
        private int m_xSeeks;
        private bool m_xFoundTold, m_xHadVag, m_xHadAnus;
        // How much of it there is now (it fades in and out), and how much of his semen is in her.
        private float m_xW, m_xSemen;
        // His semen in her as it shows through her body: how full she is drawn (it rises over a few seconds as he
        // comes, and goes as m_xSemen does), and whether it is her rectum it is in.
        private bool m_xFillAnal, m_xFillBare;
        private int m_xFails;
        // After the sex: until when it may stay on her, on whom, and whether that is what it is now.
        private float m_xSexUntil;
        private Woman m_xSexWoman;
        private bool m_xAfter, m_xAfterTold;

        internal bool XRayOn => Plugin.XRay.Value;

        // The key, or the switch in the window.
        internal void ToggleXRay()
        {
            Plugin.XRay.Value = !Plugin.XRay.Value;
            try
            {
                Plugin.Cfg.Save();
            }
            catch (Exception)
            {
            }
            bool sex = m_ride && m_toggled && !m_switching;
            Notice(Plugin.XRay.Value
                ? (sex ? Loc.T("X-ray: on.", "X-ray: включён.") : Loc.T("X-ray: on (shown during the sex).", "X-ray: включён (виден во время секса)."))
                : Loc.T("X-ray: off.", "X-ray: выключен."));
        }

        private static bool XRayThrough => !XRayGameWanted && !string.Equals(Plugin.XRayView.Value, "Inset", StringComparison.OrdinalIgnoreCase);
        private static bool XRayInsetWanted => !XRayGameWanted && !string.Equals(Plugin.XRayView.Value, "Through", StringComparison.OrdinalIgnoreCase);

        // He came (Climax.cs): in her, his semen is in the picture for a while.
        private void XRayHeCame(bool inHer)
        {
            if (inHer)
            {
                m_xSemen = 1f;
                m_xFillAnal = m_holeAnal;
                m_xFillBare = m_condom != 4;
            }
        }

        // Her insides are looked for: those whose hole is one of the two the sex has found on her.
        private void XRayFind(Woman w)
        {
            m_xVag = m_xAnus = null;
            m_xVagIn = null;
            m_xAnusIn = null;
            m_xWoman = w;
            m_xFor = m_vagHole;
            if (w == null || w.character == null)
            {
                return;
            }
            GameHole mine = m_vagHole, other = m_otherHole;
            var all = Resources.FindObjectsOfTypeAll<HoleInternal>();
            int seen = 0;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                HoleInternal hi = all[i];
                if (hi == null || !hi.gameObject.scene.IsValid())
                {
                    continue;
                }
                seen++;
                GameHole hole = hi.m_hole;
                bool hers = hole != null && ((mine != null && hole.Pointer == mine.Pointer) || (other != null && hole.Pointer == other.Pointer));
                if (!hers && mine == null)
                {
                    // (The sex has not found her holes: hers are the ones under her.)
                    hers = hi.transform.IsChildOf(w.character.transform) || (w.hip != null && hi.transform.root == w.hip.root);
                }
                if (!hers)
                {
                    continue;
                }
                VagInternals v = hi.TryCast<VagInternals>();
                AnusInternals a = hi.TryCast<AnusInternals>();
                if (v != null)
                {
                    m_xVag = hi;
                    m_xVagIn = v;
                }
                else if (a != null)
                {
                    m_xAnus = hi;
                    m_xAnusIn = a;
                }
            }
            m_xHadVag = m_xVag != null;
            m_xHadAnus = m_xAnus != null;
            if (!m_xFoundTold || m_xVag != null || m_xAnus != null)
            {
                m_xFoundTold = true;
                Plugin.ModLog.LogInfo($"X-ray: her insides as the game has them: {(m_xVag != null ? "her vagina's ('" + m_xVag.gameObject.name + "')" : "none for her vagina")}, {(m_xAnus != null ? "her anus's ('" + m_xAnus.gameObject.name + "')" : "none for her anus")} (of {seen} in the scene).{(m_xVag == null && m_xAnus == null ? " The inset is painted from her holes' own entrance and bottom then, and nothing of hers is shown through her body." : "")}");
            }
            // (His semen in her as the game has it: for every view of the X-ray.)
            SemenFind();
        }

        // Once a frame, at its end (she and he are where they are drawn): the X-ray is kept up, or taken down.
        internal void XRayStep()
        {
            if (m_xFails > 5)
            {
                return;
            }
            try
            {
                float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.004f, 0.05f);
                float now = Time.unscaledTime;
                Woman w = m_rideWoman;
                bool sex = Plugin.XRay.Value && m_ride && m_toggled && !m_switching && m_heroReady && w != null && w.character != null && m_poseW > 0.5f && m_penisValid;
                if (sex)
                {
                    m_xSexUntil = now + Mathf.Clamp(Plugin.XRayAfter.Value, 0f, 600f);
                    m_xSexWoman = w;
                    m_xAfterTold = false;
                }
                else if (!Plugin.XRay.Value || m_toggled)
                {
                    // (Switched off, or another sex or a twerk begun: nothing is kept for after.)
                    m_xSexUntil = 0f;
                }
                // After the sex, on her: for as long as the game still has his semen in her (it lets it out of her now
                // that nothing is in her), up to a while. The game's X-ray stays out of it.
                bool after = !sex && Plugin.XRay.Value && !m_toggled && w != null && w == m_xSexWoman && w == m_xWoman && w.character != null
                    && now < m_xSexUntil && SemenStillInHer && !XRayGameWanted;
                if (after && !m_xAfterTold)
                {
                    m_xAfterTold = true;
                    Plugin.ModLog.LogInfo($"X-ray: the sex is over; it stays on her while the game still has his semen in her (vagina {m_xMlV:F1} ml, anus {m_xMlA:F1} ml), for {Mathf.Max(0f, m_xSexUntil - now):F0} s at most.");
                }
                m_xAfter = after;
                bool on = sex || after;
                m_xW = Mathf.MoveTowards(m_xW, on ? 1f : 0f, dt / 0.35f);
                // (His semen in her by the plugin's own count, where the game keeps none: it stays while he is in her,
                // and is gone a while after he is out.)
                m_xSemen = Mathf.Max(0f, m_xSemen - dt / (m_in ? 150f : 25f));
                if (!on)
                {
                    GhostOff();
                    GameXRayOff();
                    if (m_xW <= 0f)
                    {
                        InsetOff();
                        m_xWoman = null;
                        m_xSeeks = 0;
                    }
                    return;
                }
                // (Hers are gone: another scene, or she was made anew.)
                bool lost = (m_xHadVag && m_xVag == null) || (m_xHadAnus && m_xAnus == null);
                if (w != m_xWoman || lost || m_xFor != m_vagHole || (m_xVag == null && m_xAnus == null && now >= m_xSeekAt && m_xSeeks < 6))
                {
                    // (Whatever was shown through her is taken down first: it is put up again for what is found.)
                    GhostOff();
                    if (w != m_xWoman || lost || m_xFor != m_vagHole)
                    {
                        m_xSeeks = 0;
                    }
                    m_xSeeks++;
                    m_xSeekAt = now + 3f;
                    XRayFind(w);
                    InsetNew();
                }
                SemenRead(dt);
                if (XRayGameWanted)
                {
                    GameXRayStep(w);
                }
                else
                {
                    GameXRayOff();
                }
                if (XRayThrough)
                {
                    GhostStep(w);
                }
                else
                {
                    GhostOff();
                }
                if (XRayInsetWanted)
                {
                    InsetStep(w);
                }
                else
                {
                    InsetOff();
                }
            }
            catch (Exception ex)
            {
                m_xFails++;
                Plugin.ModLog.LogWarning("X-ray: failed (" + ex.Message + ")" + (m_xFails > 5 ? "; switched off for this session of the game." : "."));
                try
                {
                    GhostOff();
                    GameXRayOff();
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
