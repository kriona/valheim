using VikingVillages;

namespace Kriona.Vikings
{
	/// <summary>
	/// What Viking Villages forgets as it leaves a world
	/// </summary>
	internal static partial class ServerCheck
	{
		/// <summary>
		/// Forgets what was noted in the world - the villages' timers, what was read from them and the server's answers
		/// about them, which are by the world's ZDOs
		/// </summary>
		static partial void OnLeave()
		{
			VillageRespawn.Forget();
			VillageRecords.Forget();
			VillageLocations.Forget();
			VillagePortals.Forget();
			FarmLayout.Forget();
		}
	}
}
