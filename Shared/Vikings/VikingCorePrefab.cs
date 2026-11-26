using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// Building a viking's prefab from the player's, with the player's parts swapped for a monster's, and adding it to
	/// the game's scene
	/// </summary>
	internal partial class VikingCore
	{
		/// <summary>
		/// Adds this mod's viking prefab to a world's prefabs, building it the first time (see BuildOwnPrefab)
		/// </summary>
		/// <param name="scene">The world's ZNetScene, as it wakes</param>
		public static void Register(ZNetScene scene)
		{
			if (prefab == null)
			{
				prefab = BuildOwnPrefab(scene);
			}
			if (prefab == null)
			{
				VikingMod.Log.LogWarning("Couldn't build the vikings - the Player or Draugr prefab is missing");
				return;
			}
			AddToScene(scene, prefab);
		}

		/// <summary>
		/// Adds a built prefab to a world's prefabs, unless it's in already
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		/// <param name="built">The prefab</param>
		private static void AddToScene(ZNetScene scene, GameObject built)
		{
			int hash = built.name.GetStableHashCode();
			if (!scene.m_namedPrefabs.ContainsKey(hash))
			{
				scene.m_prefabs.Add(built);
				scene.m_namedPrefabs.Add(hash, built);
			}
		}

		/// <summary>
		/// Builds the viking prefab this mod adds, with its kind's body and AI (see BuildPrefab)
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		/// <returns>The prefab, or null when the player's or draugr's prefab is missing</returns>
		private static partial GameObject BuildOwnPrefab(ZNetScene scene);

		/// <summary>
		/// Builds a viking prefab from the player's prefab, swapping the player's parts for a monster's with the
		/// draugr's AI
		/// </summary>
		/// <remarks>
		/// The copy sits under an inactive object, so none of its parts wake while it's changed, and it's kept across
		/// worlds
		/// </remarks>
		/// <typeparam name="TBody">The kind's body</typeparam>
		/// <typeparam name="TAI">The kind's AI</typeparam>
		/// <param name="scene">The world's ZNetScene</param>
		/// <param name="name">The prefab's name</param>
		/// <param name="faction">The viking's faction</param>
		/// <param name="finish">Sets up what only the kind has, before its VikingCore is added - given the prefab and
		/// its AI - or null when it has nothing more</param>
		/// <returns>The prefab, or null when the player's or draugr's prefab is missing</returns>
		private static GameObject BuildPrefab<TBody, TAI>(ZNetScene scene, string name, Character.Faction faction, System.Action<GameObject, MonsterAI> finish) where TBody : VikingBody where TAI : VikingAI
		{
			GameObject playerPrefab = scene.GetPrefab("Player");
			GameObject draugrPrefab = scene.GetPrefab("Draugr");
			if (playerPrefab == null || draugrPrefab == null || draugrPrefab.GetComponent<MonsterAI>() == null)
			{
				return(null);
			}
			if (prefabHolder == null)
			{
				prefabHolder = new GameObject(VikingMod.Name + " prefabs");
				prefabHolder.SetActive(false);
				Object.DontDestroyOnLoad(prefabHolder);
			}
			GameObject viking = Object.Instantiate(playerPrefab, prefabHolder.transform, false);
			viking.name = name;
			Player player = viking.GetComponent<Player>();
			VikingBody body = viking.AddComponent<TBody>();
			CopyFields(typeof(Character), player, body);
			CopyFields(typeof(Humanoid), player, body);
			Object.DestroyImmediate(player);
			RemoveComponent<PlayerController>(viking);
			RemoveComponent<Talker>(viking);
			RemoveComponent<Skills>(viking);
			body.m_name = "Viking";
			body.m_faction = faction;
			body.m_runSpeed = RunSpeed;
			body.m_defaultItems = new GameObject[0];
			// A player's death shakes their own controller, which a viking's shouldn't
			EffectList.EffectData[] deathEffects = body.m_deathEffects.m_effectPrefabs;
			body.m_deathEffects = new EffectList();
			body.m_deathEffects.m_effectPrefabs = deathEffects.Where((EffectList.EffectData effect) => effect.m_prefab == null || !effect.m_prefab.name.Contains("vibration")).ToArray();
			ZNetView view = viking.GetComponent<ZNetView>();
			view.m_persistent = true;
			view.m_type = ZDO.ObjectType.Default;
			MonsterAI ai = viking.AddComponent<TAI>();
			MonsterAI draugrAI = draugrPrefab.GetComponent<MonsterAI>();
			CopyFields(typeof(BaseAI), draugrAI, ai);
			CopyFields(typeof(MonsterAI), draugrAI, ai);
			ai.m_pathAgentType = Pathfinding.AgentType.Humanoid;
			// The game's sight through the Mistlands' mist goes by the mist this client shows, so a viking's goes by
			// MistSight in its place
			ai.m_mistVision = true;
			ai.m_avoidWater = false;
			ai.m_randomMoveRange = GuardRange;
			ai.m_enableHuntPlayer = false;
			finish?.Invoke(viking, ai);
			viking.AddComponent<VikingCore>();
			return(viking);
		}

		/// <summary>
		/// Copies the fields one type declares from one component to another
		/// </summary>
		/// <param name="type">The type whose own fields are copied</param>
		/// <param name="from">The component copied from</param>
		/// <param name="to">The component copied to</param>
		private static void CopyFields(System.Type type, object from, object to)
		{
			foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
			{
				if (!field.IsInitOnly && !field.IsLiteral)
				{
					field.SetValue(to, field.GetValue(from));
				}
			}
		}

		/// <summary>
		/// Removes a component from an object when it has one
		/// </summary>
		/// <typeparam name="T">The component's type</typeparam>
		/// <param name="target">The object</param>
		private static void RemoveComponent<T>(GameObject target) where T : Component
		{
			T component = target.GetComponent<T>();
			if (component != null)
			{
				Object.DestroyImmediate(component);
			}
		}
	}
}
