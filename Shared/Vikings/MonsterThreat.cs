using System.Collections.Generic;
using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// Weighs up what a monster can do to a viking - how hard it hits and how far it reaches - from the attacks the
	/// game gives it, so a viking can tell a tough one from one it can take on (see VikingCore.IsTough) and keep out
	/// of its reach (see VillageLife.UpdateKite)
	/// </summary>
	/// <remarks>
	/// A monster fighting with the weapons in its inventory is weighed by them each time, since two of the same prefab
	/// can carry different ones, like vikings, and one's gear can change or load after it spawns. One with none is
	/// weighed by the weapons its prefab gives it, which are the same for every one of that prefab, so they're
	/// remembered by prefab name
	/// </remarks>
	internal static class MonsterThreat
	{
		/// <summary>
		/// How far a monster with no attack the game says the reach of reaches, past its own size, in meters
		/// </summary>
		internal const float DefaultReach = 2f;

		/// <summary>
		/// Checks whether a creature is a root, or any other monster that can't move from where it stands, which the
		/// vikings fighting hand to hand keep clear of
		/// </summary>
		/// <param name="creature">The creature</param>
		/// <returns>True when it is</returns>
		public static bool IsRoot(Character creature)
		{
			return(creature != null && !creature.IsPlayer() && creature.m_speed <= 0f && creature.m_runSpeed <= 0f && creature.m_walkSpeed <= 0f && creature.GetBaseAI() != null);
		}

		/// <summary>
		/// What the weapons each monster prefab gives do, by prefab name - the hardest hit at one star and the farthest
		/// reach hand to hand, past the monster's own size
		/// </summary>
		private static readonly Dictionary<string, KeyValuePair<float, float>> known = new Dictionary<string, KeyValuePair<float, float>>();

		/// <summary>
		/// What one of a monster's attacks does
		/// </summary>
		internal struct Swing
		{
			/// <summary>
			/// The damage it deals at one star, scaled by the attack
			/// </summary>
			public float Hit;

			/// <summary>
			/// How far it reaches, in meters past the monster's own size
			/// </summary>
			public float Range;

			/// <summary>
			/// Whether it shoots or throws, so its range isn't a reach hand to hand
			/// </summary>
			public bool Projectile;
		}

		/// <summary>
		/// Finds a monster's hardest hit - the most any of its attacks deals, the way the game scales it by the attack
		/// and the monster's stars, with a bow's or crossbow's best ammo the monster carries, and a zombie viking's by
		/// its skill and its biome's MeleeScale the way its hits are scaled as it deals them (see VikingHit)
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>The damage, or 0 when it has no attacks the game says the damage of</returns>
		public static float Hit(Character monster)
		{
			return(Read(monster).Key * LevelFactor(monster.GetLevel()));
		}

		/// <summary>
		/// Finds how far a monster reaches hand to hand from its middle - the farthest of its attacks that don't shoot or
		/// throw, past its own size
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>The reach, in meters</returns>
		public static float Reach(Character monster)
		{
			return(Read(monster).Value + monster.GetRadius());
		}

		/// <summary>
		/// Works out how much harder a monster's stars make it hit - half as hard again for each star past the first
		/// </summary>
		/// <param name="level">The monster's level, 1 for no stars</param>
		/// <returns>The factor its one-star hit is multiplied by</returns>
		internal static float LevelFactor(int level)
		{
			return(1f + Mathf.Max(0, level - 1) * 0.5f);
		}

		/// <summary>
		/// Works out how hard an attack hits - all the damage it deals to a creature, leaving out what only harms trees,
		/// rocks and pieces, scaled by the attack
		/// </summary>
		/// <param name="damage">The weapon's damage</param>
		/// <param name="multiplier">The attack's damage multiplier</param>
		/// <returns>The damage</returns>
		internal static float SwingHit(HitData.DamageTypes damage, float multiplier)
		{
			float total = damage.m_damage + damage.GetTotalPhysicalDamage() + damage.GetTotalElementalDamage() + damage.m_poison + damage.m_spirit;
			return(total * multiplier);
		}

		/// <summary>
		/// Works out how hard a shot hits - the weapon's damage with the ammo that adds the most to it, the way the game
		/// adds the ammo's damage to the shot, scaled by the attack (see SwingHit)
		/// </summary>
		/// <param name="damage">The weapon's damage</param>
		/// <param name="ammo">The damage of each kind of ammo the shooter carries for it, none for the weapon alone</param>
		/// <param name="multiplier">The attack's damage multiplier</param>
		/// <returns>The damage</returns>
		internal static float ShotHit(HitData.DamageTypes damage, List<HitData.DamageTypes> ammo, float multiplier)
		{
			float best = SwingHit(damage, multiplier);
			foreach (HitData.DamageTypes shot in ammo)
			{
				HitData.DamageTypes total = damage;
				total.Add(shot);
				best = Mathf.Max(best, SwingHit(total, multiplier));
			}
			return(best);
		}

		/// <summary>
		/// Works out how hard a viking's attack hits as it deals it - scaled by its skill with the weapon, on average, the
		/// way a player's is (see VikingCore.SkillScaled), and an attack hand to hand by its biome's MeleeScale (see
		/// VikingCore.DamageScale)
		/// </summary>
		/// <param name="hit">The attack's hit (see SwingHit)</param>
		/// <param name="projectile">Whether it shoots or throws</param>
		/// <param name="meleeScale">How much harder the viking hits hand to hand than a player of its skill</param>
		/// <param name="skill">Its skill with the weapon, from 0 to 1</param>
		/// <returns>The damage</returns>
		internal static float VikingHit(float hit, bool projectile, float meleeScale, float skill)
		{
			float scale = (projectile ? 1f : meleeScale);
			return(hit * scale * Mathf.Lerp(VikingCore.SkillDamageBase, 1f, Mathf.Clamp01(skill)));
		}

		/// <summary>
		/// Finds how much harder a zombie viking hits hand to hand than a player of its skill - its biome's MeleeScale,
		/// read from the data on its ZDO the way VikingCore.DamageScale reads it, its biome where it stands when its data
		/// doesn't say, and none for a tombstone viking or one with no data yet, the same as DamageScale
		/// </summary>
		/// <remarks>
		/// The data is read from the ZDO rather than the zombie's VikingCore, which is Zombie Apocalypse's own type, so it
		/// works with any version of it - its MeleeScale is this mod's copy of the table
		/// </remarks>
		/// <param name="monster">The zombie viking</param>
		/// <returns>The multiplier</returns>
		private static float ZombieMeleeScale(Character monster)
		{
			ZDO zdo = ((monster.m_nview != null && monster.m_nview.IsValid()) ? monster.m_nview.GetZDO() : null);
			VikingData data = ((zdo != null) ? VikingData.FromBytes(zdo.GetByteArray(VikingCore.DataKey)) : null);
			if (data == null || data.Tombstone)
			{
				return(1f);
			}
			int index = ((data.Biome >= 0) ? data.Biome : VikingGear.IndexOf(WorldGenerator.instance.GetBiome(monster.transform.position)));
			return(VikingGear.Get(index).MeleeScale);
		}

		/// <summary>
		/// Weighs up a monster's attacks - its hardest hit, and its farthest reach hand to hand, leaving out attacks that
		/// shoot or throw, or DefaultReach when it has none that reach anywhere
		/// </summary>
		/// <param name="swings">The attacks</param>
		/// <returns>The hardest hit, or 0 with no attacks, and the farthest reach past its own size</returns>
		internal static KeyValuePair<float, float> Weigh(List<Swing> swings)
		{
			float hit = 0f;
			float reach = 0f;
			foreach (Swing swing in swings)
			{
				hit = Mathf.Max(hit, swing.Hit);
				if (!swing.Projectile)
				{
					reach = Mathf.Max(reach, swing.Range);
				}
			}
			return(new KeyValuePair<float, float>(hit, ((reach > 0f) ? reach : DefaultReach)));
		}

		/// <summary>
		/// Reads a monster's attacks from the weapons it fights with - those it carries each time, with the ammo it
		/// carries for them and a zombie viking's skill and MeleeScale, or those its prefab gives it, remembered for each
		/// prefab
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>Its hardest hit at one star, and its farthest reach hand to hand past its own size</returns>
		private static KeyValuePair<float, float> Read(Character monster)
		{
			List<ItemDrop.ItemData> carried = Carried(monster);
			if (carried.Count > 0)
			{
				return(Weigh(Swings(carried, monster)));
			}
			string name = Utils.GetPrefabName(monster.gameObject);
			if (known.TryGetValue(name, out KeyValuePair<float, float> found))
			{
				return(found);
			}
			KeyValuePair<float, float> read = Weigh(Swings(Given(monster), null));
			known[name] = read;
			return(read);
		}

		/// <summary>
		/// Lists what each of a monster's weapons does when it attacks with it - a bow or crossbow with the best ammo the
		/// monster carries for it, and a zombie viking's attacks as it deals them (see VikingHit)
		/// </summary>
		/// <param name="weapons">The weapons</param>
		/// <param name="carrier">The monster carrying them, or null for weapons its prefab gives it, which are weighed
		/// alone</param>
		/// <returns>Their attacks, leaving out weapons with no attack</returns>
		private static List<Swing> Swings(List<ItemDrop.ItemData> weapons, Character carrier)
		{
			List<Swing> swings = new List<Swing>();
			bool viking = (carrier != null && VikingCore.IsZombieViking(carrier));
			float meleeScale = (viking ? ZombieMeleeScale(carrier) : 1f);
			foreach (ItemDrop.ItemData weapon in weapons)
			{
				Attack attack = weapon.m_shared.m_attack;
				if (attack == null)
				{
					continue;
				}
				bool projectile = (attack.m_attackType == Attack.AttackType.Projectile);
				float hit = ShotHit(weapon.GetDamage(), AmmoFor(weapon, carrier), attack.m_damageMultiplier);
				if (viking)
				{
					hit = VikingHit(hit, projectile, meleeScale, carrier.GetSkillFactor(weapon.m_shared.m_skillType));
				}
				swings.Add(new Swing
				{
					Hit = hit,
					Range = Mathf.Max(weapon.m_shared.m_aiAttackRange, attack.m_attackRange),
					Projectile = projectile
				});
			}
			return(swings);
		}

		/// <summary>
		/// Lists the damage of each piece of ammo a monster carries that a weapon shoots
		/// </summary>
		/// <param name="weapon">The weapon</param>
		/// <param name="carrier">The monster, or null</param>
		/// <returns>The ammo's damage, none when the weapon takes no ammo or the monster carries none for it</returns>
		private static List<HitData.DamageTypes> AmmoFor(ItemDrop.ItemData weapon, Character carrier)
		{
			List<HitData.DamageTypes> ammo = new List<HitData.DamageTypes>();
			Humanoid humanoid = carrier as Humanoid;
			Inventory inventory = ((humanoid != null) ? humanoid.GetInventory() : null);
			string ammoType = weapon.m_shared.m_ammoType;
			if (inventory == null || string.IsNullOrEmpty(ammoType))
			{
				return(ammo);
			}
			foreach (ItemDrop.ItemData item in inventory.GetAllItems())
			{
				ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
				if ((type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable) && item.m_shared.m_ammoType == ammoType)
				{
					ammo.Add(item.GetDamage());
				}
			}
			return(ammo);
		}

		/// <summary>
		/// Lists the weapons a monster carries in its inventory, leaving out those that only hurt tamed creatures, like a
		/// butcher knife a zombie viking carries
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>The weapons, none when it isn't a humanoid</returns>
		private static List<ItemDrop.ItemData> Carried(Character monster)
		{
			Humanoid humanoid = monster as Humanoid;
			Inventory inventory = ((humanoid != null) ? humanoid.GetInventory() : null);
			if (inventory == null)
			{
				return(new List<ItemDrop.ItemData>());
			}
			return(inventory.GetAllItems().FindAll((ItemDrop.ItemData item) => item.IsWeapon() && !item.m_shared.m_tamedOnly));
		}

		/// <summary>
		/// Lists the weapons a monster's prefab gives it when it spawns
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>The weapons, none when it isn't a humanoid</returns>
		private static List<ItemDrop.ItemData> Given(Character monster)
		{
			List<ItemDrop.ItemData> weapons = new List<ItemDrop.ItemData>();
			Humanoid humanoid = monster as Humanoid;
			if (humanoid == null)
			{
				return(weapons);
			}
			List<GameObject> given = new List<GameObject>();
			if (humanoid.m_defaultItems != null)
			{
				given.AddRange(humanoid.m_defaultItems);
			}
			if (humanoid.m_randomWeapon != null)
			{
				given.AddRange(humanoid.m_randomWeapon);
			}
			foreach (GameObject prefab in given)
			{
				ItemDrop drop = ((prefab != null) ? prefab.GetComponent<ItemDrop>() : null);
				if (drop != null && drop.m_itemData.IsWeapon())
				{
					weapons.Add(drop.m_itemData);
				}
			}
			return(weapons);
		}
	}
}
