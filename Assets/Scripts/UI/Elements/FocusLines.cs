using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Elements
{
	/// <summary>
	/// Manga focus lines (集中線): thin wedges radiating from a point, drawn behind a sound effect.
	/// The centre is a fraction of the element's size. Colour from --lines-color. Changing <see cref="Phase"/> rotates
	/// the burst, which flickers it when changed every few frames.
	/// </summary>
	[UxmlElement]
	public partial class FocusLines : VisualElement
	{
		private static readonly CustomStyleProperty<Color> ColorProperty = new CustomStyleProperty<Color>("--lines-color");

		private Color _color = Color.white;
		private float _phase;

		public FocusLines()
		{
			pickingMode = PickingMode.Ignore;
			generateVisualContent += Draw;
			RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
		}

		[UxmlAttribute] public float CenterX { get; set; } = .25f;
		[UxmlAttribute] public float CenterY { get; set; } = .32f;
		[UxmlAttribute] public float InnerRadius { get; set; } = 170f;
		[UxmlAttribute] public int Count { get; set; } = 56;

		/// <summary>Rotation of the burst in radians.</summary>
		public float Phase
		{
			get => _phase;
			set
			{
				_phase = value;
				MarkDirtyRepaint();
			}
		}

		private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
		{
			_color = evt.customStyle.TryGetValue(ColorProperty, out Color color) ? color : Color.white;
			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			float width = resolvedStyle.width;
			float height = resolvedStyle.height;
			Vector2 center = new Vector2(width * CenterX, height * CenterY);
			float outer = new Vector2(width, height).magnitude;
			Painter2D painter = context.painter2D;
			painter.fillColor = _color;

			// Fixed pseudo-random widths and lengths so the burst looks hand-drawn but never flickers.
			for (int i = 0; i < Count; i++)
			{
				float angle = i / (float)Count * Mathf.PI * 2f + (i * 37 % 11) * .006f + _phase;
				float halfWidth = .008f + (i * 53 % 7) * .004f;
				float inner = InnerRadius + (i * 29 % 5) * 26f;

				painter.BeginPath();
				painter.MoveTo(center + Direction(angle) * inner);
				painter.LineTo(center + Direction(angle - halfWidth) * outer);
				painter.LineTo(center + Direction(angle + halfWidth) * outer);
				painter.ClosePath();
				painter.Fill();
			}
		}

		private static Vector2 Direction(float angle)
		{
			return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
		}
	}
}
