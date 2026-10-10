                Vector3 neutral = side == 0 ? -hinge : hinge;
                float turn = WristTurn(f, neutral, bk);
                float pron = side == 1 ? turn : -turn;
                float excess = pron > PronateMost ? pron - PronateMost : (pron < -SupinateMost ? pron + SupinateMost : 0f);
                if (excess != 0f)
                {
                    // (Turned back about the forearm by what is too much: fingers and back of the hand together. The palm then
                    // does not lie quite flat: the forearm cannot turn it so.)
                    Quaternion undo = Quaternion.AngleAxis(side == 1 ? -excess : excess, f);
                    fg = undo * fg;
                    bk = undo * bk;
                }
                cost += Sq(Mathf.Max(0f, pron - 55f) / 30f) + Sq(Mathf.Max(0f, -pron - 35f) / 40f) + Sq(excess / 12f);