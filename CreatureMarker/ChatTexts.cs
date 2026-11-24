using System.Collections.Generic;
using UnityEngine;

namespace CreatureMarker
{
	/// <summary>
	/// Where the markers moved the game's ping and shout texts this frame, put back after the game places them
	/// </summary>
	/// <remarks>
	/// Unity runs the chat's LateUpdate and the camera's in no set order, so the chat can place its texts again after
	/// the markers have moved them
	/// </remarks>
	internal static class ChatTexts
	{
		private static readonly Dictionary<GameObject, Vector3> held = new Dictionary<GameObject, Vector3>();

		/// <summary>
		/// The frame the held positions are for
		/// </summary>
		private static int frame = -1;

		/// <summary>
		/// Holds a text at a screen position for the rest of the frame
		/// </summary>
		/// <param name="text">The game's text</param>
		/// <param name="position">Where the text goes, in screen pixels</param>
		public static void Hold(GameObject text, Vector3 position)
		{
			if (frame != Time.frameCount)
			{
				held.Clear();
				frame = Time.frameCount;
			}
			held[text] = position;
		}

		/// <summary>
		/// Puts the texts held this frame back where the markers moved them
		/// </summary>
		public static void Apply()
		{
			if (frame != Time.frameCount)
			{
				return;
			}
			foreach (KeyValuePair<GameObject, Vector3> entry in held)
			{
				if (entry.Key != null)
				{
					entry.Key.transform.position = entry.Value;
				}
			}
		}
	}
}
