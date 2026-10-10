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
		/// ZDO key holding the color text as typed, blank for the light's own color
		/// </summary>
		private static readonly int ColorHash = "kriona.LightColor".GetStableHashCode();

		private const string DefaultText = "default";
		private const int CharacterLimit = 32;

		private ZNetView nview;
		private Tintable tintable;

		/// <summary>
		/// The ZDO data revision last checked, so the color is only read again when the ZDO changes
		/// </summary>
		private uint revision;
		private bool checkedRevision;

		/// <summary>
		/// The color text currently shown on the lights, or null when the lights haven't been set since they were scanned
		/// </summary>
		private string applied = "";

		/// <summary>
		/// Whether the piece is showing a stored color rather than its own colors
		/// </summary>
		public bool HasColor { get; private set; }

		/// <summary>
		/// The stored color the piece is showing, opaque, when HasColor is true
		/// </summary>
		public Color Color { get; private set; }

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
			tintable.Apply(false, Color.white);
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
		/// Opens the text input for typing the light's color
		/// </summary>
		public void RequestColor()
		{
			if (nview == null || !nview.IsValid() || TextInput.instance == null)
			{
				return;
			}
			TextInput.instance.RequestText(this, "Light color - a name or #RRGGBB, blank for default", CharacterLimit);
		}

		/// <summary>
		/// The stored color text, shown in the text input when it opens
		/// </summary>
		/// <returns>The color text, blank for the light's own color</returns>
		public string GetText()
		{
			if (nview == null || !nview.IsValid())
			{
				return("");
			}
			return(nview.GetZDO().GetString(ColorHash));
		}

		/// <summary>
		/// Stores the typed color, or clears it for blank or "default", leaving it unchanged when the text isn't a color
		/// </summary>
		/// <param name="text">The text from the text input</param>
		public void SetText(string text)
		{
			if (nview == null || !nview.IsValid() || !PrivateArea.CheckAccess(transform.position))
			{
				return;
			}
			text = text.Trim();
			if (text.Equals(DefaultText, System.StringComparison.OrdinalIgnoreCase))
			{
				text = "";
			}
			if (text.Length > 0 && !ColorUtility.TryParseHtmlString(text, out Color _))
			{
				Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "Couldn't read the color " + text);
				return;
			}
			nview.ClaimOwnership();
			nview.GetZDO().Set(ColorHash, text);
			Refresh();
		}

		/// <summary>
		/// Applies the stored color when the ZDO has changed since the last check
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
			// Text that doesn't parse, which only another version of the mod could have stored, shows the light's own color
			Color color = Color.white;
			bool hasColor = (text.Length > 0 && ColorUtility.TryParseHtmlString(text, out color));
			color.a = 1f;
			HasColor = hasColor;
			Color = color;
			tintable.Apply(hasColor, color);
		}
	}
}
