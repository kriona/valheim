using HarmonyLib;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeps a valkyrie's hidden Fallen Valkyrie skeleton from beating its wings out loud unless she's attacking - the
	/// Fallen Valkyrie hovers even standing still and walking, so its standing, walking, running and turning all flap,
	/// but she stands on the ground, and her own fly clip flaps when she flies (see ValkyrieModel)
	/// </summary>
	[HarmonyPatch(typeof(AnimationEffect), nameof(AnimationEffect.Effect))]
	internal static class ValkyrieWingSoundPatch
	{
		/// <summary>
		/// The name every Fallen Valkyrie wing beat's sound has in it
		/// </summary>
		private const string WingBeat = "wingflap";

		/// <summary>
		/// Skips a wing beat's sound from a valkyrie's hidden skeleton while she isn't attacking
		/// </summary>
		/// <param name="__instance">The effect player the animation event went to</param>
		/// <param name="e">The animation event</param>
		/// <returns>False to skip the sound</returns>
		private static bool Prefix(AnimationEffect __instance, AnimationEvent e)
		{
			GameObject effect = e.objectReferenceParameter as GameObject;
			if (effect == null || !effect.name.Contains(WingBeat) || __instance.GetComponent<ValkyrieModel>() != null)
			{
				return(true);
			}
			ValkyrieFlight flight = __instance.GetComponentInParent<ValkyrieFlight>();
			if (flight == null)
			{
				return(true);
			}
			return(flight.TryGetComponent(out Humanoid body) && body.InAttack());
		}
	}
}
