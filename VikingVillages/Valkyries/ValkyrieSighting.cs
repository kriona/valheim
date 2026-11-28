using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Has the vikings below a flying valkyrie shout their surprise as she passes over them - one at a time, each at most
	/// every ViewerGap seconds, so a village she flies over calls out to her one after another rather than all at once
	/// (see ValkyrieSightSpeech)
	/// </summary>
	/// <remarks>
	/// Runs on the client that owns her, which says the line for every client nearby. Her own traveler, and vikings
	/// asleep, fighting or at odds with her, say nothing
	/// </remarks>
	internal class ValkyrieSighting : MonoBehaviour
	{
		/// <summary>
		/// Seconds between her looks for vikings below her
		/// </summary>
		private const float CheckInterval = 0.5f;

		/// <summary>
		/// The least time between two vikings shouting at her, in seconds
		/// </summary>
		private const float ShoutGap = 5f;

		/// <summary>
		/// The least time between two shouts from the same viking, at any valkyrie, in seconds
		/// </summary>
		private const float ViewerGap = 120f;

		/// <summary>
		/// How high above a viking she has to be for them to shout at her, and how far out to the side, in meters
		/// </summary>
		internal const float MinHeight = 3f;
		internal const float MaxHeight = 40f;
		internal const float Radius = 15f;

		/// <summary>
		/// When each viking may next shout at a valkyrie, by their ZDO
		/// </summary>
		private static readonly Dictionary<ZDOID, float> nextShout = new Dictionary<ZDOID, float>();

		private Character character;
		private ZNetView nview;
		private Tameable tameable;
		private float nextCheck;
		private float nextAny;

		/// <summary>
		/// Finds her parts
		/// </summary>
		private void Awake()
		{
			character = GetComponent<Character>();
			nview = GetComponent<ZNetView>();
			tameable = GetComponent<Tameable>();
		}

		/// <summary>
		/// Checks whether a viking would see a valkyrie as flying over them - high enough above them, and near enough
		/// overhead
		/// </summary>
		/// <param name="valkyrie">Where she is</param>
		/// <param name="viking">Where the viking is</param>
		/// <returns>True when she's over them</returns>
		internal static bool Overhead(Vector3 valkyrie, Vector3 viking)
		{
			float height = valkyrie.y - viking.y;
			if (height < MinHeight || height > MaxHeight)
			{
				return(false);
			}
			Vector2 across = new Vector2(valkyrie.x - viking.x, valkyrie.z - viking.z);
			return(across.magnitude <= Radius);
		}

		/// <summary>
		/// While she flies, has one viking below her who hasn't shouted lately shout at her
		/// </summary>
		private void Update()
		{
			float now = Time.time;
			if (now < nextCheck || now < nextAny || character == null || nview == null || !nview.IsValid() || !nview.IsOwner() || !character.IsFlying() || character.IsDead() || !ServerCheck.Active)
			{
				return;
			}
			nextCheck = now + CheckInterval;
			Character viking = Viewer(now);
			if (viking == null)
			{
				return;
			}
			if (nextShout.Count > 256)
			{
				nextShout.Clear();
			}
			nextShout[viking.GetZDOID()] = now + ViewerGap;
			nextAny = now + ShoutGap;
			bool ridden = Rider(out Player player);
			string rider = ((player != null && VikingSpeech.Knows(viking, player.GetPlayerID())) ? player.GetPlayerName() : "");
			string line = ValkyrieSightSpeech.Shout(VikingCore.IsDvergrViking(viking), ridden, rider, Random.value);
			VikingSpeech.Say(viking, line, VikingSpeech.CallTime, VikingSpeech.CallCull);
		}

		/// <summary>
		/// Finds a living viking below her who'd shout at her - awake, not fighting, not at odds with her, not her own
		/// traveler, and not shouted lately
		/// </summary>
		/// <param name="now">The time now</param>
		/// <returns>The viking, or null when there's none</returns>
		private Character Viewer(float now)
		{
			Vector3 at = transform.position;
			ZDOID owner = nview.GetZDO().GetZDOID(TravelerPets.OwnerKey);
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(at, MaxHeight + Radius, near);
			foreach (Character other in near)
			{
				if (other == character || other.IsDead() || !VikingCore.IsLivingViking(other) || !Overhead(at, other.transform.position))
				{
					continue;
				}
				ZDOID id = other.GetZDOID();
				if (id == owner || (nextShout.TryGetValue(id, out float until) && now < until))
				{
					continue;
				}
				BaseAI ai = other.GetBaseAI();
				if (VikingSpeech.Asleep(other) || (ai != null && ai.GetTargetCreature() != null) || BaseAI.IsEnemy(other, character))
				{
					continue;
				}
				return(other);
			}
			return(null);
		}

		/// <summary>
		/// Finds the player riding her - the saddle holds the user ID of the client they play on, which their character's
		/// ZDO ID carries
		/// </summary>
		/// <param name="player">The player, or null when no one rides her or they aren't loaded here</param>
		/// <returns>True when someone rides her</returns>
		private bool Rider(out Player player)
		{
			player = null;
			Sadle saddle = ((tameable != null) ? tameable.m_saddle : null);
			if (saddle == null || !saddle.isActiveAndEnabled || !saddle.HaveValidUser())
			{
				return(false);
			}
			long user = saddle.GetUser();
			foreach (Player each in Player.GetAllPlayers())
			{
				if (each.GetZDOID().UserID == user)
				{
					player = each;
				}
			}
			return(true);
		}
	}
}
