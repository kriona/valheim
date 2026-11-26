using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeps a village's vikings out of one of its chests, or lets them use it again, with VillageArea.AreaKey while a
	/// player looks at it - any chest in the village they'd use, whoever built it - playing the ward's activate sound as
	/// it's closed to them and its deactivate sound as it's opened. A ward keeps players it doesn't permit from changing
	/// it. Every chest the vikings may use is one of the village's own: they stock it and take from it, count its room
	/// before building another, and empty it when the village is disbanded (see NoteDisbandChests)
	/// </summary>
	/// <remarks>
	/// Whoever owns the chest notes it, told by a routed RPC, so the player changing it never takes it over from a
	/// player or viking who has it open
	/// </remarks>
	internal static class VillageChestUse
	{
		/// <summary>
		/// On a chest, that a player keeps the vikings out of it
		/// </summary>
		public static readonly int KeptOutKey = "kriona.VikingVillages.chestKeptOut".GetStableHashCode();

		/// <summary>
		/// On the bonfire of a disbanded village, the chests its vikings empty before they set out - the ones they used
		/// when it was disbanded
		/// </summary>
		public static readonly int DisbandChestsKey = "kriona.VikingVillages.fireDisbandChests".GetStableHashCode();

		/// <summary>
		/// The routed RPC that notes on a chest whether the vikings are kept out of it, with the chest's ZDO and whether
		/// they are
		/// </summary>
		public const string KeptOutRpc = Plugin.PluginGuid + ".VillageChestKeptOut";

		/// <summary>
		/// The ward, whose activate and deactivate sounds are played
		/// </summary>
		private const string WardPrefab = "guard_stone";

		/// <summary>
		/// The ward's activate sound, played as the vikings are kept out of a chest, and its deactivate sound, played as
		/// they're let in again - found the first time each is wanted
		/// </summary>
		private static EffectList keepOutSound;
		private static EffectList letInSound;

		/// <summary>
		/// Registers the RPC with a world's routed RPC
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register<ZDOID, bool>(KeptOutRpc, OnKeptOut);
		}

		/// <summary>
		/// Keeps the vikings out of the chest the player looks at, or lets them in again, when the key is pressed - call
		/// it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			Player player = Player.m_localPlayer;
			if (KeyTarget(player, out Container chest))
			{
				Toggle(player, chest);
			}
		}

		/// <summary>
		/// Checks whether the key does something this frame - it was just pressed while the player takes input and looks
		/// at a chest in a village - so the game's own use of the key, toggling auto-pickup, is left out (see
		/// VillageAreaKeyPatch)
		/// </summary>
		/// <returns>True when it does</returns>
		public static bool KeyTaken()
		{
			return(KeyTarget(Player.m_localPlayer, out Container _));
		}

		/// <summary>
		/// Finds the chest the key keeps the vikings out of or lets them into this frame - nothing unless it was just
		/// pressed while the player takes input and looks at a chest in a village
		/// </summary>
		/// <param name="player">The local player, or null</param>
		/// <param name="chest">Set to the chest looked at</param>
		/// <returns>True when the key does something</returns>
		private static bool KeyTarget(Player player, out Container chest)
		{
			chest = null;
			if (player == null || !ZInput.GetKeyDown(VillageArea.AreaKey, false) || !player.TakeInput())
			{
				return(false);
			}
			GameObject hover = player.GetHoverObject();
			chest = ((hover != null) ? hover.GetComponentInParent<Container>() : null);
			return(chest != null && InVillage(chest));
		}

		/// <summary>
		/// Checks whether a chest is one the vikings of the village it stands in would use, when they aren't kept out of
		/// it
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>True when it is</returns>
		private static bool InVillage(Container chest)
		{
			return(VillageLife.ChestOfVillage(Village.Find(chest.transform.position), chest));
		}

		/// <summary>
		/// Checks whether a player keeps the vikings out of a chest
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>True when they do</returns>
		public static bool KeptOut(Container chest)
		{
			ZNetView view = chest.m_nview;
			return(view != null && view.IsValid() && view.GetZDO().GetBool(KeptOutKey));
		}

		/// <summary>
		/// The line a chest in a village adds to its hover text for the key
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>The line, or null when the key does nothing there</returns>
		public static string HoverLine(Container chest)
		{
			if (chest == null || !InVillage(chest))
			{
				return(null);
			}
			return("[<color=yellow><b>" + VillageArea.AreaKey + "</b></color>] " + (KeptOut(chest) ? "Let the Vikings use this chest" : "Keep the Vikings out of this chest"));
		}

		/// <summary>
		/// Keeps the vikings out of a chest, or lets them in again, unless a ward keeps the player out - having whoever
		/// owns the chest note it, and playing the ward's sound for it
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="chest">The chest</param>
		private static void Toggle(Player player, Container chest)
		{
			Vector3 position = chest.transform.position;
			if (ZRoutedRpc.instance == null || !PrivateArea.CheckAccess(position))
			{
				return;
			}
			bool keepOut = !KeptOut(chest);
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, KeptOutRpc, chest.m_nview.GetZDO().m_uid, keepOut);
			EffectList sound = Sound(keepOut);
			if (sound != null)
			{
				sound.Create(position, chest.transform.rotation);
			}
			player.Message(MessageHud.MessageType.Center, (keepOut ? "The Vikings won't use this chest" : "The Vikings will use this chest"));
		}

		/// <summary>
		/// Notes on a chest whether the vikings are kept out of it when this client owns it - or, on the server, when no
		/// one does, taking it over to note it
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="id">The chest's ZDO</param>
		/// <param name="keptOut">Whether they're kept out</param>
		private static void OnKeptOut(long sender, ZDOID id, bool keptOut)
		{
			ZDO zdo = ZDOMan.instance.GetZDO(id);
			if (zdo == null)
			{
				return;
			}
			if (!zdo.IsOwner())
			{
				if (!ZNet.instance.IsServer() || zdo.HasOwner())
				{
					return;
				}
				zdo.SetOwner(ZDOMan.GetSessionID());
			}
			zdo.Set(KeptOutKey, keptOut);
		}

		/// <summary>
		/// Gets the ward's activate or deactivate sound - only the effects of it that make a sound, without its flash -
		/// finding it the first time it's wanted
		/// </summary>
		/// <param name="keepOut">True for the activate sound, false for the deactivate sound</param>
		/// <returns>The sound, or null when the ward can't be found</returns>
		private static EffectList Sound(bool keepOut)
		{
			EffectList found = (keepOut ? keepOutSound : letInSound);
			if (found != null)
			{
				return(found);
			}
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(WardPrefab) : null);
			PrivateArea ward = ((prefab != null) ? prefab.GetComponent<PrivateArea>() : null);
			if (ward == null)
			{
				return(null);
			}
			EffectList effects = (keepOut ? ward.m_activateEffect : ward.m_deactivateEffect);
			found = new EffectList();
			found.m_effectPrefabs = effects.m_effectPrefabs.Where(IsSound).ToArray();
			if (keepOut)
			{
				keepOutSound = found;
			}
			else
			{
				letInSound = found;
			}
			return(found);
		}

		/// <summary>
		/// Checks whether one of an effect list's effects makes a sound
		/// </summary>
		/// <param name="effect">The effect</param>
		/// <returns>True when it's turned on and its prefab plays a sound</returns>
		private static bool IsSound(EffectList.EffectData effect)
		{
			return(effect != null && effect.m_enabled && effect.m_prefab != null && effect.m_prefab.GetComponentInChildren<ZSFX>(true) != null);
		}

		/// <summary>
		/// Notes on a village's bonfire, as it's disbanded, the chests its vikings use, which they empty before they set
		/// out - so a chest a player built is taken from only when it was the village's
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="fire">Its bonfire's ZDO, owned by this client</param>
		public static void NoteDisbandChests(Village village, ZDO fire)
		{
			List<ZDOID> chests = new List<ZDOID>();
			foreach (Container chest in VillageLife.VillageChests(village))
			{
				chests.Add(chest.m_nview.GetZDO().m_uid);
			}
			fire.Set(DisbandChestsKey, EncodeChests(chests));
		}

		/// <summary>
		/// Reads the chests a disbanded village's vikings empty, as noted on its bonfire
		/// </summary>
		/// <param name="fire">The bonfire's ZDO</param>
		/// <returns>The chests' ZDOs</returns>
		public static HashSet<ZDOID> DisbandChests(ZDO fire)
		{
			return(new HashSet<ZDOID>(DecodeChests(fire.GetByteArray(DisbandChestsKey))));
		}

		/// <summary>
		/// Writes a list of chests as bytes
		/// </summary>
		/// <param name="chests">The chests' ZDOs</param>
		/// <returns>The bytes</returns>
		internal static byte[] EncodeChests(List<ZDOID> chests)
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(chests.Count);
			foreach (ZDOID chest in chests)
			{
				pkg.Write(chest);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Reads a list of chests from the bytes they're noted as - none when there are no bytes or they can't be read
		/// </summary>
		/// <param name="bytes">The bytes (see EncodeChests), or null</param>
		/// <returns>The chests' ZDOs</returns>
		internal static List<ZDOID> DecodeChests(byte[] bytes)
		{
			List<ZDOID> chests = new List<ZDOID>();
			if (bytes == null)
			{
				return(chests);
			}
			try
			{
				ZPackage pkg = new ZPackage(bytes);
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					chests.Add(pkg.ReadZDOID());
				}
			}
			catch (System.Exception)
			{
				chests.Clear();
			}
			return(chests);
		}
	}
}
