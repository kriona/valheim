using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Gathering around the village's fire at dusk (see GatherJob) - picking the fire and the viking's spot in its ring
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Picks the gathering fire the viking goes to - the one nearest its home with room for it, outside the pens, or
		/// else the one with the most room for its size (see FireRoom)
		/// </summary>
		/// <returns>The fire, or null when the village has none to gather at</returns>
		private Fireplace ChooseGatherFire()
		{
			List<Fireplace> fires = village.GatherFires().FindAll((Fireplace fire) => fire != null && !Village.FencedIn(fire.transform.position));
			if (fires.Count == 0)
			{
				return(null);
			}
			Vector3 from = (Home(out VillagePaths.House house) ? house.Center : transform.position);
			fires.Sort((Fireplace a, Fireplace b) => Utils.DistanceXZ(a.transform.position, from).CompareTo(Utils.DistanceXZ(b.transform.position, from)));
			Fireplace roomiest = null;
			float lightest = float.MaxValue;
			foreach (Fireplace fire in fires)
			{
				int gathered = Gatherers(fire);
				int room = FireRoom(fire);
				if (gathered < room)
				{
					return(fire);
				}
				float load = (float)gathered / room;
				if (load < lightest)
				{
					roomiest = fire;
					lightest = load;
				}
			}
			return(roomiest);
		}

		/// <summary>
		/// Picks a spot on a house's floor, on the navmesh at least a meter inside the walls, not on the roof and
		/// DoorClearance from its doors, so the viking isn't in the way of anyone coming in or out - or, when it finds
		/// none in twice RestFloorSamples tries, the first such spot it found however near a door, or else the middle of
		/// the house
		/// </summary>
		/// <param name="house">The house</param>
		/// <returns>The spot</returns>
		private static Vector3 FloorSpot(VillagePaths.House house)
		{
			List<Vector3> doors = Village.Doors(house);
			Vector3 nearDoor = house.Center;
			bool hasNearDoor = false;
			for (int i = 0; i < RestFloorSamples * 2; i++)
			{
				Vector2 offset = Random.insideUnitCircle * Mathf.Max(0.5f, house.HalfSize - 1.5f);
				Vector3 point = house.Center + new Vector3(offset.x, 0f, offset.y);
				Vector3 valid = point;
				if (Pathfinding.instance != null && (!NavmeshPoints.FindValidPoint(out valid, point, house.HalfSize, Pathfinding.AgentType.Humanoid) || !Village.InHouse(house, valid, -1f) || NavmeshRoofs.OnRoof(valid)))
				{
					continue;
				}
				if (!doors.Exists((Vector3 door) => Utils.DistanceXZ(door, valid) < DoorClearance))
				{
					return(valid);
				}
				if (!hasNearDoor)
				{
					nearDoor = valid;
					hasNearDoor = true;
				}
			}
			return(nearDoor);
		}

		/// <summary>
		/// Counts the other loaded vikings of the village gathered or gathering at a fire
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>How many</returns>
		private int Gatherers(Fireplace fire)
		{
			int count = 0;
			foreach (VillageLife other in All)
			{
				if (other != this && other != null && other.village == village && other.job is GatherJob gather && gather.Fire() == fire)
				{
					count++;
				}
			}
			return(count);
		}

		/// <summary>
		/// Works out how many vikings a fire has room for in the ring around it, GatherSpacing apart
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>How many, at least 1</returns>
		private static int FireRoom(Fireplace fire)
		{
			return(RingRoom(FireRing(fire, 0.5f)));
		}

		/// <summary>
		/// Works out how many vikings a circle around a fire has room for, GatherSpacing apart
		/// </summary>
		/// <param name="radius">The circle's radius, in meters</param>
		/// <returns>How many, at least 1</returns>
		internal static int RingRoom(float radius)
		{
			return(Mathf.Max(1, Mathf.FloorToInt(2f * Mathf.PI * radius / GatherSpacing)));
		}

		/// <summary>
		/// Widens the ring the vikings gather in around a fire when more gather than it has room for, keeping its inside
		/// and growing its area in step with how many more there are
		/// </summary>
		/// <param name="min">The ring's inside, in meters from the fire</param>
		/// <param name="max">The ring's outside</param>
		/// <param name="crowd">How many vikings gather there</param>
		/// <param name="room">How many the fire has room for (see FireRoom)</param>
		/// <returns>The ring's outside, unchanged while there's room for them all</returns>
		internal static float WidenRing(float min, float max, int crowd, int room)
		{
			if (crowd <= room)
			{
				return(max);
			}
			return(Mathf.Sqrt(min * min + (max * max - min * min) * crowd / room));
		}

		/// <summary>
		/// Narrows a ring around a fire to within its warmth, less WarmthMargin, and clear of its flames by
		/// WarmthMargin, keeping the ring at least a meter deep where the warmth leaves room and its inside never past
		/// its outside
		/// </summary>
		/// <param name="warmth">How far the fire's warmth reaches, in meters, or 0 when it has none</param>
		/// <param name="flames">How far its flames reach, in meters, or 0 when it has none</param>
		/// <param name="min">The ring's inside, in meters from the fire</param>
		/// <param name="max">The ring's outside</param>
		internal static void WarmRingWithin(float warmth, float flames, ref float min, ref float max)
		{
			if (warmth > 0f)
			{
				max = Mathf.Min(max, warmth - WarmthMargin);
			}
			min = Mathf.Min(min, max - 1f);
			min = Mathf.Max(min, flames + WarmthMargin);
			max = Mathf.Max(max, min);
		}

		/// <summary>
		/// Works out how far around to turn the next of several vikings spread out on either side of a line - none for
		/// the first, then a step one way, a step the other, two steps the first way, and so on
		/// </summary>
		/// <param name="i">Which of them it is, from 0</param>
		/// <param name="step">How far apart they stand, in degrees</param>
		/// <returns>The turn, in degrees</returns>
		internal static float Fan(int i, float step)
		{
			return(((i + 1) / 2) * step * ((i % 2 == 1) ? -1f : 1f));
		}

		/// <summary>
		/// Gathering around the village's fire at dusk, standing in a ring facing it, with some sitting down once the
		/// torches are out and facing whoever sits next to them - and a wounded viking sitting by it straight away, in
		/// its warmth, to rest, which by day a wounded viking or one getting rested again does until it's rested. By day
		/// in freezing weather, a viking sits in a lit fire's warmth the same way to keep from freezing until the weather
		/// clears, or stands inside its house when there's no fire burning. In the rain a wounded viking whose place by the
		/// fire isn't under a roof sits on its house's floor instead, dry, until it's no longer badly hurt or the rain
		/// stops, and in a thunderstorm the village gathers inside its houses. A traveler always sits, on a seat when
		/// there's one free, sitting down at once at night, and stays there the whole night, storm or not
		/// </summary>
		private class GatherJob : Job
		{
			private readonly bool healing;
			private readonly bool warming;

			/// <summary>
			/// How long a traveler sits by the fire by day, in seconds, or 0 when it isn't sitting there a while, and until
			/// when, by the game's time, once it's got there
			/// </summary>
			private readonly float lingerTime;
			private float lingerUntil = -1f;

			/// <summary>
			/// Whether the viking sits by the fire the whole night rather than going to bed - a traveler, which has no bed in
			/// the village
			/// </summary>
			private readonly bool allNight;
			private Fireplace fire;
			private System.Random rolls;
			private Vector3 spot;
			private float reach = 0.75f;
			private float ringMin;
			private float ringMax;
			private bool hasSpot;
			private bool towardFire;
			private int tries;
			private bool sits;
			private float sitAt;
			private bool sitting;
			private float satAt;
			private bool settled;
			private Vector3 settledAt;
			private float settledTime;
			private bool goingBack;

			/// <summary>
			/// Whether the viking went inside its house instead of to the fire (see GoInside), and when it next checks
			/// whether the rain has it go inside
			/// </summary>
			private bool indoors;
			private float nextRainCheck;

			/// <summary>
			/// The seat by the fire it sits on, the piece it's part of, whether it's on it yet, and when its claim on it is
			/// renewed
			/// </summary>
			private Chair seat;
			private ZNetView seatView;
			private bool seated;
			private float nextRenew;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="rest">True for resting by the fire by day until it's rested, sitting at once in its warmth</param>
			/// <param name="warm">True for keeping warm by day while the weather is freezing, sitting at once in a lit
			/// fire's warmth</param>
			/// <param name="linger">How long a traveler sits by the fire by day, sitting at once in its warmth, in seconds -
			/// or 0 for any other gathering</param>
			public GatherJob(VillageLife owner, bool rest, bool warm = false, float linger = 0f) : base(owner)
			{
				healing = rest;
				warming = warm;
				lingerTime = linger;
				allNight = (!rest && !warm && linger <= 0f && owner.IsTraveler());
			}

			/// <summary>
			/// Checks whether it's a traveler sitting by the fire a while by day
			/// </summary>
			/// <returns>True when it is</returns>
			private bool Lingering()
			{
				return(lingerTime > 0f);
			}

			/// <summary>
			/// Gets the fire the viking gathers at
			/// </summary>
			/// <returns>The fire, or null when it gathers at its doorstep</returns>
			public Fireplace Fire()
			{
				return(fire);
			}

			/// <summary>
			/// Checks whether this is the village's gathering at the end of the day, rather than resting or keeping warm
			/// </summary>
			/// <returns>True when it is</returns>
			public bool Evening()
			{
				return(!healing && !warming && !Lingering());
			}

			/// <summary>
			/// Picks the fire it gathers at (see ChooseGatherFire), whether and when the viking sits - a wounded or cold one
			/// at once - and its place in the ring, a free seat by the fire for one that sits when there is one, each viking
			/// keeping its own side of the fire and a wounded or cold one keeping within the fire's warmth, or its doorstep
			/// when there's no fire. The ring widens past the fire's room for as many as gather there, keeping as much
			/// ground for each. It goes inside its house instead (see GoInside) to keep warm when the fire isn't burning, to
			/// gather in a thunderstorm, and to rest in the rain when its place by the fire isn't under a roof, since a wet
			/// viking can't rest
			/// </summary>
			public override void Begin()
			{
				// The whole village doesn't crowd into a pen around a fire that stands in one, so it gathers at home
				fire = life.ChooseGatherFire();
				rolls = new System.Random(life.nview.GetZDO().m_uid.GetHashCode());
				if (warming && (fire == null || !fire.IsBurning() || Time.time < life.warmInsideUntil))
				{
					GoInside();
					return;
				}
				// A traveler has no house to go inside, so it sits out the storm by the fire
				if (!healing && !warming && !allNight && !Lingering() && VikingWeather.Storming() && VikingWeather.RainsOn(life.transform.position))
				{
					GoInside();
					return;
				}
				bool wounded = (healing || warming || Lingering() || life.Wounded());
				sits = (fire != null && (allNight || rolls.NextDouble() < GatherSitChance || wounded));
				sitAt = (wounded ? 0f : Mathf.Lerp(GatherSitStart, GatherSitEnd, (float)rolls.NextDouble()));
				if (fire != null)
				{
					ringMin = FireRing(fire, 0f);
					ringMax = FireRing(fire, 1f);
					ringMax = WidenRing(ringMin, ringMax, life.Gatherers(fire) + 1, FireRoom(fire));
					if (wounded)
					{
						WarmRing(fire, ref ringMin, ref ringMax);
					}
					if (sits && FindSeat())
					{
						spot = seat.m_attachPoint.position;
						reach = GatherSeatReach;
						hasSpot = true;
					}
					else
					{
						hasSpot = PickSpot();
					}
					if (healing && hasSpot && Exposed(spot + Vector3.up))
					{
						LeaveSeat();
						GoInside();
					}
					return;
				}
				if (life.Home(out VillagePaths.House house))
				{
					List<Vector3> steps = Village.Doorsteps(house);
					spot = ((steps.Count > 0) ? steps[0] : house.Center);
					hasSpot = true;
				}
			}

			/// <summary>
			/// Has the viking go inside its house instead of to the fire - to a spot on its floor clear of the doors (see
			/// FloorSpot), or nowhere when it has no house standing
			/// </summary>
			private void GoInside()
			{
				fire = null;
				indoors = true;
				reach = 0.75f;
				hasSpot = false;
				if (life.Home(out VillagePaths.House shelter))
				{
					spot = FloorSpot(shelter);
					hasSpot = true;
				}
			}

			/// <summary>
			/// Checks whether a spot is out in the rain - raining on it, and not under a roof
			/// </summary>
			/// <param name="point">The spot, at about a body's middle</param>
			/// <returns>True when it is</returns>
			private static bool Exposed(Vector3 point)
			{
				return(VikingWeather.RainsOn(point) && !VikingWeather.UnderRoof(point));
			}

			/// <summary>
			/// Picks the next place in the ring that's on the navmesh and outside the houses, or once GatherTries
			/// places have failed, the fire itself to get as near to as it can
			/// </summary>
			/// <returns>False when there's nowhere left to try</returns>
			private bool PickSpot()
			{
				while (tries < GatherTries)
				{
					tries++;
					float angle = (float)(rolls.NextDouble() * 360.0);
					float distance = Mathf.Lerp(ringMin, ringMax, (float)rolls.NextDouble());
					Vector3 point = fire.transform.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
					if (Pathfinding.instance == null)
					{
						spot = point;
						return(true);
					}
					if (!NavmeshPoints.FindValidPoint(out Vector3 valid, point, GatherSnap, Pathfinding.AgentType.Humanoid))
					{
						continue;
					}
					float fromFire = Utils.DistanceXZ(valid, fire.transform.position);
					if (fromFire < ringMin - 0.5f || fromFire > ringMax + 1f || life.village.InHouse(valid, 0.5f))
					{
						continue;
					}
					spot = valid;
					return(true);
				}
				if (towardFire)
				{
					return(false);
				}
				towardFire = true;
				spot = fire.transform.position;
				reach = ringMax;
				return(true);
			}

			/// <summary>
			/// Finds the free seat by the fire nearest the viking and claims it - a sitting log, bench or chair outside the
			/// houses, its seat clear of the flames and no farther from the fire than the ring, with no player on it
			/// </summary>
			/// <returns>False when there's none</returns>
			private bool FindSeat()
			{
				Vector3 middle = fire.transform.position;
				float flames = AreaReach(fire, EffectArea.Type.Burning);
				Chair best = null;
				ZNetView bestView = null;
				float bestDistance = float.MaxValue;
				foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
				{
					ZNetView view = instance.Value;
					if (view == null || !view.IsValid() || Utils.DistanceXZ(view.transform.position, middle) > ringMax + GatherSeatSearch)
					{
						continue;
					}
					Chair candidate = view.GetComponentInChildren<Chair>();
					if (candidate == null || candidate.m_attachPoint == null || candidate.m_inShip)
					{
						continue;
					}
					Vector3 point = candidate.m_attachPoint.position;
					float fromFire = Utils.DistanceXZ(point, middle);
					if (fromFire > ringMax + GatherSeatSlack || fromFire < flames || life.village.InHouse(point, 0.5f) || PlayerOn(candidate) || !life.IsFree(view))
					{
						continue;
					}
					float distance = Utils.DistanceXZ(point, life.transform.position);
					if (distance < bestDistance)
					{
						best = candidate;
						bestView = view;
						bestDistance = distance;
					}
				}
				if (best == null || !life.ClaimObject(bestView))
				{
					return(false);
				}
				seat = best;
				seatView = bestView;
				nextRenew = Time.time + ClaimRenew;
				return(true);
			}

			/// <summary>
			/// Checks whether a player sits on a seat
			/// </summary>
			/// <param name="chair">The seat</param>
			/// <returns>True when one does</returns>
			private static bool PlayerOn(Chair chair)
			{
				return(Player.IsPlayerInRange(chair.m_attachPoint.position, GatherSeatTaken));
			}

			/// <summary>
			/// Lets go of its seat, getting off it first when it's on it
			/// </summary>
			private void LeaveSeat()
			{
				if (seated)
				{
					VikingPoses.SetChair(life.humanoid, false);
					life.Detach();
					seated = false;
					sitting = false;
				}
				if (seatView != null)
				{
					life.ReleaseObject(seatView);
				}
				seat = null;
				seatView = null;
			}

			/// <summary>
			/// Narrows the ring to within the fire's warmth, where a viking rests, and clear of its flames
			/// </summary>
			/// <param name="fire">The fire</param>
			/// <param name="min">The ring's inside, in meters from the fire</param>
			/// <param name="max">The ring's outside</param>
			public static void WarmRing(Fireplace fire, ref float min, ref float max)
			{
				WarmRingWithin(AreaReach(fire, EffectArea.Type.Heat), AreaReach(fire, EffectArea.Type.Burning), ref min, ref max);
			}

			/// <summary>
			/// Checks whether the viking sits by the fire
			/// </summary>
			/// <returns>True while it does</returns>
			public override bool Sitting()
			{
				return(sitting);
			}

			/// <summary>
			/// Checks whether the viking rests by the fire by day, rather than gathering at dusk
			/// </summary>
			/// <returns>True when it does</returns>
			public bool Heals()
			{
				return(healing);
			}

			/// <summary>
			/// Checks whether the viking keeps warm in freezing weather by day, rather than gathering at dusk
			/// </summary>
			/// <returns>True when it does</returns>
			public bool Warms()
			{
				return(warming);
			}

			/// <summary>
			/// Describes the job for the debug hover text - gathering at the fire at dusk, resting by it by day, or keeping
			/// warm by it or inside in freezing weather
			/// </summary>
			/// <returns>What it's doing, and whether it's sitting yet</returns>
			public override string Describe()
			{
				string name = (warming ? "Warm up" : (healing ? "Rest" : (Lingering() ? "Sit" : "Gather"))) + (indoors ? " inside" : " by fire");
				return(name + (seated ? ", on a seat" : (sitting ? ", sitting" : (settled ? ", standing" : ", on the way"))));
			}

			/// <summary>
			/// Walks to its place and faces the fire, sitting down at its time if it's one that sits - on its seat, or
			/// else on the ground, turning to the nearest viking sitting next to it - until night falls, or by day until
			/// it's rested. It sits in the ring when it finds no way to its seat or a player takes it, tries another place
			/// when it finds no way to its own, stands where it got to when it finds none, and goes back to its place
			/// when it's pushed off it
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's the viking's bedtime (see Bedtime) or day - for a traveler, only once it's day - or for a wounded viking by day, once it's
			/// rested or it's dusk - or resting inside, once it's no longer badly hurt or the rain stops - and for a cold
			/// one, once the weather clears, it's dusk or its fire goes out. A traveler sitting by the fire a while by day is
			/// done once it's sat there that long, or it's dusk. Out in the rain where it would be better off inside (see
			/// GoInside), or a traveler sitting a while out in the rain, it's done too, so it's picked again</returns>
			public override bool Tick(float dt)
			{
				if (warming)
				{
					if (IsNight() || IsDusk() || !VikingWeather.FreezingFor(life.humanoid) || (fire != null && !fire.IsBurning()))
					{
						return(true);
					}
				}
				else if (Lingering())
				{
					if (IsNight() || IsDusk() || (lingerUntil >= 0f && Time.time >= lingerUntil))
					{
						return(true);
					}
				}
				else if (healing ? (IsNight() || IsDusk() || life.body.Rest.IsRested() || (indoors && (!life.Wounded() || !VikingWeather.RainsOn(life.transform.position)))) : (!IsDusk() && (!IsNight() || (!allNight && life.Bedtime()))))
				{
					return(true);
				}
				if (settled && !indoors && !warming && Time.time >= nextRainCheck)
				{
					nextRainCheck = Time.time + RainCheckInterval;
					if ((healing || Lingering() || VikingWeather.Storming()) && Exposed(life.humanoid.GetCenterPoint()))
					{
						return(true);
					}
				}
				if (!hasSpot)
				{
					life.ai.StopMoving();
					return(false);
				}
				if (!settled)
				{
					if (!life.Go(dt, spot, reach, false))
					{
						return(false);
					}
					if (life.moveFailed)
					{
						life.moveFailed = false;
						if (Utils.DistanceXZ(life.transform.position, spot) > reach + 1f)
						{
							if (seat != null)
							{
								// It found no way to its seat, so it sits in the ring instead
								LeaveSeat();
								reach = 0.75f;
							}
							if (!goingBack && fire != null && PickSpot())
							{
								return(false);
							}
							sits = false;
						}
					}
					settled = true;
					goingBack = false;
					settledAt = life.transform.position;
					settledTime = Time.time;
					if ((healing || Lingering()) && !sits && !indoors)
					{
						// It found no way to a seat by the fire
						life.restAgainAt = Time.time + RestRetryTime;
						return(true);
					}
					if (Lingering() && lingerUntil < 0f)
					{
						lingerUntil = Time.time + lingerTime;
					}
				}
				if (warming && fire != null && Time.time - settledTime > WarmFireTimeout && VikingWeather.IsFreezing(life.humanoid))
				{
					// It couldn't get into the fire's warmth, so it goes inside instead
					life.warmInsideUntil = Time.time + WarmInsideTime;
					return(true);
				}
				if (healing && sitting && !indoors && Time.time - satAt > RestRetryTime && !life.body.Rest.IsResting())
				{
					// Its seat isn't warm enough to rest at
					life.restAgainAt = Time.time + RestRetryTime;
					return(true);
				}
				// Resting inside, it sits down on the floor
				if (healing && indoors && !sitting)
				{
					VikingPoses.SetSitting(life.humanoid, true);
					sitting = true;
					satAt = Time.time;
				}
				life.ai.StopMoving();
				// Its seat was broken - a seat destroyed whole takes its view and attach point with it
				if ((seated && life.attachPoint == null) || (seatView != null && (seat == null || !seatView.IsValid())))
				{
					LeaveSeat();
				}
				if (seatView != null && Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					if (!life.ClaimObject(seatView))
					{
						LeaveSeat();
					}
				}
				if (!seated && Utils.DistanceXZ(life.transform.position, settledAt) > PushedDistance)
				{
					if (sitting)
					{
						VikingPoses.SetSitting(life.humanoid, false);
						sitting = false;
					}
					settled = false;
					goingBack = true;
					spot = settledAt;
					reach = 0.75f;
					return(false);
				}
				if (fire == null)
				{
					return(false);
				}
				if (sits && !sitting && (EnvMan.instance.GetDayFraction() >= sitAt || (allNight && IsNight())))
				{
					if (seat != null && PlayerOn(seat))
					{
						LeaveSeat();
					}
					if (seat != null)
					{
						life.Attach(seat.m_attachPoint, seatView.gameObject);
						VikingPoses.SetChair(life.humanoid, true);
						seated = true;
					}
					else
					{
						VikingPoses.SetSitting(life.humanoid, true);
					}
					sitting = true;
					satAt = Time.time;
				}
				// A seat faces the way it was built
				if (seated)
				{
					return(false);
				}
				VillageLife talker = (sitting ? life.SeatedTalker() : null);
				VillageLife neighbor = ((sitting && talker == null) ? SittingNeighbor() : null);
				life.Face((talker != null) ? talker.transform.position : ((neighbor != null) ? neighbor.transform.position : fire.transform.position));
				return(false);
			}

			/// <summary>
			/// Finds the nearest other viking sitting at the fire within GatherNeighbor of this one
			/// </summary>
			/// <returns>The viking's life, or null</returns>
			private VillageLife SittingNeighbor()
			{
				VillageLife nearest = null;
				float nearestDistance = GatherNeighbor;
				foreach (VillageLife other in All)
				{
					if (other == life || !(other.job is GatherJob gather) || !gather.sitting)
					{
						continue;
					}
					float distance = Utils.DistanceXZ(other.transform.position, life.transform.position);
					if (distance <= nearestDistance)
					{
						nearest = other;
						nearestDistance = distance;
					}
				}
				return(nearest);
			}

			/// <summary>
			/// Gets up, and lets go of its seat
			/// </summary>
			public override void End()
			{
				if (sitting && !seated)
				{
					VikingPoses.SetSitting(life.humanoid, false);
				}
				LeaveSeat();
			}
		}
	}
}
