using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Who gave the local player a forsaken power, so its message names them - the game shows every player a power
	/// reaches its own start message, which for Eikthyr's says "You activate the power of Eikthyr" even when another
	/// player used it. Whoever uses a power - a player (see VikingSharedPowerPatch) or a living or dvergr viking (see
	/// VikingPowers.Use) - announces it to every client before the power goes out, and a power reaching the local
	/// player from someone else shows who activated it in place of its own message (see PowerGiverMessagePatch)
	/// </summary>
	internal static class PowerGivers
	{
		/// <summary>
		/// The routed RPC that announces a power about to be used, with who uses it, the power's status effect name hash
		/// and where they stand
		/// </summary>
		public const string NoticeRpc = Plugin.PluginGuid + ".PowerGiver";

		/// <summary>
		/// How near where a power is used the local player has to be for its notice to be kept, in meters - farther than
		/// the power's own reach of 10, since a viking's power goes out a second after it's announced and either of them
		/// may have moved
		/// </summary>
		private const float NoticeReach = 20f;

		/// <summary>
		/// How long a notice stands, in seconds - a power that reaches the local player later than that names no one
		/// </summary>
		private const float NoticeTime = 5f;

		/// <summary>
		/// Who last announced each power near the local player, and when, by the power's status effect name hash
		/// </summary>
		private static readonly Dictionary<int, Notice> notices = new Dictionary<int, Notice>();

		/// <summary>
		/// A power announced near the local player - who used it, and when the notice came, in Time.time seconds
		/// </summary>
		private class Notice
		{
			public ZDOID Giver;
			public float At;
		}

		/// <summary>
		/// Registers the RPC with a world's routed RPC
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register<ZDOID, int, Vector3>(NoticeRpc, OnNotice);
			notices.Clear();
		}

		/// <summary>
		/// Announces to every client, this one included, that a character is about to use a power - before the power
		/// goes out, so the notice comes first
		/// </summary>
		/// <param name="giver">Who uses it</param>
		/// <param name="power">The power's status effect name hash</param>
		public static void Announce(Character giver, int power)
		{
			if (ZRoutedRpc.instance == null || giver == null || giver.m_nview == null || !giver.m_nview.IsValid())
			{
				return;
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, NoticeRpc, giver.GetZDOID(), power, giver.transform.position);
		}

		/// <summary>
		/// Keeps a power's notice when the local player is near where it's used
		/// </summary>
		/// <param name="sender">The client that announced it</param>
		/// <param name="giver">Who uses it</param>
		/// <param name="power">The power's status effect name hash</param>
		/// <param name="at">Where they stand</param>
		private static void OnNotice(long sender, ZDOID giver, int power, Vector3 at)
		{
			Player player = Player.m_localPlayer;
			if (player == null || Vector3.Distance(player.transform.position, at) > NoticeReach)
			{
				return;
			}
			Notice notice = new Notice();
			notice.Giver = giver;
			notice.At = Time.time;
			notices[power] = notice;
		}

		/// <summary>
		/// Gets the message a power shows as it reaches a character - who activated it, when it's the local player and
		/// someone else used it within NoticeTime, using up the notice
		/// </summary>
		/// <param name="effect">The power's status effect, as it's set up</param>
		/// <param name="character">Who it reaches</param>
		/// <returns>The message, or null to show the power's own</returns>
		public static string MessageFor(StatusEffect effect, Character character)
		{
			if (character == null || character != Player.m_localPlayer)
			{
				return(null);
			}
			int power = effect.NameHash();
			if (!notices.TryGetValue(power, out Notice notice))
			{
				return(null);
			}
			notices.Remove(power);
			if (Time.time - notice.At > NoticeTime || notice.Giver == character.GetZDOID())
			{
				return(null);
			}
			return(Line(GiverName(notice.Giver), Localization.instance.Localize(effect.m_name)));
		}

		/// <summary>
		/// Gets who used a power, as the local player knows them - a player by their name, a viking by the name it goes by
		/// once it knows them, or else "A Viking", and anyone else by their kind
		/// </summary>
		/// <param name="giver">Who used it</param>
		/// <returns>The name</returns>
		private static string GiverName(ZDOID giver)
		{
			GameObject found = ((ZNetScene.instance != null) ? ZNetScene.instance.FindInstance(giver) : null);
			Character character = ((found != null) ? found.GetComponent<Character>() : null);
			if (character is Player player)
			{
				return(player.GetPlayerName());
			}
			if (character is LivingVikingBody viking)
			{
				string name = (viking.Stranger() ? "" : viking.ShownName());
				return((name.Length > 0) ? name : "A Viking");
			}
			if (character != null)
			{
				return(Localization.instance.Localize(character.m_name));
			}
			return("Someone");
		}

		/// <summary>
		/// Makes the message naming who activated a power, the way the game words Eikthyr's - "Astrid activates the power
		/// of the Elder" - with a power's leading "The" in lowercase, since it comes mid-sentence
		/// </summary>
		/// <param name="giver">Who activated it</param>
		/// <param name="power">The power's name, like "Eikthyr" or "The Elder"</param>
		/// <returns>The message</returns>
		internal static string Line(string giver, string power)
		{
			if (power.StartsWith("The "))
			{
				power = "the " + power.Substring(4);
			}
			return(giver + " activates the power of " + power);
		}
	}
}
