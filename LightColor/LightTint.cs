using UnityEngine;

namespace LightColor
{
	/// <summary>
	/// Added to every built piece with lights, keeping their color in step with the color text stored on the piece
	/// and letting the player type a new one, like the text on a sign
	/// </summary>
	/// <remarks>
	/// The color is stored in the piece's ZDO, which the server saves with the world and sends to other players
	/// without needing the mod itself. Players without the mod ignore it and see the light's own color
	/// </remarks>
	public class LightTint : MonoBehaviour, TextReceiver
	{
		/// <summary>
		/// ZDO key holding the settings text as typed, like "blue 150% 20m", blank for the piece's own settings
		/// </summary>
		private static readonly int ColorHash = "kriona.LightColor".GetStableHashCode();

		private const int CharacterLimit = 32;

		private ZNetView nview;
		private Tintable tintable;

		/// <summary>
		/// The ZDO data revision last checked, so the color is only read again when the ZDO changes
		/// </summary>
		private uint revision;
		private bool checkedRevision;

		/// <summary>
		/// The settings text currently shown on the lights, or null when the lights haven't been set since they were scanned
		/// </summary>
		private string applied = "";

		/// <summary>
		/// The stored settings the piece is showing
		/// </summary>
		public LightSettings Settings { get; private set; } = LightSettings.Default;

		/// <summary>
		/// Whether the piece has anything left to color - a light, a particle system or a recolorable material
		/// </summary>
		public bool HasEffects
		{
			get
			{
				return(tintable.HasEffects);
			}
		}

		/// <summary>
		/// Records the piece's lights, particle systems and recolorable renderers with their own colors and applies the stored color
		/// </summary>
		/// <param name="view">The piece's network view</param>
		public void Init(ZNetView view)
		{
			nview = view;
			tintable = new Tintable(gameObject);
			Refresh();
		}

		/// <summary>
		/// Puts back the colors of everything recorded, then records the piece's children again and applies the
		/// stored color to them, for when an item stand swaps the item it shows
		/// </summary>
		public void Rescan()
		{
			tintable.Apply(LightSettings.Default);
			tintable = new Tintable(gameObject);
			applied = null;
			checkedRevision = false;
			Refresh();
		}

		/// <summary>
		/// Picks up colors changed by other players
		/// </summary>
		private void Update()
		{
			Refresh();
		}

		/// <summary>
		/// Opens the text input for typing the light's color, brightness and range
		/// </summary>
		public void RequestColor()
		{
			if (nview == null || !nview.IsValid() || TextInput.instance == null)
			{
				return;
			}
			TextInput.instance.RequestText(this, "Light - a color, brightness % and range m, e.g. blue 150% 20m", CharacterLimit);
		}

		/// <summary>
		/// The stored settings text, shown in the text input when it opens
		/// </summary>
		/// <returns>The settings text, blank for the piece's own settings</returns>
		public string GetText()
		{
			if (nview == null || !nview.IsValid())
			{
				return("");
			}
			return(nview.GetZDO().GetString(ColorHash));
		}

		/// <summary>
		/// Stores the typed settings, or clears them when they change nothing, like blank or "default", leaving them
		/// unchanged and showing why when the text can't be read
		/// </summary>
		/// <param name="text">The text from the text input</param>
		public void SetText(string text)
		{
			if (nview == null || !nview.IsValid() || !PrivateArea.CheckAccess(transform.position))
			{
				return;
			}
			text = text.Trim();
			if (!LightSettings.TryParse(text, out LightSettings settings, out string error))
			{
				Player.m_localPlayer?.Message(MessageHud.MessageType.Center, error);
				return;
			}
			if (settings.IsDefault())
			{
				text = "";
			}
			nview.ClaimOwnership();
			nview.GetZDO().Set(ColorHash, text);
			Refresh();
		}

		/// <summary>
		/// Applies the stored settings when the ZDO has changed since the last check
		/// </summary>
		private void Refresh()
		{
			if (nview == null || !nview.IsValid())
			{
				return;
			}
			ZDO zdo = nview.GetZDO();
			if (checkedRevision && zdo.DataRevision == revision)
			{
				return;
			}
			checkedRevision = true;
			revision = zdo.DataRevision;
			string text = zdo.GetString(ColorHash);
			if (text == applied)
			{
				return;
			}
			applied = text;
			// Text that can't be read, which only another version of the mod could have stored, leaves the piece as it is
			LightSettings.TryParse(text, out LightSettings settings, out string _);
			Settings = settings;
			tintable.Apply(settings);
		}
	}
}
