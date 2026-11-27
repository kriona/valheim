using System.Collections.Generic;
using Splatform;

namespace Kriona.Vikings
{
	/// <summary>
	/// A PC or dvergr viking's name and the players it knows, kept in its ZDO under keys that never change - so its
	/// zombie carries them when it rises (see VikingCore.HandToCorpse and HandOver), and shows its name to the players
	/// the viking knew, and is plain Zombie Viking to the rest
	/// </summary>
	internal static class VikingName
	{
		/// <summary>
		/// The name a player gave the viking and who gave it, by platform user ID or "host", and the players it knows -
		/// its count, then each player's ID, the one it met longest ago first
		/// </summary>
		public static readonly int NameKey = "kriona.VikingVillages.vikingName".GetStableHashCode();
		public static readonly int AuthorKey = "kriona.VikingVillages.vikingNameAuthor".GetStableHashCode();
		public static readonly int KnownKey = "kriona.VikingVillages.vikingKnown".GetStableHashCode();

		/// <summary>
		/// Takes the "$" out of a name, which the game's localization would read as a key, and trims it
		/// </summary>
		/// <param name="name">The name</param>
		/// <returns>The name without them</returns>
		public static string Plain(string name)
		{
			return(name.Replace("$", "").Trim());
		}

		/// <summary>
		/// Filters a name for its author's words the way a tamed creature's is
		/// </summary>
		/// <param name="name">The name</param>
		/// <param name="author">Who gave it, by platform user ID or "host"</param>
		/// <returns>The name, filtered, or ""</returns>
		public static string Filter(string name, string author)
		{
			if (name.Length == 0)
			{
				return("");
			}
			if (string.IsNullOrEmpty(author) || author == "host")
			{
				return(CensorShittyWords.FilterUGC(name, UGCType.Text, default, 0L));
			}
			return(CensorShittyWords.FilterUGC(name, UGCType.Text, new PlatformUserID(author), 0L));
		}

		/// <summary>
		/// Gets a viking's name from its ZDO as it's shown - filtered for its author's words and without rich text
		/// </summary>
		/// <param name="zdo">The viking's ZDO</param>
		/// <returns>The name, or "" when it has none</returns>
		public static string Shown(ZDO zdo)
		{
			return(Filter(Plain(zdo.GetString(NameKey)), zdo.GetString(AuthorKey)).RemoveRichTextTags().Trim());
		}

		/// <summary>
		/// Gets a viking's name as a player may see it - only once the viking knows them
		/// </summary>
		/// <param name="zdo">The viking's ZDO</param>
		/// <param name="playerId">The player's ID</param>
		/// <returns>The name, or "" when it has none or doesn't know them</returns>
		public static string ShownTo(ZDO zdo, long playerId)
		{
			return(ReadKnown(zdo.GetByteArray(KnownKey)).Contains(playerId) ? Shown(zdo) : "");
		}

		/// <summary>
		/// Reads the players a viking knows
		/// </summary>
		/// <param name="bytes">What's under KnownKey, or null</param>
		/// <returns>Their IDs, the one it met longest ago first</returns>
		internal static List<long> ReadKnown(byte[] bytes)
		{
			List<long> known = new List<long>();
			if (bytes == null)
			{
				return(known);
			}
			ZPackage pkg = new ZPackage(bytes);
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				known.Add(pkg.ReadLong());
			}
			return(known);
		}

		/// <summary>
		/// Writes the players a viking knows, for KnownKey
		/// </summary>
		/// <param name="known">Their IDs, the one it met longest ago first</param>
		/// <returns>The bytes</returns>
		internal static byte[] WriteKnown(List<long> known)
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(known.Count);
			foreach (long id in known)
			{
				pkg.Write(id);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Copies a viking's name, who gave it and the players it knows from one ZDO to another - a dying viking's to its
		/// corpse, and the corpse's to the zombie rising from it
		/// </summary>
		/// <param name="from">The ZDO they're on</param>
		/// <param name="to">The ZDO they go to, owned by this client</param>
		public static void Carry(ZDO from, ZDO to)
		{
			string name = from.GetString(NameKey);
			if (name.Length > 0)
			{
				to.Set(NameKey, name);
				to.Set(AuthorKey, from.GetString(AuthorKey));
			}
			byte[] known = from.GetByteArray(KnownKey);
			if (known != null)
			{
				to.Set(KnownKey, known);
			}
		}
	}
}
