using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The portals that join each village of the Ashlands and the Deep North to a village of the Plains or an easier
	/// biome - an Ashlands stone portal near the middle of the one and a wood portal near the middle of the other, each
	/// with a path to its village's nearest path and a sign on its frame naming the village on its other side. Both are
	/// named after the Ashlands or Deep North village, with a number after the name when another portal has it already,
	/// and the game joins the two by their name
	/// </summary>
	/// <remarks>
	/// A portal goes up as its village's paths are laid. Only the server has the world's plan of its locations and every
	/// portal, so the client laying an Ashlands or Deep North village's paths asks it where the portal leads and waits
	/// for its answer. The server finds the village on the other side (see VillagePortalSites), generates its farm,
	/// names it and puts its portal up at the farm's middle, where it waits for the farm's paths to be laid - so the
	/// portal can be used before anyone has been there - and whoever lays them takes it down and puts it up again near
	/// the village's middle with its path and sign. A village the server finds nowhere for gets no portal
	/// </remarks>
	internal static class VillagePortals
	{
		/// <summary>
		/// On a portal, where the farm of the village it stands in and of the village on its other side stand, and
		/// whether it's waiting at the farm's middle for its village's paths to be laid, with the name for its sign
		/// </summary>
		public static readonly int VillageKey = "kriona.VikingVillages.portalVillage".GetStableHashCode();
		public static readonly int OtherKey = "kriona.VikingVillages.portalOther".GetStableHashCode();
		public static readonly int WaitingKey = "kriona.VikingVillages.portalWaiting".GetStableHashCode();
		public static readonly int SignTextKey = "kriona.VikingVillages.portalSignText".GetStableHashCode();

		/// <summary>
		/// On a sign, that it stands on a village's portal
		/// </summary>
		private static readonly int SignKey = "kriona.VikingVillages.portalSign".GetStableHashCode();

		/// <summary>
		/// Where a portal with no farm noted stands, for a key that isn't set - far from any farm
		/// </summary>
		public static readonly Vector3 Nowhere = new Vector3(1e9f, 0f, 1e9f);

		/// <summary>
		/// The portal in the Ashlands or Deep North village, the one in the village on its other side, and the sign on
		/// each
		/// </summary>
		public const string StonePortal = "portal_stone";
		public const string WoodPortal = "portal_wood";
		private const string SignPrefab = "sign";

		/// <summary>
		/// The biomes whose villages get a portal, and those the village on its other side can be in
		/// </summary>
		private const Heightmap.Biome PortalBiomes = Heightmap.Biome.AshLands | Heightmap.Biome.DeepNorth;
		private const Heightmap.Biome DestinationBiomes = Heightmap.Biome.Meadows | Heightmap.Biome.BlackForest | Heightmap.Biome.Swamp | Heightmap.Biome.Mountain | Heightmap.Biome.Plains;

		/// <summary>
		/// How far from a farm's spot a portal noted as standing at it can be, in meters - as far as its generator can be
		/// from its planned spot
		/// </summary>
		public const float FarmMatch = 16f;

		/// <summary>
		/// How much open ground a portal needs around its middle, how far from the farm's middle it's looked for, how far
		/// its frame reaches out from its middle, where its path starts, and how far around the middle of the village's
		/// new fire it can't stand on, in meters
		/// </summary>
		private const float PortalRoom = 3f;
		private const float PortalSearch = 40f;
		private const float PortalHalf = 2f;
		private const float FireKeep = 5f;

		/// <summary>
		/// How far a portal is set into the ground so it touches it, in meters
		/// </summary>
		public const float PortalSink = 0.1f;

		/// <summary>
		/// Where a portal's sign hangs on its frame - how far to the portal's right, up from its foot and in front of it,
		/// in meters, and how far it's turned from the way the portal faces, in degrees
		/// </summary>
		internal struct SignPlace
		{
			public Vector3 Offset;
			public float Turn;
		}

		/// <summary>
		/// Where the sign hangs on each portal, by the portal's prefab - beside the frame, partway up it
		/// </summary>
		private static readonly Dictionary<string, SignPlace> SignPlaces = new Dictionary<string, SignPlace>
		{
			{ StonePortal, new SignPlace { Offset = new Vector3(2.283f, 1.716f, 0.748f), Turn = -14.5f } },
			{ WoodPortal, new SignPlace { Offset = new Vector3(-1.573f, 1.249f, 0.270f), Turn = -174f } }
		};

		/// <summary>
		/// The name of a portal whose Ashlands or Deep North village has no name, since every name is taken
		/// </summary>
		private const string FallbackName = "Village";

		/// <summary>
		/// The most characters a portal's name can have - as many as the box for naming a portal takes (see
		/// TeleportWorld.Interact), so a player who opens it doesn't cut the name short and lose its link
		/// </summary>
		internal const int TagMax = 10;

		/// <summary>
		/// On a village's generator, once its portal is up - the portal's name, where the farm on its other side stands,
		/// and what the sign on the portal says and where it stands, for the vikings to build them back as they
		/// were (see Restore)
		/// </summary>
		private static readonly int LinkTagKey = "kriona.VikingVillages.villagePortalTag".GetStableHashCode();
		private static readonly int LinkKey = "kriona.VikingVillages.villagePortalLink".GetStableHashCode();
		private static readonly int LinkSignTextKey = "kriona.VikingVillages.villagePortalSignText".GetStableHashCode();
		private static readonly int LinkSignAtKey = "kriona.VikingVillages.villagePortalSignAt".GetStableHashCode();

		/// <summary>
		/// How far from where a portal's sign stood a sign can stand and still be it, in meters
		/// </summary>
		private const float SignMatch = 0.5f;

		/// <summary>
		/// The routed RPCs - a client asking the server whether the village on the other side of a village's portal is
		/// still up, with the village's generator and where the other farm stands, and the server's answer, with the
		/// generator, whether it is, and its name and who gave it
		/// </summary>
		private const string LinkAskRpc = Plugin.PluginGuid + ".VillagePortalLinkAsk";
		private const string LinkAnswerRpc = Plugin.PluginGuid + ".VillagePortalLinkAnswer";

		/// <summary>
		/// Seconds a server's answer about the village on the other side of a portal is kept before it's asked again
		/// </summary>
		private const float LinkCheckInterval = 60f;

		/// <summary>
		/// What the server said about the village on the other side of a village's portal - whether it's still up, its
		/// name as it was typed and who gave it, and when it was said, by Time.time
		/// </summary>
		private struct Link
		{
			public bool Up;
			public string Name;
			public string Author;
			public float At;
		}

		/// <summary>
		/// The server's answers about the villages on the other side of portals, by the village's generator, and when
		/// this client last asked about each
		/// </summary>
		private static readonly Dictionary<ZDOID, Link> links = new Dictionary<ZDOID, Link>();
		private static readonly Dictionary<ZDOID, float> linkAskedAt = new Dictionary<ZDOID, float>();

		/// <summary>
		/// The routed RPCs - a client asking the server where an Ashlands or Deep North village's portal leads, with its
		/// generator, where it stands and its name, and the server's answer, with the generator and what it found
		/// </summary>
		private const string AskRpc = Plugin.PluginGuid + ".VillagePortalAsk";
		private const string AnswerRpc = Plugin.PluginGuid + ".VillagePortalAnswer";

		/// <summary>
		/// Seconds between a client's questions about the same village, and between the server's looks at what it's
		/// been asked
		/// </summary>
		private const float AskInterval = 5f;
		private const float ServerInterval = 1f;

		/// <summary>
		/// What the server found for an Ashlands or Deep North village's portal - whether it leads anywhere, its name,
		/// and where the farm of the village on its other side stands and what that village is called
		/// </summary>
		internal struct Answer
		{
			public bool Found;
			public string Tag;
			public Vector3 Destination;
			public string DestinationName;
		}

		/// <summary>
		/// A client's question the server is working on - who asked, about which village, and since when
		/// </summary>
		internal class Request
		{
			public long Sender;
			public ZDOID Generator;
			public Vector3 Farm;
			public string Name;
			public float Since;
		}

		/// <summary>
		/// The portal a village's paths are laid with - the prefab, its name, where the farm on its other side stands and
		/// what its sign says, and the portal already waiting at the farm's middle to put up in its place, if any
		/// </summary>
		public class Plan
		{
			public string Prefab;
			public string Tag;
			public Vector3 Other;
			public string SignText;
			public ZDO Waiting;
		}

		/// <summary>
		/// Where a portal goes up - on the ground, whether the ground there is flattened first, which way it faces, and
		/// its path to the village's nearest path, or null when there's no way there
		/// </summary>
		public class Site
		{
			public Vector3 Spot;
			public bool Flatten;
			public Quaternion Rotation;
			public List<Vector3> Leg;
		}

		/// <summary>
		/// The server's answers about each village, by its generator, and when this client last asked about each
		/// </summary>
		private static readonly Dictionary<ZDOID, Answer> answers = new Dictionary<ZDOID, Answer>();
		private static readonly Dictionary<ZDOID, float> askedAt = new Dictionary<ZDOID, float>();

		/// <summary>
		/// The questions the server is working on, by the village's generator, and when it next looks at them
		/// </summary>
		private static readonly Dictionary<ZDOID, Request> requests = new Dictionary<ZDOID, Request>();
		private static float nextServerCheck;

		/// <summary>
		/// Registers the RPCs with a world's routed RPC, starting the world with no questions
		/// </summary>
		public static void Register()
		{
			if (ZRoutedRpc.instance == null)
			{
				return;
			}
			ZRoutedRpc.instance.Register<ZDOID, Vector3, string>(AskRpc, OnAsk);
			ZRoutedRpc.instance.Register<ZDOID, ZPackage>(AnswerRpc, OnAnswer);
			ZRoutedRpc.instance.Register<ZDOID, Vector3>(LinkAskRpc, OnLinkAsk);
			ZRoutedRpc.instance.Register<ZDOID, ZPackage>(LinkAnswerRpc, OnLinkAnswer);
			requests.Clear();
			nextServerCheck = 0f;
		}

		/// <summary>
		/// Forgets the server's answers when leaving a world
		/// </summary>
		public static void Forget()
		{
			answers.Clear();
			askedAt.Clear();
			requests.Clear();
			links.Clear();
			linkAskedAt.Clear();
		}

		/// <summary>
		/// Checks whether a biome's villages get a portal
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <returns>True for the Ashlands and the Deep North</returns>
		internal static bool GetsPortal(Heightmap.Biome biome)
		{
			return(biome != Heightmap.Biome.None && (biome & PortalBiomes) == biome);
		}

		/// <summary>
		/// Checks whether a portal can lead to a village in a biome
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <returns>True for the Plains and every biome before it</returns>
		internal static bool IsDestination(Heightmap.Biome biome)
		{
			return(biome != Heightmap.Biome.None && (biome & DestinationBiomes) == biome);
		}

		/// <summary>
		/// Checks whether a sign stands on a village's portal
		/// </summary>
		/// <param name="zdo">The sign's ZDO</param>
		/// <returns>True when it does</returns>
		public static bool IsPortalSign(ZDO zdo)
		{
			return(zdo.GetBool(SignKey));
		}

		/// <summary>
		/// Names a portal after its village, with " #2", " #3" and so on after the name when a portal has it already -
		/// the name cut short so the whole of it, number and all, fits in TagMax characters
		/// </summary>
		/// <param name="name">The village's name</param>
		/// <param name="taken">The names the world's portals have</param>
		/// <returns>The portal's name</returns>
		internal static string UniqueTag(string name, ICollection<string> taken)
		{
			HashSet<string> used = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
			foreach (string tag in taken)
			{
				used.Add(tag);
			}
			string plain = FitTag(name, "");
			if (!used.Contains(plain))
			{
				return(plain);
			}
			for (int i = 2; ; i++)
			{
				string tag = FitTag(name, " #" + i);
				if (!used.Contains(tag))
				{
					return(tag);
				}
			}
		}

		/// <summary>
		/// Cuts a name short so it fits in TagMax characters with an ending after it, leaving no space before the ending
		/// </summary>
		/// <param name="name">The name</param>
		/// <param name="ending">What goes after it, or ""</param>
		/// <returns>The name and its ending</returns>
		internal static string FitTag(string name, string ending)
		{
			int room = Mathf.Max(0, TagMax - ending.Length);
			string fitted = ((name.Length > room) ? name.Substring(0, room) : name).TrimEnd();
			return((fitted + ending).Trim());
		}

		/// <summary>
		/// Finds the point nearest another across the ground
		/// </summary>
		/// <param name="points">The points</param>
		/// <param name="to">The other point</param>
		/// <param name="nearest">The nearest</param>
		/// <returns>False when there are no points</returns>
		internal static bool NearestPoint(List<Vector3> points, Vector3 to, out Vector3 nearest)
		{
			nearest = to;
			float best = float.MaxValue;
			foreach (Vector3 point in points)
			{
				float distance = Utils.DistanceXZ(point, to);
				if (distance < best)
				{
					nearest = point;
					best = distance;
				}
			}
			return(best < float.MaxValue);
		}

		/// <summary>
		/// Works out which way across the ground a portal faces - toward where its path heads, or toward a fallback
		/// point when that's right where it stands, or else north
		/// </summary>
		/// <param name="from">Where the portal stands</param>
		/// <param name="toward">Where its path heads</param>
		/// <param name="fallback">The point it faces when its path heads nowhere</param>
		/// <returns>The way it faces, flat and a meter long</returns>
		internal static Vector3 Facing(Vector3 from, Vector3 toward, Vector3 fallback)
		{
			foreach (Vector3 target in new[] { toward, fallback })
			{
				Vector3 way = target - from;
				way.y = 0f;
				if (way.sqrMagnitude > 0.01f)
				{
					return(way.normalized);
				}
			}
			return(Vector3.forward);
		}

		/// <summary>
		/// Works out the portal a village's paths are laid with, on the client laying them - for an Ashlands or Deep
		/// North village, the one the server found a village on the other side for, asking it every AskInterval seconds
		/// until it answers, and for a village a portal leads to, the one waiting at its farm's middle
		/// </summary>
		/// <remarks>
		/// An Ashlands or Deep North village is named first, since its portal is named after it
		/// </remarks>
		/// <param name="generator">The farm's generator, owned by this client</param>
		/// <param name="farm">Where the generator stands</param>
		/// <param name="plan">The portal, or null when the village gets none</param>
		/// <returns>False while it waits for the village's name or the server's answer</returns>
		public static bool Prepare(ZDO generator, Vector3 farm, out Plan plan)
		{
			plan = null;
			if (WorldGenerator.instance == null)
			{
				return(true);
			}
			if (GetsPortal(WorldGenerator.instance.GetBiome(farm)))
			{
				return(Ask(generator, farm, out plan));
			}
			ZDO waiting = FindWaiting(farm);
			if (waiting != null)
			{
				plan = new Plan { Prefab = WoodPortal, Tag = waiting.GetString(ZDOVars.s_tag), Other = waiting.GetVec3(OtherKey, Nowhere), SignText = waiting.GetString(SignTextKey), Waiting = waiting };
			}
			return(true);
		}

		/// <summary>
		/// Finds where an Ashlands or Deep North village's portal leads, from the server's answer, asking for one when
		/// there's none and it wasn't asked within AskInterval seconds
		/// </summary>
		/// <param name="generator">The farm's generator, owned by this client</param>
		/// <param name="farm">Where the generator stands</param>
		/// <param name="plan">The portal, or null when it leads nowhere</param>
		/// <returns>False while it waits for the village's name or the server's answer</returns>
		private static bool Ask(ZDO generator, Vector3 farm, out Plan plan)
		{
			plan = null;
			VillageNames.TryAutoName(generator, farm);
			if (!VillageNames.At(generator, out string name, out string _))
			{
				if (!VillagePlannedNames.Ready || !VillageNameList.Ready)
				{
					return(false);
				}
				name = FallbackName;
			}
			ZDOID id = generator.m_uid;
			if (answers.TryGetValue(id, out Answer answer))
			{
				if (answer.Found)
				{
					plan = new Plan { Prefab = StonePortal, Tag = answer.Tag, Other = answer.Destination, SignText = answer.DestinationName };
				}
				return(true);
			}
			if (ZRoutedRpc.instance != null && (!askedAt.TryGetValue(id, out float asked) || Time.time - asked >= AskInterval))
			{
				askedAt[id] = Time.time;
				ZRoutedRpc.instance.InvokeRoutedRPC(AskRpc, id, farm, name);
			}
			return(false);
		}

		/// <summary>
		/// Finds the portal waiting at a farm's middle for its village's paths to be laid, among the ZDOs this client has
		/// </summary>
		/// <param name="farm">Where the farm's generator stands</param>
		/// <returns>The portal's ZDO, or null</returns>
		private static ZDO FindWaiting(Vector3 farm)
		{
			if (Game.instance == null)
			{
				return(null);
			}
			List<ZDO> zdos = new List<ZDO>();
			VillageRespawn.ZdosNear(farm, FarmMatch * 2f, zdos);
			foreach (ZDO zdo in zdos)
			{
				if (Game.instance.PortalPrefabHash.Contains(zdo.GetPrefab()) && zdo.GetBool(WaitingKey) && Utils.DistanceXZ(zdo.GetVec3(VillageKey, Nowhere), farm) <= FarmMatch)
				{
					return(zdo);
				}
			}
			return(null);
		}

		/// <summary>
		/// Leaves the portal waiting at a farm's middle out of what its paths steer around, since it's put up again
		/// elsewhere
		/// </summary>
		/// <param name="blockers">The objects around the village</param>
		/// <param name="plan">The village's portal, or null</param>
		public static void LeaveOut(List<VillagePaths.Blocker> blockers, Plan plan)
		{
			if (plan == null || plan.Waiting == null)
			{
				return;
			}
			blockers.RemoveAll((VillagePaths.Blocker blocker) => blocker.View != null && blocker.View.GetZDO() == plan.Waiting);
		}

		/// <summary>
		/// Finds where a village's portal goes up - the open ground nearest the farm's middle with room all around it,
		/// kept clear of the village's new fire, facing the nearest of the village's paths, or its nearest house when it
		/// has none - and its path there, from in front of it, steering around what the village's paths steer around
		/// </summary>
		/// <param name="grid">The village's ground, with its paths marked</param>
		/// <param name="farm">Where the farm's generator stands</param>
		/// <param name="fire">Where the village's new fire goes, or null when it has one already, which its paths steer
		/// around</param>
		/// <param name="path">The points of every path the village is laid with</param>
		/// <param name="houses">The village's houses, with their doors found</param>
		/// <returns>The site, or null when there's no open ground for it</returns>
		public static Site FindSite(VillagePaths.Grid grid, Vector3 farm, Vector3? fire, List<Vector3> path, List<VillagePaths.House> houses)
		{
			if (fire.HasValue)
			{
				grid.Mark(new List<VillagePaths.Blocker> { new VillagePaths.Blocker { Center = fire.Value, Radius = FireKeep, Kind = VillagePaths.Kind.Avoid } });
			}
			if (!VillagePaths.FindLevelSpot(grid, farm, PortalRoom, PortalSearch, out Vector3 spot, out bool flatten))
			{
				return(null);
			}
			VillagePaths.House target = new VillagePaths.House { Center = (fire ?? farm) };
			Vector3 toward = target.Center;
			if (NearestPoint(path, spot, out Vector3 nearest))
			{
				target = new VillagePaths.House { Center = nearest };
				toward = nearest;
			}
			else if (houses.Count > 0)
			{
				target = houses[0];
				foreach (VillagePaths.House house in houses)
				{
					if (Utils.DistanceXZ(house.Center, spot) < Utils.DistanceXZ(target.Center, spot))
					{
						target = house;
					}
				}
				toward = target.Center;
				if (target.Doors != null && NearestPoint(target.Doors, spot, out Vector3 door))
				{
					toward = door;
				}
			}
			Vector3 forward = Facing(spot, toward, fire ?? farm);
			grid.Mark(new List<VillagePaths.Blocker> { new VillagePaths.Blocker { Center = spot, Radius = PortalHalf, Kind = VillagePaths.Kind.Avoid } });
			VillagePaths.House from = new VillagePaths.House { Center = spot, HalfSize = PortalHalf, Doors = new List<Vector3> { spot + forward * PortalHalf } };
			List<Vector3> leg = grid.FindPath(from, target);
			if (leg == null)
			{
				Plugin.Log.LogWarning("Couldn't find a path from the portal at " + spot.ToString("F0") + " to the rest of the village at " + farm.ToString("F0"));
			}
			return(new Site { Spot = spot, Flatten = flatten, Rotation = Quaternion.LookRotation(forward), Leg = leg });
		}

		/// <summary>
		/// Puts a village's portal up where it goes, flattening the ground there first when it needs it, taking down the
		/// portal waiting at the farm's middle in its place, and hangs a sign on it naming the village on its other side.
		/// With no site found, a waiting portal stays where it is
		/// </summary>
		/// <remarks>
		/// The portal and its sign are the village's own, the same as the pieces it was generated with - they break into
		/// what they're built of, and the vikings build them back as they were (see Restore)
		/// </remarks>
		/// <param name="plan">The portal</param>
		/// <param name="site">Where it goes, or null when there's no open ground for it</param>
		/// <param name="generator">The farm's generator, owned by this client, which notes the portal</param>
		/// <param name="farm">Where the generator stands</param>
		public static void Build(Plan plan, Site site, ZDO generator, Vector3 farm)
		{
			if (site == null)
			{
				Plugin.Log.LogWarning("Found no open ground for the portal " + plan.Tag + " near " + farm.ToString("F0") + ((plan.Waiting != null) ? ", so it stays at the farm's middle" : ", so the village has none"));
				return;
			}
			GameObject prefab = ZNetScene.instance.GetPrefab(plan.Prefab);
			if (prefab == null)
			{
				Plugin.Log.LogWarning("Couldn't find the " + plan.Prefab + " prefab to put up the village's portal near " + farm.ToString("F0"));
				return;
			}
			if (site.Flatten)
			{
				VillagePaths.Flatten(site.Spot);
			}
			if (plan.Waiting != null)
			{
				TakeDown(plan.Waiting);
			}
			GameObject portal = Object.Instantiate(prefab, site.Spot - Vector3.up * PortalSink, site.Rotation);
			Name(portal, plan.Tag, farm, plan.Other);
			generator.Set(LinkTagKey, plan.Tag);
			generator.Set(LinkKey, plan.Other);
			generator.Set(LinkSignTextKey, plan.SignText ?? "");
			if (PutSign(portal, plan.Prefab, plan.SignText, out Vector3 signAt))
			{
				generator.Set(LinkSignAtKey, signAt);
			}
			Plugin.Log.LogInfo("Put up the portal " + plan.Tag + " at " + site.Spot.ToString("F1") + (site.Flatten ? " on ground flattened for it" : "") + " in the village at " + farm.ToString("F0") + ", leading to the village at " + plan.Other.ToString("F0") + ((site.Leg != null) ? ", with a path to the village's paths" : ""));
		}

		/// <summary>
		/// Takes down a portal, loaded or not
		/// </summary>
		/// <param name="portal">Its ZDO</param>
		internal static void TakeDown(ZDO portal)
		{
			ZNetView view = ZNetScene.instance.FindInstance(portal);
			if (view != null)
			{
				view.ClaimOwnership();
				ZNetScene.instance.Destroy(view.gameObject);
				return;
			}
			portal.SetOwner(ZDOMan.GetSessionID());
			ZDOMan.instance.DestroyZDO(portal);
		}

		/// <summary>
		/// Hangs a sign on a portal's frame where SignPlaces puts it, with a village's name on it - unless the village has
		/// no name
		/// </summary>
		/// <param name="portal">The portal, just put up</param>
		/// <param name="portalPrefab">The portal's prefab</param>
		/// <param name="text">The village's name</param>
		/// <param name="placed">Where the sign stands</param>
		/// <returns>False when no sign was put up</returns>
		private static bool PutSign(GameObject portal, string portalPrefab, string text, out Vector3 placed)
		{
			placed = Vector3.zero;
			GameObject prefab = ZNetScene.instance.GetPrefab(SignPrefab);
			if (prefab == null || string.IsNullOrEmpty(text) || !PlaceSign(portalPrefab, portal.transform.position, portal.transform.rotation, out placed, out Quaternion rotation))
			{
				return(false);
			}
			GameObject sign = Object.Instantiate(prefab, placed, rotation);
			ZNetView view = sign.GetComponent<ZNetView>();
			if (view != null && view.GetZDO() != null)
			{
				view.GetZDO().Set(SignKey, true);
			}
			VillageSigns.Rewrite(sign, text, "", "");
			return(true);
		}

		/// <summary>
		/// Works out where a portal's sign hangs and which way it faces, from SignPlaces
		/// </summary>
		/// <param name="portalPrefab">The portal's prefab</param>
		/// <param name="at">Where the portal stands</param>
		/// <param name="rotation">Which way the portal faces</param>
		/// <param name="signAt">Where the sign hangs</param>
		/// <param name="signRotation">Which way the sign faces</param>
		/// <returns>False for a portal with no place for a sign</returns>
		internal static bool PlaceSign(string portalPrefab, Vector3 at, Quaternion rotation, out Vector3 signAt, out Quaternion signRotation)
		{
			signAt = at;
			signRotation = rotation;
			if (!SignPlaces.TryGetValue(portalPrefab, out SignPlace place))
			{
				return(false);
			}
			signAt = at + rotation * place.Offset;
			// A turn about the up axis, built by hand since Quaternion.AngleAxis calls into the engine
			float half = place.Turn * Mathf.Deg2Rad * 0.5f;
			signRotation = rotation * new Quaternion(0f, Mathf.Sin(half), 0f, Mathf.Cos(half));
			return(true);
		}

		/// <summary>
		/// Names a portal and notes the village it stands in and the one on its other side
		/// </summary>
		/// <param name="portal">The portal, just put up</param>
		/// <param name="tag">Its name</param>
		/// <param name="farm">Where the farm of its village stands</param>
		/// <param name="other">Where the farm of the village on its other side stands</param>
		private static void Name(GameObject portal, string tag, Vector3 farm, Vector3 other)
		{
			ZNetView view = portal.GetComponent<ZNetView>();
			if (view == null || view.GetZDO() == null)
			{
				return;
			}
			ZDO zdo = view.GetZDO();
			zdo.Set(ZDOVars.s_tag, tag);
			zdo.Set(VillageKey, farm);
			zdo.Set(OtherKey, other);
		}

		/// <summary>
		/// Gives a portal or sign the vikings just built back what the village's own had - the portal its name and link
		/// to the village on its other side, and the sign on it that village's name as it goes by now, or else the
		/// name it had
		/// </summary>
		/// <param name="instance">The piece just built back</param>
		/// <param name="village">Its village</param>
		/// <returns>True when it was the village's portal or the sign on it</returns>
		public static bool Restore(GameObject instance, Village village)
		{
			if (village == null || village.Zdo == null || instance == null)
			{
				return(false);
			}
			ZDO generator = village.Zdo;
			string tag = generator.GetString(LinkTagKey);
			if (tag.Length == 0)
			{
				return(false);
			}
			if (instance.GetComponent<TeleportWorld>() != null)
			{
				Name(instance, tag, village.Farm, generator.GetVec3(LinkKey, Nowhere));
				return(true);
			}
			ZNetView view = instance.GetComponent<ZNetView>();
			if (instance.GetComponent<Sign>() == null || view == null || view.GetZDO() == null || Vector3.Distance(instance.transform.position, generator.GetVec3(LinkSignAtKey, Nowhere)) > SignMatch)
			{
				return(false);
			}
			view.GetZDO().Set(SignKey, true);
			if (links.TryGetValue(generator.m_uid, out Link link) && link.Up && link.Name.Length > 0)
			{
				VillageSigns.Rewrite(instance, link.Name, link.Author, "");
			}
			else
			{
				VillageSigns.Rewrite(instance, generator.GetString(LinkSignTextKey), "", "");
			}
			return(true);
		}

		/// <summary>
		/// Leaves a village's portal and the sign on it out of the pieces its vikings build back while the village on its
		/// other side isn't known to be up - not there anymore, abandoned, or not yet heard about from the server (see
		/// LinkUp) - and the sign while the portal is down, since it hangs on the portal
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="missing">The village's pieces that no longer stand</param>
		public static void HoldBack(Village village, List<PieceRecord> missing)
		{
			if (village == null || village.Zdo == null || Game.instance == null || village.Zdo.GetString(LinkTagKey).Length == 0)
			{
				return;
			}
			Vector3 signAt = village.Zdo.GetVec3(LinkSignAtKey, Nowhere);
			bool portalMissing = missing.Exists((PieceRecord record) => Game.instance.PortalPrefabHash.Contains(record.Prefab));
			bool up = LinkUp(village, out string _, out string _);
			if (portalMissing && !up)
			{
				missing.RemoveAll((PieceRecord record) => Game.instance.PortalPrefabHash.Contains(record.Prefab));
			}
			if (portalMissing || !up)
			{
				missing.RemoveAll((PieceRecord record) => record.Prefab == SignHash && Vector3.Distance(record.Position, signAt) <= SignMatch);
			}
		}

		/// <summary>
		/// The sign's prefab hash
		/// </summary>
		private static readonly int SignHash = SignPrefab.GetStableHashCode();

		/// <summary>
		/// Finds whether the village on the other side of a village's portal is still up - its farm's generator is still
		/// there and it isn't abandoned - from the server's answer, asking it again every LinkCheckInterval seconds
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="name">The other village's name as it was typed, or "" when it has none</param>
		/// <param name="author">Who gave the name</param>
		/// <returns>False when it isn't, the village has no portal, or the server hasn't answered yet</returns>
		private static bool LinkUp(Village village, out string name, out string author)
		{
			name = "";
			author = "";
			if (village == null || village.Zdo == null)
			{
				return(false);
			}
			Vector3 other = village.Zdo.GetVec3(LinkKey, Nowhere);
			if (other == Nowhere)
			{
				return(false);
			}
			ZDOID id = village.Zdo.m_uid;
			bool known = links.TryGetValue(id, out Link link);
			if ((!known || Time.time - link.At >= LinkCheckInterval) && ZRoutedRpc.instance != null && (!linkAskedAt.TryGetValue(id, out float asked) || Time.time - asked >= AskInterval))
			{
				linkAskedAt[id] = Time.time;
				ZRoutedRpc.instance.InvokeRoutedRPC(LinkAskRpc, id, other);
			}
			if (!known || !link.Up)
			{
				return(false);
			}
			name = link.Name;
			author = link.Author;
			return(true);
		}

		/// <summary>
		/// Finds the village on the other side of a village's portal, while it's still up and has a name - for its
		/// vikings to talk about
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="name">The other village's name, filtered for its author's words and without rich text</param>
		/// <param name="biome">The biome the other village's farm stands in</param>
		/// <returns>False when there's no such village, or the server hasn't said yet</returns>
		public static bool Linked(Village village, out string name, out Heightmap.Biome biome)
		{
			biome = Heightmap.Biome.None;
			if (!LinkUp(village, out string raw, out string author))
			{
				name = "";
				return(false);
			}
			name = VikingNames.Filter(raw, author).RemoveRichTextTags().Trim();
			Vector3 other = village.Zdo.GetVec3(LinkKey, Nowhere);
			biome = ((WorldGenerator.instance != null) ? WorldGenerator.instance.GetBiome(other) : Heightmap.Biome.None);
			return(name.Length > 0);
		}

		/// <summary>
		/// Answers a client asking whether the village on the other side of a village's portal is still up, on the server
		/// - with its name and who gave it
		/// </summary>
		/// <param name="sender">The client</param>
		/// <param name="generator">The village's generator</param>
		/// <param name="other">Where the other village's farm stands</param>
		private static void OnLinkAsk(long sender, ZDOID generator, Vector3 other)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer() || ZRoutedRpc.instance == null)
			{
				return;
			}
			ZDO found = VillageRespawn.FindGenerator(other);
			bool up = (found != null && !found.GetBool(VillagePaths.AbandonedKey));
			string name = "";
			string author = "";
			if (up)
			{
				VillageNames.At(found, out name, out author);
			}
			ZPackage pkg = new ZPackage();
			pkg.Write(up);
			pkg.Write(name ?? "");
			pkg.Write(author ?? "");
			ZRoutedRpc.instance.InvokeRoutedRPC(sender, LinkAnswerRpc, generator, pkg);
		}

		/// <summary>
		/// Keeps the server's answer about the village on the other side of a village's portal
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="generator">The village's generator</param>
		/// <param name="pkg">Whether it's up, its name and who gave it</param>
		private static void OnLinkAnswer(long sender, ZDOID generator, ZPackage pkg)
		{
			try
			{
				Link link = new Link();
				link.Up = pkg.ReadBool();
				link.Name = pkg.ReadString();
				link.Author = pkg.ReadString();
				link.At = Time.time;
				links[generator] = link;
			}
			catch (System.Exception exception)
			{
				Plugin.Log.LogWarning("Couldn't read the server's answer about the village on the other side of the portal of the village with the generator " + generator + ": " + exception.Message);
			}
		}

		/// <summary>
		/// Notes a client's question about an Ashlands or Deep North village's portal on the server, to work on until it
		/// can answer (see Update)
		/// </summary>
		/// <param name="sender">The client</param>
		/// <param name="generator">The village's generator</param>
		/// <param name="farm">Where the generator stands</param>
		/// <param name="name">The village's name</param>
		private static void OnAsk(long sender, ZDOID generator, Vector3 farm, string name)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer())
			{
				return;
			}
			if (requests.TryGetValue(generator, out Request request))
			{
				request.Sender = sender;
				request.Name = name;
				return;
			}
			requests[generator] = new Request { Sender = sender, Generator = generator, Farm = farm, Name = name, Since = Time.time };
			nextServerCheck = 0f;
		}

		/// <summary>
		/// Works on the questions about portals every ServerInterval seconds on the server, answering each once the
		/// village on the other side has been found, generated and named, or none can be found - call it from the
		/// plugin's Update
		/// </summary>
		public static void Update()
		{
			if (requests.Count == 0 || Time.time < nextServerCheck || ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null || ZNetScene.instance == null || ZDOMan.instance == null || WorldGenerator.instance == null || Game.instance == null || ZRoutedRpc.instance == null)
			{
				return;
			}
			nextServerCheck = Time.time + ServerInterval;
			foreach (Request request in new List<Request>(requests.Values))
			{
				Answer answer = new Answer();
				bool done;
				try
				{
					done = VillagePortalSites.Advance(request, out answer);
				}
				catch (System.Exception exception)
				{
					Plugin.Log.LogWarning("Couldn't find where the portal of the village at " + request.Farm.ToString("F0") + " leads: " + exception);
					answer = new Answer();
					done = true;
				}
				if (!done)
				{
					continue;
				}
				requests.Remove(request.Generator);
				ZPackage pkg = new ZPackage();
				pkg.Write(answer.Found);
				pkg.Write(answer.Tag ?? "");
				pkg.Write(answer.Destination);
				pkg.Write(answer.DestinationName ?? "");
				ZRoutedRpc.instance.InvokeRoutedRPC(request.Sender, AnswerRpc, request.Generator, pkg);
			}
		}

		/// <summary>
		/// Keeps the server's answer about an Ashlands or Deep North village's portal
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="generator">The village's generator</param>
		/// <param name="pkg">What the server found</param>
		private static void OnAnswer(long sender, ZDOID generator, ZPackage pkg)
		{
			try
			{
				Answer answer = new Answer();
				answer.Found = pkg.ReadBool();
				answer.Tag = pkg.ReadString();
				answer.Destination = pkg.ReadVector3();
				answer.DestinationName = pkg.ReadString();
				answers[generator] = answer;
			}
			catch (System.Exception exception)
			{
				Plugin.Log.LogWarning("Couldn't read the server's answer about the portal of the village with the generator " + generator + ": " + exception.Message);
			}
		}
	}
}
