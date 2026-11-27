using System.Collections.Generic;
using Kriona.Vikings;

namespace VikingVillages
{
	/// <summary>
	/// Notes when a PC viking joins a village, and the talks the village's vikings have about it for a game day
	/// after - welcoming it, or talking about it with each other, and about the village its group came from when it came
	/// from one a player disbanded
	/// </summary>
	/// <remarks>
	/// Every viking that joins a village after it has had the vikings it starts with is a newcomer - one born there in
	/// the place of one that died, one a bed brought, one coming back to it, a stray taken in, or one arriving from the
	/// road. "{newcomer}" in a talk is the newcomer's name and "{from}" the village its group came from (see
	/// VikingSpeech.Address)
	/// </remarks>
	internal static class VillageNewcomers
	{
		/// <summary>
		/// On a PC viking, when it joined its village, in ZNet time ticks, and the name of the village its group came
		/// from, or ""
		/// </summary>
		public static readonly int JoinedAtKey = "kriona.VikingVillages.vikingJoinedAt".GetStableHashCode();
		public static readonly int JoinedFromKey = "kriona.VikingVillages.vikingJoinedFrom".GetStableHashCode();

		/// <summary>
		/// The tokens a newcomer's name and the village its group came from are filled in at
		/// </summary>
		internal const string NewcomerToken = "{newcomer}";
		internal const string FromToken = "{from}";

		/// <summary>
		/// How many game days a viking is talked about as new after it joins
		/// </summary>
		private const float NewDays = 1f;

		/// <summary>
		/// How likely a viking that starts a talk with a newcomer welcomes it, and one that starts a talk with another
		/// viking talks about a newcomer, rather than about anything else
		/// </summary>
		internal const float WelcomeChance = 0.6f;
		internal const float AboutChance = 0.4f;

		/// <summary>
		/// The talks a viking has with a newcomer, welcoming it - "{name}" is the newcomer
		/// </summary>
		internal static readonly string[][] WelcomeTalks =
		{
			new[] { "Welcome to {village}, {name}.", "Thank you. It's good to have a roof again." },
			new[] { "You're new here, aren't you, {name}?", "Just arrived. Still finding my way around." },
			new[] { "Glad you found us, {name}.", "So am I. The road was long." },
			new[] { "If you need anything, {name}, just ask.", "A warm seat by the fire would do me fine.", "That we have plenty of." }
		};

		/// <summary>
		/// The talks a viking has with a newcomer whose group came from a village a player disbanded
		/// </summary>
		internal static readonly string[][] WelcomeFromTalks =
		{
			new[] { "I heard {from} broke up, {name}. I'm sorry.", "We had good years there. Maybe we'll have good years here too." },
			new[] { "What was {from} like, {name}?", "Smaller than this. But it was home." },
			new[] { "Welcome, {name}. Any friend from {from} is welcome here.", "Thank you. We'll pull our weight." }
		};

		/// <summary>
		/// The talks two vikings have about a newcomer - "{newcomer}" is the newcomer
		/// </summary>
		internal static readonly string[][] AboutTalks =
		{
			new[] { "Have you met {newcomer} yet?", "Briefly. Seems decent enough." },
			new[] { "{newcomer} turned up the other day.", "Every new face is another sword at the fence." },
			new[] { "Where did {newcomer} come from?", "Out of the wilds, by the look of it.", "Then they'll know how to handle themselves." },
			new[] { "{newcomer} is settling into {village} well.", "Better than I did when I first came." }
		};

		/// <summary>
		/// The talks two vikings have about a newcomer whose group came from a village a player disbanded
		/// </summary>
		internal static readonly string[][] AboutFromTalks =
		{
			new[] { "{newcomer} came all the way from {from}.", "I heard {from} is gone now.", "Then we'll make room here." },
			new[] { "The folk from {from} have settled in well, {name}.", "{newcomer} most of all." },
			new[] { "Did {newcomer} say why {from} broke up?", "No, and I didn't ask. Let them be." }
		};

		/// <summary>
		/// Notes a viking as having just joined a village
		/// </summary>
		/// <param name="viking">The viking's ZDO, owned by this client</param>
		/// <param name="village">The name of the village it joined, or "", so the group's old village isn't the one it's in
		/// now</param>
		public static void NoteJoined(ZDO viking, string village)
		{
			string from = viking.GetString(VillageTravel.FromNameKey, "");
			if (string.Equals(from, village, System.StringComparison.OrdinalIgnoreCase))
			{
				from = "";
			}
			viking.Set(JoinedAtKey, NetTime.Now());
			// Set only when there's a village to name or one to forget, so a viking that never left one has no value
			if (from.Length > 0 || From(viking).Length > 0)
			{
				viking.Set(JoinedFromKey, from);
			}
			if (viking.GetString(VillageTravel.FromNameKey, "").Length > 0)
			{
				viking.Set(VillageTravel.FromNameKey, "");
			}
		}

		/// <summary>
		/// Checks whether a viking is still new to its village
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>True when it joined within NewDays game days</returns>
		public static bool IsNew(ZDO viking)
		{
			if (viking == null || EnvMan.instance == null)
			{
				return(false);
			}
			return(IsNew(viking.GetLong(JoinedAtKey, 0L), NetTime.Now(), NetTime.Ticks(EnvMan.instance.m_dayLengthSec * NewDays)));
		}

		/// <summary>
		/// Checks whether a viking that joined at a time is still new
		/// </summary>
		/// <param name="joinedAt">When it joined, in ZNet time ticks, or 0 when it never has</param>
		/// <param name="now">The time now, in ticks</param>
		/// <param name="window">How long it stays new, in ticks</param>
		/// <returns>True when it is</returns>
		internal static bool IsNew(long joinedAt, long now, long window)
		{
			return(joinedAt > 0L && now >= joinedAt && now - joinedAt < window);
		}

		/// <summary>
		/// Gets the village a newcomer's group came from
		/// </summary>
		/// <param name="viking">The newcomer's ZDO</param>
		/// <returns>Its name, or ""</returns>
		public static string From(ZDO viking)
		{
			return((viking != null) ? viking.GetString(JoinedFromKey, "") : "");
		}

		/// <summary>
		/// Lists the talks there are about a newcomer - only those about the village its group came from when there's one,
		/// so it's always mentioned, and leaving out those naming the village it joined when that has no name
		/// </summary>
		/// <param name="plain">The talks for any newcomer</param>
		/// <param name="withFrom">The talks about the village its group came from</param>
		/// <param name="from">Whether there's such a village</param>
		/// <param name="named">Whether the village it joined has a name</param>
		/// <returns>The talks</returns>
		internal static List<string[]> Candidates(string[][] plain, string[][] withFrom, bool from, bool named)
		{
			List<string[]> talks = new List<string[]>(from ? withFrom : plain);
			return(talks.FindAll((string[] talk) => named || !System.Array.Exists(talk, (string line) => line.Contains("{village}"))));
		}
	}
}
