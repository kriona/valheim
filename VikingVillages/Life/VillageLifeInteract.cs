using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a player does with a viking - talking to it, naming it, and opening its inventory to hand it gear, which it
	/// puts on or takes off - and the greeting it gives a stranger
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Has a player talk to the viking, or with the alternate key down name it, when it's a PC viking that isn't
		/// their enemy - its owner answers
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
			if (alt)
			{
				if (VikingNames.CanName() && TextInput.instance != null)
				{
					TextInput.instance.RequestText(this, "$hud_rename", VikingNames.MaxLength);
					naming = this;
					nextNamingPing = Time.time + NamingPingInterval;
					nview.InvokeRPC(NamingRpc, user.GetZDOID(), true);
				}
				return(false);
			}
			nview.InvokeRPC(TalkRpc, user.GetZDOID());
			return(false);
		}

		/// <summary>
		/// Keeps the viking the local player is naming facing them while the box to name it is open, telling its owner
		/// every NamingPingInterval seconds, and closes the box once the viking gets more than NamingRange from the
		/// player - in a fight, say - call it from the plugin's Update
		/// </summary>
		public static void UpdateNaming()
		{
			if (ReferenceEquals(naming, null))
			{
				return;
			}
			TextInput input = TextInput.instance;
			// A viking that unloaded has no one to give the name to
			if (naming == null)
			{
				if (input != null && ReferenceEquals(input.m_queuedSign, naming))
				{
					input.Hide();
					input.m_queuedSign = null;
				}
				naming = null;
				return;
			}
			bool open = (input != null && input.m_panel != null && input.m_panel.activeSelf && ReferenceEquals(input.m_queuedSign, naming));
			Player player = Player.m_localPlayer;
			bool valid = (naming.nview != null && naming.nview.IsValid());
			if (open && player != null && valid && Vector3.Distance(player.transform.position, naming.transform.position) > NamingRange)
			{
				input.Hide();
				input.m_queuedSign = null;
				open = false;
			}
			if (!open || player == null || !valid)
			{
				if (player != null && valid)
				{
					naming.nview.InvokeRPC(NamingRpc, player.GetZDOID(), false);
				}
				naming = null;
				return;
			}
			if (Time.time >= nextNamingPing)
			{
				nextNamingPing = Time.time + NamingPingInterval;
				naming.nview.InvokeRPC(NamingRpc, player.GetZDOID(), true);
			}
		}

		/// <summary>
		/// Has the viking stand facing the player naming it while the box to name it is open, on its owner, unless it has
		/// a fight to go to - for NamingHold seconds after each word that it's still open, and NamingLinger once it closes
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="playerId">The player's ZDO</param>
		/// <param name="open">Whether the box is still open</param>
		private void RPC_Naming(long sender, ZDOID playerId, bool open)
		{
			if (!nview.IsOwner())
			{
				return;
			}
			GameObject found = ((ZNetScene.instance != null) ? ZNetScene.instance.FindInstance(playerId) : null);
			Player player = ((found != null) ? found.GetComponent<Player>() : null);
			if (!CanTalkTo(player))
			{
				return;
			}
			if (open)
			{
				// A viking that doesn't know the player greets them as they go to name it, or gets to know them quietly when
				// it can't, so they see the name they give it
				if (!GreetIfStranger(player))
				{
					VikingSpeech.Meet(humanoid, player.GetPlayerID());
				}
				greeting = player;
				greetUntil = Time.time + NamingHold;
				playerLastNear[player.GetZDOID()] = Time.time;
			}
			else if (greeting == player)
			{
				greetUntil = Mathf.Min(greetUntil, Time.time + NamingLinger);
			}
		}

		/// <summary>
		/// Until when the viking leaves its gear as it is, while a player has its inventory open, in Time.time seconds
		/// </summary>
		private float gearHeldUntil;

		/// <summary>
		/// Since when each piece of its own gear the viking would wear has sat in its inventory unworn, in Time.time
		/// seconds, and when it next looks for any
		/// </summary>
		private readonly Dictionary<ItemDrop.ItemData, float> unwornSince = new Dictionary<ItemDrop.ItemData, float>();
		private float nextGearCheck;

		/// <summary>
		/// How long the viking leaves gear of its own it would wear in its inventory before putting it on, and how often
		/// it looks for any, in seconds
		/// </summary>
		private const float PutOnDelay = 10f;
		private const float GearCheckInterval = 2f;

		/// <summary>
		/// Puts a piece of gear on as its change in the body's queue is done (see PCVikingBody.UpdateGearChanges), the
		/// way the viking takes up gear it found (see VikingCore.TakeUpGear), or takes it off - from its back too, when
		/// it's sheathed there
		/// </summary>
		/// <param name="change">The change</param>
		/// <param name="item">The piece it's for - its item, or the one in the other hand</param>
		private void ChangeGear(PCVikingBody.GearChange change, ItemDrop.ItemData item)
		{
			if (change.PutOn)
			{
				if (viking != null && change.Chosen)
				{
					viking.Wear(item);
				}
				else if (viking != null && !GearHeld())
				{
					bool tookUp = viking.TakeUpGear(item, out ItemDrop.ItemData replaced);
					if (change.Found)
					{
						FoundGearDone(item, tookUp, replaced);
					}
				}
				return;
			}
			// Takes a sheathed piece off the viking's back too
			humanoid.UnequipItem(item, true);
		}

		/// <summary>
		/// Has the viking switch to gear of its own that's better than what it has - armor for a slot it has nothing in
		/// or better than what it wears there, or a better shield, like gear a player took off it, gave it or had it swap
		/// for something worse - once it has sat in its inventory unused for PutOnDelay seconds, while no player has its
		/// inventory open, on its owner
		/// </summary>
		private void UpdatePutOn()
		{
			if (Time.time < nextGearCheck)
			{
				return;
			}
			nextGearCheck = Time.time + GearCheckInterval;
			if (viking == null || GearHeld() || humanoid.IsDead())
			{
				unwornSince.Clear();
				return;
			}
			// Weapons are left out - the viking picks the best of its own as each fight calls for (see
			// VikingCore.ChooseWeapon) - and so are its dverger circlet and helmets, while it puts those on and takes them
			// off as it wants light (see VikingCore.UpdateTorch)
			List<ItemDrop.ItemData> unworn = humanoid.GetInventory().GetAllItems().FindAll((ItemDrop.ItemData item) => IsGear(item) && !item.IsWeapon() && !VikingItems.IsCarried(item) && !VikingItems.IsProp(item) && !humanoid.IsItemEquiped(item) && !viking.SwapsForLight(item) && viking.WouldTakeUp(item));
			List<ItemDrop.ItemData> forgotten = new List<ItemDrop.ItemData>();
			foreach (ItemDrop.ItemData item in unwornSince.Keys)
			{
				if (!unworn.Contains(item))
				{
					forgotten.Add(item);
				}
			}
			foreach (ItemDrop.ItemData item in forgotten)
			{
				unwornSince.Remove(item);
			}
			foreach (ItemDrop.ItemData item in unworn)
			{
				if (!unwornSince.TryGetValue(item, out float since))
				{
					unwornSince[item] = Time.time;
				}
				else if (Time.time - since >= PutOnDelay)
				{
					body.QueueGear(item, true, false);
				}
			}
		}

		/// <summary>
		/// Has the viking stand facing a player who has its inventory open and leave its gear as it is, for NamingHold
		/// seconds - called every frame the inventory is open, on the viking's owner, which the player's client is
		/// </summary>
		/// <param name="player">The player</param>
		public void HoldForInventory(Player player)
		{
			if (player == null)
			{
				return;
			}
			greeting = player;
			greetUntil = Time.time + NamingHold;
			gearHeldUntil = Time.time + NamingHold;
			playerLastNear[player.GetZDOID()] = Time.time;
		}

		/// <summary>
		/// Lets the viking go back to what it was doing and look after its gear again, once a player has closed its
		/// inventory, after facing them NamingLinger seconds more
		/// </summary>
		public void ReleaseInventory()
		{
			gearHeldUntil = 0f;
			greetUntil = Mathf.Min(greetUntil, Time.time + NamingLinger);
		}

		/// <summary>
		/// Checks whether the viking leaves its gear as it is - not equipping, drawing, sheathing or swapping anything -
		/// while a player has its inventory open
		/// </summary>
		/// <returns>True while it does</returns>
		public bool GearHeld()
		{
			return(Time.time < gearHeldUntil);
		}

		/// <summary>
		/// Gets the viking's name for the box to rename it
		/// </summary>
		/// <returns>The name, or ""</returns>
		public string GetText()
		{
			return(VikingNames.GetRaw(humanoid));
		}

		/// <summary>
		/// Names the viking what the player typed - its owner names it
		/// </summary>
		/// <param name="text">The name</param>
		public void SetText(string text)
		{
			if (nview != null && nview.IsValid())
			{
				nview.InvokeRPC(NameRpc, (text ?? "").Trim(), VikingNames.LocalAuthor());
			}
		}

		/// <summary>
		/// Names the viking, on its owner, and has it answer to its new name - turning down "Viking" or nothing, which
		/// would take its name away, and a creature's, a legend's, a player's or a fellow viking's name (see
		/// VikingNames.Judge)
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="name">The name</param>
		/// <param name="author">Who named it</param>
		private void RPC_Name(long sender, string name, string author)
		{
			if (!nview.IsOwner() || !VikingCore.IsPCViking(humanoid))
			{
				return;
			}
			bool renamed = (VikingNames.Get(humanoid).Length > 0);
			string plain = VikingNames.Plain(name);
			VikingNames.Verdict verdict = VikingNames.Judge(humanoid, village, plain);
			if (verdict != VikingNames.Verdict.Taken)
			{
				// Markup is said as it was typed, so the line shows what was wrong with it. It's said even in its sleep, so the
				// player isn't left wondering why the name didn't take
				string filtered = VikingNames.Filter(plain, author);
				string shown = ((verdict == VikingNames.Verdict.Markup) ? VikingNames.Literal(filtered) : filtered.RemoveRichTextTags());
				VikingSpeech.Say(humanoid, VikingSpeech.Refused(verdict, shown), VikingSpeech.TalkTime, VikingSpeech.TalkCull, true);
				return;
			}
			VikingNames.Set(humanoid, plain, author);
			string named = VikingNames.Get(humanoid);
			if (named.Length > 0)
			{
				VikingSpeech.Say(humanoid, VikingSpeech.Named(named, renamed), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
			}
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
		/// Checks whether a player can talk to the viking - it's a PC viking, alive, and not their enemy
		/// </summary>
		/// <param name="player">The player, or null</param>
		/// <returns>True when they can</returns>
		public bool CanTalkTo(Player player)
		{
			return(player != null && humanoid != null && !humanoid.IsDead() && nview != null && nview.IsValid() && VikingCore.IsPCViking(humanoid) && !BaseAI.IsEnemy(humanoid, player));
		}

		/// <summary>
		/// Answers a player talking to the viking, on its owner - in a fight or swimming it's busy and asleep it's sleepy, it greets
		/// a player it has never met as a stranger with a wave and remembers them, and chats with one it knows (see
		/// VillageAnswer) or tells them its news of them (see VikingNewsStore), turning to them for TalkFaceTime seconds
		/// either way, and noting the talk once it has said its piece (see VikingIdentity.Answered)
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
			if (ai.m_targetCreature != null)
			{
				VikingSpeech.Say(humanoid, VikingSpeech.Busy(), VikingSpeech.QuickTime, VikingSpeech.TalkCull);
				return;
			}
			if (humanoid.IsSwimming())
			{
				VikingSpeech.Say(humanoid, VikingSpeech.Swimming(), VikingSpeech.QuickTime, VikingSpeech.TalkCull);
				return;
			}
			if (inBed)
			{
				VikingSpeech.Say(humanoid, VikingSpeech.Sleepy(), VikingSpeech.TalkTime, VikingSpeech.TalkCull, true);
				return;
			}
			playerLastNear[player.GetZDOID()] = Time.time;
			if (VikingSpeech.Knows(humanoid, player.GetPlayerID()))
			{
				string news = VikingNewsStore.Tell(humanoid, player, NewsBiome());
				string answer = (news ?? (active ? VillageAnswer(player) : RoadAnswer(player)));
				VikingSpeech.Say(humanoid, answer, VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				greeting = player;
				greetUntil = Time.time + TalkFaceTime;
				VikingIdentity.Answered(nview, player.GetPlayerID(), true);
				return;
			}
			Greet(player, TalkFaceTime, true);
		}

		/// <summary>
		/// Picks what a viking at home says when a player it knows talks to it - now and then about the player wearing no
		/// clothes (see VikingClothesSpeech), the training weapon in their hand (see VikingTrainingSpeech), a quest of its
		/// village's (see QuestAnswer), its village going by its name (see VillageNamesakeSpeech), the village on the
		/// other side of its village's portal (see VillagePortalSpeech), being old friends or how the player is doing (see
		/// VikingFriendSpeech), the one of Frozen's folk it's named after (see VikingFrozenSpeech), or a runestone of the
		/// biome it stands in or of someone with its name (see VikingLoreSpeech)
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>The line</returns>
		private string VillageAnswer(Player player)
		{
			string name = player.GetPlayerName();
			bool friends = VikingIdentity.OldFriends(nview, player.GetPlayerID());
			string own = VikingNames.Get(humanoid);
			string villageName = VillageNames.Get(village);
			return(VikingClothesSpeech.Chat(player) ?? VikingTrainingSpeech.Chat(player) ?? QuestAnswer(player) ?? VillageNamesakeSpeech.Chat(own, villageName, name) ?? VillagePortalSpeech.Chat(village, name) ?? VikingFriendSpeech.Chat(friends, name) ?? VikingFrozenSpeech.Chat(name, own) ?? VikingLoreSpeech.Chat(transform.position, name, own) ?? VikingSpeech.Answer(player, IsNight(), villageName));
		}

		/// <summary>
		/// Picks what a viking away from any village says when a player it knows talks to it - lost, on the road,
		/// wandering, or gathering at the fire of a village a player disbanded
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>The line</returns>
		private string RoadAnswer(Player player)
		{
			ZDO zdo = nview.GetZDO();
			bool onRoad = VillageTravel.OnRoad(zdo);
			if (zdo.GetBool(VillageFounding.LeavingKey) && !onRoad)
			{
				return(VikingRoadSpeech.Disbanded(VillageNames.AtFire(VillageDeparted.LeftFire(zdo))));
			}
			return(VikingRoadSpeech.Answer(player, road == Road.Confused, onRoad, VillageTravel.DestinationName(zdo)));
		}

		/// <summary>
		/// Greets a player it doesn't know yet as they open its inventory or the box to name it, so it gets to know them
		/// - unless it's asleep, in a fight or swimming, the same as it wouldn't greet them on sight
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>True when it greeted them</returns>
		public bool GreetIfStranger(Player player)
		{
			if (player == null || VikingSpeech.Knows(humanoid, player.GetPlayerID()) || inBed || (job != null && job.Resting()) || ai.m_targetCreature != null || humanoid.IsSwimming())
			{
				return(false);
			}
			Greet(player, GreetFaceTime, false);
			return(true);
		}

		/// <summary>
		/// Greets a player - turning to them with a wave and a word, by name for a player it knows, now and then as an old
		/// friend at home (see VikingFriendSpeech), and as a stranger otherwise - and remembers them, noting the talk once
		/// it has said its piece (see VikingIdentity.Answered). A player it knows may hear its news of them in place of
		/// the word (see VikingNewsStore)
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="faceTime">How long it faces them, in seconds</param>
		/// <param name="talked">Whether the player talked to it, rather than it seeing them arrive</param>
		private void Greet(Player player, float faceTime, bool talked)
		{
			long playerId = player.GetPlayerID();
			bool known = VikingSpeech.Knows(humanoid, playerId);
			greeting = player;
			greetUntil = Time.time + faceTime;
			VikingPoses.Wave(humanoid);
			string news = (known ? VikingNewsStore.Tell(humanoid, player, NewsBiome()) : null);
			string friend = ((known && active) ? VikingFriendSpeech.Greeting(VikingIdentity.OldFriends(nview, playerId), player.GetPlayerName()) : null);
			// One away from any village greets them the way a traveler does, or a lost one asks where it is
			string line = friend ?? (active ? VikingSpeech.Greeting(player, IsNight(), known, VikingNames.Get(humanoid), VillageNames.Get(village)) : VikingRoadSpeech.Greeting(player, IsNight(), known, VikingNames.Get(humanoid), road == Road.Confused));
			VikingSpeech.Say(humanoid, news ?? line, VikingSpeech.TalkTime, VikingSpeech.TalkCull);
			VikingSpeech.Meet(humanoid, playerId);
			VikingIdentity.Answered(nview, playerId, talked);
		}

		/// <summary>
		/// Gets the index of the viking's biome, for how impressed it is by news of a boss or biome beyond it (see
		/// VikingNewsLines.ImpressedKey)
		/// </summary>
		/// <returns>The index into VikingGear's tables</returns>
		private int NewsBiome()
		{
			return((viking != null) ? viking.BiomeIndex() : 0);
		}
	}
}
