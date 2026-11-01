using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Elements
{
	/// <summary>
	/// Manga-panel background: a fill with an ink outline and a hard offset shadow, optionally slanted.
	/// USS has neither skew nor box-shadow, so the shape is drawn here. Styled with custom properties:
	/// --panel-fill, --panel-border, --panel-border-width, --panel-shadow, --panel-shadow-x, --panel-shadow-y,
	/// --panel-skew (degrees; negative leans the top to the right).
	/// </summary>
	[UxmlElement]
	public partial class InkPanel : VisualElement
	{
		private static readonly CustomStyleProperty<Color> FillProperty = new CustomStyleProperty<Color>("--panel-fill");
		private static readonly CustomStyleProperty<Color> BorderProperty = new CustomStyleProperty<Color>("--panel-border");
		private static readonly CustomStyleProperty<float> BorderWidthProperty = new CustomStyleProperty<float>("--panel-border-width");
		private static readonly CustomStyleProperty<Color> ShadowProperty = new CustomStyleProperty<Color>("--panel-shadow");
		private static readonly CustomStyleProperty<float> ShadowXProperty = new CustomStyleProperty<float>("--panel-shadow-x");
		private static readonly CustomStyleProperty<float> ShadowYProperty = new CustomStyleProperty<float>("--panel-shadow-y");
		private static readonly CustomStyleProperty<float> SkewProperty = new CustomStyleProperty<float>("--panel-skew");

		private Color _fill = Color.white;
		private Color _border = Color.clear;
		private float _borderWidth;
		private Color _shadow = Color.clear;
		private Vector2 _shadowOffset;
		private float _skew;

		public InkPanel()
		{
			generateVisualContent += Draw;
			RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
		}

		private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
		{
			ICustomStyle style = evt.customStyle;
			_fill = style.TryGetValue(FillProperty, out Color fill) ? fill : Color.white;
			_border = style.TryGetValue(BorderProperty, out Color border) ? border : Color.clear;
			_borderWidth = style.TryGetValue(BorderWidthProperty, out float borderWidth) ? borderWidth : 0f;
			_shadow = style.TryGetValue(ShadowProperty, out Color shadow) ? shadow : Color.clear;
			_shadowOffset = new Vector2(
				style.TryGetValue(ShadowXProperty, out float x) ? x : 0f,
				style.TryGetValue(ShadowYProperty, out float y) ? y : 0f);
			_skew = style.TryGetValue(SkewProperty, out float skew) ? skew : 0f;
			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			Rect rect = new Rect(0f, 0f, resolvedStyle.width, resolvedStyle.height);

			if (rect.width <= 0f || rect.height <= 0f)
			{
				return;
			}

			// Horizontal lean of the top edge relative to the bottom one; kept inside the rect.
			float lean = Mathf.Clamp(-Mathf.Tan(_skew * Mathf.Deg2Rad) * rect.height, -rect.width * .5f, rect.width * .5f);
			Painter2D painter = context.painter2D;

			if (_shadow.a > 0f && _shadowOffset != Vector2.zero)
			{
				painter.fillColor = _shadow;
				Trace(painter, rect, lean, _shadowOffset);
				painter.Fill();
			}

			painter.fillColor = _fill;
			Trace(painter, rect, lean, Vector2.zero);
			painter.Fill();

			if (_borderWidth > 0f && _border.a > 0f)
			{
				float inset = _borderWidth * .5f;
				Rect inner = new Rect(rect.x + inset, rect.y + inset, rect.width - _borderWidth, rect.height - _borderWidth);
				painter.strokeColor = _border;
				painter.lineWidth = _borderWidth;
				painter.lineJoin = LineJoin.Miter;
				Trace(painter, inner, lean * (inner.height / rect.height), Vector2.zero);
				painter.Stroke();
			}
		}

		private static void Trace(Painter2D painter, Rect rect, float lean, Vector2 offset)
		{
			float topShift = Mathf.Max(lean, 0f);
			float bottomShift = Mathf.Max(-lean, 0f);

			painter.BeginPath();
			painter.MoveTo(offset + new Vector2(rect.xMin + topShift, rect.yMin));
			painter.LineTo(offset + new Vector2(rect.xMax - bottomShift, rect.yMin));
			painter.LineTo(offset + new Vector2(rect.xMax - topShift, rect.yMax));
			painter.LineTo(offset + new Vector2(rect.xMin + bottomShift, rect.yMax));
			painter.ClosePath();
		}
	}
}
