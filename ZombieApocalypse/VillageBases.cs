using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Makes each loaded village's zones - its core and every outpost - a player base the size of its ring, the way a
	/// workbench's area is, so the game's random spawns and creature spawners don't spawn inside the village
	/// </summary>
	/// <remarks>
	/// The game looks for a player base with a physics overlap on the character_trigger layer, so each zone gets a
	/// trigger on that layer. It's a tall upright capsule rather than a sphere, so ground that rises or falls across the
	/// village is still inside it, the way the ring is drawn flat. Each one also counts toward the base value of a
	/// player near it, which the game's raids look at, the way every workbench and fire does
	/// </remarks>
	internal static class VillageBases
	{
		/// <summary>
		/// Seconds between looks at the loaded villages' zones
		/// </summary>
		private const float CheckInterval = 2f;

		/// <summary>
		/// How far above and below a zone's middle its area reaches, in meters
		/// </summary>
		private const float HalfHeight = 200f;

		/// <summary>
		/// The areas of each loaded village, one for each of its zones, by its generator's ZDO
		/// </summary>
		private static readonly Dictionary<ZDOID, List<EffectArea>> areas = new Dictionary<ZDOID, List<EffectArea>>();

		private static float nextCheck;

		/// <summary>
		/// Puts an area on each zone of the loaded villages, matching it to the zone, and takes away the areas of villages
		/// no longer loaded - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (Time.time < nextCheck)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			HashSet<ZDOID> loaded = new HashSet<ZDOID>();
			if (ZNet.instance != null && ServerCheck.Active && Plugin.VikingVillages.Value)
			{
				foreach (Village village in Village.Loaded())
				{
					if (village.Zdo == null)
					{
						continue;
					}
					ZDOID id = village.Zdo.m_uid;
					loaded.Add(id);
					if (!areas.TryGetValue(id, out List<EffectArea> list))
					{
						list = new List<EffectArea>();
						areas[id] = list;
					}
					Match(list, village.Zones);
				}
			}
			List<ZDOID> gone = new List<ZDOID>();
			foreach (KeyValuePair<ZDOID, List<EffectArea>> entry in areas)
			{
				if (!loaded.Contains(entry.Key))
				{
					gone.Add(entry.Key);
				}
			}
			foreach (ZDOID id in gone)
			{
				Match(areas[id], new List<Village.Zone>());
				areas.Remove(id);
			}
		}

		/// <summary>
		/// Gives a village one area for each of its zones, standing on the zone and as wide as it - making the ones it
		/// lacks and taking away the ones it has too many of, or that were destroyed with the world
		/// </summary>
		/// <param name="list">The village's areas, changed to match</param>
		/// <param name="zones">Its zones</param>
		private static void Match(List<EffectArea> list, List<Village.Zone> zones)
		{
			list.RemoveAll((EffectArea area) => area == null);
			while (list.Count > zones.Count)
			{
				Object.Destroy(list[list.Count - 1].gameObject);
				list.RemoveAt(list.Count - 1);
			}
			while (list.Count < zones.Count)
			{
				list.Add(Make());
			}
			for (int i = 0; i < zones.Count; i++)
			{
				list[i].transform.position = zones[i].Center;
				CapsuleCollider capsule = (CapsuleCollider)list[i].GetComponent<Collider>();
				capsule.radius = zones[i].Radius;
				capsule.height = HalfHeight * 2f + zones[i].Radius * 2f;
			}
		}

		/// <summary>
		/// Makes a player base area, set up before it wakes
		/// </summary>
		/// <returns>The area</returns>
		private static EffectArea Make()
		{
			GameObject holder = new GameObject("VillageBase");
			holder.SetActive(false);
			holder.layer = LayerMask.NameToLayer("character_trigger");
			CapsuleCollider capsule = holder.AddComponent<CapsuleCollider>();
			capsule.isTrigger = true;
			capsule.direction = 1;
			EffectArea area = holder.AddComponent<EffectArea>();
			area.m_type = EffectArea.Type.PlayerBase;
			holder.SetActive(true);
			return(area);
		}
	}
}
