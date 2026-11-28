using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A traveling viking's life in the village it visits (see Traveler) - gathering at the fire, talking, petting the
	/// village's animals, resting and eating its own food, but no chores - sitting by the fire all night, then in the
	/// morning trading some of its food for the village's and setting off
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// How long after a traveler that left while out of every player's reach is loaded it's taken away, so the
		/// village's chests have loaded for its trade, in seconds
		/// </summary>
		private const float TravelerGoneDelay = 5f;

		/// <summary>
		/// How often a traveler eats what it carries when it needs to, in seconds
		/// </summary>
		private const float TravelerEatInterval = 10f;

		/// <summary>
		/// How far past the village's edge a leaving traveler walks before it heads off out of sight, in meters
		/// </summary>
		private const float TravelerLeaveReach = 6f;

		/// <summary>
		/// How likely a traveler is to talk and to sit by the fire when it picks what to do by day - far more than the
		/// village's vikings talk, since it has no work to do, so its own animal, always there to pet, doesn't take up
		/// its day - and how long it sits by the fire, in seconds
		/// </summary>
		private const float TravelerTalkWeight = 100f;
		private const float TravelerFireWeight = 60f;
		private const float TravelerFireTimeMin = 60f;
		private const float TravelerFireTimeMax = 150f;

		/// <summary>
		/// Whether the traveler has checked whether it left while out of every player's reach since it loaded, when it's
		/// taken away for that, and whether it has noted its visit on the village
		/// </summary>
		private bool travelerChecked;
		private float travelerGoneAt = -1f;
		private bool travelerNoted;

		/// <summary>
		/// Whether the traveler has left and is taken away at its next update, so nothing else in the update it left in
		/// reaches it once it's gone
		/// </summary>
		private bool travelerOut;

		/// <summary>
		/// How near its animal a traveler gets on it, how near an enemy has to come for a riding traveler to get off and
		/// fight, and how far past the animal's side it gets off, in meters
		/// </summary>
		private const float MountReach = 4f;
		internal const float DismountThreat = 15f;
		private const float DismountSide = 1.5f;
		private const float DismountGap = 0.5f;

		/// <summary>
		/// The animal the traveler rides, and the saddle it sits in, while it does - and for a valkyrie, her flying and
		/// whether she has flown with it on since it got on
		/// </summary>
		private Character ridden;
		private Sadle riddenSaddle;
		private ValkyrieFlight riddenFlight;
		private bool riddenFlew;

		/// <summary>
		/// Runs a traveler's ride on its saddled animal before its AI, on its owner - getting on it as the traveler
		/// arrives outside the village, then sitting in the saddle while the animal carries it (see TravelerPet), getting
		/// off once the animal is in the village as it arrives, or a valkyrie has landed with it at the village's edge, or
		/// to fight when an enemy comes within DismountThreat - once she's landed, for a valkyrie. As it leaves, it's taken
		/// away once no player is within VillageDeparted.GoneReach, or TravelerPets.FlownGoneReach flying
		/// </summary>
		/// <remarks>
		/// While it rides, its AI and life are left out, so it neither moves nor looks for enemies itself
		/// </remarks>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it rides, so the rest of the update is skipped</returns>
		private bool UpdateRiding(float dt)
		{
			ZDO zdo = nview.GetZDO();
			// Loaded again after riding, it's no longer in the saddle
			if (ridden == null && zdo.GetBool(TravelerPets.RidingKey))
			{
				zdo.Set(TravelerPets.RidingKey, false);
			}
			if (ridden == null && zdo.GetBool(TravelerPets.RideInKey))
			{
				zdo.Set(TravelerPets.RideInKey, false);
				Character mount = TravelerPets.PetOf(zdo);
				if (mount != null && !village.Contains(transform.position) && Vector3.Distance(mount.transform.position, transform.position) <= MountReach)
				{
					Mount(mount);
				}
			}
			if (ridden == null)
			{
				return(false);
			}
			if (ridden.IsDead() || attachPoint == null || riddenSaddle == null)
			{
				Dismount();
				return(false);
			}
			bool leaving = zdo.GetBool(TravelerPets.LeavingKey);
			if (leaving && Player.GetClosestPlayer(transform.position, ((riddenFlight != null) ? TravelerPets.FlownGoneReach : VillageDeparted.GoneReach)) == null)
			{
				Plugin.Log.LogInfo(LogName() + ((riddenFlight != null) ? " flew off from " : " rode off from ") + VillageNames.Get(village) + " for " + zdo.GetString(Traveler.ToKey));
				nview.Destroy();
				return(true);
			}
			bool aloft = (riddenFlight != null && riddenFlight.IsFlying());
			riddenFlew = (riddenFlew || aloft);
			bool arrived = (!leaving && ((riddenFlight != null) ? (riddenFlew && !aloft) : village.Contains(ridden.transform.position)));
			if (arrived || (!aloft && Village.FindEnemyNear(transform.position, DismountThreat) != null))
			{
				Dismount();
				return(false);
			}
			mover = (aloft ? "flying on its " : "riding its ") + Utils.GetPrefabName(ridden.gameObject);
			ai.StopMoving();
			return(true);
		}

		/// <summary>
		/// Gets the traveler on its saddled animal, sitting in the saddle in the player's riding pose for it
		/// </summary>
		/// <param name="mount">The animal</param>
		/// <returns>False when it has no saddle to sit in</returns>
		private bool Mount(Character mount)
		{
			Sadle saddle = ((mount != null) ? mount.GetComponentInChildren<Sadle>() : null);
			if (saddle == null || saddle.m_attachPoint == null || !saddle.gameObject.activeInHierarchy)
			{
				return(false);
			}
			Interrupt("getting on its " + Utils.GetPrefabName(mount.gameObject));
			Attach(saddle.m_attachPoint, mount.gameObject);
			VikingPoses.SetRidePose(humanoid, saddle.m_attachAnimation, true);
			ridden = mount;
			riddenSaddle = saddle;
			riddenFlight = mount.GetComponent<ValkyrieFlight>();
			riddenFlew = false;
			nview.GetZDO().Set(TravelerPets.RidingKey, true);
			return(true);
		}

		/// <summary>
		/// Gets the traveler off its animal, standing beside it - DismountSide from its middle, or DismountGap past its
		/// side for one wider than that
		/// </summary>
		private void Dismount()
		{
			if (riddenSaddle != null)
			{
				VikingPoses.SetRidePose(humanoid, riddenSaddle.m_attachAnimation, false);
			}
			Vector3 beside = ((ridden != null) ? ridden.transform.position + ridden.transform.right * Mathf.Max(DismountSide, ridden.GetRadius() + DismountGap) : transform.position);
			if (attachPoint != null)
			{
				DetachTo(beside + Vector3.up * 0.5f);
			}
			ridden = null;
			riddenSaddle = null;
			riddenFlight = null;
			riddenFlew = false;
			if (nview != null && nview.IsValid())
			{
				nview.GetZDO().Set(TravelerPets.RidingKey, false);
			}
		}

		/// <summary>
		/// When the traveler next notes it's loaded and next eats, in Time.time seconds
		/// </summary>
		private float nextTravelerSeen;
		private float nextTravelerEat;

		/// <summary>
		/// Checks whether the viking is a traveler
		/// </summary>
		/// <returns>True when it is</returns>
		public bool IsTraveler()
		{
			return(nview != null && nview.IsValid() && Traveler.Is(nview.GetZDO()));
		}

		/// <summary>
		/// Runs a traveler's own part of its life each update - taking it away once it has left (see LeaveJob), or with its
		/// trade done at once when it left while out of every player's reach, noting it's loaded, noting its visit on the
		/// village once it has a name, eating what it carries when it needs to, and taking up its animal's fight
		/// </summary>
		/// <returns>True when it's been taken away</returns>
		private bool UpdateTraveler()
		{
			if (travelerOut)
			{
				Plugin.Log.LogInfo(LogName() + " left " + VillageNames.Get(village) + " for " + nview.GetZDO().GetString(Traveler.ToKey));
				nview.Destroy();
				return(true);
			}
			ZDO zdo = nview.GetZDO();
			long now = NetTime.Now();
			if (!travelerChecked)
			{
				travelerChecked = true;
				if (Traveler.LeftWhileAway(now, zdo.GetLong(Traveler.LeaveAtKey, 0L), zdo.GetLong(Traveler.SeenKey, 0L), NetTime.Ticks(Traveler.UnseenGap)))
				{
					travelerGoneAt = Time.time + TravelerGoneDelay;
				}
			}
			if (travelerGoneAt >= 0f)
			{
				if (Time.time < travelerGoneAt)
				{
					ai.StopMoving();
					return(true);
				}
				if (!zdo.GetBool(Traveler.TradedKey))
				{
					TradeAt(FindTradeChest());
				}
				Plugin.Log.LogInfo(LogName() + " left " + VillageNames.Get(village) + " while no player was near");
				nview.Destroy();
				return(true);
			}
			if (Time.time >= nextTravelerSeen)
			{
				nextTravelerSeen = Time.time + Traveler.SeenInterval;
				zdo.Set(Traveler.SeenKey, now);
			}
			if (!travelerNoted && VikingNames.Get(humanoid).Length > 0)
			{
				travelerNoted = true;
				Traveler.NoteVisit(village, zdo);
			}
			if (Time.time >= nextTravelerEat)
			{
				nextTravelerEat = Time.time + TravelerEatInterval;
				EatCarried("Eaten by travelers");
			}
			TakeUpPetFight(zdo);
			return(false);
		}

		/// <summary>
		/// Has a traveler with nothing to fight take up the enemy its animal fights, however far off - a monster or the
		/// like, since one angry at a player is so already (see TravelerPet)
		/// </summary>
		/// <param name="zdo">The traveler's ZDO</param>
		private void TakeUpPetFight(ZDO zdo)
		{
			if (ai.m_targetCreature != null)
			{
				return;
			}
			TravelerPet pet = TravelerPet.Of(TravelerPets.PetOf(zdo));
			Character enemy = ((pet != null && !pet.Orphaned()) ? pet.Target() : null);
			if (enemy != null && !enemy.IsPlayer() && BaseAI.IsEnemy(humanoid, enemy))
			{
				ai.m_targetCreature = enemy;
				ai.SetAlerted(true);
			}
		}

		/// <summary>
		/// Picks what a traveler does next - keeping guard through a raid, gathering at the fire from dusk and sitting by
		/// it all night, and by day talking - with a viking strolling or standing about, or, half the time when there's
		/// one, going up to one sitting by the fire and talking with it there - sitting by the fire a while, petting the
		/// village's animals, resting by the fire until it's rested, strolling or standing about, talking and sitting by
		/// the fire most likely (see TravelerTalkWeight) - until its time to leave in the morning, when it trades some of
		/// its food at a village chest and sets off
		/// </summary>
		/// <returns>The job</returns>
		private Job ChooseTravelerJob()
		{
			if (RaidReady())
			{
				return(new GuardJob(this));
			}
			if (IsNight() || IsDusk())
			{
				return(new GatherJob(this, false));
			}
			ZDO zdo = nview.GetZDO();
			long now = NetTime.Now();
			long leaveAt = zdo.GetLong(Traveler.LeaveAtKey, 0L);
			if (now >= leaveAt)
			{
				long giveUp = leaveAt + NetTime.Ticks(Traveler.TradeTimeoutHours / 24f * EnvMan.instance.m_dayLengthSec);
				if (!zdo.GetBool(Traveler.TradedKey) && now < giveUp)
				{
					return(new TradeJob(this));
				}
				return(new LeaveJob(this));
			}
			bool raining = VikingWeather.KeepsIndoors(transform.position);
			VillageLife partner = FindPartner();
			VillageLife sittingPartner = (raining ? null : FindSeatedPartner());
			bool seatedTalk = (sittingPartner != null && (partner == null || Random.value < 0.5f));
			if (seatedTalk)
			{
				partner = sittingPartner;
			}
			Character animal = ((raining || OtherDoing(PettingKey)) ? null : FindBoar());
			bool dry = !VikingWeather.RainsOn(transform.position);
			bool hasFire = village.GatherFires().Count > 0;
			bool refresh = (dry && !body.Rest.IsRested() && Time.time >= restAgainAt && hasFire);
			float talk = ((partner != null) ? TravelerTalkWeight : 0f);
			float fireside = ((dry && hasFire && Time.time >= restAgainAt) ? TravelerFireWeight : 0f);
			float pet = ((animal != null) ? PetWeight : 0f);
			float rest = (refresh ? RefreshWeight : 0f);
			float stroll = (raining ? 0f : StrollWeight);
			float roll = Random.value * (talk + fireside + pet + rest + stroll + IdleWeight);
			if (roll < talk)
			{
				TalkSession session = new TalkSession(this, partner, Random.Range(TalkTimeMin, TalkTimeMax), seatedTalk);
				if (seatedTalk)
				{
					partner.seatedTalk = session;
				}
				else
				{
					partner.JoinTalk(session);
				}
				return(new TalkJob(this, session));
			}
			roll -= talk;
			if (roll < fireside)
			{
				return(new GatherJob(this, false, false, Random.Range(TravelerFireTimeMin, TravelerFireTimeMax)));
			}
			roll -= fireside;
			if (roll < pet)
			{
				return(new PetJob(this, animal));
			}
			roll -= pet;
			if (roll < rest)
			{
				return(new GatherJob(this, true));
			}
			roll -= rest;
			if (roll < stroll || village.InHouse(transform.position, 0f))
			{
				return(new StrollJob(this));
			}
			return(new IdleJob(this, Random.Range(IdleTimeMin, IdleTimeMax), raining));
		}

		/// <summary>
		/// Finds the chest a traveler trades its food at - the nearest of the village's chests it may use with food in it,
		/// or else the nearest with room for its own
		/// </summary>
		/// <returns>The chest, or null when there's none</returns>
		private Container FindTradeChest()
		{
			List<Container> chests = UsableChests();
			List<Container> stocked = chests.FindAll((Container chest) => chest.GetInventory().GetAllItems().Exists((ItemDrop.ItemData item) => VikingFood.IsFood(item)));
			if (stocked.Count > 0)
			{
				return(Nearest(stocked));
			}
			return(Nearest(chests.FindAll((Container chest) => chest.GetInventory().GetEmptySlots() > 0)));
		}

		/// <summary>
		/// Trades some of a traveler's food at a chest it has reached - taking TradeMin-TradeMax of the food the chest holds
		/// most of, and leaving TradeMin-TradeMax of its own, foods other than those it took first - and notes that it has,
		/// so it sets off. With no chest it only notes it
		/// </summary>
		/// <param name="chest">The chest, or null</param>
		private void TradeAt(Container chest)
		{
			nview.GetZDO().Set(Traveler.TradedKey, true);
			if (chest == null || !OpenChest(chest))
			{
				return;
			}
			Inventory store = chest.GetInventory();
			Inventory own = humanoid.GetInventory();
			List<string> took = TakeFood(store, own, Random.Range(Traveler.TradeMin, Traveler.TradeMax + 1));
			int left = Random.Range(Traveler.TradeMin, Traveler.TradeMax + 1);
			List<ItemDrop.ItemData> foods = own.GetAllItems().FindAll((ItemDrop.ItemData item) => VikingFood.IsFood(item) && !IsCarriedFor(item, CarryLoot) && !IsCarriedFor(item, CarryWood) && !IsCarriedFor(item, CarryCraft));
			foods.Sort((ItemDrop.ItemData a, ItemDrop.ItemData b) => took.Contains(a.m_dropPrefab.name).CompareTo(took.Contains(b.m_dropPrefab.name)));
			int given = 0;
			foreach (ItemDrop.ItemData food in foods)
			{
				if (given >= left)
				{
					break;
				}
				ItemDrop.ItemData put = food.Clone();
				put.m_stack = Mathf.Min(food.m_stack, left - given);
				put.m_equipped = false;
				if (!store.AddItem(put))
				{
					continue;
				}
				own.RemoveItem(food, put.m_stack);
				given += put.m_stack;
				TestLog.Count("Left by travelers", TestLog.ItemName(put), put.m_stack);
			}
			chest.m_closeEffects.Create(chest.transform.position, chest.transform.rotation);
			Plugin.Log.LogInfo(LogName() + " traded food at " + VillageNames.Get(village) + ", taking " + string.Join(", ", took) + " and leaving " + given);
			string line = TravelerSpeech.Pick(TravelerSpeech.TradeLines, TravelerValues(""));
			if (!string.IsNullOrEmpty(line))
			{
				VikingSpeech.Say(humanoid, line, VikingSpeech.TalkTime, VikingSpeech.TalkCull);
			}
		}

		/// <summary>
		/// Takes food out of a chest into the traveler's own inventory - the food the chest holds most of first
		/// </summary>
		/// <param name="store">The chest's inventory</param>
		/// <param name="own">The traveler's inventory</param>
		/// <param name="wanted">How many to take</param>
		/// <returns>The prefab names of the foods taken</returns>
		private static List<string> TakeFood(Inventory store, Inventory own, int wanted)
		{
			Dictionary<string, int> counts = new Dictionary<string, int>();
			Dictionary<string, ItemDrop.ItemData> samples = new Dictionary<string, ItemDrop.ItemData>();
			foreach (ItemDrop.ItemData item in store.GetAllItems())
			{
				if (!VikingFood.IsFood(item))
				{
					continue;
				}
				string name = item.m_dropPrefab.name;
				counts[name] = (counts.TryGetValue(name, out int count) ? count : 0) + item.m_stack;
				samples[name] = item;
			}
			List<string> kinds = new List<string>(counts.Keys);
			kinds.Sort((string a, string b) => counts[b].CompareTo(counts[a]));
			List<string> took = new List<string>();
			foreach (string kind in kinds)
			{
				int amount = Traveler.TakeCount(wanted, counts[kind]);
				if (amount <= 0)
				{
					break;
				}
				ItemDrop.ItemData sample = samples[kind];
				if (own.AddItem(kind, amount, sample.m_quality, sample.m_variant, 0L, "", false) == null)
				{
					continue;
				}
				store.RemoveItem(sample.m_shared.m_name, amount);
				wanted -= amount;
				took.Add(kind);
				TestLog.Count("Taken by travelers", TestLog.ItemName(sample), amount);
			}
			return(took);
		}

		/// <summary>
		/// Gathers what a traveler's lines are filled in with (see TravelerSpeech)
		/// </summary>
		/// <param name="player">The name of the player it speaks to, or ""</param>
		/// <returns>The values, by token</returns>
		private Dictionary<string, string> TravelerValues(string player)
		{
			ZDO zdo = nview.GetZDO();
			return(TravelerSpeech.Values(player, VillageNames.Get(village), zdo.GetString(Traveler.FromKey), zdo.GetString(Traveler.ToKey), AnyOf(Traveler.Folk(zdo, Traveler.FromFolkKey)), AnyOf(Traveler.Folk(zdo, Traveler.ToFolkKey)), Traveler.HomeName(zdo.GetInt(Traveler.OriginKey, -1)), VikingNames.Get(humanoid)));
		}

		/// <summary>
		/// Picks one of a list of names at random
		/// </summary>
		/// <param name="names">The names</param>
		/// <returns>One of them, or "" when there are none</returns>
		private static string AnyOf(List<string> names)
		{
			return((names.Count > 0) ? names[Random.Range(0, names.Count)] : "");
		}

		/// <summary>
		/// Picks a traveler's greeting for a player (see TravelerSpeech.Greeting)
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="known">Whether it knows them</param>
		/// <returns>The line</returns>
		private string TravelerGreeting(Player player, bool known)
		{
			return(TravelerSpeech.Greeting(IsNight(), known, VikingNames.Get(humanoid), TravelerValues(player.GetPlayerName())));
		}

		/// <summary>
		/// Picks what a traveler says when a player it knows talks to it - about where it came from and goes, the vikings
		/// it knows there, its own biome and the sea
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>The line</returns>
		private string TravelerAnswer(Player player)
		{
			return(TravelerSpeech.Pick(IsNight() ? TravelerSpeech.NightAnswers : TravelerSpeech.DayAnswers, TravelerValues(player.GetPlayerName())) ?? VikingSpeech.Answer(player, IsNight(), ""));
		}

		/// <summary>
		/// Picks what one of the village's vikings says to a player about the traveler visiting, or that visited lately,
		/// now and then (see TravelerSpeech.VillageAnswerChance)
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>The line, or null for anything else</returns>
		private string VisitAnswer(Player player)
		{
			Traveler.Visit visit = ((Random.value < TravelerSpeech.VillageAnswerChance) ? Traveler.VisitOf(village) : null);
			if (visit == null)
			{
				return(null);
			}
			Dictionary<string, string> values = TravelerSpeech.Values(player.GetPlayerName(), VillageNames.Get(village), visit.From, visit.To, "", "", visit.Home, visit.Name);
			return(TravelerSpeech.Pick(visit.Here ? TravelerSpeech.VisitingAnswers : TravelerSpeech.VisitedAnswers, values));
		}

		/// <summary>
		/// Picks a talk with or about a traveler - one a traveler starts about its travels, one a viking of the village
		/// starts with a traveler, or now and then one two of the village's vikings have about the traveler visiting, or
		/// that visited lately (see TravelerSpeech.VillageTalkChance)
		/// </summary>
		/// <param name="leader">The viking that started the talk</param>
		/// <param name="partner">The viking it talks to</param>
		/// <returns>The talk's lines, or null for a talk about anything else</returns>
		private static string[] TravelerTalk(VillageLife leader, VillageLife partner)
		{
			if (leader.IsTraveler())
			{
				return(TravelerSpeech.PickTalk(TravelerSpeech.TravelerTalks, leader.TravelerValues("")));
			}
			if (partner.IsTraveler())
			{
				return(TravelerSpeech.PickTalk(TravelerSpeech.VillagerTalks, partner.TravelerValues("")));
			}
			Traveler.Visit visit = ((Random.value < TravelerSpeech.VillageTalkChance) ? Traveler.VisitOf(leader.village) : null);
			if (visit == null)
			{
				return(null);
			}
			Dictionary<string, string> values = TravelerSpeech.Values("", VillageNames.Get(leader.village), visit.From, visit.To, "", "", visit.Home, visit.Name);
			return(TravelerSpeech.PickTalk(visit.Here ? TravelerSpeech.VisitingTalks : TravelerSpeech.VisitedTalks, values));
		}

		/// <summary>
		/// Trading some of a traveler's food at a village chest as it leaves (see TradeAt) - walking to the chest, opening
		/// it and trading there
		/// </summary>
		private class TradeJob : Job
		{
			private Container chest;
			private float tradeAt = -1f;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			public TradeJob(VillageLife owner) : base(owner)
			{
			}

			/// <summary>
			/// Walks to the chest and trades there - finding another when it can't be used or got to, and trading nothing
			/// when there's none
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has traded</returns>
			public override bool Tick(float dt)
			{
				if (chest == null || !life.Usable(chest))
				{
					chest = life.FindTradeChest();
					tradeAt = -1f;
					if (chest == null)
					{
						life.TradeAt(null);
						return(true);
					}
				}
				if (tradeAt >= 0f)
				{
					if (Time.time < tradeAt)
					{
						return(false);
					}
					life.TradeAt(chest);
					return(true);
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				life.Face(chest.transform.position);
				VikingPoses.Interact(life.humanoid);
				chest.m_openEffects.Create(chest.transform.position, chest.transform.rotation);
				tradeAt = Time.time + ReachTime;
				return(false);
			}
		}

		/// <summary>
		/// A traveler setting off - saying goodbye, walking out past the village's edge and heading off out of every
		/// player's sight, the way the game's night monsters leave at dawn, until no player is within
		/// VillageDeparted.GoneReach and it's gone. One with an animal it rides walks to it and gets on first, riding or
		/// flying off on it (see UpdateRiding), and walks when it can't get to it. One with a valkyrie calls her down by
		/// name from where she roosts and waits for her to land near it (see TravelerPet.Answer) - up to CallWait seconds
		/// </summary>
		private class LeaveJob : Job
		{
			private Vector3 spot;
			private bool hasSpot;
			private bool outside;
			private bool walking;

			/// <summary>
			/// When it began, whether it has called its valkyrie down yet, how long after beginning it does, so its goodbye
			/// is seen first, and how long it waits for her before walking off without her, in seconds
			/// </summary>
			private float begunAt;
			private bool called;
			private const float CallDelay = 3f;
			private const float CallWait = 60f;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			public LeaveJob(VillageLife owner) : base(owner)
			{
			}

			/// <summary>
			/// Says goodbye and picks the spot outside the village it walks to
			/// </summary>
			public override void Begin()
			{
				// Its animal goes with it
				life.nview.GetZDO().Set(TravelerPets.LeavingKey, true);
				hasSpot = life.village.ArrivalSpot(out spot);
				begunAt = Time.time;
				string line = TravelerSpeech.Pick(TravelerSpeech.FarewellLines, life.TravelerValues(""));
				if (!string.IsNullOrEmpty(line))
				{
					VikingSpeech.Say(life.humanoid, line, VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				}
				Plugin.Log.LogInfo(life.LogName() + " is setting off from " + VillageNames.Get(life.village));
			}

			/// <summary>
			/// Walks to its animal and gets on, or else walks out of the village and away from the players, and is taken
			/// away once none is near (see UpdateTraveler)
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>False - it goes on until the viking is gone</returns>
			public override bool Tick(float dt)
			{
				if (Player.GetClosestPlayer(life.transform.position, VillageDeparted.GoneReach) == null)
				{
					life.travelerOut = true;
					life.ai.StopMoving();
					return(false);
				}
				Character mount = (walking ? null : TravelerPets.PetOf(life.nview.GetZDO()));
				TravelerPet pet = TravelerPet.Of(mount);
				if (mount != null && pet != null && !pet.Orphaned() && !mount.IsDead() && TravelerPets.IsRidden(mount))
				{
					// A valkyrie is called down by name from her roof, and waited for until she's landed near
					ValkyrieFlight flight = mount.GetComponent<ValkyrieFlight>();
					if (flight != null)
					{
						if (!called && Time.time - begunAt >= CallDelay)
						{
							called = true;
							life.nview.GetZDO().Set(TravelerPet.CalledKey, true);
							string name = (mount.TryGetComponent(out ValkyrieTalk talk) ? talk.Name() : "");
							VikingSpeech.SayFirst(life.humanoid, ValkyrieSightSpeech.Call(((name.Length > 0) ? name : ValkyriePrefab.DisplayName), Random.value), VikingSpeech.CallTime, VikingSpeech.CallCull);
						}
						if (Time.time - begunAt > CallWait)
						{
							walking = true;
							return(false);
						}
						if (!pet.Answered(life.humanoid))
						{
							life.mover = "waiting for its " + Utils.GetPrefabName(mount.gameObject) + " to come down";
							life.Face(mount.transform.position);
							return(false);
						}
					}
					if (Vector3.Distance(mount.transform.position, life.transform.position) > MountReach - 1f)
					{
						life.Go(dt, mount.transform.position, MountReach - 1f, false);
						if (life.moveFailed)
						{
							life.moveFailed = false;
							walking = true;
						}
						return(false);
					}
					if (!life.Mount(mount))
					{
						walking = true;
					}
					return(false);
				}
				if (!outside && hasSpot)
				{
					life.Go(dt, spot, 1f, false);
					if (life.moveFailed || life.village.DistanceOutside(life.transform.position) > TravelerLeaveReach || Utils.DistanceXZ(life.transform.position, spot) < 1.5f)
					{
						life.moveFailed = false;
						outside = true;
					}
					return(false);
				}
				life.mover = "leaving the village it visited";
				life.ai.MoveAwayAndDespawn(dt, false);
				return(false);
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Whether it's still walking out of the village</returns>
			public override string Describe()
			{
				return("Leave" + (outside ? ", heading off" : ", walking out"));
			}
		}
	}
}
