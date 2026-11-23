using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Tames the animals an abandoned farm's pens spawned that came out wild, while the farm's village has living
	/// vikings to keep them
	/// </summary>
	/// <remarks>
	/// A pen's animal is tamed as it spawns when a living viking is near (see VikingFarmAnimalsPatch), but the pens can
	/// spawn before the village's vikings have loaded, or while they're off in another part of the village. Each farm's
	/// spawners are looked through every CheckInterval seconds, and the animal each one spawned is tamed by the client
	/// that owns it, so it's the vikings' livestock like the rest. Only what a farm's spawners spawned is tamed, not wild
	/// animals that wander in.
	///
	/// The livestock fights off the enemies that come near its pen, the way the vikings do (see Guard). A boar's own AI
	/// runs from an enemy more than its alert range off rather than fighting it, lets go of it near a fire, and, once
	/// it's tamed, lets go of one more than its alert range from where it spawned - so each client that has the animal
	/// loaded sets its AI up to stand and fight, whoever owns it now or later
	/// </remarks>
	internal static class VillageLivestock
	{
		/// <summary>
		/// Seconds between looks at the farms' spawners
		/// </summary>
		private const float CheckInterval = 1f;

		/// <summary>
		/// How far from where it spawned livestock goes for an enemy, and notices one, in meters - past the pen it stands
		/// in, so it meets what comes at the pen
		/// </summary>
		private const float GuardRange = 12f;

		private static float nextCheck;

		/// <summary>
		/// Tames the wild animals the loaded farms' pens spawned, in villages with living vikings, and sets their
		/// livestock up to fight off enemies - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (Time.time < nextCheck || ZNetScene.instance == null || !ServerCheck.Active || !Plugin.VikingVillages.Value)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			List<Village> villages = new List<Village>();
			foreach (Village village in Village.Loaded())
			{
				if (village.Vikings().Count > 0)
				{
					villages.Add(village);
				}
			}
			if (villages.Count == 0)
			{
				return;
			}
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZNetView view = instance.Value;
				if (view == null)
				{
					continue;
				}
				Vector3 position = instance.Key.GetPosition();
				bool near = false;
				foreach (Village village in villages)
				{
					if (village.DistanceOutside(position) <= Village.PieceMargin)
					{
						near = true;
						break;
					}
				}
				if (!near)
				{
					continue;
				}
				CreatureSpawner spawner = view.GetComponent<CreatureSpawner>();
				if (spawner != null && VikingSpawns.InFarm(spawner.transform.position))
				{
					Tame(instance.Key.GetConnectionZDOID(ZDOExtraData.ConnectionType.Spawned));
				}
			}
		}

		/// <summary>
		/// Tames an animal a farm's pen spawned when it's wild, alive, not a zombie, and owned by this client, and sets up
		/// livestock to fight off enemies (see Guard)
		/// </summary>
		/// <param name="spawned">The animal's ZDO, or ZDOID.None when the spawner has none</param>
		private static void Tame(ZDOID spawned)
		{
			if (spawned == ZDOID.None)
			{
				return;
			}
			GameObject found = ZNetScene.instance.FindInstance(spawned);
			Character animal = ((found != null) ? found.GetComponent<Character>() : null);
			if (animal == null || animal.IsDead() || animal.GetComponent<Tameable>() == null || Zombies.IsZombie(animal))
			{
				return;
			}
			if (animal.IsTamed())
			{
				if (VikingSpawns.IsLivestock(animal))
				{
					Guard(animal);
				}
				return;
			}
			if (animal.m_nview == null || !animal.m_nview.IsValid() || !animal.m_nview.IsOwner())
			{
				return;
			}
			animal.m_nview.GetZDO().Set(VikingSpawns.LivestockKey, true);
			animal.SetTamed(true);
			Guard(animal);
			Plugin.Log.LogInfo("Tamed the " + animal.GetHoverName() + " at " + animal.transform.position.ToString("F0") + " as a farm's livestock");
		}

		/// <summary>
		/// Sets livestock's AI up to fight the enemies that come near its pen rather than run from them - it no longer
		/// runs from an enemy it isn't alerted to or lets go of one near a fire, though it still keeps out of the flames,
		/// and goes for one within GuardRange of where it spawned
		/// </summary>
		/// <param name="animal">The livestock</param>
		private static void Guard(Character animal)
		{
			MonsterAI ai = animal.GetBaseAI() as MonsterAI;
			if (ai == null || (!ai.m_fleeIfNotAlerted && !ai.m_afraidOfFire && ai.m_alertRange >= GuardRange))
			{
				return;
			}
			ai.m_fleeIfNotAlerted = false;
			ai.m_afraidOfFire = false;
			ai.m_avoidFire = true;
			ai.m_alertRange = Mathf.Max(ai.m_alertRange, GuardRange);
		}
	}
}
