using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Elements
{
	/// <summary>
	/// Circle that fills clockwise from the top, with an ink outline and a paper hole in the middle for an icon.
	/// Styled with --ring-color, --ring-track, --ring-border, --ring-thickness.
	/// </summary>
	[UxmlElement]
	public partial class RadialProgress : VisualElement
	{
		private static readonly CustomStyleProperty<Color> ColorProperty = new CustomStyleProperty<Color>("--ring-color");
		private static readonly CustomStyleProperty<Color> TrackProperty = new CustomStyleProperty<Color>("--ring-track");
		private static readonly CustomStyleProperty<Color> BorderProperty = new CustomStyleProperty<Color>("--ring-border");
		private static readonly CustomStyleProperty<float> ThicknessProperty = new CustomStyleProperty<float>("--ring-thickness");

		private const float BorderWidth = 3f;

		private float _value = 1f;
		private Color _color = Color.white;
		private Color _track = Color.white;
		private Color _border = Color.black;
		private float _thickness = 8f;

		public RadialProgress()
		{
			generateVisualContent += Draw;
			RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
		}

		/// <summary>Filled part, 0..1.</summary>
		[UxmlAttribute("value"), CreateProperty]
		public float Value
		{
			get => _value;
			set
			{
				float clamped = Mathf.Clamp01(value);

				if (Mathf.Approximately(clamped, _value))
				{
					return;
				}

				_value = clamped;
				MarkDirtyRepaint();
			}
		}

		private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
		{
			ICustomStyle style = evt.customStyle;
			_color = style.TryGetValue(ColorProperty, out Color color) ? color : Color.white;
			_track = style.TryGetValue(TrackProperty, out Color track) ? track : Color.white;
			_border = style.TryGetValue(BorderProperty, out Color border) ? border : Color.black;
			_thickness = style.TryGetValue(ThicknessProperty, out float thickness) ? thickness : 8f;
			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			float size = Mathf.Min(resolvedStyle.width, resolvedStyle.height);

			if (size <= 0f)
			{
				return;
			}

			Vector2 center = new Vector2(resolvedStyle.width, resolvedStyle.height) * .5f;
			float outer = size * .5f - BorderWidth * .5f;
			float inner = outer - _thickness;
			Painter2D painter = context.painter2D;

			painter.fillColor = _track;
			painter.BeginPath();
			painter.Arc(center, outer, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
			painter.Fill();

			if (_value > 0f)
			{
				painter.fillColor = _color;
				painter.BeginPath();
				painter.MoveTo(center);
				painter.Arc(center, outer, Angle.Degrees(-90f), Angle.Degrees(-90f + 360f * _value), ArcDirection.Clockwise);
				painter.ClosePath();
				painter.Fill();
			}

			painter.fillColor = _track;
			painter.BeginPath();
			painter.Arc(center, inner, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
			painter.Fill();

			painter.strokeColor = _border;
			painter.lineWidth = BorderWidth;
			painter.BeginPath();
			painter.Arc(center, outer, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
			painter.Stroke();

			painter.lineWidth = BorderWidth * .7f;
			painter.BeginPath();
			painter.Arc(center, inner, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
			painter.Stroke();
		}
	}
}
