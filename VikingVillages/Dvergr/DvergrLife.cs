using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A dvergr viking's life with the dvergr - greeting a player arriving and answering one who talks to it in words of
	/// its own (see DvergrSpeech), passing on their news, calling out the enemies it goes after and shouting for help
	/// when it's badly hurt, and using the forsaken powers it knows in a fight and when it's hurt, the way a living
	/// viking does - on its owner
	/// </summary>
	internal class DvergrLife : MonoBehaviour, Interactable
	{
		/// <summary>
		/// The RPC that asks the viking's owner to answer a player talking to it, with the player
		/// </summary>
		private const string TalkRpc = Plugin.PluginGuid + ".DvergrTalk";

		/// <summary>
		/// How the viking greets a player arriving - within GreetRange, after they've been away from it for
		/// GreetAwayTime seconds, looking every GreetCheckInterval seconds - and how long it faces a player it greeted, and
		/// one who talked to it, in seconds - longer for a talk, so it doesn't walk off while they read what it says
		/// </summary>
		private const float GreetRange = 10f;
		private const float GreetAwayTime = 180f;
		private const float GreetCheckInterval = 1f;
		private const float GreetFaceTime = 2f;
		private const float TalkFaceTime = GreetFaceTime + 2f;

		/// <summary>
		/// The least time between two answers to a player talking to it, in seconds
		/// </summary>
		private const float TalkAnswerInterval = 1.5f;

		/// <summary>
		/// The least time between two enemies the viking calls out, in seconds
		/// </summary>
		private const float SpotInterval = 15f;

		/// <summary>
		/// How badly hurt the viking is before it shouts for help, as a share of its health, how long after it was last
		/// hurt it still counts as in a fight, and the least time between two shouts, in seconds
		/// </summary>
		private const float HelpShare = 0.3f;
		private const float HurtTime = 5f;
		private const float HelpInterval = 30f;

		/// <summary>
		/// Seconds between the viking's looks at whether to use a power while it's hurt with nothing to fight
		/// </summary>
		private const float PowerCheckInterval = 1f;

		private DvergrVikingBody body;
		private ZNetView nview;
		private MonsterAI ai;
		private VikingCore viking;

		/// <summary>
		/// When each player was last near the viking, in Time.time seconds, so it greets one arriving and not one who
		/// never left
		/// </summary>
		private readonly Dictionary<ZDOID, float> playerLastNear = new Dictionary<ZDOID, float>();

		/// <summary>
		/// The player the viking faces after greeting or answering them, and until when
		/// </summary>
		private Player greeting;
		private float greetUntil;

		/// <summary>
		/// When the viking next looks for a player arriving, next answers a player, may next call out an enemy and may
		/// next shout for help, and next looks at whether to use a power while it's hurt
		/// </summary>
		private float nextGreetCheck;
		private float nextAnswer;
		private float nextCallOut;
		private float nextHelp;
		private float nextPowerCheck;

		/// <summary>
		/// The enemy the viking was last after, so it calls each one out once
		/// </summary>
		private Character lastTarget;

		/// <summary>
		/// Finds the viking's parts and listens for players talking to it
		/// </summary>
		private void Awake()
		{
			body = GetComponent<DvergrVikingBody>();
			nview = GetComponent<ZNetView>();
			ai = GetComponent<MonsterAI>();
			viking = GetComponent<VikingCore>();
			if (nview != null && nview.GetZDO() != null)
			{
				nview.Register<ZDOID>(TalkRpc, RPC_Talk);
			}
		}

		/// <summary>
		/// Has a player talk to the viking when they can (see CanTalkTo) - its owner answers
		/// </summary>
		/// <param name="user">Who's talking to it</param>
		/// <param name="hold">Whether the key is held</param>
		/// <param name="alt">Whether the alternate key is down</param>
		/// <returns>False, so the player doesn't reach out to it</returns>
		public bool Interact(Humanoid user, bool hold, bool alt)
		{
			if (hold || !CanTalkTo(user as Player))
			{
				return(false);
			}
			nview.InvokeRPC(TalkRpc, user.GetZDOID());
			return(false);
		}

		/// <summary>
		/// Leaves items used on the viking to the game
		/// </summary>
		/// <param name="user">Who used it</param>
		/// <param name="item">The item</param>
		/// <returns>False</returns>
		public bool UseItem(Humanoid user, ItemDrop.ItemData item)
		{
			return(false);
		}

		/// <summary>
		/// Checks whether a player can talk to the viking - it's a dvergr viking, alive, and not their enemy
		/// </summary>
		/// <param name="player">The player, or null</param>
		/// <returns>True when they can</returns>
		public bool CanTalkTo(Player player)
		{
			return(player != null && body != null && !body.IsDead() && nview != null && nview.IsValid() && VikingCore.IsDvergrViking(body) && !BaseAI.IsEnemy(body, player));
		}

		/// <summary>
		/// Answers a player talking to the viking, on its owner - in a fight or swimming it's busy, it greets a player it has never
		/// met as a stranger and remembers them, and chats with one it knows - now and then about being old friends or how
		/// the player is doing (see VikingFriendSpeech), the one of Frozen's folk it's named after (see
		/// VikingFrozenSpeech), or a runestone of the biome it stands in or of someone with its name (see
		/// VikingLoreSpeech) -
		/// or tells them its news of them (see VikingNewsStore), turning to them for TalkFaceTime seconds either way, and
		/// noting the talk once it has said its piece (see VikingIdentity.Answered)
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="playerId">The player's ZDO</param>
		private void RPC_Talk(long sender, ZDOID playerId)
		{
			if (!nview.IsOwner() || Time.time < nextAnswer)
			{
				return;
			}
			GameObject found = ((ZNetScene.instance != null) ? ZNetScene.instance.FindInstance(playerId) : null);
			Player player = ((found != null) ? found.GetComponent<Player>() : null);
			if (!CanTalkTo(player))
			{
				return;
			}
			nextAnswer = Time.time + TalkAnswerInterval;
			if (ai != null && ai.m_targetCreature != null)
			{
				VikingSpeech.Say(body, DvergrSpeech.Busy(), VikingSpeech.QuickTime, VikingSpeech.TalkCull);
				return;
			}
			if (body.IsSwimming())
			{
				VikingSpeech.Say(body, VikingSpeech.Swimming(), VikingSpeech.QuickTime, VikingSpeech.TalkCull);
				return;
			}
			playerLastNear[player.GetZDOID()] = Time.time;
			long id = player.GetPlayerID();
			if (VikingSpeech.Knows(body, id))
			{
				string name = player.GetPlayerName();
				string news = VikingNewsStore.Tell(body, player, NewsBiome());
				string chat = VikingFriendSpeech.Chat(VikingIdentity.OldFriends(nview, id), name) ?? VikingFrozenSpeech.Chat(name, VikingNames.Get(body)) ?? VikingLoreSpeech.Chat(transform.position, name, VikingNames.Get(body));
				VikingSpeech.Say(body, news ?? chat ?? DvergrSpeech.Answer(player, EnvMan.IsNight()), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				Face(player, TalkFaceTime);
				VikingIdentity.Answered(nview, id, true);
				return;
			}
			Greet(player, TalkFaceTime, true);
		}

		/// <summary>
		/// Greets a player - turning to them with a wave and a word, by name for a player it knows, now and then as an old
		/// friend (see VikingFriendSpeech), and as a stranger otherwise - and remembers them, noting the talk once it has
		/// said its piece (see VikingIdentity.Answered). A player it knows may hear its news of them in place of the word
		/// (see VikingNewsStore)
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="faceTime">How long it faces them, in seconds</param>
		/// <param name="talked">Whether the player talked to it, rather than it seeing them arrive</param>
		private void Greet(Player player, float faceTime, bool talked)
		{
			long id = player.GetPlayerID();
			bool known = VikingSpeech.Knows(body, id);
			Face(player, faceTime);
			VikingPoses.Wave(body);
			string news = (known ? VikingNewsStore.Tell(body, player, NewsBiome()) : null);
			string friend = (known ? VikingFriendSpeech.Greeting(VikingIdentity.OldFriends(nview, id), player.GetPlayerName()) : null);
			string line = friend ?? DvergrSpeech.Greeting(player, EnvMan.IsNight(), known, VikingNames.Get(body));
			VikingSpeech.Say(body, news ?? line, VikingSpeech.TalkTime, VikingSpeech.TalkCull);
			VikingSpeech.Meet(body, id);
			VikingIdentity.Answered(nview, id, talked);
		}

		/// <summary>
		/// Has the viking face a player for a while
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="time">How long, in seconds</param>
		private void Face(Player player, float time)
		{
			greeting = player;
			greetUntil = Time.time + time;
		}

		/// <summary>
		/// Gets the index of the viking's biome, for how impressed it is by news of a boss or biome beyond it
		/// </summary>
		/// <returns>The index into VikingGear's tables</returns>
		private int NewsBiome()
		{
			return((viking != null) ? viking.BiomeIndex() : VikingGear.IndexOf(Heightmap.Biome.Mistlands));
		}

		/// <summary>
		/// Turns the viking to the player it greeted or answered while it has nothing to fight, in place of the AI's own
		/// update
		/// </summary>
		/// <returns>True while it faces them, so the AI's own update is skipped</returns>
		public bool UpdateFacing()
		{
			if (greeting == null || Time.time >= greetUntil || ai == null || ai.GetTargetCreature() != null)
			{
				greeting = null;
				return(false);
			}
			ai.StopMoving();
			Vector3 look = greeting.GetEyePoint();
			look.y = body.m_eye.position.y;
			ai.LookAt(look);
			return(true);
		}

		/// <summary>
		/// Notes which powers the viking is under, has it use one for the fight it's in or for being hurt, calls out the
		/// enemy it goes after, shouts for help when it's badly hurt and greets a player arriving, on its owner while
		/// the server has the mod
		/// </summary>
		private void Update()
		{
			if (body == null || nview == null || !nview.IsValid() || !nview.IsOwner() || body.IsDead() || !ServerCheck.Active)
			{
				return;
			}
			if (body.Powers != null)
			{
				body.Powers.Update();
				body.Powers.UpdateFight();
				if (Time.time >= nextPowerCheck)
				{
					nextPowerCheck = Time.time + PowerCheckInterval;
					body.Powers.UpdateWork(false, false);
				}
			}
			if (ai == null)
			{
				return;
			}
			UpdateCallOut();
			UpdateHelp();
			UpdateGreeting();
		}

		/// <summary>
		/// Has the viking call out an enemy it has just gone after - by name, every SpotInterval seconds at most, and
		/// never a player it has turned on
		/// </summary>
		private void UpdateCallOut()
		{
			Character target = ai.GetTargetCreature();
			if (target == lastTarget)
			{
				return;
			}
			lastTarget = target;
			if (target == null || target.IsPlayer() || Time.time < nextCallOut)
			{
				return;
			}
			nextCallOut = Time.time + SpotInterval;
			VikingSpeech.SayFirst(body, DvergrSpeech.Spotted(target), VikingSpeech.CallTime, VikingSpeech.CallCull);
		}

		/// <summary>
		/// Has the viking shout for help while it's below HelpShare of its health in a fight, every HelpInterval seconds
		/// at most
		/// </summary>
		private void UpdateHelp()
		{
			bool fighting = (ai.GetTargetCreature() != null || ai.m_timeSinceHurt < HurtTime);
			if (!fighting || body.GetHealthPercentage() >= HelpShare || Time.time < nextHelp)
			{
				return;
			}
			nextHelp = Time.time + HelpInterval;
			VikingSpeech.SayFirst(body, DvergrSpeech.Help(), VikingSpeech.CallTime, VikingSpeech.CallCull);
		}

		/// <summary>
		/// Has the viking greet a player arriving - one within GreetRange it can see, who hasn't been near it for
		/// GreetAwayTime seconds - while it has nothing to fight, looking every GreetCheckInterval seconds
		/// </summary>
		private void UpdateGreeting()
		{
			if (Time.time < nextGreetCheck || (greeting != null && Time.time < greetUntil) || ai.GetTargetCreature() != null)
			{
				return;
			}
			nextGreetCheck = Time.time + GreetCheckInterval;
			foreach (Player player in Player.GetAllPlayers())
			{
				if (player == null || player.IsDead() || BaseAI.IsEnemy(body, player) || Vector3.Distance(player.transform.position, transform.position) > GreetRange)
				{
					continue;
				}
				ZDOID id = player.GetZDOID();
				bool away = (!playerLastNear.TryGetValue(id, out float last) || Time.time - last > GreetAwayTime);
				// A viking that can't see a player arriving leaves them until it can
				if (away && !ai.CanSeeTarget(player))
				{
					continue;
				}
				playerLastNear[id] = Time.time;
				if (away)
				{
					Greet(player, GreetFaceTime, false);
					return;
				}
			}
		}
	}
}
