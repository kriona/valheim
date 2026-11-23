using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Slows a zombie down as it's hurt - at full speed down to half health, then slowing steadily to WoundedSpeed
	/// percent of it as its health runs out
	/// </summary>
	/// <remarks>
	/// It scales the creature's own walking, running, crouching, swimming and flying speeds from the ones it had
	/// before. A speed something else changes (e.g., a zombie viking's as it loads) becomes the new full speed
	/// </remarks>
	internal class ZombieLimp : MonoBehaviour
	{
		/// <summary>
		/// The fraction of its health a zombie can lose before it starts to slow down
		/// </summary>
		private const float SlowBelow = 0.5f;

		private const int Walk = 0;
		private const int Jog = 1;
		private const int Run = 2;
		private const int Crouch = 3;
		private const int Swim = 4;
		private const int FlySlow = 5;
		private const int FlyFast = 6;
		private const int SpeedCount = 7;

		private Character character;

		/// <summary>
		/// Each speed at full health
		/// </summary>
		private readonly float[] fullSpeeds = new float[SpeedCount];

		/// <summary>
		/// Each speed as it was last set here, to tell when something else changed it
		/// </summary>
		private readonly float[] setSpeeds = new float[SpeedCount];

		/// <summary>
		/// Finds the zombie's creature and remembers its speeds
		/// </summary>
		private void Awake()
		{
			character = GetComponent<Character>();
			for (int i = 0; i < SpeedCount; i++)
			{
				fullSpeeds[i] = setSpeeds[i] = GetSpeed(i);
			}
		}

		/// <summary>
		/// Sets the zombie's speeds from its health
		/// </summary>
		private void Update()
		{
			float scale = SpeedScale(character.GetHealth(), character.GetMaxHealth(), Plugin.WoundedSpeed.Value);
			for (int i = 0; i < SpeedCount; i++)
			{
				float speed = GetSpeed(i);
				if (speed != setSpeeds[i])
				{
					fullSpeeds[i] = speed;
				}
				setSpeeds[i] = fullSpeeds[i] * scale;
				SetSpeed(i, setSpeeds[i]);
			}
		}

		/// <summary>
		/// Works out how fast a zombie moves for its health, as a fraction of its full speed
		/// </summary>
		/// <param name="health">Its health</param>
		/// <param name="maxHealth">Its health when unhurt</param>
		/// <param name="woundedSpeed">How fast it moves as its health runs out, as a percent of its full speed (see
		/// Plugin.WoundedSpeed)</param>
		/// <returns>1 at half health or more, or with no health to lose, down to woundedSpeed as a fraction at none</returns>
		internal static float SpeedScale(float health, float maxHealth, int woundedSpeed)
		{
			if (maxHealth <= 0f)
			{
				return(1f);
			}
			float share = Mathf.Clamp01(health / maxHealth);
			if (share >= SlowBelow)
			{
				return(1f);
			}
			return(Mathf.Lerp(woundedSpeed / 100f, 1f, share / SlowBelow));
		}

		/// <summary>
		/// Reads one of the creature's speeds
		/// </summary>
		/// <param name="index">Which speed</param>
		/// <returns>The speed</returns>
		private float GetSpeed(int index)
		{
			switch (index)
			{
				case Walk: return(character.m_walkSpeed);
				case Jog: return(character.m_speed);
				case Run: return(character.m_runSpeed);
				case Crouch: return(character.m_crouchSpeed);
				case Swim: return(character.m_swimSpeed);
				case FlySlow: return(character.m_flySlowSpeed);
				default: return(character.m_flyFastSpeed);
			}
		}

		/// <summary>
		/// Sets one of the creature's speeds
		/// </summary>
		/// <param name="index">Which speed</param>
		/// <param name="speed">The speed</param>
		private void SetSpeed(int index, float speed)
		{
			switch (index)
			{
				case Walk: character.m_walkSpeed = speed; break;
				case Jog: character.m_speed = speed; break;
				case Run: character.m_runSpeed = speed; break;
				case Crouch: character.m_crouchSpeed = speed; break;
				case Swim: character.m_swimSpeed = speed; break;
				case FlySlow: character.m_flySlowSpeed = speed; break;
				default: character.m_flyFastSpeed = speed; break;
			}
		}
	}
}
