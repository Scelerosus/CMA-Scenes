using System;
using Assets.Productos.Juegos.Reception.Scripts.XRays;
using Assets.TValle.BeachGirl.Runtime.XRays;
using UnityEngine;
using PelvisState = Assets.TValle.BeachGirl.Runtime.XRays.XRaysParaFemaleCharacter.EstadoPelvis;

namespace CMAScenes
{
    // The game's own X-ray, switched on for the sex.
    //
    // The game has one (XRaysParaFemaleCharacter on each woman: a camera of its own on her pelvis, shown in a frame of
    // the game's interface; and MainInternalsXRayCamera, which has her insides alone on its layers). It is the player's
    // to switch on with a key of the game, once the agency has reached a level (EnableXRaysSiNivelAlcanzado), and it
    // may be barred for a woman (pelvisIsBlocked). With the view "Game" the plugin's own X-ray stands back and this one
    // is switched on for her while she is on him, for the hole he is at, and switched off again after.
    //
    // Only its signatures are known. What number ChangePelvis takes for which hole is found out by trying: after each
    // call the state it has left her X-ray in is read (off, could not, vagina, anus, both), a wrong hole is switched
    // off again, and the number that gave the right one is kept. Everything it does is written to the log.
    public sealed partial class ShakeController
    {
        private XRaysParaFemaleCharacter m_xGame;
        private EnableXRaysSiNivelAlcanzado m_xGate;
        private Woman m_xGameFor;
        private bool m_xGameOn, m_xGameAnal, m_xGameTold, m_xGameHid, m_xGateLowered;
        private int m_xGameArg = -1, m_xGameTries, m_xGameFails;
        private float m_xGameAt, m_xGateWas;

        private static bool XRayGameWanted => string.Equals(Plugin.XRayView.Value, "Game", StringComparison.OrdinalIgnoreCase);

        private static string GameXRayState(XRaysParaFemaleCharacter x)
        {
            return $"state {x.m_pelvisEstado}, may be shown {x.canShowPelvis}, barred {x.pelvisIsBlocked}, hidden by force {x.forceHidePelvis}";
        }

        // While the X-ray is on with the view "Game": hers is found and switched on for the hole he is at.
        private void GameXRayStep(Woman w)
        {
            if (m_xGameFails > 3)
            {
                return;
            }
            float now = Time.unscaledTime;
            try
            {
                if (w != m_xGameFor)
                {
                    GameXRayOff();
                    m_xGameFor = w;
                    m_xGame = null;
                    m_xGate = null;
                    m_xGameTries = 0;
                    m_xGameAt = 0f;
                    m_xGameArg = -1;
                }
                if (m_xGameOn && m_xGameAnal != m_holeAnal)
                {
                    // (The other hole was picked: the one that is shown is switched off, the other on.)
                    GameXRayOff();
                    m_xGameFor = w;
                    m_xGameTries = 0;
                    m_xGameAt = 0f;
                }
                if (m_xGameOn || now < m_xGameAt || m_xGameTries >= 8)
                {
                    return;
                }
                m_xGameAt = now + 2f;
                m_xGameTries++;
                if (m_xGame == null)
                {
                    m_xGame = w.character.GetComponentInChildren<XRaysParaFemaleCharacter>(true);
                    if (m_xGame == null)
                    {
                        var all = Resources.FindObjectsOfTypeAll<XRaysParaFemaleCharacter>();
                        for (int i = 0; all != null && i < all.Length; i++)
                        {
                            XRaysParaFemaleCharacter x = all[i];
                            if (x != null && x.gameObject.scene.IsValid() && x.m_pelvis != null && w.hip != null && (x.m_pelvis.IsChildOf(w.hip.root) || x.transform.root == w.hip.root))
                            {
                                m_xGame = x;
                                break;
                            }
                        }
                    }
                    if (m_xGame == null)
                    {
                        if (!m_xGameTold)
                        {
                            m_xGameTold = true;
                            Plugin.ModLog.LogInfo("X-ray: the game's own: she has none (no XRaysParaFemaleCharacter on her).");
                            Notice(Loc.T("The game has no X-ray of its own for her: pick another view on the Sex page.", "У игры нет своего X-ray для неё: выберите другой вид на странице «Секс»."));
                        }
                        return;
                    }
                    Plugin.ModLog.LogInfo($"X-ray: the game's own: found on '{m_xGame.gameObject.name}': {GameXRayState(m_xGame)}.");
                }
                XRaysParaFemaleCharacter g = m_xGame;
                // The level the agency has to have reached for it: taken down for as long as this is on.
                if (!g.canShowPelvis && !m_xGateLowered)
                {
                    if (m_xGate == null)
                    {
                        var gates = Resources.FindObjectsOfTypeAll<EnableXRaysSiNivelAlcanzado>();
                        for (int i = 0; gates != null && i < gates.Length; i++)
                        {
                            EnableXRaysSiNivelAlcanzado gate = gates[i];
                            if (gate != null && gate.gameObject.scene.IsValid() && (gate.m_XRaysParaFemaleCharacter == null || gate.m_XRaysParaFemaleCharacter == g))
                            {
                                m_xGate = gate;
                                if (gate.m_XRaysParaFemaleCharacter == g)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    if (m_xGate != null)
                    {
                        m_xGateWas = m_xGate.nivelParaPelvis;
                        m_xGate.nivelParaPelvis = 0f;
                        m_xGateLowered = true;
                        Plugin.ModLog.LogInfo($"X-ray: the game's own: it is not to be shown yet (the agency's level: {m_xGateWas:F0} is asked for). The level asked for is taken down to 0 while the X-ray is on; the game looks at it again in a moment.");
                        return;
                    }
                }
                if (g.forceHidePelvis)
                {
                    g.forceHidePelvis = false;
                    m_xGameHid = true;
                }
                PelvisState want = m_holeAnal ? PelvisState.anus : PelvisState.vagina;
                PelvisState other = m_holeAnal ? PelvisState.vagina : PelvisState.anus;
                PelvisState was = g.m_pelvisEstado;
                if (was == want || was == PelvisState.vaginaAndAnus)
                {
                    Plugin.ModLog.LogInfo($"X-ray: the game's own: it is on already ({was}); left as the player has it.");
                    m_xGameTries = 8;
                    return;
                }
                // Which number is which hole is not known: tried in turn, by what state each leaves.
                var said = new System.Text.StringBuilder();
                bool done = false;
                int first = m_xGameArg >= 0 ? m_xGameArg : 0;
                for (int k = 0; k < 5 && !done; k++)
                {
                    int arg = (first + k) % 5;
                    g.ChangePelvis(true, arg);
                    PelvisState got = g.m_pelvisEstado;
                    said.Append(said.Length > 0 ? "; " : "").Append("ChangePelvis(true, ").Append(arg).Append(") left ").Append(got.ToString());
                    if (got == want || got == PelvisState.vaginaAndAnus)
                    {
                        m_xGameArg = arg;
                        done = true;
                    }
                    else if (got == other || (got != was && got != PelvisState.off && got != PelvisState.noPudoOff))
                    {
                        g.ChangePelvis(false, arg);
                        said.Append(" (the wrong hole: off again, ").Append(g.m_pelvisEstado.ToString()).Append(')');
                    }
                }
                if (done)
                {
                    m_xGameOn = true;
                    m_xGameAnal = m_holeAnal;
                    var cam = g.m_XRayPelvisCamera;
                    Camera c = cam != null ? cam.m_camera : null;
                    Plugin.ModLog.LogInfo($"X-ray: the game's own: switched on for her {(m_holeAnal ? "anus" : "vagina")}: {said}. {GameXRayState(g)}; its camera {(c != null ? $"'{c.name}' (on {c.enabled}, {(c.orthographic ? "flat, size " + c.orthographicSize.ToString("F3") : "field of view " + c.fieldOfView.ToString("F0"))}, to a picture {(c.targetTexture != null ? c.targetTexture.width + "x" + c.targetTexture.height : "of the screen")})" : "is not there")}.");
                }
                else
                {
                    Plugin.ModLog.LogInfo($"X-ray: the game's own: could not be switched on (try {m_xGameTries} of 8): {said}. {GameXRayState(g)}.");
                    if (m_xGameTries == 8)
                    {
                        Notice(g.pelvisIsBlocked
                            ? Loc.T("The game bars its own X-ray of her now: pick another view on the Sex page.", "Игра сейчас не даёт показать свой X-ray: выберите другой вид на странице «Секс».")
                            : Loc.T("The game's own X-ray did not come on: pick another view on the Sex page.", "Собственный X-ray игры не включился: выберите другой вид на странице «Секс»."));
                    }
                }
            }
            catch (Exception ex)
            {
                m_xGameFails++;
                Plugin.ModLog.LogWarning("X-ray: the game's own: failed (" + ex.Message + ")" + (m_xGameFails > 3 ? "; no longer tried." : "."));
            }
        }

        // What was switched on here is switched off again, and what was changed for it is put back.
        private void GameXRayOff()
        {
            try
            {
                if (m_xGameOn && m_xGame != null)
                {
                    PelvisState before = m_xGame.m_pelvisEstado;
                    m_xGame.ChangePelvis(false, m_xGameArg >= 0 ? m_xGameArg : 0);
                    PelvisState after = m_xGame.m_pelvisEstado;
                    if (after != PelvisState.off && after != PelvisState.noPudoOff && after == before)
                    {
                        // (Not off by that: by its own switch then.)
                        m_xGame.TogglePelvis();
                        after = m_xGame.m_pelvisEstado;
                    }
                    Plugin.ModLog.LogInfo($"X-ray: the game's own: switched off ({before} to {after}).");
                }
                if (m_xGameHid && m_xGame != null)
                {
                    m_xGame.forceHidePelvis = true;
                }
                if (m_xGateLowered && m_xGate != null)
                {
                    m_xGate.nivelParaPelvis = m_xGateWas;
                }
            }
            catch (Exception)
            {
            }
            m_xGameOn = false;
            m_xGameHid = false;
            m_xGateLowered = false;
            m_xGameFor = null;
        }
    }
}
