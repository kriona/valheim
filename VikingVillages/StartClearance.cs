using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeps abandoned farms, and the villages built around them, away from the world's start location, where players
	/// first arrive and come back to when they've no bed
	/// </summary>
	internal static class StartClearance
	{
		/// <summary>
		/// The game's start location, for when Game hasn't named one
		/// </summary>
		private const string DefaultStart = "StartTemple";

		/// <summary>
		/// How much ground is kept clear between a village's fence at its widest and the edge of the start location, in
		/// meters
		/// </summary>
		private const float Gap = 24f;

		/// <summary>
		/// Checks whether a farm standing at a spot would come too close to the world's start location, planned or
		/// placed
		/// </summary>
		/// <param name="spot">Where the farm would stand</param>
		/// <returns>True when it's too close</returns>
		public static bool NearStart(Vector3 spot)
		{
			if (ZoneSystem.instance == null)
			{
				return(false);
			}
			string start = ((Game.instance != null && !string.IsNullOrEmpty(Game.instance.m_StartLocation)) ? Game.instance.m_StartLocation : DefaultStart);
			foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.m_locationInstances.Values)
			{
				ZoneSystem.ZoneLocation location = instance.m_location;
				string name = ((location != null) ? (!string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefabName : location.m_name) : null);
				if (!string.Equals(name, start, System.StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				float reach = VillageFarmhouses.FenceRadiusMax + Gap + Mathf.Max(location.m_exteriorRadius, location.m_interiorRadius);
				if (Utils.DistanceXZ(instance.m_position, spot) < reach)
				{
					return(true);
				}
			}
			return(false);
		}
	}
}
