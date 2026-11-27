using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What the PC vikings watch out for that isn't an enemy yet - corpses about to rise as zombies, and the poison
	/// clouds zombies' corpses leave, and the corpses that might leave one - all of which come from Zombie Apocalypse,
	/// read through ZombieState and ZombieLink, so there are none without it
	/// </summary>
	/// <remarks>
	/// The corpses and clouds are looked through once every RefreshInterval seconds, for every viking on this client. A
	/// zombie's corpse may leave a cloud as it rots away, rolled only then, so every zombie's corpse where a cloud can be
	/// left counts until it's gone
	/// </remarks>
	internal static class VillageHazards
	{
		/// <summary>
		/// A corpse about to rise - its ZDO, so its zombie can be found once it stands, and where it lies
		/// </summary>
		public struct Rising
		{
			public ZDOID Id;
			public Vector3 Position;
		}

		/// <summary>
		/// Seconds between looks through the corpses and clouds
		/// </summary>
		private const float RefreshInterval = 1f;

		private static readonly List<Rising> rising = new List<Rising>();
		private static readonly List<Vector3> poison = new List<Vector3>();

		/// <summary>
		/// Every corpse that has woken, dropped once it's destroyed
		/// </summary>
		private static readonly List<Ragdoll> corpses = new List<Ragdoll>();

		private static float nextRefresh;

		/// <summary>
		/// Finds the corpse about to rise nearest a point
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="range">How far from it to look, in meters</param>
		/// <param name="corpse">The corpse</param>
		/// <returns>False when there's none that near</returns>
		public static bool NearestRising(Vector3 point, float range, out Rising corpse)
		{
			Refresh();
			corpse = new Rising();
			float nearest = range;
			bool found = false;
			foreach (Rising candidate in rising)
			{
				float distance = Vector3.Distance(candidate.Position, point);
				if (distance <= nearest)
				{
					corpse = candidate;
					nearest = distance;
					found = true;
				}
			}
			return(found);
		}

		/// <summary>
		/// Finds the poison nearest a point - a cloud, or a zombie's corpse that might leave one
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="range">How far from it to look, in meters</param>
		/// <param name="spot">Where the poison is</param>
		/// <returns>False when there's none that near</returns>
		public static bool NearestPoison(Vector3 point, float range, out Vector3 spot)
		{
			Refresh();
			spot = Vector3.zero;
			float nearest = range;
			bool found = false;
			foreach (Vector3 candidate in poison)
			{
				float distance = Utils.DistanceXZ(candidate, point);
				if (distance <= nearest)
				{
					spot = candidate;
					nearest = distance;
					found = true;
				}
			}
			return(found);
		}

		/// <summary>
		/// Checks whether a corpse has risen, and finds its zombie when it has
		/// </summary>
		/// <param name="corpse">The corpse's ZDO</param>
		/// <param name="zombie">Its zombie, or null when it hasn't risen or isn't loaded</param>
		/// <returns>True once it has risen or is gone</returns>
		public static bool HasRisen(ZDOID corpse, out Character zombie)
		{
			zombie = null;
			ZDO zdo = ((ZDOMan.instance != null) ? ZDOMan.instance.GetZDO(corpse) : null);
			if (zdo == null)
			{
				return(true);
			}
			ZDOID zombieId = zdo.GetZDOID(ZombieState.ZombieIdKey);
			if (zombieId == ZDOID.None)
			{
				return(false);
			}
			GameObject found = ((ZNetScene.instance != null) ? ZNetScene.instance.FindInstance(zombieId) : null);
			zombie = ((found != null) ? found.GetComponent<Character>() : null);
			return(true);
		}

		/// <summary>
		/// Notes a corpse as it wakes, so Refresh can look through the corpses alone
		/// </summary>
		/// <param name="corpse">The corpse</param>
		public static void Register(Ragdoll corpse)
		{
			corpses.Add(corpse);
		}

		/// <summary>
		/// Looks through the corpses and clouds for corpses about to rise, clouds and zombies' corpses that might leave
		/// one, every RefreshInterval seconds
		/// </summary>
		private static void Refresh()
		{
			if (Time.time < nextRefresh || ZNetScene.instance == null)
			{
				return;
			}
			nextRefresh = Time.time + RefreshInterval;
			rising.Clear();
			poison.Clear();
			ZombieLink.PoisonClouds(poison);
			corpses.RemoveAll((Ragdoll candidate) => candidate == null);
			foreach (Ragdoll corpse in corpses)
			{
				ZNetView view = corpse.m_nview;
				if (view == null || !view.IsValid())
				{
					continue;
				}
				ZDO zdo = view.GetZDO();
				bool waiting = (zdo.GetLong(ZombieState.RiseAtKey) > 0L && zdo.GetZDOID(ZombieState.ZombieIdKey) == ZDOID.None);
				bool zombie = zdo.GetBool(ZombieState.ZombieKey);
				if (!waiting && !zombie)
				{
					continue;
				}
				Vector3 position = corpse.GetAverageBodyPosition();
				if (waiting)
				{
					rising.Add(new Rising { Id = zdo.m_uid, Position = position });
				}
				else if (ZombieLink.CanLeaveCloud(position))
				{
					poison.Add(position);
				}
			}
		}
	}
}
