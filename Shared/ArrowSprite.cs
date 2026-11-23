using UnityEngine;

namespace Kriona.Shared
{
	/// <summary>
	/// The outlined arrow that points at something, drawn into a texture - a triangular head with its tip at the bottom
	/// and a shaft above it, filled white so an image's color tints it while its dark outline stays dark under any tint
	/// </summary>
	internal static class ArrowSprite
	{
		/// <summary>
		/// The color of the arrow's outline
		/// </summary>
		internal static readonly Color OutlineColor = new Color(0.1f, 0.1f, 0.1f, 1f);

		/// <summary>
		/// The texture's size and how thick the outline is, in pixels
		/// </summary>
		private const int Width = 48;
		private const int Height = 64;
		private const float Outline = 3f;

		/// <summary>
		/// Draws the arrow into a new texture, with its pivot on the tip - the caller destroys the sprite and its texture
		/// once it's done with them
		/// </summary>
		/// <returns>The sprite</returns>
		internal static Sprite Create()
		{
			Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.filterMode = FilterMode.Bilinear;
			Color[] pixels = new Color[Width * Height];
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					float px = Mathf.Abs(x + 0.5f - Width * 0.5f);
					float py = y + 0.5f;
					Color color = Color.clear;
					if (InArrow(px, py, Outline))
					{
						color = Color.white;
					}
					else if (InArrow(px, py, 0f))
					{
						color = OutlineColor;
					}
					pixels[y * Width + x] = color;
				}
			}
			texture.SetPixels(pixels);
			texture.Apply();
			return(Sprite.Create(texture, new Rect(0f, 0f, Width, Height), new Vector2(0.5f, 0f)));
		}

		/// <summary>
		/// Checks whether a point is inside the arrow's shape shrunk by an inset
		/// </summary>
		/// <param name="px">How far the point is across from the arrow's middle, in pixels</param>
		/// <param name="py">How high the point is above the texture's bottom, in pixels</param>
		/// <param name="inset">How far to shrink the shape on every side, in pixels</param>
		/// <returns>True when the point is inside</returns>
		internal static bool InArrow(float px, float py, float inset)
		{
			float headHeight = Height * 0.55f;
			float halfHead = Width * 0.5f;
			float halfShaft = Width * 0.2f;
			float slope = halfHead / headHeight;
			// How far across matches an inset at right angles to the slanted edges
			float slantInset = inset * Mathf.Sqrt(1f + slope * slope);
			bool inHead = (py <= headHeight - inset && px <= py * slope - slantInset);
			bool inShaft = (py >= headHeight * 0.5f && py <= Height - inset && px <= halfShaft - inset);
			return(inHead || inShaft);
		}
	}
}
