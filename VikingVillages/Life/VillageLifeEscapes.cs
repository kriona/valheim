using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Getting a viking out of where it shouldn't be - out of a pen, off a roof, out of the water - and putting out a
	/// fire it's burning in
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Gets the viking out of a pen it finds itself in with no danger near and nothing to do there - not foraging
		/// or petting a boar - by walking for the village's rally point, which jumps the fence when there's no way out
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it's getting out, so the AI's own update is skipped</returns>
		private bool UpdateOutOfPen(float dt)
		{
			// A viking that went into a pen for wood has its reason to be there until it's back out
			if (inPenForWood && !woodInPen && Time.time >= nextPenCheck)
			{
				nextPenCheck = Time.time + PenCheckInterval;
				if (!Village.FencedIn(transform.position))
				{
					inPenForWood = false;
				}
			}
			if (viking == null || !viking.NoDangerNear() || GoesIntoPens() || attachPoint != null)
			{
				gettingOutOfPen = false;
				return(false);
			}
			if (!gettingOutOfPen)
			{
				if (Time.time < nextPenCheck)
				{
					return(false);
				}
				nextPenCheck = Time.time + PenCheckInterval;
				if (!Village.FencedIn(transform.position))
				{
					return(false);
				}
				LogEscape("out of a pen", village.RallyPoint());
				Interrupt("a pen");
				gettingOutOfPen = true;
				ResetStuck();
			}
			Go(dt, village.RallyPoint(), 1f, false);
			if (moveFailed || !Village.FencedIn(transform.position))
			{
				moveFailed = false;
				gettingOutOfPen = false;
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Gets the viking off a roof it finds itself on out of a fight, to the nearest ground within RoofExitSearch
		/// with a way to the village's open ground (see FreeSpot) - climbing straight down when that's right below it or
		/// beside it, or else stopping what it's doing and walking there, and climbing down to it when walking doesn't
		/// get it off. With no such ground, it gives up for a while
		/// </summary>
		/// <remarks>
		/// A roof that comes down to the ground, like a farmhouse's, can be walked up, but the ground a viking stands
		/// over up there - the house's floor under the roof, or the ground at its edge - often can't be walked down to
		/// </remarks>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it's getting off, so the AI's own update is skipped</returns>
		private bool UpdateOffRoof(float dt)
		{
			if (viking == null || !viking.AvoidsRoofs())
			{
				gettingOffRoof = false;
				return(false);
			}
			if (!gettingOffRoof)
			{
				if (Time.time < nextRoofCheck || attachPoint != null)
				{
					return(false);
				}
				nextRoofCheck = Time.time + RoofCheckInterval;
				if (!NavmeshRoofs.OnRoof(transform.position) || Pathfinding.instance == null)
				{
					return(false);
				}
				// Roofs are left out of the viking's navmesh here, so the spots found are off the roof
				if (!FreeSpot(transform.position, village.RallyPoint(), RoofExitSearch, out roofExit) || Utils.DistanceXZ(roofExit, transform.position) > RoofExitSearch)
				{
					nextRoofCheck = Time.time + RoofGiveUpTime;
					return(false);
				}
				if (ClimbsOffRoof(transform.position, roofExit))
				{
					ClimbOffRoof("stood right over it");
					return(false);
				}
				LogEscape("off " + RoofPiece(transform.position), roofExit);
				Interrupt("a roof");
				gettingOffRoof = true;
				ResetStuck();
			}
			if (!NavmeshRoofs.OnRoof(transform.position))
			{
				gettingOffRoof = false;
				return(false);
			}
			// Go is done when the viking is above the spot or gave up walking, either way still up on the roof
			if (Go(dt, roofExit, 0.5f, false))
			{
				gettingOffRoof = false;
				moveFailed = false;
				ClimbOffRoof("couldn't walk there");
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Climbs the viking down from the roof it's on to the ground found for it (see UpdateOffRoof)
		/// </summary>
		/// <param name="why">Why it climbs rather than walks, for the log</param>
		private void ClimbOffRoof(string why)
		{
			Vector3 from = transform.position;
			Vector3 down = roofExit + Vector3.up * 0.1f;
			transform.position = down;
			humanoid.m_body.position = down;
			humanoid.m_body.linearVelocity = Vector3.zero;
			ai.StopMoving();
			ResetStuck();
			TestLog.Count("Troubles", "climbed down off a roof");
			if (TestLog.On || Player.m_debugMode)
			{
				Plugin.Log.LogInfo("Climb: " + TestLog.Name(humanoid, "F1") + " was on a roof " + (from.y - roofExit.y).ToString("F1") + "m above the ground found for it and " + why + ", so it climbs down to " + down.ToString("F1") + ", " + Utils.DistanceXZ(from, down).ToString("F1") + "m away, at its job: " + ((job != null) ? job.Describe() : "none"));
			}
		}

		/// <summary>
		/// Checks whether a viking on a roof climbs straight down to the ground found for it rather than walking there -
		/// when the ground is within ArriveReach of it seen from above, which walking would take for there already, or
		/// well below it close by (see Stranded)
		/// </summary>
		/// <param name="at">Where it stands</param>
		/// <param name="exit">The ground found for it</param>
		/// <returns>True when it climbs</returns>
		internal static bool ClimbsOffRoof(Vector3 at, Vector3 exit)
		{
			float dx = at.x - exit.x;
			float dz = at.z - exit.z;
			return(dx * dx + dz * dz <= ArriveReach * ArriveReach || Stranded(at, exit));
		}

		/// <summary>
		/// Gets the viking out of the water when it stands about in it with no danger near - in the water at two checks
		/// WaterCheckInterval apart, having moved less than WaterStillDistance between them, so one wading across a
		/// stream on its way somewhere keeps going. It stops what it's doing and walks to the nearest dry ground, giving
		/// up for a while when it finds none or can't get there
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it's getting out, so the AI's own update is skipped</returns>
		private bool UpdateOutOfWater(float dt)
		{
			if (attachPoint != null)
			{
				gettingOutOfWater = false;
				return(false);
			}
			if (!gettingOutOfWater)
			{
				if (Time.time < nextWaterCheck)
				{
					return(false);
				}
				nextWaterCheck = Time.time + WaterCheckInterval;
				bool inWater = humanoid.InWater();
				bool still = (wasInWater && inWater && Utils.DistanceXZ(transform.position, waterCheckAt) < WaterStillDistance);
				wasInWater = inWater;
				waterCheckAt = transform.position;
				if (!still)
				{
					return(false);
				}
				wasInWater = false;
				if (!FindDryGround(out waterExit))
				{
					nextWaterCheck = Time.time + WaterGiveUpTime;
					return(false);
				}
				Interrupt("standing in water");
				gettingOutOfWater = true;
				ResetStuck();
			}
			if (Go(dt, waterExit, 0.5f, false))
			{
				gettingOutOfWater = false;
				if (moveFailed)
				{
					moveFailed = false;
					nextWaterCheck = Time.time + WaterGiveUpTime;
				}
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Runs the viking to the water to put itself out when it's on fire and there's water within DouseReach - even in
		/// a fight, which waits until it's out. In the water it's soaked, which puts the fire out
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it runs for the water, so the AI's own update is skipped</returns>
		private bool UpdateDousing(float dt)
		{
			if (attachPoint != null || !humanoid.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectBurning) || humanoid.InWater())
			{
				dousing = false;
				return(false);
			}
			if (!dousing)
			{
				if (Time.time < nextDouseCheck)
				{
					return(false);
				}
				nextDouseCheck = Time.time + DouseRetryTime;
				if (!FindWater(out douseSpot))
				{
					return(false);
				}
				Interrupt("on fire");
				dousing = true;
				ResetStuck();
			}
			if (Go(dt, douseSpot, 0.5f, true))
			{
				dousing = false;
				moveFailed = false;
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Finds the nearest water within DouseReach of the viking deep enough to soak it, that it can get to on the
		/// navmesh - never the boiling sea around the Ashlands (see DangerousGround)
		/// </summary>
		/// <param name="water">The spot found</param>
		/// <returns>False when there's none</returns>
		private bool FindWater(out Vector3 water)
		{
			water = Vector3.zero;
			if (Pathfinding.instance == null || ZoneSystem.instance == null)
			{
				return(false);
			}
			float deep = ZoneSystem.instance.m_waterLevel - DouseDepth;
			for (float distance = WaterExitStep; distance <= DouseReach; distance += WaterExitStep)
			{
				for (int i = 0; i < WaterExitDirections; i++)
				{
					Vector3 point = transform.position + Quaternion.Euler(0f, i * 360f / WaterExitDirections, 0f) * Vector3.forward * distance;
					if (ZoneSystem.instance.GetGroundHeight(point) > deep || DangerousGround.IsDangerous(point) || !NavmeshPoints.FindValidPoint(out Vector3 valid, point, WaterExitStep / 2f, Pathfinding.AgentType.Humanoid))
					{
						continue;
					}
					if (ZoneSystem.instance.GetGroundHeight(valid) <= deep)
					{
						water = valid;
						return(true);
					}
				}
			}
			return(false);
		}

		/// <summary>
		/// Finds the nearest dry ground within WaterExitSearch of the viking that it can walk to - on the navmesh, outside
		/// the pens and above the water level by DryHeight
		/// </summary>
		/// <param name="dry">The spot found</param>
		/// <returns>False when there's none</returns>
		private bool FindDryGround(out Vector3 dry)
		{
			dry = Vector3.zero;
			if (Pathfinding.instance == null || ZoneSystem.instance == null)
			{
				return(false);
			}
			float water = ZoneSystem.instance.m_waterLevel + DryHeight;
			for (float distance = WaterExitStep; distance <= WaterExitSearch; distance += WaterExitStep)
			{
				for (int i = 0; i < WaterExitDirections; i++)
				{
					Vector3 point = transform.position + Quaternion.Euler(0f, i * 360f / WaterExitDirections, 0f) * Vector3.forward * distance;
					if (ZoneSystem.instance.GetGroundHeight(point) < water || !NavmeshPoints.FindValidPoint(out Vector3 valid, point, WaterExitStep / 2f, Pathfinding.AgentType.Humanoid))
					{
						continue;
					}
					if (valid.y >= water && !Village.FencedIn(valid))
					{
						dry = valid;
						return(true);
					}
				}
			}
			return(false);
		}
	}
}
