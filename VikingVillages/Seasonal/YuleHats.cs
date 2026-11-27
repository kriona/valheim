using System;
using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Shows every helmet a PC or dvergr viking wears as a Yule hat on Christmas Day, by the date on the computer it
	/// shows on - all but a Dverger circlet, which stays as it is. Players, zombie vikings, armor stands and creatures
	/// keep their helmets. Only how the helmet looks changes: what's worn, its armor and what's sent to other clients
	/// stay the game's own. The Command console command, left out of the console's help, shows or hides the hats
	/// whatever the date, for testing
	/// </summary>
	internal static class YuleHats
	{
		/// <summary>
		/// Whether the hats go by the date, or show or stay hidden whatever it is
		/// </summary>
		internal enum Mode
		{
			Date,
			On,
			Off
		}

		/// <summary>
		/// The console command, and the words it takes
		/// </summary>
		public const string Command = "kri_vv_yule";
		private const string OnWord = "on";
		private const string OffWord = "off";
		private const string DateWord = "date";

		/// <summary>
		/// The item hashes of the Yule hat, shown in place of a helmet, and of the Dverger circlet, which stays as it is
		/// </summary>
		internal static readonly int YuleHash = "HelmetYule".GetStableHashCode();
		internal static readonly int CircletHash = "HelmetDverger".GetStableHashCode();

		/// <summary>
		/// Seconds between looks at the date
		/// </summary>
		private const float DateCheckSeconds = 60f;

		private static Mode mode = Mode.Date;
		private static bool christmas;
		private static float nextDateCheck = -1f;

		/// <summary>
		/// Checks whether a date is Christmas Day
		/// </summary>
		/// <param name="date">The date</param>
		/// <returns>True on the 25th of December</returns>
		internal static bool IsChristmas(DateTime date)
		{
			return(date.Month == 12 && date.Day == 25);
		}

		/// <summary>
		/// Works out whether the hats show
		/// </summary>
		/// <param name="mode">Whether they go by the date or are turned on or off</param>
		/// <param name="christmas">Whether it's Christmas Day</param>
		/// <returns>True when they show</returns>
		internal static bool Shows(Mode mode, bool christmas)
		{
			return(mode == Mode.On || (mode == Mode.Date && christmas));
		}

		/// <summary>
		/// Gets the helmet to show for the one worn - the Yule hat while the hats show, unless nothing or a Dverger
		/// circlet is worn
		/// </summary>
		/// <param name="hash">The item hash of the helmet worn, or 0 for none</param>
		/// <param name="shows">Whether the hats show</param>
		/// <returns>The item hash of the helmet to show</returns>
		internal static int HatFor(int hash, bool shows)
		{
			if (!shows || hash == 0 || hash == CircletHash)
			{
				return(hash);
			}
			return(YuleHash);
		}

		/// <summary>
		/// Gets the helmet a character's equipment shows for the one it wears - the Yule hat in its place while the hats
		/// show, on a PC or dvergr viking only
		/// </summary>
		/// <param name="visual">The character's equipment</param>
		/// <param name="hash">The item hash of the helmet worn, or 0 for none</param>
		/// <returns>The item hash of the helmet to show</returns>
		public static int Swap(VisEquipment visual, int hash)
		{
			// The date is looked at first, so the rest of the year no character is looked up
			if (hash == 0 || !Showing() || !VikingCore.IsLivingViking(visual.GetComponent<Character>()))
			{
				return(hash);
			}
			return(HatFor(hash, true));
		}

		/// <summary>
		/// Checks whether the hats show now, looking at the date every DateCheckSeconds - and only once the game has
		/// the Yule hat to show
		/// </summary>
		/// <returns>True when they show</returns>
		private static bool Showing()
		{
			if (mode == Mode.Date && Time.realtimeSinceStartup >= nextDateCheck)
			{
				christmas = IsChristmas(DateTime.Now);
				nextDateCheck = Time.realtimeSinceStartup + DateCheckSeconds;
			}
			return(Shows(mode, christmas) && ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(YuleHash) != null);
		}

		/// <summary>
		/// Adds the command to the console, kept out of its help
		/// </summary>
		public static void Register()
		{
			new Terminal.ConsoleCommand(Command, "[" + OnWord + "|" + OffWord + "|" + DateWord + "] - shows or hides the Yule hats, or has them go by the date again - toggles them with no word", Run, isSecret: true, optionsFetcher: () => new List<string> { OnWord, OffWord, DateWord });
		}

		/// <summary>
		/// Runs the command - on or off turns the hats on or off whatever the date, date has them go by it again, and no
		/// word turns them off while they show and on while they don't
		/// </summary>
		/// <param name="args">The command line</param>
		private static void Run(Terminal.ConsoleEventArgs args)
		{
			string word = ((args.Length > 1) ? args[1].ToLowerInvariant() : "");
			if (word == OnWord)
			{
				mode = Mode.On;
			}
			else if (word == OffWord)
			{
				mode = Mode.Off;
			}
			else if (word == DateWord)
			{
				mode = Mode.Date;
				nextDateCheck = -1f;
			}
			else if (word.Length == 0)
			{
				mode = (Showing() ? Mode.Off : Mode.On);
			}
			else
			{
				args.Context.AddString("Usage: " + Command + " [" + OnWord + "|" + OffWord + "|" + DateWord + "]");
				return;
			}
			string state = ((mode == Mode.Date) ? "go by the date" : (mode == Mode.On ? "on" : "off"));
			args.Context.AddString("Yule hats " + state + " - they're " + (Showing() ? "showing" : "hidden"));
		}
	}
}
