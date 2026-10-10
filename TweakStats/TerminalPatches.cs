using HarmonyLib;

namespace TweakStats
{
	/// <summary>
	/// Adds the tweakstats command when the console sets up its commands
	/// </summary>
	[HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
	public static class TerminalInitPatch
	{
		/// <summary>
		/// Registers the command after the game's own
		/// </summary>
		private static void Postfix()
		{
			Commands.Register();
		}
	}
}
