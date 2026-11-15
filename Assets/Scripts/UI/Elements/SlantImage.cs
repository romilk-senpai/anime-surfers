using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Elements
{
	/// <summary>
	/// An image clipped to the same slanted quad an <see cref="InkPanel"/> draws, scaled to cover it and cropped.
	/// USS can't clip to a parallelogram, so the textured quad is built here. Styled with --panel-skew (as InkPanel),
	/// --image-focus-y (0 = keep the top of the image, 1 = the bottom) and -unity-background-image-tint-color.
	/// </summary>
	[UxmlElement]
	public partial class SlantImage : VisualElement
	{
		private static readonly CustomStyleProperty<float> SkewProperty = new CustomStyleProperty<float>("--panel-skew");
		private static readonly CustomStyleProperty<float> FocusYProperty = new CustomStyleProperty<float>("--image-focus-y");
		private static readonly ushort[] Indices = { 0, 1, 2, 0, 2, 3 };

		private readonly Vector2[] _corners = new Vector2[4];
		private Texture _image;
		private float _skew;
		private float _focusY = .5f;

		public SlantImage()
		{
			generateVisualContent += Draw;
			RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
		}

		public Texture Image
		{
			get => _image;
			set
			{
				_image = value;
				MarkDirtyRepaint();
			}
		}

		private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
		{
			ICustomStyle style = evt.customStyle;
			_skew = style.TryGetValue(SkewProperty, out float skew) ? skew : 0f;
			_focusY = style.TryGetValue(FocusYProperty, out float focusY) ? focusY : .5f;
			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			Rect rect = new Rect(0f, 0f, resolvedStyle.width, resolvedStyle.height);

			if (_image == null || rect.width <= 0f || rect.height <= 0f)
			{
				return;
			}

			// Same lean as InkPanel, so the image lines up with a panel of the same skew.
			float lean = Mathf.Clamp(-Mathf.Tan(_skew * Mathf.Deg2Rad) * rect.height, -rect.width * .5f, rect.width * .5f);
			float topShift = Mathf.Max(lean, 0f);
			float bottomShift = Mathf.Max(-lean, 0f);
			_corners[0] = new Vector2(rect.xMin + topShift, rect.yMin);
			_corners[1] = new Vector2(rect.xMax - bottomShift, rect.yMin);
			_corners[2] = new Vector2(rect.xMax - topShift, rect.yMax);
			_corners[3] = new Vector2(rect.xMin + bottomShift, rect.yMax);

			// Cover the bounding rect: the visible window of the texture, in UV units (v runs bottom to top).
			float rectAspect = rect.width / rect.height;
			float imageAspect = (float)_image.width / _image.height;
			Vector2 window = rectAspect > imageAspect
				? new Vector2(1f, imageAspect / rectAspect)
				: new Vector2(rectAspect / imageAspect, 1f);
			float windowTop = Mathf.Lerp(1f, window.y, Mathf.Clamp01(_focusY));
			float windowLeft = (1f - window.x) * .5f;

			Color tint = resolvedStyle.unityBackgroundImageTintColor;
			MeshWriteData mesh = context.Allocate(4, Indices.Length, _image);
			Rect region = mesh.uvRegion;

			foreach (Vector2 corner in _corners)
			{
				Vector2 uv = new Vector2(
					windowLeft + corner.x / rect.width * window.x,
					windowTop - corner.y / rect.height * window.y);
				mesh.SetNextVertex(new Vertex
				{
					position = new Vector3(corner.x, corner.y, Vertex.nearZ),
					tint = tint,
					uv = region.min + uv * region.size,
				});
			}

			mesh.SetAllIndices(Indices);
		}
	}
}
