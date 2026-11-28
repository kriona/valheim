using System.Linq;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A valkyrie's banish - with two or more undead enemies within 10 meters of her, her AI casts a white spirit nova
	/// around her with her spin, whether or not Zombie Apocalypse is installed (see ValkyrieCombat.BanishFrom)
	/// </summary>
	internal class ValkyrieBanish : MonoBehaviour
	{
		/// <summary>
		/// The name of her banish's item, which tells it apart among her attacks
		/// </summary>
		public const string BanishName = "ValkyrieBanish";

		/// <summary>
		/// How far her banish bursts out around her, and how far off the undead it's cast against can be, in meters
		/// </summary>
		public const float Radius = 10f;

		/// <summary>
		/// How many undead enemies have to be near for her to banish them
		/// </summary>
		internal const int UndeadNeeded = 2;

		/// <summary>
		/// How often she counts the undead around her, in seconds
		/// </summary>
		private const float CountInterval = 0.5f;

		private Humanoid body;
		private float nextCount;

		/// <summary>
		/// Finds her body
		/// </summary>
		private void Awake()
		{
			body = GetComponent<Humanoid>();
		}

		/// <summary>
		/// Checks whether a creature is undead the way Zombie Apocalypse's dvergr mages go by - of the Undead faction, as
		/// zombies are, or weak to spirit damage
		/// </summary>
		/// <param name="faction">Its faction</param>
		/// <param name="spirit">How it takes spirit damage</param>
		/// <returns>True when it's undead</returns>
		internal static bool Undead(Character.Faction faction, HitData.DamageModifier spirit)
		{
			return(faction == Character.Faction.Undead || spirit == HitData.DamageModifier.Weak || spirit == HitData.DamageModifier.VeryWeak);
		}

		/// <summary>
		/// Banishes the undead around her with her AI, on the client that owns her, before her AI's update - when two or
		/// more undead enemies are within reach and her banish is ready again, as often as a dvergr mage casts its nova
		/// </summary>
		/// <returns>True when she starts banishing, so her AI leaves her be this update</returns>
		public bool Think()
		{
			if (Time.time < nextCount || body.InAttack())
			{
				return(false);
			}
			nextCount = Time.time + CountInterval;
			ItemDrop.ItemData banish = body.GetInventory().GetAllItems().FirstOrDefault((ItemDrop.ItemData item) => item.m_shared.m_name == BanishName);
			if (banish == null || !ValkyrieCombat.Ready(Time.time, banish.m_lastAttackTime, banish.m_shared.m_aiAttackInterval) || UndeadNear() < UndeadNeeded)
			{
				return(false);
			}
			if (!body.IsItemEquiped(banish))
			{
				body.EquipItem(banish, false);
			}
			return(body.StartAttack(null, false));
		}

		/// <summary>
		/// Counts the living undead enemies within reach of her
		/// </summary>
		/// <returns>How many there are</returns>
		private int UndeadNear()
		{
			int count = 0;
			Vector3 at = transform.position;
			foreach (Character character in Character.GetAllCharacters())
			{
				if (character == null || character == body || character.IsDead() || !Undead(character.m_faction, character.m_damageModifiers.m_spirit))
				{
					continue;
				}
				if (Vector3.Distance(character.transform.position, at) - character.GetRadius() <= Radius && BaseAI.IsEnemy(body, character))
				{
					count++;
				}
			}
			return(count);
		}
	}
}
