using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Petting one of the village's boars (see PetJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Finds one of the village's boars to pet - in a pen or out of one, since petting is one of the few things a
		/// viking goes into a pen for - or an animal a traveler brought, or left to the village, in it or waiting at its
		/// edge (see TravelerPets.PettableIn)
		/// </summary>
		/// <returns>The boar, or null</returns>
		private Character FindBoar()
		{
			List<Character> boars = new List<Character>();
			foreach (Character character in Character.GetAllCharacters())
			{
				bool livestock = (character != null && !character.IsDead() && character.IsTamed() && FarmSpawns.IsLivestock(character) && village.Contains(character.transform.position));
				if (!livestock && !TravelerPets.PettableIn(character, village))
				{
					continue;
				}
				boars.Add(character);
			}
			return((boars.Count > 0) ? boars[Random.Range(0, boars.Count)] : null);
		}

		/// <summary>
		/// Walking up to one of the village's boars and petting it, one viking of the village at a time
		/// </summary>
		private class PetJob : Job
		{
			private readonly Character boar;
			private float pettedAt = -1f;
			private float nextRenew;

			/// <summary>
			/// Finds the boar it's petting
			/// </summary>
			/// <returns>The boar</returns>
			public override Character Focus()
			{
				return(boar);
			}

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="animal">The boar</param>
			public PetJob(VillageLife owner, Character animal) : base(owner)
			{
				boar = animal;
			}

			/// <summary>
			/// Notes that the viking is petting a boar, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(PettingKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Walks to the boar, reaches out to pet it and shows the hearts, then stands a moment
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's done, or the boar has gone</returns>
			public override bool Tick(float dt)
			{
				if (boar == null || boar.IsDead())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					life.SetDoing(PettingKey, true);
					nextRenew = Time.time + ClaimRenew;
				}
				if (pettedAt >= 0f)
				{
					life.Face(boar.transform.position);
					return(Time.time - pettedAt >= 2.5f);
				}
				if (Utils.DistanceXZ(boar.transform.position, life.transform.position) > 1.6f)
				{
					life.Go(dt, boar.transform.position, 1.6f, false);
					return(life.moveFailed);
				}
				life.Face(boar.transform.position);
				VikingPoses.Interact(life.humanoid);
				VillageSignals.SendEffect(VillageSignals.PetEffect, boar.GetZDOID());
				pettedAt = Time.time;
				return(false);
			}

			/// <summary>
			/// Notes it's no longer petting a boar
			/// </summary>
			public override void End()
			{
				life.SetDoing(PettingKey, false);
			}
		}
	}
}
