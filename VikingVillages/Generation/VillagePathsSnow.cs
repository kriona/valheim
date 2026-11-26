using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Clearing the deep snow off a Deep North village's ground as it's set up
	/// </summary>
	internal static partial class VillagePaths
	{
		/// <summary>
		/// Clears the Deep North's deep snow off the ground within SnowPaintRadius of a spot, the way a village's ground
		/// is cleared as it's set up
		/// </summary>
		/// <param name="spot">The spot, on the ground</param>
		/// <returns>False when the terrain operation isn't set up</returns>
		public static bool ClearSnowAt(Vector3 spot)
		{
			if (snowOp == null)
			{
				return(false);
			}
			Object.Instantiate(snowOp, spot, Quaternion.identity);
			return(true);
		}

		/// <summary>
		/// How far from a spot clearing the ground's snow reaches (see ClearSnowAt), in meters
		/// </summary>
		public const float SnowReach = SnowPaintRadius;

		/// <summary>
		/// Clears the Deep North's deep snow off a village's ground - within SnowFenceMargin past its farm's fence and
		/// SnowHouseMargin past its houses' walls, its outposts' included - putting the snow-clearing paint down every
		/// SnowSpacing meters where the ground is the Deep North's
		/// </summary>
		/// <param name="generator">The farm's generator</param>
		/// <param name="farm">Where the farm stands</param>
		/// <param name="houses">The village's houses</param>
		/// <param name="min">The lowest corner of the ground around the village</param>
		/// <param name="max">The highest corner of it</param>
		/// <returns>How many spots it was put down at</returns>
		private static int ClearSnow(ZDO generator, Vector3 farm, List<House> houses, Vector3 min, Vector3 max)
		{
			if (snowOp == null)
			{
				return(0);
			}
			float fence = generator.GetFloat(VillageFarmhouses.FenceKey, VillageFarmhouses.FenceRadiusMax) + SnowFenceMargin;
			int spots = 0;
			for (float x = min.x; x <= max.x; x += SnowSpacing)
			{
				for (float z = min.z; z <= max.z; z += SnowSpacing)
				{
					Vector3 point = new Vector3(x, 0f, z);
					if (!WorldGenerator.IsDeepnorth(x, z))
					{
						continue;
					}
					if (Utils.DistanceXZ(point, farm) > fence && !houses.Exists((House house) => Village.InHouse(house, point, SnowHouseMargin)))
					{
						continue;
					}
					point.y = ZoneSystem.instance.GetGroundHeight(point);
					Object.Instantiate(snowOp, point, Quaternion.identity);
					spots++;
				}
			}
			return(spots);
		}
	}
}
