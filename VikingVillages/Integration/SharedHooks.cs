using System.Collections.Generic;
using VikingVillages;

namespace Kriona.Vikings
{
	/// <summary>
	/// What Viking Villages adds to each world once its PC viking's prefab is in - the dvergr viking's prefab
	/// </summary>
	internal static partial class VikingPrefabPatch
	{
		/// <summary>
		/// Adds the dvergr viking's prefab once the PC viking's is in
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		static partial void AfterRegister(ZNetScene scene)
		{
			VikingCore.RegisterDvergr(scene);
		}
	}

	/// <summary>
	/// What Viking Villages adds that's held back in a world whose server doesn't have it, besides its PC viking
	/// </summary>
	internal static partial class CommandGuard
	{
		/// <summary>
		/// Names its console commands, all starting "kri_vv_", the anchor of a village a player started and the dvergr
		/// viking - it has no raids of its own
		/// </summary>
		/// <param name="commandPrefixes">The starts of its commands' names, added to</param>
		/// <param name="prefabs">Its prefabs and items, added to</param>
		/// <param name="raids">Its raids, added to</param>
		static partial void AddOwned(List<string> commandPrefixes, List<string> prefabs, List<string> raids)
		{
			commandPrefixes.Add("kri_vv_");
			prefabs.Add(VillageFounding.PrefabName);
			prefabs.Add(VikingCore.DvergrPrefabName);
		}
	}

	/// <summary>
	/// What Viking Villages forgets as it leaves a world
	/// </summary>
	internal static partial class ServerCheck
	{
		/// <summary>
		/// Forgets what was noted in the world - the villages' timers, what was read from them and the server's answers
		/// about them, which are by the world's ZDOs, and the dvergr outposts players were near
		/// </summary>
		static partial void OnLeave()
		{
			VillageRespawn.Forget();
			VillageRecords.Forget();
			VillageLocations.Forget();
			VillagePortals.Forget();
			VillageDisband.Forget();
			FarmLayout.Forget();
			ResourceBookGame.Forget();
			VillageLife.ForgetGoals();
			VillageQuests.Forget();
			DvergrVikingSpawns.Forget();
		}
	}
}
