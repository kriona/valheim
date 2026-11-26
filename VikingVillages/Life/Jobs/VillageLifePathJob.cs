using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Making paths by day from a house added for a player's bed (see PathJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Describes a village's path for the debug hover text of its fire, while a house added to it for a player's bed
		/// has none yet - who's making it, or what it's waiting on (see FindPathWork)
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The line, ending in a line break, or "" when no house needs a path</returns>
		public static string DescribePaths(Village village)
		{
			List<VillagePaths.House> added = VillageBeds.AddedHouses(village);
			if (added.Count == 0 || !VillagePaths.NeedsPath(village, added))
			{
				return("");
			}
			VillageLife maker = null;
			VillageLife any = null;
			foreach (VillageLife life in All)
			{
				if (life == null || life.village != village)
				{
					continue;
				}
				if (life.job is PathJob)
				{
					maker = life;
				}
				if (life.active)
				{
					any = life;
				}
			}
			string why;
			if (maker != null)
			{
				string name = VikingNames.Get(maker.humanoid);
				why = ((name.Length > 0) ? name : "a viking") + " is making it - " + maker.job.Describe();
			}
			else if (any == null)
			{
				why = "for the client that runs the village's vikings";
			}
			else if (any.FindStation(WorkbenchHash) == null)
			{
				why = "waiting for a workbench";
			}
			else if (VikingWeather.Storming())
			{
				why = "waiting out the thunderstorm";
			}
			else if (any.OwnTool(HoePrefab) == null && any.ChestWithCraft(HoePrefab) == null && any.PlanCraft(HoePrefab, out string missing) == null)
			{
				why = "no hoe, and short of " + (missing ?? "what one takes") + " to craft one";
			}
			else
			{
				why = "waiting for a viking free of other work";
			}
			if (maker == null && pathProgress.TryGetValue(village.Zdo.m_uid, out PathProgress started))
			{
				why += " - " + started.Next + "/" + started.Plan.Spots.Count + " made so far";
			}
			return("Paths: a house added for a player's bed has none yet, " + why + "\n");
		}

		/// <summary>
		/// Finds a path for the viking to make with a hoe - from a house added to the village for a player's bed to the
		/// rest of the village, when no other viking is making one and the village has a workbench, with a hoe of its own
		/// or in a chest, or what one takes to craft. A path partway made is carried on from where it was left, without
		/// waiting to plan one (see pathProgress). When the village is short of something a hoe takes, like stone, it
		/// goes out for it instead (see GatherFor)
		/// </summary>
		/// <returns>The job, or null when there's no path to make or no way to get a hoe</returns>
		private Job FindPathWork()
		{
			if (OtherDoing(PathingKey) || FindStation(WorkbenchHash) == null)
			{
				return(null);
			}
			List<VillagePaths.House> added = VillageBeds.AddedHouses(village);
			ZDOID id = village.Zdo.m_uid;
			if (added.Count == 0 || !VillagePaths.NeedsPath(village, added))
			{
				pathProgress.Remove(id);
				return(null);
			}
			if (pathProgress.TryGetValue(id, out PathProgress started) && !VillagePaths.StillNeedsPath(village, added, started.Plan.House))
			{
				pathProgress.Remove(id);
				started = null;
			}
			if (started == null && nextPathPlan.TryGetValue(id, out float next) && Time.time < next)
			{
				return(null);
			}
			ArrowPlan craft = null;
			Container hoeChest = null;
			if (OwnTool(HoePrefab) == null)
			{
				hoeChest = ChestWithCraft(HoePrefab);
				if (hoeChest == null)
				{
					craft = PlanCraft(HoePrefab, out string missing);
					if (craft == null)
					{
						return(GatherFor(missing));
					}
					bool woodLeft = (Time.time >= noWoodUntil || CarriedWood() >= craft.Wood || FindWoodChest() != null);
					if (!woodLeft)
					{
						return(null);
					}
				}
			}
			if (started == null)
			{
				nextPathPlan[id] = Time.time + PathPlanInterval;
				VillagePaths.HousePath plan = VillagePaths.PlanHousePath(village, added);
				if (plan == null)
				{
					return(null);
				}
				if (plan.Spots.Count == 0)
				{
					VillagePaths.Pathed(village, plan.House);
					return(null);
				}
				started = new PathProgress();
				started.Plan = plan;
				pathProgress[id] = started;
			}
			if (craft != null && craft.Station != null && !ClaimObject(craft.Station.GetComponent<ZNetView>()))
			{
				return(null);
			}
			return(new PathJob(this, started, hoeChest, craft));
		}

		/// <summary>
		/// Making paths by day - from a house added to the village for a player's bed to the rest of the village (see
		/// VillagePaths.PlanHousePath). The viking gets a hoe - one of its own, one from a chest, or one it crafts at the
		/// workbench from what the village has, gathering the wood - then walks the path, swinging the hoe at each spot
		/// the way a player does, painting the hoe's path there and clearing the clutter lying on it, the hoe wearing down
		/// as a player's does. Where the path crosses water, it gathers the wood for a bridge when it reaches the bank and
		/// hammers it up (see VillagePaths.HouseBridge). A hoe that breaks is repaired at the workbench before it carries
		/// on. Once the path is made, it repairs the hoe at the workbench and puts it away in a chest
		/// </summary>
		private class PathJob : Job
		{
			private readonly PathProgress progress;
			private readonly VillagePaths.HousePath plan;
			private readonly ItemRun hoeRun;
			private CraftingStation repairBench;

			/// <summary>
			/// The wood it's getting for the path's next bridge, and its hammering at it
			/// </summary>
			private WoodRun bridgeRun;
			private HammerWork bridgeWork;
			private bool finishing;
			private float repairUntil = -1f;
			private float hitAt = -1f;
			private float nextSwing;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the station the hoe is crafted at already claimed when it's crafted
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="path">The path it makes, carried on from where it was left</param>
			/// <param name="fromChest">The chest it takes a hoe from, or null</param>
			/// <param name="hoe">What the hoe takes to craft when it crafts one, or null</param>
			public PathJob(VillageLife owner, PathProgress path, Container fromChest, ArrowPlan hoe) : base(owner)
			{
				progress = path;
				plan = path.Plan;
				hoeRun = new ItemRun(owner, fromChest, (ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == HoePrefab, HoePrefab, hoe);
			}

			/// <summary>
			/// Notes that the viking is making paths, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(PathingKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name, and how far along the path it is</returns>
			public override string Describe()
			{
				return("Making paths (" + progress.Next + "/" + plan.Spots.Count + ")");
			}

			/// <summary>
			/// Checks whether the path or the wood the viking is after has it out of the village
			/// </summary>
			/// <returns>True when it has</returns>
			public override bool Outing()
			{
				bool spotOutside = (progress.Next < plan.Spots.Count && !life.village.Contains(plan.Spots[progress.Next]));
				return(spotOutside || hoeRun.Outing() || (bridgeRun != null && bridgeRun.Outing()));
			}

			/// <summary>
			/// Gets a hoe, makes the path a spot at a time, repairing the hoe when it breaks, then repairs it at the end
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once the path is made and the hoe repaired, there's no hoe to be had, it's dusk, or a broken
			/// hoe can't be repaired</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.SetDoing(PathingKey, true);
					hoeRun.Renew();
					if (repairBench != null)
					{
						life.ClaimObject(repairBench.GetComponent<ZNetView>());
					}
				}
				ItemDrop.ItemData hoe = life.OwnTool(HoePrefab);
				if (hoe == null)
				{
					return(hoeRun.Tick(dt, out ItemDrop.ItemData _) && life.OwnTool(HoePrefab) == null);
				}
				bool broken = (hoe.m_shared.m_useDurability && hoe.m_durability <= 0f);
				if (finishing || broken)
				{
					bool worn = (hoe.m_shared.m_useDurability && hoe.m_durability < hoe.GetMaxDurability());
					if (worn && !Repair(dt, hoe, out bool gaveUp))
					{
						return(gaveUp);
					}
					return(finishing);
				}
				return(MakePath(dt, hoe));
			}

			/// <summary>
			/// Walks to the next spot of the path, swings the hoe at it and paints the path there, clearing its clutter
			/// and wearing the hoe down - leaving out a spot it can't get to
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="hoe">The hoe</param>
			/// <returns>False, so the job goes on</returns>
			private bool MakePath(float dt, ItemDrop.ItemData hoe)
			{
				if (progress.BridgeNext < plan.Bridges.Count && progress.Next >= plan.Bridges[progress.BridgeNext].Before)
				{
					BuildBridge(dt);
					return(false);
				}
				Vector3 spot = plan.Spots[progress.Next];
				if (hitAt >= 0f)
				{
					life.Face(spot);
					if (Time.time < hitAt)
					{
						return(false);
					}
					hitAt = -1f;
					VillagePaths.PaintSpot(spot, plan.Clutter[progress.Next]);
					if (hoe.m_shared.m_useDurability)
					{
						hoe.m_durability = Mathf.Max(0f, hoe.m_durability - hoe.m_shared.m_useDurabilityDrain * Game.m_durabilityRate);
					}
					Advance();
					return(false);
				}
				if (Utils.DistanceXZ(spot, life.transform.position) > WorkReach)
				{
					life.Go(dt, spot, WorkReach, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						Advance();
					}
					return(false);
				}
				life.Face(spot);
				if (!life.humanoid.IsItemEquiped(hoe))
				{
					life.humanoid.EquipItem(hoe, false);
				}
				if (Time.time < nextSwing)
				{
					return(false);
				}
				VikingPoses.Swing(life.humanoid, hoe);
				hitAt = Time.time + SwingHitTime;
				nextSwing = Time.time + SwingInterval;
				return(false);
			}

			/// <summary>
			/// Builds the path's next bridge once it reaches the bank it starts from - gathering the wood it takes, then
			/// walking to the bank and hammering it up - leaving it out when there's no wood left to get or it can't get
			/// to the bank
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			private void BuildBridge(float dt)
			{
				VillagePaths.HouseBridge bridge = plan.Bridges[progress.BridgeNext];
				int wood = Mathf.Min(bridge.Wood, CarryMax);
				if (!life.HaveWood(wood, ref bridgeRun, dt, out bool done))
				{
					if (done)
					{
						NextBridge();
					}
					return;
				}
				if (bridgeWork == null)
				{
					bridgeWork = new HammerWork(life, RebuildSwings);
				}
				if (!bridgeWork.Tick(dt, bridge.Start))
				{
					if (life.moveFailed)
					{
						life.moveFailed = false;
						NextBridge();
					}
					return;
				}
				if (life.CarriedWood() >= wood)
				{
					life.UseWood(wood);
					VillagePaths.BuildHouseBridge(life.village, bridge);
				}
				NextBridge();
			}

			/// <summary>
			/// Moves on to the path's next bridge
			/// </summary>
			private void NextBridge()
			{
				progress.BridgeNext++;
				bridgeWork = null;
				if (bridgeRun != null)
				{
					bridgeRun.Release();
					bridgeRun = null;
				}
			}

			/// <summary>
			/// Moves on to the path's next spot, noting the house as joined to the village once it's past the last, and
			/// the path as no longer partway made
			/// </summary>
			private void Advance()
			{
				progress.Next++;
				if (progress.Next >= plan.Spots.Count)
				{
					VillagePaths.Pathed(life.village, plan.House);
					pathProgress.Remove(life.village.Zdo.m_uid);
					finishing = true;
					ItemDrop.ItemData hoe = life.OwnTool(HoePrefab);
					if (hoe != null && life.humanoid.IsItemEquiped(hoe))
					{
						life.humanoid.UnequipItem(hoe, false);
					}
				}
			}

			/// <summary>
			/// Repairs the hoe at the village's workbench, working at it with the player's crafting animation
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="hoe">The hoe</param>
			/// <param name="gaveUp">Set to true when there's no workbench that can repair it, or it can't get there</param>
			/// <returns>True once it's repaired</returns>
			private bool Repair(float dt, ItemDrop.ItemData hoe, out bool gaveUp)
			{
				gaveUp = false;
				if (repairBench == null)
				{
					repairBench = life.FindStation(WorkbenchHash);
					if (repairBench == null || !CanRepairAt(hoe, repairBench) || !life.ClaimObject(repairBench.GetComponent<ZNetView>()))
					{
						repairBench = null;
						gaveUp = true;
						return(false);
					}
				}
				if (repairUntil >= 0f)
				{
					life.Face(repairBench.transform.position);
					if (Time.time < repairUntil)
					{
						return(false);
					}
					repairUntil = -1f;
					life.SetCrafting(0);
					float max = hoe.GetMaxDurability();
					life.RaiseSkill(Skills.SkillType.Crafting, ((max > 0f) ? 1f - hoe.m_durability / max : 0f));
					hoe.m_durability = max;
					VillageSignals.SendEffect(VillageSignals.GearRepairEffect, repairBench.GetComponent<ZNetView>().GetZDO().m_uid);
					life.ReleaseObject(repairBench.GetComponent<ZNetView>());
					repairBench = null;
					return(true);
				}
				if (Utils.DistanceXZ(repairBench.transform.position, life.transform.position) > CraftReach)
				{
					life.Go(dt, repairBench.transform.position, CraftReach, false);
					gaveUp = life.moveFailed;
					return(false);
				}
				life.Face(repairBench.transform.position);
				life.SetCrafting(Mathf.Max(1, repairBench.m_useAnimation));
				repairUntil = Time.time + GearRepairTime;
				return(false);
			}

			/// <summary>
			/// Stops working, lets go of the stations and the wood it was after, carries what it carries for crafting
			/// back to a chest, puts the hoe away in a chest and notes it's no longer making paths
			/// </summary>
			public override void End()
			{
				hoeRun.Release();
				if (bridgeRun != null)
				{
					bridgeRun.Release();
				}
				if (repairBench != null)
				{
					life.ReleaseObject(repairBench.GetComponent<ZNetView>());
				}
				life.PutToolAway(HoePrefab);
				life.SetDoing(PathingKey, false);
			}
		}
	}
}
