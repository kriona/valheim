using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The kri_vv_spawnviking console command - spawns PC or NPC vikings of a biome named, in front of the player, rather
	/// than of the village they join or the biome they stand in the way the game's spawn command has them
	/// </summary>
	/// <remarks>
	/// The biome's noted in a new viking's ZDO as it spawns, which its rolling-up goes by once it starts (see
	/// VikingCore.Generate). Near a village, they still join it, keeping the biome they were spawned with
	/// </remarks>
	internal static class VikingSpawnCommand
	{
		/// <summary>
		/// The command's name, the word for NPC vikings, and the most it spawns at once
		/// </summary>
		private const string Command = "kri_vv_spawnviking";
		private const string NPCWord = "npc";
		private const int MaxAmount = 20;

		/// <summary>
		/// The ZDO key on a spawned viking holding the biome it's rolled up for, by its index in VikingGear's tables - 0
		/// isn't kept, so the index is kept one higher
		/// </summary>
		public static readonly int BiomeKey = "kri_vv_spawn_biome".GetStableHashCode();

		/// <summary>
		/// The words for each biome, as the game names them and as players might
		/// </summary>
		internal static readonly Dictionary<string, Heightmap.Biome> BiomeWords = new Dictionary<string, Heightmap.Biome>
		{
			{ "meadows", Heightmap.Biome.Meadows },
			{ "blackforest", Heightmap.Biome.BlackForest },
			{ "forest", Heightmap.Biome.BlackForest },
			{ "swamp", Heightmap.Biome.Swamp },
			{ "mountain", Heightmap.Biome.Mountain },
			{ "mountains", Heightmap.Biome.Mountain },
			{ "plains", Heightmap.Biome.Plains },
			{ "mistlands", Heightmap.Biome.Mistlands },
			{ "ashlands", Heightmap.Biome.AshLands },
			{ "deepnorth", Heightmap.Biome.DeepNorth }
		};

		/// <summary>
		/// Adds the command to the console
		/// </summary>
		public static void Register()
		{
			new Terminal.ConsoleCommand(Command, "<biome> [amount] [" + NPCWord + "] - spawns PC vikings of a biome in front of you, or NPC vikings with " + NPCWord + " - meadows, blackforest, swamp, mountains, plains, mistlands, ashlands or deepnorth", Run, isCheat: true, optionsFetcher: Options);
		}

		/// <summary>
		/// Lists what the command can be given, for the console to complete
		/// </summary>
		/// <returns>The biomes' words and the word for NPC vikings</returns>
		private static List<string> Options()
		{
			List<string> options = new List<string> { "meadows", "blackforest", "swamp", "mountains", "plains", "mistlands", "ashlands", "deepnorth" };
			options.Add(NPCWord);
			return(options);
		}

		/// <summary>
		/// Finds the biome a word names, ignoring case, spaces, dashes and underscores
		/// </summary>
		/// <param name="word">The word</param>
		/// <returns>The biome, or None when it names none</returns>
		internal static Heightmap.Biome ParseBiome(string word)
		{
			string plain = (word ?? "").ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
			return((BiomeWords.TryGetValue(plain, out Heightmap.Biome biome) ? biome : Heightmap.Biome.None));
		}

		/// <summary>
		/// Gets the biome a viking's rolled up for - the one it was spawned with by the command, or otherwise its own
		/// </summary>
		/// <param name="zdo">The viking's ZDO</param>
		/// <param name="otherwise">Its biome by its village or where it stands, by its index in VikingGear's tables</param>
		/// <returns>The biome's index</returns>
		public static int BiomeFor(ZDO zdo, int otherwise)
		{
			int kept = ((zdo != null) ? zdo.GetInt(BiomeKey, 0) : 0);
			return(((kept > 0) ? Mathf.Clamp(kept - 1, 0, VikingGear.Biomes.Length - 1) : otherwise));
		}

		/// <summary>
		/// Runs the command
		/// </summary>
		/// <param name="args">The command line</param>
		private static void Run(Terminal.ConsoleEventArgs args)
		{
			Player player = Player.m_localPlayer;
			if (player == null || ZNetScene.instance == null)
			{
				args.Context.AddString("Load a world first");
				return;
			}
			Heightmap.Biome biome = Heightmap.Biome.None;
			int amount = 1;
			bool npc = false;
			for (int i = 1; i < args.Length; i++)
			{
				if (string.Equals(args[i], NPCWord, System.StringComparison.OrdinalIgnoreCase))
				{
					npc = true;
				}
				else if (int.TryParse(args[i], out int asked))
				{
					amount = Mathf.Clamp(asked, 1, MaxAmount);
				}
				else
				{
					biome = ParseBiome(args[i]);
				}
			}
			if (biome == Heightmap.Biome.None)
			{
				args.Context.AddString("Usage: " + Command + " <meadows|blackforest|swamp|mountains|plains|mistlands|ashlands|deepnorth> [amount] [" + NPCWord + "]");
				return;
			}
			GameObject prefab = ZNetScene.instance.GetPrefab((npc ? VikingCore.NPCPrefabHash : VikingCore.PCPrefabHash));
			if (prefab == null)
			{
				args.Context.AddString("Couldn't find the viking's prefab");
				return;
			}
			int index = VikingGear.IndexOf(biome);
			for (int i = 0; i < amount; i++)
			{
				GameObject viking = Object.Instantiate(prefab, SpawnPoint(player, i), Quaternion.LookRotation(-player.transform.forward));
				ZNetView view = viking.GetComponent<ZNetView>();
				if (view != null && view.IsValid())
				{
					view.GetZDO().Set(BiomeKey, index + 1);
				}
			}
			string message = "Spawned " + amount + " " + VikingGear.Get(index).Biome + " " + (npc ? "NPC" : "PC") + " viking" + ((amount == 1) ? "" : "s");
			Plugin.Log.LogInfo(message);
			args.Context.AddString(message);
		}

		/// <summary>
		/// Finds where a spawned viking appears - a little in front of the player, spread around when there are several,
		/// the way the game's spawn command spreads them
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="i">Which of them it is</param>
		/// <returns>The spot</returns>
		internal static Vector3 SpawnPoint(Player player, int i)
		{
			Vector3 ahead = player.transform.position + player.transform.forward * 2f + Vector3.up;
			if (i == 0)
			{
				return(ahead);
			}
			Vector2 spread = Random.insideUnitCircle * 1.5f;
			return(ahead + new Vector3(spread.x, 0f, spread.y));
		}
	}
}
