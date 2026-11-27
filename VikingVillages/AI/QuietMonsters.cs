using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// The monsters PC vikings leave alone until they hurt someone - leeches, which lie in the water and only come
	/// after whoever wades in, so going after one would only take a viking into the Swamp's water
	/// </summary>
	/// <remarks>
	/// A leech counts as having hurt someone once it hurts a player or a PC viking, from the call for help every
	/// client hears (see VillageSignals.OnHurt), and from then on for as long as it's loaded. A hunter after one as
	/// its prey goes after it anyway, and a viking it attacks fights back, since the AI takes on its attacker
	/// </remarks>
	internal static class QuietMonsters
	{
		/// <summary>
		/// The prefab names of the monsters left alone
		/// </summary>
		private static readonly HashSet<string> Quiet = new HashSet<string> { "Leech", "Leech_cave" };

		/// <summary>
		/// The monsters left alone that have hurt someone, by ZDO
		/// </summary>
		private static readonly HashSet<ZDOID> provoked = new HashSet<ZDOID>();

		/// <summary>
		/// Checks whether a kind of monster is left alone until it hurts someone
		/// </summary>
		/// <param name="prefab">The monster's prefab name</param>
		/// <returns>True when it is</returns>
		internal static bool IsQuiet(string prefab)
		{
			return(Quiet.Contains(prefab));
		}

		/// <summary>
		/// Checks whether PC vikings leave a monster alone - one of the kinds left alone that hasn't hurt anyone
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>True when they do</returns>
		public static bool LeftAlone(Character monster)
		{
			if (monster == null || !IsQuiet(Utils.GetPrefabName(monster.gameObject)))
			{
				return(false);
			}
			return(!provoked.Contains(monster.GetZDOID()));
		}

		/// <summary>
		/// Notes that a monster has hurt someone, so it's no longer left alone
		/// </summary>
		/// <param name="monster">The monster</param>
		public static void Provoked(Character monster)
		{
			if (monster != null && IsQuiet(Utils.GetPrefabName(monster.gameObject)))
			{
				provoked.Add(monster.GetZDOID());
			}
		}

		/// <summary>
		/// Forgets every monster that has hurt someone, as a world loads
		/// </summary>
		public static void Clear()
		{
			provoked.Clear();
		}
	}
}
