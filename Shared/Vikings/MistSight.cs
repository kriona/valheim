using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// How far a viking sees in the Mistlands' mist - no farther than Range while it or what it looks at is in the mist,
	/// unless either is within reach of something that pushes the mist back, like a wisp light, a wisp torch or the
	/// dvergr's demisters
	/// </summary>
	/// <remarks>
	/// It takes the place of the game's own mist sight, which goes by the mist this client shows, so a viking is held
	/// to it the same on every client that runs it (see VikingMistSightPatch)
	/// </remarks>
	internal static class MistSight
	{
		/// <summary>
		/// How far a viking sees in the mist, in meters - as far as the dvergr and the game's other creatures do
		/// </summary>
		public const float Range = 10f;

		/// <summary>
		/// Checks whether the mist keeps a viking from seeing something
		/// </summary>
		/// <param name="eye">Where the viking looks from</param>
		/// <param name="target">Where what it looks at is</param>
		/// <returns>True when it can't see it for the mist</returns>
		public static bool Blocks(Vector3 eye, Vector3 target)
		{
			float distance = Vector3.Distance(eye, target);
			if (distance <= Range)
			{
				return(false);
			}
			return(Blocked(distance, Mister.InsideMister(eye), Mister.InsideMister(target), NearDemister(eye), NearDemister(target)));
		}

		/// <summary>
		/// Works out whether the mist keeps a viking from seeing something - farther than Range, with either of them in
		/// the mist and neither within reach of anything that pushes it back
		/// </summary>
		/// <param name="distance">How far apart they are, in meters</param>
		/// <param name="viewerInMist">Whether the viking is in the mist</param>
		/// <param name="targetInMist">Whether what it looks at is</param>
		/// <param name="viewerLit">Whether the viking is within reach of something that pushes the mist back</param>
		/// <param name="targetLit">Whether what it looks at is</param>
		/// <returns>True when it can't see it</returns>
		internal static bool Blocked(float distance, bool viewerInMist, bool targetInMist, bool viewerLit, bool targetLit)
		{
			return(distance > Range && (viewerInMist || targetInMist) && !viewerLit && !targetLit);
		}

		/// <summary>
		/// Checks whether a point is within reach of something that pushes the mist back - a wisp light, a wisp torch, the
		/// dvergr's demisters and the like
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>True when it is</returns>
		private static bool NearDemister(Vector3 point)
		{
			foreach (Demister demister in Demister.GetDemisters())
			{
				ParticleSystemForceField field = ((demister != null) ? demister.m_forceField : null);
				if (field != null && field.enabled && Vector3.Distance(demister.transform.position, point) < field.endRange)
				{
					return(true);
				}
			}
			return(false);
		}
	}
}
