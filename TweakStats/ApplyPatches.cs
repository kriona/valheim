using HarmonyLib;

namespace TweakStats
{
	/// <summary>
	/// Applies the tweaks once the game's items and recipes are loaded, after other mods have added theirs
	/// </summary>
	[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
	public static class ObjectDBAwakePatch
	{
		/// <summary>
		/// Applies the tweaks after the item database starts
		/// </summary>
		[HarmonyPriority(Priority.Last)]
		private static void Postfix()
		{
			Tweaks.Apply();
		}
	}

	/// <summary>
	/// Applies the tweaks when the main menu copies the item database for the character preview
	/// </summary>
	[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
	public static class ObjectDBCopyOtherDBPatch
	{
		/// <summary>
		/// Applies the tweaks after the item database is copied
		/// </summary>
		[HarmonyPriority(Priority.Last)]
		private static void Postfix()
		{
			Tweaks.Apply();
		}
	}

	/// <summary>
	/// Applies the tweaks once the world's creature and build piece prefabs are loaded
	/// </summary>
	[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
	public static class ZNetSceneAwakePatch
	{
		/// <summary>
		/// Applies the tweaks after the prefab list starts
		/// </summary>
		[HarmonyPriority(Priority.Last)]
		private static void Postfix()
		{
			Tweaks.Apply();
		}
	}

	/// <summary>
	/// Applies the world's tweaks when a world starts as single player, the host or a dedicated server
	/// </summary>
	[HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
	public static class ZNetAwakePatch
	{
		/// <summary>
		/// Records the world being played when this game runs it, then applies the tweaks
		/// </summary>
		/// <param name="__instance">The network manager</param>
		private static void Postfix(ZNet __instance)
		{
			if (__instance.IsServer())
			{
				Tweaks.CurrentWorld = ZNet.World;
				Tweaks.Apply();
			}
		}
	}

	/// <summary>
	/// Applies the world's tweaks when a client joins a server and learns which world it runs
	/// </summary>
	[HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
	public static class ZNetPeerInfoPatch
	{
		/// <summary>
		/// Records the server's world once its details arrive, then applies the tweaks
		/// </summary>
		/// <param name="__instance">The network manager</param>
		private static void Postfix(ZNet __instance)
		{
			if (!__instance.IsServer() && ZNet.World != Tweaks.CurrentWorld)
			{
				Tweaks.CurrentWorld = ZNet.World;
				Tweaks.Apply();
			}
		}
	}

	/// <summary>
	/// Puts back the tweaks for no world when leaving a world
	/// </summary>
	[HarmonyPatch(typeof(ZNet), nameof(ZNet.OnDestroy))]
	public static class ZNetOnDestroyPatch
	{
		/// <summary>
		/// Forgets the world and any server's config, and applies the tweaks that apply everywhere
		/// </summary>
		private static void Postfix()
		{
			Tweaks.CurrentWorld = null;
			Tweaks.ClearServerConfig();
			ServerSync.Clear();
			Tweaks.Apply();
		}
	}
}
