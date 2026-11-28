using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Kriona.Vikings;
using Splatform;

namespace VikingVillages
{
	/// <summary>
	/// The names players give PC vikings, the way they name a tamed creature - kept in the viking's ZDO with who
	/// named it, so the name is filtered for that player's words wherever it shows, over its health bar, when it's
	/// looked at and when other vikings talk to it - and the names a viking won't take: a creature's, a legend's, a
	/// place's, a player's who has been in the world, another viking's of its village, or text markup
	/// </summary>
	internal static class VikingNames
	{
		/// <summary>
		/// On a PC viking, the name a player gave it and who gave it, by platform user ID or "host"
		/// </summary>
		private static readonly int NameKey = VikingName.NameKey;
		private static readonly int AuthorKey = VikingName.AuthorKey;

		/// <summary>
		/// The most characters a name can have - room for a name and a byname, like "Alfric Stormbreaker"
		/// </summary>
		public const int MaxLength = 24;

		/// <summary>
		/// What a viking makes of a name it's given - it takes it, or it turns it down as no name at all, for "Viking" or
		/// nothing, which would take its own name away, a creature's, a legend's - a god's, a boss's or someone the
		/// players meet - a place's - a biome or somewhere from the lore - someone else's in the world or the village, or
		/// text markup rather than a name
		/// </summary>
		public enum Verdict
		{
			Taken,
			Nameless,
			Creature,
			Legend,
			Place,
			Someone,
			Markup
		}

		/// <summary>
		/// The start of the global keys that note the players who have been in the world, each followed by a player's
		/// name in lowercase, written in hex so it's a single word
		/// </summary>
		private const string PlayerKeyPrefix = "kriona_za_player_";

		/// <summary>
		/// The legends a viking won't be named after - the gods, giants, beasts and others of the myths, the figures of
		/// the game's world and its legendary weapons, by name, as well as the bosses and traders found among the game's
		/// creatures
		/// </summary>
		private static readonly string[] Legends =
		{
			"Odin", "Oden", "Thor", "Loki", "Freya", "Freyja", "Frigg", "Baldr", "Baldur", "Balder", "Heimdall", "Tyr",
			"Hel", "Ymir", "Hugin", "Munin", "Valkyrie", "Haldor", "Hildir", "Halstein", "Bog Witch", "Eikthyr",
			"The Elder", "Elder", "Bonemass", "Moder", "Yagluth", "The Queen", "Queen", "Fader",
			"Vidar", "Sif", "Nanna", "Gerd", "Eira", "Frey", "Freyr", "Njord", "Bragi", "Idun", "Idunn", "Hod", "Hodr",
			"Forseti", "Ull", "Ullr", "Vali", "Vili", "Ve", "Hoenir", "Mimir", "Kvasir", "Skadi", "Sigyn", "Gefjon", "Saga",
			"Fulla", "Snotra", "Hlin", "Jord", "Sol", "Mani", "Nott",
			"Fenrir", "Jormungandr", "Sleipnir", "Surt", "Surtr", "Surtur", "Aegir", "Ran", "Angrboda", "Nidhogg",
			"Nidhögg", "Ratatoskr", "Urd", "Verdandi", "Skuld",
			"Dyrnwyn", "Himminafl"
		};

		/// <summary>
		/// The places a viking won't be named after - the game's biomes, by name, as well as in this game's language, and
		/// the realms and places of the lore and the game's own world
		/// </summary>
		private static readonly string[] Places =
		{
			"Meadows", "Black Forest", "Swamp", "Mountain", "Mountains", "Plains", "Ocean", "Mistlands", "Ashlands",
			"Deep North", "Valhalla", "Asgard", "Midgard", "Helheim", "Jotunheim", "Niflheim", "Muspelheim", "Alfheim",
			"Vanaheim", "Svartalfheim", "Bifrost", "Yggdrasil", "Valheim"
		};

		/// <summary>
		/// The names a new PC viking is given, picked at random for whether it's a woman or a man (see GiveName) -
		/// among them the names carved on the game's runestones, which a viking talks about having (see
		/// VikingLoreSpeech.NamesakeLines)
		/// </summary>
		internal static readonly string[] MaleNames =
		{
			"Åke", "Alrik", "Arne", "Asger", "Birger", "Bo", "Bram", "Brand", "Knud", "Dag", "Egil", "Einar", "Erik",
			"Erling", "Eyvind", "Frode", "Gorm", "Gunnar", "Håkon", "Halfdan", "Halvar", "Harald", "Hjalmar",
			"Ivar", "Jarl", "Kari", "Kjell", "Leif", "Magnus", "Nils", "Olaf", "Orm", "Ove", "Ragnar", "Rolf", "Sigurd",
			"Sindre", "Snorri", "Sten", "Stig", "Sven", "Torsten", "Trygve", "Ulf", "Ulfric", "Valdemar", "Viggo",
			"Grettir", "Njal", "Gisli", "Thorfinn", "Ketil", "Hallvard", "Ragnvald", "Torvald", "Asmund", "Ottar", "Ulfar",
			"Steinar", "Geir", "Grim", "Hauk", "Orvar", "Svein", "Bard", "Toke", "Skarde",
			"Agnar", "Alf", "Ari", "Arnbjorn", "Arnfinn", "Arngrim", "Arnkel", "Arnljot", "Arnor", "Arnulf", "Asbjorn",
			"Asgeir", "Askel", "Aslak", "Asvald", "Atli", "Audun", "Bersi", "Bjarki", "Bjarni", "Bjørn", "Bodvar", "Bolli",
			"Brodd", "Brynjolf", "Bui", "Dagfinn", "Eigil", "Eilif", "Eindride", "Eldgrim", "Eskil", "Eyjolf", "Eystein",
			"Finn", "Finnbogi", "Flosi", "Fridleif", "Gamli", "Gaut", "Gellir", "Gest", "Gizur", "Godfred", "Grimkel",
			"Grimolf", "Gudbrand", "Gudleif", "Gudmund", "Gunnbjorn", "Gunnlaug", "Gunnstein", "Guthorm", "Gyrd", "Hafgrim",
			"Haki", "Hallbjorn", "Hallfred", "Hallkel", "Harek", "Hedin", "Helgi", "Hemming", "Hergils", "Herjolf", "Hjalti",
			"Hjorleif", "Hogni", "Holmstein", "Hord", "Hoskuld", "Hrafn", "Hrafnkel", "Hrani", "Hrapp", "Hroald", "Hromund",
			"Hrut", "Hvitserk", "Illugi", "Ingimar", "Ingimund", "Ingjald", "Ingolf", "Ingvar", "Isleif", "Jökull", "Jostein",
			"Kalf", "Ketilbjorn", "Kjartan", "Kodran", "Kol", "Kolbein", "Kolgrim", "Kolskegg", "Kormak", "Lambi", "Ljot",
			"Oddbjorn", "Ofeig", "Ogmund", "Olvir", "Onund", "Orlyg", "Ospak", "Otho", "Otkel", "Ozur", "Rorik", "Runolf",
			"Saemund", "Saxi", "Sighvat", "Sigmund", "Sigtrygg", "Sigvald", "Skapti", "Skarphedin", "Skeggi", "Skjold",
			"Skuli", "Snaebjorn", "Solvi", "Sorli", "Soti", "Starkad", "Steinthor", "Sturla", "Styr", "Svart", "Sverre",
			"Teit", "Thjodolf", "Thorarin", "Thorbjorn", "Thorgeir", "Thorgils", "Thorgrim", "Thorkel", "Thorleif",
			"Thormod", "Thorodd", "Thorolf", "Thorvard", "Thrain", "Thrand", "Tjorvi", "Tostig", "Ubbe", "Ulfkel",
			"Ulfljot", "Vagn", "Valbrand", "Valgard", "Vandrad", "Vebjorn", "Vermund", "Vestein", "Vigfus", "Vigleik",
			"Kristoff", "Hans"
		};
		internal static readonly string[] FemaleNames =
		{
			"Alva", "Asta", "Astrid", "Bodil", "Borghild", "Dagmar", "Dagny", "Embla", "Estrid",
			"Frida", "Gro", "Gudrun", "Gunhild", "Helga", "Hilda", "Inga", "Ingrid", "Jorunn", "Kara", "Liv", "Maeva",
			"Maja", "Oda", "Ragna", "Ragnhild", "Revna", "Runa", "Sigrid", "Signe", "Siv", "Solveig",
			"Thyra", "Tove", "Turid", "Ulfhild", "Unn", "Valdis", "Vigdis", "Aud", "Freydis", "Gudrid", "Hallgerd", "Aslaug",
			"Hervor", "Lagertha", "Thordis", "Gyda", "Ingunn", "Bergljot", "Svanhild", "Alfhild", "Ylva",
			"Yrsa", "Torunn",
			"Agda", "Aldis", "Alfdis", "Arnbjorg", "Arndis", "Arnfrid", "Arnora", "Åsa", "Asdis", "Asleif", "Asny", "Asvor",
			"Audbjorg", "Bera", "Bergdis", "Bergthora", "Birna", "Bjorg", "Borgny", "Brynja", "Dagrun", "Dalla", "Eldbjorg",
			"Eldrid", "Erna", "Eydis", "Eyja", "Freygerd", "Fridgerd", "Geirlaug", "Geirrid", "Grima", "Gudfinna", "Gudlaug",
			"Gudny", "Gunnbjorg", "Gunnvor", "Gyrid", "Halla", "Hallbera", "Halldis", "Hallfrid", "Hallveig", "Herborg",
			"Herdis", "Hildigunn", "Hjordis", "Hlif", "Hrafnhild", "Hrefna", "Ingibjorg", "Ingigerd", "Ingileif", "Jodis",
			"Jofrid", "Kata", "Katla", "Kolbrun", "Kolfinna", "Oddbjorg", "Oddny", "Ormhild", "Osk", "Ragnfrid", "Ragnheid",
			"Rannveig", "Saeunn", "Snaefrid", "Solbjorg", "Solrun", "Solvor", "Steinunn", "Steinvor", "Svala", "Svava",
			"Thjodhild", "Thorbjorg", "Thorgerd", "Thorhild", "Thorkatla", "Thorlaug", "Ulfrun", "Valborg", "Valgerd",
			"Vilborg", "Yngvild", "Elsa", "Anna", "Iduna"
		};

		/// <summary>
		/// The names the first vikings of a village are given, in the order they come, by the village's name - each one of
		/// FemaleNames or MaleNames, so it's rolled as a woman or a man to suit (see PresetModel)
		/// </summary>
		internal static readonly Dictionary<string, string[]> FirstVikings = new Dictionary<string, string[]>(System.StringComparer.OrdinalIgnoreCase)
		{
			{ "Arendelle", new[] { "Elsa", "Anna", "Kristoff", "Olaf" } }
		};

		/// <summary>
		/// The creatures' names a viking takes anyway, in lowercase - the bear's, which is an old Norse name too
		/// </summary>
		private static readonly HashSet<string> CreatureNamesAllowed = new HashSet<string> { "bear", "bjorn", "bjørn" };

		/// <summary>
		/// The bynames of strong leaders, one of which the leader of each village goes by - and the rest, one of which a
		/// viking is given when it joins a village where another viking already goes by its name (see GiveName)
		/// </summary>
		internal static readonly string[] LeaderBynames =
		{
			"Bloodaxe", "Ironside", "Skull-Splitter", "Stormcloak", "Draugr-Bane", "the Wise", "the Strong", "the Bold"
		};
		internal static readonly string[] Bynames =
		{
			"the Red", "the Tall", "the Lucky", "the Far-Travelled", "the Quiet",
			"the Stout", "the Old", "the Young", "Silkbeard", "Greycloak"
		};

		/// <summary>
		/// How likely a new viking of the Meadows that isn't its village's leader is to have one of Bynames after its name
		/// anyway, and how much likelier for each biome after the Meadows, by VikingGear's order - so the Deep North's
		/// have one most of the time - from 0 to 1
		/// </summary>
		private const float BynameChance = 0.2f;
		private const float BynameChancePerBiome = 0.1f;

		/// <summary>
		/// Works out how likely a new viking of a biome is to have a byname of its own (see BynameChance)
		/// </summary>
		/// <param name="biome">Its biome's index into VikingGear's tables, from 0 for the Meadows</param>
		/// <returns>The chance, from 0 to 1</returns>
		internal static float BynameChanceFor(int biome)
		{
			return(UnityEngine.Mathf.Clamp01(BynameChance + BynameChancePerBiome * UnityEngine.Mathf.Max(0, biome)));
		}

		/// <summary>
		/// The bynames only a bearded viking is given
		/// </summary>
		private static readonly HashSet<string> BeardBynames = new HashSet<string> { "Forkbeard", "Silkbeard" };

		/// <summary>
		/// How far from a village's farm the vikings whose names a new one leaves out can be, when the village isn't
		/// loaded to measure it, in meters
		/// </summary>
		private const float VillageNameReach = 120f;

		/// <summary>
		/// How far from where a village's farm stands the farm noted on a viking can be and still be it, in meters
		/// </summary>
		private const float FarmMatch = 1f;

		/// <summary>
		/// The game's biomes, whose names in this game's language are read with their text keys, "$biome_" and the
		/// biome's name in lowercase
		/// </summary>
		private static readonly Heightmap.Biome[] Biomes =
		{
			Heightmap.Biome.Meadows, Heightmap.Biome.BlackForest, Heightmap.Biome.Swamp, Heightmap.Biome.Mountain,
			Heightmap.Biome.Plains, Heightmap.Biome.Ocean, Heightmap.Biome.Mistlands, Heightmap.Biome.AshLands,
			Heightmap.Biome.DeepNorth
		};

		/// <summary>
		/// The game's creatures' names, the legends' names and the places' names, in lowercase, read once the game's
		/// prefabs are loaded
		/// </summary>
		private static HashSet<string> creatureNames;
		private static HashSet<string> legendNames;
		private static HashSet<string> placeNames;

		/// <summary>
		/// Gets a viking's name, filtered for its author's words and without rich text
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>The name, or "" when it has none</returns>
		public static string Get(Character viking)
		{
			if (viking == null || viking.m_nview == null || !viking.m_nview.IsValid())
			{
				return("");
			}
			return(Filter(viking.m_nview.GetZDO()).RemoveRichTextTags().Trim());
		}

		/// <summary>
		/// Gets a viking's name from its ZDO, filtered for its author's words and without rich text
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>The name, or "" when it has none</returns>
		public static string Get(ZDO viking)
		{
			return((viking == null) ? "" : Filter(viking).RemoveRichTextTags().Trim());
		}

		/// <summary>
		/// Gets a viking's name as it was typed, filtered for its author's words, for the box to rename it
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>The name, or "" when it has none</returns>
		public static string GetRaw(Character viking)
		{
			if (viking == null || viking.m_nview == null || !viking.m_nview.IsValid())
			{
				return("");
			}
			return(Filter(viking.m_nview.GetZDO()));
		}

		/// <summary>
		/// Names a viking, on its owner
		/// </summary>
		/// <param name="viking">The viking, owned by this client</param>
		/// <param name="name">The name, or "" to take it away</param>
		/// <param name="author">Who named it, by platform user ID or "host"</param>
		public static void Set(Character viking, string name, string author)
		{
			ZDO zdo = viking.m_nview.GetZDO();
			zdo.Set(NameKey, Kept(name));
			zdo.Set(AuthorKey, author);
		}

		/// <summary>
		/// Works out the name a viking keeps when it's given one - without its "$" (see Plain) and cut to MaxLength
		/// (see Clip) - which is the name it's judged by too
		/// </summary>
		/// <param name="name">The name as it was given</param>
		/// <returns>The name it keeps</returns>
		internal static string Kept(string name)
		{
			return(Clip(Plain(name)));
		}

		/// <summary>
		/// Cuts a name to MaxLength characters, a character less when the cut would split a character written as two
		/// (like an emoji), and without the spaces the cut leaves at its end
		/// </summary>
		/// <param name="name">The name</param>
		/// <returns>The name, cut when it's too long</returns>
		internal static string Clip(string name)
		{
			return(Clip(name, MaxLength));
		}

		/// <summary>
		/// Cuts a name to a number of characters, a character less when the cut would split a character written as two
		/// (like an emoji), and without the spaces the cut leaves at its end
		/// </summary>
		/// <param name="name">The name</param>
		/// <param name="maxLength">The most characters it may have</param>
		/// <returns>The name, cut when it's too long</returns>
		internal static string Clip(string name, int maxLength)
		{
			if (name.Length <= maxLength)
			{
				return(name);
			}
			int length = (char.IsHighSurrogate(name[maxLength - 1]) ? maxLength - 1 : maxLength);
			return(name.Substring(0, length).TrimEnd());
		}

		/// <summary>
		/// Gives a PC viking a name of its own when it has none - one picked at random for whether it's a man or a
		/// woman, that no other viking of its village goes by, with or without a byname, and that it wouldn't turn down
		/// (see Judge), noted as given by no player, with one of Bynames after it more often the harder its biome (see
		/// BynameChanceFor), like "Rolf the Red". The first of a village's vikings named once its village is known,
		/// while no other viking there goes by a leader's byname, becomes its leader, with one of LeaderBynames after its
		/// name, like "Rolf Bloodaxe" - so when the leader dies, the next to be born or join takes it up. One that joins a
		/// village with a leader already loses a leader's byname of its own, and one whose name another viking there
		/// already goes by gets one of Bynames after it instead, like "Rolf the Red", or a new name when every byname that
		/// fits is taken too. A name a player gave it is kept as it is, even one another viking goes by
		/// </summary>
		/// <param name="viking">The viking's ZDO, owned by this client</param>
		/// <param name="data">Its looks, for whether it's a woman and has a beard</param>
		/// <param name="onlyWhenTaken">True as it joins a village, leaving one with no name as it is</param>
		public static void GiveName(ZDO viking, VikingData data, bool onlyWhenTaken)
		{
			ReadRaw(viking, out string name, out string author);
			if ((name.Length > 0 && author.Length > 0) || (name.Length == 0 && onlyWhenTaken))
			{
				return;
			}
			HashSet<string> taken = NamesInVillage(viking);
			string first = ((name.Length > 0) ? WithoutByname(name) : PickFirstName(data, taken));
			if (first.Length == 0)
			{
				return;
			}
			bool inVillage = (viking.GetVec3(Village.VillageKey, UnityEngine.Vector3.zero) != UnityEngine.Vector3.zero);
			bool led = HasLeader(taken);
			bool leader = (name.Length > 0 && IsLeader(name));
			string picked = null;
			if (inVillage && !led)
			{
				picked = ((leader && !taken.Contains(name.ToLowerInvariant())) ? name : WithByname(first, data, taken, LeaderBynames));
			}
			if (picked == null)
			{
				// A leader's byname goes with being the village's one leader
				string candidate = ((name.Length == 0 || leader) ? first : name);
				// A new viking now and then has a byname of its own, which it keeps wherever it goes
				if (name.Length == 0 && UnityEngine.Random.value < BynameChanceFor(data.Biome))
				{
					candidate = WithByname(first, data, taken, Bynames) ?? candidate;
				}
				picked =(!taken.Contains(candidate.ToLowerInvariant()) ? candidate : WithByname(first, data, taken, Bynames));
			}
			if (picked == null)
			{
				picked = PickFirstName(data, taken);
			}
			if (picked.Length == 0 || picked == name)
			{
				return;
			}
			viking.Set(NameKey, picked);
			viking.Set(AuthorKey, "");
		}

		/// <summary>
		/// Gives a traveler a name of its own - one picked at random for whether it's a man or a woman that no viking of
		/// the village it visits goes by, with no byname, noted as given by no player
		/// </summary>
		/// <param name="viking">The traveler's ZDO, owned by this client</param>
		/// <param name="data">Its looks, for whether it's a woman</param>
		/// <param name="host">The farm of the village it visits</param>
		public static void GiveTravelerName(ZDO viking, VikingData data, UnityEngine.Vector3 host)
		{
			string picked = PickFirstName(data, NamesAt(viking, host));
			if (picked.Length == 0)
			{
				return;
			}
			viking.Set(NameKey, picked);
			viking.Set(AuthorKey, "");
		}

		/// <summary>
		/// Gets the name a village's viking is given for the order it comes in, when its village's first vikings have
		/// names of their own (see FirstVikings)
		/// </summary>
		/// <param name="village">The village's name</param>
		/// <param name="index">How many vikings came to the village before it</param>
		/// <returns>The name, or null when it's left to GiveName</returns>
		internal static string FirstVikingName(string village, int index)
		{
			if (village == null || !FirstVikings.TryGetValue(village.Trim(), out string[] names) || index < 0 || index >= names.Length)
			{
				return(null);
			}
			return(names[index]);
		}

		/// <summary>
		/// Names a new viking of a village before it's rolled, the name its place among the village's first vikings gives
		/// it (see FirstVikingName), noted as given by no player - it's rolled as a woman or a man to suit (see
		/// PresetModel), and GiveName keeps it, adding a byname as it would to any other
		/// </summary>
		/// <param name="viking">The viking's ZDO, owned by this client, not rolled yet</param>
		/// <param name="village">The village's name</param>
		/// <param name="index">How many vikings came to the village before it</param>
		public static void PresetFirst(ZDO viking, string village, int index)
		{
			string name = FirstVikingName(village, index);
			if (name == null)
			{
				return;
			}
			viking.Set(NameKey, name);
			viking.Set(AuthorKey, "");
		}

		/// <summary>
		/// Gets whether a new viking named before it's rolled (see PresetFirst) is to be a woman or a man
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>Its model - 1 for a woman, 0 for a man - or -1 when its name doesn't say</returns>
		public static int PresetModel(ZDO viking)
		{
			ReadRaw(viking, out string name, out string author);
			return((author.Length > 0) ? -1 : ModelFor(WithoutByname(name)));
		}

		/// <summary>
		/// The names whose vikings take after Frozen's sisters - an Elsa immune to frost (see LivingVikingBody.FrostImmune)
		/// and rolled with a long blond braid, an Anna rolled with red twin braids (see VikingCore.StyleAfterName)
		/// </summary>
		internal const string ElsaName = "Elsa";
		internal const string AnnaName = "Anna";

		/// <summary>
		/// Checks whether a viking goes by ElsaName, whatever its case and byname
		/// </summary>
		/// <param name="name">The viking's name, or null</param>
		/// <returns>True when it does</returns>
		internal static bool IsElsa(string name)
		{
			return(GoesBy(name, ElsaName));
		}

		/// <summary>
		/// Checks whether a viking goes by AnnaName, whatever its case and byname
		/// </summary>
		/// <param name="name">The viking's name, or null</param>
		/// <returns>True when it does</returns>
		internal static bool IsAnna(string name)
		{
			return(GoesBy(name, AnnaName));
		}

		/// <summary>
		/// Checks whether a viking's first name is one name, whatever its case and byname
		/// </summary>
		/// <param name="name">The viking's name, or null</param>
		/// <param name="first">The first name</param>
		/// <returns>True when it is</returns>
		private static bool GoesBy(string name, string first)
		{
			return(string.Equals(VikingLoreSpeech.FirstName(name), first, System.StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Gets whether a name is a woman's or a man's, by which of FemaleNames and MaleNames lists it
		/// </summary>
		/// <param name="name">The name, without a byname</param>
		/// <returns>1 for a woman's, 0 for a man's, or -1 when neither lists it</returns>
		internal static int ModelFor(string name)
		{
			if (System.Array.Exists(FemaleNames, (string female) => string.Equals(female, name, System.StringComparison.OrdinalIgnoreCase)))
			{
				return(1);
			}
			if (System.Array.Exists(MaleNames, (string male) => string.Equals(male, name, System.StringComparison.OrdinalIgnoreCase)))
			{
				return(0);
			}
			return(-1);
		}

		/// <summary>
		/// Picks a name for a viking at random for whether it's a woman or a man, that no other viking of its village goes
		/// by, with or without a byname, and that it wouldn't turn down (see Judge)
		/// </summary>
		/// <param name="data">Its looks, for whether it's a woman</param>
		/// <param name="taken">The names the other vikings of its village go by, in lowercase</param>
		/// <returns>The name, or "" when every one is taken</returns>
		private static string PickFirstName(VikingData data, HashSet<string> taken)
		{
			HashSet<string> firsts = new HashSet<string>();
			foreach (string other in taken)
			{
				firsts.Add(WithoutByname(other).ToLowerInvariant());
			}
			List<string> free = new List<string>();
			foreach (string choice in ((data.Model == 1) ? FemaleNames : MaleNames))
			{
				if (!firsts.Contains(choice.ToLowerInvariant()) && Judge(null, null, choice) == Verdict.Taken)
				{
					free.Add(choice);
				}
			}
			return((free.Count > 0) ? free[UnityEngine.Random.Range(0, free.Count)] : "");
		}

		/// <summary>
		/// Puts one of some bynames after a viking's name, picked at random among those that fit in MaxLength, suit it -
		/// a beard's only for a bearded viking - and make a name no other viking of its village goes by
		/// </summary>
		/// <param name="first">Its name, without a byname</param>
		/// <param name="data">Its looks, for whether it has a beard</param>
		/// <param name="taken">The names the other vikings of its village go by, in lowercase</param>
		/// <param name="bynames">The bynames to pick from</param>
		/// <returns>The name with the byname, or null when none fits</returns>
		private static string WithByname(string first, VikingData data, HashSet<string> taken, string[] bynames)
		{
			List<string> free = new List<string>();
			foreach (string byname in bynames)
			{
				string full = first + " " + byname;
				if (full.Length <= MaxLength && (!BeardBynames.Contains(byname) || data.HasBeard()) && !taken.Contains(full.ToLowerInvariant()))
				{
					free.Add(full);
				}
			}
			return((free.Count > 0) ? free[UnityEngine.Random.Range(0, free.Count)] : null);
		}

		/// <summary>
		/// Checks whether a viking's name ends with a leader's byname (see LeaderBynames)
		/// </summary>
		/// <param name="name">The name</param>
		/// <returns>True when it does</returns>
		private static bool IsLeader(string name)
		{
			return(System.Array.Exists(LeaderBynames, (string byname) => name.EndsWith(" " + byname, System.StringComparison.OrdinalIgnoreCase)));
		}

		/// <summary>
		/// Checks whether a village has a leader - a viking there going by a leader's byname
		/// </summary>
		/// <param name="taken">The names the vikings of the village go by, in lowercase</param>
		/// <returns>True when it has</returns>
		private static bool HasLeader(HashSet<string> taken)
		{
			foreach (string other in taken)
			{
				if (IsLeader(other))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Takes the byname off the end of a name given to a viking (see LeaderBynames and Bynames), if it has one
		/// </summary>
		/// <param name="name">The name, like "Rolf the Red"</param>
		/// <returns>The name before it, like "Rolf"</returns>
		private static string WithoutByname(string name)
		{
			foreach (string byname in LeaderBynames)
			{
				if (name.EndsWith(" " + byname, System.StringComparison.OrdinalIgnoreCase))
				{
					return(name.Substring(0, name.Length - byname.Length - 1));
				}
			}
			foreach (string byname in Bynames)
			{
				if (name.EndsWith(" " + byname, System.StringComparison.OrdinalIgnoreCase))
				{
					return(name.Substring(0, name.Length - byname.Length - 1));
				}
			}
			return(name);
		}

		/// <summary>
		/// Lists the names the other PC vikings of a viking's village go by, in lowercase - the vikings whose ZDOs
		/// this client has around the village that note it as theirs, and those out on the village's quest, who are
		/// still its vikings while they're out of the world (see AddQuestNames)
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>The names, or none when it belongs to no village</returns>
		private static HashSet<string> NamesInVillage(ZDO viking)
		{
			return(NamesAt(viking, viking.GetVec3(Village.VillageKey, UnityEngine.Vector3.zero)));
		}

		/// <summary>
		/// Lists the names the PC vikings of the village at a farm go by but for one viking, in lowercase, the same way as
		/// NamesInVillage
		/// </summary>
		/// <param name="viking">The ZDO of the viking to leave out</param>
		/// <param name="farm">The village's farm, or zero for none</param>
		/// <returns>The names, or none for no village</returns>
		private static HashSet<string> NamesAt(ZDO viking, UnityEngine.Vector3 farm)
		{
			HashSet<string> taken = new HashSet<string>();
			if (farm == UnityEngine.Vector3.zero)
			{
				return(taken);
			}
			Village village = Village.ForFarm(farm);
			List<ZDO> zdos = new List<ZDO>();
			if (village != null)
			{
				VillageRespawn.ZdosNear(village.Middle, village.Radius + Village.PieceMargin, zdos);
			}
			else
			{
				VillageRespawn.ZdosNear(farm, VillageNameReach, zdos);
			}
			foreach (ZDO other in zdos)
			{
				if (other == viking || !VikingCore.IsPlayerLikePrefab(other.GetPrefab()) || Utils.DistanceXZ(other.GetVec3(Village.VillageKey, UnityEngine.Vector3.zero), farm) > FarmMatch)
				{
					continue;
				}
				string name = Plain(other.GetString(NameKey)).Trim();
				if (name.Length > 0)
				{
					taken.Add(name.ToLowerInvariant());
				}
			}
			QuestRecord record = VillageQuests.Read(village);
			if (record != null)
			{
				AddQuestNames(taken, record.Members, record.Names, VikingIdentity.Get(viking));
			}
			return(taken);
		}

		/// <summary>
		/// Adds the names of the vikings on a village's quest to the names its vikings go by, in lowercase, leaving out
		/// the viking being named - so a leader out on a quest is still the village's leader, and one of a group coming
		/// home ahead of its leader doesn't take up a leader's byname
		/// </summary>
		/// <param name="taken">The names the village's vikings go by, in lowercase, added to</param>
		/// <param name="members">The quest's vikings, by their GUIDs (see VikingIdentity)</param>
		/// <param name="names">Their names, by the same index as members</param>
		/// <param name="self">The GUID of the viking being named</param>
		internal static void AddQuestNames(HashSet<string> taken, List<string> members, List<string> names, string self)
		{
			for (int i = 0; i < members.Count && i < names.Count; i++)
			{
				if (members[i] == self)
				{
					continue;
				}
				string name = Plain(names[i]).Trim();
				if (name.Length > 0)
				{
					taken.Add(name.ToLowerInvariant());
				}
			}
		}

		/// <summary>
		/// Reads a viking's name as it was typed and who gave it, unfiltered, to send on or keep elsewhere
		/// </summary>
		/// <param name="viking">The viking's ZDO, or another ZDO the name was copied to (see CopyTo)</param>
		/// <param name="name">The name, without its "$", or ""</param>
		/// <param name="author">Who named it, by platform user ID or "host", or ""</param>
		public static void ReadRaw(ZDO viking, out string name, out string author)
		{
			name = Plain(viking.GetString(NameKey));
			author = viking.GetString(AuthorKey);
		}

		/// <summary>
		/// Reads a viking's name as it was typed and who gave it, unfiltered, from the values it's kept as while it's out
		/// of the world (see VillageDeparted.SnapshotOf)
		/// </summary>
		/// <param name="values">The viking's values</param>
		/// <param name="name">The name, without its "$", or ""</param>
		/// <param name="author">Who named it, by platform user ID or "host", or ""</param>
		public static void ReadRaw(VillageDeparted.VikingValues values, out string name, out string author)
		{
			KeyValuePair<int, string> named = values.Strings.Find((KeyValuePair<int, string> entry) => entry.Key == NameKey);
			KeyValuePair<int, string> by = values.Strings.Find((KeyValuePair<int, string> entry) => entry.Key == AuthorKey);
			name = Plain((named.Key == NameKey) ? named.Value : "");
			author = ((by.Key == AuthorKey) ? (by.Value ?? "") : "");
		}

		/// <summary>
		/// Copies a viking's name and who gave it to another ZDO, like its corpse's, where Get reads it the same way
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <param name="other">The other ZDO, owned by this client</param>
		public static void CopyTo(ZDO viking, ZDO other)
		{
			ReadRaw(viking, out string name, out string author);
			if (name.Length == 0)
			{
				return;
			}
			other.Set(NameKey, name);
			other.Set(AuthorKey, author);
		}

		/// <summary>
		/// Works out what a viking makes of a name it's given
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <param name="village">Its village, or null</param>
		/// <param name="name">The name, without its "$"</param>
		/// <returns>Whether it takes it or turns it down, and why</returns>
		public static Verdict Judge(Character viking, Village village, string name)
		{
			Verdict verdict = JudgeText(name, ZombieLink.ZombieName());
			if (verdict != Verdict.Taken)
			{
				return(verdict);
			}
			string lower = Kept(name).ToLowerInvariant();
			if (IsPlayerName(lower))
			{
				return(Verdict.Someone);
			}
			if (village != null)
			{
				foreach (Character fellow in village.Vikings())
				{
					if (fellow != viking && Get(fellow).ToLowerInvariant() == lower)
					{
						return(Verdict.Someone);
					}
				}
			}
			return(Verdict.Taken);
		}

		/// <summary>
		/// Works out what a viking makes of a name it's given from the name alone, judging the name it would keep (see
		/// Kept) - text markup, "Viking" or nothing, a legend's, a place's, or a creature's, which takes in the zombie
		/// word and the zombie word followed by a creature's name - leaving the players and the viking's village to Judge
		/// </summary>
		/// <param name="name">The name, without its "$"</param>
		/// <param name="zombie">The word a zombie's name starts with (see ZombieLink.ZombieName), or ""</param>
		/// <returns>Why it turns it down, or Taken when the name alone doesn't rule it out</returns>
		internal static Verdict JudgeText(string name, string zombie)
		{
			string lower = Kept(name).ToLowerInvariant();
			if (IsMarkup(lower))
			{
				return(Verdict.Markup);
			}
			if (lower.Length == 0 || lower == "viking")
			{
				return(Verdict.Nameless);
			}
			LoadNames();
			if (legendNames.Contains(lower))
			{
				return(Verdict.Legend);
			}
			if (placeNames.Contains(lower))
			{
				return(Verdict.Place);
			}
			if (IsCreatureName(lower, creatureNames, zombie))
			{
				return(Verdict.Creature);
			}
			return(Verdict.Taken);
		}

		/// <summary>
		/// Checks whether a name is a creature's - one of the game's creatures but those a viking takes anyway (see
		/// CreatureNamesAllowed), the zombie word, or the zombie word followed by a creature's name, the way a zombie is
		/// named for the creature it was
		/// </summary>
		/// <param name="lower">The name, in lowercase</param>
		/// <param name="creatures">The game's creatures' names, in lowercase</param>
		/// <param name="zombie">The word a zombie's name starts with (see ZombieLink.ZombieName), or ""</param>
		/// <returns>True when it is</returns>
		internal static bool IsCreatureName(string lower, HashSet<string> creatures, string zombie)
		{
			zombie = zombie.Trim().ToLowerInvariant();
			if (zombie.Length > 0 && (lower == zombie || lower.StartsWith(zombie + " ")))
			{
				return(true);
			}
			return(creatures.Contains(lower) && !CreatureNamesAllowed.Contains(lower));
		}

		/// <summary>
		/// Checks whether a name is a place's - a biome's, in English or this game's language, or a place from the lore -
		/// which vikings turn down for themselves and their village alike
		/// </summary>
		/// <param name="name">The name</param>
		/// <returns>True when it is</returns>
		public static bool IsPlace(string name)
		{
			LoadNames();
			return(placeNames.Contains(Plain(name).ToLowerInvariant()));
		}

		/// <summary>
		/// Checks whether a name is a legend's - a god, giant or beast of the myths, a figure of the game's world, a
		/// legendary weapon, or a boss or trader among the game's creatures - which vikings turn down for themselves and
		/// their village alike
		/// </summary>
		/// <param name="name">The name</param>
		/// <returns>True when it is</returns>
		public static bool IsLegend(string name)
		{
			LoadNames();
			return(legendNames.Contains(Plain(name).ToLowerInvariant()));
		}

		/// <summary>
		/// Notes that a player has been in the world, so no viking takes their name, in a global key the world keeps
		/// </summary>
		/// <param name="player">The player</param>
		public static void NotePlayer(Player player)
		{
			if (player == null || ZoneSystem.instance == null || ZRoutedRpc.instance == null)
			{
				return;
			}
			string key = PlayerKey(player.GetPlayerName().ToLowerInvariant());
			if (!ZoneSystem.instance.GetGlobalKey(key))
			{
				ZoneSystem.instance.SetGlobalKey(key);
			}
		}

		/// <summary>
		/// Checks whether a name is a player's - one in the game now, or one that has been in the world
		/// </summary>
		/// <param name="lower">The name, in lowercase</param>
		/// <returns>True when it is</returns>
		private static bool IsPlayerName(string lower)
		{
			foreach (Player player in Player.GetAllPlayers())
			{
				if (player != null && player.GetPlayerName().ToLowerInvariant() == lower)
				{
					return(true);
				}
			}
			if (ZNet.instance != null)
			{
				foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
				{
					if (info.m_name != null && info.m_name.ToLowerInvariant() == lower)
					{
						return(true);
					}
				}
			}
			return(ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(PlayerKey(lower)));
		}

		/// <summary>
		/// Makes the global key that notes a player has been in the world
		/// </summary>
		/// <param name="lower">The player's name, in lowercase</param>
		/// <returns>The key</returns>
		internal static string PlayerKey(string lower)
		{
			StringBuilder key = new StringBuilder(PlayerKeyPrefix);
			foreach (byte part in Encoding.UTF8.GetBytes(lower))
			{
				key.Append(part.ToString("x2"));
			}
			return(key.ToString());
		}

		/// <summary>
		/// Reads the game's creatures' names, in this game's language - a boss or trader among them as a legend - and
		/// the legends' names, once the game's prefabs are loaded. The sets are filled before they're kept, so a name
		/// judged meanwhile is judged by whole sets
		/// </summary>
		private static void LoadNames()
		{
			if (creatureNames != null && creatureNames.Count > 0)
			{
				return;
			}
			HashSet<string> creatures = new HashSet<string>();
			HashSet<string> legends = new HashSet<string>();
			HashSet<string> places = new HashSet<string>();
			foreach (string legend in Legends)
			{
				legends.Add(legend.ToLowerInvariant());
			}
			foreach (string place in Places)
			{
				places.Add(place.ToLowerInvariant());
			}
			if (ZNetScene.instance != null && Localization.instance != null)
			{
				foreach (Heightmap.Biome biome in Biomes)
				{
					string name = LoadedName(Localization.instance.Localize("$biome_" + biome.ToString().ToLowerInvariant()));
					if (name.Length > 0 && !name.StartsWith("["))
					{
						places.Add(name);
					}
				}
				foreach (UnityEngine.GameObject prefab in ZNetScene.instance.m_prefabs)
				{
					if (prefab == null)
					{
						continue;
					}
					Character character = prefab.GetComponent<Character>();
					Trader trader = prefab.GetComponent<Trader>();
					string token = ((character != null) ? character.m_name : ((trader != null) ? trader.m_name : null));
					if (string.IsNullOrEmpty(token))
					{
						continue;
					}
					string name = LoadedName(Localization.instance.Localize(token));
					if (name.Length == 0 || name.StartsWith("[") || name == "viking")
					{
						continue;
					}
					if (trader != null || character.m_boss)
					{
						legends.Add(name);
					}
					else
					{
						creatures.Add(name);
					}
				}
			}
			legendNames = legends;
			placeNames = places;
			creatureNames = creatures;
		}

		/// <summary>
		/// Works out the name a creature, trader or biome goes by from its text in this game's language, as names given to
		/// vikings are compared with it - in lowercase, and without the rich text some have, like the color of the names of
		/// Hildir's quest bosses
		/// </summary>
		/// <param name="localized">The text, like "&lt;color=orange&gt;Brenna&lt;/color&gt;"</param>
		/// <returns>The name, like "brenna"</returns>
		internal static string LoadedName(string localized)
		{
			return(localized.RemoveRichTextTags().Trim().ToLowerInvariant());
		}

		/// <summary>
		/// Takes the "$" out of a name, which the game - and mods that show a creature's name, like markers - would read
		/// as the start of a text key to look up and show in its place
		/// </summary>
		/// <param name="name">The name</param>
		/// <returns>The name without it</returns>
		public static string Plain(string name)
		{
			return(VikingName.Plain(name));
		}

		/// <summary>
		/// Checks whether a name holds text markup - a rich text tag like &lt;color=red&gt; or &lt;b&gt; - which a viking
		/// won't take for a name, nor for its village's
		/// </summary>
		/// <param name="name">The name</param>
		/// <returns>True when it does</returns>
		internal static bool IsMarkup(string name)
		{
			return(name != null && Regex.IsMatch(name, "<[^<>]*>"));
		}

		/// <summary>
		/// Wraps a name in a noparse tag so its markup shows as it was typed rather than coloring or styling the line it's
		/// said in, taking out any noparse tags of its own that would end the wrapping early
		/// </summary>
		/// <param name="name">The name</param>
		/// <returns>The wrapped name</returns>
		internal static string Literal(string name)
		{
			return("<noparse>" + Regex.Replace(name ?? "", "</?noparse>", "", RegexOptions.IgnoreCase) + "</noparse>");
		}

		/// <summary>
		/// Gets who the local player is as a name's author
		/// </summary>
		/// <returns>Their platform user ID, or "host" when they're not signed in</returns>
		public static string LocalAuthor()
		{
			return(PlatformManager.DistributionPlatform.LocalUser.IsSignedIn ? PlatformManager.DistributionPlatform.LocalUser.PlatformUserID.ToString() : "host");
		}

		/// <summary>
		/// Checks whether the local player may see and give names other players type, the way the game checks before
		/// renaming a tamed creature
		/// </summary>
		/// <returns>True when they may</returns>
		public static bool CanName()
		{
			return(PlatformManager.DistributionPlatform.PrivilegeProvider.CheckPrivilege(Privilege.ViewUserGeneratedContent).IsGranted());
		}

		/// <summary>
		/// Reads a viking's name, filtered for its author's words the way a tamed creature's is
		/// </summary>
		/// <param name="zdo">The viking's ZDO</param>
		/// <returns>The name, or ""</returns>
		private static string Filter(ZDO zdo)
		{
			return(Filter(Plain(zdo.GetString(NameKey)), zdo.GetString(AuthorKey)));
		}

		/// <summary>
		/// Filters a name for its author's words the way a tamed creature's is
		/// </summary>
		/// <param name="name">The name</param>
		/// <param name="author">Who gave it, by platform user ID or "host"</param>
		/// <returns>The name, filtered, or ""</returns>
		public static string Filter(string name, string author)
		{
			return(VikingName.Filter(name, author));
		}
	}
}
