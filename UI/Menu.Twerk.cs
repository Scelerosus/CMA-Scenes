using System;
using BepInEx.Configuration;
using UnityEngine;

namespace CMAScenes
{
    internal static partial class Menu
    {
        // ---- Twerk ----
        //
        // What the player chooses: the pose, how fast, how high, where her hands are, what shakes. The finer points of the
        // pose that the four ready-made poses set (how far her bottom is pushed out, her toes, the rock of her pelvis)
        // have settings in the file of settings and no row here.

        private static void DrawTwerk(ShakeController host, Styles st)
        {
            CardBegin(st, null);
            if (GUILayout.Button(host.Twerking ? T("Stop the twerk", "Остановить тверк") : T("Start the twerk", "Запустить тверк"), host.Twerking ? st.button : st.primary, GUILayout.Height(40f)))
            {
                host.ToggleMode(false);
            }
            CardEnd();

            CardBegin(st, T("Twerk", "Тверк"));
            PoseRow(st, T("Pose", "Поза"), Poses.Twerk, Plugin.TwerkPoseType);
            Slider(st, T("Tempo", "Темп"), Plugin.Frequency, 0.1f, 5f, "0.00", PerSec, 1f,
                T("How many times a second she bounces her hips.", "Сколько раз в секунду она подбрасывает бёдра."));
            Slider(st, T("How high she bounces", "Высота подбрасывания"), Plugin.BounceHeight, 0f, 0.1f, "0.0", Cm, 100f,
                T("How far her hips go up and down with each bounce.", "На сколько бёдра поднимаются и опускаются за раз."));
            ChoiceRow(st, T("Her hands", "Её руки"), new[] { T("On her hips", "На бёдрах"), T("On her knees", "На коленях"), T("As in the game", "Как в игре") }, new[] { "Hips", "Knees", "None" }, Plugin.HandsOn, 100f,
                T("Where she puts her hands. As in the game: the mod leaves her arms alone.", "Куда она кладёт руки. «Как в игре» — мод не трогает руки."),
                new[] { "pose.hands-on-hips", "pose.bent-over", "pose.standing" });
            CardEnd();

            CardBegin(st, T("What shakes", "Что трясётся"));
            Slider(st, T("Her buttocks", "Ягодицы"), Plugin.ButtAmplitude, 0f, 0.08f, "0.0", Cm, 100f,
                T("How much her bottom jiggles after each bounce.", "Насколько попа колышется после каждого движения."));
            Check(st, T("Her breasts too", "Грудь тоже"), Plugin.ShakeChest,
                T("Lets her breasts move with her. They stay on her chest and never go far.", "Грудь двигается вместе с ней, но остаётся на месте и далеко не уходит."));
            CardEnd();

            if (FoldBegin(st, "twerk.more", T("Her pose in detail", "Её поза подробно"), T("bend, arch, squat, legs", "наклон, прогиб, присед, ноги"), false))
            {
                Slider(st, T("Bends over", "Наклон вперёд"), Plugin.LeanDegrees, 0f, 85f, "0", "°", 1f,
                    T("How far she bends over.", "Насколько она наклоняется вперёд."));
                Slider(st, T("Arches her back", "Прогиб в пояснице"), Plugin.Arch, 0f, 30f, "0", "°", 1f,
                    T("Arches her back: her bottom goes up and back.", "Прогибает спину: попа уходит вверх и назад."));
                Slider(st, T("Squat", "Присед"), Plugin.SquatDepth, 0f, 0.5f, "0", Cm, 100f,
                    T("How far her hips drop.", "На сколько опускаются бёдра."));
                Slider(st, T("Legs apart", "Ноги в стороны"), Plugin.Stance, 0f, 0.5f, "0", Cm, 100f,
                    T("How much wider each foot stands.", "На сколько шире она ставит каждую ногу."));
            }
            CardEnd();

            ResetButton(st, T("Reset everything about the twerk", "Сбросить всё про тверк"), () =>
                ResetAll(Plugin.Frequency, Plugin.BounceHeight, Plugin.TiltDegrees, Plugin.ButtAmplitude,
                    Plugin.ShakeChest, Plugin.ChestAmplitude, Plugin.LeanDegrees, Plugin.Arch, Plugin.SquatDepth, Plugin.HipBack, Plugin.Stance, Plugin.ToesOut, Plugin.HandsOn));
        }
    }
}
