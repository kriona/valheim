using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeps a friendly valkyrie from spinning where her spin would hit buildings - its chop and pickaxe damage tear
	/// down a village's houses and walls, or a player's. Angry, she spins wherever she likes
	/// </summary>
	/// <remarks>
	/// Her AI passes over her spin for another attack while it's held back (see ValkyrieSpinGuardPatch), and her rider's
	/// secondary attack doesn't spin her (see ValkyrieCombat.RiderAttack)
	/// </remarks>
	internal static class ValkyrieSpinGuard
	{
		/// <summary>
		/// How much farther than her spin reaches buildings are kept clear of it, in meters, and how often she looks
		/// again, in seconds
		/// </summary>
		private const float Margin = 1f;
		private const float CheckInterval = 0.5f;

		/// <summary>
		/// What each valkyrie last found, and when she looks again
		/// </summary>
		private static readonly Dictionary<Character, KeyValuePair<float, bool>> found = new Dictionary<Character, KeyValuePair<float, bool>>();

		/// <summary>
		/// The layers buildings are on
		/// </summary>
		private static int pieceMask;

		/// <summary>
		/// Checks whether she's friendly - befriended, or not angry with players
		/// </summary>
		/// <param name="valkyrie">The valkyrie</param>
		/// <returns>True when she is</returns>
		public static bool Friendly(Character valkyrie)
		{
			BaseAI ai = valkyrie.GetBaseAI();
			return(valkyrie.IsTamed() || ai == null || !ai.IsAggravated());
		}

		/// <summary>
		/// Checks whether her spin is held back - she's friendly and buildings are within its reach
		/// </summary>
		/// <param name="valkyrie">The valkyrie</param>
		/// <param name="spin">Her spin's item</param>
		/// <returns>True when she mustn't spin</returns>
		public static bool Holds(Character valkyrie, ItemDrop.ItemData spin)
		{
			if (valkyrie == null || spin == null || !Friendly(valkyrie))
			{
				return(false);
			}
			float now = Time.time;
			if (found.TryGetValue(valkyrie, out KeyValuePair<float, bool> last) && now < last.Key)
			{
				return(last.Value);
			}
			if (found.Count > 64)
			{
				found.Clear();
			}
			bool near = BuildingsNear(valkyrie, spin.m_shared.m_attack);
			found[valkyrie] = new KeyValuePair<float, bool>(now + CheckInterval, near);
			return(near);
		}

		/// <summary>
		/// Checks whether any building is within reach of a spin, where the game looks for what an area attack hits (see
		/// Attack.DoAreaAttack), and Margin more
		/// </summary>
		/// <param name="valkyrie">The valkyrie</param>
		/// <param name="attack">Her spin's attack</param>
		/// <returns>True when one is</returns>
		private static bool BuildingsNear(Character valkyrie, Attack attack)
		{
			if (pieceMask == 0)
			{
				pieceMask = LayerMask.GetMask("piece", "piece_nonsolid");
			}
			Transform body = valkyrie.transform;
			Vector3 center = body.position + Vector3.up * attack.m_attackHeight + body.forward * attack.m_attackRange + body.right * attack.m_attackOffset;
			float radius = attack.m_attackRayWidth + attack.m_attackRayWidthCharExtra + Margin;
			foreach (Collider collider in Physics.OverlapSphere(center, radius, pieceMask, QueryTriggerInteraction.Ignore))
			{
				if (collider.GetComponentInParent<WearNTear>() != null)
				{
					return(true);
				}
			}
			return(false);
		}
	}
}
