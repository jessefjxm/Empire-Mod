﻿using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace FactionColonies {
    public class WorldSettlementFC : MapParent, ITrader, ITraderRestockingInfoProvider {
        public static readonly FieldInfo traitCachedIcon = typeof(WorldObjectDef).GetField("expandingIconTextureInt",
            BindingFlags.NonPublic | BindingFlags.Instance);

        public static readonly FieldInfo traitCachedMaterial = typeof(WorldObjectDef).GetField("material",
            BindingFlags.NonPublic | BindingFlags.Instance);

        public WorldSettlementTraderTracker trader;

        public SettlementFC settlement;

        public List<Pawn> attackers = new List<Pawn>();

        public List<Pawn> defenders = new List<Pawn>();

        public List<CaravanSupporting> supporting = new List<CaravanSupporting>();

        public militaryForce defenderForce;

        public militaryForce attackerForce;

        public string Name {
            get => settlement.name;
            set => settlement.name = value;
        }

        public override string Label => Name;


        public TraderKindDef TraderKind {
            get {
                if (trader.settlement == null) trader.settlement = this;
                return trader?.TraderKind;
            }
        }

        public IEnumerable<Thing> Goods => trader?.StockListForReading;

        public int RandomPriceFactorSeed => trader?.RandomPriceFactorSeed ?? 0;

        public string TraderName => trader?.TraderName;

        public bool CanTradeNow => trader != null && trader.CanTradeNow;

        public float TradePriceImprovementOffsetForPlayer => trader?.TradePriceImprovementOffsetForPlayer ?? 0.0f;

        public TradeCurrency TradeCurrency => TraderKind.tradeCurrency;

        public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn playerNegotiator) => trader?.ColonyThingsWillingToBuy(playerNegotiator);

        public void GiveSoldThingToTrader(Thing toGive, int countToGive, Pawn playerNegotiator) => trader.GiveSoldThingToTrader(toGive, countToGive, playerNegotiator);

        public void GiveSoldThingToPlayer(Thing toGive, int countToGive, Pawn playerNegotiator) => trader.GiveSoldThingToPlayer(toGive, countToGive, playerNegotiator);

        public bool EverVisited => trader.EverVisited;

        public bool RestockedSinceLastVisit => trader.RestockedSinceLastVisit;

        public int NextRestockTick => trader.NextRestockTick;

        public override void PostMake() {
            trader = new WorldSettlementTraderTracker(this);

            updateTechIcon();
            def.expandingIconTexture = "FactionIcons/" + Find.World.GetComponent<FactionFC>().factionIconPath;
            traitCachedIcon.SetValue(def, ContentFinder<Texture2D>.Get(def.expandingIconTexture));
            base.PostMake();

            attackers = new List<Pawn>();
            defenders = new List<Pawn>();
            supporting = new List<CaravanSupporting>();
        }

        public void updateTechIcon() {
            TechLevel techLevel = Find.World.GetComponent<FactionFC>().techLevel;
            Log.Message("Got tech level " + techLevel);
            if (techLevel == TechLevel.Animal || techLevel == TechLevel.Neolithic) {
                def.texture = "World/WorldObjects/TribalSettlement";
            } else {
                def.texture = "World/WorldObjects/DefaultSettlement";
            }

            traitCachedMaterial.SetValue(def, MaterialPool.MatFrom(def.texture,
                ShaderDatabase.WorldOverlayTransparentLit, WorldMaterials.WorldObjectRenderQueue));
        }

        public override void ExposeData() {
            base.ExposeData();
            Scribe_References.Look(ref settlement, "settlement");
            Scribe_Collections.Look(ref attackers, "attackers", LookMode.Reference);
            Scribe_Collections.Look(ref defenders, "defenders", LookMode.Reference);
            Scribe_Collections.Look(ref supporting, "supporting", LookMode.Reference);
            Scribe_Deep.Look(ref defenderForce, "defenderForce");
            Scribe_Deep.Look(ref attackerForce, "attackerForce");
            Scribe_Deep.Look(ref trader, "trader");
        }

        public override IEnumerable<Gizmo> GetGizmos() {
            if (!settlement.isUnderAttack) yield break;
            yield return new Command_Action {
                defaultLabel = "DefendColony".Translate(),
                defaultDesc = "DefendColonyDesc".Translate(),
                icon = TexLoad.iconMilitary,
                action = () => {
                    startDefense(MilitaryUtilFC.returnMilitaryEventByLocation(settlement.mapLocation),
                        () => { });
                }
            };

            FactionFC faction = Find.World.GetComponent<FactionFC>();

            if (attackers.Any()) {
                yield break;
            }

            yield return new Command_Action {
                defaultLabel = "DefendSettlement".Translate(),
                defaultDesc = "",
                icon = TexLoad.iconCustomize,
                action = delegate {
                    List<FloatMenuOption> list = new List<FloatMenuOption>();

                    FCEvent evt = MilitaryUtilFC.returnMilitaryEventByLocation(settlement.mapLocation);
                    if (evt == null) return;
                    list.Add(new FloatMenuOption(
                        "SettlementDefendingInformation".Translate(
                            evt.militaryForceDefending.homeSettlement.name,
                            evt.militaryForceDefending.militaryLevel), null,
                        MenuOptionPriority.High));
                    list.Add(new FloatMenuOption("ChangeDefendingForce".Translate(), delegate {
                        List<FloatMenuOption> settlementList = new List<FloatMenuOption>
                        {
                            new FloatMenuOption(
                                "ResetToHomeSettlement".Translate(settlement.settlementMilitaryLevel),
                                delegate { MilitaryUtilFC.changeDefendingMilitaryForce(evt, settlement); },
                                MenuOptionPriority.High)
                        };


                        settlementList.AddRange(from foundSettlement in faction.settlements
                                                where foundSettlement.isMilitaryValid() && foundSettlement != settlement
                                                select new FloatMenuOption(foundSettlement.name + " " +
                                                                           "ShortMilitary".Translate() + " " +
                                                                           foundSettlement.settlementMilitaryLevel + " - " +
                                                                           "FCAvailable".Translate() + ": " +
                                                                           (!foundSettlement.isMilitaryBusySilent()).ToString(), delegate {
                                                                               if (foundSettlement.isMilitaryBusy()) {
                                                                                   //military is busy
                                                                               } else {
                                                                                   MilitaryUtilFC.changeDefendingMilitaryForce(evt, foundSettlement);
                                                                               }
                                                                           }));

                        if (settlementList.Count == 0) {
                            settlementList.Add(
                                new FloatMenuOption("NoValidMilitaries".Translate(), null));
                        }

                        FloatMenu floatMenu2 = new FloatMenu(settlementList) {
                            vanishIfMouseDistant = true
                        };
                        Find.WindowStack.Add(floatMenu2);
                    }));

                    FloatMenu floatMenu = new FloatMenu(list) {
                        vanishIfMouseDistant = true
                    };
                    Find.WindowStack.Add(floatMenu);
                }
            };
        }

        public void CaravanDefend(Caravan caravan) {
            startDefense(
                MilitaryUtilFC.returnMilitaryEventByLocation(caravan.Tile), () => {
                    CaravanSupporting caravanSupporting = new CaravanSupporting {
                        pawns = caravan.pawns.InnerListForReading.ListFullCopy()
                    };
                    supporting.Add(caravanSupporting);
                    if (!caravan.Destroyed) {
                        caravan.Destroy();
                    }

                    IntVec3 enterCell = FindNearEdgeCell(Map);
                    foreach (Pawn pawn in caravanSupporting.pawns) {
                        IntVec3 loc =
                            CellFinder.RandomSpawnCellForPawnNear(enterCell, Map);
                        GenSpawn.Spawn(pawn, loc, Map, Rot4.Random);
                        if (defenders.Any()) {
                            defenders[0].GetLord().AddPawn(pawn);
                        } else {
                            LordMaker.MakeNewLord(
                                FactionColonies.getPlayerColonyFaction(), new LordJob_DefendColony(new Dictionary<Pawn, Pawn>()), Map, caravanSupporting.pawns);
                        }
                    }
                    defenders.AddRange(caravanSupporting.pawns);
                });
        }
        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan) {
            if (settlement.isUnderAttack) {
                yield return new Command_Action {
                    defaultLabel = "DefendColony".Translate(),
                    defaultDesc = "DefendColonyDesc".Translate(),
                    icon = TexLoad.iconMilitary,
                    action = () => {
                        startDefense(MilitaryUtilFC.returnMilitaryEventByLocation(settlement.mapLocation),
                            () => CaravanDefend(caravan));
                    }
                };
            } else {
                Command_Action action = (Command_Action)CaravanVisitUtility.TradeCommand(caravan, Faction, trader.TraderKind);

                Pawn bestNegotiator = BestCaravanPawnUtility.FindBestNegotiator(caravan, Faction, trader.TraderKind);
                action.action = () => {
                    if (!CanTradeNow)
                        return;
                    Find.WindowStack.Add(new Dialog_Trade(bestNegotiator, this));
                    PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(Goods.OfType<Pawn>(),
                        "LetterRelatedPawnsTradingWithSettlement"
                            .Translate((NamedArgument)Faction.OfPlayer.def.pawnsPlural), LetterDefOf.NeutralEvent);
                };

                yield return action;
            }
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(
            Caravan caravan) {
            if (!settlement.isUnderAttack) {
                foreach (FloatMenuOption option in WorldSettlementTradeAction.GetFloatMenuOptions(caravan, this)) {
                    yield return option;
                }
            } else {
                foreach (FloatMenuOption option in WorldSettlementDefendAction.GetFloatMenuOptions(caravan, this)) {
                    yield return option;
                }
            }
        }

        private void deleteMap() {
            if (Map == null) return;
            Map.lordManager.lords.Clear();

            CameraJumper.TryJump(settlement.mapLocation);
            //Prevent player from zooming back into the settlement
            Current.Game.CurrentMap = Find.AnyPlayerHomeMap;

            //Ignore any empty caravans
            foreach (CaravanSupporting caravanSupporting in supporting.Where(supporting => supporting.pawns.Find(
                pawn => !pawn.Downed && !pawn.Dead) != null)) {
                CaravanFormingUtility.FormAndCreateCaravan(caravanSupporting.pawns.Where(pawn => pawn.Spawned),
                    Faction.OfPlayer, settlement.mapLocation, settlement.mapLocation, -1);
            }

            if (Map.mapPawns?.AllPawnsSpawned == null) return;

            //Despawn removes them from AllPawnsSpawned, so we copy it
            foreach (Pawn pawn in Map.mapPawns.AllPawnsSpawned.ListFullCopy()) {
                pawn.DeSpawn();
            }
        }

        public override bool ShouldRemoveMapNow(out bool removeWorldObject) {
            removeWorldObject = false;
            return !defenders.Any() && !attackers.Any();
        }

        public void startDefense(FCEvent evt, Action after) {
            if (FactionColonies.Settings().settlementsAutoBattle) {
                bool won = SimulateBattleFc.FightBattle(evt.militaryForceAttacking, evt.militaryForceDefending) == 1;
                endBattle(won, (int)evt.militaryForceDefending.forceRemaining);
                return;
            }
            if (defenderForce == null) {
                endBattle(false, 0);
                return;
            }
            LongEventHandler.QueueLongEvent(() => {
                if (Map == null) {
                    MapGenerator.GenerateMap(new IntVec3(70 + settlement.settlementLevel * 10,
                            1, 70 + settlement.settlementLevel * 10), this,
                        MapGeneratorDef, ExtraGenStepDefs).mapDrawer.RegenerateEverythingNow();
                }

                zoomIntoTile(evt);
                after.Invoke();
            },
                "GeneratingMap", false, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
        }

        public override void Tick() {
            base.Tick();
            trader?.TraderTrackerTick();
        }

        private void zoomIntoTile(FCEvent evt) {
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            if (Current.Game.CurrentMap != Map && !defenders.Any()) {
                if (evt == null) {
                    Log.Warning("Aborting defense, null FCEvent!");
                    return;
                }

                evt.timeTillTrigger = Find.TickManager.TicksGame;
                militaryForce force = MilitaryUtilFC.returnDefendingMilitaryForce(evt);
                if (force == null) {
                    return;
                }

                force.homeSettlement.militaryBusy = true;

                foreach (Building building in Map.listerBuildings.allBuildingsColonist) {
                    FloodFillerFog.FloodUnfog(building.InteractionCell, Map);
                }

                generateFriendlies(force);
            }

            if (Current.Game.CurrentMap == Map && Find.World.renderer.wantedMode != WorldRenderMode.Planet) {
                return;
            }

            if (defenders.Any()) {
                CameraJumper.TryJump(new GlobalTargetInfo(defenders[0]));
            } else if (Map.mapPawns.AllPawnsSpawned.Any()) {
                CameraJumper.TryJump(new GlobalTargetInfo(Map.mapPawns.AllPawnsSpawned[0]));
            } else {
                CameraJumper.TryJump(new IntVec3(Map.Size.x / 2, 0, Map.Size.z / 2), Map);
            }
        }

        public override void Notify_CaravanFormed(Caravan caravan) {
            List<CaravanSupporting> foundCaravan = new List<CaravanSupporting>();
            foreach (Pawn found in caravan.pawns) {
                if (found.GetLord() != null) {
                    found.GetLord().ownedPawns.Remove(found);
                }

                foreach (CaravanSupporting caravanSupporting in
                    supporting.Where(caravanSupporting => caravanSupporting.pawns.Contains(found))) {
                    foundCaravan.Add(caravanSupporting);
                    caravanSupporting.pawns.Remove(found);
                    break;
                }
            }

            foreach (CaravanSupporting caravanSupporting in foundCaravan.Where(caravanSupporting =>
                caravanSupporting.pawns.Find(pawn => !pawn.Downed &&
                                                     !pawn.Dead && !pawn.AnimalOrWildMan()) == null)) {
                //Prevent removing while creating end battle caravans
                if (settlement.isUnderAttack) {
                    supporting.Remove(caravanSupporting);
                }

                /*It appears vanilla handles this automatically
                foreach (Pawn animal in caravanSupporting.supporting.FindAll(pawn => pawn.AnimalOrWildMan()))
                {
                    animal.holdingOwner = null;
                    animal.DeSpawn();
                    Find.WorldPawns.PassToWorld(animal);
                    caravan.pawns.TryAdd(animal);
                }*/
            }

            //Appears to not happen sometimes, no clue why
            foreach (Pawn pawn in caravan.pawns) {
                Map.reservationManager.ReleaseAllClaimedBy(pawn);
            }

            base.Notify_CaravanFormed(caravan);
        }

        public static IntVec3 FindNearEdgeCell(Map map) {
            bool BaseValidator(IntVec3 x) => x.Standable(map) && !x.Fogged(map);
            Faction hostFaction = map.ParentFaction;
            if (CellFinder.TryFindRandomEdgeCellWith(x => {
                if (!BaseValidator(x))
                    return false;
                if (hostFaction != null && map.reachability.CanReachFactionBase(x, hostFaction))
                    return true;
                return hostFaction == null && map.reachability.CanReachBiggestMapEdgeDistrict(x);
            }, map, CellFinder.EdgeRoadChance_Neutral, out var result))
                return CellFinder.RandomClosewalkCellNear(result, map, 5);
            if (CellFinder.TryFindRandomEdgeCellWith(BaseValidator, map, CellFinder.EdgeRoadChance_Neutral, out result))
                return CellFinder.RandomClosewalkCellNear(result, map, 5);
            Log.Warning("Could not find any valid edge cell.");
            return CellFinder.RandomCell(map);
        }

        private void generateFriendlies(militaryForce force) {
            float points = (float)(force.militaryLevel * force.militaryEfficiency * 100);
            List<Pawn> friendlies;
            Dictionary<Pawn, Pawn> riders = new Dictionary<Pawn, Pawn>();
            if (force.homeSettlement.militarySquad != null &&
                force.homeSettlement.militarySquad.mercenaries.Any()) {
                MercenarySquadFC squad = force.homeSettlement.militarySquad;

                squad.OutfitSquad(squad.settlement.militarySquad.outfit);
                squad.updateSquadStats(squad.settlement.settlementMilitaryLevel);
                squad.resetNeeds();

                friendlies = squad.AllEquippedMercenaryPawns.ToList();

                foreach (Mercenary animal in squad.animals) {
                    riders.Add(animal.handler.pawn, animal.pawn);
                }
            } else {
                IncidentParms parms = new IncidentParms {
                    target = Map,
                    faction = FactionColonies.getPlayerColonyFaction(),
                    generateFightersOnly = true,
                    raidStrategy = RaidStrategyDefOf.ImmediateAttackFriendly
                };
                parms.points = IncidentWorker_Raid.AdjustedRaidPoints(points,
                    PawnsArrivalModeDefOf.EdgeWalkIn, parms.raidStrategy,
                    parms.faction, PawnGroupKindDefOf.Combat);
                friendlies = PawnGroupMakerUtility.GeneratePawns(
                    IncidentParmsUtility.GetDefaultPawnGroupMakerParms(
                        PawnGroupKindDefOf.Combat, parms, true)).ToList();
                if (!friendlies.Any()) {
                    Log.Error("Got no pawns spawning raid from parms " + parms);
                }
            }

            foreach (Pawn friendly in friendlies) {
                IntVec3 loc;
                if (friendly.AnimalOrWildMan()) {
                    Pawn owner = riders.First(pair => pair.Value.thingIDNumber == friendly.thingIDNumber).Key;
                    CellFinder.TryFindRandomCellInsideWith(new CellRect((int)owner.DrawPos.x - 5,
                            (int)owner.DrawPos.z - 5, 10, 10),
                        testing => testing.Standable(Map) && Map.reachability.CanReachMapEdge(testing,
                            TraverseParms.For(TraverseMode.PassDoors)), out loc);
                } else {
                    int min = (70 + settlement.settlementLevel * 10) / 2 - 5 - 5 * settlement.settlementLevel;
                    int size = 10 + settlement.settlementLevel * 10;
                    CellFinder.TryFindRandomCellInsideWith(new CellRect(min, min, size, size),
                        testing => testing.Standable(Map) && Map.reachability.CanReachMapEdge(testing,
                            TraverseParms.For(TraverseMode.PassDoors)), out loc);
                    if (loc.x == -1000) {
                        Log.Message("Failed with " + friendly + ", " + loc);
                        CellFinder.TryFindRandomCellNear(new IntVec3(min + 10 + settlement.settlementLevel, 1,
                                min + 10 + settlement.settlementLevel), Map, 75,
                            testing => testing.Standable(Map), out loc);
                    }
                }

                GenSpawn.Spawn(friendly, loc, Map, new Rot4());
                if (friendly.drafter == null) {
                    friendly.drafter = new Pawn_DraftController(friendly);
                }


                Map.mapPawns.RegisterPawn(friendly);
                friendly.drafter.Drafted = true;
            }

            LordMaker.MakeNewLord(
                FactionColonies.getPlayerColonyFaction(), new LordJob_DefendColony(riders), Map, friendlies);

            defenders = friendlies;
        }

        private void endBattle(bool won, int remaining) {
            FactionFC faction = Find.World.GetComponent<FactionFC>();
            if (won) {
                faction.addExperienceToFactionLevel(5f);
                //if winner is player
                Find.LetterStack.ReceiveLetter("DefenseSuccessful".Translate(),
                    "DefenseSuccessfulFull".Translate(settlement.name, attackerForce.homeFaction),
                    LetterDefOf.PositiveEvent, new LookTargets(this));
            } else {
                //get multipliers
                double happinessLostMultiplier =
                    (TraitUtilsFC.cycleTraits(new double(), "happinessLostMultiplier",
                        settlement.traits, "multiply") * TraitUtilsFC.cycleTraits(new double(),
                        "happinessLostMultiplier", faction.traits, "multiply"));
                double loyaltyLostMultiplier =
                    (TraitUtilsFC.cycleTraits(new double(), "loyaltyLostMultiplier", settlement.traits,
                        "multiply") * TraitUtilsFC.cycleTraits(new double(), "loyaltyLostMultiplier",
                        faction.traits, "multiply"));

                int muliplier = 1;
                if (faction.hasPolicy(FCPolicyDefOf.feudal))
                    muliplier = 2;
                float prosperityMultiplier = 1;
                bool canDestroyBuildings = true;
                if (faction.hasTrait(FCPolicyDefOf.resilient)) {
                    prosperityMultiplier = .5f;
                    canDestroyBuildings = false;
                }

                //if winner are enemies
                settlement.prosperity -= 20 * prosperityMultiplier;
                settlement.happiness -= 25 * happinessLostMultiplier;
                settlement.loyalty -= 15 * loyaltyLostMultiplier * muliplier;

                string str = "DefenseFailureFull".Translate(settlement.name, attackerForce.homeFaction);


                for (int k = 0; k < 4; k++) {
                    int num = new IntRange(0, 10).RandomInRange;
                    if (num < 7 || settlement.buildings[k].defName == "Empty" ||
                        settlement.buildings[k].defName == "Construction" || !canDestroyBuildings) continue;
                    str += "\n" +
                           "BulidingDestroyedInRaid".Translate(settlement.buildings[k].label);
                    settlement.deconstructBuilding(k);
                }

                //level remover checker
                if (settlement.settlementLevel > 1 && canDestroyBuildings) {
                    int num = new IntRange(0, 10).RandomInRange;
                    if (num >= 7) {
                        str += "\n\n" + "SettlementDeleveledRaid".Translate();
                        settlement.delevelSettlement();
                    }
                }

                Find.LetterStack.ReceiveLetter("DefenseFailure".Translate(), str, LetterDefOf.Death,
                    new LookTargets(this));
            }

            if (defenderForce.homeSettlement != settlement) {
                //if not the home settlement defending
                if (remaining >= 7) {
                    Find.LetterStack.ReceiveLetter("OverwhelmingVictory".Translate(),
                        "OverwhelmingVictoryDesc".Translate(), LetterDefOf.PositiveEvent);
                    defenderForce.homeSettlement.returnMilitary(true);
                } else {
                    defenderForce.homeSettlement.cooldownMilitary();
                }
            }

            settlement.isUnderAttack = false;
        }

        private void endAttack() {
            endBattle(defenders.Any(), defenders.Count);
            deleteMap();

            supporting.Clear();
            defenders.Clear();
            defenderForce = null;
            attackers.Clear();
            attackerForce = null;
        }

        public void removeAttacker(Pawn downed) {
            attackers.Remove(downed);
            if (attackers.Any()) return;
            LongEventHandler.QueueLongEvent(endAttack,
                "EndingAttack", false, error => {
                    DelayedErrorWindowRequest.Add("ErrorEndingAttack".Translate(),
                        "ErrorEndingAttackDescription".Translate());
                    Log.Error(error.Message);
                });
        }

        public void removeDefender(Pawn defender) {
            defenders.Remove(defender);
            if (defenders.Any()) return;
            LongEventHandler.QueueLongEvent(endAttack,
                "EndingAttack", false, error => {
                    DelayedErrorWindowRequest.Add("ErrorEndingAttack".Translate(),
                        "ErrorEndingAttackDescription".Translate());
                    Log.Error(error.Message);
                });
        }
    }

    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    class PawnGizmos {
        static void Postfix(ref Pawn __instance, ref IEnumerable<Gizmo> __result) {
            List<Gizmo> output = __result.ToList();
            if (__result == null || __instance?.Faction == null || !output.Any() ||
                !(__instance.Map.Parent is WorldSettlementFC)) {
                return;
            }

            Pawn found = __instance;
            WorldSettlementFC settlementFc = (WorldSettlementFC)__instance.Map.Parent;
            if (__instance.Faction.Equals(FactionColonies.getPlayerColonyFaction())) {
                Pawn_DraftController pawnDraftController = __instance.drafter;
                if (pawnDraftController == null) {
                    pawnDraftController = new Pawn_DraftController(__instance);
                    __instance.drafter = pawnDraftController;
                }

                Command_Toggle draftColonists = new Command_Toggle {
                    hotKey = KeyBindingDefOf.Command_ColonistDraft,
                    isActive = () => false,
                    toggleAction = () => {
                        if (pawnDraftController.pawn.Faction.Equals(Faction.OfPlayer)) return;
                        pawnDraftController.pawn.SetFaction(Faction.OfPlayer);
                        pawnDraftController.Drafted = true;
                    },
                    defaultDesc = "CommandToggleDraftDesc".Translate(),
                    icon = TexCommand.Draft,
                    turnOnSound = SoundDefOf.DraftOn,
                    groupKey = 81729172,
                    defaultLabel = "CommandDraftLabel".Translate()
                };
                if (pawnDraftController.pawn.Downed)
                    draftColonists.Disable("IsIncapped".Translate(
                        (NamedArgument)pawnDraftController.pawn.LabelShort,
                        (NamedArgument)pawnDraftController.pawn));
                draftColonists.tutorTag = "Draft";
                output.Add(draftColonists);
            } else if (__instance.Faction.Equals(Faction.OfPlayer) && __instance.Drafted &&
                       !settlementFc.supporting.Any(caravan => caravan.pawns.Any(pawn => pawn.Equals(found)))) {
                foreach (Command_Toggle action in output.Where(gizmo => gizmo is Command_Toggle)) {
                    if (action.hotKey != KeyBindingDefOf.Command_ColonistDraft) {
                        continue;
                    }

                    int index = output.IndexOf(action);
                    action.toggleAction = () => {
                        found.SetFaction(FactionColonies.getPlayerColonyFaction());
                        //settlementFc.worldSettlement.defenderLord.AddPawn(__instance);
                    };
                    output[index] = action;
                    break;
                }
            }

            __result = output;
        }
    }
}