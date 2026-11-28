using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Lets players she's friendly with pass through a valkyrie rather than be shoved by her - her body is big, and
	/// bumping into her, or getting off her right beside it, could push someone off a ledge. A player she's angry with
	/// she collides with like any monster
	/// </summary>
	/// <remarks>
	/// Only bodies touching are changed - what her attacks hit is found another way, so passing through her doesn't stop
	/// her striking or being struck. Each client sets it for itself, for the players near her there, every
	/// CheckInterval seconds
	/// </remarks>
	internal class ValkyriePassThrough : MonoBehaviour
	{
		/// <summary>
		/// How often she looks at the players near her again, in seconds, and how near they have to be, in meters
		/// </summary>
		private const float CheckInterval = 1f;
		private const float Range = 30f;

		private Character character;
		private float nextCheck;

		/// <summary>
		/// The players near her, and whether each passes through her now
		/// </summary>
		private readonly Dictionary<Player, bool> passing = new Dictionary<Player, bool>();

		/// <summary>
		/// Finds her body
		/// </summary>
		private void Awake()
		{
			character = GetComponent<Character>();
		}

		/// <summary>
		/// Lets the players near her she's friendly with pass through her, and has those she's angry with collide with her
		/// </summary>
		private void Update()
		{
			if (Time.time < nextCheck || character == null)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			foreach (Player player in Player.GetAllPlayers())
			{
				if (player != null && Vector3.Distance(player.transform.position, transform.position) < Range)
				{
					Apply(player, Wanted(player), false);
				}
			}
			List<Player> gone = new List<Player>();
			foreach (Player player in passing.Keys)
			{
				if (player == null)
				{
					gone.Add(player);
				}
			}
			foreach (Player player in gone)
			{
				passing.Remove(player);
			}
		}

		/// <summary>
		/// Checks whether a player should pass through her - she's alive and not angry with them
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>True when they should</returns>
		private bool Wanted(Player player)
		{
			return(!character.IsDead() && !BaseAI.IsEnemy(character, player));
		}

		/// <summary>
		/// Has a player pass through her or collide with her, unless they already do
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="pass">Whether they pass through her</param>
		/// <param name="force">Whether to set it even when it's noted as set already</param>
		private void Apply(Player player, bool pass, bool force)
		{
			if (!force && passing.TryGetValue(player, out bool now) && now == pass)
			{
				return;
			}
			ValkyrieCatch.IgnoreCollisions(gameObject, player.gameObject, pass);
			passing[player] = pass;
		}

		/// <summary>
		/// Sets whether something passes through a valkyrie by her rules again, once something else - like her carrying a
		/// rider she caught - has changed it: a player passes through her while she's friendly with them, and anything
		/// else collides with her
		/// </summary>
		/// <param name="valkyrie">The valkyrie</param>
		/// <param name="other">What touched her</param>
		public static void Settle(GameObject valkyrie, GameObject other)
		{
			if (valkyrie == null || other == null)
			{
				return;
			}
			if (valkyrie.TryGetComponent(out ValkyriePassThrough passThrough) && other.TryGetComponent(out Player player) && passThrough.character != null)
			{
				passThrough.Apply(player, passThrough.Wanted(player), true);
				return;
			}
			ValkyrieCatch.IgnoreCollisions(valkyrie, other, false);
		}
	}
}
