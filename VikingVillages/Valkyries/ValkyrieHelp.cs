using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Has valkyries answer a call for help the way the village's vikings do - when a player or viking is hurt by a
	/// monster, or a badly hurt viking shouts for help (see VillageSignals.OnDistress) - so one comes to a fight beyond the
	/// little she sees and hears herself, without taking up wildlife no one's fighting
	/// </summary>
	internal static class ValkyrieHelp
	{
		/// <summary>
		/// How near a call for help a valkyrie has to be to answer it, in meters
		/// </summary>
		internal const float Reach = 40f;

		/// <summary>
		/// Has every valkyrie this client runs near a call for help take up the attacker - when she's free (see
		/// ValkyrieFlight.Free), has nothing to fight already, and would fight it
		/// </summary>
		/// <param name="where">Where the call came from</param>
		/// <param name="attacker">Who it's for help against</param>
		public static void Answer(Vector3 where, Character attacker)
		{
			if (attacker == null || attacker.IsDead() || attacker.IsPlayer())
			{
				return;
			}
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(where, Reach, near);
			foreach (Character valkyrie in near)
			{
				if (valkyrie.IsDead() || valkyrie.m_nview == null || !valkyrie.m_nview.IsValid() || !valkyrie.m_nview.IsOwner() || !valkyrie.TryGetComponent(out ValkyrieFlight flight) || !flight.Free())
				{
					continue;
				}
				MonsterAI ai = valkyrie.GetBaseAI() as MonsterAI;
				if (ai == null || ai.m_targetCreature != null || !BaseAI.IsEnemy(valkyrie, attacker))
				{
					continue;
				}
				// The game's own taking up of an attacker, as when she's hit, noting where it was for her to go to - and
				// sensed just now, so she isn't given up on it before she's had time to get there
				ai.SetAlerted(true);
				ai.SetTarget(attacker);
				ai.m_timeSinceSensedTargetCreature = 0f;
			}
		}
	}
}
