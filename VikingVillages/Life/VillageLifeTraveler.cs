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
		/// it all night, and by day talking, petting the village's animals, resting by the fire until it's rested, strolling
		/// or standing about - until its time to leave in the morning, when it trades some of its food at a village chest
		/// and sets off
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
			Character animal = ((raining || OtherDoing(PettingKey)) ? null : FindBoar());
			bool refresh = (!VikingWeather.RainsOn(transform.position) && !body.Rest.IsRested() && Time.time >= restAgainAt && village.GatherFires().Count > 0);
			float talk = ((partner != null) ? TalkWeight : 0f);
			float pet = ((animal != null) ? PetWeight : 0f);
			float rest = (refresh ? RefreshWeight : 0f);
			float stroll = (raining ? 0f : StrollWeight);
			float roll = Random.value * (talk + pet + rest + stroll + IdleWeight);
			if (roll < talk)
			{
				TalkSession session = new TalkSession(this, partner, Random.Range(TalkTimeMin, TalkTimeMax));
				partner.JoinTalk(session);
				return(new TalkJob(this, session));
			}
			roll -= talk;
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
		/// VillageDeparted.GoneReach and it's gone
		/// </summary>
		private class LeaveJob : Job
		{
			private Vector3 spot;
			private bool hasSpot;
			private bool outside;

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
				string line = TravelerSpeech.Pick(TravelerSpeech.FarewellLines, life.TravelerValues(""));
				if (!string.IsNullOrEmpty(line))
				{
					VikingSpeech.Say(life.humanoid, line, VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				}
				Plugin.Log.LogInfo(life.LogName() + " is setting off from " + VillageNames.Get(life.village));
			}

			/// <summary>
			/// Walks out of the village and away from the players, and is taken away once none is near (see
			/// UpdateTraveler)
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
