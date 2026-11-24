using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Has living and dvergr vikings scare off birds the way players do - a bird on the ground takes off when one comes
	/// within its danger distance, and never lands near one (see RandomFlyingBird.DangerNearby)
	/// </summary>
	/// <remarks>
	/// Every bird on the ground checks each physics step, so the vikings' spots are listed once a step and shared
	/// </remarks>
	internal static class BirdScare
	{
		/// <summary>
		/// Where each loaded living or dvergr viking stands, as of the physics step it was listed at
		/// </summary>
		private static readonly List<Vector3> spots = new List<Vector3>();

		/// <summary>
		/// The physics step's time the vikings' spots were listed at, or below 0 before they ever were
		/// </summary>
		private static float listedAt = -1f;

		/// <summary>
		/// Checks whether a living or dvergr viking stands within a bird's danger distance of a spot
		/// </summary>
		/// <param name="point">The spot</param>
		/// <param name="range">The bird's danger distance, in meters</param>
		/// <returns>True when one does</returns>
		public static bool VikingNear(Vector3 point, float range)
		{
			if (listedAt != Time.fixedTime)
			{
				listedAt = Time.fixedTime;
				spots.Clear();
				foreach (Character character in Character.GetAllCharacters())
				{
					if (character != null && !character.IsDead() && VikingCore.IsLivingOrDvergr(character))
					{
						spots.Add(character.transform.position);
					}
				}
			}
			return(AnyWithin(spots, point, range));
		}

		/// <summary>
		/// Checks whether any of a list of spots is closer to a point than a range, measured the way the game measures a
		/// player's distance from a bird (see Player.IsPlayerInRange)
		/// </summary>
		/// <param name="spots">The spots</param>
		/// <param name="point">The point</param>
		/// <param name="range">The range, in meters</param>
		/// <returns>True when one is</returns>
		internal static bool AnyWithin(List<Vector3> spots, Vector3 point, float range)
		{
			foreach (Vector3 spot in spots)
			{
				if (Vector3.Distance(spot, point) < range)
				{
					return(true);
				}
			}
			return(false);
		}
	}
}
