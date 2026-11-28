using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What the PC vikings of a village tell each other across clients - that a viking or player is being attacked
	/// or a badly hurt viking is shouting for help, that the night watch has raised the alarm, and the effects of what
	/// they do, which every client shows
	/// </summary>
	/// <remarks>
	/// Each viking's AI runs on whichever client owns it, so a call for help is sent to every client, and each one
	/// hands it to the vikings it owns
	/// </remarks>
	internal static class VillageSignals
	{
		/// <summary>
		/// The routed RPCs - a call for help with where it came from and the attacker, an alarm with the village's farm,
		/// and an effect with its kind and the object it's shown on
		/// </summary>
		public const string DistressRpc = Plugin.PluginGuid + ".VillageDistress";
		public const string AlarmRpc = Plugin.PluginGuid + ".VillageAlarm";
		public const string EffectRpc = Plugin.PluginGuid + ".VillageEffect";

		/// <summary>
		/// The routed RPC that shows a viking's bonus food from foraging, with the pickable, the viking and how much
		/// </summary>
		public const string ForageBonusRpc = Plugin.PluginGuid + ".VillageForageBonus";

		/// <summary>
		/// How far above the viking's head its bonus food shows, in meters
		/// </summary>
		private const float BonusTextLift = 0.3f;

		/// <summary>
		/// The effects shown on every client - the hearts of a boar being petted, the dust of a piece being repaired, a
		/// crafting station's sounds as a viking starts and finishes crafting at it, a chest's sounds as a viking opens
		/// and closes it, and a crafting station's sound as a viking repairs its gear there
		/// </summary>
		public const int PetEffect = 0;
		public const int RepairEffect = 1;
		public const int CraftEffect = 2;
		public const int CraftDoneEffect = 3;
		public const int ChestOpenEffect = 4;
		public const int ChestCloseEffect = 5;
		public const int GearRepairEffect = 6;

		/// <summary>
		/// The effect of a viking reaching a new level in a skill - the glow and sound a player gets around their head
		/// </summary>
		public const int LevelUpEffect = 8;

		/// <summary>
		/// How a viking's level-up sounds - how loud against a player's, and how near it's heard at full volume and how
		/// far it fades out to, in meters
		/// </summary>
		private const float LevelUpVolume = 0.5f;
		private const float LevelUpNear = 3f;
		private const float LevelUpFar = 30f;

		/// <summary>
		/// The least time between calls for help for the same victim, in seconds
		/// </summary>
		private const float DistressInterval = 1f;

		/// <summary>
		/// When each victim last called for help, by its ZDO
		/// </summary>
		private static readonly Dictionary<ZDOID, float> lastDistress = new Dictionary<ZDOID, float>();

		/// <summary>
		/// The least time between calls for help against the same monster for breaking a village's pieces, in seconds,
		/// and when each monster last set one off, by its ZDO
		/// </summary>
		private const float PieceDistressInterval = 3f;
		private static readonly Dictionary<ZDOID, float> lastPieceDistress = new Dictionary<ZDOID, float>();

		/// <summary>
		/// Registers the RPCs with a world's routed RPC
		/// </summary>
		public static void Register()
		{
			if (ZRoutedRpc.instance == null)
			{
				return;
			}
			ZRoutedRpc.instance.Register<Vector3, ZDOID>(DistressRpc, OnDistress);
			ZRoutedRpc.instance.Register<Vector3>(AlarmRpc, OnAlarm);
			ZRoutedRpc.instance.Register<int, ZDOID>(EffectRpc, OnEffect);
			VikingSpeech.Register();
			VillageWatch.Register();
			VillageFallen.Register();
			VillageNames.Register();
			VillageNameList.Register();
			VillagePlannedNames.Register();
			TravelerPlaces.Register();
			VillageArea.Register();
			VillageChestUse.Register();
			VillagePaths.Register();
			VillageLocations.Register();
			VillagePortals.Register();
			VillageFounding.RegisterRpc();
			VillageTravel.Register();
			VikingNewsStore.Register();
			NeighborNews.Register();
			PowerGivers.Register();
			ZRoutedRpc.instance.Register<ZDOID, ZDOID, int>(ForageBonusRpc, OnForageBonus);
			lastDistress.Clear();
			lastPieceDistress.Clear();
			QuietMonsters.Clear();
		}

		/// <summary>
		/// Calls for help when a player or PC viking is hurt by a zombie or monster, on the client that owns the
		/// victim
		/// </summary>
		/// <param name="victim">Who was hurt</param>
		/// <param name="attacker">Who hurt them, or null</param>
		public static void OnHurt(Character victim, Character attacker)
		{
			if (victim == null || attacker == null || ZRoutedRpc.instance == null || !ServerCheck.Active)
			{
				return;
			}
			if (!CallsForHelp(victim.IsPlayer(), VikingCore.IsPlayerLikeViking(victim), attacker.IsPlayer(), attacker.IsTamed(), VikingCore.IsPlayerLikeViking(attacker), attacker.GetFaction()))
			{
				return;
			}
			if (!Due(lastDistress, victim.GetZDOID(), Time.time, DistressInterval))
			{
				return;
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, DistressRpc, victim.transform.position, attacker.GetZDOID());
		}

		/// <summary>
		/// Calls the PC vikings to help when a zombie or monster hits a building piece in a village - its own, or one
		/// a player built there - from where the piece stands, on the client that owns the piece, once every
		/// PieceDistressInterval seconds for each monster
		/// </summary>
		/// <param name="piece">The piece hit</param>
		/// <param name="attacker">Who hit it, or null</param>
		public static void OnPieceHit(WearNTear piece, Character attacker)
		{
			if (piece == null || attacker == null || attacker.IsDead() || ZRoutedRpc.instance == null || !ServerCheck.Active)
			{
				return;
			}
			// The same monsters a viking hurt by them calls for help against
			if (!CallsForHelp(false, true, attacker.IsPlayer(), attacker.IsTamed(), VikingCore.IsPlayerLikeViking(attacker), attacker.GetFaction()))
			{
				return;
			}
			Vector3 position = piece.transform.position;
			bool inVillage = false;
			foreach (Village village in Village.Loaded())
			{
				if (village.Contains(position))
				{
					inVillage = true;
					break;
				}
			}
			if (!inVillage || !Due(lastPieceDistress, attacker.GetZDOID(), Time.time, PieceDistressInterval))
			{
				return;
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, DistressRpc, position, attacker.GetZDOID());
		}

		/// <summary>
		/// Checks whether someone hurt calls for help - a player or PC viking hurt by a zombie or monster, rather than
		/// by a player, a tamed creature, a PC viking, a dverger or something a player summoned
		/// </summary>
		/// <param name="victimPlayer">Whether who was hurt is a player</param>
		/// <param name="victimViking">Whether who was hurt is a PC viking</param>
		/// <param name="attackerPlayer">Whether who hurt them is a player</param>
		/// <param name="attackerTamed">Whether who hurt them is tamed</param>
		/// <param name="attackerViking">Whether who hurt them is a PC viking</param>
		/// <param name="attackerFaction">The faction of who hurt them</param>
		/// <returns>True when they call</returns>
		internal static bool CallsForHelp(bool victimPlayer, bool victimViking, bool attackerPlayer, bool attackerTamed, bool attackerViking, Character.Faction attackerFaction)
		{
			if (!victimPlayer && !victimViking)
			{
				return(false);
			}
			return(!attackerPlayer && !attackerTamed && !attackerViking && attackerFaction != Character.Faction.Dverger && attackerFaction != Character.Faction.PlayerSpawned);
		}

		/// <summary>
		/// Checks whether a victim's call for help can go out - none for it within the interval before - noting the time
		/// when it can, and dropping the calls noted an interval or more ago, which hold nothing up
		/// </summary>
		/// <param name="last">When each victim last called, by its ZDO, added to and dropped from</param>
		/// <param name="id">The victim's ZDO</param>
		/// <param name="now">The time, in seconds</param>
		/// <param name="interval">The least time between calls for the same victim, in seconds</param>
		/// <returns>True when it can</returns>
		internal static bool Due(Dictionary<ZDOID, float> last, ZDOID id, float now, float interval)
		{
			if (last.TryGetValue(id, out float at) && now - at < interval)
			{
				return(false);
			}
			List<ZDOID> stale = new List<ZDOID>();
			foreach (KeyValuePair<ZDOID, float> call in last)
			{
				if (now - call.Value >= interval)
				{
					stale.Add(call.Key);
				}
			}
			foreach (ZDOID old in stale)
			{
				last.Remove(old);
			}
			last[id] = now;
			return(true);
		}

		/// <summary>
		/// Sends the night watch's alarm to every client, waking the village's vikings
		/// </summary>
		/// <param name="village">The village</param>
		public static void SendAlarm(Village village)
		{
			if (ZRoutedRpc.instance != null)
			{
				ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, AlarmRpc, village.Farm);
			}
		}

		/// <summary>
		/// Has a badly hurt viking shout for help - its words show on every client, and every client's vikings near it
		/// come to help against the enemy, the same as for a call for help when someone's hurt
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <param name="enemy">The enemy it wants help against</param>
		public static void SendShout(Character viking, Character enemy)
		{
			if (ZRoutedRpc.instance == null || viking == null || enemy == null)
			{
				return;
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, DistressRpc, viking.transform.position, enemy.GetZDOID());
			VikingSpeech.SayFirst(viking, VikingSpeech.Help(), VikingSpeech.CallTime, VikingSpeech.CallCull);
		}

		/// <summary>
		/// Shows an effect on an object on every client
		/// </summary>
		/// <param name="kind">One of the effects - PetEffect, RepairEffect, CraftEffect, CraftDoneEffect, ChestOpenEffect,
		/// ChestCloseEffect, GearRepairEffect or LevelUpEffect</param>
		/// <param name="target">The object's ZDO</param>
		public static void SendEffect(int kind, ZDOID target)
		{
			if (ZRoutedRpc.instance != null)
			{
				ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, EffectRpc, kind, target);
			}
		}

		/// <summary>
		/// Shows a viking's bonus food from foraging on every client - the amount above its head, the same as a
		/// player's, with the pickable's bonus sound
		/// </summary>
		/// <param name="pickable">The pickable's ZDO</param>
		/// <param name="viking">The viking's ZDO</param>
		/// <param name="bonus">How much more food it got</param>
		public static void SendForageBonus(ZDOID pickable, ZDOID viking, int bonus)
		{
			if (ZRoutedRpc.instance != null)
			{
				ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, ForageBonusRpc, pickable, viking, bonus);
			}
		}

		/// <summary>
		/// Shows a viking's bonus from cooking or crafting on every client - the amount above its head, the same as a
		/// player's, with the game's crafting bonus sound
		/// </summary>
		/// <param name="station">The station it cooked or crafted at</param>
		/// <param name="viking">The viking's ZDO</param>
		/// <param name="bonus">How much more it got</param>
		public static void SendCraftBonus(ZDOID station, ZDOID viking, int bonus)
		{
			SendForageBonus(station, viking, bonus);
		}

		/// <summary>
		/// Shows a viking's bonus from foraging, cooking or crafting, when this client has the viking loaded - with the
		/// pickable's bonus sound, or for a station the game's crafting bonus sound
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="pickableId">The pickable's or station's ZDO</param>
		/// <param name="vikingId">The viking's ZDO</param>
		/// <param name="bonus">How much more food it got</param>
		private static void OnForageBonus(long sender, ZDOID pickableId, ZDOID vikingId, int bonus)
		{
			if (ZNetScene.instance == null)
			{
				return;
			}
			GameObject found = ZNetScene.instance.FindInstance(vikingId);
			Character viking = ((found != null) ? found.GetComponent<Character>() : null);
			if (viking == null)
			{
				return;
			}
			Vector3 above = viking.GetTopPoint() + Vector3.up * BonusTextLift;
			if (DamageText.instance != null)
			{
				DamageText.instance.ShowText(DamageText.TextType.Bonus, above, "+" + bonus, true);
			}
			GameObject pickableObject = ZNetScene.instance.FindInstance(pickableId);
			Pickable pickable = ((pickableObject != null) ? pickableObject.GetComponent<Pickable>() : null);
			if (pickable != null && pickable.m_bonusEffect != null)
			{
				pickable.m_bonusEffect.Create(viking.transform.position, Quaternion.identity);
			}
			else if (pickable == null && InventoryGui.instance != null)
			{
				InventoryGui.instance.m_craftBonusEffect.Create(viking.transform.position, Quaternion.identity);
			}
		}

		/// <summary>
		/// Hands a call for help to the PC vikings this client owns near where it came from, and the valkyries (see
		/// ValkyrieHelp)
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="where">Where the victim was</param>
		/// <param name="attackerId">The attacker's ZDO</param>
		private static void OnDistress(long sender, Vector3 where, ZDOID attackerId)
		{
			GameObject found = ((ZNetScene.instance != null) ? ZNetScene.instance.FindInstance(attackerId) : null);
			Character attacker = ((found != null) ? found.GetComponent<Character>() : null);
			if (attacker == null || attacker.IsDead())
			{
				return;
			}
			QuietMonsters.Provoked(attacker);
			foreach (VillageLife life in VillageLife.All)
			{
				life.OnDistress(where, attacker);
			}
			ValkyrieHelp.Answer(where, attacker);
		}

		/// <summary>
		/// Wakes the PC vikings this client owns in the village whose watch raised the alarm
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="farm">The village's farm</param>
		private static void OnAlarm(long sender, Vector3 farm)
		{
			foreach (VillageLife life in VillageLife.All)
			{
				life.OnAlarm(farm);
			}
		}

		/// <summary>
		/// Has the sound of a viking's level-up - the player's own, made to be heard as the player's - come from the
		/// viking and fade with distance, at LevelUpVolume, so it isn't taken for the player's
		/// </summary>
		/// <param name="created">What the effect made</param>
		private static void QuietLevelUp(GameObject[] created)
		{
			if (created == null)
			{
				return;
			}
			foreach (GameObject effect in created)
			{
				if (effect == null)
				{
					continue;
				}
				foreach (ZSFX sound in effect.GetComponentsInChildren<ZSFX>(true))
				{
					sound.SetVolumeModifier(sound.GetVolumeModifier() * LevelUpVolume);
				}
				foreach (AudioSource source in effect.GetComponentsInChildren<AudioSource>(true))
				{
					source.spatialBlend = 1f;
					source.panStereo = 0f;
					source.rolloffMode = AudioRolloffMode.Linear;
					source.minDistance = LevelUpNear;
					source.maxDistance = LevelUpFar;
				}
			}
		}

		/// <summary>
		/// Shows an effect on an object this client has loaded
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="kind">One of the effects - PetEffect, RepairEffect, CraftEffect, CraftDoneEffect, ChestOpenEffect,
		/// ChestCloseEffect, GearRepairEffect or LevelUpEffect</param>
		/// <param name="targetId">The object's ZDO</param>
		private static void OnEffect(long sender, int kind, ZDOID targetId)
		{
			GameObject target = ((ZNetScene.instance != null) ? ZNetScene.instance.FindInstance(targetId) : null);
			if (target == null)
			{
				return;
			}
			if (kind == PetEffect)
			{
				Tameable tameable = target.GetComponent<Tameable>();
				if (tameable != null)
				{
					tameable.m_petEffect.Create(target.transform.position, target.transform.rotation);
				}
			}
			else if (kind == RepairEffect)
			{
				Piece piece = target.GetComponent<Piece>();
				if (piece != null)
				{
					piece.m_placeEffect.Create(piece.transform.position, piece.transform.rotation, piece.gameObject.transform);
				}
			}
			else if (kind == CraftEffect || kind == CraftDoneEffect || kind == GearRepairEffect)
			{
				CraftingStation station = target.GetComponent<CraftingStation>();
				if (station != null)
				{
					EffectList effects = ((kind == CraftEffect) ? station.m_craftItemEffects : ((kind == CraftDoneEffect) ? station.m_craftItemDoneEffects : station.m_repairItemDoneEffects));
					effects.Create(station.transform.position, Quaternion.identity);
				}
			}
			else if (kind == ChestOpenEffect || kind == ChestCloseEffect)
			{
				Container chest = target.GetComponent<Container>();
				if (chest != null)
				{
					EffectList effects = ((kind == ChestOpenEffect) ? chest.m_openEffects : chest.m_closeEffects);
					effects.Create(chest.transform.position, chest.transform.rotation);
				}
			}
			else if (kind == LevelUpEffect)
			{
				Character viking = target.GetComponent<Character>();
				Player player = ((Game.instance != null && Game.instance.m_playerPrefab != null) ? Game.instance.m_playerPrefab.GetComponent<Player>() : null);
				if (viking != null && viking.m_head != null && player != null)
				{
					GameObject[] created = player.m_skillLevelupEffects.Create(viking.m_head.position, viking.m_head.rotation, viking.m_head);
					QuietLevelUp(created);
				}
			}
		}
	}
}
