﻿using UnityEngine;
using Verse;

namespace FactionColonies {
    public abstract class windowFC : Window {
        public override Vector2 InitialSize {
            get {
                return new Vector2(350f, 400f);
            }
        }

        public override void DoWindowContents(Rect inRect) {
            //grab before anchor/font
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;







            //reset anchor/font
            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
