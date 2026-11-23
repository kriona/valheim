using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// House designs saved as blueprints, which village farms are built with alongside the game's own farmhouses
	/// </summary>
	/// <remarks>
	/// Every .blueprint file in the Blueprints folder beside the mod's DLL is a design - the text format PlanBuild and
	/// Infinity Hammer save, a few header lines then one line per piece after #Pieces, its fields split by ';': prefab,
	/// category, position x, y and z, rotation x, y, z and w, and more this ignores. Snap points, terrain and the other
	/// sections are skipped, and so are PlanBuild's markers. A design is laid out around the middle of its pieces, with
	/// its lowest piece standing on the ground, so it doesn't matter where its blueprint's center was. One with no door
	/// isn't used, since the vikings couldn't get in
	/// </remarks>
	internal static class VillageBlueprints
	{
		/// <summary>
		/// The farm generator's ZDO key for where its blueprint houses stand, so they're left as they're designed rather
		/// than furnished
		/// </summary>
		public static readonly int HousesKey = "kriona.ZombieApocalypse.villageBlueprintHouses".GetStableHashCode();

		/// <summary>
		/// The folder beside the mod's DLL the designs are read from, and the files in it that are designs
		/// </summary>
		private const string FolderName = "Blueprints";
		private const string FilePattern = "*.blueprint";

		/// <summary>
		/// The start of the names of PlanBuild's markers - its center, snap point and terrain pieces - which aren't
		/// built
		/// </summary>
		private const string MarkerPrefix = "piece_bp";

		/// <summary>
		/// How far past the pieces' own spots a design's footprint reaches, in meters, since a piece's spot is its middle
		/// or its foot rather than its edge
		/// </summary>
		private const float FootprintMargin = 1f;

		/// <summary>
		/// How near a house has to stand to where a blueprint house was built to be that house, in meters
		/// </summary>
		private const float MatchDistance = 1f;

		/// <summary>
		/// A piece of a design - its prefab, and where it stands and which way it faces from the design's middle
		/// </summary>
		public class Part
		{
			public string Prefab;
			public Vector3 Position;
			public Quaternion Rotation;
			public GameObject Resolved;
		}

		/// <summary>
		/// A house design read from a blueprint
		/// </summary>
		public class Design
		{
			public string Name;
			public string File;
			public List<Part> Parts = new List<Part>();

			/// <summary>
			/// Half the size of its footprint across and deep, in meters, before it's turned
			/// </summary>
			public Vector2 Half;

			/// <summary>
			/// Which way its doors face out from its middle, before it's turned, and how many it has
			/// </summary>
			public Vector3 DoorDirection = Vector3.forward;
			public int Doors;
			public int Beds;
			public int Chests;

			/// <summary>
			/// What's wrong with it, for the kri_vv_blueprints command
			/// </summary>
			public List<string> Problems = new List<string>();

			/// <summary>
			/// Whether it's built in villages - it has pieces and a door
			/// </summary>
			public bool Usable;
		}

		private static List<Design> designs;
		private static bool resolved;

		/// <summary>
		/// The folder the designs are read from
		/// </summary>
		public static string Folder
		{
			get
			{
				return(Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location), FolderName));
			}
		}

		/// <summary>
		/// Lists every design read from the folder, reading them the first time, and finding their pieces once the game
		/// has its prefabs
		/// </summary>
		/// <returns>The designs</returns>
		public static List<Design> All()
		{
			if (designs == null)
			{
				designs = Load();
			}
			if (!resolved && ZNetScene.instance != null)
			{
				foreach (Design design in designs)
				{
					Resolve(design);
				}
				resolved = true;
			}
			return(designs);
		}

		/// <summary>
		/// Lists the designs villages are built with
		/// </summary>
		/// <returns>The usable designs, or none before the game has its prefabs</returns>
		public static List<Design> Usable()
		{
			List<Design> usable = new List<Design>();
			if (ZNetScene.instance == null)
			{
				return(usable);
			}
			foreach (Design design in All())
			{
				if (design.Usable)
				{
					usable.Add(design);
				}
			}
			return(usable);
		}

		/// <summary>
		/// Forgets the designs read, so the folder is read again the next time they're wanted
		/// </summary>
		public static void Reload()
		{
			designs = null;
			resolved = false;
		}

		/// <summary>
		/// Adds the kri_vv_blueprints command to the console, which lists the house designs, or reads them again first with
		/// "reload"
		/// </summary>
		public static void RegisterCommand()
		{
			new Terminal.ConsoleCommand("kri_vv_blueprints", "[reload] - lists the house blueprints Zombie Apocalypse builds villages with, reading them again first with reload", RunCommand, optionsFetcher: () => new List<string> { "reload" });
		}

		/// <summary>
		/// Runs the kri_vv_blueprints command
		/// </summary>
		/// <param name="args">The command line</param>
		private static void RunCommand(Terminal.ConsoleEventArgs args)
		{
			if (args.Length > 1 && args[1].ToLowerInvariant() == "reload")
			{
				Reload();
			}
			if (ZNetScene.instance == null)
			{
				args.Context.AddString("Load a world first - the blueprints' pieces are looked up in the game");
				return;
			}
			List<Design> all = All();
			args.Context.AddString(all.Count + " house blueprints in " + Folder);
			foreach (Design design in all)
			{
				string size = (design.Half.x * 2f).ToString("F0") + "x" + (design.Half.y * 2f).ToString("F0") + "m";
				args.Context.AddString((design.Usable ? "  " : "  (not used) ") + design.Name + " - " + design.File + ": " + design.Parts.Count + " pieces, " + size + ", " + design.Doors + " doors, " + design.Beds + " beds, " + design.Chests + " chests");
				foreach (string problem in design.Problems)
				{
					args.Context.AddString("      " + problem);
				}
			}
		}

		/// <summary>
		/// Reads every design in the folder
		/// </summary>
		/// <returns>The designs, in file name order</returns>
		private static List<Design> Load()
		{
			List<Design> loaded = new List<Design>();
			string folder = Folder;
			if (!Directory.Exists(folder))
			{
				return(loaded);
			}
			List<string> files = new List<string>(Directory.GetFiles(folder, FilePattern, SearchOption.AllDirectories));
			files.Sort(System.StringComparer.OrdinalIgnoreCase);
			foreach (string file in files)
			{
				try
				{
					loaded.Add(Read(file));
				}
				catch (System.Exception e)
				{
					Plugin.Log.LogWarning("Couldn't read the blueprint " + file + ": " + e.Message);
				}
			}
			Plugin.Log.LogInfo("Read " + loaded.Count + " house blueprints from " + folder);
			return(loaded);
		}

		/// <summary>
		/// Reads a design from a blueprint file and lays its pieces out around its middle, on the ground
		/// </summary>
		/// <param name="file">The file</param>
		/// <returns>The design</returns>
		private static Design Read(string file)
		{
			return(Parse(file, File.ReadAllLines(file)));
		}

		/// <summary>
		/// Reads a design from the lines of a blueprint and lays its pieces out around its middle, on the ground - the
		/// piece lines after #Pieces, up to the next section, leaving out PlanBuild's markers
		/// </summary>
		/// <param name="file">The blueprint's file, whose name the design goes by unless the blueprint names it</param>
		/// <param name="lines">The blueprint's lines</param>
		/// <returns>The design</returns>
		internal static Design Parse(string file, IEnumerable<string> lines)
		{
			Design design = new Design();
			design.File = Path.GetFileName(file);
			design.Name = Path.GetFileNameWithoutExtension(file);
			bool inPieces = false;
			int unreadable = 0;
			foreach (string raw in lines)
			{
				string line = raw.Trim();
				if (line.Length == 0)
				{
					continue;
				}
				if (line.StartsWith("#"))
				{
					if (line.StartsWith("#Name:"))
					{
						string name = line.Substring("#Name:".Length).Trim();
						if (name.Length > 0)
						{
							design.Name = name;
						}
					}
					inPieces = (line == "#Pieces");
					continue;
				}
				if (!inPieces)
				{
					continue;
				}
				Part part = ReadPart(line);
				if (part == null)
				{
					unreadable++;
					continue;
				}
				if (!part.Prefab.StartsWith(MarkerPrefix, System.StringComparison.OrdinalIgnoreCase))
				{
					design.Parts.Add(part);
				}
			}
			if (unreadable > 0)
			{
				design.Problems.Add(unreadable + " piece lines it couldn't read");
			}
			if (design.Parts.Count == 0)
			{
				design.Problems.Add("no pieces");
				return(design);
			}
			// Laid out around the middle of its footprint, its lowest piece on the ground
			Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
			Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
			foreach (Part part in design.Parts)
			{
				min = Vector3.Min(min, part.Position);
				max = Vector3.Max(max, part.Position);
			}
			Vector3 middle = new Vector3((min.x + max.x) / 2f, min.y, (min.z + max.z) / 2f);
			foreach (Part part in design.Parts)
			{
				part.Position -= middle;
			}
			design.Half = new Vector2((max.x - min.x) / 2f + FootprintMargin, (max.z - min.z) / 2f + FootprintMargin);
			return(design);
		}

		/// <summary>
		/// Reads a piece line of a blueprint
		/// </summary>
		/// <param name="line">The line</param>
		/// <returns>The piece, or null when the line can't be read</returns>
		internal static Part ReadPart(string line)
		{
			string[] fields = line.Split(';');
			if (fields.Length < 9 || fields[0].Trim().Length == 0)
			{
				return(null);
			}
			float[] numbers = new float[7];
			for (int i = 0; i < 7; i++)
			{
				// Older blueprints were saved with commas for decimal points
				if (!float.TryParse(fields[i + 2].Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]))
				{
					return(null);
				}
			}
			Quaternion rotation = new Quaternion(numbers[3], numbers[4], numbers[5], numbers[6]);
			float length = Mathf.Sqrt(rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w);
			if (length < 0.0001f)
			{
				return(null);
			}
			Part part = new Part();
			part.Prefab = fields[0].Trim();
			part.Position = new Vector3(numbers[0], numbers[1], numbers[2]);
			part.Rotation = new Quaternion(rotation.x / length, rotation.y / length, rotation.z / length, rotation.w / length);
			return(part);
		}

		/// <summary>
		/// Finds the prefab of each of a design's pieces, leaving out those the game doesn't have or that aren't
		/// building pieces, and works out which way its doors face
		/// </summary>
		/// <param name="design">The design</param>
		private static void Resolve(Design design)
		{
			Dictionary<string, int> unknown = new Dictionary<string, int>();
			Dictionary<string, int> notPieces = new Dictionary<string, int>();
			List<Vector3> doors = new List<Vector3>();
			List<Part> kept = new List<Part>();
			foreach (Part part in design.Parts)
			{
				GameObject prefab = ZNetScene.instance.GetPrefab(part.Prefab);
				if (prefab == null)
				{
					unknown[part.Prefab] = (unknown.TryGetValue(part.Prefab, out int count) ? count : 0) + 1;
					continue;
				}
				if (prefab.GetComponent<Piece>() == null)
				{
					notPieces[part.Prefab] = (notPieces.TryGetValue(part.Prefab, out int count) ? count : 0) + 1;
					continue;
				}
				part.Resolved = prefab;
				kept.Add(part);
				if (prefab.GetComponent<Door>() != null)
				{
					design.Doors++;
					doors.Add(part.Position);
				}
				if (prefab.GetComponent<Bed>() != null)
				{
					design.Beds++;
				}
				if (prefab.GetComponent<Container>() != null)
				{
					design.Chests++;
				}
			}
			design.Parts = kept;
			foreach (KeyValuePair<string, int> entry in unknown)
			{
				design.Problems.Add("unknown prefab " + entry.Key + " x" + entry.Value + ", left out");
			}
			foreach (KeyValuePair<string, int> entry in notPieces)
			{
				design.Problems.Add(entry.Key + " x" + entry.Value + " isn't a building piece, left out");
			}
			if (design.Doors == 0)
			{
				design.Problems.Add("no door - not used, since the vikings couldn't get in");
			}
			else
			{
				design.DoorDirection = DoorDirection(doors);
			}
			if (design.Parts.Count > 0 && design.Beds == 0)
			{
				design.Problems.Add("no bed - its vikings sleep sitting up");
			}
			design.Usable = (design.Parts.Count > 0 && design.Doors > 0);
		}

		/// <summary>
		/// Works out which way a design's doors face out from its middle, seen from above - toward where they are on
		/// the whole, or forward when they're spread evenly around it
		/// </summary>
		/// <param name="doors">Where its doors stand, around its middle</param>
		/// <returns>The direction, level and of length 1</returns>
		internal static Vector3 DoorDirection(List<Vector3> doors)
		{
			Vector3 sum = Vector3.zero;
			foreach (Vector3 door in doors)
			{
				sum += new Vector3(door.x, 0f, door.z);
			}
			return((sum.sqrMagnitude > 0.01f) ? sum.normalized : Vector3.forward);
		}

		/// <summary>
		/// Builds a design, as the farm it's in is generated
		/// </summary>
		/// <param name="design">The design</param>
		/// <param name="position">Where its middle stands, on the ground</param>
		/// <param name="rotation">Which way it's turned</param>
		/// <param name="mode">How the farm is being placed - a ghost placement saves its pieces without showing them</param>
		public static void Build(Design design, Vector3 position, Quaternion rotation, ZoneSystem.SpawnMode mode)
		{
			foreach (Part part in design.Parts)
			{
				GameObject piece = Object.Instantiate(part.Resolved, position + rotation * part.Position, rotation * part.Rotation);
				VillageRecords.MarkBuilt(piece);
				ZNetView view = piece.GetComponent<ZNetView>();
				if (view != null && view.GetZDO() != null && piece.GetComponent<WearNTear>() != null)
				{
					view.GetZDO().Set(ZDOVars.s_preSnow, true);
				}
				// The generation is in its ghost window, so the piece's ZDO is saved and the object can go, the same as
				// the generator's own rooms
				if (mode == ZoneSystem.SpawnMode.Ghost)
				{
					Object.Destroy(piece);
				}
			}
		}

		/// <summary>
		/// Notes in a farm generator's ZDO that a blueprint house stands at a spot
		/// </summary>
		/// <param name="generator">The farm's generator</param>
		/// <param name="center">Where the house's middle stands</param>
		public static void RecordHouse(DungeonGenerator generator, Vector3 center)
		{
			ZDO zdo = ((generator.m_nview != null) ? generator.m_nview.GetZDO() : null);
			if (zdo == null)
			{
				return;
			}
			List<Vector3> houses = ReadHouses(zdo);
			houses.Add(center);
			zdo.Set(HousesKey, WritePoints(houses));
		}

		/// <summary>
		/// Checks whether a village's house was built from a blueprint
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="house">The house</param>
		/// <returns>True when it was</returns>
		public static bool IsBlueprintHouse(Village village, VillagePaths.House house)
		{
			foreach (Vector3 center in ReadHouses(village.Zdo))
			{
				if (Utils.DistanceXZ(center, house.Center) <= MatchDistance)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Reads where a farm's blueprint houses stand from its generator's ZDO
		/// </summary>
		/// <param name="zdo">The generator's ZDO</param>
		/// <returns>Where their middles stand</returns>
		private static List<Vector3> ReadHouses(ZDO zdo)
		{
			return(ReadPoints((zdo != null) ? zdo.GetByteArray(HousesKey) : null));
		}

		/// <summary>
		/// Packs a list of points to keep in a ZDO - how many there are, then each in turn - the way the farm's blueprint
		/// houses and the sitting logs its vikings are to take down (see VillageSeats.TakeDowns) are kept
		/// </summary>
		/// <param name="points">The points</param>
		/// <returns>The packed points</returns>
		internal static byte[] WritePoints(List<Vector3> points)
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(points.Count);
			foreach (Vector3 point in points)
			{
				pkg.Write(point);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Unpacks a list of points kept in a ZDO (see WritePoints)
		/// </summary>
		/// <param name="bytes">The packed points, or null when none were kept</param>
		/// <returns>The points, in the order they were packed</returns>
		internal static List<Vector3> ReadPoints(byte[] bytes)
		{
			List<Vector3> points = new List<Vector3>();
			if (bytes == null)
			{
				return(points);
			}
			ZPackage pkg = new ZPackage(bytes);
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				points.Add(pkg.ReadVector3());
			}
			return(points);
		}
	}
}
