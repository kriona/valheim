using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Shows how far a village reaches, as a ring on the ground like a workbench's while placing it, turned on and off
	/// with AreaKey while looking at the village's fire - and makes the village's other fires gathering fires, or
	/// ordinary fires again, and starts a village at a bonfire a player built (see VillageFounding), with the same key,
	/// or disbands such a village with shift and the key at its fire - and opens the box to name the village with
	/// VillageNames.NameKey at its fire, and shows the key for its stock (see VillageStock) in the fire's hover text
	/// </summary>
	/// <remarks>
	/// The ring is the workbench's own, without its floating hammer, drawn around each of the village's zones - around
	/// its fenced ground, its houses, its pieces and along its paths (see Village.Zones) - and follows the village as it grows
	/// or shrinks. It's only shown to the player who turned it on, until they turn it off again - the village notes who
	/// has it shown, so it's shown again whenever they're back, after the village unloads or they rejoin the world. A
	/// gathering fire is a fire in the village that gives warmth and stands outside its houses, which its vikings gather and rest at like the village's own (see VillageLife.ChooseGatherFire),
	/// unless a ward keeps the player out
	/// </remarks>
	internal static class VillageArea
	{
		/// <summary>
		/// The key that turns the ring on and off
		/// </summary>
		public const KeyCode AreaKey = KeyCode.V;

		/// <summary>
		/// The piece whose ring is drawn, and how far apart the ring's marks are when the piece's own spacing can't be
		/// read, in meters
		/// </summary>
		private const string RingPiece = "piece_workbench";
		private const float FallbackSpacing = 1.5f;

		/// <summary>
		/// The color the village's ring is drawn in, in place of the workbench's white
		/// </summary>
		private static readonly Color RingColor = new Color(0.35f, 1f, 0.35f, 1f);

		/// <summary>
		/// The material color properties tinted to RingColor, keeping each one's own alpha
		/// </summary>
		private static readonly string[] ColorProperties = { "_Color", "_TintColor" };

		/// <summary>
		/// The workbench ring's mark, tinted to RingColor, which the village's rings are drawn with - made the first time
		/// it's wanted and kept under an inactive holder, so it's never shown itself
		/// </summary>
		private static GameObject greenMark;
		private static GameObject markHolder;

		/// <summary>
		/// The rings shown around each village, one for each of its zones in the same order, by its generator's or
		/// anchor's ZDO
		/// </summary>
		private static readonly Dictionary<ZDOID, List<CircleProjector>> rings = new Dictionary<ZDOID, List<CircleProjector>>();

		/// <summary>
		/// On a farm's generator or a started village's anchor, the players who have its ring shown, by player ID, so it's
		/// shown again for them when they come back - kept with the village, so a player's own data doesn't fill up with
		/// villages
		/// </summary>
		private static readonly int ShownKey = "kriona.ZombieApocalypse.villageAreaShown".GetStableHashCode();

		/// <summary>
		/// The routed RPC that notes a player showing or hiding a village's ring, with where the village's farm stands,
		/// the player's ID and whether it's shown
		/// </summary>
		public const string ShownRpc = Plugin.PluginGuid + ".VillageAreaShown";

		/// <summary>
		/// The most players a village notes as having its ring shown
		/// </summary>
		private const int MaxShown = 64;

		/// <summary>
		/// The villages the local player has their rings shown for, by generator or anchor ZDO, whether they're loaded or
		/// not, and the villages whose note of it has been read since the world was joined
		/// </summary>
		private static readonly HashSet<ZDOID> wanted = new HashSet<ZDOID>();
		private static readonly HashSet<ZDOID> read = new HashSet<ZDOID>();

		/// <summary>
		/// Registers the RPC with a world's routed RPC, forgetting the rings of the world being left
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register<Vector3, long, bool>(ShownRpc, OnShown);
			wanted.Clear();
			read.Clear();
		}

		/// <summary>
		/// Turns a village's ring on or off when the key is pressed while looking at its fire - or with shift held,
		/// disbands a village a player started - makes another fire in a village a gathering fire or an ordinary one, or
		/// starts a village at a bonfire a player built, opens the box to name a village when the naming key is pressed
		/// at its fire, and keeps the rings shown around their villages - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			Player player = Player.m_localPlayer;
			if (KeyTarget(player, out Fireplace fire, out Village village, out Village around))
			{
				if (village != null && Shifted() && village.IsFounded())
				{
					VillageFounding.TryDisband(player, village);
				}
				else if (village != null)
				{
					Toggle(village);
				}
				else if (around != null)
				{
					ToggleGathering(player, around, fire);
				}
				else
				{
					VillageFounding.TryStart(player, fire);
				}
			}
			if (NameTarget(player, out Fireplace named, out Village naming))
			{
				VillageNames.RequestName(naming, named);
			}
			Follow();
		}

		/// <summary>
		/// Finds the village whose name the naming key opens the box for this frame - nothing unless it was just pressed
		/// while the player takes input and looks at a village's own fire
		/// </summary>
		/// <param name="player">The local player, or null</param>
		/// <param name="fire">The fire looked at</param>
		/// <param name="village">The village whose fire it is</param>
		/// <returns>True when it opens the box</returns>
		private static bool NameTarget(Player player, out Fireplace fire, out Village village)
		{
			fire = null;
			village = null;
			if (player == null || !ZInput.GetKeyDown(VillageNames.NameKey, false) || !player.TakeInput())
			{
				return(false);
			}
			GameObject hover = player.GetHoverObject();
			fire = ((hover != null) ? hover.GetComponentInParent<Fireplace>() : null);
			village = ((fire != null) ? VillageDebug.Of(fire) : null);
			return(village != null);
		}

		/// <summary>
		/// Checks whether the key does something this frame - it was just pressed while the player takes input and looks
		/// at a village's fire, at another fire in a village that could be a gathering fire, or at a bonfire a village
		/// could be started at - so the game's own use of the key, toggling auto-pickup, is left out (see
		/// VillageAreaKeyPatch)
		/// </summary>
		/// <returns>True when it does</returns>
		public static bool KeyTaken()
		{
			return(KeyTarget(Player.m_localPlayer, out Fireplace _, out Village _, out Village _));
		}

		/// <summary>
		/// Finds what the key does this frame - nothing unless it was just pressed while the player takes input and looks
		/// at a fire
		/// </summary>
		/// <param name="player">The local player, or null</param>
		/// <param name="fire">The fire looked at</param>
		/// <param name="village">The village whose own fire it is, or null</param>
		/// <param name="around">The village it could be a gathering fire of, when it isn't a village's own, or null</param>
		/// <returns>True when the key shows or hides a village's area, makes a fire a gathering fire or an ordinary one,
		/// or starts a village at it - with neither village set</returns>
		private static bool KeyTarget(Player player, out Fireplace fire, out Village village, out Village around)
		{
			fire = null;
			village = null;
			around = null;
			if (player == null || !ZInput.GetKeyDown(AreaKey, false) || !player.TakeInput())
			{
				return(false);
			}
			GameObject hover = player.GetHoverObject();
			fire = ((hover != null) ? hover.GetComponentInParent<Fireplace>() : null);
			if (fire == null)
			{
				return(false);
			}
			village = VillageDebug.Of(fire);
			if (village != null || GatherVillage(fire, out around))
			{
				return(true);
			}
			around = null;
			return(VillageFounding.Offers(fire));
		}

		/// <summary>
		/// The line a fire adds to its hover text for the key - showing or hiding the village's area on the village's
		/// own fire, making another fire in it a gathering fire or an ordinary one, or starting a village at a bonfire a
		/// player built
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>The line, or null when the key does nothing there</returns>
		public static string HoverLine(Fireplace fire)
		{
			Village village = VillageDebug.Of(fire);
			if (village != null)
			{
				string name = VillageNames.Get(village);
				string lines = ((name.Length > 0) ? "<color=orange>" + name + "</color>\n" : "") + Prompt((IsShown(village) ? "Hide" : "Show") + " village area");
				if (VikingNames.CanName())
				{
					lines += "\n" + Prompt(VillageNames.NameKey.ToString(), ((name.Length > 0) ? "Rename village" : "Name village"));
				}
				lines += "\n" + Prompt(VillageStock.StockKey.ToString(), "Village stock");
				return(village.IsFounded() ? lines + "\n" + Prompt("Shift+" + AreaKey, "Disband village") : lines);
			}
			if (GatherVillage(fire, out Village _))
			{
				return(Prompt(IsGathering(fire) ? "Make ordinary fire" : "Make village gathering fire"));
			}
			return(VillageFounding.Offers(fire) ? Prompt("Start a village") : null);
		}

		/// <summary>
		/// Formats the key and what it does the way the game's hover text does
		/// </summary>
		/// <param name="action">What it does</param>
		/// <returns>The line</returns>
		private static string Prompt(string action)
		{
			return(Prompt(AreaKey.ToString(), action));
		}

		/// <summary>
		/// Formats a key and what it does the way the game's hover text does
		/// </summary>
		/// <param name="key">The key, as it's shown</param>
		/// <param name="action">What it does</param>
		/// <returns>The line</returns>
		private static string Prompt(string key, string action)
		{
			return("[<color=yellow><b>" + key + "</b></color>] " + action);
		}

		/// <summary>
		/// Checks whether either shift key is held, which has the key disband a village a player started rather than show
		/// its area
		/// </summary>
		/// <returns>True when one is</returns>
		private static bool Shifted()
		{
			return(ZInput.GetKey(KeyCode.LeftShift, false) || ZInput.GetKey(KeyCode.RightShift, false));
		}

		/// <summary>
		/// Finds the village a fire could be a gathering fire of - one it's in, when it gives warmth and stands outside
		/// the village's houses
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <param name="village">The village</param>
		/// <returns>False when it couldn't be one</returns>
		private static bool GatherVillage(Fireplace fire, out Village village)
		{
			Vector3 position = fire.transform.position;
			village = Village.Find(position);
			if (village == null || !village.Contains(position) || village.InHouse(position, 0f))
			{
				return(false);
			}
			return(VillageLife.AreaReach(fire, EffectArea.Type.Heat) > 0f);
		}

		/// <summary>
		/// Checks whether a fire is a gathering fire
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>True when it is</returns>
		private static bool IsGathering(Fireplace fire)
		{
			ZNetView view = fire.GetComponent<ZNetView>();
			return(view != null && view.IsValid() && view.GetZDO().GetBool(Village.GatherFireKey));
		}

		/// <summary>
		/// Makes a fire in a village a gathering fire, or an ordinary one again, unless a ward keeps the player out
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="village">The village</param>
		/// <param name="fire">The fire</param>
		private static void ToggleGathering(Player player, Village village, Fireplace fire)
		{
			ZNetView view = fire.GetComponent<ZNetView>();
			if (view == null || !view.IsValid() || !PrivateArea.CheckAccess(fire.transform.position))
			{
				return;
			}
			bool gathering = !IsGathering(fire);
			view.ClaimOwnership();
			view.GetZDO().Set(Village.GatherFireKey, gathering);
			village.ForgetGatherFires();
			player.Message(MessageHud.MessageType.Center, (gathering ? "The Vikings will gather at this fire" : "The Vikings won't gather at this fire"));
		}

		/// <summary>
		/// Checks whether the local player has a village's ring shown
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when they have</returns>
		public static bool IsShown(Village village)
		{
			return(wanted.Contains(village.Zdo.m_uid));
		}

		/// <summary>
		/// Shows a village's ring for the local player, or takes it away when it's shown, and has the village note it
		/// </summary>
		/// <param name="village">The village</param>
		private static void Toggle(Village village)
		{
			ZDOID id = village.Zdo.m_uid;
			bool show = !wanted.Contains(id);
			if (show)
			{
				wanted.Add(id);
			}
			else
			{
				wanted.Remove(id);
			}
			Player player = Player.m_localPlayer;
			if (ZRoutedRpc.instance != null && player != null)
			{
				ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, ShownRpc, village.Farm, player.GetPlayerID(), show);
			}
			Follow();
		}

		/// <summary>
		/// Notes a player showing or hiding a village's ring in its generator or anchor when this client owns it - or, on
		/// the server, when no one does, taking it over to note it - keeping the latest MaxShown players
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="farm">Where the village's farm stands</param>
		/// <param name="playerId">The player's ID</param>
		/// <param name="shown">Whether they have it shown</param>
		private static void OnShown(long sender, Vector3 farm, long playerId, bool shown)
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
			List<long> players = UpdateShown(ReadShown(generator), playerId, shown, MaxShown);
			generator.Set(ShownKey, EncodeShown(players));
		}

		/// <summary>
		/// Notes a player showing or hiding a village's ring in the players who have it shown - moving one who shows it
		/// to the end, and keeping only the latest to show it
		/// </summary>
		/// <param name="players">The players' IDs, the last to show it last, changed</param>
		/// <param name="playerId">The player's ID</param>
		/// <param name="shown">Whether they have it shown</param>
		/// <param name="max">The most players kept</param>
		/// <returns>The same list</returns>
		internal static List<long> UpdateShown(List<long> players, long playerId, bool shown, int max)
		{
			players.Remove(playerId);
			if (shown)
			{
				players.Add(playerId);
			}
			if (players.Count > max)
			{
				players.RemoveRange(0, players.Count - max);
			}
			return(players);
		}

		/// <summary>
		/// Reads the players a village notes as having its ring shown
		/// </summary>
		/// <param name="generator">The village's generator's or anchor's ZDO</param>
		/// <returns>Their IDs, the last to show it last</returns>
		private static List<long> ReadShown(ZDO generator)
		{
			return(DecodeShown(generator.GetByteArray(ShownKey)));
		}

		/// <summary>
		/// Reads the players who have a village's ring shown from the bytes they're noted as - none when there are no
		/// bytes or they can't be read
		/// </summary>
		/// <param name="bytes">The bytes (see EncodeShown), or null</param>
		/// <returns>Their IDs, the last to show it last</returns>
		internal static List<long> DecodeShown(byte[] bytes)
		{
			List<long> players = new List<long>();
			if (bytes == null)
			{
				return(players);
			}
			try
			{
				ZPackage pkg = new ZPackage(bytes);
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					players.Add(pkg.ReadLong());
				}
			}
			catch (System.Exception)
			{
				players.Clear();
			}
			return(players);
		}

		/// <summary>
		/// Writes the players who have a village's ring shown as bytes
		/// </summary>
		/// <param name="players">Their IDs, the last to show it last</param>
		/// <returns>The bytes</returns>
		internal static byte[] EncodeShown(List<long> players)
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(players.Count);
			foreach (long player in players)
			{
				pkg.Write(player);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Shows the ring of each loaded village the local player has it shown for - reading whether they had it shown
		/// from the village the first time it loads since the world was joined - and keeps each ring around its village
		/// as it grows or shrinks, taking away the rings of villages no longer loaded or hidden
		/// </summary>
		private static void Follow()
		{
			Player player = Player.m_localPlayer;
			Dictionary<ZDOID, Village> loaded = new Dictionary<ZDOID, Village>();
			foreach (Village village in Village.Loaded())
			{
				ZDOID id = village.Zdo.m_uid;
				loaded[id] = village;
				// The first time a village loads since the world was joined, its note of who has its ring shown says
				// whether the local player had it shown when they were last here
				if (player != null && read.Add(id) && ReadShown(village.Zdo).Contains(player.GetPlayerID()))
				{
					wanted.Add(id);
				}
				if (wanted.Contains(id))
				{
					Show(id, village);
				}
			}
			List<ZDOID> gone = new List<ZDOID>();
			foreach (ZDOID id in rings.Keys)
			{
				if (!wanted.Contains(id) || !loaded.ContainsKey(id))
				{
					gone.Add(id);
				}
			}
			foreach (ZDOID id in gone)
			{
				foreach (CircleProjector ring in rings[id])
				{
					if (ring != null)
					{
						Object.Destroy(ring.gameObject);
					}
				}
				rings.Remove(id);
			}
		}

		/// <summary>
		/// Shows a ring around each of a village's zones, making or taking away rings as its zones come and go
		/// </summary>
		/// <param name="id">The village's generator's or anchor's ZDO</param>
		/// <param name="village">The village</param>
		private static void Show(ZDOID id, Village village)
		{
			if (!rings.TryGetValue(id, out List<CircleProjector> shown))
			{
				shown = new List<CircleProjector>();
				rings[id] = shown;
			}
			shown.RemoveAll((CircleProjector ring) => ring == null);
			while (shown.Count < village.Zones.Count)
			{
				CircleProjector ring = MakeRing();
				if (ring == null)
				{
					Plugin.Log.LogWarning("Couldn't find the workbench's ring to show the village's area with");
					wanted.Remove(id);
					return;
				}
				shown.Add(ring);
			}
			while (shown.Count > village.Zones.Count)
			{
				Object.Destroy(shown[shown.Count - 1].gameObject);
				shown.RemoveAt(shown.Count - 1);
			}
			for (int i = 0; i < shown.Count; i++)
			{
				Place(shown[i], village.Zones[i]);
				VillageAreaRing clip = shown[i].GetComponent<VillageAreaRing>();
				if (clip != null)
				{
					clip.SetOthers(village.Zones, i);
				}
			}
		}

		/// <summary>
		/// Puts a ring around one of a village's zones at its reach, with its marks as far apart as the workbench's
		/// </summary>
		/// <param name="ring">The ring</param>
		/// <param name="zone">The zone</param>
		private static void Place(CircleProjector ring, Village.Zone zone)
		{
			ring.transform.position = zone.Center;
			ring.m_radius = zone.Radius;
			ring.m_nrOfSegments = Segments(zone.Radius, Spacing());
		}

		/// <summary>
		/// Works out how many marks a ring has so they're no farther apart than a spacing, and never fewer than 8
		/// </summary>
		/// <param name="radius">The ring's radius, in meters</param>
		/// <param name="spacing">The most the marks are apart, in meters, more than 0</param>
		/// <returns>How many marks</returns>
		internal static int Segments(float radius, float spacing)
		{
			return(Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / spacing)));
		}

		/// <summary>
		/// Works out how far apart a ring's marks are, or FallbackSpacing when the ring has no size or no marks
		/// </summary>
		/// <param name="radius">The ring's radius, in meters</param>
		/// <param name="segments">How many marks it has</param>
		/// <returns>The spacing, in meters</returns>
		internal static float MarkSpacing(float radius, int segments)
		{
			if (radius <= 0f || segments <= 0)
			{
				return(FallbackSpacing);
			}
			return(2f * Mathf.PI * radius / segments);
		}

		/// <summary>
		/// Makes a copy of the workbench's ring, shown, drawn in RingColor, without the hammer floating over it
		/// </summary>
		/// <returns>The ring, or null when the workbench has none</returns>
		private static CircleProjector MakeRing()
		{
			GameObject marker = Marker();
			if (marker == null)
			{
				return(null);
			}
			GameObject copy = Object.Instantiate(marker);
			copy.name = "ZombieApocalypse village area";
			// The marker's own children are the workbench's floating hammer - the ring's marks are made from its prefab
			// once it's shown
			foreach (Transform child in copy.transform)
			{
				child.gameObject.SetActive(false);
				Object.Destroy(child.gameObject);
			}
			CircleProjector ring = copy.GetComponent<CircleProjector>();
			copy.AddComponent<VillageAreaRing>();
			Tint(copy);
			GameObject mark = GreenMark(ring.m_prefab);
			if (mark != null)
			{
				ring.m_prefab = mark;
			}
			copy.SetActive(true);
			return(ring);
		}

		/// <summary>
		/// Gets the ring's mark tinted to RingColor, making it the first time
		/// </summary>
		/// <param name="mark">The workbench ring's mark</param>
		/// <returns>The tinted mark, or null when the ring has none</returns>
		private static GameObject GreenMark(GameObject mark)
		{
			if (greenMark != null || mark == null)
			{
				return(greenMark);
			}
			markHolder = new GameObject("ZombieApocalypse village area mark");
			markHolder.SetActive(false);
			Object.DontDestroyOnLoad(markHolder);
			greenMark = Object.Instantiate(mark, markHolder.transform, false);
			greenMark.name = mark.name;
			Tint(greenMark);
			return(greenMark);
		}

		/// <summary>
		/// Tints an object's particles and materials to RingColor, giving each renderer its own copies of its materials
		/// so the workbench's own ring stays as it is
		/// </summary>
		/// <param name="target">The object</param>
		private static void Tint(GameObject target)
		{
			foreach (ParticleSystem particles in target.GetComponentsInChildren<ParticleSystem>(true))
			{
				ParticleSystem.MainModule main = particles.main;
				main.startColor = RingColor;
			}
			foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
			{
				Material[] materials = renderer.sharedMaterials;
				for (int i = 0; i < materials.Length; i++)
				{
					if (materials[i] == null)
					{
						continue;
					}
					Material tinted = new Material(materials[i]);
					foreach (string property in ColorProperties)
					{
						if (tinted.HasProperty(property))
						{
							Color color = RingColor;
							color.a = tinted.GetColor(property).a;
							tinted.SetColor(property, color);
						}
					}
					materials[i] = tinted;
				}
				renderer.sharedMaterials = materials;
			}
		}

		/// <summary>
		/// Finds the workbench's ring
		/// </summary>
		/// <returns>Its marker, or null when it has none</returns>
		private static GameObject Marker()
		{
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(RingPiece) : null);
			CraftingStation station = ((prefab != null) ? prefab.GetComponent<CraftingStation>() : null);
			return((station != null && station.m_areaMarker != null && station.m_areaMarker.GetComponent<CircleProjector>() != null) ? station.m_areaMarker : null);
		}

		/// <summary>
		/// Works out how far apart the workbench's ring's marks are
		/// </summary>
		/// <returns>The spacing, in meters</returns>
		private static float Spacing()
		{
			GameObject marker = Marker();
			CircleProjector ring = ((marker != null) ? marker.GetComponent<CircleProjector>() : null);
			if (ring == null)
			{
				return(FallbackSpacing);
			}
			return(MarkSpacing(ring.m_radius, ring.m_nrOfSegments));
		}
	}
}
