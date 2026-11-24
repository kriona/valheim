using HarmonyLib;

namespace CreatureMarker
{
	/// <summary>
	/// Keeps the ping and shout texts the markers moved this frame where they put them
	/// </summary>
	[HarmonyPatch(typeof(Chat), nameof(Chat.UpdateWorldTexts))]
	public static class ChatUpdateWorldTextsPatch
	{
		/// <summary>
		/// Puts the moved texts back after the game places them
		/// </summary>
		private static void Postfix()
		{
			ChatTexts.Apply();
		}
	}
}
