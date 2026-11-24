using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The names players give villages - typed in a box opened with NameKey while looking at the village's fire, kept in
	/// its generator's or anchor's ZDO with who named it, so the name is filtered for that player's words wherever it
	/// shows - in what its vikings say, on its fire and on each player's map (see VillageMapPins). Its vikings turn
	/// down a place's name for it and text markup, the way they do for themselves (see VikingNames.IsPlace and
	/// VikingNames.IsMarkup)
	/// </summary>
	/// <remarks>
	/// The name is sent to every client, and the one that owns the village's generator or anchor notes it, the way a
	/// viking's death is
	/// </remarks>
	internal static class VillageNames
	{
		/// <summary>
		/// The key that opens the box to name the village, while looking at its fire
		/// </summary>
		public const KeyCode NameKey = KeyCode.N;

		/// <summary>
		/// On a farm's generator or a started village's anchor, the name a player gave the village and who gave it, by
		/// platform user ID or "host"
		/// </summary>
		private static readonly int NameZdoKey = "kriona.VikingVillages.villageName".GetStableHashCode();
		private static readonly int AuthorKey = "kriona.VikingVillages.villageNameAuthor".GetStableHashCode();

		/// <summary>
		/// The routed RPC that names a village, with where its farm stands, the name and who gave it
		/// </summary>
		public const string NameRpc = Plugin.PluginGuid + ".VillageName";

		/// <summary>
		/// The most characters a village's name can have
		/// </summary>
		public const int MaxLength = 24;

		/// <summary>
		/// The box's title
		/// </summary>
		private const string Topic = "Name the village";

		/// <summary>
		/// The names a village no player has named is given, picked at random among those no other village in the world
		/// has (see TryAutoName) - Norse towns, farms and places of the Viking age, ones of the game's world, and Old Norse
		/// words with the endings of the Norse settlers' villages: -by (a farm or village), -thorp (an outlying farm),
		/// -toft (a homestead), -thwaite (a clearing), -kirk (a church) and -ness (a headland)
		/// </summary>
		internal static readonly string[] AutoNames =
		{
			"Aalborg", "Aarhus", "Avaldsnes", "Gamla Uppsala", "Hedeby", "Helgö", "Hólar", "Jelling", "Jomsborg", "Jórvík",
			"Kalmar", "Kaupang", "Kirkwall", "Lejre", "Lödöse", "Lund", "Nidaros", "Njardarheimr", "Odense", "Ribe",
			"Sigtuna", "Uppsala", "Visby", "Bjallabrekka", "Bjarnarøy", "Bjornstead", "Driftgarth", "Eirikrheim",
			"Fjelldal", "Frostfjord", "Frostvik", "Gråbergsby", "Gullstrand", "Hagagaror", "Hakonarhella", "Hrafntún",
			"Hringdalir", "Ingolfsfell", "Ironfjell", "Kongsvik", "Lindhavn", "Maelifellsa", "Mogilsa", "Ravenfell",
			"Sealskerry", "Skalavik", "Skaldford", "Stormnes", "Tømmerby",
			"Birka", "Borre", "Gokstad", "Oseberg", "Tønsberg", "Skiringssal", "Hladir", "Stiklestad", "Hafrsfjord",
			"Trelleborg", "Fyrkat", "Aggersborg", "Roskilde", "Viborg", "Uppåkra", "Vendel", "Valsgärde", "Skara",
			"Bjørgvin", "Lindholm", "Sliesthorp", "Truso", "Aldeigjuborg", "Holmgard", "Dyflin", "Vedrafjord", "Hlymrek",
			"Birsay", "Tingwall", "Leirvik", "Brattahlid", "Gardar", "Hvalsey", "Leifsbudir", "Thingvellir", "Reykholt",
			"Skálholt", "Helgafell", "Mosfell", "Hlidarendi", "Bergthorshvoll", "Hjardarholt", "Haukadal", "Oddi", "Borg",
			"Askby", "Eikby", "Lindby", "Holmby", "Sandby", "Hagaby", "Akerby", "Fossby", "Strandby", "Sundby", "Hvitby",
			"Svartby", "Krakaby", "Uxaby", "Geitby", "Svanby", "Laxby", "Hvalby", "Smidby", "Kaupby", "Jarlsby", "Karlsby",
			"Hofby", "Thingby", "Skalaby", "Kornby", "Mjodby", "Skogby", "Grimsby", "Ivarsby", "Ketilsby", "Gunnarsby",
			"Asgeirsby", "Einarsby", "Rolfsby",
			"Birkthorp", "Bergthorpe", "Engthorpe", "Myrthorp", "Vikthorpe", "Raudthorpe", "Ulfthorp", "Saudathorp",
			"Gasathorpe", "Byggthorp", "Linthorpe", "Haugthorpe", "Brekkuthorp", "Vindthorp", "Solthorpe", "Eldthorpe",
			"Hamarthorpe", "Klettathorp", "Fiskthorpe", "Saltathorpe", "Hestathorpe", "Nautathorp", "Gamlathorpe",
			"Austhorpe", "Tokethorpe", "Kolthorpe", "Thorkelthorpe", "Asmundthorp", "Halvarthorpe",
			"Langtoft", "Brunatoft", "Kjarrtoft", "Grjottoft", "Leirtoft", "Mosatoft", "Hrisatoft", "Vatnatoft",
			"Tjarnatoft", "Melatoft", "Hvannatoft", "Laukatoft", "Hrossatoft", "Svinatoft", "Lambatoft", "Kalfatoft",
			"Selatoft", "Batatoft", "Hafratoft", "Rugatoft", "Nytoft", "Sveinstoft", "Bardstoft", "Gormstoft", "Haukstoft",
			"Dagstoft", "Brandstoft",
			"Furuthwaite", "Bjarkthwaite", "Reynithwaite", "Einithwaite", "Haslathwaite", "Vidithwaite", "Thornthwaite",
			"Lyngthwaite", "Lundthwaite", "Brennthwaite", "Setrathwaite", "Breidthwaite", "Keldthwaite", "Bekkthwaite",
			"Hrafnthwaite", "Refthwaite", "Hjorthwaite", "Elgthwaite", "Hunangthwaite", "Grettisthwaite", "Leifthwaite",
			"Halfdanthwaite", "Gislithwaite", "Njalsthwaite", "Ottarthwaite",
			"Steinkirk", "Fjallkirk", "Dalkirk", "Eyjakirk", "Holtkirk", "Hlidkirk", "Brattkirk", "Hafkirk", "Ljosakirk",
			"Mjollkirk", "Snaekirk", "Himinkirk", "Klukkukirk", "Nordkirk", "Sudkirk", "Vestkirk", "Ormskirk", "Olafskirk",
			"Magnuskirk", "Haraldskirk", "Knudskirk", "Erlingskirk", "Sigurdskirk",
			"Arnarness", "Skarfness", "Lundaness", "Thernaness", "Aedarness", "Mavaness", "Drangness", "Skerness",
			"Brimness", "Rifness", "Bodaness", "Myrkness", "Isaness", "Hrimness", "Graenaness", "Horgness", "Vardaness",
			"Skipaness", "Naustaness", "Kirkness", "Skegness", "Flokaness", "Hjalmarsness"
		};

		/// <summary>
		/// Checks whether a village's generator or anchor has ever been named - it has a name, or a player took its name
		/// away
		/// </summary>
		/// <param name="zdo">The generator's or anchor's ZDO</param>
		/// <returns>True when it has</returns>
		public static bool EverNamed(ZDO zdo)
		{
			return(zdo.GetString(NameZdoKey).Length > 0 || zdo.GetString(AuthorKey).Length > 0);
		}

		/// <summary>
		/// Gives a village the name settled for its farm in advance (see VillagePlannedNames), on the client that owns its
		/// generator or anchor, when it has never been named - or, with none settled or the one settled taken, one of
		/// AutoNames no other village goes by or has settled - noted as given by no player. A village whose name a player
		/// took away keeps having none, and its map pin shows once it's named (see VillageMapPins)
		/// </summary>
		/// <param name="village">The village, whose generator or anchor this client owns</param>
		public static void TryAutoName(Village village)
		{
			if (village == null || village.Zdo == null)
			{
				return;
			}
			TryAutoName(village.Zdo, village.Farm);
		}

		/// <summary>
		/// Gives the village a farm's generator or a started village's anchor is the record of the name settled for its
		/// farm, or one no other village goes by, the way TryAutoName(Village) does
		/// </summary>
		/// <param name="zdo">The generator's or anchor's ZDO, owned by this client</param>
		/// <param name="farm">Where the village's farm stands</param>
		public static void TryAutoName(ZDO zdo, Vector3 farm)
		{
			// The names are settled in advance (see VillagePlannedNames), so it waits until they're known
			if (zdo == null || EverNamed(zdo) || !VillagePlannedNames.Ready || !VillageNameList.Ready)
			{
				return;
			}
			string picked = PickName(farm, TakenNames(zdo));
			if (picked != null)
			{
				Give(zdo, farm, picked);
			}
		}

		/// <summary>
		/// Lists the names other villages go by - those in the world's list of names and those of the villages loaded on
		/// this client
		/// </summary>
		/// <param name="own">The generator or anchor of the village being named, whose own name is left out</param>
		/// <returns>The names, in lower case</returns>
		internal static HashSet<string> TakenNames(ZDO own)
		{
			HashSet<string> taken = new HashSet<string>();
			foreach (VillageNameList.Entry entry in VillageNameList.Others(""))
			{
				taken.Add(entry.Name.ToLowerInvariant());
			}
			foreach (Village other in Village.Loaded())
			{
				if (other.Zdo != null && other.Zdo != own)
				{
					taken.Add(Raw(other).ToLowerInvariant());
				}
			}
			return(taken);
		}

		/// <summary>
		/// Picks a name for a village - the one settled for its farm in advance, or with none settled or that one taken,
		/// one of AutoNames that isn't taken, isn't a place's name and no farm has settled
		/// </summary>
		/// <param name="farm">Where the village's farm stands</param>
		/// <param name="taken">The names other villages go by, in lower case (see TakenNames)</param>
		/// <returns>The name, or null when every name is taken</returns>
		internal static string PickName(Vector3 farm, HashSet<string> taken)
		{
			string picked = VillagePlannedNames.For(farm);
			if (picked != null && !taken.Contains(picked.ToLowerInvariant()))
			{
				return(picked);
			}
			// A village with none settled, like one a player started, takes one no farm has settled
			List<string> free = new List<string>();
			foreach (string name in AutoNames)
			{
				if (!taken.Contains(name.ToLowerInvariant()) && !VikingNames.IsPlace(name) && !VillagePlannedNames.IsSettled(name))
				{
					free.Add(name);
				}
			}
			return((free.Count > 0) ? free[Random.Range(0, free.Count)] : null);
		}

		/// <summary>
		/// Names a village, noted as given by no player
		/// </summary>
		/// <param name="zdo">Its generator's or anchor's ZDO, owned by this client</param>
		/// <param name="farm">Where its farm stands</param>
		/// <param name="name">The name</param>
		internal static void Give(ZDO zdo, Vector3 farm, string name)
		{
			zdo.Set(NameZdoKey, name);
			VillageNameList.Note(name, "");
			Plugin.Log.LogInfo("The village at " + farm.ToString("F0") + " is called " + name);
		}

		/// <summary>
		/// How near the player a viking of the village has to be to say something when they name it, or to turn a name
		/// down for it out loud, in meters
		/// </summary>
		private const float SpeakRange = 30f;

		/// <summary>
		/// What the box to name a village reads its name from and sends the new one to
		/// </summary>
		private class NameBox : TextReceiver
		{
			private readonly Vector3 farm;
			private readonly string current;

			/// <summary>
			/// Makes the box's go-between for a village
			/// </summary>
			/// <param name="village">The village</param>
			public NameBox(Village village)
			{
				farm = village.Farm;
				current = Raw(village);
			}

			/// <summary>
			/// Gets the village's name for the box
			/// </summary>
			/// <returns>The name, or ""</returns>
			public string GetText()
			{
				return(current);
			}

			/// <summary>
			/// Names the village what the player typed - the client that owns it notes it - and tells them so, with a word
			/// from the village's viking nearest them within SpeakRange, unless it's text markup or a place's name, which
			/// its vikings turn down (see Refuse)
			/// </summary>
			/// <param name="text">The name, or "" to take it away</param>
			public void SetText(string text)
			{
				string name = VikingNames.Clip(VikingNames.Plain(text ?? ""), MaxLength);
				if (ZRoutedRpc.instance == null || name == current)
				{
					return;
				}
				string filtered = VikingNames.Filter(name, VikingNames.LocalAuthor());
				if (VikingNames.IsMarkup(name))
				{
					string literal = VikingNames.Literal(filtered);
					Refuse(farm, VikingSpeech.MarkupRefused(literal), literal + " doesn't sound like a name");
					return;
				}
				string shown = filtered.RemoveRichTextTags();
				if (name.Length > 0 && VikingNames.IsPlace(name))
				{
					Refuse(farm, VikingSpeech.VillageRefused(shown), "The Vikings won't have their village named " + shown);
					return;
				}
				ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, NameRpc, farm, name, VikingNames.LocalAuthor());
				Player player = Player.m_localPlayer;
				if (player == null)
				{
					return;
				}
				Character speaker = NearestViking(farm, player);
				if (speaker != null)
				{
					string line = ((name.Length == 0) ? VikingSpeech.VillageCleared() : VikingSpeech.VillageNamed(shown, current.Length > 0));
					VikingSpeech.Say(speaker, line, VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				}
				if (name.Length == 0)
				{
					player.Message(MessageHud.MessageType.Center, "The village no longer has a name");
				}
				else if (MessageHud.instance != null)
				{
					MessageHud.instance.ShowBiomeFoundMsg(shown, false);
				}
			}
		}

		/// <summary>
		/// Turns down a name for a village - a place's, or text markup - the village's viking nearest the local player
		/// within SpeakRange that's awake says so over its head, or with none that near, it's said on screen
		/// </summary>
		/// <param name="farm">Where the village's farm stands</param>
		/// <param name="line">What the viking says</param>
		/// <param name="message">What's said on screen instead</param>
		private static void Refuse(Vector3 farm, string line, string message)
		{
			Player player = Player.m_localPlayer;
			if (player == null)
			{
				return;
			}
			Character nearest = NearestViking(farm, player);
			if (nearest != null)
			{
				VikingSpeech.Say(nearest, line, VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				return;
			}
			player.Message(MessageHud.MessageType.Center, message);
		}

		/// <summary>
		/// Finds the village's viking nearest a player within SpeakRange that's awake, to speak for the village when the
		/// player names it
		/// </summary>
		/// <param name="farm">Where the village's farm stands</param>
		/// <param name="player">The player</param>
		/// <returns>The viking, or null when none awake is that near</returns>
		private static Character NearestViking(Vector3 farm, Player player)
		{
			Village village = Village.ForFarm(farm);
			if (village == null)
			{
				return(null);
			}
			Character nearest = null;
			float nearestDistance = SpeakRange;
			foreach (Character viking in village.Vikings())
			{
				float distance = Vector3.Distance(viking.transform.position, player.transform.position);
				if (distance <= nearestDistance && !VikingSpeech.Asleep(viking))
				{
					nearest = viking;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Registers the RPC with a world's routed RPC
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register<Vector3, string, string>(NameRpc, OnName);
		}

		/// <summary>
		/// Gets a village's name, filtered for its author's words and without rich text
		/// </summary>
		/// <param name="village">The village, or null</param>
		/// <returns>The name, or "" when it has none</returns>
		public static string Get(Village village)
		{
			if (village == null || village.Zdo == null)
			{
				return("");
			}
			return(VikingNames.Filter(Raw(village), village.Zdo.GetString(AuthorKey)).RemoveRichTextTags().Trim());
		}

		/// <summary>
		/// Gets a village's name as it was typed
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The name, or ""</returns>
		private static string Raw(Village village)
		{
			return(VikingNames.Plain(village.Zdo.GetString(NameZdoKey)));
		}

		/// <summary>
		/// Gets the name a player gave the village a farm's generator or started village's anchor is the record of
		/// </summary>
		/// <param name="zdo">The generator's or anchor's ZDO</param>
		/// <param name="name">The name as it was typed, without rich text</param>
		/// <param name="author">Who gave it, by platform user ID or "host"</param>
		/// <returns>True when the village has a name</returns>
		public static bool At(ZDO zdo, out string name, out string author)
		{
			name = VikingNames.Plain(zdo.GetString(NameZdoKey));
			author = zdo.GetString(AuthorKey);
			return(name.Length > 0);
		}

		/// <summary>
		/// Gets a village's name as it was typed, for a sign of its own to leave out
		/// </summary>
		/// <param name="village">The village, or null</param>
		/// <returns>The name, or ""</returns>
		public static string Typed(Village village)
		{
			return((village == null || village.Zdo == null) ? "" : Raw(village));
		}

		/// <summary>
		/// Keeps a village's name at its bonfire as it's disbanded, so a village started there again has it back (see
		/// Restore)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="fire">Its bonfire's ZDO, owned by this client</param>
		public static void KeepAt(Village village, ZDO fire)
		{
			fire.Set(NameZdoKey, Raw(village));
			fire.Set(AuthorKey, village.Zdo.GetString(AuthorKey));
		}

		/// <summary>
		/// Gets the name kept at a bonfire from the village disbanded at it, for its vikings to name as they leave
		/// </summary>
		/// <param name="zdo">The bonfire's ZDO, or null</param>
		/// <returns>The name, filtered for its author's words and without rich text, or "" when there's none</returns>
		public static string AtFire(ZDO zdo)
		{
			if (zdo == null)
			{
				return("");
			}
			return(VikingNames.Filter(VikingNames.Plain(zdo.GetString(NameZdoKey)), zdo.GetString(AuthorKey)).RemoveRichTextTags().Trim());
		}

		/// <summary>
		/// Gives a village started at a bonfire the name kept there from a village disbanded at it, if any
		/// </summary>
		/// <param name="fire">The bonfire's ZDO, owned by this client</param>
		/// <param name="anchor">The new village's anchor's ZDO, owned by this client</param>
		public static void Restore(ZDO fire, ZDO anchor)
		{
			string name = fire.GetString(NameZdoKey);
			if (name.Length == 0)
			{
				return;
			}
			anchor.Set(NameZdoKey, name);
			anchor.Set(AuthorKey, fire.GetString(AuthorKey));
		}

		/// <summary>
		/// Opens the box to name a village for the local player, unless a ward keeps them out of its fire or they may not
		/// see names other players type
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="fire">Its fire, looked at</param>
		public static void RequestName(Village village, Fireplace fire)
		{
			if (TextInput.instance == null || !VikingNames.CanName() || !PrivateArea.CheckAccess(fire.transform.position))
			{
				return;
			}
			TextInput.instance.RequestText(new NameBox(village), Topic, MaxLength);
		}

		/// <summary>
		/// Notes a village's name in its generator or anchor when this client owns it - or, on the server, when no one
		/// does, taking it over to note it
		/// </summary>
		/// <param name="sender">The client that named it</param>
		/// <param name="farm">Where the village's farm stands</param>
		/// <param name="name">The name, or "" to take it away</param>
		/// <param name="author">Who named it</param>
		private static void OnName(long sender, Vector3 farm, string name, string author)
		{
			ZDO generator = VillageRespawn.FindGenerator(farm);
			if (generator == null)
			{
				return;
			}
			if (!generator.IsOwner())
			{
				if (!ZNet.instance.IsServer() || generator.HasOwner())
				{
					return;
				}
				generator.SetOwner(ZDOMan.GetSessionID());
			}
			name = VikingNames.Plain(name);
			// Turned down as it was typed (see NameBox.SetText), and here too, for the client that keeps the name
			if (VikingNames.IsMarkup(name) || (name.Length > 0 && VikingNames.IsPlace(name)))
			{
				return;
			}
			generator.Set(NameZdoKey, VikingNames.Clip(name, MaxLength));
			generator.Set(AuthorKey, author);
			Plugin.Log.LogInfo("The village at " + farm.ToString("F0") + (name.Length > 0 ? " is now called " + name : " no longer has a name"));
		}
	}
}
