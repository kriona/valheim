namespace Kriona.Vikings
{
	/// <summary>
	/// What Zombie Apocalypse marks on a zombie, and on a corpse that's going to rise, in their ZDOs - read by both mods,
	/// so a living viking can tell a zombie from the creature it was and keep watch over a corpse about to get up
	/// </summary>
	/// <remarks>
	/// A zombie is the creature's own prefab marked with ZombieKey, so it's turned into a zombie again whenever it
	/// loads, on every client. A corpse that's going to rise is the creature's ragdoll marked with RiseAtKey and the
	/// other corpse keys, so it keeps waiting through a reload or a change of owner. The keys can't change, since
	/// Zombie Apocalypse writes them and Viking Villages reads them, whichever versions of the two are installed
	/// </remarks>
	internal static class ZombieState
	{
		/// <summary>
		/// On a creature, that it's a zombie, and on a corpse, that it's a zombie's corpse
		/// </summary>
		public static readonly int ZombieKey = "kriona.ZombieApocalypse.zombie".GetStableHashCode();

		/// <summary>
		/// When a zombie starts getting up, in ZNet time ticks
		/// </summary>
		public static readonly int RiseStartKey = "kriona.ZombieApocalypse.riseStart".GetStableHashCode();

		/// <summary>
		/// When a zombie is standing and its AI takes over, in ZNet time ticks
		/// </summary>
		public static readonly int RiseEndKey = "kriona.ZombieApocalypse.riseEnd".GetStableHashCode();

		/// <summary>
		/// The share of its max health a zombie rises with, until its owner first sets its health - 0 once it's set
		/// </summary>
		public static readonly int RiseHealthKey = "kriona.ZombieApocalypse.riseHealth".GetStableHashCode();

		/// <summary>
		/// When a corpse's creature died, in ZNet time ticks
		/// </summary>
		public static readonly int DiedAtKey = "kriona.ZombieApocalypse.diedAt".GetStableHashCode();

		/// <summary>
		/// When a corpse starts getting up, in ZNet time ticks
		/// </summary>
		public static readonly int RiseAtKey = "kriona.ZombieApocalypse.riseAt".GetStableHashCode();

		/// <summary>
		/// The prefab of a corpse's creature
		/// </summary>
		public static readonly int RisePrefabKey = "kriona.ZombieApocalypse.risePrefab".GetStableHashCode();

		/// <summary>
		/// The seed a corpse's creature picked its random items with, so its zombie picks the same ones
		/// </summary>
		public static readonly int RiseSeedKey = "kriona.ZombieApocalypse.riseSeed".GetStableHashCode();

		/// <summary>
		/// When a risen corpse is removed, in ZNet time ticks
		/// </summary>
		public static readonly int RemoveAtKey = "kriona.ZombieApocalypse.removeAt".GetStableHashCode();

		/// <summary>
		/// The zombie a corpse rose as - a ZDOID, which is stored under a pair of keys made from this name
		/// </summary>
		public const string ZombieIdKey = "kriona.ZombieApocalypse.zombieId";

		/// <summary>
		/// On a corpse and its zombie, who dealt the creature's killing blow - a ZDOID, stored like ZombieIdKey
		/// </summary>
		public const string KillerIdKey = "kriona.ZombieApocalypse.killerId";

		/// <summary>
		/// Checks whether a creature is a zombie
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True when it's a zombie</returns>
		public static bool IsZombie(Character character)
		{
			ZNetView nview = character.m_nview;
			return(nview != null && nview.IsValid() && nview.GetZDO().GetBool(ZombieKey));
		}

		/// <summary>
		/// Checks whether a creature is a zombie that's still getting up
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True until the zombie is standing</returns>
		public static bool IsRising(Character character)
		{
			return(IsZombie(character) && NetTime.Now() < character.m_nview.GetZDO().GetLong(RiseEndKey));
		}

		/// <summary>
		/// Sets a creature's health on its owner after its max health - part of it the first time for a zombie, as it
		/// rises, and all of it when it's unhurt
		/// </summary>
		/// <param name="character">The creature</param>
		/// <param name="maxHealth">Its max health</param>
		/// <param name="unhurt">Whether it was unhurt before its max health was set</param>
		public static void SetHealth(Character character, float maxHealth, bool unhurt)
		{
			ZDO zdo = character.m_nview.GetZDO();
			float share = zdo.GetFloat(RiseHealthKey);
			if (share > 0f)
			{
				zdo.Set(RiseHealthKey, 0f);
				character.SetHealth(maxHealth * share);
			}
			else if (unhurt)
			{
				character.SetHealth(maxHealth);
			}
		}
	}
}
