using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Repairing a damaged piece of the village (see RepairJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Repairing a damaged piece of the village - walking to it and swinging at it, which takes no wood
		/// </summary>
		private class RepairJob : Job
		{
			private readonly WearNTear piece;
			private readonly ZDOID pieceId;
			private HammerWork work;

			/// <summary>
			/// The part of the piece it works on, where it walks to reach it, and whether they've been picked
			/// </summary>
			private Vector3 aim;
			private Vector3 stand;
			private bool hasAim;

			/// <summary>
			/// Whether the spot it walks to was found on the navmesh, rather than being the ground under the aim
			/// </summary>
			private bool standOnMesh;

			/// <summary>
			/// When it gives up on the piece if it hasn't got within reach of it
			/// </summary>
			private readonly float giveUpAt;

			/// <summary>
			/// Makes the job, with the piece already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="target">The piece</param>
			/// <param name="id">Its ZDO</param>
			public RepairJob(VillageLife owner, WearNTear target, ZDOID id) : base(owner)
			{
				piece = target;
				pieceId = id;
				work = new HammerWork(owner, RepairSwings);
				giveUpAt = Time.time + RepairGiveUpTime;
			}

			/// <summary>
			/// Walks to the piece, swings at it and repairs it
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's repaired, or gone, or it can't get there</returns>
			public override bool Tick(float dt)
			{
				if (piece == null || piece.GetHealthPercentage() >= 1f)
				{
					return(true);
				}
				// It can keep moving without ever getting within reach, which never counts as stuck, so it gives up in time
				if (!work.Started && Time.time >= giveUpAt)
				{
					life.LogHammerGiveUp("Repair", piece, false, hasAim, aim, stand, standOnMesh);
					life.ai.StopMoving();
					life.AvoidHammerWork(piece.m_nview);
					return(true);
				}
				// A long piece like a fence is worked on from wherever along it is nearest where the viking started, picked
				// once so the way there doesn't change as it walks - a point up on a roof would have its way to the door
				// flip between inside and out - and walked to on the ground under it
				if (!hasAim)
				{
					aim = NearestPoint(piece, life.transform.position);
					Vector3 ground = aim;
					ground.y = ZoneSystem.instance.GetGroundHeight(aim);
					standOnMesh = NavmeshPoints.FindValidPoint(out Vector3 valid, ground, WorkReach, Pathfinding.AgentType.Humanoid);
					stand = (standOnMesh ? valid : ground);
					hasAim = true;
				}
				if (!work.Tick(dt, aim, stand))
				{
					if (life.moveFailed)
					{
						// It can't get to this piece, so it leaves it alone for a while rather than trying it again
						life.LogHammerGiveUp("Repair", piece, true, hasAim, aim, stand, standOnMesh);
						life.AvoidHammerWork(piece.m_nview);
						return(true);
					}
					return(false);
				}
				if (piece.Repair())
				{
					VillageSignals.SendEffect(VillageSignals.RepairEffect, pieceId);
					TestLog.Count("Work done", "pieces repaired");
				}
				return(true);
			}

			/// <summary>
			/// Lets go of the piece
			/// </summary>
			public override void End()
			{
				life.ReleaseWork(pieceId);
			}
		}
	}
}
