using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The village's own steps toward its goal (see VillageLifeGoal) - smelting ore into bars, burning wood into coal and
	/// crafting what the goal's gear is made from, like bronze - and keeping the fuel up in a smelter that's still
	/// working through the ore it was given
	/// </summary>
	/// <remarks>
	/// A viking smelts the way a player does - taking the ore and fuel from the village's chests, walking to the smelter
	/// and putting the fuel in, then the ore, a piece at a time. The smelter drops its bars beside it as it works, and
	/// the vikings pick them up and put them away like any loot. One viking of the village smelts at a time
	/// </remarks>
	internal partial class VillageLife
	{
		/// <summary>
		/// Seconds between a viking's looks at the next step toward its village's goal, and how long the step worked out
		/// for a village is kept before it's worked out again
		/// </summary>
		private const float GoalWorkInterval = 15f;
		private const float GoalStepKeep = 15f;

		/// <summary>
		/// How near a smelter's switch the viking stands to put things in, in meters, and the seconds between each piece
		/// it puts in
		/// </summary>
		private const float SmelterReach = 2f;
		private const float FeedInterval = 0.6f;

		/// <summary>
		/// The wood a kiln is never given from the village's chests, so there's always some for the fire and repairs
		/// </summary>
		private const int KilnWoodReserve = 20;

		/// <summary>
		/// When the viking next looks at the next step toward its village's goal, in Time.time seconds
		/// </summary>
		private float nextGoalWork;

		/// <summary>
		/// The step toward each village's goal last worked out on this client, by the village's ZDO, with when it was
		/// worked out, in Time.time seconds
		/// </summary>
		private static readonly Dictionary<ZDOID, KeyValuePair<float, PlanStep>> goalSteps = new Dictionary<ZDOID, KeyValuePair<float, PlanStep>>();

		/// <summary>
		/// Gets the next step toward a village's goal, worked out at most every GoalStepKeep seconds
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The step, or null when it has no goal</returns>
		private static PlanStep CurrentGoalStep(Village village)
		{
			ZDOID id = village.Zdo.m_uid;
			if (goalSteps.TryGetValue(id, out KeyValuePair<float, PlanStep> kept) && Time.time - kept.Key < GoalStepKeep)
			{
				return(kept.Value);
			}
			PlanStep step = GoalStep(village);
			goalSteps[id] = new KeyValuePair<float, PlanStep>(Time.time, step);
			return(step);
		}

		/// <summary>
		/// Forgets the step worked out toward a village's goal, so it's worked out afresh once something has changed it
		/// </summary>
		/// <param name="village">The village</param>
		private static void ForgetGoalStep(Village village)
		{
			if (village != null && village.Zdo != null)
			{
				goalSteps.Remove(village.Zdo.m_uid);
			}
		}

		/// <summary>
		/// Finds work toward the village's goal - fuel for a smelter that has run short while it still has ore to work
		/// through, then the goal's next step when it's one the village does at home: smelting at a smelter or kiln, or
		/// crafting an item and putting it away
		/// </summary>
		/// <returns>The job, or null when there's nothing to do toward it here</returns>
		private Job FindGoalWork()
		{
			if (viking == null || Time.time < nextGoalWork)
			{
				return(null);
			}
			nextGoalWork = Time.time + GoalWorkInterval;
			Job fuel = FindSmelterFuelWork();
			if (fuel != null)
			{
				return(fuel);
			}
			PlanStep step = CurrentGoalStep(village);
			if (step == null || step.Source == null)
			{
				return(null);
			}
			if (step.Kind == StepKind.Smelt)
			{
				return(FindSmeltWork(step.Source, step.Amount));
			}
			if (step.Kind == StepKind.Craft && !OtherDoing(ArmingKey))
			{
				ItemDrop.ItemData item = ItemOf(step.Item);
				string name = ((item != null) ? Localization.instance.Localize(item.m_shared.m_name) : step.Item);
				return(CraftWeaponWork(step.Item, "Crafting " + name + " for the village", true));
			}
			return(null);
		}

		/// <summary>
		/// Works out what to put in a smelter - as much of the ore wanted as it has room for, and the fuel to smelt that
		/// and what it holds already, as much of it as it has room for
		/// </summary>
		/// <param name="wanted">How much ore is wanted smelted</param>
		/// <param name="queued">How much ore it holds already</param>
		/// <param name="maxOre">The most ore it holds</param>
		/// <param name="fuel">The fuel it holds</param>
		/// <param name="maxFuel">The most fuel it holds, 0 for one that burns none</param>
		/// <param name="fuelPer">The fuel each piece of ore takes</param>
		/// <param name="ore">Set to how much ore to put in</param>
		/// <param name="fuelAdd">Set to how much fuel to put in</param>
		internal static void SmeltLoad(int wanted, int queued, int maxOre, float fuel, int maxFuel, int fuelPer, out int ore, out int fuelAdd)
		{
			ore = Mathf.Clamp(wanted, 0, Mathf.Max(0, maxOre - queued));
			fuelAdd = 0;
			if (maxFuel <= 0 || fuelPer <= 0)
			{
				return;
			}
			int needed = Mathf.CeilToInt((queued + ore) * fuelPer - fuel - 0.001f);
			fuelAdd = Mathf.Clamp(needed, 0, Mathf.Max(0, Mathf.FloorToInt(maxFuel - fuel)));
		}

		/// <summary>
		/// Finds a smelter of the village's that still has ore to work through but too little fuel for it, when the
		/// village's chests hold the fuel - for the viking to top it up
		/// </summary>
		/// <returns>The job, or null when none needs fuel the village has</returns>
		private Job FindSmelterFuelWork()
		{
			if (OtherDoing(SmeltingKey))
			{
				return(null);
			}
			List<Container> chests = UsableChests();
			foreach (Smelter smelter in VillageSmelters(null))
			{
				int queued = smelter.GetQueueSize();
				if (queued <= 0 || smelter.m_fuelItem == null || smelter.m_maxFuel <= 0)
				{
					continue;
				}
				SmeltLoad(0, queued, smelter.m_maxOre, smelter.GetFuel(), smelter.m_maxFuel, smelter.m_fuelPerProduct, out int _, out int fuelAdd);
				string fuel = smelter.m_fuelItem.gameObject.name;
				fuelAdd = Mathf.Min(fuelAdd, CraftStock(fuel, chests));
				if (fuelAdd > 0 && ClaimObject(smelter.m_nview))
				{
					return(new SmeltJob(this, smelter, null, 0, fuel, fuelAdd));
				}
			}
			return(null);
		}

		/// <summary>
		/// Finds the smelting the village's goal needs - at the nearest of the village's smelters of the kind, as much of
		/// the ore as the chests hold and it has room for, with the fuel for it, keeping KilnWoodReserve wood back from a
		/// kiln
		/// </summary>
		/// <param name="source">The smelt</param>
		/// <param name="wanted">How much is wanted smelted</param>
		/// <returns>The job, or null when the village has no such smelter free to use, or nothing to put in it</returns>
		private Job FindSmeltWork(ResourceSource source, int wanted)
		{
			if (OtherDoing(SmeltingKey))
			{
				return(null);
			}
			List<Smelter> smelters = VillageSmelters(source.Station);
			smelters.Sort((Smelter a, Smelter b) => Vector3.Distance(a.transform.position, transform.position).CompareTo(Vector3.Distance(b.transform.position, transform.position)));
			List<Container> chests = UsableChests();
			foreach (Smelter smelter in smelters)
			{
				bool fueled = (smelter.m_fuelItem != null && smelter.m_maxFuel > 0);
				SmeltLoad(wanted, smelter.GetQueueSize(), smelter.m_maxOre, smelter.GetFuel(), (fueled ? smelter.m_maxFuel : 0), smelter.m_fuelPerProduct, out int ore, out int fuelAdd);
				int stock = CraftStock(source.From, chests);
				if (WoodItems.Contains(source.From))
				{
					stock = Mathf.Min(stock, VillageWood() - KilnWoodReserve);
				}
				ore = Mathf.Min(ore, stock);
				string fuel = (fueled ? smelter.m_fuelItem.gameObject.name : null);
				fuelAdd = ((fuel != null) ? Mathf.Min(fuelAdd, CraftStock(fuel, chests)) : 0);
				if (ore <= 0 || !ClaimObject(smelter.m_nview))
				{
					continue;
				}
				return(new SmeltJob(this, smelter, source.From, ore, fuel, fuelAdd));
			}
			return(null);
		}

		/// <summary>
		/// Lists the village's smelters a viking can use - of a kind, or of every kind - leaving out those that won't work
		/// now, for want of a roof or with their smoke blocked
		/// </summary>
		/// <param name="prefab">The smelter's prefab name, or null for every kind</param>
		/// <returns>The smelters</returns>
		private List<Smelter> VillageSmelters(string prefab)
		{
			List<Smelter> smelters = new List<Smelter>();
			int hash = ((prefab != null) ? prefab.GetStableHashCode() : 0);
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if ((prefab != null && instance.Key.GetPrefab() != hash) || instance.Value == null || !village.OwnsPiece(instance.Value.transform.position))
				{
					continue;
				}
				Smelter smelter = instance.Value.GetComponent<Smelter>();
				if (smelter == null || !smelter.m_nview.IsValid() || (smelter.m_requiresRoof && !smelter.m_haveRoof) || smelter.m_blockedSmoke || Warded(smelter.transform.position))
				{
					continue;
				}
				smelters.Add(smelter);
			}
			return(smelters);
		}

		/// <summary>
		/// Uses up one of an item the viking carries for crafting
		/// </summary>
		/// <param name="name">The item's prefab name</param>
		/// <returns>True when it carried one</returns>
		private bool UseOneCraft(string name)
		{
			Inventory inventory = humanoid.GetInventory();
			ItemDrop.ItemData item = inventory.m_inventory.Find((ItemDrop.ItemData carried) => IsCarriedFor(carried, CarryCraft) && carried.m_dropPrefab != null && carried.m_dropPrefab.name == name);
			if (item == null)
			{
				return(false);
			}
			inventory.RemoveItem(item, 1);
			return(true);
		}

		/// <summary>
		/// Smelting by day - taking the ore and fuel from the village's chests, walking to the smelter, which it has
		/// claimed, and putting the fuel in, then the ore, a piece at a time, the way a player does. It smelts with what
		/// it could get when the chests run out or it can carry no more, and what it doesn't put in goes back in a chest
		/// </summary>
		private class SmeltJob : Job
		{
			private readonly Smelter smelter;
			private readonly string ore;
			private readonly int oreCount;
			private readonly string fuel;
			private readonly int fuelCount;
			private Container chest;
			private bool fetching = true;
			private int oreAdded;
			private int fuelAdded;
			private float actAt = -1f;
			private float feedAt;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the smelter already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="target">The smelter</param>
			/// <param name="orePrefab">The ore it puts in, or null for fuel only</param>
			/// <param name="ores">How much ore</param>
			/// <param name="fuelPrefab">The fuel it puts in, or null for a smelter that burns none</param>
			/// <param name="fuels">How much fuel</param>
			public SmeltJob(VillageLife owner, Smelter target, string orePrefab, int ores, string fuelPrefab, int fuels) : base(owner)
			{
				smelter = target;
				ore = orePrefab;
				oreCount = ((orePrefab != null) ? ores : 0);
				fuel = fuelPrefab;
				fuelCount = ((fuelPrefab != null) ? fuels : 0);
			}

			/// <summary>
			/// Notes that the viking is smelting, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(SmeltingKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>What it smelts, and how much it has put in</returns>
			public override string Describe()
			{
				string what = ((ore != null) ? oreAdded + "/" + oreCount + " " + ore : fuelAdded + "/" + fuelCount + " " + fuel);
				return("Smelting " + what + " at " + Utils.GetPrefabName(smelter.gameObject));
			}

			/// <summary>
			/// Fetches the ore and fuel, then walks to the smelter and puts them in
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has put in all it carries, or can't get to the smelter, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk() || smelter == null || smelter.m_nview == null || !smelter.m_nview.IsValid())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.SetDoing(SmeltingKey, true);
					life.ClaimObject(smelter.m_nview);
				}
				if (fetching)
				{
					if (Short(out string name, out int needed))
					{
						return(Fetch(dt, name, needed));
					}
					fetching = false;
				}
				bool fuelLeft = (fuel != null && fuelAdded < fuelCount && life.CarriedCraft(fuel) > 0);
				bool oreLeft = (ore != null && oreAdded < oreCount && life.CarriedCraft(ore) > 0);
				if (!fuelLeft && !oreLeft)
				{
					return(true);
				}
				Vector3 spot = ((smelter.m_addOreSwitch != null) ? smelter.m_addOreSwitch.transform.position : smelter.transform.position);
				if (Utils.DistanceXZ(spot, life.transform.position) > SmelterReach)
				{
					life.Go(dt, spot, SmelterReach, false);
					return(life.moveFailed);
				}
				life.Face(spot);
				if (Time.time < feedAt)
				{
					return(false);
				}
				feedAt = Time.time + FeedInterval;
				VikingPoses.Interact(life.humanoid);
				// Fuel goes in first, so the ore starts smelting as soon as it's in
				if (fuelLeft && life.UseOneCraft(fuel))
				{
					smelter.m_nview.InvokeRPC("RPC_AddFuel");
					fuelAdded++;
					TestLog.Count("Work done", "fuel put in smelters");
				}
				else if (oreLeft && life.UseOneCraft(ore))
				{
					smelter.m_nview.InvokeRPC("RPC_AddOre", ore, false);
					oreAdded++;
					TestLog.Count("Work done", "ore put in smelters");
				}
				return(false);
			}

			/// <summary>
			/// Finds what the viking is still short of to put in - the fuel, then the ore
			/// </summary>
			/// <param name="name">Set to the item's prefab name</param>
			/// <param name="needed">Set to how many more it needs</param>
			/// <returns>True when it's short of one</returns>
			private bool Short(out string name, out int needed)
			{
				name = null;
				needed = 0;
				if (fuel != null && life.CarriedCraft(fuel) < fuelCount)
				{
					name = fuel;
					needed = fuelCount - life.CarriedCraft(fuel);
				}
				else if (ore != null && life.CarriedCraft(ore) < oreCount)
				{
					name = ore;
					needed = oreCount - life.CarriedCraft(ore);
				}
				return(name != null);
			}

			/// <summary>
			/// Walks to the nearest chest holding an item and takes what it needs of it - smelting with what it has once no
			/// chest holds any more, or it can carry no more
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="name">The item's prefab name</param>
			/// <param name="needed">How many more it needs</param>
			/// <returns>Always false, as the job goes on to the smelter afterwards</returns>
			private bool Fetch(float dt, string name, int needed)
			{
				if (chest == null || !life.Usable(chest))
				{
					chest = life.ChestWithCraft(name);
					actAt = -1f;
					if (chest == null)
					{
						fetching = false;
						return(false);
					}
				}
				if (Utils.DistanceXZ(chest.transform.position, life.transform.position) > ChestReach)
				{
					life.Go(dt, chest.transform.position, ChestReach, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(chest.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(chest);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				actAt = -1f;
				// Taking nothing means it can carry no more, so it smelts what it has
				if (life.TakeCraft(chest, name, needed) <= 0)
				{
					fetching = false;
				}
				chest = null;
				return(false);
			}

			/// <summary>
			/// Lets go of the smelter, carries what it didn't put in back to a chest, notes it's no longer smelting, and
			/// has the village's goal worked out afresh
			/// </summary>
			public override void End()
			{
				if (smelter != null && smelter.m_nview != null)
				{
					life.ReleaseObject(smelter.m_nview);
				}
				life.ReturnCraft();
				life.SetDoing(SmeltingKey, false);
				ForgetGoalStep(life.village);
			}
		}
	}
}
