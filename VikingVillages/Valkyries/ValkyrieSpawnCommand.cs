using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The kri_vv_spawnvalkyrie console command - spawns valkyries in front of the player, of a color named or at random
	/// the way the game's spawn command has them (see ValkyrieColor)
	/// </summary>
	internal static class ValkyrieSpawnCommand
	{
		/// <summary>
		/// The command's name, the word for a color picked at random, and the most it spawns at once
		/// </summary>
		private const string Command = "kri_vv_spawnvalkyrie";
		private const string RandomWord = "random";
		private const int MaxAmount = 10;

		/// <summary>
		/// How far in front of the player she appears, in meters - clear of her wings
		/// </summary>
		private const float Ahead = 6f;

		/// <summary>
		/// Adds the command to the console
		/// </summary>
		public static void Register()
		{
			new Terminal.ConsoleCommand(Command, "[color] [amount] - spawns valkyries in front of you, of a color - " + string.Join(", ", Words()) + " - or one at random each", Run, isCheat: true, optionsFetcher: Options);
		}

		/// <summary>
		/// Lists what the command can be given, for the console to complete
		/// </summary>
		/// <returns>The colors' words and the word for one at random</returns>
		private static List<string> Options()
		{
			List<string> options = Words();
			options.Add(RandomWord);
			return(options);
		}

		/// <summary>
		/// The word for each color, as one word - "sky blue" as "skyblue"
		/// </summary>
		/// <returns>The words, by the colors' indexes</returns>
		private static List<string> Words()
		{
			List<string> words = new List<string>();
			foreach (string name in ValkyrieColors.Names)
			{
				words.Add(name.Replace(" ", ""));
			}
			return(words);
		}

		/// <summary>
		/// Finds the color a word names among a list of colors' names, ignoring case, spaces, dashes and underscores
		/// </summary>
		/// <param name="word">The word</param>
		/// <param name="names">The colors' names, by their indexes</param>
		/// <returns>The color's index, or -1 when it names none</returns>
		internal static int ParseColor(string word, string[] names)
		{
			string plain = Plain(word);
			for (int i = 0; i < names.Length; i++)
			{
				if (Plain(names[i]) == plain)
				{
					return(i);
				}
			}
			return(-1);
		}

		/// <summary>
		/// A word in lowercase without spaces, dashes or underscores
		/// </summary>
		/// <param name="word">The word</param>
		/// <returns>The plain word</returns>
		private static string Plain(string word)
		{
			return((word ?? "").ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", ""));
		}

		/// <summary>
		/// Runs the command
		/// </summary>
		/// <param name="args">The command line</param>
		private static void Run(Terminal.ConsoleEventArgs args)
		{
			Player player = Player.m_localPlayer;
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(ValkyriePrefab.PrefabName) : null);
			if (player == null || prefab == null)
			{
				args.Context.AddString("Load a world first");
				return;
			}
			int color = -1;
			int amount = 1;
			for (int i = 1; i < args.Length; i++)
			{
				if (int.TryParse(args[i], out int asked))
				{
					amount = Mathf.Clamp(asked, 1, MaxAmount);
				}
				else if (!string.Equals(args[i], RandomWord, System.StringComparison.OrdinalIgnoreCase))
				{
					color = ParseColor(args[i], ValkyrieColors.Names);
					if (color < 0)
					{
						args.Context.AddString("Usage: " + Command + " [" + string.Join("|", Words()) + "|" + RandomWord + "] [amount]");
						return;
					}
				}
			}
			Vector3 forward = player.transform.forward;
			forward.y = 0f;
			forward = ((forward.sqrMagnitude > 0.01f) ? forward.normalized : Vector3.forward);
			for (int i = 0; i < amount; i++)
			{
				// Several stand side by side, a wingspan's half apart
				Vector3 side = Vector3.Cross(Vector3.up, forward) * ((i - (amount - 1) * 0.5f) * 4f);
				Vector3 at = player.transform.position + forward * Ahead + side + Vector3.up;
				GameObject valkyrie = Object.Instantiate(prefab, at, Quaternion.LookRotation(-forward));
				ZNetView view = valkyrie.GetComponent<ZNetView>();
				if (color >= 0 && view != null && view.IsValid())
				{
					// She picked her own color as she woke, which this takes the place of
					view.GetZDO().Set(ValkyrieColor.ColorKey, color);
				}
			}
			string message = "Spawned " + amount + " " + ((color >= 0) ? ValkyrieColors.Names[color] : "random") + " valkyrie" + ((amount == 1) ? "" : "s");
			Plugin.Log.LogInfo(message);
			args.Context.AddString(message);
		}
	}
}
