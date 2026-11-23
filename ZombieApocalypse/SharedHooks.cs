using ZombieApocalypse;

namespace Kriona.Vikings
{
	/// <summary>
	/// What Zombie Apocalypse adds to each world once its zombie viking prefab is in - its poison cloud and spirit spell
	/// prefabs, the zombie viking in the draugr's body piles, and the message that a viking has used a forsaken power
	/// </summary>
	internal static partial class VikingPrefabPatch
	{
		/// <summary>
		/// Adds the mod's other prefabs and messages once the zombie viking's prefab is in
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		static partial void AfterRegister(ZNetScene scene)
		{
			PoisonCloud.Register(scene);
			SpiritSpells.Register(scene);
			VikingSpawns.AddToBodyPiles(scene);
			ZRoutedRpc.instance?.Register<long>(VikingCore.PowerRpc, VikingCore.OnPowerUsed);
		}
	}

	/// <summary>
	/// What Zombie Apocalypse forgets as it leaves a world
	/// </summary>
	internal static partial class ServerCheck
	{
		/// <summary>
		/// Forgets the forsaken power's cooldown, which is in the world's time
		/// </summary>
		static partial void OnLeave()
		{
			VikingCore.ResetPowerCooldown();
		}
	}
}
