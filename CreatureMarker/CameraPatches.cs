using HarmonyLib;

namespace CreatureMarker
{
	/// <summary>
	/// Places the markers each frame once the camera has moved
	/// </summary>
	/// <remarks>
	/// Unity runs different scripts' LateUpdates in no set order, so markers placed from the plugin's own LateUpdate
	/// can be projected with the camera from the frame before. A creature that only moves in FixedUpdate, like the
	/// Valkyrie, then flickers between two spots as frames with and without a physics step alternate
	/// </remarks>
	[HarmonyPatch(typeof(GameCamera), nameof(GameCamera.LateUpdate))]
	public static class GameCameraLateUpdatePatch
	{
		/// <summary>
		/// Places the markers
		/// </summary>
		private static void Postfix()
		{
			Plugin.UpdateMarkers();
		}
	}
}
