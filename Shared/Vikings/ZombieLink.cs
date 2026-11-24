using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// What Viking Villages asks Zombie Apocalypse about the world's zombies, through Zombie Apocalypse's Link class -
	/// every answer is what's true in the world being played, so with the mod missing, or running on a client whose
	/// server doesn't have it, the slain never rise and nothing here is true
	/// </summary>
	/// <remarks>
	/// Each method names a Link method that Zombie Apocalypse keeps for as long as it's released (see ModLink), and
	/// answers its fallback when the installed version doesn't have it
	/// </remarks>
	internal static class ZombieLink
	{
		/// <summary>
		/// Zombie Apocalypse's GUID, which its plugin is found by
		/// </summary>
		public const string Guid = "kriona.ZombieApocalypse";

		private static readonly ModLink link = new ModLink(Guid, "ZombieApocalypse.Link");

		/// <summary>
		/// Gets the percent chance that a slain creature rises as a zombie in the world being played
		/// </summary>
		/// <returns>The chance, or 0 without Zombie Apocalypse</returns>
		public static int RiseChance()
		{
			Func<int> chance = link.Get<Func<int>>("RiseChance");
			return((chance != null) ? chance() : 0);
		}

		/// <summary>
		/// Gets the share of the damage that killed a creature that has to be fire for it to stay dead, as a percent
		/// </summary>
		/// <returns>The percent, or 0 when fire keeps nothing dead, as without Zombie Apocalypse</returns>
		public static int FireStaysDead()
		{
			Func<int> share = link.Get<Func<int>>("FireStaysDead");
			return((share != null) ? share() : 0);
		}

		/// <summary>
		/// Checks whether animals that only flee, like deer, rise in the world being played
		/// </summary>
		/// <returns>True when they do</returns>
		public static bool AnimalsRise()
		{
			Func<bool> rise = link.Get<Func<bool>>("AnimalsRise");
			return(rise != null && rise());
		}

		/// <summary>
		/// Checks whether zombie vikings raid players' bases in the world being played
		/// </summary>
		/// <returns>True when they do</returns>
		public static bool VikingRaids()
		{
			Func<bool> raids = link.Get<Func<bool>>("VikingRaids");
			return(raids != null && raids());
		}

		/// <summary>
		/// Checks whether a player a zombie kills gets back up as a zombie viking in the world being played
		/// </summary>
		/// <returns>True when they do</returns>
		public static bool TombstoneZombies()
		{
			Func<bool> zombies = link.Get<Func<bool>>("TombstoneZombies");
			return(zombies != null && zombies());
		}

		/// <summary>
		/// Gets the word put in front of a zombie's name
		/// </summary>
		/// <returns>The word, or "" when there's none, as without Zombie Apocalypse</returns>
		public static string ZombieName()
		{
			Func<string> name = link.Get<Func<string>>("ZombieName");
			return(((name != null) ? name() : null) ?? "");
		}

		/// <summary>
		/// Checks whether a creature dying on this client rises as a zombie, once Zombie Apocalypse has decided - from a
		/// prefix of Character.OnDeath that runs after Zombie Apocalypse's own
		/// </summary>
		/// <param name="dying">The creature</param>
		/// <returns>True when it rises</returns>
		public static bool Rises(Character dying)
		{
			Func<Character, bool> rises = link.Get<Func<Character, bool>>("Rises");
			return(rises != null && rises(dying));
		}

		/// <summary>
		/// Adds where each of the poison clouds zombies' corpses leave is to a list
		/// </summary>
		/// <param name="clouds">The list</param>
		public static void PoisonClouds(List<Vector3> clouds)
		{
			Action<List<Vector3>> find = link.Get<Action<List<Vector3>>>("PoisonClouds");
			if (find != null)
			{
				find(clouds);
			}
		}

		/// <summary>
		/// Checks whether a zombie's corpse at a spot might leave a poison cloud as it rots away
		/// </summary>
		/// <param name="position">The spot</param>
		/// <returns>True when it might</returns>
		public static bool CanLeaveCloud(Vector3 position)
		{
			Func<Vector3, bool> can = link.Get<Func<Vector3, bool>>("CanLeaveCloud");
			return(can != null && can(position));
		}

		/// <summary>
		/// Checks whether Zombie Apocalypse is running in the world being played
		/// </summary>
		/// <returns>True when it is</returns>
		public static bool Active()
		{
			Func<bool> active = link.Get<Func<bool>>("Active");
			return(active != null && active());
		}

		/// <summary>
		/// Gets the recipe that lines a weapon with silver at a forge, which takes silver and ectoplasm
		/// </summary>
		/// <param name="item">The weapon</param>
		/// <returns>The recipe, or null when the weapon can't be lined</returns>
		public static Recipe LiningRecipe(ItemDrop.ItemData item)
		{
			Func<ItemDrop.ItemData, Recipe> recipe = link.Get<Func<ItemDrop.ItemData, Recipe>>("LiningRecipe");
			return((recipe != null) ? recipe(item) : null);
		}

		/// <summary>
		/// Lines a weapon with silver, when it can be (see LiningRecipe), without taking anything for it
		/// </summary>
		/// <param name="item">The weapon</param>
		/// <returns>True when it was lined</returns>
		public static bool Line(ItemDrop.ItemData item)
		{
			Func<ItemDrop.ItemData, bool> line = link.Get<Func<ItemDrop.ItemData, bool>>("Line");
			return(line != null && line(item));
		}

		/// <summary>
		/// Works out how a zombie of a creature takes damage
		/// </summary>
		/// <param name="modifiers">The creature's resistances</param>
		/// <returns>The zombie's, or the creature's own without Zombie Apocalypse</returns>
		public static HitData.DamageModifiers UndeadModifiers(HitData.DamageModifiers modifiers)
		{
			Func<HitData.DamageModifiers, HitData.DamageModifiers> undead = link.Get<Func<HitData.DamageModifiers, HitData.DamageModifiers>>("UndeadModifiers");
			return((undead != null) ? undead(modifiers) : modifiers);
		}
	}
}
