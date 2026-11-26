using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Notices what's news (see VikingNews) on the client it happens on and reports it to the server - a boss, a
	/// miniboss or a sea serpent killed, by the players who hit it, a monster killed by a tree, by the player who felled
	/// the tree, the local player's death, and the local player coming near a leviathan or going into a dungeon
	/// </summary>
	/// <remarks>
	/// The game notes each player who hits a creature in its ZDO, and marks a creature killed by someone cheating, which
	/// isn't news - nor is a creature killed by the console's kill commands. A tree's log is given the player who felled
	/// it, which its halves keep when it breaks, so the log that lands on a monster knows who dropped it
	/// </remarks>
	internal static class VikingNewsWatch
	{
		/// <summary>
		/// On a tree's log, the player who felled the tree or last hit the log, by player ID
		/// </summary>
		private static readonly int FellerKey = "kriona.VikingVillages.feller".GetStableHashCode();

		/// <summary>
		/// How long a player's hit on a tree counts toward felling it when something else brings it down - a falling log,
		/// say - in seconds
		/// </summary>
		private const float FellWindow = 30f;

		/// <summary>
		/// How far from where a tree hit something the log that hit it can be, in meters
		/// </summary>
		private const float LogReach = 3f;

		/// <summary>
		/// The least damage the console's kill commands deal, which a real hit never does
		/// </summary>
		private const float ConsoleKillDamage = 1E+9f;

		/// <summary>
		/// How near a leviathan the local player sails to be news, and how far they get from it before coming near it
		/// again is news again, in meters
		/// </summary>
		private const float LeviathanNear = 30f;
		private const float LeviathanFar = 40f;

		/// <summary>
		/// How far a living brother of Zil and Thungr can be for killing the other to not yet be killing both, in meters
		/// </summary>
		private const float BrotherReach = 50f;

		/// <summary>
		/// How often the local player's surroundings are checked for leviathans and dungeons, in seconds
		/// </summary>
		private const float CheckInterval = 1f;

		/// <summary>
		/// The last player to hit each tree this client owns, by player ID, and when in Time.time seconds
		/// </summary>
		private static readonly Dictionary<TreeBase, KeyValuePair<long, float>> treeHitters = new Dictionary<TreeBase, KeyValuePair<long, float>>();

		/// <summary>
		/// The player who brought down the tree or broke the log being taken apart right now, whose logs get them as
		/// they spawn
		/// </summary>
		private static long felling;

		/// <summary>
		/// Every leviathan loaded, the ones the local player is near, and the dungeons they've been reported in this
		/// session
		/// </summary>
		private static readonly List<Leviathan> leviathans = new List<Leviathan>();
		private static readonly HashSet<Leviathan> nearLeviathans = new HashSet<Leviathan>();
		private static readonly HashSet<string> dungeonsReported = new HashSet<string>();

		/// <summary>
		/// When the local player's surroundings are next checked, in Time.time seconds
		/// </summary>
		private static float nextCheck;

		/// <summary>
		/// Whether the game is taming every animal around at once, for the console's tame command
		/// </summary>
		public static bool TamingAll;

		/// <summary>
		/// Checks the local player's surroundings every CheckInterval seconds - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			Player player = Player.m_localPlayer;
			if (player == null || ZRoutedRpc.instance == null || Time.time < nextCheck)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			CheckLeviathans(player);
			CheckDungeon(player);
		}

		/// <summary>
		/// Notes a leviathan as it loads
		/// </summary>
		/// <param name="leviathan">The leviathan</param>
		public static void AddLeviathan(Leviathan leviathan)
		{
			if (leviathan != null && !leviathans.Contains(leviathan))
			{
				leviathans.Add(leviathan);
			}
		}

		/// <summary>
		/// Reports the local player coming within LeviathanNear of a leviathan, once until they're LeviathanFar from it
		/// </summary>
		/// <param name="player">The local player</param>
		private static void CheckLeviathans(Player player)
		{
			leviathans.RemoveAll((Leviathan leviathan) => leviathan == null);
			nearLeviathans.RemoveWhere((Leviathan leviathan) => leviathan == null);
			foreach (Leviathan leviathan in leviathans)
			{
				float distance = Vector3.Distance(player.transform.position, leviathan.transform.position);
				if (distance > LeviathanFar)
				{
					nearLeviathans.Remove(leviathan);
				}
				else if (distance <= LeviathanNear && nearLeviathans.Add(leviathan))
				{
					VikingNewsStore.Report(VikingNews.Leviathan, "", "", Only(player), false);
				}
			}
		}

		/// <summary>
		/// Reports the local player going into a burial chamber, troll cave or frost cave - inside its interior, or for
		/// a troll cave, inside the cave's own weather - once per dungeon each session, which the server only takes as
		/// news the first time
		/// </summary>
		/// <param name="player">The local player</param>
		private static void CheckDungeon(Player player)
		{
			Location location = Location.GetLocation(player.transform.position);
			if (location == null)
			{
				return;
			}
			string dungeon = VikingNewsLines.DungeonKey(Utils.GetPrefabName(location.gameObject));
			if (dungeon.Length == 0 || dungeonsReported.Contains(dungeon))
			{
				return;
			}
			bool inside = ((dungeon == "TrollCave") ? EnvZone.s_triggered != null : player.InInterior());
			if (inside)
			{
				dungeonsReported.Add(dungeon);
				VikingNewsStore.Report(VikingNews.Dungeon, dungeon, "", Only(player), true);
			}
		}

		/// <summary>
		/// Reports what a creature's death is news of, on the client that owns it - a boss, Hildir's miniboss or a sea
		/// serpent killed, by the players who hit it and whoever dropped a tree on it, and any monster killed by a tree
		/// </summary>
		/// <param name="creature">The dying creature, not a player</param>
		public static void OnCreatureDeath(Character creature)
		{
			if (creature == null || creature.m_nview == null || !creature.m_nview.IsValid() || !creature.m_nview.IsOwner())
			{
				return;
			}
			ZDO zdo = creature.m_nview.GetZDO();
			HitData hit = creature.m_lastHit;
			if (zdo.GetInt(ZDOVars.s_cheated) == 2 || (hit != null && !hit.HaveAttacker() && hit.GetTotalDamage() >= ConsoleKillDamage))
			{
				return;
			}
			string prefab = Utils.GetPrefabName(creature.gameObject);
			List<long> players = Attackers(zdo);
			long feller = ((hit != null && hit.m_hitType == HitData.HitType.Tree) ? FellerNear(hit.m_point) : 0L);
			if (feller != 0L)
			{
				if (!players.Contains(feller))
				{
					players.Add(feller);
				}
				if (!creature.IsTamed() && !VikingCore.IsLivingViking(creature))
				{
					VikingNewsStore.Report(VikingNews.Tree, prefab, creature.m_name, new List<long> { feller }, false);
				}
			}
			string boss = VikingNewsLines.BossKey(prefab);
			if (boss.Length > 0 && creature.IsBoss() && !string.IsNullOrEmpty(creature.m_defeatSetGlobalKey))
			{
				VikingNewsStore.Report(VikingNews.Boss, boss, creature.m_name, players, false);
				return;
			}
			string miniboss = VikingNewsLines.MinibossKey(prefab);
			if (miniboss.Length > 0)
			{
				if (miniboss != "ZilThungr" || !BrotherAlive(creature, prefab))
				{
					VikingNewsStore.Report(VikingNews.Miniboss, miniboss, creature.m_name, players, true);
				}
				return;
			}
			if (VikingNewsLines.Serpents.Contains(prefab))
			{
				VikingNewsStore.Report(VikingNews.Serpent, prefab, creature.m_name, players, false);
			}
		}

		/// <summary>
		/// Reports the local player's death - what killed them, or sailing off the edge of the world, which isn't a death
		/// to the vikings
		/// </summary>
		/// <param name="player">The local player, dying</param>
		/// <param name="hit">The hit that killed them, or null</param>
		public static void OnPlayerDeath(Player player, HitData hit)
		{
			if (hit != null && hit.m_hitType == HitData.HitType.EdgeOfWorld)
			{
				VikingNewsStore.Report(VikingNews.Edge, "", "", Only(player), false);
				return;
			}
			DeathCause(player, hit, out string subject, out string detail);
			VikingNewsStore.Report(VikingNews.Death, subject, detail, Only(player), false);
		}

		/// <summary>
		/// Works out what killed a player - a falling tree, the creature or player that hit them, or else the kind of hit
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="hit">The hit that killed them, or null</param>
		/// <param name="subject">Set to the death's subject (see VikingNewsLines.DeathSubject)</param>
		/// <param name="detail">Set to the killer's name as the game writes it, or ""</param>
		private static void DeathCause(Player player, HitData hit, out string subject, out string detail)
		{
			subject = "any";
			detail = "";
			if (hit == null)
			{
				return;
			}
			if (hit.m_hitType != HitData.HitType.Tree && !hit.m_attacker.IsNone() && hit.m_attacker != player.GetZDOID())
			{
				ZDO attacker = ZDOMan.instance.GetZDO(hit.m_attacker);
				GameObject prefab = ((attacker != null && ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(attacker.GetPrefab()) : null);
				if (prefab != null)
				{
					if (prefab.GetComponent<Player>() != null)
					{
						subject = "player";
						detail = attacker.GetString(ZDOVars.s_playerName, "");
						return;
					}
					Character character = prefab.GetComponent<Character>();
					subject = VikingNewsLines.DeathSubject(prefab.name);
					detail = ((character != null) ? character.m_name : prefab.name);
					return;
				}
			}
			subject = "hit/" + hit.m_hitType.ToString();
		}

		/// <summary>
		/// Notes the last player to hit a tree this client owns - or, for a tree knocked by a falling log, the player who
		/// dropped the log - so the log it falls as knows who felled it
		/// </summary>
		/// <param name="tree">The tree</param>
		/// <param name="hit">The hit</param>
		public static void OnTreeHit(TreeBase tree, HitData hit)
		{
			if (tree == null || hit == null || tree.m_nview == null || !tree.m_nview.IsOwner())
			{
				return;
			}
			long player = PlayerOf(hit);
			if (player == 0L && hit.m_hitType == HitData.HitType.Tree)
			{
				player = FellerNear(hit.m_point);
			}
			if (player != 0L)
			{
				treeHitters[tree] = new KeyValuePair<long, float>(player, Time.time);
			}
		}

		/// <summary>
		/// Notes who brought a tree down as it falls, for its log to take
		/// </summary>
		/// <param name="tree">The falling tree</param>
		public static void StartFelling(TreeBase tree)
		{
			felling = 0L;
			if (tree != null && treeHitters.TryGetValue(tree, out KeyValuePair<long, float> hitter))
			{
				treeHitters.Remove(tree);
				if (Time.time - hitter.Value <= FellWindow)
				{
					felling = hitter.Key;
				}
			}
			ForgetStaleHitters();
		}

		/// <summary>
		/// Forgets the trees that have unloaded or were last hit more than FellWindow seconds ago
		/// </summary>
		private static void ForgetStaleHitters()
		{
			List<TreeBase> stale = new List<TreeBase>();
			foreach (KeyValuePair<TreeBase, KeyValuePair<long, float>> hitter in treeHitters)
			{
				if (hitter.Key == null || Time.time - hitter.Value.Value > FellWindow)
				{
					stale.Add(hitter.Key);
				}
			}
			foreach (TreeBase tree in stale)
			{
				treeHitters.Remove(tree);
			}
		}

		/// <summary>
		/// Notes who felled a log as it breaks apart, for its pieces to take
		/// </summary>
		/// <param name="log">The breaking log</param>
		public static void StartBreaking(TreeLog log)
		{
			felling = ((log != null && log.m_nview != null && log.m_nview.IsValid()) ? log.m_nview.GetZDO().GetLong(FellerKey, 0L) : 0L);
		}

		/// <summary>
		/// Forgets who brought down the tree or broke the log once its logs have spawned
		/// </summary>
		public static void EndFelling()
		{
			felling = 0L;
		}

		/// <summary>
		/// Gives a log that just spawned the player who brought down its tree or log
		/// </summary>
		/// <param name="log">The new log</param>
		public static void OnLogSpawned(TreeLog log)
		{
			if (felling != 0L && log != null && log.m_nview != null && log.m_nview.IsValid() && log.m_nview.IsOwner())
			{
				log.m_nview.GetZDO().Set(FellerKey, felling);
			}
		}

		/// <summary>
		/// Gives a log the player who hit it, on its owner, since they're the one who sent it rolling
		/// </summary>
		/// <param name="log">The log</param>
		/// <param name="hit">The hit</param>
		public static void OnLogHit(TreeLog log, HitData hit)
		{
			long player = PlayerOf(hit);
			if (player != 0L && log != null && log.m_nview != null && log.m_nview.IsValid() && log.m_nview.IsOwner())
			{
				log.m_nview.GetZDO().Set(FellerKey, player);
			}
		}

		/// <summary>
		/// Finds the player who dropped the log that hit something, from the logs right where it hit
		/// </summary>
		/// <param name="point">Where it hit</param>
		/// <returns>The player's ID, or 0 when no log there was felled by a player</returns>
		private static long FellerNear(Vector3 point)
		{
			long feller = 0L;
			float nearest = float.MaxValue;
			foreach (Collider collider in Physics.OverlapSphere(point, LogReach))
			{
				TreeLog log = ((collider != null) ? collider.GetComponentInParent<TreeLog>() : null);
				if (log == null || log.m_nview == null || !log.m_nview.IsValid())
				{
					continue;
				}
				long player = log.m_nview.GetZDO().GetLong(FellerKey, 0L);
				float distance = Vector3.Distance(collider.ClosestPoint(point), point);
				if (player != 0L && distance < nearest)
				{
					nearest = distance;
					feller = player;
				}
			}
			return(feller);
		}

		/// <summary>
		/// Gets the player who dealt a hit
		/// </summary>
		/// <param name="hit">The hit</param>
		/// <returns>The player's ID, or 0 when no player dealt it</returns>
		private static long PlayerOf(HitData hit)
		{
			Player player = ((hit != null) ? hit.GetAttacker() as Player : null);
			return((player != null) ? player.GetPlayerID() : 0L);
		}

		/// <summary>
		/// Finds the players loaded on this client who hit a creature, as the game notes them in its ZDO by name
		/// </summary>
		/// <param name="zdo">The creature's ZDO</param>
		/// <returns>Their IDs</returns>
		private static List<long> Attackers(ZDO zdo)
		{
			List<long> players = new List<long>();
			foreach (Player player in Player.GetAllPlayers())
			{
				if (player != null && zdo.GetBool(ZDOVars.s_attackers.ToString() + player.GetPlayerName()))
				{
					players.Add(player.GetPlayerID());
				}
			}
			return(players);
		}

		/// <summary>
		/// Checks whether Zil or Thungr's brother is still alive nearby as the other dies
		/// </summary>
		/// <param name="creature">The brother dying</param>
		/// <param name="prefab">Its prefab</param>
		/// <returns>True while the other is alive</returns>
		private static bool BrotherAlive(Character creature, string prefab)
		{
			foreach (Character other in Character.GetAllCharacters())
			{
				if (other == null || other == creature || other.IsDead() || Vector3.Distance(other.transform.position, creature.transform.position) > BrotherReach)
				{
					continue;
				}
				string otherPrefab = Utils.GetPrefabName(other.gameObject);
				if (otherPrefab != prefab && VikingNewsLines.MinibossKey(otherPrefab) == "ZilThungr")
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Makes the list of players for an event that happened to one player
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>The list, with their ID</returns>
		public static List<long> Only(Player player)
		{
			return(new List<long> { player.GetPlayerID() });
		}
	}
}
