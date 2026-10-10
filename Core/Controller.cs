using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers.Interacciones;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace CMAScenes
{
    public sealed partial class ShakeController : MonoBehaviour
    {
        private bool m_toggled;
        private float m_shakeAt;
        private float m_poseW, m_poseVel;
        private float m_shakeW, m_shakeVel;
        private float m_nextScan;
        private GUIStyle m_style;

        private string m_notice = "";
        private float m_noticeUntil;

        private void Notice(string text)
        {
            m_notice = text;
            m_noticeUntil = Time.unscaledTime + 3f;
            Plugin.ModLog.LogInfo(text);
        }

        public ShakeController(IntPtr pointer) : base(pointer)
        {
        }

        private bool m_reapplyPending;
        private float m_reapplyAt;

        // The game hides and locks the mouse pointer while you play. Its own windows show it through this driver, which
        // also holds the game's input (camera look, movement), so the settings window uses the same one.
        private Assets.NewFeatures.Common.CursorInputDriver m_cursorDriver;
        private bool m_cursorForced;
        private bool m_cursorWarned;
        private CursorLockMode m_prevLock;
        private bool m_prevVisible;

        internal void SetMenuCursor(bool shown)
        {
            if (shown)
            {
                m_prevLock = Cursor.lockState;
                m_prevVisible = Cursor.visible;
            }
            try
            {
                if (m_cursorDriver == null)
                {
                    m_cursorDriver = new Assets.NewFeatures.Common.CursorInputDriver(this);
                }
                m_cursorDriver.SetCursorShown(shown);
            }
            catch (Exception ex)
            {
                if (!m_cursorWarned)
                {
                    m_cursorWarned = true;
                    Plugin.ModLog.LogWarning("Could not use the game's cursor driver, showing the pointer directly: " + ex.Message);
                }
            }
            if (shown)
            {
                m_cursorForced = true;
                KeepCursor();
            }
            else if (m_cursorForced)
            {
                m_cursorForced = false;
                Cursor.lockState = m_prevLock;
                Cursor.visible = m_prevVisible;
            }
        }

        // Called every frame while the window is open: the game puts the pointer away again when it can.
        internal void KeepCursor()
        {
            if (m_cursorForced)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        // The mode she is in, or is on her way into while one is being changed for the other.
        private bool m_switching;
        private bool m_switchRide;
        private bool HeadedForRide => m_switching ? m_switchRide : m_ride;

        internal bool Twerking => m_toggled && !HeadedForRide;
        internal bool Riding => m_toggled && HeadedForRide;

        // Starts or stops the twerk, or the ride, the same way their keys do.
        internal void ToggleMode(bool ride)
        {
            SetActive(ride ? !Riding : !Twerking, ride);
        }

        // What begins a mode: the bounce starts a moment after the pose, the rhythm starts over, and the ride finds its woman.
        private void EnterMode()
        {
            m_shakeAt = Time.time + Plugin.ShakeDelay.Value;
            ResetDirector();
            if (m_ride)
            {
                StartRide();
            }
        }

        // Puts the bikini on or takes it off, the same way its key does.
        internal void ToggleBikini()
        {
            bool on = !Bikini.AnyOn();
            if (on && string.IsNullOrWhiteSpace(Plugin.BikiniTop.Value) && string.IsNullOrWhiteSpace(Plugin.BikiniBottom.Value))
            {
                Notice(Loc.T("Pick a top or a bottom first.", "Сначала выберите верх или низ бикини."));
                return;
            }
            m_reapplyPending = false;
            // Put on or taken off by hand: it is hers now, the mode no longer takes it off.
            m_bikiniByMode = false;
            Bikini.Set(this, on);
        }

        // After another piece or colour mode is picked while she wears the bikini: take it off and put it on again.
        internal void ReapplyBikini()
        {
            if (!Bikini.AnyOn() && !m_reapplyPending)
            {
                return;
            }
            if (Bikini.AnyOn())
            {
                Bikini.Set(this, false);
            }
            m_reapplyPending = true;
            m_reapplyAt = Time.unscaledTime + 0.5f;
        }

        private void Update()
        {
            try
            {
                if (m_reapplyPending && Time.unscaledTime >= m_reapplyAt)
                {
                    m_reapplyPending = false;
                    Bikini.Set(this, true);
                }
                // Typing in a text box of the window ("j" included) must not open, close or trigger anything.
                if (Menu.TypingText)
                {
                    return;
                }
                if (Plugin.MenuKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.MenuKey.Value))
                {
                    Menu.Toggle(this);
                }
                if (Plugin.PresetKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.PresetKey.Value))
                {
                    CyclePreset();
                }
                if (Plugin.CondomKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.CondomKey.Value))
                {
                    ToggleCondom();
                }
                if (Plugin.XRayKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.XRayKey.Value))
                {
                    ToggleXRay();
                }
                if (Plugin.PlacePickKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.PlacePickKey.Value))
                {
                    PlacePick();
                }
                if (Plugin.BikiniKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.BikiniKey.Value))
                {
                    ToggleBikini();
                }
                if (Plugin.ToggleKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.ToggleKey.Value))
                {
                    SetActive(!Twerking, false);
                }
                if (Plugin.RideKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.RideKey.Value))
                {
                    SetActive(!Riding, true);
                }
                if (Plugin.FasterKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.FasterKey.Value))
                {
                    ChangeSpeed(1);
                }
                if (Plugin.SlowerKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.SlowerKey.Value))
                {
                    ChangeSpeed(-1);
                }
            }
            catch (Exception ex)
            {
                if (m_inputWarns++ < 5)
                {
                    Plugin.ModLog.LogWarning("Input failed: " + ex.Message);
                }
            }
        }

        private int m_inputWarns;

        // The speed keys: a step faster or slower for the mode that is running (the ride when neither is), within the
        // range of its slider in the window. Saved at once, like a slider let go.
        internal void ChangeSpeed(int dir)
        {
            bool ride = !Twerking;
            ConfigEntry<float> e = ride ? Plugin.RideFrequency : Plugin.Frequency;
            float step = ride ? 0.1f : 0.2f;
            float min = ride ? 0.3f : 0.1f, max = ride ? 2.5f : 5f;
            float next = Mathf.Clamp(Mathf.Round((e.Value + dir * step) / step) * step, min, max);
            if (Mathf.Abs(next - e.Value) < 1e-4f)
            {
                Notice(Loc.T(dir > 0 ? "Already as fast as it goes." : "Already as slow as it goes.", dir > 0 ? "Быстрее уже нельзя." : "Медленнее уже нельзя."));
                return;
            }
            e.Value = next;
            try
            {
                Plugin.Cfg.Save();
            }
            catch (Exception)
            {
            }
            Notice((ride ? Loc.T("Tempo of the sex: ", "Темп секса: ") : Loc.T("Tempo of the twerk: ", "Темп тверка: ")) + next.ToString("0.0") + Loc.T(" a second", " в секунду"));
        }

        // Starts, stops or switches between the two modes: the twerk and the ride.
        private void SetActive(bool on, bool ride)
        {
            bool wasOn = m_toggled;
            m_nextScan = 0f;
            if (on && wasOn)
            {
                // Already going, so this is a change of mode. She is never thrown from one pose into the other: she comes out
                // of the one she is in first, and only then goes into the new one (RunFrame does the second half).
                m_switching = ride != m_ride;
                m_switchRide = ride;
                Plugin.ModLog.LogInfo(m_switching ? (ride ? "Twerk -> Ride" : "Ride -> Twerk") + ": coming out of the pose first" : "Staying in " + (m_ride ? "Ride" : "Twerk"));
                return;
            }
            m_toggled = on;
            m_switching = false;
            if (on)
            {
                if (ride != m_ride && m_poseW > 0.02f)
                {
                    // Turned on again while she is still coming out of the other mode: that is finished first.
                    m_switching = true;
                    m_switchRide = ride;
                }
                else
                {
                    m_ride = ride;
                    EnterMode();
                }
            }
            Plugin.ModLog.LogInfo((ride ? "Ride " : "Twerk ") + (on ? "on" : "off"));
            if (!on && wasOn && m_ride && SessionText().Length > 0)
            {
                Plugin.ModLog.LogInfo("Ride: the tally: " + SessionText() + ".");
            }
            if (on && !wasOn)
            {
                // Put on for the mode, if she is not wearing it already and there is one picked. Only a bikini put on here is
                // taken off again when the mode stops: one she was already wearing stays on her.
                m_bikiniByMode = Plugin.BikiniWithTwerk.Value && !Bikini.AnyOn()
                    && !(string.IsNullOrWhiteSpace(Plugin.BikiniTop.Value) && string.IsNullOrWhiteSpace(Plugin.BikiniBottom.Value));
                if (m_bikiniByMode)
                {
                    Bikini.Set(this, true);
                }
            }
            else if (!on && wasOn && m_bikiniByMode)
            {
                m_bikiniByMode = false;
                m_reapplyPending = false;
                Bikini.Set(this, false);
            }
        }

        internal static ShakeController Instance;
        private int m_lastFrame = -1;
        private bool m_bikiniByMode;

        // The game runs everything on its own ordered update stages (GlobalUpdater). Posing her at the first late
        // stage of the frame, before the IK, hair, constraint and collider stages, makes all of them see the posed body.
        private static readonly string[] LateStagePrefixes =
        {
            "lateUpdate", "onLookAt", "afterLookAt", "beforeOral", "onOral", "afterOral",
            "beforeAnimation", "onAnimation", "afterAnimation", "beforeDynamic", "onDynamic", "afterDynamic",
            "meshGeneral", "meshVertex", "meshUpdate", "onAI"
        };

        private readonly Dictionary<int, bool> m_isLateStage = new Dictionary<int, bool>();
        private long m_subscribedTo;
        private Il2CppSystem.Action<Assets._ReusableScripts.Globales.Updater.GlobalUpdater.UpdateType> m_stageCallback;
        private bool m_loggedStage;
        // Whether the game's stages are really had (when they could not be, everything is done from LateUpdate).
        private bool m_stagesLive;

        private void TrySubscribe()
        {
            var updater = Assets._ReusableScripts.Globales.Updater.GlobalUpdater.m_Singleton;
            if (updater == null)
            {
                return;
            }
            long ptr = updater.Pointer.ToInt64();
            if (ptr == m_subscribedTo)
            {
                return;
            }
            try
            {
                if (m_stageCallback == null)
                {
                    m_stageCallback = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Assets._ReusableScripts.Globales.Updater.GlobalUpdater.UpdateType>>(
                        new Action<Assets._ReusableScripts.Globales.Updater.GlobalUpdater.UpdateType>(OnStage));
                }
                updater.add_updatingType(m_stageCallback);
                m_subscribedTo = ptr;
                m_stagesLive = true;
                Plugin.ModLog.LogInfo("Subscribed to the game's update stages.");
            }
            catch (Exception ex)
            {
                m_subscribedTo = ptr;
                m_stagesLive = false;
                Plugin.ModLog.LogWarning("Could not subscribe to the game's update stages, posing in LateUpdate instead: " + ex.Message);
            }
        }

        private void OnStage(Assets._ReusableScripts.Globales.Updater.GlobalUpdater.UpdateType stage)
        {
            try
            {
                int key = (int)stage;
                if (!m_isLateStage.TryGetValue(key, out bool late))
                {
                    string name = stage.ToString();
                    late = false;
                    foreach (string prefix in LateStagePrefixes)
                    {
                        if (name.StartsWith(prefix, StringComparison.Ordinal))
                        {
                            late = true;
                            break;
                        }
                    }
                    m_isLateStage[key] = late;
                }
                Prof.B("HoldStage");
                HoldStage(stage, late);
                Prof.E("HoldStage");
                if (stage == Assets._ReusableScripts.Globales.Updater.GlobalUpdater.UpdateType.lateUpdateAfterCameraController)
                {
                    Prof.B("HeroFinal");
                    HeroFinal();
                    Prof.E("HeroFinal");
                    Prof.B("HerBodiesFinal");
                    HerBodiesFinal();
                    Prof.E("HerBodiesFinal");
                    Prof.B("HeroWatchStep");
                    HeroWatchStep();
                    Prof.E("HeroWatchStep");
                    Prof.B("StrandsStep");
                    StrandsStep();
                    Prof.E("StrandsStep");
                    PoseFixMeasure();
                    Prof.B("AimMeasure");
                    AimMeasure();
                    Prof.E("AimMeasure");
                    Prof.B("HoldCapture");
                    HoldCapture();
                    Prof.E("HoldCapture");
                    Prof.B("XRayStep");
                    XRayStep();
                    Prof.E("XRayStep");
                }
                // Right after the game's inverse kinematics (its heel system among them): her hips back where they were put.
                else if (stage == Assets._ReusableScripts.Globales.Updater.GlobalUpdater.UpdateType.lateUpdateAfterFinalIK)
                {
                    Prof.B("PoseBack");
                    PoseBack();
                    Prof.E("PoseBack");
                }
                // Once the game has turned her head (and then her eyes) to whatever it has her look at: to him instead.
                else if (stage == Assets._ReusableScripts.Globales.Updater.GlobalUpdater.UpdateType.afterLookAtPupet)
                {
                    Prof.B("HeadLate");
                    HeadLate();
                    Prof.E("HeadLate");
                }
                else if (stage == Assets._ReusableScripts.Globales.Updater.GlobalUpdater.UpdateType.afterLookAtEyes)
                {
                    Prof.B("EyesLate");
                    EyesLate();
                    Prof.E("EyesLate");
                }
                if (late && RunFrame(true) && !m_loggedStage && m_poseW > 0f)
                {
                    m_loggedStage = true;
                    Plugin.ModLog.LogInfo($"Posing from the game's '{stage}' stage.");
                }
            }
            catch (Exception ex)
            {
                // (It runs at every stage of every frame: a failure that repeats is told a few times, not every frame.)
                if (WarnOnce(ref m_stageWarns, ref m_stageQuiet))
                {
                    Plugin.ModLog.LogWarning("Stage callback failed: " + ex.Message);
                }
            }
        }

        private void LateUpdate()
        {
            TrySubscribe();
            if (Time.frameCount != m_lastFrame)
            {
                HoldUndo();
            }
            RunFrame(false);
            Prof.B("HeadLate");
            HeadLate();
            Prof.E("HeadLate");
            Prof.B("EyesLate");
            EyesLate();
            Prof.E("EyesLate");
            Prof.B("HeroFinal");
            HeroFinal();
            Prof.E("HeroFinal");
            Prof.B("HerBodiesFinal");
            HerBodiesFinal();
            Prof.E("HerBodiesFinal");
            Prof.B("HeroWatchStep");
            HeroWatchStep();
            Prof.E("HeroWatchStep");
            Prof.B("StrandsStep");
            StrandsStep();
            Prof.E("StrandsStep");
            if (!m_stagesLive)
            {
                PoseFixMeasure();
                Prof.B("AimMeasure");
                AimMeasure();
                Prof.E("AimMeasure");
                Prof.B("HoldCapture");
                HoldCapture();
                Prof.E("HoldCapture");
                Prof.B("XRayStep");
                XRayStep();
                Prof.E("XRayStep");
            }
            KeepCursor();
            Prof.Tick();
        }

        private bool RunFrame(bool early)
        {
            if (Time.frameCount == m_lastFrame)
            {
                return false;
            }
            m_lastFrame = Time.frameCount;
            Instance = this;
            try
            {
                float dt = Time.deltaTime;
                float ramp = Mathf.Max(0.05f, Plugin.PoseRamp.Value);
                // While one mode is being changed for the other she is brought out of the pose; once out, the new one begins.
                bool posing = m_toggled && !m_switching;
                m_poseW = Damp(m_poseW, posing ? 1f : 0f, ref m_poseVel, ramp * 0.3f, dt);
                m_shakeW = Damp(m_shakeW, posing && Time.time >= m_shakeAt ? 1f : 0f, ref m_shakeVel, ShakeRamp * 0.3f, dt);
                if (m_switching && m_poseW <= 0f && m_shakeW <= 0f)
                {
                    m_switching = false;
                    if (m_toggled)
                    {
                        m_ride = m_switchRide;
                        EnterMode();
                        Plugin.ModLog.LogInfo((m_ride ? "Ride" : "Twerk") + " begins");
                    }
                }

                bool active = m_toggled || m_poseW > 0f || m_shakeW > 0f || m_heroReady || m_wet > 0f;
                if (active && Time.unscaledTime >= m_nextScan)
                {
                    m_nextScan = Time.unscaledTime + 2f;
                    FindWomen();
                    // The ride has no woman (none was there when it began, or she is gone): the nearest one now is taken.
                    if (m_ride && posing && m_women.Count > 0 && !m_women.Contains(m_rideWoman))
                    {
                        StartRide();
                    }
                }
                PruneWomen();
                if (!active && m_women.Count == 0)
                {
                    return true;
                }


                // Both are already eased (see Damp): they start, end and turn back without a corner.
                float poseW = m_poseW;
                float shakeW = m_shakeW;
                HeelsStep();
                BodyStep(Mathf.Clamp(dt, 0.004f, 0.05f));
                Prof.B("StepDirector");
                StepDirector(dt, shakeW);
                Prof.E("StepDirector");
                shakeW *= m_motion.intensity;
                // The hero is laid down first, before the game poses its puppets, so that they and she see him lying.
                Prof.B("PoseHero");
                PoseHero();
                Prof.E("PoseHero");
                Prof.B("UpdateWet");
                UpdateWet(dt);
                Prof.E("UpdateWet");
                foreach (Woman w in m_women)
                {
                    if (m_ride)
                    {
                        // Only the woman being ridden is posed; any other is let go.
                        if (w == m_rideWoman)
                        {
                            Prof.B("ApplyRide");
                            ApplyRide(w, poseW, shakeW, m_motion);
                            Prof.E("ApplyRide");
                            HerBodyFollow(w, poseW > 0.0005f || shakeW > 0.0005f);
                        }
                        else
                        {
                            Apply(w, 0f, 0f, m_motion);
                            HerBodyFollow(w, false);
                        }
                    }
                    else
                    {
                        Apply(w, poseW, shakeW, m_motion);
                        HerBodyFollow(w, poseW > 0.0005f || shakeW > 0.0005f);
                    }
                    if (!early)
                    {
                        // Fallback only: the game already updated its hair from the unposed head, so redo it.
                        ApplyHair(w, poseW, shakeW);
                    }
                }
            }
            catch (Exception ex)
            {
                // (Once a frame: the first few in full, then one now and then.)
                if (WarnOnce(ref m_frameWarns, ref m_frameQuiet))
                {
                    Plugin.ModLog.LogWarning("Twerk failed: " + (m_frameWarns <= 3 ? ex.ToString() : ex.Message));
                }
            }
            return true;
        }

        private int m_frameWarns, m_stageWarns;
        private float m_frameQuiet, m_stageQuiet;

        // Whether a warning that may repeat every frame is to be written: the first five, then at most one every 30 s.
        private static bool WarnOnce(ref int count, ref float quietUntil)
        {
            count++;
            if (count <= 5)
            {
                return true;
            }
            if (Time.unscaledTime < quietUntil)
            {
                return false;
            }
            quietUntil = Time.unscaledTime + 30f;
            return true;
        }

        private string m_onScreen = "", m_onScreenStatus = "";
        private float m_onScreenAt;
        private GUIStyle m_statusStyle;

        // The second line on the screen while she rides: what she is doing, the pose, her speed and her arousal.
        private string RideStatusLine()
        {
            var sb = new System.Text.StringBuilder(96);
            sb.Append(RideStatusText());
            string pose = Plugin.RidePoseType.Value;
            if (!string.IsNullOrEmpty(pose))
            {
                sb.Append("  ·  ").Append(Loc.Pose(pose));
            }
            sb.Append("  ·  ").Append(m_speedNow.ToString("0.0")).Append(Loc.T("/s", "/с"));
            if (m_heroReady && m_herShine != null)
            {
                sb.Append("  ·  ").Append(Loc.T("wet ", "намокла на ")).Append(Mathf.RoundToInt(m_herWet * 100f)).Append('%');
            }
            string body = m_heroReady ? BodyText() : "";
            if (body.Length > 0)
            {
                sb.Append("  ·  ").Append(body);
            }
            if (Plugin.FasterKey.Value != KeyCode.None || Plugin.SlowerKey.Value != KeyCode.None)
            {
                sb.Append("  ·  ").Append(Loc.Key(Plugin.SlowerKey.Value)).Append(" / ").Append(Loc.Key(Plugin.FasterKey.Value)).Append(Loc.T(" speed", " скорость"));
            }
            return sb.ToString();
        }

        private void OnGUI()
        {
            try
            {
                if (m_style == null)
                {
                    m_style = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
                    if (Menu.UiFont != null)
                    {
                        m_style.font = Menu.UiFont;
                    }
                }
                if (Time.unscaledTime < m_noticeUntil && !string.IsNullOrEmpty(m_notice))
                {
                    Rect nr = new Rect(0f, 110f, Screen.width, 50f);
                    Color keep = GUI.color;
                    GUI.color = new Color(0f, 0f, 0f, 0.8f);
                    GUI.Label(new Rect(nr.x + 2f, nr.y + 2f, nr.width, nr.height), m_notice, m_style);
                    GUI.color = new Color(1f, 0.85f, 0.4f, 1f);
                    GUI.Label(nr, m_notice, m_style);
                    GUI.color = keep;
                }
                // (The cut to black as she is carried to a place or back: under everything of the mod's own. Places.cs)
                FadeGui();
                // (Under the window, should they overlap.)
                XRayGui();
                Menu.Draw(this);
                if (!Plugin.ShowNotice.Value || m_poseW <= 0.01f)
                {
                    return;
                }
                // (Made anew a few times a second, not at every call: OnGUI is called several times a frame.)
                if (Time.unscaledTime >= m_onScreenAt)
                {
                    m_onScreenAt = Time.unscaledTime + 0.25f;
                    string label = m_ride ? Loc.T("SEX", "СЕКС") : Loc.T("TWERK", "ТВЕРК");
                    string stopKey = Loc.Key(m_ride ? Plugin.RideKey.Value : Plugin.ToggleKey.Value);
                    m_onScreen = m_women.Count == 0 ? label + Loc.T(" (no model found)", " (модель не найдена)") : label + Loc.T(" ON  -  ", " ИДЁТ  -  ") + stopKey + Loc.T(" to stop", ": остановить");
                    m_onScreenStatus = m_ride && m_toggled && !m_switching && Plugin.ShowStatus.Value ? RideStatusLine() : "";
                }
                Rect rect = new Rect(0f, 40f, Screen.width, 50f);
                Color previous = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.8f * m_poseW);
                GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), m_onScreen, m_style);
                GUI.color = new Color(1f, 0.45f, 0.75f, m_poseW);
                GUI.Label(rect, m_onScreen, m_style);
                if (m_onScreenStatus.Length > 0)
                {
                    if (m_statusStyle == null)
                    {
                        m_statusStyle = new GUIStyle(m_style) { fontSize = 18, fontStyle = FontStyle.Normal };
                    }
                    Rect sr = new Rect(0f, 78f, Screen.width, 30f);
                    GUI.color = new Color(0f, 0f, 0f, 0.8f * m_poseW);
                    GUI.Label(new Rect(sr.x + 1f, sr.y + 1f, sr.width, sr.height), m_onScreenStatus, m_statusStyle);
                    GUI.color = new Color(1f, 0.9f, 0.95f, m_poseW);
                    GUI.Label(sr, m_onScreenStatus, m_statusStyle);
                }
                GUI.color = previous;
            }
            catch (Exception)
            {
            }
        }
    }
}
