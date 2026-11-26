using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeping guard around the fire through a raid (see GuardJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Keeping guard through a raid on the village - standing in a ring around the fire it gathers at (see
		/// ChooseGatherFire) with its weapon drawn (see IsWary), facing out and looking around: an archer in the ring the
		/// village gathers in at dusk (see FireRing), and one fighting hand to hand GuardRingGap farther out, in front of
		/// the archers - each viking on its own side of the fire, shifting a little along it every GuardTimeMin to
		/// GuardTimeMax, until the raid is over. With no fire to gather at, it keeps guard at an open spot in the core
		/// </summary>
		private class GuardJob : Job
		{
			private Vector3 post;
			private bool hasPost;
			private Vector3 middle;
			private bool aroundFire;
			private float until;
			private float nextLook;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			public GuardJob(VillageLife owner) : base(owner)
			{
			}

			/// <summary>
			/// Picks where it keeps guard - in its ring around the fire, or else an open spot in the core - and for how
			/// long
			/// </summary>
			public override void Begin()
			{
				until = Time.time + Random.Range(GuardTimeMin, GuardTimeMax);
				Fireplace fire = life.ChooseGatherFire();
				if (fire != null)
				{
					middle = fire.transform.position;
					float inner = FireRing(fire, 0f);
					float outer = FireRing(fire, 1f);
					if (life.viking == null || !life.viking.IsArcher())
					{
						float width = outer - inner;
						inner = outer + GuardRingGap;
						outer = inner + width;
					}
					aroundFire = RingPost(inner, outer);
					hasPost = aroundFire;
				}
				if (!hasPost)
				{
					hasPost = life.village.RandomOpenSpot(out post);
				}
			}

			/// <summary>
			/// Picks a spot in its ring around the fire, on its own side of it - the side its ZDO picks for it, so it keeps
			/// to the same one, shifted a little each time, or else turned farther around for a spot inside a house or a
			/// pen, or in the water
			/// </summary>
			/// <param name="inner">The inside of the ring, from the fire's middle, in meters</param>
			/// <param name="outer">The outside of it, in meters</param>
			/// <returns>False when none of GuardTries spots would do</returns>
			private bool RingPost(float inner, float outer)
			{
				float side = (life.nview.GetZDO().m_uid.GetHashCode() & 0xFFFF) / 65536f * 360f;
				for (int i = 0; i < GuardTries; i++)
				{
					float turn = Fan(i, GuardStep) + Random.Range(-GuardJitter, GuardJitter);
					Vector3 point = middle + Quaternion.Euler(0f, side + turn, 0f) * Vector3.forward * Random.Range(inner, outer);
					if (ZoneSystem.instance.GetGroundHeight(point, out float ground))
					{
						point.y = ground;
					}
					if (point.y < ZoneSystem.instance.m_waterLevel + GuardWaterClearance || life.village.InHouse(point, 0.5f) || Village.FencedIn(point))
					{
						continue;
					}
					post = point;
					return(true);
				}
				return(false);
			}

			/// <summary>
			/// Describes the job for the debug hover text - where it keeps guard, how far off it is and how long it stays
			/// </summary>
			/// <returns>The description</returns>
			public override string Describe()
			{
				string where = (!hasPost ? "with nowhere to stand" : (aroundFire ? "around the fire" : "in the core") + " at " + post.ToString("F0") + ", " + Utils.DistanceXZ(life.transform.position, post).ToString("F1") + "m off");
				return("Guard " + where + ", " + Mathf.Max(0f, until - Time.time).ToString("F0") + "s left" + (VillageRaids.On(life.village) ? "" : ", raid over"));
			}

			/// <summary>
			/// Walks to its spot, then stands there looking around every few seconds - out from the fire, in its ring
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has stood there long enough, it can't get there, or the raid is over</returns>
			public override bool Tick(float dt)
			{
				if (!VillageRaids.On(life.village) || Time.time >= until)
				{
					return(true);
				}
				if (hasPost && Utils.DistanceXZ(life.transform.position, post) > 1.5f)
				{
					life.Go(dt, post, 1f, false);
					return(life.moveFailed);
				}
				life.ai.StopMoving();
				if (Time.time >= nextLook)
				{
					nextLook = Time.time + Random.Range(2f, 4f);
					Vector3 outward = life.transform.position - middle;
					outward.y = 0f;
					Vector2 look = Random.insideUnitCircle.normalized;
					Vector3 around = new Vector3(look.x, 0f, look.y);
					life.ai.LookTowards((aroundFire && outward.sqrMagnitude > 0.01f) ? Quaternion.Euler(0f, Random.Range(-GuardLookSpread, GuardLookSpread), 0f) * outward.normalized : around);
				}
				return(false);
			}
		}
	}
}
