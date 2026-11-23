using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// The villages players start - pressing VillageArea.AreaKey while looking at a bonfire a player built, away from
	/// every other village and farm, and saying yes when asked, makes it the fire of a new village with no houses and
	/// no vikings, which the beds players put up around it bring vikings to (see VillageBeds) - shown with the game's
	/// banner and sting for finding a biome, and Hugin's tip about beds. Shift and the same key at its fire, and saying
	/// yes again, disbands it, and its vikings say goodbye and run off
	/// </summary>
	/// <remarks>
	/// A started village is kept in an anchor of its own in place of a farm's generator - an object with nothing to see,
	/// registered as a prefab so every client loads it, standing where the bonfire stands - so the village carries on
	/// when its bonfire is destroyed, and its vikings build the bonfire back. It's set up the way a farm's village is
	/// once its paths are laid and it's furnished, so nothing is laid out, built or furnished for it, and its head count
	/// is only what its beds bring
	/// </remarks>
	internal static class VillageFounding
	{
		/// <summary>
		/// The anchor's prefab
		/// </summary>
		public const string PrefabName = "ZombieApocalypseVillage";
		public static readonly int AnchorHash = PrefabName.GetStableHashCode();

		/// <summary>
		/// On a living viking, that it's leaving a village that was disbanded, which it no longer belongs to
		/// </summary>
		public static readonly int LeavingKey = "kriona.ZombieApocalypse.vikingLeaving".GetStableHashCode();

		/// <summary>
		/// On a living viking, that it turned back to a village started again at the bonfire it was leaving, and has yet
		/// to be given one of the village's beds (see VillageBeds.Invite)
		/// </summary>
		public static readonly int TurnedBackKey = "kriona.ZombieApocalypse.vikingTurnedBack".GetStableHashCode();

		/// <summary>
		/// The bonfire a village can be started at
		/// </summary>
		private const string BonfirePrefab = "bonfire";

		/// <summary>
		/// How far past another village's edge, and how far from another started village's anchor, a village can be
		/// started, and how far from an abandoned farm, the same as the farm's guard range (see VikingSpawns.NearestFarm),
		/// in meters
		/// </summary>
		private const float VillageGap = 60f;
		private const float FarmGap = 100f;

		/// <summary>
		/// How far past a village's edge its vikings are looked for to leave as it's disbanded, and how far from the
		/// village's anchor a viking's noted village can be and still be it, in meters
		/// </summary>
		private const float DisbandReach = 100f;
		private const float FarmMatch = 16f;

		/// <summary>
		/// How far from a bonfire the vikings still leaving the village disbanded at it are looked for to turn back as a
		/// village is started there again, in meters - past where the last player near them could be before they go
		/// </summary>
		private const float TurnBackReach = 200f;

		/// <summary>
		/// The routed RPC that sends a viking of a disbanded village away on the client that owns it, with the viking's
		/// ZDO and its bonfire's, or ZDOID.None
		/// </summary>
		public const string SendAwayRpc = Plugin.PluginGuid + ".VillageSendAway";

		/// <summary>
		/// What the popups ask before a village is started or disbanded
		/// </summary>
		private const string StartHeader = "Start a village?";
		private const string StartText = "This bonfire becomes the heart of a new village. Vikings come to live in the beds you build around it, gather at the fire and look after the village. You can disband it again with Shift+V at the fire.";
		private const string DisbandHeader = "Disband this village?";

		/// <summary>
		/// What the banner says as a village is started, and what's shown after it every time
		/// </summary>
		private const string Banner = "A New Village";
		private const string Reminder = "Build unclaimed beds near the fire, under a roof, for Vikings to move in";

		private static GameObject prefab;
		private static GameObject prefabHolder;

		/// <summary>
		/// Adds the anchor's prefab to a world's prefabs, building it the first time
		/// </summary>
		/// <param name="scene">The world's ZNetScene, as it wakes</param>
		public static void Register(ZNetScene scene)
		{
			if (scene.m_namedPrefabs.ContainsKey(AnchorHash))
			{
				return;
			}
			GameObject built = BuildPrefab();
			scene.m_prefabs.Add(built);
			scene.m_namedPrefabs.Add(AnchorHash, built);
		}

		/// <summary>
		/// Builds the anchor - an empty object kept with the world, with nothing to see or bump into
		/// </summary>
		/// <remarks>
		/// It sits under an inactive object, so its network view doesn't wake while it's set up, and it's kept across
		/// worlds
		/// </remarks>
		/// <returns>The prefab</returns>
		private static GameObject BuildPrefab()
		{
			if (prefab != null)
			{
				return(prefab);
			}
			if (prefabHolder == null)
			{
				prefabHolder = new GameObject("ZombieApocalypse village anchor");
				prefabHolder.SetActive(false);
				Object.DontDestroyOnLoad(prefabHolder);
			}
			GameObject anchor = new GameObject(PrefabName);
			anchor.transform.SetParent(prefabHolder.transform, false);
			ZNetView view = anchor.AddComponent<ZNetView>();
			view.m_persistent = true;
			view.m_type = ZDO.ObjectType.Default;
			prefab = anchor;
			return(prefab);
		}

		/// <summary>
		/// Checks whether a prefab is a started village's anchor
		/// </summary>
		/// <param name="hash">The prefab's hash</param>
		/// <returns>True when it is</returns>
		public static bool IsAnchor(int hash)
		{
			return(hash == AnchorHash);
		}

		/// <summary>
		/// Checks whether a fire is one a village could be started at - a bonfire a player built that's no village's
		/// fire, while VikingVillages is on - so its hover text offers it
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>True when it is</returns>
		public static bool Offers(Fireplace fire)
		{
			if (!Plugin.VikingVillages.Value || !ServerCheck.Active || fire == null || Utils.GetPrefabName(fire.gameObject) != BonfirePrefab)
			{
				return(false);
			}
			ZNetView view = fire.GetComponent<ZNetView>();
			if (view == null || !view.IsValid())
			{
				return(false);
			}
			ZDO zdo = view.GetZDO();
			return(zdo.GetLong(ZDOVars.s_creator, 0L) != 0L && !zdo.GetBool(VillagePaths.BonfireKey));
		}

		/// <summary>
		/// Asks a player whether to start a village at a bonfire, and starts it when they say yes - or tells them why it
		/// can't be started, when it's too near another village or a farm. A ward that keeps them out says so itself
		/// </summary>
		/// <param name="player">The local player</param>
		/// <param name="fire">The bonfire, which offers it (see Offers)</param>
		public static void TryStart(Player player, Fireplace fire)
		{
			if (!CanStart(player, fire))
			{
				return;
			}
			Confirm(StartHeader, StartText, () =>
			{
				// The bonfire, or what's around it, may have changed while the player made up their mind
				int returning = 0;
				if (fire == null || !Offers(fire) || !CanStart(player, fire) || !Start(fire, out returning))
				{
					return;
				}
				if (MessageHud.instance != null)
				{
					MessageHud.instance.ShowBiomeFoundMsg(Banner, true);
				}
				player.Message(MessageHud.MessageType.TopLeft, ReturningMessage(returning));
				VikingPvpTip.ShowFounding(player);
			});
		}

		/// <summary>
		/// Says what's shown after the banner as a village is started - where its vikings come from, or which beds the
		/// ones that left a village disbanded at its bonfire come back to
		/// </summary>
		/// <param name="returning">How many vikings are coming back</param>
		/// <returns>The message</returns>
		internal static string ReturningMessage(int returning)
		{
			if (returning == 0)
			{
				return(Reminder);
			}
			return((returning == 1) ? "The Viking who left comes back to the first unclaimed bed near the fire" : "The " + returning + " Vikings who left come back to the unclaimed beds near the fire, one to each");
		}

		/// <summary>
		/// Checks whether a player can start a village at a bonfire - a ward lets them in and it isn't too near another
		/// village or a farm - telling them why not when they can't
		/// </summary>
		/// <param name="player">The local player</param>
		/// <param name="fire">The bonfire</param>
		/// <returns>True when they can</returns>
		private static bool CanStart(Player player, Fireplace fire)
		{
			Vector3 position = fire.transform.position;
			if (!PrivateArea.CheckAccess(position))
			{
				return(false);
			}
			if (TooClose(position, out string why))
			{
				player.Message(MessageHud.MessageType.Center, why);
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Asks a player whether to disband a village they started, and disbands it when they say yes, unless a ward
		/// keeps them out
		/// </summary>
		/// <param name="player">The local player</param>
		/// <param name="village">The village, one a player started (see Village.IsFounded)</param>
		public static void TryDisband(Player player, Village village)
		{
			Fireplace fire = village.Fire();
			if (!village.IsFounded() || !PrivateArea.CheckAccess((fire != null) ? fire.transform.position : village.Farm))
			{
				return;
			}
			Confirm(DisbandHeader, DisbandText(village.Vikings().Count), () =>
			{
				if (village.View == null || !village.View.IsValid())
				{
					return;
				}
				int left = Disband(village);
				player.Message(MessageHud.MessageType.Center, DisbandedMessage(left));
			});
		}

		/// <summary>
		/// Says what the popup asks before a village is disbanded - what becomes of its vikings and its bonfire
		/// </summary>
		/// <param name="count">How many vikings the village has</param>
		/// <returns>The text</returns>
		internal static string DisbandText(int count)
		{
			if (count == 0)
			{
				return("It has no Vikings yet. The bonfire stays.");
			}
			string leaving = ((count == 1) ? "Its Viking says goodbye and leaves" : "Its " + count + " Vikings say goodbye and leave");
			return(leaving + " with everything they have. Start a village at this bonfire again, and they come back to its beds. The bonfire stays.");
		}

		/// <summary>
		/// Says what's shown once a village is disbanded
		/// </summary>
		/// <param name="left">How many of its vikings are leaving</param>
		/// <returns>The message</returns>
		internal static string DisbandedMessage(int left)
		{
			if (left == 0)
			{
				return("The village is disbanded");
			}
			return((left == 1) ? "The village is disbanded, and its Viking is leaving" : "The village is disbanded, and its " + left + " Vikings are leaving");
		}

		/// <summary>
		/// Shows the game's yes or no popup, doing something when the player says yes - or does it straight away when the
		/// popup can't be shown
		/// </summary>
		/// <param name="header">The popup's title</param>
		/// <param name="text">What it asks</param>
		/// <param name="yes">What saying yes does</param>
		private static void Confirm(string header, string text, System.Action yes)
		{
			if (!UnifiedPopup.IsAvailable())
			{
				yes();
				return;
			}
			UnifiedPopup.Push(new YesNoPopup(header, text, () =>
			{
				UnifiedPopup.Pop();
				yes();
			}, () =>
			{
				UnifiedPopup.Pop();
			}, false));
		}

		/// <summary>
		/// Disbands a village a player started - its vikings leave with everything they have (see SendAway), kept at its
		/// bonfire with its name to come back to a village started there again, the beds that brought them are let go of,
		/// its pin is taken off this player's map, its bonfire is an ordinary fire again, and its anchor is taken away
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>How many vikings are leaving</returns>
		private static int Disband(Village village)
		{
			Fireplace fire = village.Fire();
			ZNetView fireView = ((fire != null) ? fire.GetComponent<ZNetView>() : null);
			ZDO fireZdo = ((fireView != null && fireView.IsValid()) ? fireView.GetZDO() : null);
			if (fireZdo != null)
			{
				fireView.ClaimOwnership();
				fireZdo.Set(VillagePaths.BonfireKey, false);
				VillageNames.KeepAt(village, fireZdo);
			}
			int left = SendAway(village, fireZdo);
			VillageBeds.Release(village);
			VillageMapPins.Forget(village);
			Vector3 position = village.Farm;
			village.View.ClaimOwnership();
			ZNetScene.instance.Destroy(village.View.gameObject);
			Village.Drop(village);
			Plugin.Log.LogInfo("A player disbanded the village at " + position.ToString("F1") + ", and its " + left + " vikings are leaving");
			return(left);
		}

		/// <summary>
		/// Registers the RPC that sends a viking away on the client that owns it with a world's routed RPC
		/// </summary>
		public static void RegisterRpc()
		{
			ZRoutedRpc.instance.Register<ZDOID, ZDOID>(SendAwayRpc, OnSendAway);
		}

		/// <summary>
		/// Sends a village's living vikings away with everything they have - those this client has, in the village or out
		/// to DisbandReach past its edge. Each one loaded here is marked as leaving, so it says goodbye to a player near
		/// it and runs off until it's out of every player's reach, and is kept at the bonfire as it goes (see
		/// VillageLife.UpdateLeaving). One another client owns is marked as leaving by that client (see OnSendAway),
		/// since a value set here on a ZDO it owns would be lost to its next change, and the rest, which no one is near to
		/// see, are kept there and taken away at once
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="fire">Its bonfire's ZDO, owned by this client, or null when it has none, when the vikings aren't
		/// kept</param>
		/// <returns>How many were sent away</returns>
		private static int SendAway(Village village, ZDO fire)
		{
			List<ZDO> zdos = new List<ZDO>();
			VillageRespawn.ZdosNear(village.Middle, village.Radius + DisbandReach, zdos);
			int left = 0;
			foreach (ZDO zdo in zdos)
			{
				if (!BelongsTo(zdo.GetPrefab(), zdo.GetVec3(Village.VillageKey, Vector3.zero), village.Farm, zdo.GetBool(LeavingKey)))
				{
					continue;
				}
				ZNetView view = ZNetScene.instance.FindInstance(zdo);
				Humanoid viking = ((view != null) ? view.GetComponent<Humanoid>() : null);
				if (viking != null && viking.IsDead())
				{
					continue;
				}
				if (viking != null)
				{
					view.ClaimOwnership();
					MarkLeaving(zdo, fire);
				}
				else if (zdo.HasOwner() && !zdo.IsOwner())
				{
					ZRoutedRpc.instance.InvokeRoutedRPC(zdo.GetOwner(), SendAwayRpc, zdo.m_uid, ((fire != null) ? fire.m_uid : ZDOID.None));
				}
				else
				{
					VillageDeparted.Keep(fire, zdo);
					zdo.SetOwner(ZDOMan.GetSessionID());
					ZDOMan.instance.DestroyZDO(zdo);
				}
				left++;
			}
			return(left);
		}

		/// <summary>
		/// Checks whether a ZDO is a living viking of a village that isn't already leaving one, to send away as the village
		/// is disbanded
		/// </summary>
		/// <param name="prefab">The ZDO's prefab's hash</param>
		/// <param name="farm">The village it notes, or zero for none</param>
		/// <param name="villageFarm">Where the village's farm or anchor stands</param>
		/// <param name="leaving">Whether it's leaving a village</param>
		/// <returns>True when it is - noting a village within FarmMatch of the village's across the ground</returns>
		internal static bool BelongsTo(int prefab, Vector3 farm, Vector3 villageFarm, bool leaving)
		{
			return(prefab == VikingCore.LivingPrefabHash && farm != Vector3.zero && Utils.DistanceXZ(farm, villageFarm) <= FarmMatch && !leaving);
		}

		/// <summary>
		/// Marks a living viking as leaving a disbanded village, so it says goodbye and runs off, to be kept at the
		/// bonfire as it goes
		/// </summary>
		/// <param name="zdo">The viking's ZDO, owned by this client</param>
		/// <param name="fire">The bonfire's ZDO, or null when it isn't kept</param>
		private static void MarkLeaving(ZDO zdo, ZDO fire)
		{
			if (fire != null)
			{
				VillageDeparted.NoteFire(zdo, fire);
			}
			zdo.Set(TurnedBackKey, false);
			zdo.Set(LeavingKey, true);
		}

		/// <summary>
		/// Sends away a viking of a disbanded village that this client owns, on another client's word (see SendAway) -
		/// marked as leaving whether it's loaded here or about to be, since its owner is near it. One that's dead, already
		/// leaving, or that this client neither owns nor has loaded is left as it is
		/// </summary>
		/// <param name="sender">The client that disbanded the village</param>
		/// <param name="vikingId">The viking's ZDO</param>
		/// <param name="fireId">The bonfire's ZDO, or ZDOID.None when the viking isn't kept</param>
		private static void OnSendAway(long sender, ZDOID vikingId, ZDOID fireId)
		{
			if (ZDOMan.instance == null || ZNetScene.instance == null)
			{
				return;
			}
			ZDO zdo = ZDOMan.instance.GetZDO(vikingId);
			if (zdo == null || zdo.GetBool(LeavingKey))
			{
				return;
			}
			ZNetView view = ZNetScene.instance.FindInstance(zdo);
			Humanoid viking = ((view != null) ? view.GetComponent<Humanoid>() : null);
			if (viking != null && viking.IsDead())
			{
				return;
			}
			if (viking != null)
			{
				view.ClaimOwnership();
			}
			else if (!zdo.IsOwner())
			{
				return;
			}
			MarkLeaving(zdo, ZDOMan.instance.GetZDO(fireId));
		}

		/// <summary>
		/// Turns back the vikings still leaving a village disbanded at a bonfire, as a village is started there again -
		/// each belongs to the new village, and says so to a player near it as it turns (see VillageLife.BeforeAI), and
		/// takes the first of the village's beds that calls for a viking (see VillageBeds.Invite)
		/// </summary>
		/// <param name="fire">The bonfire's ZDO</param>
		/// <param name="farm">Where the new village's anchor stands</param>
		/// <returns>How many turned back</returns>
		private static int TurnBack(ZDO fire, Vector3 farm)
		{
			List<ZDO> zdos = new List<ZDO>();
			VillageRespawn.ZdosNear(farm, TurnBackReach, zdos);
			int turned = 0;
			foreach (ZDO zdo in zdos)
			{
				if (zdo.GetPrefab() != VikingCore.LivingPrefabHash || !zdo.GetBool(LeavingKey) || VillageDeparted.LeftFire(zdo) != fire)
				{
					continue;
				}
				if (!zdo.IsOwner())
				{
					zdo.SetOwner(ZDOMan.GetSessionID());
				}
				zdo.Set(LeavingKey, false);
				zdo.Set(TurnedBackKey, true);
				zdo.Set(Village.VillageKey, farm);
				turned++;
			}
			return(turned);
		}

		/// <summary>
		/// Checks whether a spot is too near another village to start one at - within VillageGap of a loaded village's
		/// edge or another started village's anchor, or within FarmGap of an abandoned farm
		/// </summary>
		/// <param name="position">The spot</param>
		/// <param name="why">What to tell the player when it is</param>
		/// <returns>True when it's too near</returns>
		private static bool TooClose(Vector3 position, out string why)
		{
			why = "";
			foreach (Village village in Village.Loaded())
			{
				if (village.DistanceOutside(position) <= VillageGap)
				{
					why = "Too close to another village to start one here";
					return(true);
				}
			}
			List<ZDO> zdos = new List<ZDO>();
			VillageRespawn.ZdosNear(position, FarmGap, zdos);
			if (zdos.Exists((ZDO zdo) => IsAnchor(zdo.GetPrefab()) && Utils.DistanceXZ(zdo.GetPosition(), position) <= VillageGap))
			{
				why = "Too close to another village to start one here";
				return(true);
			}
			// A client may not know where the world plans its farms, so the farms it has loaded are looked at too
			if (VikingSpawns.NearestFarm(position, out Vector3 _) || zdos.Exists((ZDO zdo) => zdo.GetPrefab() == VillagePaths.GeneratorHash && Utils.DistanceXZ(zdo.GetPosition(), position) <= FarmGap))
			{
				why = "Too close to a farm to start a village here";
				return(true);
			}
			return(false);
		}

		/// <summary>
		/// Puts a village's anchor down where a bonfire stands, set up with no houses, nothing to lay out or furnish and
		/// no vikings, and makes the bonfire its fire - a village disbanded at the bonfire getting its name back, and the
		/// vikings that left it coming back, as its beds bring vikings (see VillageDeparted)
		/// </summary>
		/// <param name="fire">The bonfire</param>
		/// <param name="returning">How many vikings are coming back</param>
		/// <returns>False when the anchor's prefab is missing</returns>
		private static bool Start(Fireplace fire, out int returning)
		{
			returning = 0;
			GameObject anchorPrefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(AnchorHash) : null);
			ZNetView fireView = fire.GetComponent<ZNetView>();
			if (anchorPrefab == null || fireView == null || !fireView.IsValid())
			{
				return(false);
			}
			Vector3 position = fire.transform.position;
			GameObject anchor = Object.Instantiate(anchorPrefab, position, Quaternion.identity);
			ZDO zdo = anchor.GetComponent<ZNetView>().GetZDO();
			List<VillagePaths.House> none = new List<VillagePaths.House>();
			VillagePaths.RecordFounded(zdo);
			Village.RecordHouses(zdo, none);
			VillageFurnishing.MarkFurnished(zdo);
			fireView.ClaimOwnership();
			fireView.GetZDO().Set(VillagePaths.BonfireKey, true);
			returning = VillageDeparted.HandOver(fireView.GetZDO(), zdo) + TurnBack(fireView.GetZDO(), position);
			VillageNames.Restore(fireView.GetZDO(), zdo);
			Plugin.Log.LogInfo("A player started a village at the bonfire at " + position.ToString("F1") + ((returning > 0) ? ", with " + returning + " vikings that left it coming back" : ""));
			return(true);
		}
	}
}
