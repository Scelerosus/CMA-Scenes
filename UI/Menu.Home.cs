using System;
using System.Collections.Generic;
using Assets.TValle.Tools.Runtime.Moddding.Clothing.Maps;
using BepInEx.Configuration;
using UnityEngine;

namespace CMAScenes
{
    internal static partial class Menu
    {
        // ---- Home ----

        // The first page: the start buttons, and under them only what is changed most often. Everything else is on the
        // page of its mode.
        private static void DrawHome(ShakeController host, Styles st)
        {
            bool twerk = host.Twerking;
            bool ride = host.Riding;
            bool bikini = Bikini.AnyOn();

            GUILayout.Label("Scenes Hub", st.brand, GUILayout.Height(36f));
            GUILayout.Label(ride ? T("Sex", "Секс") + ":  " + host.RideStatusText() : twerk ? T("She is twerking", "Она тверкает") : T("Stand in front of her and press Sex or Twerk", "Встаньте перед девушкой и нажмите «Секс» или «Тверк»"), ride || twerk ? st.good : st.dim);
            Gap(10f);

            CardBegin(st, null);
            GUILayout.BeginHorizontal();
            if (BigButton(st, T("Twerk", "Тверк"), twerk ? T("stop", "остановить") : T("start", "запустить"), Plugin.ToggleKey.Value, twerk, TabTwerk))
            {
                host.ToggleMode(false);
            }
            Tip(T("She bends over and twerks where she stands.", "Она наклоняется и тверкает там, где стоит."));
            GUILayout.Space(8f);
            if (BigButton(st, T("Sex", "Секс"), ride ? T("stop", "закончить") : T("start", "начать"), Plugin.RideKey.Value, ride, TabRide))
            {
                host.ToggleMode(true);
            }
            Tip(T("He lies down on his back and she gets on top of him. The woman nearest to your view is the one.", "Он ложится на спину, и она садится на него сверху. Берётся ближайшая к камере девушка."));
            GUILayout.Space(8f);
            if (BigButton(st, T("Bikini", "Бикини"), bikini ? T("take off", "снять") : T("put on", "надеть"), Plugin.BikiniKey.Value, bikini, TabBikini))
            {
                host.ToggleBikini();
            }
            Tip(T("Puts her in the bikini picked on the Bikini page, or back in her own clothes.", "Надевает бикини, выбранное на странице «Бикини», или возвращает её одежду."));
            GUILayout.EndHorizontal();
            CardEnd();

            CardBegin(st, T("Sex: she is on top", "Секс: она сверху"));
            HoleRow(st);
            PoseRow(st, T("Position", "Поза"), Poses.Ride, Plugin.RidePoseType);
            Slider(st, T("Tempo", "Темп"), Plugin.RideFrequency, 0.3f, 2.5f, "0.00", PerSec, 1f,
                T("How many strokes a second she makes on him.", "Сколько толчков в секунду она делает на нём."));
            Slider(st, T("Depth", "Глубина проникновения"), Plugin.RideDepth, 0.3f, 1f, "0", " %", 100f,
                T("How much of his penis she takes when she sits down: 100 % is all of it. While she warms up she takes less.", "Какую часть члена она принимает, когда садится: 100 % — весь. Пока разогревается, берёт меньше."));
            CondomRow(host, st);
            CardEnd();

            // The tally of the ride that is on, or of the last one; and how the two of them are right now.
            string tally = host.SessionText();
            if (tally.Length > 0)
            {
                CardBegin(st, ride ? T("This time", "В этот раз") : T("Last time", "В прошлый раз"));
                GUILayout.Label(tally, st.dim);
                string body = ride ? host.BodyText() : "";
                if (body.Length > 0)
                {
                    GUILayout.Label(body, st.good);
                }
                CardEnd();
            }

            CardBegin(st, T("Twerk", "Тверк"));
            PoseRow(st, T("Pose", "Поза"), Poses.Twerk, Plugin.TwerkPoseType);
            Slider(st, T("Tempo", "Темп"), Plugin.Frequency, 0.1f, 5f, "0.00", PerSec, 1f,
                T("How many times a second she bounces her hips.", "Сколько раз в секунду она подбрасывает бёдра."));
            CardEnd();

            CardBegin(st, T("Mood (twerk and sex)", "Настрой (тверк и секс)"));
            PresetRow(st);
            CardEnd();
        }
    }
}
