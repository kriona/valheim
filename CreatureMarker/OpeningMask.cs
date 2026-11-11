using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureMarker
{
	/// <summary>
	/// The shape of a portal's opening on screen, which a Mask uses to cut the markers of the traders and pets seen
	/// through the portal so they only show within the opening, not over its frame
	/// </summary>
	/// <remarks>
	/// The outline is drawn as a fan from the opening's center, which works for any outline that every point can be
	/// seen from the center of, as portal openings can
	/// </remarks>
	internal class OpeningMask : MaskableGraphic
	{
		/// <summary>
		/// The outline's points in screen pixels
		/// </summary>
		private readonly List<Vector2> points = new List<Vector2>();

		/// <summary>
		/// The opening's center in screen pixels
		/// </summary>
		private Vector2 center;

		/// <summary>
		/// Creates a mask filling the markers' canvas, drawing nothing itself
		/// </summary>
		/// <param name="layer">The markers' canvas</param>
		/// <returns>The mask</returns>
		public static OpeningMask Create(RectTransform layer)
		{
			GameObject go = new GameObject("CreatureMarkerPortalMask", typeof(RectTransform));
			go.transform.SetParent(layer, false);
			RectTransform rect = (RectTransform)go.transform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			OpeningMask opening = go.AddComponent<OpeningMask>();
			opening.raycastTarget = false;
			go.AddComponent<Mask>().showMaskGraphic = false;
			return(opening);
		}

		/// <summary>
		/// Sets the opening's shape on screen
		/// </summary>
		/// <param name="outline">The outline's points in screen pixels</param>
		/// <param name="openingCenter">The opening's center in screen pixels</param>
		public void SetShape(List<Vector2> outline, Vector2 openingCenter)
		{
			points.Clear();
			points.AddRange(outline);
			center = openingCenter;
			SetVerticesDirty();
		}

		/// <summary>
		/// Builds the fan of triangles covering the opening
		/// </summary>
		/// <param name="vh">The mesh being built</param>
		protected override void OnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();
			int count = points.Count;
			if (count < 3)
			{
				return;
			}
			UIVertex vertex = UIVertex.simpleVert;
			vertex.color = color;
			// Positions are set in screen pixels, which are the canvas's world space
			vertex.position = rectTransform.InverseTransformPoint(center);
			vh.AddVert(vertex);
			foreach (Vector2 point in points)
			{
				vertex.position = rectTransform.InverseTransformPoint(point);
				vh.AddVert(vertex);
			}
			for (int i = 0; i < count; i++)
			{
				vh.AddTriangle(0, i + 1, ((i + 1) % count) + 1);
			}
		}
	}
}
