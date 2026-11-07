using HarmonyLib;

namespace ChestPeek
{
	/// <summary>
	/// Starts recoloring a chest's fittings by how full it is once the chest has woken up
	/// </summary>
	[HarmonyPatch(typeof(Container), nameof(Container.Awake))]
	public static class ContainerAwakePatch
	{
		/// <summary>
		/// Hands the container to ChestColors, which follows its contents from then on
		/// </summary>
		/// <param name="__instance">The container that woke up</param>
		private static void Postfix(Container __instance)
		{
			ChestColors.Track(__instance);
		}
	}

	/// <summary>
	/// Recolors a chest each time it reads its saved contents again, including a newly placed chest with nothing saved
	/// </summary>
	[HarmonyPatch(typeof(Container), nameof(Container.Load))]
	public static class ContainerLoadPatch
	{
		/// <summary>
		/// Hands the container to ChestColors when the load found a change
		/// </summary>
		/// <param name="__instance">The container that loaded</param>
		/// <param name="__result">Whether its saved data had changed since the last load</param>
		private static void Postfix(Container __instance, bool __result)
		{
			if (__result)
			{
				ChestColors.Loaded(__instance);
			}
		}
	}
}
