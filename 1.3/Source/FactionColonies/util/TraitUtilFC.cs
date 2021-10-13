﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace FactionColonies {
    class TraitUtilsFC {
        public static double returnVariable(double output, string field, FCTraitEffectDef def) {
            Type typ = def.GetType();
            FieldInfo fieldInfo = typ.GetField(field);
            return Convert.ToDouble(fieldInfo.GetValue(def));
        }
        public static string returnVariable(string output, string field, FCTraitEffectDef def) {
            Type typ = def.GetType();
            FieldInfo fieldInfo = typ.GetField(field);
            return fieldInfo.GetValue(def).ToString();
        }
        public static List<TraitDef> returnVariable(List<TraitDef> output, string field, FCTraitEffectDef def) {
            Type typ = def.GetType();
            FieldInfo fieldInfo = typ.GetField(field);
            return (List<TraitDef>)fieldInfo.GetValue(def);
        }
        public static List<Thing> returnVariable(List<Thing> output, string field, FCTraitEffectDef def) {
            Type typ = def.GetType();
            FieldInfo fieldInfo = typ.GetField(field);
            return (List<Thing>)fieldInfo.GetValue(def);
        }
        public static List<PawnKindDef> returnVariable(List<PawnKindDef> output, string field, FCTraitEffectDef def) {
            Type typ = def.GetType();
            FieldInfo fieldInfo = typ.GetField(field);
            return (List<PawnKindDef>)fieldInfo.GetValue(def);
        }

        public static double cycleTraits(double var, string field, List<FCTraitEffectDef> traits, string addOrMultiply) {

            double tempTrait = -1;
            if (addOrMultiply == "add") {
                tempTrait = 0;
            } else
               if (addOrMultiply == "multiply") {
                tempTrait = 1;
            }

            foreach (FCTraitEffectDef trait in traits) {
                if (addOrMultiply == "add") {
                    tempTrait += TraitUtilsFC.returnVariable(new double(), field, trait);
                } else
                if (addOrMultiply == "multiply") {
                    tempTrait *= TraitUtilsFC.returnVariable(new double(), field, trait);
                }
            }
            return tempTrait;
        }

        public static int returnResearchAmount() {
            int research = 0;
            research += Convert.ToInt32(cycleTraits(new double(), "researchBaseProduction", Find.World.GetComponent<FactionFC>().traits, "add"));
            foreach (SettlementFC settlement in Find.World.GetComponent<FactionFC>().settlements) {
                research += Convert.ToInt32(cycleTraits(new double(), "researchBaseProduction", settlement.traits, "add"));
            }
            return research;
        }
    }
}
