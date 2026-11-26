using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Standing about for a while (see IdleJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Standing about for a while, looking around now and then - in the rain, inside its house, clear of the doors
		/// </summary>
		private class IdleJob : Job
		{
			private readonly float duration;
			private readonly bool shelter;
			private float until = -1f;
			private float nextLook;
			private Vector3 spot;
			private bool walking;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="duration">How long it stands, in seconds</param>
			/// <param name="shelter">True to stand inside its house, out of the rain</param>
			public IdleJob(VillageLife owner, float duration, bool shelter = false) : base(owner)
			{
				this.duration = duration;
				this.shelter = shelter;
			}

			/// <summary>
			/// Picks a spot on its house's floor clear of the doors (see FloorSpot) to stand out of the rain, unless it
			/// already stands inside its house clear of them
			/// </summary>
			public override void Begin()
			{
				if (!shelter || !life.Home(out VillagePaths.House house))
				{
					return;
				}
				Vector3 position = life.transform.position;
				if (Village.InHouse(house, position, -1f) && !Village.Doors(house).Exists((Vector3 door) => Utils.DistanceXZ(door, position) < DoorClearance))
				{
					return;
				}
				spot = FloorSpot(house);
				walking = true;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name, and whether it's standing about inside</returns>
			public override string Describe()
			{
				return(shelter ? "Idle inside" + (walking ? ", on the way" : "") : "Idle");
			}

			/// <summary>
			/// Walks inside first when it stands about out of the rain, then stands still, turning to look somewhere else
			/// every few seconds
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has stood long enough</returns>
			public override bool Tick(float dt)
			{
				if (walking)
				{
					if (!life.Go(dt, spot, 0.75f, false))
					{
						return(false);
					}
					life.moveFailed = false;
					walking = false;
				}
				if (until < 0f)
				{
					until = Time.time + duration;
				}
				life.ai.StopMoving();
				if (Time.time >= nextLook)
				{
					nextLook = Time.time + Random.Range(3f, 6f);
					Vector2 look = Random.insideUnitCircle.normalized;
					life.ai.LookTowards(new Vector3(look.x, 0f, look.y));
				}
				return(Time.time >= until);
			}
		}
	}
}
