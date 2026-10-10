using HarmonyLib;
using UnityEngine;

namespace LightColor
{
	/// <summary>
	/// Keeps the lights on a player's equipped helmet, like the Dverger circlet, in step with the settings stored on
	/// that player
	/// </summary>
	/// <remarks>
	/// Each player stores their own Helmet setting in their player ZDO, which reaches every other player without the
	/// server needing the mod, so everyone with the mod sees each player's helmet the way that player set it
	/// </remarks>
	public class HelmetTint : MonoBehaviour
	{
		/// <summary>
		/// Player ZDO key holding the Helmet setting text, blank for the helmet's own settings
		/// </summary>
		public static readonly int SettingsHash = "kriona.LightColor.helmet".GetStableHashCode();

		private VisEquipment visEquipment;
		private ZNetView nview;

		/// <summary>
		/// The helmet object the lights were recorded from, so a newly equipped helmet is recorded again
		/// </summary>
		private GameObject helmet;
		private Tintable tintable;

		/// <summary>
		/// The ZDO data revision last checked, so the settings are only read again when the ZDO changes
		/// </summary>
		private uint revision;
		private bool checkedRevision;
		private string text = "";

		/// <summary>
		/// The settings text shown on the current helmet, or null when it hasn't been set since the helmet was recorded
		/// </summary>
		private string applied;

		/// <summary>
		/// Records the equipment the helmet is shown on
		/// </summary>
		/// <param name="equipment">The player's equipment visuals</param>
		public void Init(VisEquipment equipment)
		{
			visEquipment = equipment;
			nview = equipment.m_nview;
		}

		/// <summary>
		/// Records a newly equipped helmet and applies the stored settings when they or the helmet have changed
		/// </summary>
		private void Update()
		{
			if (nview == null || !nview.IsValid())
			{
				return;
			}
			GameObject current = visEquipment.m_helmetItemInstance;
			if (current != helmet)
			{
				helmet = current;
				tintable = ((helmet != null) ? new Tintable(helmet) : null);
				applied = null;
			}
			ZDO zdo = nview.GetZDO();
			if (!checkedRevision || zdo.DataRevision != revision)
			{
				checkedRevision = true;
				revision = zdo.DataRevision;
				text = zdo.GetString(SettingsHash);
			}
			if (tintable == null || text == applied)
			{
				return;
			}
			applied = text;
			// Text that can't be read, which only another version of the mod could have stored, leaves the helmet as it is
			LightSettings.TryParse(text, out LightSettings settings, out string _);
			tintable.Apply(settings);
		}
	}

	/// <summary>
	/// Adds a HelmetTint to every player's equipment visuals
	/// </summary>
	[HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.Awake))]
	public static class VisEquipmentAwakePatch
	{
		/// <summary>
		/// Adds the tint to player equipment, leaving out other characters that wear equipment
		/// </summary>
		/// <param name="__instance">The equipment visuals that just woke</param>
		private static void Postfix(VisEquipment __instance)
		{
			if (__instance.m_isPlayer)
			{
				__instance.gameObject.AddComponent<HelmetTint>().Init(__instance);
			}
		}
	}
}
