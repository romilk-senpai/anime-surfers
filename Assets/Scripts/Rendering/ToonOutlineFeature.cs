using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Game.Rendering
{
	/// <summary>
	/// Draws the "ToonOutline" pass of opaque objects after all opaques, so the outlines batch together instead of
	/// switching shaders on every object. Renderers moved to the <see cref="HiddenLayer"/> rendering layer with
	/// <see cref="SetOutlined"/> are skipped: far away the outline is thinner than a pixel, so its draw is wasted.
	/// </summary>
	public class ToonOutlineFeature : ScriptableRendererFeature
	{
		/// <summary>
		/// Distance at which spawners drop the outline. ToonCharacter thins it to width * reference distance /
		/// distance pixels, about a quarter pixel here at the default 1.5 px and 6 m.
		/// </summary>
		public const float MaxDistance = 35f;

		/// <summary>
		/// The only rendering layer of renderers without an outline. Light layers are off, so the layer changes
		/// nothing else; with them on, these renderers would also lose the lights of the default layer.
		/// </summary>
		private const uint HiddenLayer = 1u << 31;

		private class OutlinePass : ScriptableRenderPass
		{
			private static readonly ShaderTagId OutlineTag = new ShaderTagId("ToonOutline");

			private class PassData
			{
				public RendererListHandle RendererList;
			}

			public OutlinePass()
			{
				renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
				profilingSampler = new ProfilingSampler("Toon Outlines");
			}

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
			{
				var renderingData = frameData.Get<UniversalRenderingData>();
				var cameraData = frameData.Get<UniversalCameraData>();
				var lightData = frameData.Get<UniversalLightData>();
				var resourceData = frameData.Get<UniversalResourceData>();

				DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(OutlineTag, renderingData, cameraData,
					lightData, SortingCriteria.CommonOpaque);
				var filtering = new FilteringSettings(RenderQueueRange.opaque, -1, ~HiddenLayer);

				using var builder = renderGraph.AddRasterRenderPass<PassData>("Toon Outlines", out var passData,
					profilingSampler);

				passData.RendererList = renderGraph.CreateRendererList(
					new RendererListParams(renderingData.cullResults, drawing, filtering));
				builder.UseRendererList(passData.RendererList);
				builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
				builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);

				builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
					context.cmd.DrawRendererList(data.RendererList));
			}
		}

		private OutlinePass _pass;

		/// <summary>The rendering layer mask for objects with or without an outline, for draws without a renderer.</summary>
		public static uint RenderingLayer(bool outlined)
		{
			return outlined ? RenderingLayerMask.defaultRenderingLayerMask : HiddenLayer;
		}

		/// <summary>Turns the outline of the given renderers on or off.</summary>
		public static void SetOutlined(List<Renderer> renderers, bool outlined)
		{
			uint layer = RenderingLayer(outlined);

			foreach (Renderer renderer in renderers)
			{
				renderer.renderingLayerMask = layer;
			}
		}

		public override void Create()
		{
			_pass = new OutlinePass();
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			renderer.EnqueuePass(_pass);
		}
	}
}
