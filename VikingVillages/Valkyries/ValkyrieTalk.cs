using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A valkyrie's name and her few words - she's given one of the valkyries' names as she's spawned, keeps it to
	/// herself until a player talks to her, and then tells them it, so they know her by it from then on. She never
	/// speaks first, and says little (see ValkyrieLines)
	/// </summary>
	/// <remarks>
	/// Her name and the players who know it are in her ZDO, so every client shows her name to the players who know it.
	/// Only the client that owns her writes it, so a player talking to her on another client asks that one to note them
	/// </remarks>
	internal class ValkyrieTalk : MonoBehaviour
	{
		/// <summary>
		/// The ZDO keys holding her name and the players who know it
		/// </summary>
		private static readonly int NameKey = "kri_vv_valkyrie_name".GetStableHashCode();
		private static readonly int KnownKey = "kri_vv_valkyrie_known".GetStableHashCode();

		/// <summary>
		/// The RPC a player who's talked to her sends the client that owns her, to be noted as knowing her name
		/// </summary>
		private const string MeetRPC = "kri_vv_valkyrie_meet";

		/// <summary>
		/// The most players she remembers telling her name to, forgetting the one she told longest ago past it
		/// </summary>
		private const int KnownMax = 32;

		/// <summary>
		/// The least time between two of her answers, in seconds
		/// </summary>
		private const float TalkGap = 1f;

		private Character character;
		private ZNetView nview;
		private float lastTalk = -100f;

		/// <summary>
		/// Finds her parts, listens for players who've talked to her, and on the client that owns her, names her when
		/// she has no name yet
		/// </summary>
		private void Awake()
		{
			character = GetComponent<Character>();
			nview = GetComponent<ZNetView>();
			if (nview == null || !nview.IsValid())
			{
				return;
			}
			nview.Register<long>(MeetRPC, RPC_Meet);
			ZDO zdo = nview.GetZDO();
			if (nview.IsOwner() && zdo.GetString(NameKey).Length == 0)
			{
				zdo.Set(NameKey, ValkyrieLines.PickName(Random.value));
			}
		}

		/// <summary>
		/// Her name
		/// </summary>
		/// <returns>The name, or "" when she has none yet</returns>
		public string Name()
		{
			return(((nview != null && nview.IsValid()) ? nview.GetZDO().GetString(NameKey) : ""));
		}

		/// <summary>
		/// Checks whether a player knows her name - she's told them it
		/// </summary>
		/// <param name="playerId">The player's ID</param>
		/// <returns>True when they do</returns>
		public bool Knows(long playerId)
		{
			return(nview != null && nview.IsValid() && VikingName.ReadKnown(nview.GetZDO().GetByteArray(KnownKey)).Contains(playerId));
		}

		/// <summary>
		/// The name the local player sees her by - hers once she's told them it, and plain valkyrie until then
		/// </summary>
		/// <returns>The name</returns>
		public string ShownName()
		{
			Player player = Player.m_localPlayer;
			string name = Name();
			return(((player != null && name.Length > 0 && Knows(player.GetPlayerID())) ? name : ValkyriePrefab.DisplayName));
		}

		/// <summary>
		/// The name the local player sees her by (see ShownName), with "Hungry" after it while she is - the only state of
		/// hers shown, since tamed or wild says nothing about her (see ValkyriePack)
		/// </summary>
		/// <returns>The name</returns>
		public string NameWithHunger()
		{
			string name = ShownName();
			if (TryGetComponent(out ValkyriePack pack) && pack.Hungry())
			{
				return(name + Localization.instance.Localize(" ( $hud_tamehungry )"));
			}
			return(name);
		}

		/// <summary>
		/// Checks whether a player can talk to her - she's alive and not angry with them
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>True when they can</returns>
		public bool CanTalk(Player player)
		{
			return(player != null && character != null && !character.IsDead() && !BaseAI.IsEnemy(character, player));
		}

		/// <summary>
		/// Answers a player who talks to her - telling them her name the first time, and briefly after that, friendlier
		/// once she's tamed (see ValkyrieLines). One she's angry with she doesn't answer at all (see CanTalk)
		/// </summary>
		/// <param name="player">The player</param>
		public void Talk(Player player)
		{
			if (!CanTalk(player) || Time.time - lastTalk < TalkGap)
			{
				return;
			}
			lastTalk = Time.time;
			long id = player.GetPlayerID();
			bool tamed = character.IsTamed();
			string line;
			if (!Knows(id) && Name().Length > 0)
			{
				line = ValkyrieLines.Introduction(Name(), tamed, Random.value);
				Meet(id);
			}
			else
			{
				bool hungry = TryGetComponent(out ValkyriePack pack) && pack.Hungry();
				line = ValkyrieLines.Answer(tamed, hungry, Random.value);
			}
			VikingSpeech.Say(character, line, VikingSpeech.TalkTime, VikingSpeech.TalkCull);
		}

		/// <summary>
		/// Tames her the way the game tames a creature (see Tameable.Tame), on the client that owns her - with the game's
		/// effect, and in place of its "has been tamed", telling the nearest player within 30 meters she's been
		/// befriended, by her name, which they know from then on as if she'd told them
		/// </summary>
		/// <param name="tameable">Her Tameable</param>
		public void Befriend(Tameable tameable)
		{
			Game.instance.IncrementPlayerStat(PlayerStatType.CreatureTamed);
			if (nview == null || !nview.IsValid() || !nview.IsOwner() || tameable.m_monsterAI == null || character == null || tameable.IsTamed())
			{
				return;
			}
			tameable.m_monsterAI.MakeTame();
			tameable.m_tamedEffect.Create(transform.position, transform.rotation);
			Player closest = Player.GetClosestPlayer(transform.position, 30f);
			if (closest == null)
			{
				return;
			}
			Meet(closest.GetPlayerID());
			string name = Name();
			closest.Message(MessageHud.MessageType.Center, ((name.Length > 0) ? name : ValkyriePrefab.DisplayName) + " has been befriended");
		}

		/// <summary>
		/// Notes that a player knows her name, on the client that owns her, asking it to when that isn't this one
		/// </summary>
		/// <param name="playerId">The player's ID</param>
		private void Meet(long playerId)
		{
			if (nview.IsOwner())
			{
				RPC_Meet(0L, playerId);
				return;
			}
			nview.InvokeRPC(nview.GetZDO().GetOwner(), MeetRPC, playerId);
		}

		/// <summary>
		/// Notes that a player knows her name, forgetting the one she told longest ago past the most she remembers
		/// </summary>
		/// <param name="sender">The client asking</param>
		/// <param name="playerId">The player's ID</param>
		private void RPC_Meet(long sender, long playerId)
		{
			if (!nview.IsValid() || !nview.IsOwner())
			{
				return;
			}
			ZDO zdo = nview.GetZDO();
			System.Collections.Generic.List<long> known = VikingName.ReadKnown(zdo.GetByteArray(KnownKey));
			if (known.Contains(playerId))
			{
				return;
			}
			known.Add(playerId);
			if (known.Count > KnownMax)
			{
				known.RemoveAt(0);
			}
			zdo.Set(KnownKey, VikingName.WriteKnown(known));
		}
	}

	/// <summary>
	/// What a valkyrie says - very little, neutral to a stranger and friendlier, if still brief, once she's tamed, which
	/// only means she's willing to carry someone
	/// </summary>
	/// <remarks>
	/// Every line here is listed in the mod's DIALOG.md, which changes along with them
	/// </remarks>
	internal static class ValkyrieLines
	{
		/// <summary>
		/// How she tells a player her name the first time they talk, wild or tamed - {0} is her name
		/// </summary>
		internal static readonly string[] WildIntroductions = { "{0}.", "I am {0}.", "{0}. Remember it." };
		internal static readonly string[] TamedIntroductions = { "I am {0}. Well met.", "{0}. Good to know you.", "Call me {0}." };

		/// <summary>
		/// What she answers a player who knows her name, wild or tamed
		/// </summary>
		internal static readonly string[] WildAnswers = { "Yes?", "Speak.", "What is it?", "Hm.", "I am listening." };
		internal static readonly string[] TamedAnswers = { "Yes, friend?", "I'm here.", "Ready when you are.", "Good to see you.", "Shall we fly?" };

		/// <summary>
		/// What she answers while she's hungry and befriended, in place of her other answers
		/// </summary>
		internal static readonly string[] HungryAnswers = { "I'm hungry, friend.", "Food, soon?" };

		/// <summary>
		/// What she says as she catches her rider falling (see ValkyrieCatch)
		/// </summary>
		internal static readonly string[] CatchLines = { "Got you.", "I have you.", "Hold still.", "Careful." };

		/// <summary>
		/// What she says when her rider has her spin where it would hit buildings, which she won't (see ValkyrieSpinGuard)
		/// </summary>
		internal static readonly string[] NotHereLines = { "Not here.", "Too close.", "Not near the halls." };

		/// <summary>
		/// Picks one of the valkyries' names
		/// </summary>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>The name</returns>
		internal static string PickName(float roll)
		{
			return(Pick(TravelerPets.ValkyrieNames, roll));
		}

		/// <summary>
		/// Picks how she tells a player her name
		/// </summary>
		/// <param name="name">Her name</param>
		/// <param name="tamed">Whether she's tamed</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>The line</returns>
		internal static string Introduction(string name, bool tamed, float roll)
		{
			return(string.Format(Pick((tamed ? TamedIntroductions : WildIntroductions), roll), name));
		}

		/// <summary>
		/// Picks what she answers a player who already knows her name - about her hunger while she's hungry and
		/// befriended
		/// </summary>
		/// <param name="tamed">Whether she's tamed</param>
		/// <param name="hungry">Whether she's hungry</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>The line</returns>
		internal static string Answer(bool tamed, bool hungry, float roll)
		{
			if (hungry && tamed)
			{
				return(Pick(HungryAnswers, roll));
			}
			return(Pick((tamed ? TamedAnswers : WildAnswers), roll));
		}

		/// <summary>
		/// Picks what she says as she catches her rider falling
		/// </summary>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>The line</returns>
		internal static string Catch(float roll)
		{
			return(Pick(CatchLines, roll));
		}

		/// <summary>
		/// Picks what she says when she won't spin among buildings
		/// </summary>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>The line</returns>
		internal static string NotHere(float roll)
		{
			return(Pick(NotHereLines, roll));
		}

		/// <summary>
		/// Picks one of a list by a roll
		/// </summary>
		/// <param name="options">The list</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>The one picked</returns>
		private static string Pick(string[] options, float roll)
		{
			return(options[Mathf.Clamp(Mathf.FloorToInt(roll * options.Length), 0, options.Length - 1)]);
		}
	}
}
