using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A valkyrie's hit and run against an enemy that can't strike at range - with no rider, she hovers above its reach
	/// loosing her feather volley, dives in for a swoop whenever it's ready and climbs straight back out of reach
	/// </summary>
	/// <remarks>
	/// Whether a creature can strike at range, and how far its blows reach, are read from its prefab's attacks, so every
	/// client agrees and they're only worked out once for each kind. A player can carry a bow, so she never fights one
	/// this way
	/// </remarks>
	internal class ValkyrieSkirmish : MonoBehaviour
	{
		/// <summary>
		/// What she's doing in the fight - hovering out of reach, diving in for a swoop, or climbing back out
		/// </summary>
		private enum Phase
		{
			Hover,
			Dive,
			Climb
		}

		/// <summary>
		/// The start of the prefab names of the creatures whose ranged attacks don't count - greydwarves' thrown stones
		/// hurt her too little to go after them first, or keep her from fighting them hit and run
		/// </summary>
		private const string HarmlessThrowers = "Greydwarf";

		/// <summary>
		/// How far a creature with no attacks of its own, or none listed, is taken to reach, in meters
		/// </summary>
		internal const float DefaultReach = 3f;

		/// <summary>
		/// How far above an enemy's reach she hovers, in meters
		/// </summary>
		internal const float Margin = 2f;

		/// <summary>
		/// How far to the side of the enemy she hovers, in meters - inside her volley's range, and off to one side so
		/// she can face it
		/// </summary>
		private const float HoverOffset = 6f;

		/// <summary>
		/// How near the enemy she swoops, and how near the spot she's flying to counts as there, in meters
		/// </summary>
		private const float SwoopReach = 4f;
		private const float Arrived = 2f;

		/// <summary>
		/// How long a dive lasts at most before she gives it up and climbs back out, in seconds
		/// </summary>
		private const float DiveLimit = 4f;

		/// <summary>
		/// Each creature's kind's reach and whether it can strike at range, by prefab name, once worked out
		/// </summary>
		private static readonly Dictionary<string, KeyValuePair<bool, float>> kinds = new Dictionary<string, KeyValuePair<bool, float>>();

		/// <summary>
		/// How often she looks for an enemy that can strike her at range, in seconds
		/// </summary>
		private const float RangedLookInterval = 1f;

		private Humanoid body;
		private Phase phase;
		private float diveStart;
		private float nextRangedLook;

		/// <summary>
		/// Finds her body
		/// </summary>
		private void Awake()
		{
			body = GetComponent<Humanoid>();
		}

		/// <summary>
		/// Checks whether any of a creature's attacks strikes at range - throwing or shooting something
		/// </summary>
		/// <param name="types">Its attacks' types</param>
		/// <returns>True when one does</returns>
		internal static bool StrikesAtRange(IEnumerable<Attack.AttackType> types)
		{
			return(types.Any((Attack.AttackType type) => type == Attack.AttackType.Projectile || type == Attack.AttackType.TriggerProjectile));
		}

		/// <summary>
		/// Checks whether a kind of creature's ranged attacks count - every kind's do but greydwarves', whose thrown
		/// stones hurt her too little
		/// </summary>
		/// <param name="prefabName">The creature's prefab name</param>
		/// <returns>True when they count</returns>
		internal static bool RangedCounts(string prefabName)
		{
			return(!prefabName.StartsWith(HarmlessThrowers));
		}

		/// <summary>
		/// How far a creature's blows reach - its longest attack's, or a default when it has none
		/// </summary>
		/// <param name="ranges">Its attacks' ranges, in meters</param>
		/// <returns>The reach, in meters</returns>
		internal static float ReachOf(IEnumerable<float> ranges)
		{
			float reach = 0f;
			foreach (float range in ranges)
			{
				reach = Mathf.Max(reach, range);
			}
			return(((reach > 0f) ? reach : DefaultReach));
		}

		/// <summary>
		/// How high above an enemy's feet she's out of its reach - its height and its reach, and a margin
		/// </summary>
		/// <param name="height">The enemy's height, in meters</param>
		/// <param name="reach">How far its blows reach, in meters</param>
		/// <returns>The height, in meters</returns>
		internal static float SafeHeight(float height, float reach)
		{
			return(height + reach + Margin);
		}

		/// <summary>
		/// Checks whether a creature or player can strike her at range in the air - a creature whose kind has an attack
		/// that throws or shoots, or a player holding something that does, by what the game shows in their hands
		/// </summary>
		/// <param name="character">The creature or player</param>
		/// <returns>True when it can</returns>
		public static bool CanStrikeAtRange(Character character)
		{
			if (!character.IsPlayer())
			{
				return(Kind(character).Key);
			}
			ZNetView view = character.m_nview;
			if (view == null || !view.IsValid() || ObjectDB.instance == null)
			{
				return(false);
			}
			foreach (int hash in new[] { view.GetZDO().GetInt(ZDOVars.s_rightItem), view.GetZDO().GetInt(ZDOVars.s_leftItem) })
			{
				GameObject item = ((hash != 0) ? ObjectDB.instance.GetItemPrefab(hash) : null);
				ItemDrop drop = ((item != null) ? item.GetComponent<ItemDrop>() : null);
				if (drop == null)
				{
					continue;
				}
				ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
				List<Attack.AttackType> types = new List<Attack.AttackType>();
				if (shared.m_attack != null)
				{
					types.Add(shared.m_attack.m_attackType);
				}
				if (shared.m_secondaryAttack != null)
				{
					types.Add(shared.m_secondaryAttack.m_attackType);
				}
				if (StrikesAtRange(types))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Finds the nearest enemy she can see that can strike her at range in the air, now and then, to go after first
		/// </summary>
		/// <param name="ai">Her AI</param>
		/// <returns>The enemy, or null when there's none or it isn't time to look again</returns>
		public Character RangedTarget(MonsterAI ai)
		{
			if (Time.time < nextRangedLook)
			{
				return(null);
			}
			nextRangedLook = Time.time + RangedLookInterval;
			Character nearest = null;
			float nearestDistance = ai.m_viewRange;
			foreach (Character character in Character.GetAllCharacters())
			{
				if (character == null || character == body || character.IsDead() || !BaseAI.IsEnemy(body, character))
				{
					continue;
				}
				float distance = Vector3.Distance(character.transform.position, transform.position);
				if (distance < nearestDistance && CanStrikeAtRange(character) && ai.CanSeeTarget(character))
				{
					nearest = character;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Checks whether she fights a creature hit and run - one that isn't a player, doesn't fly and can't strike at
		/// range
		/// </summary>
		/// <param name="target">The creature</param>
		/// <returns>True when she does</returns>
		public static bool HitAndRun(Character target)
		{
			return(target != null && !target.IsPlayer() && !target.IsFlying() && !Kind(target).Key);
		}

		/// <summary>
		/// Works out a creature's kind's reach and whether it can strike at range from its prefab's attacks - its own,
		/// and those it might be given at random - once for each kind
		/// </summary>
		/// <param name="target">The creature</param>
		/// <returns>Whether it can strike at range, and its reach in meters</returns>
		private static KeyValuePair<bool, float> Kind(Character target)
		{
			string name = Utils.GetPrefabName(target.gameObject);
			if (kinds.TryGetValue(name, out KeyValuePair<bool, float> known))
			{
				return(known);
			}
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(name) : null);
			Humanoid humanoid = ((prefab != null) ? prefab.GetComponent<Humanoid>() : null);
			List<Attack> attacks = new List<Attack>();
			if (humanoid != null)
			{
				IEnumerable<GameObject> items = (humanoid.m_defaultItems ?? new GameObject[0]).Concat(humanoid.m_randomWeapon ?? new GameObject[0]);
				if (humanoid.m_randomSets != null)
				{
					items = items.Concat(humanoid.m_randomSets.SelectMany((Humanoid.ItemSet set) => set.m_items ?? new GameObject[0]));
				}
				foreach (GameObject item in items)
				{
					ItemDrop drop = ((item != null) ? item.GetComponent<ItemDrop>() : null);
					if (drop == null || !drop.m_itemData.IsWeapon())
					{
						continue;
					}
					ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
					if (shared.m_attack != null)
					{
						attacks.Add(shared.m_attack);
					}
					if (shared.m_secondaryAttack != null && shared.m_secondaryAttack.m_attackType != Attack.AttackType.None)
					{
						attacks.Add(shared.m_secondaryAttack);
					}
				}
			}
			bool ranged = RangedCounts(name) && StrikesAtRange(attacks.Select((Attack attack) => attack.m_attackType));
			float reach = ReachOf(attacks.Where((Attack attack) => attack.m_attackType != Attack.AttackType.None).Select((Attack attack) => Mathf.Max(attack.m_attackRange, attack.m_attackRayWidth)));
			known = new KeyValuePair<bool, float>(ranged, reach);
			kinds[name] = known;
			return(known);
		}

		/// <summary>
		/// Fights her target hit and run, on the client that owns her while she flies with no rider, before her AI's
		/// update - hovering above its reach loosing her volley, diving in for a swoop and climbing back out
		/// </summary>
		/// <param name="ai">Her AI</param>
		/// <returns>True when she's fighting this way, so her AI leaves her be this update</returns>
		public bool Think(MonsterAI ai)
		{
			Character target = ai.GetTargetCreature();
			if (!body.IsFlying() || !HitAndRun(target))
			{
				phase = Phase.Hover;
				return(false);
			}
			KeyValuePair<bool, float> kind = Kind(target);
			Vector3 hover = HoverPoint(target, kind.Value);
			if (body.InAttack())
			{
				if (phase == Phase.Dive)
				{
					phase = Phase.Climb;
				}
				FlyTo(ai, hover);
				return(true);
			}
			Vector3 center = target.GetCenterPoint();
			switch (phase)
			{
				case Phase.Dive:
					if (Time.time - diveStart > DiveLimit)
					{
						phase = Phase.Climb;
						break;
					}
					if (Vector3.Distance(center, transform.position) - target.GetRadius() <= SwoopReach && Use(ValkyrieCombat.SwoopName, target))
					{
						phase = Phase.Climb;
						break;
					}
					FlyTo(ai, center);
					return(true);
				case Phase.Climb:
					if (Vector3.Distance(transform.position, hover) <= Arrived)
					{
						phase = Phase.Hover;
					}
					break;
			}
			if (phase == Phase.Hover)
			{
				ai.LookAt(center);
				if (Use(ValkyrieCombat.VolleyName, target))
				{
					return(true);
				}
				if (Ready(ValkyrieCombat.SwoopName))
				{
					phase = Phase.Dive;
					diveStart = Time.time;
					FlyTo(ai, center);
					return(true);
				}
			}
			FlyTo(ai, hover);
			return(true);
		}

		/// <summary>
		/// Where she hovers out of an enemy's reach - over it at the height it can't reach, off to the side she's on
		/// </summary>
		/// <param name="target">The enemy</param>
		/// <param name="reach">How far its blows reach, in meters</param>
		/// <returns>The spot</returns>
		private Vector3 HoverPoint(Character target, float reach)
		{
			Vector3 side = transform.position - target.transform.position;
			side.y = 0f;
			side = ((side.sqrMagnitude > 0.01f) ? side.normalized : -target.transform.forward);
			CapsuleCollider capsule = target.GetComponent<CapsuleCollider>();
			float height = ((capsule != null) ? capsule.height * target.transform.lossyScale.y : 2f);
			return(target.transform.position + side * HoverOffset + Vector3.up * SafeHeight(height, reach));
		}

		/// <summary>
		/// Flies her toward a spot, at her fast flying speed, stopping once she's there
		/// </summary>
		/// <param name="ai">Her AI</param>
		/// <param name="point">The spot</param>
		private void FlyTo(MonsterAI ai, Vector3 point)
		{
			Vector3 toward = point - transform.position;
			if (toward.magnitude <= Arrived * 0.5f)
			{
				ai.StopMoving();
				return;
			}
			ai.MoveTowards(toward, true);
		}

		/// <summary>
		/// Checks whether one of her attacks is ready again
		/// </summary>
		/// <param name="name">The attack's name</param>
		/// <returns>True when she has it and it's ready</returns>
		private bool Ready(string name)
		{
			ItemDrop.ItemData attack = Find(name);
			return(attack != null && ValkyrieCombat.Ready(Time.time, attack.m_lastAttackTime, attack.m_shared.m_aiAttackInterval));
		}

		/// <summary>
		/// Makes one of her attacks at her target when it's ready and the target's within its range
		/// </summary>
		/// <param name="name">The attack's name</param>
		/// <param name="target">Her target</param>
		/// <returns>True when she started it</returns>
		private bool Use(string name, Character target)
		{
			ItemDrop.ItemData attack = Find(name);
			if (attack == null || !ValkyrieCombat.Ready(Time.time, attack.m_lastAttackTime, attack.m_shared.m_aiAttackInterval))
			{
				return(false);
			}
			float distance = Vector3.Distance(target.transform.position, transform.position) - target.GetRadius();
			if (distance < attack.m_shared.m_aiAttackRangeMin || distance > attack.m_shared.m_aiAttackRange)
			{
				return(false);
			}
			Vector3 facing = target.transform.position - transform.position;
			facing.y = 0f;
			if (facing.sqrMagnitude > 0.0001f)
			{
				transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
			}
			if (!body.IsItemEquiped(attack))
			{
				body.EquipItem(attack, false);
			}
			return(body.StartAttack(target, false));
		}

		/// <summary>
		/// Finds one of her attacks by name
		/// </summary>
		/// <param name="name">The attack's name</param>
		/// <returns>The attack, or null when she hasn't got it</returns>
		private ItemDrop.ItemData Find(string name)
		{
			return(body.GetInventory().GetAllItems().FirstOrDefault((ItemDrop.ItemData item) => item.m_shared.m_name == name));
		}
	}
}
