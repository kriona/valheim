using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SafeSneak
{
	internal static class Ledge
	{
		/// <summary>
		/// How far short of a ledge the player's center stops, in meters - close enough to peer over, while the bottom of
		/// the player's capsule still rests on flat ground rather than rolling off the corner
		/// </summary>
		private const float EdgeMargin = 0.1f;

		/// <summary>
		/// How far above the feet each ground probe starts, so ground that rises ahead is still found
		/// </summary>
		private const float ProbeHeight = 1f;

		/// <summary>
		/// Number of directions sampled around the player to work out which way a ledge faces
		/// </summary>
		private const int RingSamples = 16;

		/// <summary>
		/// Speeds below this, in meters per second, count as standing still
		/// </summary>
		private const float MinSpeed = 0.001f;

		/// <summary>
		/// Applies the walking velocity change, first taking out any part of it that would carry a sneaking local player
		/// over a ledge
		/// </summary>
		/// <remarks>
		/// Replaces the Rigidbody.AddForce call in Character.UpdateWalking, where the force is the change from the body's
		/// velocity to the velocity the walk wants, so the body's velocity plus the force is where it ends up
		/// </remarks>
		/// <param name="body">The character's rigidbody</param>
		/// <param name="force">The velocity change the walk applies</param>
		/// <param name="mode">How the force is applied</param>
		/// <param name="character">The walking character</param>
		public static void AddWalkForce(Rigidbody body, Vector3 force, ForceMode mode, Character character)
		{
			if (mode == ForceMode.VelocityChange && IsGuarded(character))
			{
				Vector3 target = body.linearVelocity + force;
				force += Hold(character, target) - target;
			}
			body.AddForce(force, mode);
		}

		/// <summary>
		/// Whether the character is the local player sneaking on the ground, and not dodging or being knocked back or
		/// staggered
		/// </summary>
		/// <param name="character">The walking character</param>
		/// <returns>True when ledges should stop the character</returns>
		private static bool IsGuarded(Character character)
		{
			return(character == Player.m_localPlayer && character.IsCrouching() && character.IsOnGround() && !character.IsKnockedBack() && !character.IsStaggering() && !character.InDodge() && !character.IsAttached() && !character.IsDead());
		}

		/// <summary>
		/// Takes the part of the target velocity that leads over a ledge out of it, letting the player slide along the
		/// edge, and takes the same part out of the character's walking velocity so the walk animation stops with them
		/// </summary>
		/// <remarks>
		/// Movement is measured against whatever the player is standing on, so the deck of a moving ship counts as still
		/// </remarks>
		/// <param name="character">The walking character</param>
		/// <param name="target">The velocity the walk wants</param>
		/// <returns>The target velocity without the part that leads over a ledge</returns>
		private static Vector3 Hold(Character character, Vector3 target)
		{
			Vector3 position = character.transform.position;
			Vector3 groundVelocity = GetGroundVelocity(character, position);
			Vector3 move = target - groundVelocity;
			move.y = 0f;
			if (move.magnitude < MinSpeed || !HasGround(position, position.y))
			{
				return(target);
			}
			if (IsSafe(position, move))
			{
				return(target);
			}
			Vector3 normal = GetLedgeNormal(position, move);
			Vector3 slide = Without(move, normal);
			if (slide.magnitude < MinSpeed || !IsSafe(position, slide))
			{
				character.m_currentVel = Vector3.zero;
				return(new Vector3(groundVelocity.x, target.y, groundVelocity.z));
			}
			character.m_currentVel = Without(character.m_currentVel, normal);
			return(new Vector3(groundVelocity.x + slide.x, target.y, groundVelocity.z + slide.z));
		}

		/// <summary>
		/// Gets the horizontal velocity of whatever the character is standing on at its feet
		/// </summary>
		/// <param name="character">The walking character</param>
		/// <param name="position">The character's feet</param>
		/// <returns>The ground's velocity, or zero when the ground has no rigidbody</returns>
		private static Vector3 GetGroundVelocity(Character character, Vector3 position)
		{
			Rigidbody ground = character.m_lastGroundBody;
			if (ground == null)
			{
				return(Vector3.zero);
			}
			Vector3 velocity = ground.GetPointVelocity(position);
			velocity.y = 0f;
			return(velocity);
		}

		/// <summary>
		/// Whether there's still ground under the player after a physics step of the move, with the edge margin added
		/// </summary>
		/// <param name="position">The player's feet</param>
		/// <param name="move">The horizontal move, in meters per second</param>
		/// <returns>True when the move doesn't lead over a ledge</returns>
		private static bool IsSafe(Vector3 position, Vector3 move)
		{
			float speed = move.magnitude;
			return(HasGround(position + move / speed * GetProbeDistance(speed), position.y));
		}

		/// <summary>
		/// Gets how far ahead of the player to look for ground
		/// </summary>
		/// <param name="speed">The player's horizontal speed, in meters per second</param>
		/// <returns>The distance the player moves in a physics step plus the edge margin</returns>
		private static float GetProbeDistance(float speed)
		{
			return(speed * Time.fixedDeltaTime + EdgeMargin);
		}

		/// <summary>
		/// Works out which way the ledge in front of the player faces by sampling the ground in a ring around them
		/// </summary>
		/// <param name="position">The player's feet</param>
		/// <param name="move">The horizontal move that leads over the ledge</param>
		/// <returns>A horizontal unit vector pointing out over the ledge</returns>
		private static Vector3 GetLedgeNormal(Vector3 position, Vector3 move)
		{
			float distance = GetProbeDistance(move.magnitude);
			Vector3 sum = Vector3.zero;
			for (int i = 0; i < RingSamples; i++)
			{
				float angle = i * Mathf.PI * 2f / RingSamples;
				Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				if (!HasGround(position + direction * distance, position.y))
				{
					sum += direction;
				}
			}
			return((sum.magnitude > MinSpeed) ? sum.normalized : move.normalized);
		}

		/// <summary>
		/// Takes out the part of a velocity that heads along the normal, leaving any part heading away from it
		/// </summary>
		/// <param name="velocity">The velocity to change</param>
		/// <param name="normal">A horizontal unit vector pointing out over a ledge</param>
		/// <returns>The velocity without its part toward the ledge</returns>
		private static Vector3 Without(Vector3 velocity, Vector3 normal)
		{
			return(velocity - normal * Mathf.Max(0f, Vector3.Dot(velocity, normal)));
		}

		/// <summary>
		/// Whether there's ground below a point within the allowed drop from the player's feet
		/// </summary>
		/// <param name="point">The point to probe, only its horizontal position is used</param>
		/// <param name="feet">The height of the player's feet</param>
		/// <returns>True when ground was found</returns>
		private static bool HasGround(Vector3 point, float feet)
		{
			Vector3 start = new Vector3(point.x, feet + ProbeHeight, point.z);
			return(Physics.Raycast(start, Vector3.down, ProbeHeight + Plugin.MaxDrop.Value, Character.s_groundRayMask, QueryTriggerInteraction.Ignore));
		}
	}

	/// <summary>
	/// Stops a sneaking local player at ledges by routing the walking velocity change through Ledge.AddWalkForce
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.UpdateWalking))]
	public static class UpdateWalkingPatch
	{
		/// <summary>
		/// Replaces the Rigidbody.AddForce call with Ledge.AddWalkForce, passing the character as an extra argument
		/// </summary>
		/// <param name="instructions">The method's instructions</param>
		/// <returns>The patched instructions</returns>
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo addForce = AccessTools.Method(typeof(Rigidbody), nameof(Rigidbody.AddForce), new[] { typeof(Vector3), typeof(ForceMode) });
			MethodInfo addWalkForce = AccessTools.Method(typeof(Ledge), nameof(Ledge.AddWalkForce));
			int replaced = 0;
			foreach (CodeInstruction instruction in instructions)
			{
				if (instruction.Calls(addForce))
				{
					CodeInstruction loadCharacter = new CodeInstruction(OpCodes.Ldarg_0);
					instruction.MoveLabelsTo(loadCharacter);
					yield return(loadCharacter);
					instruction.opcode = OpCodes.Call;
					instruction.operand = addWalkForce;
					replaced++;
				}
				yield return(instruction);
			}
			if (replaced != 1)
			{
				Plugin.Log.LogWarning("Expected one Rigidbody.AddForce call in Character.UpdateWalking but found " + replaced + " - ledges may not stop you");
			}
		}
	}
}
