using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeps the oak trees a village is built among standing - clearing the ground for it never takes one down, its
	/// houses are placed out from under their branches, its other rooms and fence clear of their trunks, and its paths
	/// and flattened ground go around their trunks
	/// </summary>
	internal static class VillageOaks
	{
		/// <summary>
		/// How far an oak's trunk reaches from where it stands at its prefab's size, in meters - what the paths steer
		/// around and the ground isn't flattened under
		/// </summary>
		internal const float TrunkRadius = 2f;

		/// <summary>
		/// How far a farmhouse or house keeps from where an oak stands at its prefab's size, in meters, so its roof is
		/// out from under the branches
		/// </summary>
		internal const float HouseClearance = 7f;

		/// <summary>
		/// How far a field, pen, fence section or other low room keeps from where an oak stands at its prefab's size, in
		/// meters - only clear of the trunk, since it fits under the branches
		/// </summary>
		internal const float RoomClearance = 3f;

		/// <summary>
		/// How far past where a farm's rooms and houses can stand its oaks are looked for, in meters - past the largest
		/// room's half and a house's clearance from a large oak
		/// </summary>
		internal const float SearchMargin = 30f;

		/// <summary>
		/// An oak standing near a farm, and how many times its prefab's size it grew to
		/// </summary>
		internal struct Oak
		{
			public Vector3 Position;
			public float Scale;
		}

		/// <summary>
		/// The oaks around the farm being generated, which its rooms, houses and fence are kept clear of - empty between
		/// farms
		/// </summary>
		private static readonly List<Oak> farmOaks = new List<Oak>();

		/// <summary>
		/// Whether each prefab is an oak, by its name hash
		/// </summary>
		private static readonly Dictionary<int, bool> oakPrefabs = new Dictionary<int, bool>();

		/// <summary>
		/// How many oaks the farm being generated is kept clear of
		/// </summary>
		public static int FarmOakCount
		{
			get
			{
				return(farmOaks.Count);
			}
		}

		/// <summary>
		/// Checks whether a prefab's name is an oak's - Oak1, the Meadows' oak, or another mod's that starts the same
		/// </summary>
		/// <param name="name">The prefab's name</param>
		/// <returns>True when it is</returns>
		internal static bool IsOakName(string name)
		{
			return(name.StartsWith("Oak", System.StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Checks whether a loaded object is a grown oak tree - not its stump, its logs or a sapling
		/// </summary>
		/// <param name="view">The object</param>
		/// <returns>True when it is</returns>
		public static bool IsOak(ZNetView view)
		{
			return(view != null && view.GetComponent<TreeBase>() != null && IsOakName(Utils.GetPrefabName(view.gameObject)));
		}

		/// <summary>
		/// Checks whether a prefab is a grown oak tree (see IsOak), remembering the answer
		/// </summary>
		/// <param name="prefab">The prefab's name hash</param>
		/// <returns>True when it is</returns>
		private static bool IsOak(int prefab)
		{
			if (oakPrefabs.TryGetValue(prefab, out bool oak))
			{
				return(oak);
			}
			GameObject found = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(prefab) : null);
			if (found == null)
			{
				return(false);
			}
			oak = (found.GetComponent<TreeBase>() != null && IsOakName(found.name));
			oakPrefabs[prefab] = oak;
			return(oak);
		}

		/// <summary>
		/// Reads how many times its prefab's size an object grew to from its ZDO, the way the game sets its scale as it
		/// loads (see ZNetView.Awake)
		/// </summary>
		/// <param name="zdo">The object's ZDO</param>
		/// <returns>The scale, the wider of across and deep</returns>
		private static float ScaleOf(ZDO zdo)
		{
			Vector3 scale = zdo.GetVec3(ZDOVars.s_scaleHash, Vector3.zero);
			if (scale != Vector3.zero)
			{
				return(Mathf.Max(scale.x, scale.z));
			}
			return(zdo.GetFloat(ZDOVars.s_scaleScalarHash, 1f));
		}

		/// <summary>
		/// Notes the oaks within a distance of a farm about to be generated, from the world's objects whether they're
		/// loaded or not, so its rooms, houses and fence are kept clear of them until Forget
		/// </summary>
		/// <param name="middle">The farm's middle</param>
		/// <param name="reach">How far from it to look, in meters</param>
		public static void FindAround(Vector3 middle, float reach)
		{
			farmOaks.Clear();
			List<ZDO> zdos = new List<ZDO>();
			VillageRespawn.ZdosNear(middle, reach, zdos);
			foreach (ZDO zdo in zdos)
			{
				if (zdo != null && IsOak(zdo.GetPrefab()) && Utils.DistanceXZ(zdo.GetPosition(), middle) <= reach)
				{
					farmOaks.Add(new Oak { Position = zdo.GetPosition(), Scale = ScaleOf(zdo) });
				}
			}
		}

		/// <summary>
		/// Forgets the oaks of the farm that was being generated
		/// </summary>
		public static void Forget()
		{
			farmOaks.Clear();
		}

		/// <summary>
		/// Checks whether a room of the farm being generated, placed at a spot, would come too close to one of its oaks
		/// - a farmhouse within HouseClearance of one, any other room or fence section within RoomClearance
		/// </summary>
		/// <param name="room">The room</param>
		/// <param name="point">Where its middle would go</param>
		/// <param name="rotation">Which way it would face</param>
		/// <returns>True when it would</returns>
		public static bool Crowds(Room room, Vector3 point, Quaternion rotation)
		{
			if (farmOaks.Count == 0)
			{
				return(false);
			}
			float clearance = (VillageFarmhouses.IsFarmhouse(room) ? HouseClearance : RoomClearance);
			return(Crowds(FarmLayout.RoomFootprint(room, point, rotation), clearance, farmOaks));
		}

		/// <summary>
		/// Checks whether a house of the farm being generated would come within HouseClearance of one of its oaks
		/// </summary>
		/// <param name="footprint">The house's footprint</param>
		/// <returns>True when it would</returns>
		public static bool CrowdsHouse(FarmLayout.Footprint footprint)
		{
			return(Crowds(footprint, HouseClearance, farmOaks));
		}

		/// <summary>
		/// Checks whether a footprint comes within a clearance of any of some oaks, each clearance grown with its oak
		/// </summary>
		/// <param name="footprint">The footprint</param>
		/// <param name="clearance">How far it keeps from an oak at its prefab's size, in meters</param>
		/// <param name="oaks">The oaks</param>
		/// <returns>True when it does</returns>
		internal static bool Crowds(FarmLayout.Footprint footprint, float clearance, List<Oak> oaks)
		{
			return(oaks.Exists((Oak oak) => FarmLayout.Within(footprint, oak.Position, clearance * oak.Scale)));
		}
	}
}
