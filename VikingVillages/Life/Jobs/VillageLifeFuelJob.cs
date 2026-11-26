using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeping the village's fire stacked with wood (see FuelJob), and getting wood for a job that needs it (see WoodRun)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Checks whether a fire holds as much wood as it can
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>True when it's full</returns>
		private static bool IsFull(Fireplace fire)
		{
			return(FuelNeeded(fire) <= 0);
		}

		/// <summary>
		/// Works out how much wood a fire takes to fill
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>The wood, or 0 when it's full, burns forever or isn't loaded</returns>
		private static int FuelNeeded(Fireplace fire)
		{
			ZNetView view = fire.GetComponent<ZNetView>();
			if (view == null || !view.IsValid() || fire.m_infiniteFuel)
			{
				return(0);
			}
			return(Mathf.Max(0, Mathf.FloorToInt(fire.m_maxFuel - Mathf.CeilToInt(view.GetZDO().GetFloat(ZDOVars.s_fuel)))));
		}

		/// <summary>
		/// Getting wood for a job that needs it, step by step - from the nearest of the village's chests with wood in it
		/// when the job takes stored wood, or else the nearest branch or wood lying on the ground near the village -
		/// added to the wood the viking carries
		/// </summary>
		private class WoodRun
		{
			private readonly VillageLife life;
			private readonly int wanted;
			private bool fromChests;
			private Container chest;
			private ZNetView wood;
			private ZDOID woodId;
			private Vector3 stand;
			private float takeAt = -1f;

			/// <summary>
			/// Finds the nearest wood on the ground the viking can get within reach of, leaving alone what it can't for a
			/// while - or in a pen, which has no way in but over its fence, which the viking jumps
			/// </summary>
			/// <returns>The wood, or null when there's none it can reach</returns>
			private ZNetView FindReachable()
			{
				for (int i = 0; i < ReachTries; i++)
				{
					ZNetView found = life.FindWood();
					if (found == null)
					{
						return(null);
					}
					if (InPen(found.GetZDO().m_uid, found.transform.position))
					{
						stand = found.transform.position;
						return(found);
					}
					if (life.CanReach(found.transform.position, out stand))
					{
						return(found);
					}
					life.Avoid(found);
				}
				return(null);
			}

			/// <summary>
			/// Whether no wood could be found, which keeps the viking from work that needs it for a while
			/// </summary>
			public bool None;

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public bool Outing()
			{
				return(wood != null && !life.village.Contains(wood.transform.position));
			}

			/// <summary>
			/// Starts a run for a viking
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="needed">How much wood the job needs in all</param>
			/// <param name="chests">True to take stored wood from the village's chests first</param>
			public WoodRun(VillageLife owner, int needed, bool chests)
			{
				life = owner;
				wanted = needed;
				fromChests = chests;
			}

			/// <summary>
			/// Finds the nearest chest with wood or wood on the ground, walks to it and takes or gathers some
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has some, or None is set when there's none to get</returns>
			public bool Tick(float dt)
			{
				if (fromChests)
				{
					if (chest == null || !life.Usable(chest) || ChestWood(chest) <= 0)
					{
						chest = life.FindWoodChest();
						takeAt = -1f;
					}
					if (chest != null)
					{
						return(TakeFromChest(dt));
					}
					fromChests = false;
				}
				if (wood == null || !IsWood(wood))
				{
					Release();
					wood = (life.CanCarryWood() ? FindReachable() : null);
					if (wood == null)
					{
						GiveUp();
						return(false);
					}
					woodId = wood.GetZDO().m_uid;
					life.ClaimWork(woodId);
					life.UpdateOuting(Outing());
					life.woodInPen = InPen(woodId, wood.transform.position);
					if (life.woodInPen)
					{
						life.inPenForWood = true;
					}
					takeAt = -1f;
				}
				if (takeAt >= 0f)
				{
					if (Time.time < takeAt)
					{
						return(false);
					}
					int gathered = life.GatherWood(wood);
					Release();
					wood = null;
					return(gathered > 0);
				}
				// Out of the village with a monster in sight, it leaves the wood and heads home before it's seen
				if (Outing() && life.DangerNear(life.transform.position))
				{
					Release();
					wood = null;
					life.Flee(FleeReason.Forage);
					return(false);
				}
				if (Utils.DistanceXZ(wood.transform.position, life.transform.position) > PickReach && !life.Go(dt, stand, 0.5f, false))
				{
					return(false);
				}
				// Go counts it there within a meter of where it stands to reach
				if (life.moveFailed || Utils.DistanceXZ(wood.transform.position, life.transform.position) > PickReach + 1f)
				{
					// Stuck on the way, or it can't get near enough after all - it leaves this wood and tries another
					life.moveFailed = false;
					life.Avoid(wood);
					Release();
					wood = null;
					return(false);
				}
				life.Face(wood.transform.position);
				VikingPoses.Interact(life.humanoid);
				RequestItem(wood);
				takeAt = Time.time + ReachTime;
				return(false);
			}

			/// <summary>
			/// Walks to the chest, opens it and takes as much wood as the job still needs - leaving a chest it can't get
			/// to alone and trying another
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has taken some</returns>
			private bool TakeFromChest(float dt)
			{
				if (takeAt >= 0f)
				{
					if (Time.time < takeAt)
					{
						return(false);
					}
					takeAt = -1f;
					int taken = life.TakeWood(chest, Mathf.Max(1, wanted - life.CarriedWood()));
					if (taken <= 0)
					{
						// Its inventory is full, or the chest was emptied or opened meanwhile
						if (!life.CanCarryWood())
						{
							fromChests = false;
						}
						chest = null;
						return(false);
					}
					return(true);
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				life.Face(chest.transform.position);
				VikingPoses.Interact(life.humanoid);
				life.ShowChestOpen(chest);
				takeAt = Time.time + ReachTime;
				return(false);
			}

			/// <summary>
			/// Notes that there's no wood to gather, so the viking leaves work that needs it for a while
			/// </summary>
			private void GiveUp()
			{
				None = true;
				life.noWoodUntil = Time.time + NoWoodWait;
			}

			/// <summary>
			/// Lets go of the wood it was after
			/// </summary>
			public void Release()
			{
				if (woodId != ZDOID.None)
				{
					life.ReleaseWork(woodId);
					woodId = ZDOID.None;
				}
				life.woodInPen = false;
			}
		}

		/// <summary>
		/// Keeping the village's fire stacked - getting wood when it carries none, from the village's chests or the
		/// ground, and adding it a piece at a time, until the fire is full
		/// </summary>
		private class FuelJob : Job
		{
			private readonly Fireplace fire;
			private WoodRun run;
			private float addAt = -1f;
			private int trips;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the fire already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="target">The fire</param>
			public FuelJob(VillageLife owner, Fireplace target) : base(owner)
			{
				fire = target;
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Gathers wood when it has none and adds it to the fire a piece at a time
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once the fire is full, there's no wood left to gather, or it has added enough</returns>
			public override bool Tick(float dt)
			{
				if (fire == null || IsFull(fire) || trips >= FuelTripsMax)
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.ClaimObject(fire.GetComponent<ZNetView>());
				}
				if (life.CarriedWood() <= 0)
				{
					if (run == null)
					{
						run = new WoodRun(life, Mathf.Min(FuelTripsMax - trips, FuelNeeded(fire)), true);
					}
					if (run.Tick(dt))
					{
						run = null;
					}
					return(run != null && run.None);
				}
				if (addAt >= 0f)
				{
					if (Time.time < addAt)
					{
						return(false);
					}
					ZNetView view = fire.GetComponent<ZNetView>();
					if (view != null && view.IsValid())
					{
						view.InvokeRPC("RPC_AddFuel");
						life.UseWood(1);
						TestLog.Count("Work done", "wood put on fires");
					}
					addAt = -1f;
					trips++;
					return(false);
				}
				// Fuel goes on from outside the flames and the fire's no-walk zone
				float reach = Mathf.Max(FireRing(fire, 0f) - 0.5f, FireSafety.StandOff(fire));
				if (Utils.DistanceXZ(fire.transform.position, life.transform.position) > reach)
				{
					life.Go(dt, fire.transform.position, reach, false);
					return(life.moveFailed);
				}
				life.Face(fire.transform.position);
				VikingPoses.Interact(life.humanoid);
				addAt = Time.time + 0.8f;
				return(false);
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
			/// Lets go of the fire and the wood it was after
			/// </summary>
			public override void End()
			{
				if (run != null)
				{
					run.Release();
				}
				if (fire != null)
				{
					life.ReleaseObject(fire.GetComponent<ZNetView>());
				}
			}
		}
	}
}
