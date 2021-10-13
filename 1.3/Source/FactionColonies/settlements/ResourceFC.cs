﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies {
    public class ResourceFC : IExposable {
        public ResourceFC() {
        }

        public ResourceFC(double baseProduction, ResourceType type, SettlementFC settlement = null) {
            name = type.ToString().ToLower();
            label = type.ToString();
            this.baseProduction = baseProduction;
            endProduction = baseProduction;
            amount = 0;
            baseProductionMultiplier = 1;
            baseProductionAdditives.Add(new ProductionAdditive("", 0, ""));
            baseProductionMultipliers.Add(new ProductionMultiplier("", 0, ""));
            this.settlement = settlement;
            filter = new ThingFilter();
            if (settlement != null) {
                PaymentUtil.resetThingFilter(settlement, type);
            }
        }

        public bool checkMinimum() {
            if (taxStock >= taxMinimumToTithe) {
                return true;
            }

            return false;
        }
        public double returnTaxPercentage() {
            taxPercentage = Math.Round(taxStock / taxMinimumToTithe, 2) * 100;
            return taxPercentage;
        }

        public double returnLowestCost() {
            double minimum = filter.AllowedThingDefs.Aggregate<ThingDef, double>(999999,
                (current, thing) => Math.Min(thing?.BaseMarketValue ?? 100, current));
            //Log.Message(minimum.ToString());
            taxMinimumToTithe = minimum + FactionColonies.Settings().productionTitheMod +
            TraitUtilsFC.cycleTraits(0.0, "taxBaseRandomModifier", Find.World.GetComponent<FactionFC>().traits, "add") +
            TraitUtilsFC.cycleTraits(0.0, "taxBaseRandomModifier", settlement.traits, "add");
            return minimum;
        }

        public Texture2D getIcon() {
            return (from texture in TexLoad.textures where texture.Key == name select texture.Value).FirstOrDefault();
        }


        public void ExposeData() {
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref baseProduction, "baseProduction");
            Scribe_Values.Look(ref endProduction, "endProduction");
            Scribe_Values.Look(ref baseProductionMultiplier, "baseProductionMultiplier");
            Scribe_Values.Look(ref endProductionMultiplier, "endProductionMultiplier");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Collections.Look(ref baseProductionAdditives, "baseProductionAdditives", LookMode.Deep);
            Scribe_Collections.Look(ref baseProductionMultipliers, "baseProductionMultipliers", LookMode.Deep);

            //tithe and income data
            Scribe_Values.Look(ref isTithe, "isTithe");
            Scribe_Values.Look(ref isTitheBool, "isTitheBool");
            Scribe_Values.Look(ref assignedWorkers, "assignedWorkers");

            Scribe_Deep.Look(ref filter, "filter");
            //Tax Stock
            Scribe_Values.Look(ref taxStock, "taxStock");
            Scribe_Values.Look(ref taxMinimumToTithe, "taxMinimumToTithe");
            Scribe_Values.Look(ref taxPercentage, "taxPercentage");

            Scribe_References.Look(ref settlement, "settlement");
        }

        public string getTranslateName() {
            switch (name) {
                case "food":
                    return "农业";
                case "weapons":
                    return "武器";
                case "apparel":
                    return "装备";
                case "animals":
                    return "畜牧";
                case "logging":
                    return "林业";
                case "mining":
                    return "矿业";
                case "research":
                    return "科研";
                case "power":
                    return "电力";
                case "medicine":
                    return "医药";
                default:
                    return name;
            }
        }

        public string name;
        public string label;
        public double baseProduction; //base production for resource
        public double endProduction;  //production after modifiers
        public double baseProductionMultiplier = 1;  //base production modifier for resource
        public double endProductionMultiplier = 1;  //end production modifier for resource
        public List<ProductionAdditive> baseProductionAdditives = new List<ProductionAdditive>();    // {ID, Value, Desc}
        public List<ProductionMultiplier> baseProductionMultipliers = new List<ProductionMultiplier>();  // {ID, Value, Desc}
        public double amount;
        public int assignedWorkers;
        public bool isTithe;
        public bool isTitheBool; //used to track if isTithe is changed. AGHHH

        public ThingFilter filter = new ThingFilter();
        public double taxStock;
        public double taxMinimumToTithe = 99999;
        public double taxPercentage;
        public SettlementFC settlement;

    }

    public class ProductionAdditive : IExposable {
        public ProductionAdditive() {

        }

        public ProductionAdditive(string id, double value, string desc) {
            this.id = id;
            this.value = value;
            this.desc = desc;
        }

        public void ExposeData() {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref value, "value");
            Scribe_Values.Look(ref desc, "desc");
        }

        public string id;
        public double value;
        public string desc;
    }

    public class ProductionMultiplier : IExposable {
        public ProductionMultiplier() {

        }
        public ProductionMultiplier(string id, double value, string desc) {
            this.id = id;
            this.value = value;
            this.desc = desc;
        }

        public void ExposeData() {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref value, "value");
            Scribe_Values.Look(ref desc, "desc");
        }

        public string id;
        public double value;
        public string desc;
    }
}
