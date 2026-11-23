using HarmonyLib;

namespace ZombieApocalypse
{
	/// <summary>
	/// Places the vikings' speech bubbles (see VikingBubbles) once the game has placed its own
	/// </summary>
	[HarmonyPatch(typeof(Chat), nameof(Chat.UpdateNpcTexts))]
	public static class VikingBubbleChatPatch
	{
		/// <summary>
		/// Places the bubbles
		/// </summary>
		private static void Postfix()
		{
			VikingBubbles.Place();
		}
	}

	/// <summary>
	/// Places the vikings' speech bubbles (see VikingBubbles) once the camera has moved, so they keep up with it
	/// </summary>
	/// <remarks>
	/// Unity runs different scripts' LateUpdates in no set order, so the game's own placing can go by the camera from
	/// the frame before
	/// </remarks>
	[HarmonyPatch(typeof(GameCamera), nameof(GameCamera.LateUpdate))]
	public static class VikingBubbleCameraPatch
	{
		/// <summary>
		/// Places the bubbles
		/// </summary>
		private static void Postfix()
		{
			VikingBubbles.Place();
		}
	}
}
