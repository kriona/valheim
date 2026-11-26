using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Working within reach of a piece - how close a viking stands to the piece or fire it works at - and the hammer
	/// swing it builds, repairs and takes down with (see HammerWork)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// How far from a fire the vikings gather, by how big it is - from the inside of the ring to the outside
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <param name="along">How far across the ring, from 0 to 1</param>
		/// <returns>The distance, in meters</returns>
		public static float FireRing(Fireplace fire, float along)
		{
			return(FireRingFor(Utils.GetPrefabName(fire.gameObject), along));
		}

		/// <summary>
		/// How far from a fire the vikings gather, by its prefab - farther around a bonfire than any other fire
		/// </summary>
		/// <param name="prefab">The fire's prefab name</param>
		/// <param name="along">How far across the ring, from 0 to 1</param>
		/// <returns>The distance, in meters</returns>
		internal static float FireRingFor(string prefab, float along)
		{
			bool bonfire = prefab.StartsWith("bonfire", System.StringComparison.OrdinalIgnoreCase);
			return(bonfire ? Mathf.Lerp(BonfireRingMin, BonfireRingMax, along) : Mathf.Lerp(CampfireRingMin, CampfireRingMax, along));
		}

		/// <summary>
		/// Measures how far a fire's effect areas of some types reach from it
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <param name="types">The types</param>
		/// <returns>The farthest reach, in meters, or 0 when it has none</returns>
		public static float AreaReach(Fireplace fire, EffectArea.Type types)
		{
			float reach = 0f;
			foreach (EffectArea area in fire.GetComponentsInChildren<EffectArea>(true))
			{
				if ((area.m_type & types) == 0)
				{
					continue;
				}
				Vector3 offset = area.transform.position - fire.transform.position;
				offset.y = 0f;
				Vector3 scale = area.transform.lossyScale;
				float size = 0f;
				// Measured from the collider's own size rather than its bounds, which are empty while the area is switched
				// off, as a fire's flames are while it's unlit
				SphereCollider sphere = area.GetComponent<SphereCollider>();
				CapsuleCollider capsule = area.GetComponent<CapsuleCollider>();
				BoxCollider box = area.GetComponent<BoxCollider>();
				if (sphere != null)
				{
					size = sphere.radius * Mathf.Max(scale.x, scale.z);
				}
				else if (capsule != null)
				{
					// A capsule standing up reaches its radius around, and one lying down half its height along it
					float across = capsule.radius * Mathf.Max(scale.x, scale.z);
					size = ((capsule.direction == 1) ? across : Mathf.Max(across, capsule.height / 2f * Mathf.Max(scale.x, scale.z)));
				}
				else if (box != null)
				{
					size = Mathf.Max(box.size.x * scale.x, box.size.z * scale.z) / 2f;
				}
				else
				{
					Collider collider = area.GetComponent<Collider>();
					if (collider != null)
					{
						size = Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
					}
				}
				reach = Mathf.Max(reach, offset.magnitude + size);
			}
			return(reach);
		}

		/// <summary>
		/// Works out how near the viking walks to the spot it stands at to work on a piece - near enough that it's within
		/// WorkReach of the piece once there, or right up to the spot when it's too far from the piece for that, as one
		/// beside a piece whose own ground can't be stood on can be
		/// </summary>
		/// <param name="offset">How far the spot is from the part of the piece it works on, seen from above, in meters</param>
		/// <returns>How near it walks to the spot, in meters</returns>
		internal static float StandReach(float offset)
		{
			return(StandReach(offset, WorkReach));
		}

		/// <summary>
		/// Works out how near the viking walks to the spot it stands at to reach something - near enough that it's within
		/// reach of it once there, or right up to the spot when it's too far from it for that
		/// </summary>
		/// <param name="offset">How far the spot is from what it reaches, seen from above, in meters</param>
		/// <param name="reach">How near it has to be to what it reaches, in meters</param>
		/// <returns>How near it walks to the spot, in meters</returns>
		internal static float StandReach(float offset, float reach)
		{
			return(Mathf.Max(ArriveReach, reach - offset));
		}

		/// <summary>
		/// Checks whether the viking is within reach of a piece to work on it - within WorkReach of it, or at the spot it
		/// walks to to reach it
		/// </summary>
		/// <param name="toPoint">How far it is from the part of the piece it works on, seen from above, in meters</param>
		/// <param name="toStand">How far it is from the spot it walks to, seen from above, in meters</param>
		/// <returns>True when it is</returns>
		internal static bool InWorkReach(float toPoint, float toStand)
		{
			return(InReach(toPoint, toStand, WorkReach));
		}

		/// <summary>
		/// Checks whether the viking is within reach of something - within reach of it, or at the spot it walks to to
		/// reach it
		/// </summary>
		/// <param name="toPoint">How far it is from what it reaches, seen from above, in meters</param>
		/// <param name="toStand">How far it is from the spot it walks to, seen from above, in meters</param>
		/// <param name="reach">How near it has to be to what it reaches, in meters</param>
		/// <returns>True when it is</returns>
		internal static bool InReach(float toPoint, float toStand, float reach)
		{
			return(toPoint <= reach || toStand <= ArriveReach);
		}

		/// <summary>
		/// Swinging a hammer at a piece of the village, standing within reach of it
		/// </summary>
		private class HammerWork
		{
			private readonly VillageLife life;
			private readonly int count;
			private int swings;
			private float nextSwing;

			/// <summary>
			/// Whether the viking has got within reach and started swinging
			/// </summary>
			public bool Started
			{
				get
				{
					return(swings > 0);
				}
			}

			/// <summary>
			/// Starts the work for a viking
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="swingCount">How many times it swings</param>
			public HammerWork(VillageLife owner, int swingCount)
			{
				life = owner;
				count = swingCount;
			}

			/// <summary>
			/// Walks within reach of the piece, then faces it and swings the hammer
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="point">Where the piece stands</param>
			/// <returns>True once the last swing's hammer has landed</returns>
			public bool Tick(float dt, Vector3 point)
			{
				return(Tick(dt, point, point));
			}

			/// <summary>
			/// Walks toward a spot until it's within reach of the piece, then faces the piece and swings the hammer
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="point">The part of the piece it works on</param>
			/// <param name="stand">Where it walks to reach it</param>
			/// <returns>True once the last swing's hammer has landed</returns>
			public bool Tick(float dt, Vector3 point, Vector3 stand)
			{
				Vector3 at = life.transform.position;
				if (swings == 0 && !InWorkReach(Utils.DistanceXZ(point, at), Utils.DistanceXZ(stand, at)))
				{
					life.Go(dt, stand, StandReach(Utils.DistanceXZ(stand, point)), false);
					return(false);
				}
				life.Face(point);
				life.HoldHammer(true);
				if (Time.time < nextSwing)
				{
					return(false);
				}
				if (swings >= count)
				{
					return(true);
				}
				VikingPoses.Swing(life.humanoid);
				swings++;
				nextSwing = Time.time + ((swings >= count) ? SwingHitTime : SwingInterval);
				return(false);
			}
		}

		/// <summary>
		/// Finds the point of a piece nearest a spot - on its solid colliders, or where it stands when it has none
		/// </summary>
		/// <param name="piece">The piece</param>
		/// <param name="from">The spot</param>
		/// <returns>The point</returns>
		private static Vector3 NearestPoint(WearNTear piece, Vector3 from)
		{
			Vector3 nearest = piece.transform.position;
			float nearestDistance = float.MaxValue;
			foreach (Collider collider in piece.GetComponentsInChildren<Collider>())
			{
				if (collider == null || collider.isTrigger || !collider.enabled)
				{
					continue;
				}
				// The physics can't find the nearest point of a mesh that isn't convex, so its bounds stand in for it
				MeshCollider mesh = collider as MeshCollider;
				Vector3 point = ((mesh != null && !mesh.convex) ? collider.bounds.ClosestPoint(from) : collider.ClosestPoint(from));
				float distance = Utils.DistanceXZ(point, from);
				if (distance < nearestDistance)
				{
					nearest = point;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// How far from a viking giving up on hammer work flames are looked for, for the log, in meters
		/// </summary>
		private const float GiveUpFlameReach = 10f;

		/// <summary>
		/// Counts the viking giving up on a piece it was to repair or clear the snow off, and in a test run or debug mode
		/// logs why - its walk there stuck or its time up - where it aimed and stood to work, how far it is from each,
		/// how near a fire's flames it is and what moved it last
		/// </summary>
		/// <param name="work">The kind of work, like "Repair"</param>
		/// <param name="piece">The piece</param>
		/// <param name="stuck">True when its walk there got stuck, false when its time ran out</param>
		/// <param name="hasAim">Whether it had picked where to aim and stand</param>
		/// <param name="aim">The part of the piece it worked on</param>
		/// <param name="stand">Where it walked to reach it</param>
		/// <param name="onMesh">Whether that spot was found on the navmesh, rather than being the ground under the aim</param>
		private void LogHammerGiveUp(string work, WearNTear piece, bool stuck, bool hasAim, Vector3 aim, Vector3 stand, bool onMesh)
		{
			TestLog.Count("Hammer work given up", work + ", " + (stuck ? "stuck" : "out of time"));
			if ((!TestLog.On && !Player.m_debugMode) || piece == null)
			{
				return;
			}
			string why = (stuck ? "its walk there got stuck" : "it didn't get within reach in " + RepairGiveUpTime.ToString("F0") + "s");
			string spots = "no spot picked to work from";
			if (hasAim)
			{
				spots = "aiming at " + aim.ToString("F1") + ", " + Utils.DistanceXZ(aim, transform.position).ToString("F1") + "m off, from " + stand.ToString("F1") + (onMesh ? " on the navmesh" : " off the navmesh") + ", " + Utils.DistanceXZ(stand, transform.position).ToString("F1") + "m off";
			}
			string flames = "no flames within " + GiveUpFlameReach.ToString("F0") + "m";
			if (FireSafety.FlamesNear(humanoid.GetCenterPoint(), GiveUpFlameReach, out Vector3 middle, out float reach))
			{
				flames = "the flames of the fire at " + middle.ToString("F1") + " " + Mathf.Max(0f, Utils.DistanceXZ(middle, transform.position) - reach).ToString("F1") + "m off";
				if (hasAim)
				{
					flames += ", " + Mathf.Max(0f, Utils.DistanceXZ(middle, aim) - reach).ToString("F1") + "m from the aim";
				}
			}
			Plugin.Log.LogInfo(work + ": " + TestLog.Name(humanoid, "F1") + " gives up on " + Utils.GetPrefabName(piece.gameObject) + " at " + piece.transform.position.ToString("F1") + " as " + why + ", leaving it alone for " + RepairAvoidTime.ToString("F0") + "s - " + spots + ", " + flames + ", moved by " + mover);
		}
	}
}
