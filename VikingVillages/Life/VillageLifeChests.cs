using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Building more chests when the village's own chests run low on room - looked at for a while after its vikings
	/// come home from a quest, travelers arrive or a cart is unloaded, which is when a lot comes in at once (see
	/// NoteChestCheck)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// On a farm's generator, until when in ZNet time ticks its vikings look at whether its chests need more room
		/// </summary>
		private static readonly int ChestCheckUntilKey = "kriona.VikingVillages.villageChestCheckUntil".GetStableHashCode();

		/// <summary>
		/// How long after a lot comes in the vikings look at whether the chests need more room, in seconds, how often a
		/// viking looks, the fewest free slots across the village's own chests before another is built, and the most
		/// chests a house holds - the two it's furnished with and two more
		/// </summary>
		private const float ChestCheckSeconds = 600f;
		private const float ChestCheckInterval = 15f;
		internal const int ChestFreeLow = 4;
		internal const int ChestsPerHouse = 4;

		/// <summary>
		/// When the viking next looks at whether the village needs another chest, in Time.time seconds
		/// </summary>
		private float nextChestCheck;

		/// <summary>
		/// Has a village's vikings look at whether its chests need more room for a while (see ChestCheckSeconds) - when
		/// a lot comes in at once
		/// </summary>
		/// <param name="village">The village</param>
		internal static void NoteChestCheck(Village village)
		{
			if (village != null && village.Zdo != null)
			{
				village.Zdo.Set(ChestCheckUntilKey, NetTime.Now() + NetTime.Ticks(ChestCheckSeconds));
			}
		}

		/// <summary>
		/// Checks whether a chest is one of the village's own - put up with the village or by its vikings, not by a player
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>True when it is</returns>
		private static bool OwnChest(Container chest)
		{
			return(chest != null && chest.m_nview != null && chest.m_nview.IsValid() && chest.m_nview.GetZDO().GetLong(ZDOVars.s_creator, 0L) == 0L);
		}

		/// <summary>
		/// Finds a chest for the viking to build, while the village is looking at its chests' room (see NoteChestCheck)
		/// and its own chests have fewer than ChestFreeLow free slots between them - one viking of the village at a time,
		/// beside a chest of a house of its core with fewer than ChestsPerHouse, or else in one with none (see
		/// VillageFurnishing.ChestSpotNear), when there's wood to get
		/// </summary>
		/// <returns>The job, or null when the chests have room enough or there's nowhere for one</returns>
		private Job FindChestWork()
		{
			if (Time.time < nextChestCheck || NetTime.Now() >= village.Zdo.GetLong(ChestCheckUntilKey, 0L))
			{
				return(null);
			}
			nextChestCheck = Time.time + ChestCheckInterval;
			List<Container> own = VillageChests(village).FindAll(OwnChest);
			int free = 0;
			foreach (Container chest in own)
			{
				free += chest.GetInventory().GetEmptySlots();
			}
			if (free >= ChestFreeLow)
			{
				return(null);
			}
			int wood = VillageRecords.WoodCost(VillageFurnishing.ChestPiece.GetStableHashCode());
			bool woodLeft = (Time.time >= noWoodUntil || CarriedWood() >= wood || FindWoodChest() != null);
			if (!woodLeft || !ClaimWork(ChestBuildKey(village)))
			{
				return(null);
			}
			if (!PlanChest(own, out Vector3 spot, out Quaternion rotation))
			{
				ReleaseWork(ChestBuildKey(village));
				// With no room for one anywhere, it isn't looked at again until more comes in
				village.Zdo.Set(ChestCheckUntilKey, 0L);
				Plugin.Log.LogInfo("Chests: the village at " + village.Farm.ToString("F0") + " has " + free + " free slots in its chests, but no room in its houses for another");
				return(null);
			}
			return(new ChestBuildJob(this, spot, rotation, wood));
		}

		/// <summary>
		/// The claim on building a village's chest, so one viking builds one at a time
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The claim's key</returns>
		private static string ChestBuildKey(Village village)
		{
			return("chest " + village.Zdo.m_uid);
		}

		/// <summary>
		/// Picks where another chest goes - in a house of the village's core with fewer than ChestsPerHouse chests, beside
		/// one of its chests, houses holding a chest first and then the nearest the viking
		/// </summary>
		/// <param name="own">The village's own chests</param>
		/// <param name="spot">Set to where the chest stands</param>
		/// <param name="rotation">Set to which way it faces</param>
		/// <returns>False when there's no room anywhere</returns>
		private bool PlanChest(List<Container> own, out Vector3 spot, out Quaternion rotation)
		{
			spot = Vector3.zero;
			rotation = Quaternion.identity;
			List<KeyValuePair<VillagePaths.House, Container>> houses = new List<KeyValuePair<VillagePaths.House, Container>>();
			foreach (VillagePaths.House house in village.Houses)
			{
				if (!village.IsCore(house))
				{
					continue;
				}
				List<Container> inside = own.FindAll((Container chest) => Village.InHouse(house, chest.transform.position, 0.5f));
				if (inside.Count < ChestsPerHouse)
				{
					houses.Add(new KeyValuePair<VillagePaths.House, Container>(house, (inside.Count > 0) ? inside[0] : null));
				}
			}
			Vector3 at = transform.position;
			houses.Sort((KeyValuePair<VillagePaths.House, Container> a, KeyValuePair<VillagePaths.House, Container> b) =>
			{
				int held = (b.Value != null).CompareTo(a.Value != null);
				return((held != 0) ? held : Utils.DistanceXZ(a.Key.Center, at).CompareTo(Utils.DistanceXZ(b.Key.Center, at)));
			});
			foreach (KeyValuePair<VillagePaths.House, Container> entry in houses)
			{
				Vector3 near = ((entry.Value != null) ? entry.Value.transform.position : entry.Key.Center);
				if (VillageFurnishing.ChestSpotNear(entry.Key, near, out spot, out rotation))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Building a chest when the village's chests run low on room - gathering its wood like any piece's, then swinging
		/// a hammer where it goes and putting it up as one of the village's own pieces, which the vikings build back like
		/// the rest of the village
		/// </summary>
		private class ChestBuildJob : Job
		{
			private readonly Vector3 spot;
			private readonly Quaternion rotation;
			private readonly int wood;
			private readonly HammerWork work;
			private WoodRun run;

			/// <summary>
			/// Makes the job, with the building already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="at">Where the chest goes</param>
			/// <param name="facing">Which way it faces</param>
			/// <param name="cost">The wood it takes</param>
			public ChestBuildJob(VillageLife owner, Vector3 at, Quaternion facing, int cost) : base(owner)
			{
				spot = at;
				rotation = facing;
				wood = cost;
				work = new HammerWork(owner, RebuildSwings);
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name</returns>
			public override string Describe()
			{
				return("Building a chest");
			}

			/// <summary>
			/// Gathers the wood, then builds the chest
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's built, there's no wood left to get, it can't get there or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk())
				{
					return(true);
				}
				if (!life.HaveWood(wood, ref run, dt, out bool done))
				{
					return(done);
				}
				if (!work.Tick(dt, spot))
				{
					return(life.moveFailed);
				}
				GameObject prefab = ZNetScene.instance.GetPrefab(VillageFurnishing.ChestPiece);
				if (prefab == null || life.CarriedWood() < wood)
				{
					return(true);
				}
				life.UseWood(wood);
				GameObject made = Object.Instantiate(prefab, spot, rotation);
				VillageRecords.MarkBuilt(made);
				WearNTear wear = made.GetComponent<WearNTear>();
				if (wear != null)
				{
					wear.OnPlaced();
				}
				int hash = prefab.name.GetStableHashCode();
				VillageRecords.Add(life.village, new List<PieceRecord> { new PieceRecord { Prefab = hash, Position = spot, Rotation = rotation, Wood = wood } });
				TestLog.Count("Work done", "chests built");
				Plugin.Log.LogInfo("Chests: " + TestLog.Name(life.humanoid, "F0") + " built a chest at " + spot.ToString("F1") + " in the village at " + life.village.Farm.ToString("F0"));
				return(true);
			}

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(run != null && run.Outing());
			}

			/// <summary>
			/// Lets go of the building and the wood it was after
			/// </summary>
			public override void End()
			{
				if (run != null)
				{
					run.Release();
				}
				life.ReleaseWork(ChestBuildKey(life.village));
			}
		}
	}
}
