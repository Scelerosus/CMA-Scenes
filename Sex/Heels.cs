using System;
using Assets.Base.RootMotion.BeachGirl.Runtime.FinalIk.HighHeelScripts;
using UnityEngine;

namespace CMAScenes
{
    // High heels in the sex.
    //
    // The game has a system for them (FemaleHighHeelSystem, a FinalIK one): while she wears heels it stands her on her toes
    // and lifts her pelvis by the height of the heel, after the plugin has posed her. Her hips are put back where they
    // were put right after that, in the same frame (PoseFix.cs), so she keeps her heels and still comes down onto him.
    //
    // Only as a way out (the setting "FlatFeetInHeels") is her heel system let off for the sex: its weight is zero, she
    // stands flat with her feet in her shoes, and it is given back as it was when the sex is over.
    public sealed partial class ShakeController
    {
        private FemaleHighHeelSystem m_heelSys;
        private Woman m_heelWoman;
        private float m_heelWeightWas;
        private bool m_heelHeld, m_heelWarned, m_heelSearched, m_heelsTold;

        // What her heel system says of her heels, for the log (empty when she has none).
        private string HeelsText()
        {
            try
            {
                if (m_heelSys == null)
                {
                    return m_heelSearched ? "she has no heel system" : "";
                }
                return $"her heel system: weight {m_heelSys.weight:F2}, heel {m_heelSys.m_realHeelLocalHeight * 100f:F1} cm (now {m_heelSys.m_currentHeelLocalHeight * 100f:F1}), toe {m_heelSys.m_realToeLocalHeight * 100f:F1} cm";
            }
            catch (Exception)
            {
                return "";
            }
        }

        private void HeelsStep()
        {
            Woman w = m_rideWoman;
            bool riding = m_ride && m_toggled && !m_switching && w != null && w.character != null;
            if (!riding)
            {
                HeelsRestore();
                return;
            }
            try
            {
                if (w != m_heelWoman)
                {
                    HeelsRestore();
                    m_heelWoman = w;
                    m_heelSearched = false;
                    m_heelsTold = false;
                }
                if (!m_heelSearched)
                {
                    m_heelSearched = true;
                    m_heelSys = w.character.GetComponentInChildren<FemaleHighHeelSystem>(true);
                    Plugin.ModLog.LogInfo("Ride: heels: " + (m_heelSys != null ? HeelsText() + "." : "she has no heel system."));
                }
                if (m_heelSys == null)
                {
                    return;
                }
                bool flat = Plugin.RideFlatFeet.Value;
                if (flat)
                {
                    if (!m_heelHeld)
                    {
                        m_heelHeld = true;
                        m_heelWeightWas = m_heelSys.weight;
                        Plugin.ModLog.LogInfo($"Ride: heels: her heel system is let off for the sex (its weight was {m_heelWeightWas:F2}): she stands flat.");
                    }
                    if (m_heelSys.weight != 0f)
                    {
                        m_heelSys.weight = 0f;
                    }
                }
                else if (m_heelHeld)
                {
                    m_heelHeld = false;
                    m_heelSys.weight = m_heelWeightWas;
                }
            }
            catch (Exception ex)
            {
                m_heelSys = null;
                m_heelHeld = false;
                if (!m_heelWarned)
                {
                    m_heelWarned = true;
                    Plugin.ModLog.LogWarning("Ride: heels: her heel system could not be read: " + ex.Message);
                }
            }
        }

        private void HeelsRestore()
        {
            try
            {
                if (m_heelHeld && m_heelSys != null)
                {
                    m_heelSys.weight = m_heelWeightWas;
                    Plugin.ModLog.LogInfo($"Ride: heels: her heel system is given back (weight {m_heelWeightWas:F2}).");
                }
            }
            catch (Exception)
            {
            }
            m_heelHeld = false;
            m_heelSys = null;
            m_heelWoman = null;
            m_heelSearched = false;
        }
    }
}
