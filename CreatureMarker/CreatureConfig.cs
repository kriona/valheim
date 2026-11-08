using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using Kriona.Shared;
using UnityEngine;

namespace CreatureMarker
{
	/// <summary>
	/// The mod's config file - display settings, the All switches and one "Prefab = inherit/true/false ## Name" line
	/// per creature, deciding which creatures get a marker and how it looks
	/// </summary>
	/// <remarks>
	/// BepInEx's config format spends several lines on every entry, so the mod keeps its own file. The file is read
	/// and rewritten when a world's ZNetScene starts, which adds every creature the game knows about, including ones
	/// added by other mods, while keeping the values already set, and read again whenever it is saved while a world is
	/// loaded. Lines for creatures that are no longer loaded are kept so their values survive a mod being removed for a
	/// while
	///
	/// Every setting and creature line is also mirrored as an entry in the plugin's BepInEx config so Configuration
	/// Manager can list and change them, and a change made there is written to this file
	/// </remarks>
	internal static class CreatureConfig
	{
		private const string FileName = Plugin.PluginGuid + ".cfg";
		private const string Comment = "##";

		private const string MarkersSection = "1 - Markers";
		private const string ColorsSection = "2 - Colors";
		private const string CreaturesSection = "3 - Creatures and Fish";

		private const string ShowOnScreenKey = "Show On Screen";
		private const string ShowOnMapKey = "Show On Map";
		private const string MaxDistanceKey = "Max Distance";
		private const string FarDistanceKey = "Far Distance";
		private const string FarScaleKey = "Far Scale";
		private const string BobHeightKey = "Bob Height";
		private const string ShowNameKey = "Show Name";
		private const string ShowDistanceKey = "Show Distance";
		private const string ShowLevelKey = "Show Level";
		private const string PassiveColorKey = "Passive Color";
		private const string HostileColorKey = "Hostile Color";
		private const string AngryColorKey = "Angry Color";
		private const string BossColorKey = "Boss Color";
		private const string FishColorKey = "Fish Color";
		private const string PetColorKey = "Pet Color";
		private const string TraderColorKey = "Trader Color";
		private const string LeviathanColorKey = "Leviathan Color";
		private const string PlayerColorKey = "Player Color";
		private const string AllPassiveKey = "All Passive Creatures";

		/// <summary>
		/// The switch config files from before 1.6.0 have in place of All Passive Creatures, which marked every
		/// creature, trader, pet and bird, and which LoadSettings converts
		/// </summary>
		private const string OldAllCreaturesKey = "All Creatures";
		private const string AllHostileKey = "All Hostile Creatures";
		private const string AllFishKey = "All Fish";
		private const string AllBirdsKey = "All Birds";
		private const string AllLeviathansKey = "All Leviathans";
		private const string AllPlayersKey = "All Players";
		private const string MinCreatureLevelKey = "Min Creature Level";
		private const string MinFishLevelKey = "Min Fish Level";

		private const float DefaultMaxDistance = 50f;
		private const float MinMaxDistance = 5f;
		private const float MaxMaxDistance = 500f;
		private const float DefaultFarDistance = 50f;
		private const float DefaultFarScale = 0.66f;
		private const float MinFarScale = 0.2f;
		private const float DefaultBobHeight = 0f;
		private const float MaxBobHeight = 30f;
		private const string DefaultPassiveColor = "#40D940";
		private const string DefaultHostileColor = "#FFD91A";
		private const string DefaultAngryColor = "#FF801A";
		private const string DefaultBossColor = "#FF3333";
		private const string DefaultFishColor = "#33CCFF";
		private const string DefaultPetColor = "#FFF";
		private const string DefaultLeviathanColor = "#B366FF";
		private const string DefaultPlayerColor = "#FFF";
		private const int DefaultMinCreatureLevel = 0;
		private const int MaxMinCreatureLevel = 9;
		private const int DefaultMinFishLevel = 1;
		private const int MaxMinFishLevel = 5;

		private static readonly string[] header =
		{
			"## " + Plugin.PluginName,
			"## Changes take effect as soon as this file is saved; you don't need to restart Valheim or rejoin the world",
			"## Colors are #RRGGBB or a color name like red, yellow, green, cyan or magenta",
			"",
		};

		private static readonly string[] creatureHeader =
		{
			"",
			"## All Passive Creatures marks every creature that won't attack you, including traders and their pets",
			"## All Hostile Creatures marks every creature that will attack you",
			"## All Fish marks every fish",
			"## All Birds marks every bird",
			"## All Leviathans marks every leviathan",
			"## All Players marks every other player who is sharing their position on the map and isn't sneaking",
			"",
		};

		private static readonly string[] creatureLinesHeader =
		{
			"",
			"## Each creature, trader, pet, fish, bird and leviathan line is: Prefab = inherit/true/false ## Name in game",
			"## inherit follows the All switches, while true or false always shows or hides that one's marker",
			"",
		};

		private static readonly string showOnScreenNote = "Show an arrow above each marked creature, pinned to the screen edge when it is off-screen, default true";
		private static readonly string showOnMapNote = "Show a dot on the minimap and the large map for each marked creature, default true";
		private static readonly string showNameNote = "Show the creature's name with its marker, default false";
		private static readonly string showLevelNote = "Show stars after a creature's name and the level after a fish's name, like Perch[3], for those above the lowest level, default false";
		private static readonly string showDistanceNote = "Show the creature's distance in meters with its marker, after the name when Show Name is on, default false";
		private static readonly string maxDistanceNote = "Meters, from " + MinMaxDistance + " to " + MaxMaxDistance + ", default " + DefaultMaxDistance;
		private static readonly string farDistanceNote = "Meters beyond which a marker is drawn at Far Scale, from " + MinMaxDistance + " to " + MaxMaxDistance + ", default " + DefaultFarDistance;
		private static readonly string farScaleNote = "Size of a marker beyond Far Distance, from " + MinFarScale.ToString(CultureInfo.InvariantCulture) + " to 1 (no shrinking), default " + DefaultFarScale.ToString(CultureInfo.InvariantCulture);
		private static readonly string bobHeightNote = "Pixels an on-screen arrow bobs up and down, from 0 (disables bobbing) to " + MaxBobHeight + ", default " + DefaultBobHeight;
		private static readonly string passiveColorNote = "Creatures that won't attack you, including birds, default " + DefaultPassiveColor;
		private static readonly string hostileColorNote = "Creatures that will attack you, default " + DefaultHostileColor;
		private static readonly string angryColorNote = "Hostile creatures that are after you right now, default " + DefaultAngryColor;
		private static readonly string bossColorNote = "Bosses, default " + DefaultBossColor;
		private static readonly string fishColorNote = "Fish, default " + DefaultFishColor;
		private static readonly string petColorNote = "Creatures you have tamed, default " + DefaultPetColor;
		private static readonly string traderColorNote = "Traders like Haldor, Hildir and the Bog Witch, blank to use Passive Color, default blank";
		private static readonly string leviathanColorNote = "Leviathans, default " + DefaultLeviathanColor;
		private static readonly string playerColorNote = "Other players, default " + DefaultPlayerColor;
		private static readonly string minCreatureLevelNote = "Lowest level a creature needs to get a marker, which is its number of stars, from 0 (every creature) to " + MaxMinCreatureLevel + ", default " + DefaultMinCreatureLevel + ", traders, their pets, birds and leviathans aren't affected";
		private static readonly string minFishLevelNote ="Lowest level a fish needs to get a marker, even when its own line is true, from 1 (every fish) to " + MaxMinFishLevel + ", default " + DefaultMinFishLevel;

		/// <summary>
		/// Mirrors the settings as entries for Configuration Manager
		/// </summary>
		private static ConfigMirror mirror;

		/// <summary>
		/// The entries of the creature lines
		/// </summary>
		private static StateEntries creatureEntries;

		private static ConfigEntry<bool> showOnScreenEntry;
		private static ConfigEntry<bool> showOnMapEntry;
		private static ConfigEntry<bool> showNameEntry;
		private static ConfigEntry<bool> showLevelEntry;
		private static ConfigEntry<bool> showDistanceEntry;
		private static ConfigEntry<float> maxDistanceEntry;
		private static ConfigEntry<float> farDistanceEntry;
		private static ConfigEntry<float> farScaleEntry;
		private static ConfigEntry<float> bobHeightEntry;
		private static ConfigEntry<Color> passiveColorEntry;
		private static ConfigEntry<Color> hostileColorEntry;
		private static ConfigEntry<Color> angryColorEntry;
		private static ConfigEntry<Color> bossColorEntry;
		private static ConfigEntry<Color> fishColorEntry;
		private static ConfigEntry<Color> petColorEntry;

		/// <summary>
		/// Text rather than a color, since a blank value falls back to Passive Color
		/// </summary>
		private static ConfigEntry<string> traderColorEntry;
		private static ConfigEntry<Color> leviathanColorEntry;
		private static ConfigEntry<Color> playerColorEntry;
		private static ConfigEntry<bool> allPassiveEntry;
		private static ConfigEntry<bool> allHostileEntry;
		private static ConfigEntry<bool> allFishEntry;
		private static ConfigEntry<bool> allBirdsEntry;
		private static ConfigEntry<bool> allLeviathansEntry;
		private static ConfigEntry<bool> allPlayersEntry;
		private static ConfigEntry<int> minCreatureLevelEntry;
		private static ConfigEntry<int> minFishLevelEntry;

		/// <summary>
		/// Name token, or plain text that localizes to itself, by prefab name of the birds, leviathans, Odin and Valkyrie
		/// whose name isn't their prefab name split into words - the game gives them no name of their own
		/// </summary>
		private static readonly Dictionary<string, string> prefabNameTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			{ "Seagal", "$caption_seagull" },
			{ "LeviathanLava", "Lava Leviathan" },
			{ "odin", "Odin" },
			{ "Valkyrie", "$npc_valkyrie_end" },
		};

		/// <summary>
		/// Name token by prefab hash of the birds marked so far, so a bird's name is only worked out once
		/// </summary>
		private static readonly Dictionary<int, string> birdNamesByHash = new Dictionary<int, string>();

		/// <summary>
		/// Display name by prefab name of every creature line, sorted, as last read - blank when the name isn't known
		/// </summary>
		private static SortedDictionary<string, string> names = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Display name by prefab name of the traders, pets, leviathans, Odin, ravens and Valkyries seen in the world, which aren't all in the scene's prefab
		/// list, kept so their lines keep their names each time the file is read
		/// </summary>
		private static readonly Dictionary<string, string> npcNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Whether marked creatures get an arrow on screen
		/// </summary>
		public static bool ShowOnScreen { get; private set; } = true;

		/// <summary>
		/// Whether marked creatures get a dot on the minimap and the large map
		/// </summary>
		public static bool ShowOnMap { get; private set; } = true;

		/// <summary>
		/// Furthest a creature can be from the player, in meters, and still get a marker
		/// </summary>
		public static float MaxDistance { get; private set; } = DefaultMaxDistance;

		/// <summary>
		/// Distance from the player, in meters, beyond which markers are drawn at Far Scale
		/// </summary>
		public static float FarDistance { get; private set; } = DefaultFarDistance;

		/// <summary>
		/// Size of the arrow and label of a target beyond Far Distance, as a fraction of their full size
		/// </summary>
		public static float FarScale { get; private set; } = DefaultFarScale;

		/// <summary>
		/// How far, in pixels, an on-screen arrow bobs up and down, 0 disables bobbing
		/// </summary>
		public static float BobHeight { get; private set; } = DefaultBobHeight;

		/// <summary>
		/// Whether the creature's name is shown with its marker
		/// </summary>
		public static bool ShowName { get; private set; }

		/// <summary>
		/// Whether the creature's distance from the player is shown with its marker
		/// </summary>
		public static bool ShowDistance { get; private set; }

		/// <summary>
		/// Whether the creature's or fish's level is shown with its marker
		/// </summary>
		public static bool ShowLevel { get; private set; }

		private static string passiveColorText = DefaultPassiveColor;
		private static string hostileColorText = DefaultHostileColor;
		private static string angryColorText = DefaultAngryColor;
		private static string bossColorText = DefaultBossColor;
		private static string fishColorText = DefaultFishColor;
		private static string petColorText = DefaultPetColor;
		private static Color passiveColor = ParseColor(DefaultPassiveColor, Color.green);
		private static Color hostileColor = ParseColor(DefaultHostileColor, Color.yellow);
		private static Color angryColor = ParseColor(DefaultAngryColor, Color.red);
		private static Color bossColor = ParseColor(DefaultBossColor, Color.red);
		private static Color fishColor = ParseColor(DefaultFishColor, Color.cyan);
		private static Color petColor = ParseColor(DefaultPetColor, Color.white);
		private static string traderColorText = "";

		/// <summary>
		/// The trader color, or null when it is blank and Passive Color is used instead
		/// </summary>
		private static Color? traderColor;
		private static string leviathanColorText = DefaultLeviathanColor;
		private static Color leviathanColor = ParseColor(DefaultLeviathanColor, Color.magenta);
		private static string playerColorText = DefaultPlayerColor;
		private static Color playerColor = ParseColor(DefaultPlayerColor, Color.white);

		/// <summary>
		/// The marker color of Odin, the hooded figure who watches the player from a distance, his ravens, Hugin and
		/// Munin, and the Valkyrie who carries a player into a new world
		/// </summary>
		private static readonly Color odinColor = ParseColor("#888", Color.gray);
		private static bool allPassive;
		private static bool allHostile = true;
		private static bool allFish;
		private static bool allBirds;
		private static bool allLeviathans = true;
		private static bool allPlayers = true;

		/// <summary>
		/// Lowest number of stars a creature needs to get a marker
		/// </summary>
		private static int minCreatureLevel = DefaultMinCreatureLevel;

		/// <summary>
		/// Lowest level a fish needs to get a marker
		/// </summary>
		private static int minFishLevel = DefaultMinFishLevel;

		/// <summary>
		/// The true or false set on creature lines, by prefab hash, leaving out the ones set to inherit
		/// </summary>
		private static readonly Dictionary<int, bool> overrides = new Dictionary<int, bool>();

		/// <summary>
		/// The config file's full path
		/// </summary>
		public static string FilePath => Path.Combine(Paths.ConfigPath, FileName);

		/// <summary>
		/// Binds the settings as entries in the plugin's BepInEx config for Configuration Manager, and reads the config
		/// file so they and any creature lines already in it show its values before a world loads
		/// </summary>
		/// <param name="file">The plugin's BepInEx config</param>
		public static void Init(ConfigFile file)
		{
			mirror = new ConfigMirror(file, OnEntryChanged);
			creatureEntries = new StateEntries(mirror, "inherit follows the All switches, while true or false always shows or hides its marker");
			BindSettings();
			LoadWithoutScene();
		}

		/// <summary>
		/// Reads the config file again, writing it back with any new creatures when a world is loaded
		/// </summary>
		/// <remarks>
		/// Writing the file back changes it again, which reads it once more, finds nothing new and leaves it alone
		/// </remarks>
		public static void Reload()
		{
			if (ZNetScene.instance != null)
			{
				Load(ZNetScene.instance);
			}
			else
			{
				LoadWithoutScene();
			}
		}

		/// <summary>
		/// Reads the config file without a prefab list, taking the creature lines and their names from the file
		/// itself and leaving it unwritten
		/// </summary>
		private static void LoadWithoutScene()
		{
			if (!TryRead(out Dictionary<string, string> values, out Dictionary<string, string> notes))
			{
				return;
			}
			LoadSettings(values);
			SortedDictionary<string, string> fileNames = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (string key in values.Keys)
			{
				fileNames[key] = (notes.TryGetValue(key, out string note) ? note : "");
			}
			names = fileNames;
			SyncEntries(BuildStates((string key) => (values.TryGetValue(key, out string text) ? ParseState(text) : null)));
		}

		/// <summary>
		/// Reads the config file, logging a warning when it can't be read
		/// </summary>
		/// <param name="values">Set to the value text by key</param>
		/// <param name="notes">Set to the comment after the value by key, for the lines that have one</param>
		/// <returns>Whether the file could be read</returns>
		private static bool TryRead(out Dictionary<string, string> values, out Dictionary<string, string> notes)
		{
			try
			{
				values = Read(FilePath, out notes);
				return(true);
			}
			catch (IOException e)
			{
				Debug.LogWarning("[" + Plugin.PluginName + "] Couldn't read " + FilePath + ": " + e.Message);
				values = null;
				notes = null;
				return(false);
			}
		}

		/// <summary>
		/// Reads the config file, adds any prefabs with a Character component other than the player, a Trader, a
		/// Petable, a Fish, a RandomFlyingBird, a Leviathan, an Odin or a Valkyrie component, and the traders and pets
		/// seen in the world, and writes it back
		/// </summary>
		/// <param name="scene">The scene holding the prefab list</param>
		public static void Load(ZNetScene scene)
		{
			if (!TryRead(out Dictionary<string, string> values, out _))
			{
				return;
			}
			LoadSettings(values);
			SortedDictionary<string, string> names = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (GameObject prefab in scene.m_prefabs)
			{
				if (prefab == null)
				{
					continue;
				}
				Character character = prefab.GetComponent<Character>();
				if (character != null && !(character is Player))
				{
					names[prefab.name] = GetDisplayName(character.m_name);
					continue;
				}
				Trader trader = prefab.GetComponent<Trader>();
				if (trader != null)
				{
					names[prefab.name] = GetDisplayName(trader.m_name);
					continue;
				}
				Petable petable = prefab.GetComponent<Petable>();
				if (petable != null)
				{
					names[prefab.name] = GetDisplayName(petable.m_name);
					continue;
				}
				Fish fish = prefab.GetComponent<Fish>();
				if (fish != null)
				{
					names[prefab.name] = GetDisplayName(fish.m_name);
					continue;
				}
				if (prefab.GetComponent<RandomFlyingBird>() != null || prefab.GetComponent<Leviathan>() != null || prefab.GetComponent<Odin>() != null || prefab.GetComponent<Valkyrie>() != null)
				{
					names[prefab.name] = GetDisplayName(GetPrefabNameToken(prefab.name));
				}
			}
			foreach (KeyValuePair<string, string> npc in npcNames)
			{
				if (!names.ContainsKey(npc.Key))
				{
					names[npc.Key] = npc.Value;
				}
			}
			foreach (string key in values.Keys)
			{
				if (!names.ContainsKey(key))
				{
					names[key] = "";
				}
			}
			CreatureConfig.names = names;
			Dictionary<string, bool?> states = BuildStates((string key) => (values.TryGetValue(key, out string text) ? ParseState(text) : null));
			Write(FilePath, names, states);
			SyncEntries(states);
		}

		/// <summary>
		/// Gives a trader, pet, leviathan, Odin, raven or Valkyrie seen in the world a line in the config file when it
		/// doesn't have one with its name yet
		/// </summary>
		/// <remarks>
		/// Pets are part of the locations that place them rather than prefabs of their own, so their lines can only be
		/// added once one has been seen
		/// </remarks>
		/// <param name="key">The prefab name</param>
		/// <param name="nameToken">The name token</param>
		public static void AddNPC(string key, string nameToken)
		{
			string name = GetDisplayName(nameToken);
			if (npcNames.TryGetValue(key, out string known) && known == name)
			{
				return;
			}
			npcNames[key] = name;
			if (ZNetScene.instance == null || (names.TryGetValue(key, out string listed) && listed.Length > 0))
			{
				return;
			}
			Load(ZNetScene.instance);
			Plugin.Watcher?.Remember();
		}

		/// <summary>
		/// The value of every creature line and the overrides they give
		/// </summary>
		/// <remarks>
		/// The keys in names keep the prefabs' own casing, which the hashes need to match
		/// </remarks>
		/// <param name="getState">Gives a creature line's value by prefab name, null for inherit</param>
		/// <returns>Value by prefab name, null for inherit</returns>
		private static Dictionary<string, bool?> BuildStates(Func<string, bool?> getState)
		{
			Dictionary<string, bool?> states = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
			overrides.Clear();
			foreach (string key in names.Keys)
			{
				bool? state = getState(key);
				states[key] = state;
				if (state.HasValue)
				{
					overrides[key.GetStableHashCode()] = state.Value;
				}
			}
			return(states);
		}

		/// <summary>
		/// Binds an entry for each setting and All switch, in the order the file lists them
		/// </summary>
		private static void BindSettings()
		{
			int order = 100;
			showOnScreenEntry = BindSetting(MarkersSection, ShowOnScreenKey, true, showOnScreenNote, null, order--);
			showOnMapEntry = BindSetting(MarkersSection, ShowOnMapKey, true, showOnMapNote, null, order--);
			showNameEntry = BindSetting(MarkersSection, ShowNameKey, false, showNameNote, null, order--);
			showLevelEntry = BindSetting(MarkersSection, ShowLevelKey, false, showLevelNote, null, order--);
			showDistanceEntry = BindSetting(MarkersSection, ShowDistanceKey, false, showDistanceNote, null, order--);
			maxDistanceEntry = BindSetting(MarkersSection, MaxDistanceKey, DefaultMaxDistance, maxDistanceNote, new AcceptableValueRange<float>(MinMaxDistance, MaxMaxDistance), order--);
			farDistanceEntry = BindSetting(MarkersSection, FarDistanceKey, DefaultFarDistance, farDistanceNote, new AcceptableValueRange<float>(MinMaxDistance, MaxMaxDistance), order--);
			farScaleEntry = BindSetting(MarkersSection, FarScaleKey, DefaultFarScale, farScaleNote, new AcceptableValueRange<float>(MinFarScale, 1f), order--);
			bobHeightEntry = BindSetting(MarkersSection, BobHeightKey, DefaultBobHeight, bobHeightNote, new AcceptableValueRange<float>(0f, MaxBobHeight), order--);
			passiveColorEntry = BindSetting(ColorsSection, PassiveColorKey, ParseColor(DefaultPassiveColor, Color.green), passiveColorNote, null, order--);
			hostileColorEntry = BindSetting(ColorsSection, HostileColorKey, ParseColor(DefaultHostileColor, Color.yellow), hostileColorNote, null, order--);
			angryColorEntry = BindSetting(ColorsSection, AngryColorKey, ParseColor(DefaultAngryColor, Color.red), angryColorNote, null, order--);
			bossColorEntry = BindSetting(ColorsSection, BossColorKey, ParseColor(DefaultBossColor, Color.red), bossColorNote, null, order--);
			fishColorEntry = BindSetting(ColorsSection, FishColorKey, ParseColor(DefaultFishColor, Color.cyan), fishColorNote, null, order--);
			petColorEntry = BindSetting(ColorsSection, PetColorKey, ParseColor(DefaultPetColor, Color.white), petColorNote, null, order--);
			traderColorEntry = BindSetting(ColorsSection, TraderColorKey, "", traderColorNote, null, order--);
			leviathanColorEntry = BindSetting(ColorsSection, LeviathanColorKey, ParseColor(DefaultLeviathanColor, Color.magenta), leviathanColorNote, null, order--);
			playerColorEntry = BindSetting(ColorsSection, PlayerColorKey, ParseColor(DefaultPlayerColor, Color.white), playerColorNote, null, order--);
			allPassiveEntry = BindSetting(CreaturesSection, AllPassiveKey, false, "Mark every creature that won't attack you, including traders and their pets", null, order--);
			allHostileEntry = BindSetting(CreaturesSection, AllHostileKey, true, "Mark every creature that will attack you", null, order--);
			allFishEntry = BindSetting(CreaturesSection, AllFishKey, false, "Mark every fish", null, order--);
			allBirdsEntry = BindSetting(CreaturesSection, AllBirdsKey, false, "Mark every bird", null, order--);
			allLeviathansEntry = BindSetting(CreaturesSection, AllLeviathansKey, true, "Mark every leviathan", null, order--);
			allPlayersEntry = BindSetting(CreaturesSection, AllPlayersKey, true, "Mark every other player who is sharing their position on the map and isn't sneaking", null, order--);
			minCreatureLevelEntry = BindSetting(CreaturesSection, MinCreatureLevelKey, DefaultMinCreatureLevel, minCreatureLevelNote, new AcceptableValueRange<int>(0, MaxMinCreatureLevel), order--);
			minFishLevelEntry = BindSetting(CreaturesSection, MinFishLevelKey, DefaultMinFishLevel, minFishLevelNote, new AcceptableValueRange<int>(1, MaxMinFishLevel), order--);
		}

		/// <summary>
		/// Binds one setting's entry
		/// </summary>
		/// <param name="section">The Configuration Manager category</param>
		/// <param name="key">The setting's key, the same as in the file</param>
		/// <param name="defaultValue">The setting's default</param>
		/// <param name="description">Shown when hovering over the setting</param>
		/// <param name="range">The values the setting can take, or null for any</param>
		/// <param name="order">Position in its category, higher first</param>
		/// <returns>The entry</returns>
		private static ConfigEntry<T> BindSetting<T>(string section, string key, T defaultValue, string description, AcceptableValueBase range, int order)
		{
			return(mirror.Bind(section, key, defaultValue, description, range, order, null));
		}

		/// <summary>
		/// Gives the entries the values just read, binding entries for new creature lines and removing those of lines
		/// that are gone, without writing the file
		/// </summary>
		/// <param name="states">Value by prefab name, null for inherit</param>
		private static void SyncEntries(Dictionary<string, bool?> states)
		{
			if (mirror == null)
			{
				return;
			}
			mirror.Sync(() =>
			{
				showOnScreenEntry.Value = ShowOnScreen;
				showOnMapEntry.Value = ShowOnMap;
				showNameEntry.Value = ShowName;
				showLevelEntry.Value = ShowLevel;
				showDistanceEntry.Value = ShowDistance;
				maxDistanceEntry.Value = MaxDistance;
				farDistanceEntry.Value = FarDistance;
				farScaleEntry.Value = FarScale;
				bobHeightEntry.Value = BobHeight;
				passiveColorEntry.Value = passiveColor;
				hostileColorEntry.Value = hostileColor;
				angryColorEntry.Value = angryColor;
				bossColorEntry.Value = bossColor;
				fishColorEntry.Value = fishColor;
				petColorEntry.Value = petColor;
				traderColorEntry.Value = traderColorText;
				leviathanColorEntry.Value = leviathanColor;
				playerColorEntry.Value = playerColor;
				allPassiveEntry.Value = allPassive;
				allHostileEntry.Value = allHostile;
				allFishEntry.Value = allFish;
				allBirdsEntry.Value = allBirds;
				allLeviathansEntry.Value = allLeviathans;
				allPlayersEntry.Value = allPlayers;
				minCreatureLevelEntry.Value = minCreatureLevel;
				minFishLevelEntry.Value = minFishLevel;
				foreach (KeyValuePair<string, string> name in names)
				{
					creatureEntries.Set(CreaturesSection, name.Key, name.Value, states[name.Key]);
				}
				creatureEntries.RemoveUnset();
			});
		}

		/// <summary>
		/// Takes the values of the entries after one is changed in Configuration Manager and writes them to the file
		/// </summary>
		private static void OnEntryChanged()
		{
			ShowOnScreen = showOnScreenEntry.Value;
			ShowOnMap = showOnMapEntry.Value;
			ShowName = showNameEntry.Value;
			ShowLevel = showLevelEntry.Value;
			ShowDistance = showDistanceEntry.Value;
			MaxDistance = maxDistanceEntry.Value;
			FarDistance = farDistanceEntry.Value;
			FarScale = farScaleEntry.Value;
			BobHeight = bobHeightEntry.Value;
			passiveColorText = TakeEntryColor(passiveColorEntry, passiveColorText, ref passiveColor);
			hostileColorText = TakeEntryColor(hostileColorEntry, hostileColorText, ref hostileColor);
			angryColorText = TakeEntryColor(angryColorEntry, angryColorText, ref angryColor);
			bossColorText = TakeEntryColor(bossColorEntry, bossColorText, ref bossColor);
			fishColorText = TakeEntryColor(fishColorEntry, fishColorText, ref fishColor);
			petColorText = TakeEntryColor(petColorEntry, petColorText, ref petColor);
			traderColorText = ParseOptionalColor(TraderColorKey, traderColorEntry.Value, out traderColor);
			leviathanColorText = TakeEntryColor(leviathanColorEntry, leviathanColorText, ref leviathanColor);
			playerColorText = TakeEntryColor(playerColorEntry, playerColorText, ref playerColor);
			allPassive = allPassiveEntry.Value;
			allHostile = allHostileEntry.Value;
			allFish = allFishEntry.Value;
			allBirds = allBirdsEntry.Value;
			allLeviathans = allLeviathansEntry.Value;
			allPlayers = allPlayersEntry.Value;
			minCreatureLevel = minCreatureLevelEntry.Value;
			minFishLevel = minFishLevelEntry.Value;
			Dictionary<string, bool?> states = BuildStates((string key) => (creatureEntries.TryGetState(key, out bool? state) ? state : null));
			Write(FilePath, names, states);
			Plugin.Watcher?.Remember();
		}

		/// <summary>
		/// Takes a color entry's value, keeping the file's color text when the color is unchanged so a name like red
		/// stays as it is
		/// </summary>
		/// <param name="entry">The color's entry</param>
		/// <param name="text">The color text from the file</param>
		/// <param name="color">The color in use, set to the entry's without its alpha when it differs</param>
		/// <returns>The color text to write to the file</returns>
		private static string TakeEntryColor(ConfigEntry<Color> entry, string text, ref Color color)
		{
			Color value = entry.Value;
			value.a = 1f;
			if (value == color)
			{
				return(text);
			}
			color = value;
			return("#" + ColorUtility.ToHtmlStringRGB(value));
		}

		/// <summary>
		/// Fills the list with the living marked creatures, the other players sharing their position who aren't
		/// sneaking, the marked traders, pets, Odin and Valkyries, the marked ravens while they are landed, the marked fish
		/// and birds and the marked leviathans that haven't started diving, within Max Distance of the player
		/// </summary>
		/// <param name="player">The local player</param>
		/// <param name="marked">The list to fill, cleared first</param>
		public static void GetMarked(Player player, List<Target> marked)
		{
			marked.Clear();
			Vector3 playerPosition = player.transform.position;
			float maxDistanceSquared = MaxDistance * MaxDistance;
			foreach (Character character in Character.GetAllCharacters())
			{
				Vector3 position = character.transform.position;
				if (Vector3.SqrMagnitude(position - playerPosition) > maxDistanceSquared)
				{
					continue;
				}
				if (character.IsDead())
				{
					continue;
				}
				if (character is Player other)
				{
					if (allPlayers && other != player && !other.IsCrouching() && IsSharingPosition(other))
					{
						Target playerTarget = new Target();
						playerTarget.source = other;
						playerTarget.position = position;
						playerTarget.top = other.GetTopPoint();
						playerTarget.nameToken = other.GetHoverName();
						playerTarget.level = 1;
						playerTarget.color = playerColor;
						marked.Add(playerTarget);
					}
					continue;
				}
				int level = character.GetLevel();
				// The game's level is the star count plus 1, while Min Creature Level counts stars
				if (level - 1 < minCreatureLevel)
				{
					continue;
				}
				bool hostile = IsHostile(character, player);
				if (!IsMarked(character.m_nview, (hostile ? allHostile : allPassive)))
				{
					continue;
				}
				Target target = new Target();
				target.source = character;
				target.position = position;
				target.top = character.GetTopPoint();
				target.nameToken = character.GetHoverName();
				target.level = level;
				target.color = GetCreatureColor(character, hostile, player);
				marked.Add(target);
			}
			Color merchantColor = (traderColor ?? passiveColor);
			foreach (NPC npc in NpcList.Get())
			{
				Vector3 position = npc.source.transform.position;
				if (Vector3.SqrMagnitude(position - playerPosition) > maxDistanceSquared)
				{
					continue;
				}
				Leviathan leviathan = npc.source as Leviathan;
				if (!IsMarked(npc.hash, ((leviathan != null) ? allLeviathans : allPassive)))
				{
					continue;
				}
				if (leviathan != null && IsDiving(leviathan))
				{
					continue;
				}
				Raven raven = npc.source as Raven;
				if (raven != null && !raven.IsSpawned())
				{
					continue;
				}
				Target target = new Target();
				target.source = npc.source;
				target.position = position;
				target.top = position + Vector3.up * npc.GetHeight();
				target.nameToken = npc.nameToken;
				target.level = 1;
				if (leviathan != null)
				{
					target.color = leviathanColor;
				}
				else if (npc.source is Odin || raven != null || npc.source is Valkyrie)
				{
					target.color = odinColor;
				}
				else
				{
					target.color = ((npc.source is Trader) ? merchantColor : passiveColor);
				}
				marked.Add(target);
			}
			foreach (IMonoUpdater updater in Fish.Instances)
			{
				Fish fish = updater as Fish;
				if (fish == null)
				{
					continue;
				}
				Vector3 position = fish.transform.position;
				if (Vector3.SqrMagnitude(position - playerPosition) > maxDistanceSquared)
				{
					continue;
				}
				int level = ((fish.m_itemDrop != null) ? fish.m_itemDrop.m_itemData.m_quality : 1);
				if (level < minFishLevel || !IsMarked(fish.m_nview, allFish))
				{
					continue;
				}
				Target target = new Target();
				target.source = fish;
				target.position = position;
				target.top = position;
				target.nameToken = fish.GetHoverName();
				target.level = level;
				target.fish = true;
				target.color = fishColor;
				marked.Add(target);
			}
			foreach (IMonoUpdater updater in RandomFlyingBird.Instances)
			{
				RandomFlyingBird bird = updater as RandomFlyingBird;
				if (bird == null || bird.m_nview == null || !bird.m_nview.IsValid())
				{
					continue;
				}
				Vector3 position = bird.transform.position;
				if (Vector3.SqrMagnitude(position - playerPosition) > maxDistanceSquared)
				{
					continue;
				}
				int prefabHash = bird.m_nview.GetZDO().GetPrefab();
				if (!IsMarked(prefabHash, allBirds))
				{
					continue;
				}
				Target target = new Target();
				target.source = bird;
				target.position = position;
				target.top = position;
				target.nameToken = GetBirdNameToken(bird, prefabHash);
				target.level = 1;
				target.color = passiveColor;
				marked.Add(target);
			}
		}

		/// <summary>
		/// The name token of a bird in the world, worked out from its prefab name the first time a bird of that prefab
		/// is marked
		/// </summary>
		/// <param name="bird">The bird</param>
		/// <param name="prefabHash">The bird's prefab name hash</param>
		/// <returns>The name token</returns>
		private static string GetBirdNameToken(RandomFlyingBird bird, int prefabHash)
		{
			if (!birdNamesByHash.TryGetValue(prefabHash, out string token))
			{
				token = GetPrefabNameToken(Utils.GetPrefabName(bird.gameObject));
				birdNamesByHash[prefabHash] = token;
			}
			return(token);
		}

		/// <summary>
		/// The name token of a bird, leviathan, Odin or Valkyrie prefab, which the game gives no name of their own - the
		/// game's seagull caption for seagulls, Lava Leviathan for LeviathanLava, Odin for odin, the game's end-credits
		/// Valkyrie name for Valkyrie, and otherwise the prefab name split into words, like Ash Crow for AshCrow
		/// </summary>
		/// <param name="prefabName">The bird's, leviathan's, Odin's or Valkyrie's prefab name</param>
		/// <returns>The name token, or plain text that localizes to itself</returns>
		public static string GetPrefabNameToken(string prefabName)
		{
			if (prefabNameTokens.TryGetValue(prefabName, out string token))
			{
				return(token);
			}
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < prefabName.Length; i++)
			{
				char c = prefabName[i];
				if (c == '_')
				{
					c = ' ';
				}
				else if (i > 0 && char.IsUpper(c) && char.IsLower(prefabName[i - 1]))
				{
					builder.Append(' ');
				}
				builder.Append(c);
			}
			return(builder.ToString().Trim());
		}

		/// <summary>
		/// Whether a creature or fish should get a marker, from its own line when that is true or false, or else from
		/// what the All switches give it
		/// </summary>
		/// <param name="nview">The creature's or fish's network view, for its prefab</param>
		/// <param name="inherited">Whether the All switches mark it</param>
		/// <returns>True when it should get a marker</returns>
		private static bool IsMarked(ZNetView nview, bool inherited)
		{
			if (nview != null && nview.IsValid())
			{
				return(IsMarked(nview.GetZDO().GetPrefab(), inherited));
			}
			return(inherited);
		}

		/// <summary>
		/// Whether a prefab should get a marker, from its own line when that is true or false, or else from what the
		/// All switches give it
		/// </summary>
		/// <param name="prefabHash">The prefab name's hash</param>
		/// <param name="inherited">Whether the All switches mark it</param>
		/// <returns>True when it should get a marker</returns>
		private static bool IsMarked(int prefabHash, bool inherited)
		{
			if (overrides.TryGetValue(prefabHash, out bool value))
			{
				return(value);
			}
			return(inherited);
		}

		/// <summary>
		/// The marker color of a creature - Boss Color for bosses, Pet Color for tamed creatures, Angry Color for hostile
		/// creatures that are after the player, then Hostile Color or Passive Color
		/// </summary>
		/// <param name="character">The creature</param>
		/// <param name="hostile">Whether the creature will attack the player</param>
		/// <param name="player">The local player</param>
		/// <returns>The color</returns>
		private static Color GetCreatureColor(Character character, bool hostile, Player player)
		{
			if (character.IsBoss())
			{
				return(bossColor);
			}
			if (character.IsTamed())
			{
				return(petColor);
			}
			if (!hostile)
			{
				return(passiveColor);
			}
			return((IsAngry(character, player) ? angryColor : hostileColor));
		}

		/// <summary>
		/// Whether a hostile creature is after the player right now
		/// </summary>
		/// <remarks>
		/// Only the game that owns a creature runs its AI and knows which creature it is targeting. For a creature owned
		/// by another player, this reads the target that player's copy of the mod stores in the creature's ZDO, trusted
		/// only while the game's own synced state says the creature is alerted and has a target, since an owner without
		/// the mod leaves the last stored target behind. With no target stored, the owner doesn't have the mod, and any
		/// alerted creature with a target counts, which is also true when it is after another player or creature
		/// </remarks>
		/// <param name="character">The creature</param>
		/// <param name="player">The local player</param>
		/// <returns>True when the creature is targeting the player</returns>
		private static bool IsAngry(Character character, Player player)
		{
			MonsterAI ai = character.GetBaseAI() as MonsterAI;
			if (ai == null || !character.m_nview.IsValid())
			{
				return(false);
			}
			if (character.m_nview.IsOwner())
			{
				return(ai.m_targetCreature == player);
			}
			if (!ai.IsAlerted() || !ai.HaveTarget())
			{
				return(false);
			}
			ZDOID target = character.m_nview.GetZDO().GetZDOID(MonsterTargetPatch.TargetHash);
			if (target.IsNone())
			{
				return(true);
			}
			return(target == player.GetZDOID());
		}

		/// <summary>
		/// Whether another player is sharing their position on the map, going by the player list the server sends
		/// </summary>
		/// <param name="other">The other player</param>
		/// <returns>True when the player list has the player with their position public</returns>
		private static bool IsSharingPosition(Player other)
		{
			ZNet net = ZNet.instance;
			if (net == null)
			{
				return(false);
			}
			ZDOID id = other.GetZDOID();
			foreach (ZNet.PlayerInfo info in net.GetPlayerList())
			{
				if (info.m_characterID == id)
				{
					return(info.m_publicPosition);
				}
			}
			return(false);
		}

		/// <summary>
		/// Whether a leviathan has started diving, after being hit, and is about to disappear
		/// </summary>
		/// <param name="leviathan">The leviathan</param>
		/// <returns>True when it is diving, or its network view is gone</returns>
		private static bool IsDiving(Leviathan leviathan)
		{
			ZNetView nview = leviathan.m_nview;
			if (nview == null || !nview.IsValid())
			{
				return(true);
			}
			return(nview.GetZDO().GetBool(ZDOVars.s_dead));
		}

		/// <summary>
		/// Whether the creature has monster AI and currently counts as the player's enemy
		/// </summary>
		/// <remarks>
		/// Animals that only flee, like deer, have AnimalAI and are left out. Tamed creatures and Dvergr that haven't
		/// been provoked aren't enemies of the player, so they are left out too
		/// </remarks>
		/// <param name="character">The creature to check</param>
		/// <param name="player">The local player</param>
		/// <returns>True when the creature is hostile</returns>
		private static bool IsHostile(Character character, Player player)
		{
			return(character.GetBaseAI() is MonsterAI && BaseAI.IsEnemy(character, player));
		}

		/// <summary>
		/// Takes the settings and All switches out of the file values, using the defaults for any that are missing or don't parse
		/// </summary>
		/// <param name="values">Value text by key, as read from the file, left holding only the creature lines</param>
		private static void LoadSettings(Dictionary<string, string> values)
		{
			MaxDistance = TakeFloat(values, MaxDistanceKey, DefaultMaxDistance, MinMaxDistance, MaxMaxDistance);
			FarDistance = TakeFloat(values, FarDistanceKey, DefaultFarDistance, MinMaxDistance, MaxMaxDistance);
			FarScale = TakeFloat(values, FarScaleKey, DefaultFarScale, MinFarScale, 1f);
			BobHeight = TakeFloat(values, BobHeightKey, DefaultBobHeight, 0f, MaxBobHeight);
			passiveColorText = TakeColor(values, PassiveColorKey, DefaultPassiveColor, out passiveColor);
			hostileColorText = TakeColor(values, HostileColorKey, DefaultHostileColor, out hostileColor);
			angryColorText = TakeColor(values, AngryColorKey, DefaultAngryColor, out angryColor);
			bossColorText = TakeColor(values, BossColorKey, DefaultBossColor, out bossColor);
			fishColorText = TakeColor(values, FishColorKey, DefaultFishColor, out fishColor);
			petColorText = TakeColor(values, PetColorKey, DefaultPetColor, out petColor);
			traderColorText = ParseOptionalColor(TraderColorKey, TakeValue(values, TraderColorKey), out traderColor);
			leviathanColorText = TakeColor(values, LeviathanColorKey, DefaultLeviathanColor, out leviathanColor);
			playerColorText = TakeColor(values, PlayerColorKey, DefaultPlayerColor, out playerColor);
			allLeviathans = (ParseState(TakeValue(values, AllLeviathansKey)) ?? true);
			allPlayers = (ParseState(TakeValue(values, AllPlayersKey)) ?? true);
			string oldAllCreaturesText = TakeValue(values, OldAllCreaturesKey);
			string allPassiveText = TakeValue(values, AllPassiveKey);
			string allBirdsText = TakeValue(values, AllBirdsKey);
			allPassive = (ParseState(allPassiveText) ?? false);
			allHostile = (ParseState(TakeValue(values, AllHostileKey)) ?? true);
			allFish = (ParseState(TakeValue(values, AllFishKey)) ?? false);
			allBirds = (ParseState(allBirdsText) ?? false);
			if (allPassiveText == null && ParseState(oldAllCreaturesText) == true)
			{
				// All Creatures from a config file before 1.6.0 marked every creature, trader, pet and bird, so it turns
				// on every switch that covers them
				allPassive = true;
				allHostile = true;
				if (allBirdsText == null)
				{
					allBirds = true;
				}
			}
			minCreatureLevel = TakeInt(values, MinCreatureLevelKey, DefaultMinCreatureLevel, 0, MaxMinCreatureLevel);
			minFishLevel = TakeInt(values, MinFishLevelKey, DefaultMinFishLevel, 1, MaxMinFishLevel);
			ShowName = (ParseState(TakeValue(values, ShowNameKey)) ?? false);
			ShowDistance = (ParseState(TakeValue(values, ShowDistanceKey)) ?? false);
			ShowLevel = (ParseState(TakeValue(values, ShowLevelKey)) ?? false);
			ShowOnScreen = (ParseState(TakeValue(values, ShowOnScreenKey)) ?? true);
			ShowOnMap = (ParseState(TakeValue(values, ShowOnMapKey)) ?? true);
		}

		/// <summary>
		/// Takes a number setting out of the file values, kept within its range
		/// </summary>
		/// <param name="values">Value text by key, as read from the file</param>
		/// <param name="key">The setting's key</param>
		/// <param name="defaultValue">Value to use when the setting is missing or doesn't parse</param>
		/// <param name="min">Lowest allowed value</param>
		/// <param name="max">Highest allowed value</param>
		/// <returns>The value</returns>
		private static float TakeFloat(Dictionary<string, string> values, string key, float defaultValue, float min, float max)
		{
			string text = TakeValue(values, key);
			if (text == null)
			{
				return(defaultValue);
			}
			if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
			{
				Warn(key, text);
				return(defaultValue);
			}
			return(Mathf.Clamp(value, min, max));
		}

		/// <summary>
		/// Takes a whole number setting out of the file values, kept within its range
		/// </summary>
		/// <param name="values">Value text by key, as read from the file</param>
		/// <param name="key">The setting's key</param>
		/// <param name="defaultValue">Value to use when the setting is missing or doesn't parse</param>
		/// <param name="min">Lowest allowed value</param>
		/// <param name="max">Highest allowed value</param>
		/// <returns>The value</returns>
		private static int TakeInt(Dictionary<string, string> values, string key, int defaultValue, int min, int max)
		{
			string text = TakeValue(values, key);
			if (text == null)
			{
				return(defaultValue);
			}
			if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
			{
				Warn(key, text);
				return(defaultValue);
			}
			return(Mathf.Clamp(value, min, max));
		}

		/// <summary>
		/// Takes a color setting out of the file values
		/// </summary>
		/// <param name="values">Value text by key, as read from the file</param>
		/// <param name="key">The setting's key</param>
		/// <param name="defaultText">Color text to use when the setting is missing or doesn't parse</param>
		/// <param name="color">The parsed color</param>
		/// <returns>The color text to write back to the file</returns>
		private static string TakeColor(Dictionary<string, string> values, string key, string defaultText, out Color color)
		{
			string text = TakeValue(values, key);
			if (text != null && ColorUtility.TryParseHtmlString(text, out color))
			{
				return(text);
			}
			if (text != null)
			{
				Warn(key, text);
			}
			color = ParseColor(defaultText, Color.white);
			return(defaultText);
		}

		/// <summary>
		/// Parses a color setting that can be left blank, logging a warning when it doesn't parse
		/// </summary>
		/// <param name="key">The setting's key</param>
		/// <param name="text">The color text, or null or blank for none</param>
		/// <param name="color">The parsed color, or null when the text is blank or doesn't parse</param>
		/// <returns>The color text to write back to the file, blank when it is blank or doesn't parse</returns>
		private static string ParseOptionalColor(string key, string text, out Color? color)
		{
			color = null;
			if (string.IsNullOrWhiteSpace(text))
			{
				return("");
			}
			text = text.Trim();
			if (!ColorUtility.TryParseHtmlString(text, out Color parsed))
			{
				Warn(key, text);
				return("");
			}
			color = parsed;
			return(text);
		}

		/// <summary>
		/// Removes a setting's value from the file values
		/// </summary>
		/// <param name="values">Value text by key, as read from the file</param>
		/// <param name="key">The setting's key</param>
		/// <returns>The value text, or null when the file doesn't have the setting</returns>
		private static string TakeValue(Dictionary<string, string> values, string key)
		{
			if (!values.TryGetValue(key, out string text))
			{
				return(null);
			}
			values.Remove(key);
			return(text);
		}

		/// <summary>
		/// Parses a true/false/inherit value
		/// </summary>
		/// <param name="text">The value text, or null</param>
		/// <returns>The value, or null for inherit, a missing value or one that doesn't parse</returns>
		private static bool? ParseState(string text)
		{
			if (text != null && bool.TryParse(text, out bool value))
			{
				return(value);
			}
			return(null);
		}

		/// <summary>
		/// Parses an HTML color string
		/// </summary>
		/// <param name="text">#RRGGBB or a color name</param>
		/// <param name="fallback">Color to use when the text doesn't parse</param>
		/// <returns>The color</returns>
		private static Color ParseColor(string text, Color fallback)
		{
			return((ColorUtility.TryParseHtmlString(text, out Color color) ? color : fallback));
		}

		/// <summary>
		/// Logs a setting that couldn't be parsed and is being reset to its default
		/// </summary>
		/// <param name="key">The setting's key</param>
		/// <param name="text">The value text that didn't parse</param>
		private static void Warn(string key, string text)
		{
			Debug.LogWarning("[" + Plugin.PluginName + "] Couldn't read " + key + " = " + text + ", using the default");
		}

		/// <summary>
		/// Reads the key and value text of every "Key = value" line, ignoring blank lines, and the comment after ## on
		/// those lines
		/// </summary>
		/// <param name="path">Path of the config file</param>
		/// <param name="notes">Set to the comment after the value by key, for the lines that have one</param>
		/// <returns>Value text by key, empty when the file doesn't exist</returns>
		private static Dictionary<string, string> Read(string path, out Dictionary<string, string> notes)
		{
			Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (!File.Exists(path))
			{
				return(values);
			}
			foreach (string line in File.ReadAllLines(path))
			{
				string text = line;
				string note = "";
				int comment = text.IndexOf(Comment, StringComparison.Ordinal);
				if (comment >= 0)
				{
					note = text.Substring(comment + Comment.Length).Trim();
					text = text.Substring(0, comment);
				}
				int equals = text.IndexOf('=');
				if (equals <= 0)
				{
					continue;
				}
				string key = text.Substring(0, equals).Trim();
				if (key.Length > 0)
				{
					values[key] = text.Substring(equals + 1).Trim();
					if (note.Length > 0)
					{
						notes[key] = note;
					}
				}
			}
			return(values);
		}

		/// <summary>
		/// Writes the settings, the All switches and one line per creature, leaving the file untouched when nothing has changed
		/// </summary>
		/// <param name="path">Path of the config file</param>
		/// <param name="names">Display name by prefab name, sorted, blank for prefabs that aren't loaded</param>
		/// <param name="states">Value by prefab name, null for inherit</param>
		private static void Write(string path, SortedDictionary<string, string> names, Dictionary<string, bool?> states)
		{
			StringBuilder builder = new StringBuilder();
			AppendLines(builder, header);
			AppendLine(builder, ShowOnScreenKey, StateEntries.Format(ShowOnScreen), showOnScreenNote);
			AppendLine(builder, ShowOnMapKey, StateEntries.Format(ShowOnMap), showOnMapNote);
			builder.Append("\r\n");
			AppendLine(builder, ShowNameKey, StateEntries.Format(ShowName), showNameNote);
			AppendLine(builder, ShowLevelKey, StateEntries.Format(ShowLevel), showLevelNote);
			builder.Append("\r\n");
			AppendLine(builder, ShowDistanceKey, StateEntries.Format(ShowDistance), showDistanceNote);
			AppendLine(builder, MaxDistanceKey, MaxDistance.ToString(CultureInfo.InvariantCulture), maxDistanceNote);
			AppendLine(builder, FarDistanceKey, FarDistance.ToString(CultureInfo.InvariantCulture), farDistanceNote);
			AppendLine(builder, FarScaleKey, FarScale.ToString(CultureInfo.InvariantCulture), farScaleNote);
			builder.Append("\r\n");
			AppendLine(builder, BobHeightKey, BobHeight.ToString(CultureInfo.InvariantCulture), bobHeightNote);
			AppendLine(builder, PassiveColorKey, passiveColorText, passiveColorNote);
			AppendLine(builder, HostileColorKey, hostileColorText, hostileColorNote);
			AppendLine(builder, AngryColorKey, angryColorText, angryColorNote);
			AppendLine(builder, BossColorKey, bossColorText, bossColorNote);
			AppendLine(builder, FishColorKey, fishColorText, fishColorNote);
			AppendLine(builder, PetColorKey, petColorText, petColorNote);
			AppendLine(builder, TraderColorKey, traderColorText, traderColorNote);
			AppendLine(builder, LeviathanColorKey, leviathanColorText, leviathanColorNote);
			AppendLine(builder, PlayerColorKey, playerColorText, playerColorNote);
			AppendLines(builder, creatureHeader);
			AppendLine(builder, AllPassiveKey, StateEntries.Format(allPassive), "");
			AppendLine(builder, AllHostileKey, StateEntries.Format(allHostile), "");
			AppendLine(builder, AllFishKey, StateEntries.Format(allFish), "");
			AppendLine(builder, AllBirdsKey, StateEntries.Format(allBirds), "");
			AppendLine(builder, AllLeviathansKey, StateEntries.Format(allLeviathans), "");
			AppendLine(builder, AllPlayersKey, StateEntries.Format(allPlayers), "");
			AppendLine(builder, MinCreatureLevelKey, minCreatureLevel.ToString(CultureInfo.InvariantCulture), minCreatureLevelNote);
			AppendLine(builder, MinFishLevelKey, minFishLevel.ToString(CultureInfo.InvariantCulture), minFishLevelNote);
			AppendLines(builder, creatureLinesHeader);
			foreach (KeyValuePair<string, string> name in names)
			{
				AppendLine(builder, name.Key, StateEntries.Format(states[name.Key]), name.Value);
			}
			string contents = builder.ToString();
			try
			{
				if (File.Exists(path) && File.ReadAllText(path) == contents)
				{
					return;
				}
				File.WriteAllText(path, contents);
			}
			catch (IOException e)
			{
				Debug.LogWarning("[" + Plugin.PluginName + "] Couldn't write " + path + ": " + e.Message);
			}
		}

		/// <summary>
		/// Appends each line followed by CRLF
		/// </summary>
		/// <param name="builder">The file contents being built</param>
		/// <param name="lines">The lines to append</param>
		private static void AppendLines(StringBuilder builder, string[] lines)
		{
			foreach (string line in lines)
			{
				builder.Append(line).Append("\r\n");
			}
		}

		/// <summary>
		/// Appends a "Key = value ## note" line, leaving off the note when it is blank
		/// </summary>
		/// <param name="builder">The file contents being built</param>
		/// <param name="key">The key</param>
		/// <param name="value">The value text</param>
		/// <param name="note">Comment after the value, or blank for none</param>
		private static void AppendLine(StringBuilder builder, string key, string value, string note)
		{
			builder.Append(key).Append(" =");
			if (value.Length > 0)
			{
				builder.Append(" ").Append(value);
			}
			if (note.Length > 0)
			{
				builder.Append(" ").Append(Comment).Append(" ").Append(note);
			}
			builder.Append("\r\n");
		}

		/// <summary>
		/// The creature's, fish's or bird's name in the current language, falling back to its name token
		/// </summary>
		/// <param name="token">The prefab's name token</param>
		/// <returns>The name to show after the value</returns>
		private static string GetDisplayName(string token)
		{
			string name = token;
			if (Localization.instance != null)
			{
				name = Localization.instance.Localize(name);
			}
			// A line break or # would split the name across lines or into the comment parser
			return(name.Replace("\r", "").Replace("\n", " ").Replace("#", ""));
		}
	}
}
