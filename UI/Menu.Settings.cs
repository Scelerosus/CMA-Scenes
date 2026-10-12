using System;
using System.Collections.Generic;
using Assets.TValle.Tools.Runtime.Moddding.Clothing.Maps;
using BepInEx.Configuration;
using UnityEngine;

namespace CMAScenes
{
    internal static partial class Menu
    {
        // ---- Settings ----

        // A hotkey: its name, the key it has now as a button, and a button that takes it away. Pressing the key button
        // waits for the next key pressed.
        private static void KeyRow(Styles st, string label, ConfigEntry<KeyCode> e, string tip)
        {
            GUILayout.BeginHorizontal();
            RowLabel(st, label, RowHeight(st, label, 34f));
            bool waiting = s_capture == e;
            string key = waiting ? T("press a key...", "нажмите клавишу...") : Loc.Key(e.Value);
            GUIStyle look = waiting ? st.primary : st.button;
            // (A long key name, "KeypadMinus", a size smaller rather than cut off.)
            if (GUILayout.Button(key, Fit(st, look, key, 190f - look.padding.horizontal), GUILayout.Width(190f), GUILayout.Height(32f)))
            {
                s_capture = waiting ? null : e;
            }
            GUILayout.Space(6f);
            if (GUILayout.Button(T("Default", "По умолчанию"), st.ghost, GUILayout.Height(32f)))
            {
                e.BoxedValue = e.DefaultValue;
                s_capture = null;
                MarkDirty();
            }
            GUILayout.EndHorizontal();
            Tip(tip);
        }

        private static void DrawSettings(ShakeController host, Styles st)
        {
            // (How long a move lasts, how smooth it is and how long a change of pose takes are the mod's own mechanics:
            // settings in the file of settings, no rows here.)
            CardBegin(st, T("Rhythm (scene)", "Ритм (сцена)"));
            RhythmRow(st);
            CardEnd();

            CardBegin(st, T("Language", "Язык"));
            ChoiceRow(st, T("Language of the window", "Язык окна"), new[] { "Русский", "English", T("Auto", "Авто") }, new[] { "ru", "en", "auto" }, Plugin.Language, 100f,
                T("Auto follows the language of your system.", "«Авто» берёт язык системы."));
            CardEnd();

            CardBegin(st, T("Window", "Окно"));
            Slider(st, T("Clothing search radius", "Радиус поиска персонажей"), Plugin.ClothingRadius, 1f, 50f, "0.0", T(" m", " м"), 1f,
                T("Characters near the camera with a wardrobe, independent of posing support.", "Персонажи рядом с камерой с доступным гардеробом, независимо от поддержки позирования."));
            Slider(st, T("Window brightness", "Яркость окна"), Plugin.UiDim, 0.3f, 1f, "0", " %", 100f,
                T("The game shows this window brighter than it is drawn, which washes its colours out. Lower it until the picked choices are a clear blue and their white text is easy to read. Applied when the slider is let go.",
                  "Игра показывает это окно ярче, чем оно нарисовано, и цвета выцветают. Убавляйте, пока выбранные плашки не станут насыщенно-синими, а белый текст на них — хорошо читаемым. Применяется, когда отпустите ползунок."));
            Slider(st, T("Window size", "Размер окна"), Plugin.UiScale, 0.75f, 1.5f, "0", " %", 100f,
                T("Makes the whole window bigger or smaller. Applied when the slider is let go.", "Увеличивает или уменьшает всё окно целиком. Применяется, когда отпустите ползунок."));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Back to the corner", "Вернуть в угол"), st.button, GUILayout.Height(32f)))
            {
                s_pos = new Vector2(24f, 24f);
                StoreLayout();
            }
            Tip(T("Puts the window back at the top-left of the screen.", "Возвращает окно в левый верхний угол экрана."));
            GUILayout.Space(6f);
            if (GUILayout.Button(T("Full height", "Во всю высоту"), st.button, GUILayout.Height(32f)))
            {
                s_height = 0f;
                StoreLayout();
            }
            Tip(T("Makes the window as tall as the screen allows. Drag its bottom-right corner to make it shorter.", "Растягивает окно на всю доступную высоту. Потяните за правый нижний угол, чтобы сделать ниже."));
            GUILayout.EndHorizontal();
            CardEnd();

            CardBegin(st, T("Keys", "Клавиши"));
            GUILayout.Label(T("Press a key's button, then the key you want. Esc leaves it as it was, Backspace sets no key.",
                "Нажмите на кнопку с клавишей, затем нужную клавишу. Esc — оставить как было, Backspace — без клавиши."), st.dim);
            Gap(4f);
            KeyRow(st, T("This window", "Это окно"), Plugin.MenuKey, T("Opens and closes this window.", "Открывает и закрывает это окно."));
            KeyRow(st, T("Sex", "Секс"), Plugin.RideKey, T("Starts and stops the sex: she gets on top of him, or off him.", "Начинает и заканчивает секс: она садится на него или слезает."));
            KeyRow(st, T("Bikini", "Бикини"), Plugin.BikiniKey, T("Puts the bikini on and takes it off.", "Надевает и снимает бикини."));
            KeyRow(st, T("Condom", "Презерватив"), Plugin.CondomKey, T("Gets a condom out, or takes off the one he wears.", "Достаёт презерватив или снимает надетый."));
            KeyRow(st, "X-ray", Plugin.XRayKey, T("Switches the X-ray on and off: the woman nearest the centre of the screen outside sex, and the ridden woman during sex.", "Включает и выключает X-ray: женщину в центре экрана вне секса и женщину, на которой вы едете, во время секса."));
            KeyRow(st, T("Next mood", "Следующий настрой"), Plugin.PresetKey, T("Switches to the next mood: Sensual, Gentle, Wild, As set.", "Переключает настрой по кругу: Чувственно, Нежно, Страстно, Как настроено."));
            KeyRow(st, T("Next pose", "Следующая поза"), Plugin.NextPoseKey, T("In the scene: next pose; hold Shift for the previous one.", "Во время сцены: следующая поза; с Shift — предыдущая."));
            KeyRow(st, T("Faster", "Быстрее"), Plugin.FasterKey, T("A step faster: the sex. The tempo is saved, as with the slider.", "На шаг быстрее: секс. Темп сохраняется, как ползунком."));
            KeyRow(st, T("Slower", "Медленнее"), Plugin.SlowerKey, T("A step slower: the sex. The tempo is saved, as with the slider.", "На шаг медленнее: секс. Темп сохраняется, как ползунком."));
            if (Plugin.MenuKey.Value == KeyCode.None)
            {
                GUILayout.Label(T("This window has no key: once closed it can only be opened again by setting MenuKey in the config file.",
                    "У этого окна нет клавиши: после закрытия открыть его можно будет только через MenuKey в файле настроек."), st.warn);
            }
            CardEnd();

            CardBegin(st, T("On the screen", "На экране"));
            Check(st, T("Show a line at the top of the screen during the scene", "Показывать строку вверху экрана во время сцены"), Plugin.ShowNotice,
                T("The line at the top of the screen that says what is running and which key stops it.", "Строка вверху экрана: что сейчас идёт и какой клавишей остановить."));
            if (Plugin.ShowNotice.Value)
            {
                Check(st, T("Under it, how the sex is going", "Под ней — как идёт секс"), Plugin.ShowStatus,
                    T("A second, smaller line during sex: what she is doing, the position, her tempo, how wet she is, what the two of them feel and how tired she is.", "Вторая строка поменьше во время секса: что она делает, поза, темп, насколько она намокла, что чувствуют оба и насколько она устала."));
            }
            CardEnd();

            ResetButton(st, T("Reset the keys and these settings", "Сбросить клавиши и эти настройки"), () =>
            {
                s_capture = null;
                ResetAll(Plugin.MenuKey, Plugin.RideKey, Plugin.BikiniKey, Plugin.CondomKey, Plugin.XRayKey, Plugin.PresetKey, Plugin.NextPoseKey, Plugin.FasterKey, Plugin.SlowerKey, Plugin.ShowNotice, Plugin.ShowStatus, Plugin.Language,
                    Plugin.Rhythm, Plugin.MoveSeconds, Plugin.Smoothness, Plugin.PoseRamp);
            });
        }
    }
}
