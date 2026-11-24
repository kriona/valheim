using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Moves Clock Mod's clock down under the name of the village the player is in while the clock draws itself, and
	/// back up after (see VillageNameHud) - only patched when Clock Mod is installed
	/// </summary>
	[HarmonyPatch]
	public static class VillageNameClockPatch
	{
		/// <summary>
		/// Whether the clock was found with the fields the move goes by, once it's been looked for - Harmony asks more
		/// than once
		/// </summary>
		private static bool? hooked;

		/// <summary>
		/// Checks that Clock Mod is installed with the fields the move goes by, which a soft dependency has loaded first
		/// </summary>
		/// <returns>False to leave the patch out</returns>
		private static bool Prepare()
		{
			return(Hooked());
		}

		/// <summary>
		/// Finds Clock Mod and the fields the move goes by the first time it's asked
		/// </summary>
		/// <returns>True when they're there</returns>
		internal static bool Hooked()
		{
			if (hooked == null)
			{
				System.Type plugin = ClockType();
				hooked = (plugin != null && VillageNameHud.HookClock(plugin));
			}
			return(hooked.Value);
		}

		/// <summary>
		/// Gets Clock Mod's OnGUI
		/// </summary>
		/// <returns>The method</returns>
		private static MethodBase TargetMethod()
		{
			return(AccessTools.Method(ClockType(), "OnGUI"));
		}

		/// <summary>
		/// Gets Clock Mod's plugin type
		/// </summary>
		/// <returns>The type, or null when it isn't installed</returns>
		internal static System.Type ClockType()
		{
			if (!Chainloader.PluginInfos.TryGetValue(VillageNameHud.ClockGuid, out PluginInfo info) || info.Instance == null)
			{
				return(null);
			}
			return(info.Instance.GetType());
		}

		/// <summary>
		/// Moves the clock down to make room for the name
		/// </summary>
		/// <param name="__state">How far it was moved, to move it back</param>
		private static void Prefix(out float __state)
		{
			__state = VillageNameHud.ClockShift;
			VillageNameHud.MoveClock(__state);
		}

		/// <summary>
		/// Moves the clock back where it was, even when drawing it threw
		/// </summary>
		/// <param name="__state">How far it was moved</param>
		private static void Finalizer(float __state)
		{
			VillageNameHud.MoveClock(-__state);
		}
	}

	/// <summary>
	/// Notes where Clock Mod's clock text sits in its window as it draws it, for the village name to go in its spot
	/// (see VillageNameHud) - only patched when Clock Mod is installed
	/// </summary>
	[HarmonyPatch]
	public static class VillageNameClockLinePatch
	{
		/// <summary>
		/// The name of Clock Mod's method that draws the clock inside its window
		/// </summary>
		private const string WindowMethod = "WindowBuilder";

		/// <summary>
		/// Checks that Clock Mod is installed with the fields the move goes by and its window's method
		/// </summary>
		/// <returns>False to leave the patch out</returns>
		private static bool Prepare()
		{
			return(VillageNameClockPatch.Hooked() && Window() != null);
		}

		/// <summary>
		/// Gets the method that draws the clock inside its window
		/// </summary>
		/// <returns>The method</returns>
		private static MethodBase TargetMethod()
		{
			return(Window());
		}

		/// <summary>
		/// Finds the method that draws the clock inside its window
		/// </summary>
		/// <returns>The method, or null when Clock Mod doesn't have it</returns>
		private static MethodInfo Window()
		{
			System.Type plugin = VillageNameClockPatch.ClockType();
			return((plugin != null) ? plugin.GetMethod(WindowMethod, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) : null);
		}

		/// <summary>
		/// Notes where the clock's text was laid out
		/// </summary>
		private static void Postfix()
		{
			VillageNameHud.NoteClockLine();
		}
	}
}
