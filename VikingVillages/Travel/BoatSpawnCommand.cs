using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The kri_vv_spawnboat console command, for testing where the vikings sit on their boats - spawning one of the boats
	/// they build on the water in front of the player, moored, with living vikings in its seats, a full crew or as many
	/// as asked for (see BoatSeats)
	/// </summary>
	/// <remarks>
	/// The boat never sets off, and its vikings sit in their seats doing nothing else until it's broken - then they swim
	/// ashore and go their way like any viking with no village. It drops nothing when it breaks, like any of the vikings'
	/// boats
	/// </remarks>
	internal static class BoatSpawnCommand
	{
		/// <summary>
		/// The command's name, and the words for a full crew
		/// </summary>
		private const string Command = "kri_vv_spawnboat";
		private const string FullWord = "full";
		private const string MaxWord = "max";

		/// <summary>
		/// The word that takes away the boats the command spawned, and how near the player they have to be, in meters
		/// </summary>
		private const string DespawnWord = "despawn";
		private const float DespawnReach = 150f;

		/// <summary>
		/// The word that says where the player stands on a boat, for laying out its seats (see BoatSeats), and how near a
		/// boat they're not standing on has to be to count, in meters
		/// </summary>
		private const string PosWord = "pos";
		private const float PosReach = 30f;

		/// <summary>
		/// The word after PosWord that copies the position to the clipboard
		/// </summary>
		private const string CopyWord = "copy";

		/// <summary>
		/// How the water in front of the player is looked for - the nearest and farthest out, in what steps, how deep it
		/// has to be, and how far past that the boat goes, in meters
		/// </summary>
		private const float WaterNearest = 6f;
		private const float WaterFarthest = 80f;
		private const float WaterStep = 2f;
		private const float WaterDepth = 2f;
		private const float WaterOut = 5f;

		/// <summary>
		/// Adds the command to the console
		/// </summary>
		public static void Register()
		{
			new Terminal.ConsoleCommand(Command, "<" + string.Join("|", TravelTime.BoatPrefabNames()) + "> [full|max|vikings] - spawns a vikings' boat moored on the water in front of you, with a full crew (the default) or as many vikings as given sitting in its seats until it's broken - the boat's name, like drakkar, works too. " + DespawnWord + " takes away the boats it spawned within " + DespawnReach.ToString("F0") + " meters, and the vikings on board them. " + PosWord + " says where you stand on the boat you're on, for laying out its seats, and " + PosWord + " " + CopyWord + " copies it to the clipboard", Run, isCheat: true, optionsFetcher: Options);
		}

		/// <summary>
		/// Lists what the command can be given, for the console to complete
		/// </summary>
		/// <returns>The boats' prefabs, and the word that takes them away</returns>
		private static List<string> Options()
		{
			List<string> options = new List<string>(TravelTime.BoatPrefabNames());
			options.Add(DespawnWord);
			options.Add(PosWord);
			return(options);
		}

		/// <summary>
		/// Runs the command
		/// </summary>
		/// <param name="args">The command line</param>
		private static void Run(Terminal.ConsoleEventArgs args)
		{
			Player player = Player.m_localPlayer;
			if (player == null || ZNetScene.instance == null || ZoneSystem.instance == null)
			{
				args.Context.AddString("Load a world first");
				return;
			}
			if (args.Length > 1 && string.Equals(args[1], DespawnWord, System.StringComparison.OrdinalIgnoreCase))
			{
				args.Context.AddString(Despawn(player.transform.position));
				return;
			}
			if (args.Length > 1 && string.Equals(args[1], PosWord, System.StringComparison.OrdinalIgnoreCase))
			{
				args.Context.AddString(Position(player, args.Length > 2 && string.Equals(args[2], CopyWord, System.StringComparison.OrdinalIgnoreCase)));
				return;
			}
			int biome = ((args.Length > 1) ? TravelTime.BiomeOfBoat(args[1]) : -1);
			if (biome < 0)
			{
				args.Context.AddString("Usage: " + Command + " <" + string.Join("|", TravelTime.BoatPrefabNames()) + "> [" + FullWord + "|" + MaxWord + "|vikings]");
				return;
			}
			int crew = TravelTime.BoatCrew(biome);
			int count = crew;
			if (args.Length > 2 && !string.Equals(args[2], FullWord, System.StringComparison.OrdinalIgnoreCase) && !string.Equals(args[2], MaxWord, System.StringComparison.OrdinalIgnoreCase))
			{
				if (!int.TryParse(args[2], out int asked))
				{
					args.Context.AddString("Give how many vikings, or " + FullWord + " or " + MaxWord + " for a full crew of " + crew);
					return;
				}
				count = Mathf.Clamp(asked, 1, crew);
				if (count != asked)
				{
					args.Context.AddString("A " + TravelTime.BoatName(biome) + " carries 1 to " + crew + " vikings, so it gets " + count);
				}
			}
			Vector3 facing = player.transform.forward;
			facing.y = 0f;
			facing = ((facing.sqrMagnitude > 0.01f) ? facing.normalized : Vector3.forward);
			if (!FindWater(player.transform.position, facing, out Vector3 spot))
			{
				args.Context.AddString("Face open water within " + WaterFarthest.ToString("F0") + " meters to spawn a boat on it");
				return;
			}
			GameObject boatPrefab = ZNetScene.instance.GetPrefab(TravelTime.BoatPrefab(biome));
			GameObject vikingPrefab = ZNetScene.instance.GetPrefab(VikingCore.LivingPrefabHash);
			if (boatPrefab == null || vikingPrefab == null)
			{
				args.Context.AddString("Couldn't find the boat's or the viking's prefab");
				return;
			}
			string group = "moored" + NetTime.Now();
			VikingBoat boat = VikingBoat.Build(boatPrefab, spot, facing, group, 0, new List<string>(), new TravelRoad(), spot, 0, 0);
			if (boat == null)
			{
				args.Context.AddString("Couldn't build the boat");
				return;
			}
			boat.Moor();
			List<string> ids = new List<string>();
			for (int i = 0; i < count; i++)
			{
				Transform seat = boat.Seat(i);
				GameObject instance = Object.Instantiate(vikingPrefab, seat.position, seat.rotation);
				ZNetView view = instance.GetComponent<ZNetView>();
				string id = VikingIdentity.Ensure(view);
				if (id.Length == 0)
				{
					continue;
				}
				ZDO zdo = view.GetZDO();
				zdo.Set(VillageTravel.ConfusedKey, false);
				zdo.Set(VillageTravel.GroupKey, group);
				zdo.Set(VikingBoat.BoatKey, boat.Key);
				zdo.Set(VikingBoat.BoardKey, VikingBoat.Aboard);
				ids.Add(id);
				VillageLife life = instance.GetComponent<VillageLife>();
				if (life != null)
				{
					life.SeatAt(boat, ids.Count - 1);
				}
			}
			boat.SetCrew(ids);
			string message = "Spawned a " + TravelTime.BoatName(biome) + " moored at " + spot.ToString("F0") + " with " + ids.Count + " of its " + crew + " seats taken, out of " + boat.SeatCount + " laid out";
			Plugin.Log.LogInfo("Boat: " + message);
			args.Context.AddString(message);
		}

		/// <summary>
		/// Says where the player stands on the boat they're on, or the nearest within PosReach - from the ship's middle,
		/// across, up and along it, the way BoatSeats lays out its spots on the deck - with the line that adds a seat there,
		/// and the nearest of the boat's seats when it's a vikings' boat, written to the log too, and copied to the
		/// clipboard when asked
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="copy">Whether to copy it to the clipboard</param>
		/// <returns>The position, to show</returns>
		private static string Position(Player player, bool copy)
		{
			Ship ship = player.GetStandingOnShip();
			if (ship == null)
			{
				float nearest = PosReach;
				foreach (VikingBoat boat in VikingBoat.All)
				{
					float distance = ((boat != null) ? Vector3.Distance(boat.transform.position, player.transform.position) : float.MaxValue);
					if (distance < nearest)
					{
						nearest = distance;
						ship = boat.Ship;
					}
				}
			}
			if (ship == null)
			{
				return("Stand on a boat, or within " + PosReach.ToString("F0") + " meters of one spawned with " + Command);
			}
			Vector3 local = ship.transform.InverseTransformPoint(player.transform.position);
			string prefab = Utils.GetPrefabName(ship.gameObject);
			string message = prefab + ": " + local.x.ToString("F2") + " across, " + local.y.ToString("F2") + " up, " + local.z.ToString("F2") + " along - Deck(" + local.x.ToString("F2") + "f, " + local.y.ToString("F2") + "f, " + local.z.ToString("F2") + "f)";
			VikingBoat viking = VikingBoat.Of(ship);
			if (viking != null && viking.SeatCount > 0)
			{
				int seat = 0;
				float nearest = float.MaxValue;
				for (int i = 0; i < viking.SeatCount; i++)
				{
					float distance = Vector3.Distance(viking.Seat(i).position, player.transform.position);
					if (distance < nearest)
					{
						nearest = distance;
						seat = i;
					}
				}
				message += ", " + nearest.ToString("F1") + " m from seat " + (seat + 1) + " of " + viking.SeatCount + " (" + viking.SeatPose(seat) + ")";
			}
			Plugin.Log.LogInfo("Boat position: " + message);
			if (!copy)
			{
				return(message + " - add " + CopyWord + " to copy it: " + Command + " " + PosWord + " " + CopyWord);
			}
			GUIUtility.systemCopyBuffer = message;
			return(message + " (copied)");
		}

		/// <summary>
		/// Takes away the boats the command spawned within DespawnReach of the player, and the vikings still on board them
		/// - leaving the vikings' own boats alone, and the vikings of one that broke, who are on their way by then
		/// </summary>
		/// <param name="from">Where the player stands</param>
		/// <returns>What was taken away, to show</returns>
		private static string Despawn(Vector3 from)
		{
			List<VikingBoat> boats = VikingBoat.All.FindAll((VikingBoat boat) => boat != null && boat.Moored() && Vector3.Distance(boat.transform.position, from) <= DespawnReach);
			HashSet<string> keys = new HashSet<string>(boats.ConvertAll((VikingBoat boat) => boat.Key));
			int vikings = 0;
			foreach (Character character in new List<Character>(Character.GetAllCharacters()))
			{
				ZNetView view = ((character != null) ? character.m_nview : null);
				// Only those still on board - once the boat broke, they're on their way, and left to it
				if (view == null || !view.IsValid() || !keys.Contains(view.GetZDO().GetString(VikingBoat.BoatKey, "")) || view.GetZDO().GetInt(VikingBoat.BoardKey) != VikingBoat.Aboard)
				{
					continue;
				}
				view.ClaimOwnership();
				view.Destroy();
				vikings++;
			}
			foreach (VikingBoat boat in boats)
			{
				ZNetView view = boat.GetComponent<ZNetView>();
				if (view != null && view.IsValid())
				{
					view.ClaimOwnership();
					view.Destroy();
				}
			}
			string message = "Took away " + boats.Count + " boats spawned with " + Command + " within " + DespawnReach.ToString("F0") + " meters, and " + vikings + " vikings on board them";
			Plugin.Log.LogInfo("Boat: " + message);
			return(message);
		}

		/// <summary>
		/// Finds deep enough water in front of the player - the first spot out from them where it is, a little past it, on
		/// the water
		/// </summary>
		/// <param name="from">Where the player stands</param>
		/// <param name="facing">Which way they face, flat</param>
		/// <param name="spot">Set to the spot</param>
		/// <returns>False when there's none within WaterFarthest</returns>
		private static bool FindWater(Vector3 from, Vector3 facing, out Vector3 spot)
		{
			float water = ZoneSystem.instance.m_waterLevel;
			for (float out1 = WaterNearest; out1 <= WaterFarthest; out1 += WaterStep)
			{
				Vector3 point = from + facing * out1;
				if (ZoneSystem.instance.GetGroundHeight(point) < water - WaterDepth)
				{
					spot = point + facing * WaterOut;
					spot.y = water;
					return(true);
				}
			}
			spot = Vector3.zero;
			return(false);
		}
	}
}
