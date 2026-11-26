using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// A small, weak, short-lived cloud of poison that a zombie's corpse sometimes leaves when it rots away, from the
	/// Black Forest on
	/// </summary>
	/// <remarks>
	/// The cloud is a copy of the ooze bomb's, registered as a prefab of its own so every client shows the same smaller
	/// cloud, with less poison and a shorter life. With no owner it poisons whatever stands in it, though the undead -
	/// zombies included - shrug poison off. Its particles stop a little before it's removed, so it thins out rather
	/// than vanishing. A zombie viking's corpse is marked as a zombie's like any other (see CorpseSetupPatch), so it can
	/// leave one too
	/// </remarks>
	internal static class PoisonCloud
	{
		public const string PrefabName = "ZombiePoisonCloud";
		public static readonly int PrefabHash = PrefabName.GetStableHashCode();

		private const string SourcePrefab = "oozebomb_explosion";

		/// <summary>
		/// The first biome where a zombie's corpse can leave a cloud
		/// </summary>
		private const Heightmap.Biome FirstBiome = Heightmap.Biome.BlackForest;

		/// <summary>
		/// The poison each hit gives, how long the cloud lasts in seconds, and its size next to the ooze bomb's
		/// </summary>
		private const float Poison = 10f;
		public const float Lifetime = 4f;
		private const float SizeScale = 0.5f;

		/// <summary>
		/// The longest any of the cloud's particles last, so the last of them are gone by the time it's removed
		/// </summary>
		public const float ParticleLifetime = 1.5f;

		private static GameObject prefab;
		private static GameObject prefabHolder;

		/// <summary>
		/// Adds the cloud prefab to a world's prefabs, building it the first time
		/// </summary>
		/// <param name="scene">The world's ZNetScene, as it wakes</param>
		public static void Register(ZNetScene scene)
		{
			if (scene.m_namedPrefabs.ContainsKey(PrefabHash))
			{
				return;
			}
			GameObject built = BuildPrefab(scene);
			if (built == null)
			{
				Plugin.Log.LogWarning("Couldn't build the zombie poison cloud - the " + SourcePrefab + " prefab is missing");
				return;
			}
			scene.m_prefabs.Add(built);
			scene.m_namedPrefabs.Add(PrefabHash, built);
		}

		/// <summary>
		/// Builds the cloud from the ooze bomb's, smaller, weaker and shorter
		/// </summary>
		/// <remarks>
		/// The copy sits under an inactive object, so none of its parts wake while it's changed, and it's kept across
		/// worlds
		/// </remarks>
		/// <param name="scene">The world's ZNetScene</param>
		/// <returns>The prefab, or null when the ooze bomb's cloud is missing</returns>
		private static GameObject BuildPrefab(ZNetScene scene)
		{
			if (prefab != null)
			{
				return(prefab);
			}
			GameObject source = scene.GetPrefab(SourcePrefab);
			if (source == null || source.GetComponent<Aoe>() == null)
			{
				return(null);
			}
			if (prefabHolder == null)
			{
				prefabHolder = new GameObject("ZombieApocalypse poison cloud");
				prefabHolder.SetActive(false);
				Object.DontDestroyOnLoad(prefabHolder);
			}
			GameObject cloud = Object.Instantiate(source, prefabHolder.transform, false);
			cloud.name = PrefabName;
			Aoe aoe = cloud.GetComponent<Aoe>();
			aoe.m_damage = new HitData.DamageTypes();
			aoe.m_damage.m_poison = Poison;
			aoe.m_radius *= SizeScale;
			aoe.m_ttl = Lifetime;
			aoe.m_ttlMax = 0f;
			aoe.m_hitProps = false;
			// A smaller cloud needs fewer particles to look as thick, in proportion to its area
			float areaScale = SizeScale * SizeScale;
			foreach (ParticleSystem particles in cloud.GetComponentsInChildren<ParticleSystem>(true))
			{
				ParticleSystem.ShapeModule shape = particles.shape;
				shape.radius *= SizeScale;
				ParticleSystem.EmissionModule emission = particles.emission;
				emission.rateOverTimeMultiplier *= areaScale;
				ParticleSystem.MainModule main = particles.main;
				main.startLifetimeMultiplier = Mathf.Min(main.startLifetimeMultiplier, ParticleLifetime);
			}
			cloud.AddComponent<PoisonCloudFade>();
			prefab = cloud;
			return(prefab);
		}

		/// <summary>
		/// Checks whether a zombie's corpse at a point might leave a cloud as it rots away - from FirstBiome on, while
		/// PoisonCloudChance is above 0
		/// </summary>
		/// <param name="position">Where the corpse lies</param>
		/// <returns>True when it might</returns>
		public static bool CanLeaveCloud(Vector3 position)
		{
			if (Plugin.PoisonCloudChance.Value <= 0f || WorldGenerator.instance == null)
			{
				return(false);
			}
			return(VikingGear.IndexOf(WorldGenerator.instance.GetBiome(position)) >= VikingGear.IndexOf(FirstBiome));
		}

		/// <summary>
		/// Leaves a cloud where a zombie's corpse rots away, by the config's PoisonCloudChance, from FirstBiome on
		/// </summary>
		/// <param name="corpse">The corpse, owned by this client, as it's removed</param>
		public static void OnCorpseRemoved(Ragdoll corpse)
		{
			ZNetView nview = corpse.m_nview;
			if (nview == null || !nview.IsValid() || !nview.IsOwner() || !nview.GetZDO().GetBool(ZombieState.ZombieKey) || ZNetScene.instance == null)
			{
				return;
			}
			Vector3 position = corpse.GetAverageBodyPosition();
			int biome = VikingGear.IndexOf(WorldGenerator.instance.GetBiome(position));
			if (biome < VikingGear.IndexOf(FirstBiome) || Random.value * 100f >= Plugin.PoisonCloudChance.Value)
			{
				return;
			}
			GameObject cloud = ZNetScene.instance.GetPrefab(PrefabHash);
			if (cloud != null)
			{
				Object.Instantiate(cloud, position, Quaternion.identity);
			}
		}
	}

	/// <summary>
	/// Stops a poison cloud's particles shortly before it's removed, so it thins out instead of vanishing
	/// </summary>
	internal class PoisonCloudFade : MonoBehaviour
	{
		/// <summary>
		/// The clouds in the world, so they can be found without looking through every loaded object
		/// </summary>
		public static readonly List<PoisonCloudFade> Clouds = new List<PoisonCloudFade>();

		private float stopTime;
		private bool stopped;

		/// <summary>
		/// Adds the cloud to Clouds
		/// </summary>
		private void Awake()
		{
			Clouds.Add(this);
		}

		/// <summary>
		/// Takes the cloud out of Clouds as it's removed
		/// </summary>
		private void OnDestroy()
		{
			Clouds.Remove(this);
		}

		/// <summary>
		/// Sets when the particles stop
		/// </summary>
		private void Start()
		{
			stopTime = Time.time + Mathf.Max(0f, PoisonCloud.Lifetime - PoisonCloud.ParticleLifetime);
		}

		/// <summary>
		/// Stops the particles once it's time, leaving the ones already out to fade
		/// </summary>
		private void Update()
		{
			if (stopped || Time.time < stopTime)
			{
				return;
			}
			stopped = true;
			foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>())
			{
				particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
			}
		}
	}
}
