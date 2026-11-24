using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A dvergr viking's life with the dvergr - using the forsaken powers it knows in a fight and when it's hurt, the
	/// way a living viking does, on its owner
	/// </summary>
	internal class DvergrLife : MonoBehaviour
	{
		/// <summary>
		/// Seconds between the viking's looks at whether to use a power while it's hurt with nothing to fight
		/// </summary>
		private const float PowerCheckInterval = 1f;

		private DvergrVikingBody body;
		private ZNetView nview;

		/// <summary>
		/// When the viking next looks at whether to use a power while it's hurt
		/// </summary>
		private float nextPowerCheck;

		/// <summary>
		/// Finds the viking's parts
		/// </summary>
		private void Awake()
		{
			body = GetComponent<DvergrVikingBody>();
			nview = GetComponent<ZNetView>();
		}

		/// <summary>
		/// Notes which powers the viking is under, and has it use one for the fight it's in or for being hurt, on its
		/// owner while the server has the mod
		/// </summary>
		private void Update()
		{
			if (body == null || body.Powers == null || nview == null || !nview.IsValid() || !nview.IsOwner() || body.IsDead() || !ServerCheck.Active)
			{
				return;
			}
			body.Powers.Update();
			body.Powers.UpdateFight();
			if (Time.time >= nextPowerCheck)
			{
				nextPowerCheck = Time.time + PowerCheckInterval;
				body.Powers.UpdateWork(false, false);
			}
		}
	}
}
