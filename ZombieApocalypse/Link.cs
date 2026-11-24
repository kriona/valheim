using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// What Zombie Apocalypse tells Viking Villages about the world's zombies (see ZombieLink there) - every answer is
	/// what's true in the world being played, so while the mod isn't running in it, the slain never rise and nothing
	/// here is true
	/// </summary>
	/// <remarks>
	/// Viking Villages finds these methods by name and parameters, so each is kept for as long as the mod is released -
	/// a new version can add methods, but never remove one or change its name, parameters or return type, since an older
	/// Viking Villages still calls it
	/// </remarks>
	public static class Link
	{
		/// <summary>
		/// Checks whether the mod is running in the world being played - the world's server has it too
		/// </summary>
		/// <returns>True when it is</returns>
		public static bool Active()
		{
			return(ServerCheck.Active);
		}

		/// <summary>
		/// Gets the percent chance that a slain creature rises as a zombie
		/// </summary>
		/// <returns>The chance, or 0 while the mod isn't running</returns>
		public static int RiseChance()
		{
			return(Active() ? Plugin.RiseChance.Value : 0);
		}

		/// <summary>
		/// Gets the share of the damage that killed a creature that has to be fire for it to stay dead, as a percent
		/// </summary>
		/// <returns>The percent, or 0 while the mod isn't running</returns>
		public static int FireStaysDead()
		{
			return(Active() ? Plugin.FireStaysDead.Value : 0);
		}

		/// <summary>
		/// Checks whether animals that only flee, like deer, rise
		/// </summary>
		/// <returns>True when they do</returns>
		public static bool AnimalsRise()
		{
			return(Active() && Plugin.AnimalsRise.Value);
		}

		/// <summary>
		/// Checks whether zombie vikings raid players' bases
		/// </summary>
		/// <returns>True when they do</returns>
		public static bool VikingRaids()
		{
			return(Active() && Plugin.VikingRaids.Value);
		}

		/// <summary>
		/// Checks whether a player a zombie kills gets back up as a zombie viking
		/// </summary>
		/// <returns>True when they do</returns>
		public static bool TombstoneZombies()
		{
			return(Active() && Plugin.TombstoneZombies.Value);
		}

		/// <summary>
		/// Gets the word put in front of a zombie's name
		/// </summary>
		/// <returns>The word, or "" for none</returns>
		public static string ZombieName()
		{
			return(Plugin.ZombieName.Value ?? "");
		}

		/// <summary>
		/// Checks whether a creature dying on this client rises as a zombie, once DeathPatch has decided - from a prefix
		/// of Character.OnDeath at a lower priority than DeathPatch's
		/// </summary>
		/// <param name="dying">The creature</param>
		/// <returns>True when it rises</returns>
		public static bool Rises(Character dying)
		{
			return(VikingCore.Rises(dying));
		}

		/// <summary>
		/// Adds where each of the poison clouds zombies' corpses leave is to a list
		/// </summary>
		/// <param name="clouds">The list</param>
		public static void PoisonClouds(List<Vector3> clouds)
		{
			foreach (PoisonCloudFade cloud in PoisonCloudFade.Clouds)
			{
				if (cloud != null)
				{
					clouds.Add(cloud.transform.position);
				}
			}
		}

		/// <summary>
		/// Checks whether a zombie's corpse at a spot might leave a poison cloud as it rots away
		/// </summary>
		/// <param name="position">The spot</param>
		/// <returns>True when it might</returns>
		public static bool CanLeaveCloud(Vector3 position)
		{
			return(Active() && PoisonCloud.CanLeaveCloud(position));
		}

		/// <summary>
		/// Gets the recipe that lines a weapon or shield with silver at a forge (see SilverLining.GetRecipe)
		/// </summary>
		/// <param name="item">The weapon or shield</param>
		/// <returns>The recipe, or null when the item can't be lined or the mod isn't running</returns>
		public static Recipe LiningRecipe(ItemDrop.ItemData item)
		{
			return(Active() ? SilverLining.GetRecipe(item) : null);
		}

		/// <summary>
		/// Lines a weapon or shield with silver, when it can be (see LiningRecipe), without taking anything for it
		/// </summary>
		/// <param name="item">The weapon or shield</param>
		/// <returns>True when it was lined</returns>
		public static bool Line(ItemDrop.ItemData item)
		{
			if (LiningRecipe(item) == null)
			{
				return(false);
			}
			SilverLining.Line(item);
			return(true);
		}

		/// <summary>
		/// Works out how a zombie of a creature takes damage (see Zombies.UndeadModifiers)
		/// </summary>
		/// <param name="modifiers">The creature's resistances</param>
		/// <returns>The zombie's, or the creature's own when zombies don't take damage like the undead</returns>
		public static HitData.DamageModifiers UndeadModifiers(HitData.DamageModifiers modifiers)
		{
			return(Plugin.UndeadWeaknesses.Value ? Zombies.UndeadModifiers(modifiers) : modifiers);
		}
	}
}
