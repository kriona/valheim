using System.Collections.Generic;
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
	/// What Zombie Apocalypse adds that's held back in a world whose server doesn't have it, besides its zombie viking
	/// </summary>
	internal static partial class CommandGuard
	{
		/// <summary>
		/// Names the poison cloud, the spirit spells and what they use, and the zombie viking raid - it has no console
		/// commands
		/// </summary>
		/// <param name="commandPrefixes">The starts of its commands' names, added to</param>
		/// <param name="prefabs">Its prefabs and items, added to</param>
		/// <param name="raids">Its raids, added to</param>
		static partial void AddOwned(List<string> commandPrefixes, List<string> prefabs, List<string> raids)
		{
			prefabs.Add(PoisonCloud.PrefabName);
			prefabs.AddRange(SpiritSpells.Names());
			raids.Add(VikingRaid.EventName);
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
