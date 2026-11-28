using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// How a valkyrie's feather volley flies - each feather straight and at one speed, neither slowing nor falling, aimed
	/// where her target will be by the time it gets there, within a small spread, and hitting what passes close by
	/// rather than only what it touches dead on
	/// </summary>
	/// <remarks>
	/// The Fallen Valkyrie's feathers fly at 25 to 60 meters a second, slow down fast, fall and only hit what their
	/// path runs right through, so most of them miss anything that isn't standing still near it
	/// </remarks>
	internal static class ValkyrieVolley
	{
		/// <summary>
		/// How fast her feathers fly, in meters a second, how far each strays from her aim at most, in degrees, and how
		/// near something has to pass to be hit, in meters
		/// </summary>
		internal const float Speed = 45f;
		internal const float Spread = 1.5f;
		internal const float HitRadius = 0.3f;

		/// <summary>
		/// How long a feather stays stuck where it hits the ground or anything else that doesn't move, in seconds - the
		/// Fallen Valkyrie's stay 2
		/// </summary>
		internal const float StayTime = 10f;

		/// <summary>
		/// Works out which way to shoot something flying straight at a speed to hit a target moving straight at its
		/// own - where their paths meet first, or straight at the target when it can't be caught
		/// </summary>
		/// <param name="from">Where it's shot from</param>
		/// <param name="target">Where the target is</param>
		/// <param name="targetVelocity">How the target is moving, in meters a second</param>
		/// <param name="speed">How fast it flies, in meters a second</param>
		/// <returns>The way to shoot it, of length 1</returns>
		internal static Vector3 Lead(Vector3 from, Vector3 target, Vector3 targetVelocity, float speed)
		{
			Vector3 offset = target - from;
			if (offset.sqrMagnitude < 0.0001f)
			{
				return(Vector3.forward);
			}
			// |offset + velocity * t| = speed * t, solved for the soonest t above 0
			float a = targetVelocity.sqrMagnitude - speed * speed;
			float b = 2f * Vector3.Dot(offset, targetVelocity);
			float c = offset.sqrMagnitude;
			float time = -1f;
			if (Mathf.Abs(a) < 0.0001f)
			{
				time = ((Mathf.Abs(b) > 0.0001f) ? -c / b : -1f);
			}
			else
			{
				float discriminant = b * b - 4f * a * c;
				if (discriminant >= 0f)
				{
					float root = Mathf.Sqrt(discriminant);
					float first = (-b - root) / (2f * a);
					float second = (-b + root) / (2f * a);
					float soonest = Mathf.Min(first, second);
					time = ((soonest > 0f) ? soonest : Mathf.Max(first, second));
				}
			}
			if (time <= 0f)
			{
				return(offset.normalized);
			}
			return((offset + targetVelocity * time).normalized);
		}

		/// <summary>
		/// Sets one of her feathers flying as it's launched - straight, at Speed, led at her target when she has one,
		/// within Spread of it, and staying StayTime seconds where it sticks
		/// </summary>
		/// <param name="projectile">The feather</param>
		/// <param name="owner">Who launched it</param>
		/// <param name="velocity">The velocity the game gave it, changed to hers</param>
		public static void Launch(Projectile projectile, Character owner, ref Vector3 velocity)
		{
			projectile.m_gravity = 0f;
			projectile.m_drag = 0f;
			projectile.m_rayRadius = HitRadius;
			projectile.m_stayTTL = StayTime;
			Vector3 direction = ((velocity.sqrMagnitude > 0.0001f) ? velocity.normalized : owner.transform.forward);
			BaseAI ai = owner.GetBaseAI();
			Character target = ((ai != null) ? ai.GetTargetCreature() : null);
			if (target != null && !target.IsDead())
			{
				direction = Lead(projectile.transform.position, target.GetCenterPoint(), target.GetVelocity(), Speed);
			}
			Vector2 stray = Random.insideUnitCircle * Spread;
			Vector3 side = Vector3.Cross(direction, Vector3.up);
			if (side.sqrMagnitude < 0.0001f)
			{
				side = Vector3.right;
			}
			direction = Quaternion.AngleAxis(stray.x, Vector3.up) * Quaternion.AngleAxis(stray.y, side.normalized) * direction;
			velocity = direction * Speed;
			projectile.transform.rotation = Quaternion.LookRotation(direction);
		}
	}
}
