using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Picking what a viking does next with nothing to fight, by the time of day, what the village is short of and
	/// what its fellows are already doing, and the tools it takes along
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Picks what the viking does next - lightening its load first when it carries more than it can, then putting
		/// away what it brought home from a quest, and otherwise by
		/// the time of day: resting or keeping watch at night, getting up in the morning, gathering at the fire at dusk, and by day working whenever there's work, or else talking, petting,
		/// gathering wood or foraging food for a chest with room for it, getting rested again by the fire before late in
		/// the day, hunting, strolling or standing about - in the rain leaving out strolling, petting and getting rested
		/// again, and standing about inside its house
		/// </summary>
		/// <returns>The job</returns>
		private Job ChooseJob()
		{
			// Carrying more than it can, it lightens its load before anything else, by day or night, so it doesn't gather
			// at the fire or go to bed held back by it
			if (body.IsEncumbered() && HeaviestSpare() != null)
			{
				return(new UnburdenJob(this));
			}
			if (IsNight() || IsDusk())
			{
				// The watch is picked before anyone heads off, so the one picked goes straight to keeping watch
				VillageWatch.EnsureWatch(village);
			}
			// Through a raid it only keeps watch, eats or rests by the fire while wounded, or keeps guard
			if (RaidReady())
			{
				return(RaidJob());
			}
			// An NPC viking woken by danger stays up inside until it has passed, rather than going back to bed
			if (Peaceful() && StaysUp())
			{
				return(StayUpJob());
			}
			// What an enemy it killed dropped, or the gear of one of its village it has just mourned, is picked up before
			// it takes up anything else
			if (PicksUpKillLoot())
			{
				Job loot = FindKillLoot();
				if (loot != null)
				{
					return(loot);
				}
			}
			// What it brought home from a quest is put away first, even before bed
			Job questLoot = FindQuestLootWork();
			if (questLoot != null)
			{
				return(questLoot);
			}
			if (IsNight())
			{
				// Food left cooking into the night is taken off once it's done, by the next viking up and about
				Job tend = FindTendWork(true, true);
				if (tend != null)
				{
					return(tend);
				}
				// A viking whose torches won't see it through the dark crafts one first - the watch, one to last its shift
				Job torch = FindNightTorchWork();
				if (torch != null)
				{
					return(torch);
				}
				if (IsWatch())
				{
					return(new WatchJob(this));
				}
				// One that isn't ready for bed yet stays by the fire a while
				return(Bedtime() ? (Job)new RestJob(this) : new GatherJob(this, false));
			}
			// Tonight's bedtime is picked again when the night comes
			bedAt = -1f;
			if (IsDusk())
			{
				// Food left on a station is taken off before the village gathers, so it doesn't burn in the night, a viking
				// whose torches won't see it through the dark crafts one, and one with a forsaken power another of the
				// village doesn't know teaches it to them
				Job tend = FindTendWork();
				return(tend ?? FindNightTorchWork() ?? FindTeachWork() ?? new GatherJob(this, false));
			}
			Job work = FindWork();
			if (work != null)
			{
				return(work);
			}
			// In the rain it doesn't stroll about or pet the boars - it stands about out of the rain instead. The Swamp's
			// own drizzle, which hardly lets up, doesn't keep it in
			bool raining = VikingWeather.KeepsIndoors(transform.position);
			VillageLife partner = FindPartner();
			Character boar = ((raining || OtherDoing(PettingKey)) ? null : FindBoar());
			Container store = ((Time.time >= noWoodUntil && !OtherDoing(StockingKey)) ? FindStockChest() : null);
			bool forage = (!OtherDoing(ForagingKey) && (FindHive() != null || FindForage() != null));
			// A PC viking leaves stocking the chests with wood and foraging to an NPC viking with nothing better to do
			if (store != null && LeavesToNPC("Stock"))
			{
				store = null;
			}
			if (forage && LeavesToNPC("Forage"))
			{
				forage = false;
			}
			// Any rain, the Swamp's drizzle too, keeps it from getting rested out by the fire, where it would only get wet
			bool refresh = (!VikingWeather.RainsOn(transform.position) && !body.Rest.IsRested() && EnvMan.instance.GetDayFraction() < RefreshDayEnd && Time.time >= restAgainAt && village.GatherFires().Count > 0);
			bool archer = false;
			bool mayHunt = (!Wounded() && !OtherDoing(HuntingKey));
			// An NPC viking never hunts wild prey, though it slaughters livestock with its knife
			bool solo = (mayHunt && !Peaceful() && MayHunt(out archer) && (FindPrey(archer, null, false) != null || (archer && FindBird(null) != null)));
			// From the Plains on, wild prey that isn't only for archers is hunted by a party (see HuntParty)
			bool party = (mayHunt && !Peaceful() && CanLeadParty(null));
			bool hunt = (solo || party);
			float talk = ((partner != null) ? TalkWeight : 0f);
			float pet = ((boar != null) ? PetWeight : 0f);
			float strong = (OutclassesVillage() ? StrongOutingBoost : 1f);
			float stock = ((store != null) ? StockWeight * strong : 0f);
			// While the chests hold too little food for the village to keep its foods up, getting more comes up far more often
			bool foodLow = FoodLow(village);
			float need = (foodLow ? FoodShortBoost : 1f);
			float gather = (forage ? ForageWeight * need * strong : 0f);
			float rest = (refresh ? RefreshWeight : 0f);
			float hunting = (hunt ? HuntWeight * need * strong : 0f);
			// Short of food with no wild prey about that drops any for the village's hunters, it slaughters one of the
			// village's livestock, which its pen brings back
			bool slaughter = (foodLow && mayHunt && !WildFoodNear() && FindPrey(HasBow(humanoid), null, true, false, true) != null && !LeavesToNPC("Slaughter"));
			float butcher = (slaughter ? HuntWeight * need : 0f);
			float stroll = (raining ? 0f : StrollWeight);
			float roll = Random.value * (talk + pet + stock + gather + rest + hunting + butcher + stroll + IdleWeight);
			if (roll < talk)
			{
				TalkSession session = new TalkSession(this, partner, Random.Range(TalkTimeMin, TalkTimeMax));
				partner.JoinTalk(session);
				return(new TalkJob(this, session));
			}
			roll -= talk;
			if (roll < pet)
			{
				return(new PetJob(this, boar));
			}
			roll -= pet;
			if (roll < stock)
			{
				return(new StockJob(this, store));
			}
			roll -= stock;
			if (roll < gather)
			{
				return(new ForageJob(this));
			}
			roll -= gather;
			if (roll < rest)
			{
				return(new GatherJob(this, true));
			}
			roll -= rest;
			if (roll < hunting)
			{
				Job led = (party ? LeadParty(null) : null);
				return(led ?? new HuntJob(this, archer, null, false));
			}
			roll -= hunting;
			if (roll < butcher)
			{
				return(new HuntJob(this, HasBow(humanoid), null, true));
			}
			roll -= butcher;
			if (raining)
			{
				return(new IdleJob(this, Random.Range(IdleTimeMin, IdleTimeMax), true));
			}
			// It doesn't stand about inside a house by day, where it'd be in the way of the chests and the door - it
			// strolls out instead
			if (roll < stroll || village.InHouse(transform.position, 0f))
			{
				return(new StrollJob(this));
			}
			return(new IdleJob(this, Random.Range(IdleTimeMin, IdleTimeMax)));
		}

		/// <summary>
		/// What a viking does as day work to get food for a village short of it (see PickFoodWork)
		/// </summary>
		internal enum FoodWork
		{
			None,
			Hunt,
			Slaughter
		}

		/// <summary>
		/// Picks what a viking does to get food for its village - nothing when the chests hold food enough or it may not
		/// hunt, hunting wild prey near the village that drops food when it can go after some, or else slaughtering one
		/// of the village's livestock, only when no wild prey that drops food is about for any of the village's hunters
		/// </summary>
		/// <param name="foodLow">Whether the chests hold too little food for the vikings to keep their foods up</param>
		/// <param name="mayHunt">Whether the viking may hunt now - not wounded, with few enough of the village
		/// hunting</param>
		/// <param name="preyNear">Whether there's wild prey near that drops food that the viking itself hunts</param>
		/// <param name="wildFoodNear">Whether there's wild prey near that drops food for any of the village's
		/// hunters</param>
		/// <param name="livestock">Whether the village has livestock that drops food to slaughter</param>
		/// <returns>What it does</returns>
		internal static FoodWork PickFoodWork(bool foodLow, bool mayHunt, bool preyNear, bool wildFoodNear, bool livestock)
		{
			if (!foodLow || !mayHunt)
			{
				return(FoodWork.None);
			}
			if (preyNear)
			{
				return(FoodWork.Hunt);
			}
			return((!wildFoodNear && livestock) ? FoodWork.Slaughter : FoodWork.None);
		}

		/// <summary>
		/// Finds work getting food while the village's chests hold too little for its vikings to keep their foods up (see
		/// FoodLow), ahead of the village's upkeep - hunting wild prey near it that drops food, or slaughtering one of its
		/// livestock (see PickFoodWork). With neither, the viking goes on to the rest of its work, and getting food is
		/// left to the rolled jobs, which favor foraging and hunting while food is low
		/// </summary>
		/// <returns>The job, or null when there's food enough, the viking may not hunt, or there's nothing to hunt or
		/// slaughter</returns>
		private Job FindFoodWork()
		{
			bool foodLow = FoodLow(village);
			bool mayHunt = (foodLow && !Wounded() && !OtherDoing(HuntingKey));
			bool archer = false;
			// An NPC viking never hunts wild prey, so it only ever slaughters livestock for food
			bool preyNear = (mayHunt && !Peaceful() && MayHunt(out archer) && FindPrey(archer, null, false, false, true) != null);
			bool wildFood = (mayHunt && !preyNear && WildFoodNear());
			bool livestock = (mayHunt && !preyNear && !wildFood && FindPrey(HasBow(humanoid), null, true, false, true) != null);
			FoodWork work = PickFoodWork(foodLow, mayHunt, preyNear, wildFood, livestock);
			if (work == FoodWork.Hunt)
			{
				return(new HuntJob(this, archer, null, false, null, true));
			}
			if (work == FoodWork.Slaughter && !LeavesToNPC("Slaughter"))
			{
				return(new HuntJob(this, HasBow(humanoid), null, true));
			}
			return(null);
		}

		/// <summary>
		/// Finds work for the viking, most needed first - leaving on a quest of its village it's on (see
		/// FindQuestWork), then when it's wounded, eating from a chest and then resting by the
		/// fire until it's rested, then eating to keep its foods up while there's food enough to go around (see
		/// FoodToSpare), then in freezing weather keeping warm by a lit fire, or inside its house when none is
		/// burning, until the weather clears, then putting away loot it already carries, then loot lying around the
		/// village to put in a chest, then cooking the raw food it carries or the chests hold at a cooking station, then a
		/// meal at the cauldron (see FindMealWork), then gear in the
		/// village's chests better than its own to take up, then for an archer arrows in a chest while it's missing a
		/// batch or better ones than its best, then while the village is short of food prey to hunt or livestock to
		/// slaughter for it (see FindFoodWork), then a path for a house added for a player's bed (see FindPathWork), then
		/// the nearest damaged piece of the village to repair, then
		/// its own gear to repair at the
		/// workbench or forge when a piece of it has worn down to GearWornShare, then the village's bonfire or campfire to
		/// build back when it's been destroyed, which takes nothing, then a sitting log left by the village's old fire to
		/// take down for its wood, then the village's cart to take down an old one of, fetch back or build (see
		/// FindCartWork), then another chest to build when the chests run low on room (see FindChestWork), then a missing
		/// piece to rebuild, then the fire to
		/// stack when it isn't full and no other viking is on it, then arrows to craft at the workbench when it has a bow
		/// and is missing a batch, then a torch or a missing weapon to craft, then a weapon, armor or a shield better than
		/// its own to craft (see FindGearCraftWork), then a piece of its gear to upgrade, each when the village has
		/// everything it takes (see FindUpgradeWork), then the next step toward the village's goal - smelting or crafting
		/// what it takes (see FindGoalWork) - then tools for the chests (see FindToolStockWork), then with Zombie
		/// Apocalypse a weapon of its own to line with silver (see FindLiningWork) and silver arrows to restock (see
		/// FindSilverArrowWork), then beehives -
		/// leaving out work that needs wood for a while after it found none to gather, unless there's wood in the
		/// village's chests
		/// </summary>
		/// <returns>The job, or null when there's nothing to do</returns>
		private Job FindWork()
		{
			// Carrying more than it can holds it back from everything else, so it lightens its load first
			if (body.IsEncumbered() && HeaviestSpare() != null)
			{
				return(new UnburdenJob(this));
			}
			// A quest setting out takes its vikings before any other work
			Job quest = FindQuestWork();
			if (quest != null)
			{
				return(quest);
			}
			if (Wounded())
			{
				Container pantry = ((Time.time >= eatAgainAt) ? FindFoodChest(false) : null);
				if (pantry != null)
				{
					return(new EatJob(this, pantry, false));
				}
				if (!body.Rest.IsRested() && Time.time >= restAgainAt && village.GatherFires().Count > 0)
				{
					return(new GatherJob(this, true));
				}
			}
			// With food enough in the chests to go around, it keeps its foods up for the health they add, eating each again
			// once it has nearly run out, on the weakest food that does it good - otherwise the food is left for the wounded
			if (Time.time >= eatAgainAt && FoodToSpare())
			{
				Container pantry = FindFoodChest(true);
				if (pantry != null)
				{
					return(new EatJob(this, pantry, true));
				}
			}
			// In freezing weather it keeps out of the cold, rather than working out in it until it freezes
			if (VikingWeather.FreezingFor(humanoid))
			{
				return(new GatherJob(this, false, true));
			}
			// Loot comes ahead of cooking, so it doesn't lie about while the cooks are busy - done food on a station is
			// still taken off before it burns, by a viking at loot work too (see Idle), and raw meat picked up as loot goes
			// straight to a cooking station
			// The village's chores are left to an NPC viking with nothing better to do (see NPCFirst) - but loot a PC
			// viking already carries it puts away itself
			bool carrying = (CarriedLoot() != null);
			Job lootWork = FindLootWork(false);
			if (!carrying)
			{
				lootWork = NPCFirst(lootWork, "Loot");
			}
			if (lootWork != null)
			{
				return(lootWork);
			}
			Job cook = NPCFirst(FindCookWork(), "Cook");
			if (cook != null)
			{
				return(cook);
			}
			// Then a meal at the cauldron, from what the chests hold
			Job meal = NPCFirst(FindMealWork(), "Meal");
			if (meal != null)
			{
				return(meal);
			}
			Container armory = (carrying ? null : FindUpgradeChest());
			if (armory != null)
			{
				return(new UpgradeJob(this, armory));
			}
			// An NPC viking has no bow to fill a quiver for
			Container quiver = ((carrying || Peaceful()) ? null : FindArrowChest());
			if (quiver != null)
			{
				return(new AmmoJob(this, quiver));
			}
			// Short of food, getting more comes ahead of the village's upkeep, so a village always being attacked and
			// repaired still gets meat
			Job food = FindFoodWork();
			if (food != null)
			{
				return(food);
			}
			// A new house's path comes ahead of the village's upkeep - one viking at a time makes it, leaving the rest to
			// the upkeep - and waits out a thunderstorm, while light rain doesn't put it off
			if (!VikingWeather.Storming())
			{
				Job paths = NPCFirst(FindPathWork(), "Paths");
				if (paths != null)
				{
					return(paths);
				}
			}
			List<WearNTear> damaged = VillageRecords.Damaged(village);
			if (damaged.Count > 0 && LeavesToNPC("Repair"))
			{
				damaged.Clear();
			}
			damaged.Sort((WearNTear a, WearNTear b) => Vector3.Distance(a.transform.position, transform.position).CompareTo(Vector3.Distance(b.transform.position, transform.position)));
			foreach (WearNTear piece in damaged)
			{
				ZNetView view = piece.GetComponent<ZNetView>();
				if (view != null && view.IsValid() && !HammerAvoided(view.GetZDO().m_uid) && ClaimWork(view.GetZDO().m_uid))
				{
					return(new RepairJob(this, piece, view.GetZDO().m_uid));
				}
			}
			// In the Deep North, heavy snow on a piece wears it down, so it's cleared off once it builds up
			bool snowLeft = false;
			foreach (WearNTear piece in VillageSnow.Snowy(village, transform.position))
			{
				if (!snowLeft && LeavesToNPC("Snow"))
				{
					break;
				}
				snowLeft = true;
				ZNetView view = piece.m_nview;
				if (!Warded(piece.transform.position) && !HammerAvoided(view.GetZDO().m_uid) && ClaimWork(view.GetZDO().m_uid))
				{
					return(new SnowJob(this, piece, view.GetZDO().m_uid));
				}
			}
			// Then the deep snow on the village's ground, which slows whoever walks through it
			if (VillageSnow.ClaimDeepSpot(village, transform.position, out Vector3 deep))
			{
				if (!Warded(deep) && !LeavesToNPC("Snow"))
				{
					return(new SnowGroundJob(this, deep));
				}
				VillageSnow.ReleaseDeepSpot(village, deep, false);
			}
			// An NPC viking leaves every job that arms the village to the PC vikings
			bool peaceful = Peaceful();
			Job gear = FindGearWork();
			if (gear != null)
			{
				return(gear);
			}
			// The village's fire is built back with nothing, so it doesn't wait on wood. A ward keeps the vikings from
			// building in it, the same as it keeps out players it doesn't permit
			if (village.FireMissing(out PieceRecord fireRecord) && !Warded(fireRecord.Position) && !LeavesToNPC("Fire") && ClaimWork(RecordKey(fireRecord)))
			{
				return(new RebuildJob(this, fireRecord, true));
			}
			// The sitting logs left by an old fire come down for their wood, which builds them around the new one
			foreach (Vector3 spot in VillageSeats.TakeDowns(village))
			{
				ZNetView log = (Warded(spot) ? null : VillageSeats.StandingLog(spot));
				if (log != null && !LeavesToNPC("Logs") && ClaimWork(log.GetZDO().m_uid))
				{
					return(new TakeDownJob(this, log));
				}
			}
			Job cart = NPCFirst(FindCartWork(), "Cart");
			if (cart != null)
			{
				return(cart);
			}
			Job chest = NPCFirst(FindChestWork(), "Chests");
			if (chest != null)
			{
				return(chest);
			}
			Job lumber = (peaceful ? null : FindLumberWork());
			if (lumber != null)
			{
				return(lumber);
			}
			if (Time.time >= noWoodUntil || CarriedWood() > 0 || FindWoodChest() != null)
			{
				Job wooden = NPCFirst(FindWoodWork(), "Wood");
				if (wooden != null)
				{
					return(wooden);
				}
			}
			Job arrows = (peaceful ? null : FindArrowWork());
			if (arrows != null)
			{
				return(arrows);
			}
			Job torch = FindTorchWork();
			if (torch != null)
			{
				return(torch);
			}
			Job sidearm = (peaceful ? null : FindSidearmWork());
			if (sidearm != null)
			{
				return(sidearm);
			}
			// Better gear is crafted before what it would replace is upgraded
			Job better = (peaceful ? null : FindGearCraftWork());
			if (better != null)
			{
				return(better);
			}
			Job upgrade = (peaceful ? null : FindUpgradeWork());
			if (upgrade != null)
			{
				return(upgrade);
			}
			// Then the next step toward the village's goal, and only after that the tools for its chests, so they never
			// take what the goal's gear needs
			Job goal = NPCFirst(FindGoalWork(), "Goal");
			if (goal != null)
			{
				return(goal);
			}
			Job tools = NPCFirst(FindToolStockWork(), "Tools");
			if (tools != null)
			{
				return(tools);
			}
			Job lining = (peaceful ? null : FindLiningWork());
			if (lining != null)
			{
				return(lining);
			}
			Job silverArrows = (peaceful ? null : FindSilverArrowWork());
			if (silverArrows != null)
			{
				return(silverArrows);
			}
			// Beehives wait out a thunderstorm, while light rain doesn't put them off
			if (VikingWeather.Storming())
			{
				return(null);
			}
			Job hive = NPCFirst(FindHiveWork(), "Hive");
			if (hive != null)
			{
				return(hive);
			}
			return(null);
		}

		/// <summary>
		/// Finds a tool of the viking's own in its inventory - not carried for a chest or held for show
		/// </summary>
		/// <param name="prefab">The tool's prefab name</param>
		/// <returns>The tool, or null when it has none</returns>
		private ItemDrop.ItemData OwnTool(string prefab)
		{
			return(humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == prefab && !VikingItems.IsCarried(item) && !VikingItems.IsProp(item)));
		}

		/// <summary>
		/// Takes a tool or weapon out of a chest the viking has reached as its own, to work or fight with
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="wanted">Which items it takes</param>
		/// <returns>The item, or null when the chest has none or the viking has no room for it</returns>
		private ItemDrop.ItemData TakeTool(Container chest, System.Predicate<ItemDrop.ItemData> wanted)
		{
			if (!OpenChest(chest))
			{
				return(null);
			}
			Inventory store = chest.GetInventory();
			ItemDrop.ItemData stored = store.GetAllItems().Find((ItemDrop.ItemData item) => !VikingItems.IsCarried(item) && wanted(item));
			Inventory inventory = humanoid.GetInventory();
			Vector2i slot = inventory.FindEmptySlot(false);
			if (stored == null || slot.x < 0 || WeightRoom(stored) < 1)
			{
				return(null);
			}
			ItemDrop.ItemData tool = stored.Clone();
			tool.m_stack = 1;
			tool.m_equipped = false;
			tool.m_gridPos = slot;
			inventory.m_inventory.Add(tool);
			inventory.Changed();
			store.RemoveItem(stored, 1);
			chest.m_closeEffects.Create(chest.transform.position, chest.transform.rotation);
			return(tool);
		}

		/// <summary>
		/// Puts a tool the viking worked with away - out of its hands, and carried for a chest, so it goes back in one
		/// </summary>
		/// <param name="prefab">The tool's prefab name</param>
		private void PutToolAway(string prefab)
		{
			ItemDrop.ItemData tool = OwnTool(prefab);
			if (tool == null)
			{
				return;
			}
			if (humanoid.IsItemEquiped(tool))
			{
				humanoid.UnequipItem(tool, false);
			}
			tool.m_customData[VikingItems.CarryKey] = CarryLoot;
			humanoid.GetInventory().Changed();
		}
	}
}
