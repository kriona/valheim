using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace CreatureMarker
{
	/// <summary>
	/// A trader or pet seen through a portal showing a PortalPreview view of its destination, placed where it appears
	/// on this side of the portal
	/// </summary>
	internal struct PortalNpc
	{
		/// <summary>
		/// The portal it is seen through
		/// </summary>
		public Transform portal;

		/// <summary>
		/// Where it appears on this side of the portal
		/// </summary>
		public Vector3 position;

		/// <summary>
		/// Where the top of it appears on this side of the portal
		/// </summary>
		public Vector3 top;

		/// <summary>
		/// The prefab name's hash, matching the creature line overrides
		/// </summary>
		public int hash;

		/// <summary>
		/// The name token, localized by the marker when it shows the name
		/// </summary>
		public string nameToken;

		/// <summary>
		/// Whether it is a trader rather than a trader's pet
		/// </summary>
		public bool trader;
	}

	/// <summary>
	/// The traders and pets around each portal PortalPreview has captured, so they can be marked where they show in
	/// the portals leading there
	/// </summary>
	/// <remarks>
	/// PortalPreview captures a portal's surroundings while the player is near it and saves them as frozen meshes with
	/// none of the game's components, then draws them in the portals linked to it. Traders and pets stay where they
	/// are, so the ones loaded during the capture are kept here, relative to the captured portal, in a file per world
	/// alongside PortalPreview's own cache. Creatures that move would be somewhere else by the time anyone looks, so
	/// they are left out
	/// </remarks>
	internal static class PortalNpcs
	{
		/// <summary>
		/// How far apart, in meters, a captured portal and a view's snapshot can be and still be the same portal
		/// </summary>
		private const float SamePortalDistance = 0.5f;

		/// <summary>
		/// A trader or pet recorded around a captured portal
		/// </summary>
		private class Recorded
		{
			/// <summary>
			/// Its position relative to the captured portal, in the portal's own space
			/// </summary>
			public Vector3 local;

			/// <summary>
			/// How far the top of it sits above its position
			/// </summary>
			public float height;

			/// <summary>
			/// The prefab name
			/// </summary>
			public string key;

			/// <summary>
			/// The prefab name's hash
			/// </summary>
			public int hash;

			/// <summary>
			/// The name token
			/// </summary>
			public string nameToken;

			/// <summary>
			/// Whether it is a trader rather than a trader's pet
			/// </summary>
			public bool trader;
		}

		/// <summary>
		/// A portal PortalPreview has captured and the traders and pets around it
		/// </summary>
		private class Captured
		{
			/// <summary>
			/// The captured portal's position
			/// </summary>
			public Vector3 position;

			/// <summary>
			/// The traders and pets loaded when it was captured
			/// </summary>
			public readonly List<Recorded> npcs = new List<Recorded>();
		}

		/// <summary>
		/// A portal PortalPreview drew a view in, with what is needed to see through it
		/// </summary>
		private class View
		{
			/// <summary>
			/// The frame the view was last drawn in
			/// </summary>
			public int frame;

			/// <summary>
			/// The position of the portal whose surroundings the view shows
			/// </summary>
			public Vector3 destination;

			/// <summary>
			/// The opening's outline, in meters from its center across and up the portal
			/// </summary>
			public Vector2[] outline;

			/// <summary>
			/// The height of the opening's center above the portal's position
			/// </summary>
			public float centerHeight;

			/// <summary>
			/// How far in front of the portal's position the view is drawn when seen from the front and from behind
			/// </summary>
			public float frontPlane;
			public float backPlane;
		}

		private static readonly List<Captured> captured = new List<Captured>();
		private static readonly Dictionary<Transform, View> views = new Dictionary<Transform, View>();
		private static readonly List<Transform> staleViews = new List<Transform>();

		/// <summary>
		/// The world whose captured portals are loaded, or 0 for none
		/// </summary>
		private static long loadedWorld;

		/// <summary>
		/// Records the traders and pets loaded around a portal PortalPreview is capturing, replacing what was recorded
		/// for it before
		/// </summary>
		/// <param name="position">The captured portal's position</param>
		/// <param name="rotation">The captured portal's rotation</param>
		public static void Record(Vector3 position, Quaternion rotation)
		{
			string path = GetPath();
			if (path == null)
			{
				return;
			}
			Load(path);
			Captured portal = new Captured();
			portal.position = position;
			Quaternion inverse = Quaternion.Inverse(rotation);
			foreach (NPC npc in NpcList.Get())
			{
				bool trader = (npc.source is Trader);
				if (!trader && !(npc.source is Petable))
				{
					continue;
				}
				Recorded recorded = new Recorded();
				recorded.local = inverse * (npc.source.transform.position - position);
				recorded.height = npc.GetHeight();
				recorded.key = npc.key;
				recorded.hash = npc.hash;
				recorded.nameToken = npc.nameToken;
				recorded.trader = trader;
				portal.npcs.Add(recorded);
			}
			int index = Find(position);
			if (index < 0 && portal.npcs.Count == 0)
			{
				return;
			}
			if (index >= 0)
			{
				captured.RemoveAt(index);
			}
			if (portal.npcs.Count > 0)
			{
				captured.Add(portal);
			}
			Save(path);
		}

		/// <summary>
		/// Notes that PortalPreview drew a view in a portal this frame
		/// </summary>
		/// <param name="portal">The portal the view is drawn in</param>
		/// <param name="destination">The position of the portal whose surroundings the view shows</param>
		/// <param name="outline">The opening's outline, in meters from its center across and up the portal</param>
		/// <param name="centerHeight">The height of the opening's center above the portal's position</param>
		/// <param name="frontPlane">How far in front of the portal's position the view is drawn from the front</param>
		/// <param name="backPlane">How far in front of the portal's position the view is drawn from behind</param>
		public static void Shown(Transform portal, Vector3 destination, Vector2[] outline, float centerHeight, float frontPlane, float backPlane)
		{
			if (!views.TryGetValue(portal, out View view))
			{
				view = new View();
				views[portal] = view;
			}
			view.frame = Time.frameCount;
			view.destination = destination;
			view.outline = outline;
			view.centerHeight = centerHeight;
			view.frontPlane = frontPlane;
			view.backPlane = backPlane;
		}

		/// <summary>
		/// The outline of a portal's opening on screen, on the side the camera is looking at it from, so the markers
		/// seen through it can be cut to it
		/// </summary>
		/// <param name="portal">The portal</param>
		/// <param name="camera">The main camera</param>
		/// <param name="points">Filled with the outline's points in screen pixels, cleared first</param>
		/// <param name="center">Set to the opening's center in screen pixels</param>
		/// <returns>False when the portal shows no view or part of the opening is behind the camera</returns>
		public static bool GetOpeningOnScreen(Transform portal, Camera camera, List<Vector2> points, out Vector2 center)
		{
			points.Clear();
			center = Vector2.zero;
			if (portal == null || !views.TryGetValue(portal, out View view))
			{
				return(false);
			}
			Vector3 opening = portal.position + portal.rotation * new Vector3(0f, view.centerHeight, 0f);
			bool fromBehind = (Vector3.Dot(camera.transform.position - opening, portal.forward) < 0f);
			float plane = (fromBehind ? view.backPlane : view.frontPlane);
			Vector3 screen = camera.WorldToScreenPointScaled(portal.position + portal.rotation * new Vector3(0f, view.centerHeight, plane));
			if (screen.z <= 0f)
			{
				return(false);
			}
			center = screen;
			foreach (Vector2 point in view.outline)
			{
				screen = camera.WorldToScreenPointScaled(portal.position + portal.rotation * new Vector3(point.x, view.centerHeight + point.y, plane));
				if (screen.z <= 0f)
				{
					points.Clear();
					return(false);
				}
				points.Add(screen);
			}
			return(true);
		}

		/// <summary>
		/// Fills the list with the recorded traders and pets on the far side of the portals showing a view, within Max
		/// Distance of the player and on screen
		/// </summary>
		/// <remarks>
		/// PortalPreview draws its views just before the camera renders, after the markers are placed, so a view drawn
		/// in the frame before still counts. The markers are cut to the portal's opening, which hides any that are
		/// outside it
		/// </remarks>
		/// <param name="playerPosition">The local player's position</param>
		/// <param name="maxDistanceSquared">Max Distance squared</param>
		/// <param name="seen">The list to fill, cleared first</param>
		public static void GetSeen(Vector3 playerPosition, float maxDistanceSquared, List<PortalNpc> seen)
		{
			seen.Clear();
			Camera camera = Utils.GetMainCamera();
			if (views.Count == 0 || camera == null)
			{
				return;
			}
			string path = GetPath();
			if (path == null)
			{
				return;
			}
			Load(path);
			Vector3 eye = camera.transform.position;
			foreach (KeyValuePair<Transform, View> entry in views)
			{
				Transform portal = entry.Key;
				View view = entry.Value;
				if (portal == null || view.frame < Time.frameCount - 1)
				{
					staleViews.Add(portal);
					continue;
				}
				int index = Find(view.destination);
				if (index < 0)
				{
					continue;
				}
				foreach (Recorded npc in captured[index].npcs)
				{
					// The view turns the destination half way round, so what is in front of the destination portal shows
					// behind this one
					Vector3 local = new Vector3(-npc.local.x, npc.local.y, -npc.local.z);
					Vector3 position = portal.position + portal.rotation * local;
					if (Vector3.SqrMagnitude(position - playerPosition) > maxDistanceSquared)
					{
						continue;
					}
					Vector3 top = position + Vector3.up * npc.height;
					if (!IsBeyond(portal, eye, (position + top) * 0.5f) || !IsOnScreen(camera, top))
					{
						continue;
					}
					PortalNpc npcSeen = new PortalNpc();
					npcSeen.portal = portal;
					npcSeen.position = position;
					npcSeen.top = top;
					npcSeen.hash = npc.hash;
					npcSeen.nameToken = npc.nameToken;
					npcSeen.trader = npc.trader;
					seen.Add(npcSeen);
				}
			}
			foreach (Transform portal in staleViews)
			{
				views.Remove(portal);
			}
			staleViews.Clear();
		}

		/// <summary>
		/// Whether a point is on the other side of the portal from the camera, where it can be seen through the opening
		/// </summary>
		/// <param name="portal">The portal</param>
		/// <param name="eye">The camera's position</param>
		/// <param name="point">The point</param>
		/// <returns>True when the portal's plane is between the camera and the point</returns>
		private static bool IsBeyond(Transform portal, Vector3 eye, Vector3 point)
		{
			float from = Vector3.Dot(eye - portal.position, portal.forward);
			float to = Vector3.Dot(point - portal.position, portal.forward);
			return((from > 0f) != (to > 0f));
		}

		/// <summary>
		/// Whether a point is on screen
		/// </summary>
		/// <param name="camera">The main camera</param>
		/// <param name="point">The world point</param>
		/// <returns>True when the point is in front of the camera and inside the screen</returns>
		private static bool IsOnScreen(Camera camera, Vector3 point)
		{
			Vector3 screen = camera.WorldToScreenPointScaled(point);
			return(screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height);
		}

		/// <summary>
		/// The index of the captured portal at a position
		/// </summary>
		/// <param name="position">The portal's position</param>
		/// <returns>The index in the captured list, or -1 when that portal hasn't been captured</returns>
		private static int Find(Vector3 position)
		{
			for (int i = 0; i < captured.Count; i++)
			{
				if (Vector3.SqrMagnitude(captured[i].position - position) <= SamePortalDistance * SamePortalDistance)
				{
					return(i);
				}
			}
			return(-1);
		}

		/// <summary>
		/// The path of the current world's file
		/// </summary>
		/// <returns>The path, or null when no world is loaded</returns>
		private static string GetPath()
		{
			if (ZNet.instance == null)
			{
				return(null);
			}
			long world = ZNet.instance.GetWorldUID();
			if (world == 0)
			{
				return(null);
			}
			return(Path.Combine(Paths.CachePath, Plugin.PluginGuid, "PortalNpcs-" + world.ToString(CultureInfo.InvariantCulture) + ".txt"));
		}

		/// <summary>
		/// Reads the current world's file, unless it is the one already loaded
		/// </summary>
		/// <remarks>
		/// The file has a "portal x y z" line for each captured portal, followed by a "npc trader|pet x y z height
		/// prefab name token" line for each of its traders and pets, separated by tabs
		/// </remarks>
		/// <param name="path">The file's path</param>
		private static void Load(string path)
		{
			long world = ZNet.instance.GetWorldUID();
			if (world == loadedWorld)
			{
				return;
			}
			loadedWorld = world;
			captured.Clear();
			views.Clear();
			if (!File.Exists(path))
			{
				return;
			}
			try
			{
				Captured portal = null;
				foreach (string line in File.ReadAllLines(path))
				{
					string[] parts = line.Split('\t');
					if (parts[0] == "portal" && parts.Length >= 4)
					{
						portal = new Captured();
						portal.position = new Vector3(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3]));
						captured.Add(portal);
					}
					else if (parts[0] == "npc" && parts.Length >= 8 && portal != null)
					{
						Recorded npc = new Recorded();
						npc.trader = (parts[1] == "trader");
						npc.local = new Vector3(ParseFloat(parts[2]), ParseFloat(parts[3]), ParseFloat(parts[4]));
						npc.height = ParseFloat(parts[5]);
						npc.key = parts[6];
						npc.hash = npc.key.GetStableHashCode();
						npc.nameToken = parts[7];
						portal.npcs.Add(npc);
					}
				}
			}
			catch (IOException e)
			{
				Debug.LogWarning("[" + Plugin.PluginName + "] Couldn't read " + path + ": " + e.Message);
			}
		}

		/// <summary>
		/// Writes the current world's file
		/// </summary>
		/// <param name="path">The file's path</param>
		private static void Save(string path)
		{
			StringBuilder builder = new StringBuilder();
			foreach (Captured portal in captured)
			{
				builder.Append("portal\t").Append(FormatVector(portal.position)).Append("\r\n");
				foreach (Recorded npc in portal.npcs)
				{
					builder.Append("npc\t").Append((npc.trader ? "trader" : "pet")).Append('\t');
					builder.Append(FormatVector(npc.local)).Append('\t');
					builder.Append(npc.height.ToString(CultureInfo.InvariantCulture)).Append('\t');
					builder.Append(npc.key).Append('\t').Append(npc.nameToken).Append("\r\n");
				}
			}
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, builder.ToString());
			}
			catch (IOException e)
			{
				Debug.LogWarning("[" + Plugin.PluginName + "] Couldn't write " + path + ": " + e.Message);
			}
		}

		/// <summary>
		/// Formats a vector as its three numbers separated by tabs
		/// </summary>
		/// <param name="vector">The vector</param>
		/// <returns>The text</returns>
		private static string FormatVector(Vector3 vector)
		{
			return(vector.x.ToString(CultureInfo.InvariantCulture) + "\t" + vector.y.ToString(CultureInfo.InvariantCulture) + "\t" + vector.z.ToString(CultureInfo.InvariantCulture));
		}

		/// <summary>
		/// Parses a number written by FormatVector or Save
		/// </summary>
		/// <param name="text">The number's text</param>
		/// <returns>The number, or 0 when it doesn't parse</returns>
		private static float ParseFloat(string text)
		{
			return((float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f));
		}
	}
}
