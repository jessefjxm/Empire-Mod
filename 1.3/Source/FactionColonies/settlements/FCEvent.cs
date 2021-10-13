﻿using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace FactionColonies {
    public static class FCEventMaker {
        public static void calculateSuccess(FCOptionDef option, FCEvent parentEvent) {
            float baseChance = option.baseChanceOfSuccess;
            int roll = Rand.Range(1, 100);
            //Log.Message(roll.ToString());

            FCEvent tempEvent = new FCEvent(true);


            if (roll <= baseChance) {
                //if success
                if (option.parentEvent.settlementsCarryOver) {
                    tempEvent = MakeRandomEvent(option.successEvent, parentEvent.settlementTraitLocations);
                } else {
                    tempEvent = MakeRandomEvent(option.successEvent, null);
                }
            } else {
                if (option.parentEvent.settlementsCarryOver) {
                    tempEvent = MakeRandomEvent(option.failEvent, parentEvent.settlementTraitLocations);
                } else {
                    tempEvent = MakeRandomEvent(option.failEvent, null);
                }
            }

            if (tempEvent.def != FCEventDefOf.Null) {
                Find.World.GetComponent<FactionFC>().addEvent(tempEvent);

                //letter

                string settlementString = "";
                foreach (SettlementFC loc in tempEvent.settlementTraitLocations) {
                    if (settlementString == "") {
                        settlementString = settlementString + loc.name;
                    } else {
                        settlementString = settlementString + ", " + loc.name;
                    }
                }

                if (settlementString != "") {
                    Find.LetterStack.ReceiveLetter(tempEvent.def.label,
                        tempEvent.def.desc + "\n 该事件正在影响以下居住地： " +
                        settlementString, LetterDefOf.NeutralEvent);
                } else {
                    Find.LetterStack.ReceiveLetter(tempEvent.def.label, tempEvent.def.desc, LetterDefOf.NeutralEvent);
                }
            }
        }

        public static bool isValidRandomEvent(FCEventDef cEvent) {
            FactionFC tmp = Find.World.GetComponent<FactionFC>();

            if (cEvent.isRandomEvent && Find.World.PlayerWealthForStoryteller >= cEvent.requiredWealth) {
                //If meets happiness requirement
                if (cEvent.minimumHappiness <= tmp.averageHappiness && tmp.averageHappiness <= cEvent.maximumHappiness) {
                    //If meets loyalty requirement
                    if (cEvent.minimumLoyalty <= tmp.averageLoyalty && tmp.averageLoyalty <= cEvent.maximumLoyalty) {
                        //If meets unrest requirement
                        if (cEvent.minimumUnrest <= tmp.averageUnrest && tmp.averageUnrest <= cEvent.maximumUnrest) {
                            //If meets prosperity requirement
                            if (cEvent.minimumProsperity <= tmp.averageProsperity &&
                                tmp.averageProsperity <= cEvent.maximumProsperity) {
                                if (cEvent.rangeSettlementsAffected.min == 0 &&
                                    cEvent.rangeSettlementsAffected.max == 0 ||
                                    Find.World.GetComponent<FactionFC>().settlements.Count() >=
                                    cEvent.rangeSettlementsAffected.min) {
                                    //if doesn't require resource or if required resource has more than 1 production
                                    if (cEvent.requiredResource == null
                                        ? Find.World.GetComponent<FactionFC>().returnResource(cEvent.requiredResource)
                                            .assignedWorkers > 0
                                        : true || (cEvent.requiredResource == "research" &&
                                                   TraitUtilsFC.returnResearchAmount() > 0)) {
                                        //if event is not incompatible with any currently-running events
                                        foreach (FCEvent evt in Find.World.GetComponent<FactionFC>().events) {
                                            if (evt.def != null) {
                                                if (cEvent == evt.def) {
                                                    //if there's already the same event running
                                                    return false;
                                                }

                                                foreach (FCEventDef inEvt in evt.def.incompatibleEvents) {
                                                    if (cEvent == inEvt) {
                                                        //if not compatible
                                                        return false;
                                                    }
                                                }
                                            }
                                        }

                                        //If there are no required biomes
                                        if (cEvent.applicableBiomes.Count() != 0) {
                                            foreach (string biome in cEvent.applicableBiomes) {
                                                // if(cEvent.)
                                            }
                                        }

                                        //else if compatible
                                        return true;
                                    }

                                    return false;
                                }

                                return false;
                            }

                            return false;
                        }

                        return false;
                    }

                    return false;
                }

                return false;
            }

            return false;
        }

        public static FCEventDef returnRandomEvent() {
            //create new list
            List<FCEventDef> tmpEventList = new List<FCEventDef>();

            foreach (FCEventDef eventDef in DefDatabase<FCEventDef>.AllDefsListForReading) {
                if (isValidRandomEvent(eventDef)) {
                    for (int i = 0; i < eventDef.weight; i++) {
                        tmpEventList.Add(eventDef);
                        //Log.Message(eventDef.label);
                    }
                }
            }

            if (tmpEventList.Count() != 0) {
                return tmpEventList.RandomElement();
            }

            return null;
        }

        public static FCEvent MakeEvent(FCEventDef def) {
            if (def == null) {
                return null;
            }

            FCEvent tempEvent = new FCEvent(true);
            tempEvent.def = def;
            tempEvent.timeTillTrigger = def.timeTillTrigger + Find.TickManager.TicksGame;
            return tempEvent;
        }

        public static FCEvent MakeRandomEvent(FCEventDef def, List<SettlementFC> SettlementTraitLocations) {
            FCEvent tempEvent = new FCEvent(true);
            tempEvent.def = def;
            tempEvent.timeTillTrigger = def.timeTillTrigger + Find.TickManager.TicksGame;
            tempEvent.traits = def.traits;
            tempEvent.settlementTraitLocations = new List<SettlementFC>();


            //if affects specific settlement(s) then get settlements.
            if (tempEvent.def.rangeSettlementsAffected.max != 0) {
                int numSettlements = tempEvent.def.rangeSettlementsAffected.RandomInRange;

                //Log.Message("Pre- " + numSettlements);
                //if random number of settlements more than total settlements, reset number settlements.
                if (numSettlements > Find.World.GetComponent<FactionFC>().settlements.Count()) {
                    numSettlements = Find.World.GetComponent<FactionFC>().settlements.Count();
                    //Log.Message("Post- " + numSettlements);
                }

                //List of map locations
                List<SettlementFC> settlements = new List<SettlementFC>();
                //temporary list of settlemnts.
                List<SettlementFC> tmp = new List<SettlementFC>();

                if (SettlementTraitLocations == null || SettlementTraitLocations.Count == 0) {
                    foreach (SettlementFC settlement in Find.World.GetComponent<FactionFC>().settlements.InRandomOrder()
                    ) {
                        if (tempEvent.def.requiredResource != "") {
                            //if there is a required resource
                            if (settlement.returnResource(tempEvent.def.requiredResource).assignedWorkers > 0) {
                                //if have someone working on that resource
                                tmp.Add(settlement);
                            }
                        } else {
                            tmp.Add(settlement);
                        }
                    }

                    while (tmp.Count() > 0) {
                        SettlementFC cSettlement = tmp.RandomElement();
                        if (settlements.Count() < numSettlements) {
                            settlements.Add(cSettlement);
                        }

                        tmp.Remove(cSettlement);
                    }

                    tempEvent.settlementTraitLocations.AddRange(settlements);
                } else {
                    tempEvent.settlementTraitLocations.AddRange(SettlementTraitLocations);
                    //Log.Message(tempEvent.settlementTraitLocations.Count().ToString());
                }

                //foreach(int loc in tempEvent.settlementTraitLocations)
                //{
                //Log.Message("Location: " + loc);
                //}
            }

            //if event has options
            //open event option window
            //Log.Message("option count: " + tempEvent.def.options.Count().ToString());
            if (tempEvent.def.options.Count > 0 && tempEvent.def.activateAtStart) {
                Find.WindowStack.Add(new FCOptionWindow(tempEvent.def, tempEvent));
                return null;
            }

            return tempEvent;

            //
        }


        public static void ProcessEvents(in List<FCEvent> events) {
            FactionFC faction = Find.World.GetComponent<FactionFC>();
            for (int i = 0; i < events.Count; i++) {
                //Log.Message(events[i].timeTillTrigger.ToString());
                if (events[i].timeTillTrigger > Find.TickManager.TicksGame) continue;
                //Make custom event functions here

                FCEvent temp = new FCEvent(true);
                if (temp.def.defName == "settlementBeingAttacked") {
                    Log.Message("Found attacked FCEvent");
                }

                temp = events[i];
                //remove event (stop spam?)
                faction.events.RemoveAt(i);

                switch (temp.def.defName) {
                    case "settleNewColony": {
                            //Settle new colony event
                            //Log.Message(events[i].def.defName + " event triggered" + Find.TickManager.TicksGame)
                            faction.addExperienceToFactionLevel(10f);
                            if (Find.World.info.name == temp.planetName) {
                                FactionColonies.createPlayerColonySettlement(temp.location, true, temp.planetName);
                            } else {
                                FactionColonies.createPlayerColonySettlement(temp.location, false, temp.planetName);
                                faction.createSettlementQueue.Add(new SettlementSoS2Info(temp.planetName, temp.location));
                            }

                            faction.settlementCaravansList.Remove(temp.location.ToString());
                            break;
                        }
                    case "taxColony" when faction.returnSettlementFCIDByLocation(temp.source, temp.planetName) == -1:
                        continue;
                    case "taxColony": {
                            Messages.Message(
                                "TaxesFrom".Translate() + " " + faction.getSettlementName(temp.source, temp.planetName) +
                                " " + "HaveBeenDelivered".Translate() + "!", MessageTypeDefOf.PositiveEvent);
                            string str = "TaxesFrom".Translate() + " " +
                                         faction.getSettlementName(temp.source, temp.planetName) + " " +
                                         "HaveBeenDelivered".Translate() + "!";

                            foreach (Thing thing in temp.goods) {
                                str = str + "\n" + thing.LabelCap;
                            }

                            Find.LetterStack.ReceiveLetter("TaxesHaveArrived".Translate(), str, LetterDefOf.PositiveEvent);
                            PaymentUtil.deliverThings(temp);
                            break;
                        }
                    case "constructBuilding":
                        //Create building
                        faction.settlements[faction.returnSettlementFCIDByLocation(temp.source, temp.planetName)]
                            .constructBuilding(temp.building, temp.buildingSlot);
                        Messages.Message(
                            temp.building.label + " " + "HasBeenConstructedAt".Translate() + " " +
                            faction.settlements[faction.returnSettlementFCIDByLocation(temp.source, temp.planetName)]
                                .name + "!", MessageTypeDefOf.PositiveEvent);
                        break;
                    case "upgradeSettlement": {
                            if (faction.returnSettlementByLocation(temp.location, temp.planetName) != null) {
                                //if settlement is not null
                                SettlementFC settlement =
                                    faction.returnSettlementByLocation(temp.location, temp.planetName);
                                settlement.upgradeSettlement();
                                Find.LetterStack.ReceiveLetter("居住地升级",
                                    settlement.name + " " + "HasBeenUpgraded".Translate() + " " +
                                    settlement.settlementLevel + "!", LetterDefOf.PositiveEvent);
                            }

                            break;
                        }
                    case "captureEnemySettlement":
                    case "raidEnemySettlement":
                    case "enslaveEnemySettlement":
                        //Process military event
                        faction.returnSettlementByLocation(temp.location, temp.planetName).processMilitaryEvent();
                        break;
                    case "cooldownMilitary": {
                            //Process military event
                            if (temp.planetName == null) {
                                Log.Message(
                                    "temp.planetName null in FCEvent.ProcessEvents. Please report to Empire Mod with what you used this settlement for.");
                                Messages.Message(
                                    "temp.planetName null in FCEvent.ProcessEvents. Please report to Empire Mod with what you used this settlement for.",
                                    MessageTypeDefOf.NegativeEvent);
                            }

                            faction.returnSettlementByLocation(temp.location, temp.planetName).returnMilitary(true);
                            break;
                        }
                }

                if (temp.def.defName == "settlementBeingAttacked") {

                    WorldSettlementFC worldSettlement = temp.settlementFCDefending.worldSettlement;

                    worldSettlement.startDefense(temp, () => setupAttack(worldSettlement, temp));
                } else //if undefined event
                  {
                    if (temp.def.goods.Count > 0) {
                        PaymentUtil.deliverThings(temp);
                    }

                    // Log.Message(temp.def.label + " " + temp.def.randomThingValue + " value:thing " + temp.def.randomThingType);
                    if (temp.def.randomThingValue > 0 && temp.def.randomThingType != "") {
                        List<Thing> list =
                            PaymentUtil.generateThing(temp.def.randomThingValue, temp.def.randomThingType);

                        string str;
                        str = "GoodsReceivedFollowing".Translate(temp.def.label);
                        foreach (Thing thing in list) {
                            str = str + "\n" + thing.LabelCap;
                        }

                        Find.LetterStack.ReceiveLetter("GoodsReceived".Translate(), str, LetterDefOf.PositiveEvent);
                        PaymentUtil.deliverThings(list);
                    }
                }

                //If has loot to give
                if (temp.def.loot.Any()) {
                    List<Thing> list = temp.def.loot.Select(thing => ThingMaker.MakeThing(thing)).ToList();

                    PaymentUtil.deliverThings(list);
                }

                if (temp.loot.Any()) {
                    List<Thing> list = new List<Thing>();

                    foreach (ThingDef thing in temp.loot) {
                        //list.Add(ThingMaker.MakeThing(thing));
                    }

                    PaymentUtil.deliverThings(list);
                }


                //check if event has a location, if does, add traits to that specific location;
                if (temp.settlementTraitLocations.Any()) //if has specific locations
                {
                    //Remove null settlements
                    ResetClear:
                    foreach (SettlementFC settlement in temp.settlementTraitLocations) {
                        if (settlement == null) {
                            temp.settlementTraitLocations.Remove(settlement);
                            goto ResetClear;
                        }
                    }

                    foreach (SettlementFC location in temp.settlementTraitLocations) {
                        if (location != null) {
                            foreach (FCTraitEffectDef trait in temp.traits) {
                                //Log.Message(trait.label);
                                while (location.traits.Contains(trait)) {
                                    location.traits.Remove(trait);
                                }
                            }

                            //prosperity loss calculation
                            location.prosperity -= temp.prosperityLost;
                        }
                    }
                } else {
                    //if no specific location then faction wide

                    foreach (FCTraitEffectDef trait in temp.traits) {
                        while (faction.traits.Contains(trait)) {
                            faction.traits.Remove(trait);
                        }
                    }

                    foreach (SettlementFC settlement in faction.settlements) {
                        settlement.prosperity -= temp.prosperityLost;
                    }
                }

                //if have options
                if (temp.def != null && temp.def.options.Count > 0 && temp.def.activateAtStart == false) {
                    Find.WindowStack.Add(new FCOptionWindow(temp.def, temp));
                }

                //if has following event
                if (temp.def.eventFollows) {
                    FCEvent tempEvent = new FCEvent(true);
                    if (temp.def.splitEventFollows) //if a split event
                    {
                        //remove null settlement references
                        float baseChance = temp.def.splitEventChance;
                        int roll = Rand.Range(1, 100);
                        if (temp.def.settlementsCarryOver) {
                            //if settlements carry
                            if (roll <= baseChance) {
                                //first event
                                tempEvent = MakeRandomEvent(temp.def.followingEvent, temp.settlementTraitLocations);
                            } else {
                                //if second event
                                tempEvent = MakeRandomEvent(temp.def.followingEvent2,
                                    temp.settlementTraitLocations);
                            }
                        } else {
                            if (roll <= baseChance) {
                                //first event
                                tempEvent = MakeRandomEvent(temp.def.followingEvent, null);
                            } else {
                                //if second event
                                tempEvent = MakeRandomEvent(temp.def.followingEvent2, null);
                            }
                        }
                    } else {
                        if (temp.def.settlementsCarryOver) {
                            //if settlements carry
                            tempEvent = MakeRandomEvent(temp.def.followingEvent, temp.settlementTraitLocations);
                        } else {
                            tempEvent = MakeRandomEvent(temp.def.followingEvent, null);
                        }
                    }


                    if (tempEvent != null) {
                        faction.addEvent(tempEvent);


                        //letter

                        string settlementString = "";
                        foreach (SettlementFC loc in tempEvent.settlementTraitLocations) {
                            //Log.Message(loc.ToString());
                            if (settlementString == "") {
                                settlementString = settlementString + " " + loc.name;
                            } else {
                                settlementString = settlementString + ", " + loc.name;
                            }
                        }

                        if (settlementString != "") {
                            Find.LetterStack.ReceiveLetter(tempEvent.def.label,
                                tempEvent.def.desc + "\n" + "EventAffectingSettlements".Translate() +
                                settlementString, LetterDefOf.NeutralEvent);
                        } else {
                            Find.LetterStack.ReceiveLetter(tempEvent.def.label, tempEvent.def.desc,
                                LetterDefOf.NeutralEvent);
                        }
                    }
                }

                temp.runAction();
            }
        }

        private static void setupAttack(WorldSettlementFC worldSettlement, FCEvent temp) {
            IncidentParms parms = new IncidentParms {
                target = worldSettlement.Map,
                faction = temp.militaryForceAttackingFaction,
                generateFightersOnly = true,
                raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                raidNeverFleeIndividual = true
            };
            parms.points = IncidentWorker_Raid.AdjustedRaidPoints(
                (float)temp.militaryForceAttacking.forceRemaining * 100,
                PawnsArrivalModeDefOf.EdgeWalkIn, parms.raidStrategy,
                parms.faction, PawnGroupKindDefOf.Combat);
            parms.raidArrivalMode = ResolveRaidArriveMode(parms) ?? PawnsArrivalModeDefOf.EdgeWalkIn;
            parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms);

            List<Pawn> attackers = PawnGroupMakerUtility.GeneratePawns(
                IncidentParmsUtility.GetDefaultPawnGroupMakerParms(
                    PawnGroupKindDefOf.Combat, parms, true)).ToList();
            if (!attackers.Any()) {
                Log.Error("Got no pawns spawning raid from parms " + parms);
            }

            parms.raidArrivalMode.Worker.Arrive(attackers, parms);

            worldSettlement.attackers = attackers;
            worldSettlement.attackerForce = temp.militaryForceAttacking;
            worldSettlement.defenderForce = temp.militaryForceDefending;
            LordMaker.MakeNewLord(
                parms.faction, new LordJob_HuntColonists(parms.raidArrivalMode != PawnsArrivalModeDefOf.CenterDrop),
                worldSettlement.Map, attackers);
        }

        private static PawnsArrivalModeDef ResolveRaidArriveMode(IncidentParms parms) {
            return
                parms.raidStrategy.arriveModes.Where(testing => testing.Worker.CanUseWith(parms))
                    .TryRandomElementByWeight(
                        x => x.Worker.GetSelectionWeight(parms), out PawnsArrivalModeDef output)
                    ? output
                    : PawnsArrivalModeDefOf.EdgeWalkIn;
        }

        public static void createTaxEvent(BillFC bill) {
            FactionFC faction = Find.World.GetComponent<FactionFC>();

            FCEvent tmp = MakeEvent(FCEventDefOf.taxColony);

            if (bill.settlement != null && faction.settlements.Contains(bill.settlement)) {
                tmp.source = bill.settlement.mapLocation; //source location
                tmp.customDescription = "TaxesFromSettlementAreBeingDelivered".Translate(bill.settlement.name);
                tmp.planetName = bill.settlement.planetName;
            } else {
                tmp.source = -1;
                tmp.planetName = Find.World.info.name;
                tmp.customDescription = "TaxesFromSettlementAreBeingDelivered".Translate("Null");
            }

            tmp.location = faction.capitalLocation;
            tmp.timeTillTrigger = Find.TickManager.TicksGame +
                                  FactionColonies.ReturnTicksToArrive(tmp.source, tmp.location);
            tmp.hasCustomDescription = true;
            //add tithe
            tmp.goods = bill.taxes.itemTithes;

            if (bill.taxes.silverAmount > 0) //if getting paid, add silver to tithe
            {
                //add to tithe
                //tmp.goods.Add()
                int silverTotal = (int)bill.taxes.silverAmount;
                while (silverTotal > 0) {
                    Thing thing = ThingMaker.MakeThing(ThingDefOf.Silver);

                    if (silverTotal > thing.def.stackLimit) {
                        thing.stackCount = thing.def.stackLimit;
                        silverTotal -= thing.def.stackLimit;
                    } else {
                        //if not above stack limit
                        thing.stackCount = silverTotal;
                        silverTotal -= silverTotal;
                    }

                    tmp.goods.Add(thing);
                }
            } else if (bill.taxes.silverAmount < 0) //if paying money
              {
                //remove money from colony
                PaymentUtil.paySilver((int)(-1 * (bill.taxes.silverAmount)));
            }


            // add event to queue and remove bill
            if (tmp.goods.Count > 0) //if any silver or tithe in bill create event. else, well, don't
            {
                faction.addEvent(tmp);
            }

            faction.Bills.Remove(bill);
        }
    }

    public class FCEvent : IExposable, ILoadReferenceable {
        public FCEvent() {
            //Constructor
        }

        public FCEvent(bool New) {
            loadID = Find.World.GetComponent<FactionFC>().GetNextEventID();
        }

        public void ExposeData() {
            //Ref
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref location, "location");
            Scribe_Values.Look(ref planetName, "planetName");
            Scribe_Values.Look(ref timeTillTrigger, "timeTillTrigger");
            Scribe_Values.Look(ref source, "source");
            Scribe_Values.Look(ref hasDestination, "hasDestination");
            Scribe_Collections.Look(ref settlementTraitLocations, "settlementTraitLocations", LookMode.Reference);
            Scribe_Collections.Look(ref goods, "goods", LookMode.Deep);
            Scribe_Collections.Look(ref traits, "traits", LookMode.Def);
            Scribe_Values.Look(ref loadID, "loadID");

            Scribe_Values.Look(ref buildingSlot, "buildingSlot");

            Scribe_Defs.Look(ref building, "building");


            Scribe_Values.Look(ref hasCustomDescription, "hasCustomDescription");
            Scribe_Values.Look(ref customDescription, "customDescription");

            //Random Events
            Scribe_Values.Look(ref isRandomEvent, "isRandomEvent");
            Scribe_Values.Look(ref perpetual, "perpetual");
            Scribe_Values.Look(ref activateAtStart, "activateatStart");
            Scribe_Collections.Look(ref pawnSpawn, "pawnSpawn", LookMode.Deep);
            Scribe_Values.Look(ref requiredWealth, "requiredWealth");
            Scribe_Values.Look(ref rangeSettlementsAffected, "rangeSettlementsAffected");
            Scribe_Values.Look(ref settlementsCarryOver, "settlementsCarryOver");
            Scribe_Values.Look(ref weight, "eventValue");
            Scribe_Values.Look(ref minimumHappiness, "minimumHappiness");
            Scribe_Values.Look(ref maximumHappiness, "maximumHappiness");
            Scribe_Values.Look(ref minimumLoyalty, "minimumLoyalty");
            Scribe_Values.Look(ref maximumLoyalty, "maximumLoyalty");
            Scribe_Values.Look(ref minimumUnrest, "minimumUnrest");
            Scribe_Values.Look(ref maximumUnrest, "maximumUnrest");
            Scribe_Values.Look(ref minimumProsperity, "minimumProsperity");
            Scribe_Values.Look(ref maximumProsperity, "maximumProsperity");
            Scribe_Values.Look(ref requiredResource, "requiredResource");
            Scribe_Values.Look(ref randomThingValue, "randomThingValue");
            Scribe_Values.Look(ref randomThingType, "randomThingType");
            Scribe_Collections.Look(ref options, "options", LookMode.Def);
            Scribe_Collections.Look(ref incompatibleEvents, "incompatibleEvents", LookMode.Def);
            Scribe_Values.Look(ref prosperityLost, "prosperityLost");
            Scribe_Values.Look(ref eventFollows, "eventFollows");
            Scribe_Defs.Look(ref followingEvent, "followingEvent");
            Scribe_Defs.Look(ref followingEvent2, "followingEvent2");
            Scribe_Values.Look(ref splitEventFollows, "splitEventFol*lows");
            Scribe_Values.Look(ref splitEventChance, "splitEventChance");
            Scribe_Values.Look(ref optionDescription, "optionDescription");
            Scribe_Collections.Look(ref applicableBiomes, "applicableBiomes", LookMode.Value);
            Scribe_Collections.Look(ref loot, "loot", LookMode.Def);
            Scribe_Values.Look(ref classToRun, "classToRun");
            Scribe_Values.Look(ref classMethodToRun, "classMethodToRun");
            Scribe_Values.Look(ref passEventToClassMethodToRun, "passEventToClassMethodToRun");

            //Military stuff
            Scribe_Deep.Look(ref militaryForceAttacking, "militaryForceAttacking");
            Scribe_References.Look(ref militaryForceAttackingFaction, "militaryForceAttackingFaction");
            Scribe_Deep.Look(ref militaryForceDefending, "militaryForceDefending");
            Scribe_References.Look(ref militaryForceDefendingFaction, "militaryForceDefendingFaction");
            Scribe_References.Look(ref settlementFCDefending, "SettlementFCDefending");
            Scribe_Values.Look(ref isMilitaryEvent, "isMilitaryEvent");
        }

        public FCEventDef def = new FCEventDef();
        public int location = -1; //destination
        public string planetName;
        public int timeTillTrigger = -1;
        public int loadID = -1;
        public int source = -1; //source location
        public bool hasDestination; //if has destination
        public int buildingSlot = -1;
        public BuildingFCDef building;
        public List<SettlementFC> settlementTraitLocations = new List<SettlementFC>();
        public List<Thing> goods = new List<Thing>();
        public List<FCTraitEffectDef> traits = new List<FCTraitEffectDef>();
        public bool hasCustomDescription;
        public string customDescription = "";


        //Random Event Information
        public bool isRandomEvent;
        public bool perpetual;
        public bool activateAtStart;
        public List<Pawn> pawnSpawn = new List<Pawn>();
        public int requiredWealth;
        public IntRange rangeSettlementsAffected = new IntRange(0, 0);
        public bool settlementsCarryOver = true;
        public int weight;
        public int minimumHappiness;
        public int maximumHappiness = 100;
        public int minimumLoyalty;
        public int maximumLoyalty = 100;
        public int minimumUnrest;
        public int maximumUnrest = 100;
        public int minimumProsperity;
        public int maximumProsperity = 100;
        public List<FCOptionDef> options = new List<FCOptionDef>();
        public string requiredResource = "";
        public int randomThingValue;
        public string randomThingType = "";
        public List<FCEventDef> incompatibleEvents = new List<FCEventDef>();
        public int prosperityLost;
        public bool eventFollows;
        public FCEventDef followingEvent;
        public FCEventDef followingEvent2;
        public bool splitEventFollows;
        public int splitEventChance = 50;
        public string optionDescription = "";
        public List<string> applicableBiomes = new List<string>();
        public List<ThingDef> loot = new List<ThingDef>();
        public string classToRun = "";
        public string classMethodToRun = "";
        public bool passEventToClassMethodToRun;

        //Military Force stuff
        public militaryForce militaryForceAttacking;
        public Faction militaryForceAttackingFaction;
        public militaryForce militaryForceDefending;
        public Faction militaryForceDefendingFaction;
        public SettlementFC settlementFCDefending;
        public bool isMilitaryEvent;


        public string GetUniqueLoadID() {
            return "FCEvent_" + loadID;
        }

        public void runAction() {
            if (!classToRun.NullOrEmpty() && !classMethodToRun.NullOrEmpty()) {
                Type typ = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) {
                    var type1 = a.GetType(classToRun);
                    if (type1 != null)
                        typ = type1;
                }

                var obj = Activator.CreateInstance(typ);
                object[] paramArgu = passEventToClassMethodToRun ? new object[] { this } : new object[] { };

                Traverse.Create(obj).Method(classMethodToRun, paramArgu).GetValue();
            }
        }
    }


    public class FCEventDef : Def {
        //Event main info
        public int timeTillTrigger = -1;
        public string desc;

        //Random Event Information
        public bool isRandomEvent = false;
        public bool perpetual = false;
        public bool activateAtStart;
        public List<Pawn> pawnSpawn = new List<Pawn>();
        public int requiredWealth = 0;
        public IntRange rangeSettlementsAffected = new IntRange(0, 0);
        public bool settlementsCarryOver = true;
        public int weight = 0;
        public int minimumHappiness = 0;
        public int maximumHappiness = 100;
        public int minimumLoyalty = 0;
        public int maximumLoyalty = 100;
        public int minimumUnrest = 0;
        public int maximumUnrest = 100;
        public int minimumProsperity = 0;
        public int maximumProsperity = 100;
        public List<FCOptionDef> options = new List<FCOptionDef>();
        public string requiredResource = "";
        public int randomThingValue = 0;
        public string randomThingType = "";
        public List<FCEventDef> incompatibleEvents = new List<FCEventDef>();
        public int prosperityLost = 0;
        public bool eventFollows = false;
        public FCEventDef followingEvent = null;
        public FCEventDef followingEvent2 = null;
        public bool splitEventFollows = false;
        public int splitEventChance = 50;
        public string optionDescription = "";
        public List<string> applicableBiomes = new List<string>();
        public List<ThingDef> loot = new List<ThingDef>();
        public bool hasCustomDescription = false;
        public string customDescription = "";
        public string classToRun;
        public string classMethodToRun;
        public bool passEventToClassMethodToRun;


        //Map info
        public int location = -1;
        public bool hasDestination = false;


        //Traits during event
        public List<FCTraitEffectDef> traits = new List<FCTraitEffectDef>();


        //Benefits after eventtime info
        public List<Thing> goods = new List<Thing>();

        //Military Force stuff
        public militaryForce militaryForceAttacking = null;
        public Faction militaryForceAttackingFaction = null;
        public militaryForce militaryForceDefending = null;
        public Faction militaryForceAttackingDefending = null;
        public SettlementFC settlementFCDefending = null;
        public bool isMilitaryEvent = false;
    }

    [DefOf]
    public class FCEventDefOf {
        //List Events here - loads events at start
        //public static FCEventDef settleNewColony;
        public static FCEventDef Null;
        public static FCEventDef settleNewColony;
        public static FCEventDef taxColony;
        public static FCEventDef constructBuilding;
        public static FCEventDef enactSettlementPolicy;
        public static FCEventDef enactFactionPolicy;
        public static FCEventDef upgradeSettlement;
        public static FCEventDef raidEnemySettlement;
        public static FCEventDef enslaveEnemySettlement;
        public static FCEventDef captureEnemySettlement;
        public static FCEventDef cooldownMilitary;
        public static FCEventDef settlementBeingAttacked;

        static FCEventDefOf() {
            DefOfHelper.EnsureInitializedInCtor(typeof(FCEventDefOf));
        }
    }
}