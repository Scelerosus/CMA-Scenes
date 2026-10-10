using System;
using BepInEx.Configuration;
using UnityEngine;

namespace CMAScenes
{
    internal static partial class Menu
    {
        // ---- Sex (the ride) ----
        //
        // The page has what the player chooses: where he goes into her, the position, how fast, how deep, who leads, what
        // happens when he is close, and the condom. What the mod does to make that work (the aim, how she gets onto him,
        // the lift of her heels, the film of her wetness, his height above the floor and the like) is not on it: those
        // are the mod's own mechanics, they have settings in the file of settings for whoever wants them, and no row here.

        private static readonly string[] CondomColors = { "Clear", "Pink", "Red", "Blue", "Black" };
        private static int s_ridePage;

        private static void DrawRideHeader(ShakeController host, Styles st)
        {
            CardBegin(st, null);
            if (GUILayout.Button(host.Riding ? T("Stop: she gets off him", "Закончить: она слезает с него") : T("Start: she gets on top of him", "Начать: она садится на него сверху"), host.Riding ? st.button : st.primary, GUILayout.Height(40f)))
            {
                host.ToggleMode(true);
            }
            if (host.Riding)
            {
                GUILayout.Label(host.RideStatusText(), st.good);
                string body = host.BodyText();
                if (body.Length > 0)
                {
                    GUILayout.Label(body, st.dim);
                }
            }
            CardEnd();

            GUILayout.BeginHorizontal();
            string[] pages = { T("Scene", "Сцена"), T("Movement", "Движение"), T("Details", "Дополнительно") };
            for (int i = 0; i < pages.Length; i++)
            {
                if (i > 0)
                {
                    GUILayout.Space(8f);
                }
                if (GUILayout.Button(pages[i], i == s_ridePage ? st.primary : st.button, GUILayout.Height(36f)))
                {
                    s_ridePage = i;
                    s_scroll = Vector2.zero;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(s_ridePage == 0 ? T("Choose where and how she sits on him.", "Выберите место и позу.")
                : s_ridePage == 1 ? T("Set the rhythm and how the two of them move.", "Настройте ритм и движения обоих.")
                : T("X-ray, orgasm and condom.", "X-ray, оргазм и презерватив."), st.dim);
            GUILayout.Space(6f);
        }

        private static void DrawRideContent(ShakeController host, Styles st)
        {
            switch (s_ridePage)
            {
                case 0: DrawRideScene(host, st); break;
                case 1: DrawRideMovement(st); break;
                default: DrawRideDetails(host, st); break;
            }
        }

        private static void DrawRideScene(ShakeController host, Styles st)
        {
            CardBegin(st, T("Place", "Место"));
            GUILayout.Label(host.PlaceLabel(), st.good);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", st.button, GUILayout.Width(40f), GUILayout.Height(30f)))
            {
                host.PlaceCycle(-1);
            }
            if (GUILayout.Button(">", st.button, GUILayout.Width(40f), GUILayout.Height(30f)))
            {
                host.PlaceCycle(1);
            }
            if (GUILayout.Button(T("Where I look", "Туда, куда смотрю"), st.button, GUILayout.Height(30f)))
            {
                host.PlacePick();
            }
            Tip(T("Look at the top of a bench or a table, or at a spot of the floor, and press this: the sex is put there. He lies along it, she is on top of him.", "Посмотри на верх скамьи или стола либо на место на полу и нажми: секс будет там. Он ложится вдоль, она сверху."));
            if (GUILayout.Button(T("Find furniture", "Найти мебель"), st.button, GUILayout.Height(30f)))
            {
                host.PlacesScan();
            }
            Tip(T("Looks through the room for benches, couches, tables and beds; the arrows then go through what was found.", "Ищет в комнате скамьи, диваны, столы и кровати; стрелки потом перебирают найденное."));
            GUILayout.EndHorizontal();
            Check(st, T("Tour: after her orgasm she moves on to the next place", "Тур: после своего оргазма она ведёт на следующее место"), Plugin.PlaceTour,
                T("With places found in the room: once she has come she gets off him and gets on him again at the next place of the list.", "Когда в комнате найдены места: кончив, она слезает с него и садится снова уже на следующем месте из списка."));
            if ((Plugin.PlaceUse.Value ?? "").Length > 0)
            {
                Slider(st, T("Along it", "Вдоль"), Plugin.PlaceAlong, -1.2f, 1.2f, "0", Cm, 100f,
                    T("Moves them along the place, toward his head or his feet.", "Сдвигает их вдоль места: к его голове или к ногам."));
                Slider(st, T("Across it", "Поперёк"), Plugin.PlaceAcross, -0.6f, 0.6f, "0", Cm, 100f,
                    T("Moves them across the place.", "Сдвигает их поперёк места."));
                Slider(st, T("Height", "Высота"), Plugin.PlaceHeight, -0.4f, 0.5f, "0", Cm, 100f,
                    T("Raises or lowers them: if he lies in the furniture or floats over it, its top was measured wrong.", "Поднимает или опускает их: если он лежит внутри мебели или парит над ней, её верх измерен неверно."));
                Slider(st, T("Turn", "Поворот"), Plugin.PlaceTurn, -180f, 180f, "0", "°", 1f,
                    T("Turns them on the place; 180 lays him the other way round.", "Поворачивает их на месте; 180 кладёт его головой в другую сторону."));
            }
            CardEnd();

            CardBegin(st, T("Position", "Поза"));
            HoleRow(st);
            PoseRow(st, T("Position", "Поза"), Poses.Ride, Plugin.RidePoseType);
            CardEnd();
        }

        private static void DrawRideMovement(Styles st)
        {
            CardBegin(st, T("Movement", "Движение"));
            Slider(st, T("Tempo", "Темп"), Plugin.RideFrequency, 0.3f, 2.5f, "0.00", PerSec, 1f,
                Plugin.RideLead.Value
                    ? T("While she leads she picks her tempo herself, by the speed of him in her that she likes. This slider (and the mood) only says which side of that she keeps to: the slower or the faster.", "Пока она ведёт сама, темп она выбирает по скорости, которая ей приятнее всего. Этот ползунок (и «Настрой») лишь задаёт, какого края она держится: медленного или быстрого.")
                    : T("How many strokes a second she makes on him.", "Сколько толчков в секунду она делает на нём."));
            Slider(st, T("Depth", "Глубина проникновения"), Plugin.RideDepth, 0.3f, 1f, "0", " %", 100f,
                T("The most of his penis she takes when she sits down: 100 % is all of it. She takes less while she warms up, and no more than does not hurt her.", "Сколько члена она принимает самое большее, когда садится: 100 % — весь. Пока разогревается, берёт меньше, и не глубже, чем ей не больно."));
            Slider(st, T("Length of the stroke", "Размах толчков"), Plugin.RideBounce, 0f, 0.3f, "0", Cm, 100f,
                T("How far she rises off him before coming back down. No further than his tip lets her: he stays in her.", "На сколько она приподнимается с него, прежде чем опуститься снова. Не выше головки: он остаётся в ней."));
            CardEnd();

            CardBegin(st, T("She", "Она"));
            Check(st, T("Leads: finds her own rhythm and depth", "Ведёт сама: ловит свой ритм и глубину"), Plugin.RideLead,
                T("The game has a rhythm of her own for every woman: a speed of him in her that she likes best (slower in her anus than in her vagina), which changes as she gets close and after she has come. With this on she rides at it: as many strokes a second, of the length her strokes have, as make that speed, with longer strokes first where it would take too many. And she takes him just short of where the game has it hurt her. Off: exactly the tempo and depth above.",
                  "У каждой девушки в игре свой ритм: скорость члена в ней, которая ей приятнее всего (в анусе она ниже, чем в вагине); она меняется, когда она близко к оргазму и после него. Если включено, она держит эту скорость: делает столько толчков в секунду, сколько для неё нужно при её размахе, а если толчков вышло бы слишком много, сначала удлиняет размах. И берёт член чуть не доходя до глубины, на которой игра делает ей больно. Выключено: ровно темп и глубина, заданные выше."));
            Check(st, T("Living body: warms up, tires, breathes", "Живое тело: разогрев, усталость, дыхание"), Plugin.RideAlive,
                T("She takes him shallow at first and deeper as she warms up, eases off while it hurts, tires (sooner squatting than kneeling) and sits down on him to get her breath back, and her chest rises and falls with her breath. Off: she rides like a machine.",
                  "Сначала берёт неглубоко и глубже по мере разогрева, сбавляет, когда больно, устаёт (на корточках быстрее, чем на коленях) и садится на него отдышаться, а грудь поднимается от дыхания. Выключено: скачет как машина."));
            Check(st, T("Changes position by herself", "Сама меняет позу"), Plugin.RideFlow,
                T("As she gets aroused she moves through Cowgirl, Astride, Over him, Leaning back, Squatting and Kneeling. She rests when tired and returns to Cowgirl after an orgasm. Reverse is chosen manually; changing pose yourself pauses the automatic sequence.", "По мере возбуждения она меняет «Наездницу», «Верхом», «Над ним», «Откинувшись», «На корточках» и «На коленях». Когда устаёт, выбирает позу полегче, после оргазма возвращается к «Наезднице». «Спиной» выбирается вручную; ваш выбор на время приостанавливает автоматическую смену."));
            Slider(st, T("He thrusts up to meet her", "Он подмахивает ей навстречу"), Plugin.RideHeMeets, 0f, 0.09f, "0", Cm, 100f,
                T("How far his hips come up as she comes down on him, in time with her, and lift her with them; at an orgasm he holds himself up into her. 0: he lies still. The mouse wheel lifts his hips by hand as well.", "На сколько его бёдра поднимаются ей навстречу, когда она опускается, в такт с ней, и поднимают её с собой; в оргазме он вжимается в неё. 0: лежит неподвижно. Колесо мыши по-прежнему поднимает его бёдра вручную."));
            Check(st, T("He holds her and moves her himself", "Он держит её и двигает сам"), Plugin.RideHeHolds,
                T("A mode of its own: his hands take her by the hips and her bottom and he moves her on himself with his own hips, stroke by stroke, at the tempo she has; she lets him, her hands on his arms and chest and her own strokes only a little.", "Отдельный режим: он берёт её руками за бёдра и попу и сам двигает её на себе тазом, толчок за толчком, в её темпе; она отдаётся, руки у неё на его руках и груди, а своих движений почти нет."));
            Slider(st, T("Her womb: how far his head goes in", "Матка: на сколько входит головка"), Plugin.RideWomb, 0f, 0.06f, "0", Cm, 100f,
                T("Her womb is the continuation of her vagina: when he comes in her his head goes in through its neck by this much (she sits down on him a little deeper, and her womb draws back round him in the X-ray). 0: his head stays in her canal.", "Матка — продолжение влагалища: когда он кончает в неё, головка входит через её шейку на столько (она садится на него чуть глубже, а матка в рентгене отодвигается вокруг него). 0: головка остаётся в канале."));
            Check(st, T("Her feet find the ground", "Ступни сами находят опору"), Plugin.RideFeetAuto,
                T("A ray down at each foot: the floor, a rug, a bench top, a step. Her ankles stand as high above it as the game's pose has them (in heels, higher) and her soles lie along a slope.", "Луч вниз к каждой ступне: пол, ковёр, верх скамьи, ступенька. Лодыжки стоят над опорой так же высоко, как в позе игры (на каблуках выше), подошвы ложатся вдоль склона."));
            CardEnd();
        }

        private static void DrawRideDetails(ShakeController host, Styles st)
        {
            CardBegin(st, "X-ray");
            Check(st, T("X-ray: see him move in her", "X-ray: видно, как он ходит в ней") + (Plugin.XRayKey.Value != KeyCode.None ? "  [" + Loc.Key(Plugin.XRayKey.Value) + "]" : ""), Plugin.XRay,
                T("During the sex her insides are shown as the game itself has them (her canal, her womb, her rectum: the game moves and stretches them round him), with what of his penis is in her, and his semen in her as the game has it: it fills her, and while the game lets it out of her it runs down her canal and out. After the sex the X-ray stays on her for as long as the game still has his semen in her. Also on its key.",
                  "Во время секса показываются её внутренние органы — такие, какими их считает сама игра (канал вагины, матка, прямая кишка: игра сама сдвигает и растягивает их вокруг члена), — та часть члена, что в ней, и его сперма в ней, как её считает игра: она заполняет её, а когда игра выпускает её наружу, стекает по каналу и вытекает. После секса X-ray остаётся на ней, пока у игры в ней ещё есть сперма. Включается и клавишей."));
            ChoiceRow(st, T("Shown", "Показывать"), new[] { T("Both", "И то и другое"), T("Through her body", "Сквозь тело"), T("Inset", "Врезка"), T("The game's own", "X-ray игры") }, new[] { "Both", "Through", "Inset", "Game" }, Plugin.XRayView, 122f,
                T("Through her body: her insides, her womb and the part of his penis that is in her are drawn half see-through over her skin, wherever you look from. Inset: a cut through her from front to back, in a frame at the side of the screen, with how deep he is in her. The game's own: the game has an X-ray of its own (a camera on her pelvis, in the game's frame), unlocked by the agency's level; this switches that one on for the sex instead, whatever the level.",
                  "«Сквозь тело»: её органы, матка и та часть члена, что в ней, рисуются полупрозрачными поверх её кожи, с какой бы стороны вы ни смотрели. «Врезка»: разрез её тела сбоку в рамке у края экрана, с глубиной, на которую он вошёл. «X-ray игры»: у игры есть собственный X-ray (камера на её тазе, в рамке самой игры), он открывается с уровнем агентства; здесь вместо первых двух на время секса включается он, каким бы ни был уровень."));
            Slider(st, T("Through her body: how solid", "Сквозь тело: плотность"), Plugin.XRayOpacity, 0.15f, 1f, "0", " %", 100f,
                T("How solid her insides, her womb and his penis in her are drawn over her skin: low is a faint veil, high hides her skin under them.",
                  "Насколько плотно её органы, матка и член в ней рисуются поверх её кожи: мало — лёгкая дымка, много — кожа под ними не видна."));
            CardEnd();

            CardBegin(st, T("Orgasm", "Оргазм"));
            ChoiceRow(st, T("When he is close", "Когда он близок"), new[] { T("Let it happen", "Как пойдёт"), T("Together with her", "Вместе с ней"), T("Kept on the edge", "Держит на грани") }, new[] { "Free", "Together", "Edge" }, Plugin.RideFinish, 150f,
                T("What she does as he nears coming (by the game's own pleasure of him). Let it happen: she rides on. Together with her: she holds him back, sitting on him and barely moving, until she is nearly there herself, then goes all out. Kept on the edge: she holds him back every time, until he has cooled a little.",
                  "Что она делает, когда он близок к оргазму (по его удовольствию в самой игре). «Как пойдёт»: скачет дальше. «Вместе с ней»: придерживает его — сидит на нём и почти не двигается, — пока сама не окажется близко, и тогда отдаётся целиком. «Держит на грани»: придерживает каждый раз, пока он немного не остынет."));
            Check(st, T("Gets off him after he comes", "Слезает после его оргазма"), Plugin.RideEndAfter,
                T("After he has come in her she stays on him for a little while, then gets off him by herself: the sex is over.", "После того как он кончил в неё, она ещё немного сидит на нём, а потом сама слезает: секс окончен."));
            CardEnd();

            // (Her wetness on him is always on: no switch for it. When he comes in her nothing is put on him; what he came
            // is in her, and is seen there in the X-ray.)
            CardBegin(st, T("Condom", "Презерватив"));
            CondomRow(host, st);
            ChoiceRow(st, T("Colour", "Цвет"), new[] { T("Clear", "Прозрачный"), T("Pink", "Розовый"), T("Red", "Красный"), T("Blue", "Синий"), T("Black", "Чёрный") }, CondomColors, Plugin.CondomColor, 84f,
                T("The colour of the latex.", "Цвет латекса."));
            Check(st, T("Rolls on without his hand", "Надевается без рук"), Plugin.CondomAuto,
                T("On: no hand is needed. She comes up off him and it rolls onto him by itself.", "Включено: рука не нужна — она приподнимается, и презерватив раскатывается сам."));
            CardEnd();

            ResetButton(st, T("Reset everything about the sex", "Сбросить всё про секс"), () =>
                ResetAll(Plugin.RideFrequency, Plugin.RideBounce, Plugin.RideGrind, Plugin.RideTilt, Plugin.RideSpring, Plugin.RideDepth, Plugin.RideHipsWork, Plugin.RideKeepHard, Plugin.RideEnterSpeed, Plugin.RideAimForward, Plugin.RideAimUp, Plugin.RideCamSteady, Plugin.RideFov,
                    Plugin.RideFeet, Plugin.RideLean, Plugin.RideFacing, Plugin.RideHeroShift, Plugin.RideHeroClearance,
                    Plugin.RideWetShine, Plugin.RideWetRelief, Plugin.RideWetDepth, Plugin.RideWetStrings, Plugin.RideAutoAim, Plugin.RideClimax, Plugin.RideBuild, Plugin.RideWaves, Plugin.RideFlow, Plugin.RideMaxSpeed, Plugin.RideLookAtHim, Plugin.RideLegs, Plugin.RideHole, Plugin.RideAnalAimForward, Plugin.RideAnalAimUp, Plugin.CondomAuto, Plugin.CondomColor,
                    Plugin.RideWarmUp, Plugin.RideListen, Plugin.RideTires, Plugin.RideStamina, Plugin.RideBreath, Plugin.RideFinish, Plugin.RideEndAfter, Plugin.RideHeMeets, Plugin.RideHeHolds, Plugin.RideWomb, Plugin.RideFeetAuto, Plugin.RideFlatFeet, Plugin.RideLead, Plugin.RideAlive,
                    Plugin.XRay, Plugin.XRayView, Plugin.XRayOpacity, Plugin.XRayInsetSize, Plugin.XRayInsetSide));
        }

    }
}
