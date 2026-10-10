using System;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;
using GamePenis = Assets._ReusableScripts.CuchiCuchi.Penis;

namespace CMAScenes
{
    // The hero outside the ride. The condom and the wet look are not things of the ride alone: he can put a condom on
    // standing, and she can take him in her mouth. For that his penis is found and kept track of whenever there is a
    // hero about, not only while he lies under her. (During the ride the ride itself does this: Ride.cs.)
    public sealed partial class ShakeController
    {
        private Character m_watchHero;
        private float m_watchAt;
        private int m_watchFrame = -1;
        private float m_watchCaptureAt;

        // Runs at the end of every frame.
        private void HeroWatchStep()
        {
            if (m_watchFrame == Time.frameCount)
            {
                return;
            }
            m_watchFrame = Time.frameCount;
            try
            {
                if (m_ride && m_hero != null)
                {
                    // The ride has him: he is the one that is watched from now on.
                    m_watchHero = m_hero;
                    return;
                }
                if (Time.unscaledTime >= m_watchAt)
                {
                    // (The man the view is on does not change from one second to the next: while the one that is watched
                    // is still there and still the one the game's camera is on, the scene is not gone through again.)
                    bool same = false;
                    try
                    {
                        same = m_watchHero != null && m_watchHero.gameObject.activeInHierarchy && m_watchHero.cameraAtada != null;
                    }
                    catch (Exception)
                    {
                    }
                    m_watchAt = Time.unscaledTime + (same ? 1f : 3f);
                    Camera cam = same ? null : Camera.main;
                    Character hero = same ? m_watchHero : cam != null ? FindHero(cam.transform.position) : null;
                    if (hero != m_watchHero)
                    {
                        // Another man, or none: what was on the one before is gone with him.
                        CondomReset();
                        WetRestore();
                        PenisForget();
                        m_heroPen = null;
                        Array.Clear(m_wetAlong, 0, WetBins);
                        Array.Clear(m_spitAlong, 0, WetBins);
                        m_watchHero = hero;
                        m_watchCaptureAt = 0f;
                    }
                    // (What could not be found on him is looked for again only every 20 s: each look goes through all of
                    // his meshes, copies maps of his skin and writes a good many lines to the log.)
                    if (hero != null && (!m_penisValid || m_wetParts.Count == 0 || m_heroPen == null) && Time.unscaledTime >= m_watchCaptureAt)
                    {
                        m_watchCaptureAt = Time.unscaledTime + 20f;
                        if (!m_penisValid)
                        {
                            PenisCapture(hero);
                        }
                        if (m_wetParts.Count == 0)
                        {
                            WetCapture(hero);
                        }
                        if (m_heroPen == null)
                        {
                            m_heroPen = hero.GetComponentInChildren<GamePenis>(true);
                            if (m_heroPen == null && hero.transform.root != null)
                            {
                                m_heroPen = hero.transform.root.GetComponentInChildren<GamePenis>(true);
                            }
                        }
                    }
                }
                if (!m_heroReady && m_penisValid)
                {
                    PenisSee();
                    CondomStep(Time.unscaledDeltaTime);
                }
                // With nothing running (no twerk, no ride) the frame's own work stops early and the wet look is not
                // kept up: the latex of a condom put on outside the ride was never drawn on his skin, only its teat
                // showed. It is kept up from here then.
                if (m_wetFrame != Time.frameCount && m_wetParts.Count > 0)
                {
                    UpdateWet(Mathf.Clamp(Time.deltaTime, 0.004f, 0.05f));
                }
            }
            catch (Exception ex)
            {
                m_watchAt = Time.unscaledTime + 10f;
                Plugin.ModLog.LogWarning("The hero could not be kept track of outside the ride: " + ex.Message);
            }
        }

        // Is there a penis for a condom to go on?
        internal bool CondomPossible => m_penisValid && m_wetParts.Count > 0;
    }
}
