using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A creature or player a valkyrie grabbed, on the client that owns it - held hanging from her talon until she lets
	/// go, then dropped, its fall hurting it when it lands as her attack if it's a creature that doesn't take fall damage
	/// of its own (see ValkyrieGrab). A rider she caught is held the same way while she flies down, and let go of
	/// safely near the ground, without the fall from where she caught them counting (see ValkyrieCatch)
	/// </summary>
	internal class ValkyrieCarried : MonoBehaviour
	{
		/// <summary>
		/// The longest it's held before it falls on its own, in seconds, in case the valkyrie never lets go
		/// </summary>
		private const float HoldLimit = 4f;

		/// <summary>
		/// How long after it's dropped landing counts, and the longest it's watched falling, in seconds
		/// </summary>
		private const float LandAfter = 0.2f;
		private const float FallLimit = 15f;

		private Character character;
		private Rigidbody body;
		private ZDOID valkyrie;
		private float grabbedAt;
		private float droppedAt = -1f;
		private float highest;
		private float hang;

		/// <summary>
		/// Whether it's a rider she caught rather than an enemy she grabbed, the valkyrie who caught them, and the longest
		/// she carries them, in seconds (see ValkyrieCatch.CatchLimit)
		/// </summary>
		private bool caught;
		private GameObject holder;
		private float catchLimit;

		/// <summary>
		/// Has the valkyrie grab a creature or player on the client that owns it, unless it's already held, dead, or a
		/// player mid-roll - then tells her it's held
		/// </summary>
		/// <param name="target">The creature or player</param>
		/// <param name="by">The valkyrie</param>
		public static void Grab(Character target, ZDOID by)
		{
			if (!target.m_nview.IsOwner() || target.IsDead() || target.InDodge() || target.GetComponent<ValkyrieCarried>() != null)
			{
				return;
			}
			GameObject valkyrie = ZNetScene.instance.FindInstance(by);
			if (valkyrie == null || !valkyrie.TryGetComponent(out ZNetView valkyrieView) || !valkyrieView.IsValid())
			{
				return;
			}
			ValkyrieCarried carried = target.gameObject.AddComponent<ValkyrieCarried>();
			carried.valkyrie = by;
			carried.grabbedAt = Time.time;
			carried.highest = target.transform.position.y;
			CapsuleCollider capsule = target.GetComponent<CapsuleCollider>();
			carried.hang = ((capsule != null) ? capsule.height * target.transform.lossyScale.y : 1.8f);
			valkyrieView.InvokeRPC(ZNetView.Everybody, ValkyrieGrab.GrabbedRPC, target.GetZDOID());
		}

		/// <summary>
		/// Has the valkyrie catch her rider on the client that owns them, unless they're already held - then tells her
		/// they're caught, so she flies them down (see ValkyrieCatch)
		/// </summary>
		/// <param name="rider">The rider</param>
		/// <param name="by">The valkyrie</param>
		/// <param name="target">Where she carries them to - the land she flies them to over water, or where they are</param>
		/// <returns>True when she caught them</returns>
		public static bool Catch(Player rider, ZDOID by, Vector3 target)
		{
			if (!rider.m_nview.IsOwner() || rider.GetComponent<ValkyrieCarried>() != null)
			{
				return(false);
			}
			GameObject valkyrie = ZNetScene.instance.FindInstance(by);
			if (valkyrie == null || !valkyrie.TryGetComponent(out ZNetView valkyrieView) || !valkyrieView.IsValid())
			{
				return(false);
			}
			ValkyrieCarried carried = rider.gameObject.AddComponent<ValkyrieCarried>();
			carried.valkyrie = by;
			carried.grabbedAt = Time.time;
			carried.highest = rider.transform.position.y;
			carried.caught = true;
			carried.holder = valkyrie;
			carried.catchLimit = ValkyrieCatch.CatchLimitFor(rider.transform.position, target);
			CapsuleCollider capsule = rider.GetComponent<CapsuleCollider>();
			carried.hang = ((capsule != null) ? capsule.height * rider.transform.lossyScale.y : 1.8f);
			ValkyrieCatch.IgnoreCollisions(valkyrie, rider.gameObject, true);
			valkyrieView.InvokeRPC(ZNetView.Everybody, ValkyrieCatch.CaughtRPC, rider.GetZDOID(), target);
			return(true);
		}

		/// <summary>
		/// Has a rider she caught touch her by her usual rules again once she's let go of them - passing through her
		/// while she's friendly with them (see ValkyriePassThrough)
		/// </summary>
		private void OnDestroy()
		{
			if (caught)
			{
				ValkyriePassThrough.Settle(holder, gameObject);
			}
		}

		/// <summary>
		/// Lets go of it, when it's held
		/// </summary>
		/// <param name="target">The creature or player</param>
		public static void Drop(Character target)
		{
			if (target.TryGetComponent(out ValkyrieCarried carried))
			{
				carried.LetGo();
			}
		}

		/// <summary>
		/// Finds its body
		/// </summary>
		private void Awake()
		{
			character = GetComponent<Character>();
			body = GetComponent<Rigidbody>();
		}

		/// <summary>
		/// Holds it at her talon while it's held, and watches it fall once it's dropped
		/// </summary>
		private void FixedUpdate()
		{
			if (character == null || character.m_nview == null || !character.m_nview.IsValid() || !character.m_nview.IsOwner() || character.IsDead())
			{
				Destroy(this);
				return;
			}
			if (droppedAt < 0f)
			{
				Hold();
				return;
			}
			Fall();
		}

		/// <summary>
		/// Holds it at her talon again after everything's moved, so it's seen there
		/// </summary>
		private void LateUpdate()
		{
			if (droppedAt < 0f)
			{
				Hold();
			}
		}

		/// <summary>
		/// Hangs it from her talon, still - or lets it fall when she's gone or has held it too long, and a rider she
		/// caught once she's carried them near enough the ground (see ValkyrieCatch.LetsGo)
		/// </summary>
		private void Hold()
		{
			GameObject holder = ZNetScene.instance.FindInstance(valkyrie);
			if (holder == null || !holder.TryGetComponent(out ValkyrieGrab grab) || Time.time - grabbedAt > (caught ? catchLimit : HoldLimit))
			{
				LetGo();
				return;
			}
			Vector3 position = grab.Talon.position + Vector3.down * hang;
			transform.position = position;
			if (body != null)
			{
				body.position = position;
				body.linearVelocity = Vector3.zero;
			}
			highest = Mathf.Max(highest, position.y);
			if (caught)
			{
				// The game hurts a player by how far they fell from the highest they were off the ground
				character.m_maxAirAltitude = position.y;
				if (ValkyrieCatch.GroundBelow(position, out float ground) && ValkyrieCatch.LetsGo(position.y, ground, ValkyrieCatch.WaterLevel()))
				{
					LetGo();
				}
			}
		}

		/// <summary>
		/// Drops it, noting when in its ZDO so no valkyrie grabs it again for a while - a player falls the game's way,
		/// taking the game's fall damage, so is let be from here. A rider she caught falls only from where she lets go,
		/// and she's told she has, to stop flying down
		/// </summary>
		private void LetGo()
		{
			if (droppedAt >= 0f)
			{
				return;
			}
			droppedAt = Time.time;
			if (caught)
			{
				character.m_maxAirAltitude = transform.position.y;
				GameObject holder = ZNetScene.instance.FindInstance(valkyrie);
				if (holder != null && holder.TryGetComponent(out ZNetView holderView) && holderView.IsValid())
				{
					holderView.InvokeRPC(ZNetView.Everybody, ValkyrieCatch.ReleasedRPC);
				}
				Destroy(this);
				return;
			}
			if (character.m_nview.IsValid())
			{
				character.m_nview.GetZDO().Set(ValkyrieGrab.DroppedKey, ZNet.instance.GetTime().Ticks);
			}
			if (character.IsPlayer())
			{
				Destroy(this);
			}
		}

		/// <summary>
		/// Hurts it by how far it fell once it lands, as the valkyrie's attack, the way a fall hurts a player - nothing
		/// when it lands in water or never seems to land
		/// </summary>
		private void Fall()
		{
			highest = Mathf.Max(highest, transform.position.y);
			if (Time.time - droppedAt > FallLimit || character.InLiquid())
			{
				Destroy(this);
				return;
			}
			if (Time.time - droppedAt < LandAfter || !character.IsOnGround())
			{
				return;
			}
			float damage = ValkyrieGrab.FallDamage(highest - transform.position.y);
			if (damage > 0f)
			{
				HitData hit = new HitData();
				hit.m_damage.m_damage = damage;
				hit.m_point = transform.position;
				hit.m_dir = Vector3.down;
				hit.m_hitType = HitData.HitType.Fall;
				hit.m_attacker = valkyrie;
				character.Damage(hit);
			}
			Destroy(this);
		}
	}
}
