using HarmonyLib;
using UnityEngine;

namespace TooLow
{
	internal static class TooLowMessage
	{
		private const string Text = "Too low";

		/// <summary>
		/// Seconds the message takes to fade out
		/// </summary>
		private const float Duration = 0.5f;

		private static bool shown;

		/// <summary>
		/// Shows the message in the center message slot and fades it out over Duration seconds
		/// </summary>
		/// <remarks>
		/// MessageHud.ShowMessage always fades center messages over 4 seconds, so the text element is driven directly.
		/// Starting a new fade replaces any fade already running on the element
		/// </remarks>
		public static void Show()
		{
			MessageHud hud = MessageHud.instance;
			if (hud == null || Hud.IsUserHidden())
			{
				return;
			}
			hud.m_messageCenterText.text = Text;
			hud.m_messageCenterText.canvasRenderer.SetAlpha(1f);
			hud.m_messageCenterText.CrossFadeAlpha(0f, Duration, true);
			shown = true;
		}

		/// <summary>
		/// Hides the message if it is still the text in the center message slot, leaving any other center message alone
		/// </summary>
		public static void Hide()
		{
			if (!shown)
			{
				return;
			}
			shown = false;
			MessageHud hud = MessageHud.instance;
			if (hud == null || hud.m_messageCenterText.text != Text)
			{
				return;
			}
			hud.m_messageCenterText.CrossFadeAlpha(0f, 0f, true);
		}

		/// <summary>
		/// Whether the attacker is the local player and the weapon is a pickaxe
		/// </summary>
		/// <param name="character">The attacking character</param>
		/// <param name="weapon">The weapon used for the attack</param>
		/// <returns>True for the local player's pickaxe</returns>
		public static bool IsLocalPickaxe(Character character, ItemDrop.ItemData weapon)
		{
			return(character != null && character == Player.m_localPlayer && weapon != null && weapon.m_shared.m_skillType == Skills.SkillType.Pickaxes);
		}
	}

	/// <summary>
	/// Shows the message when the local player's pickaxe hits terrain that is already 8m below its original height
	/// </summary>
	/// <remarks>
	/// The depth is read in the prefix because the dig is applied while the terrain op is instantiated - by the time
	/// the postfix runs, a dig by the terrain owner may already have lowered the ground to the limit. The message is
	/// shown in the postfix so digs refused by a ward or a no-build location stay silent
	/// </remarks>
	[HarmonyPatch(typeof(Attack), nameof(Attack.SpawnOnHitTerrain))]
	public static class SpawnOnHitTerrainPatch
	{
		/// <summary>
		/// Records whether a local pickaxe hit landed on terrain already at the depth limit
		/// </summary>
		/// <param name="hitPoint">Where the attack hit the terrain</param>
		/// <param name="character">The attacking character</param>
		/// <param name="weapon">The weapon used for the attack</param>
		/// <param name="__state">Set to whether the message should be shown if the dig goes ahead</param>
		private static void Prefix(Vector3 hitPoint, Character character, ItemDrop.ItemData weapon, out bool __state)
		{
			__state = (TooLowMessage.IsLocalPickaxe(character, weapon) && Heightmap.AtMaxLevelDepth(hitPoint));
		}

		/// <summary>
		/// Shows the message when the prefix flagged the hit and the terrain op was spawned
		/// </summary>
		/// <param name="__result">The spawned terrain op, or null when the dig was refused</param>
		/// <param name="__state">Whether the prefix found the terrain at the depth limit</param>
		private static void Postfix(GameObject __result, bool __state)
		{
			if (__state && __result != null)
			{
				TooLowMessage.Show();
			}
		}
	}

	/// <summary>
	/// Clears the message as soon as the local player starts another pickaxe swing
	/// </summary>
	[HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
	public static class AttackStartPatch
	{
		/// <summary>
		/// Hides the message when a local pickaxe swing has started
		/// </summary>
		/// <param name="character">The attacking character</param>
		/// <param name="weapon">The weapon being swung</param>
		/// <param name="__result">Whether the attack started</param>
		private static void Postfix(Humanoid character, ItemDrop.ItemData weapon, bool __result)
		{
			if (__result && TooLowMessage.IsLocalPickaxe(character, weapon))
			{
				TooLowMessage.Hide();
			}
		}
	}
}
