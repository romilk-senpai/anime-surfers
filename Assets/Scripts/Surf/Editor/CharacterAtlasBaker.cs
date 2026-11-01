using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Surf.EditorTools
{
	/// <summary>
	/// Cuts a textured toon character's draw calls. Packs the base maps of its skinned mesh's materials into one atlas,
	/// then merges the submeshes whose materials differ only in base map and base color, so the mesh draws once per
	/// remaining material instead of once per submesh. Base colors are baked into the atlas. The model's own mesh,
	/// materials and textures are left untouched, so re-run this after reimporting the model.
	/// </summary>
	public static class CharacterAtlasBaker
	{
		private const string ToonShader = "AnimeSurfers/ToonCharacter";

		// Folded into the atlas; every other shader property must match for two materials to merge.
		private static readonly HashSet<string> AtlasProperties = new HashSet<string> { "_BaseMap", "_BaseColor" };

		[MenuItem("Tools/Surf/Bake Character Atlas (Selected Prefabs)")]
		public static void BakeSelected()
		{
			foreach (GameObject prefab in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
			{
				Bake(AssetDatabase.GetAssetPath(prefab));
			}
		}

		[MenuItem("Tools/Surf/Bake Character Atlas (Selected Prefabs)", true)]
		private static bool CanBakeSelected()
		{
			return Selection.GetFiltered<GameObject>(SelectionMode.Assets).Length > 0;
		}

		public static void Bake(string prefabPath)
		{
			GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

			try
			{
				SkinnedMeshRenderer renderer = FindRenderer(root);
				if (renderer == null)
				{
					Debug.LogWarning($"{prefabPath}: needs a SkinnedMeshRenderer with several submeshes.");
					return;
				}

				Mesh source = renderer.sharedMesh;
				Material[] materials = renderer.sharedMaterials;
				if (!CanBake(source, materials, out string reason))
				{
					Debug.LogWarning($"{prefabPath}: can't atlas {renderer.name}, {reason}");
					return;
				}

				// A cell per distinct base map and base color pair, a group per distinct remaining material setup.
				var cells = new List<(Texture2D texture, Color color)>();
				var groups = new List<Material>();
				var cellOf = new int[materials.Length];
				var groupOf = new int[materials.Length];
				for (int i = 0; i < materials.Length; i++)
				{
					var cell = ((Texture2D)materials[i].GetTexture("_BaseMap"), materials[i].GetColor("_BaseColor"));
					cellOf[i] = cells.IndexOf(cell);
					if (cellOf[i] < 0)
					{
						cellOf[i] = cells.Count;
						cells.Add(cell);
					}

					groupOf[i] = groups.FindIndex(group => SameExceptAtlas(group, materials[i]));
					if (groupOf[i] < 0)
					{
						groupOf[i] = groups.Count;
						groups.Add(materials[i]);
					}
				}

				if (!CellsAreSeparate(source, cellOf, out reason))
				{
					Debug.LogWarning($"{prefabPath}: can't atlas {renderer.name}, {reason}");
					return;
				}

				// Power-of-two grid, so cell borders line up with mip texels and mips don't blend neighboring cells.
				int columns = Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Sqrt(cells.Count)));
				int rows = Mathf.NextPowerOfTwo(Mathf.CeilToInt(cells.Count / (float)columns));

				string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(source)).Replace('\\', '/');
				string name = Path.GetFileNameWithoutExtension(prefabPath);
				bool keepAlpha = groups.Exists(group => group.IsKeywordEnabled("_ALPHATEST_ON"));

				Texture2D atlas = SaveAtlas($"{Subfolder(folder, "Textures")}/{name}_Atlas.png", cells, columns, rows,
					keepAlpha);
				Mesh mesh = SaveMesh($"{folder}/{name}_Atlas.asset", source, cellOf, groupOf, groups.Count, columns,
					rows);
				var atlasMaterials = new Material[groups.Count];
				for (int i = 0; i < groups.Count; i++)
				{
					atlasMaterials[i] = SaveMaterial($"{Subfolder(folder, "Materials")}/{name}_Atlas_{groups[i].name}.mat",
						groups[i], atlas);
				}

				renderer.sharedMesh = mesh;
				renderer.sharedMaterials = atlasMaterials;
				PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
				Debug.Log($"{prefabPath}: {renderer.name} went from {materials.Length} submeshes to {groups.Count}, " +
					$"{cells.Count} textures packed into {atlas.name}.");
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(root);
			}
		}

		private static SkinnedMeshRenderer FindRenderer(GameObject root)
		{
			foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				if (renderer.sharedMesh != null && renderer.sharedMesh.subMeshCount > 1)
				{
					return renderer;
				}
			}

			return null;
		}

		private static string Subfolder(string parent, string name)
		{
			string path = $"{parent}/{name}";
			if (!AssetDatabase.IsValidFolder(path))
			{
				AssetDatabase.CreateFolder(parent, name);
			}

			return path;
		}

		private static bool CanBake(Mesh mesh, Material[] materials, out string reason)
		{
			if (materials.Length != mesh.subMeshCount)
			{
				reason = $"it has {mesh.subMeshCount} submeshes but {materials.Length} materials.";
				return false;
			}

			int size = 0;
			foreach (Material material in materials)
			{
				if (material == null || material.shader.name != ToonShader)
				{
					reason = $"all materials must use {ToonShader}.";
					return false;
				}

				if (!(material.GetTexture("_BaseMap") is Texture2D texture))
				{
					reason = $"{material.name} has no base map.";
					return false;
				}

				if (material.GetTextureScale("_BaseMap") != Vector2.one ||
					material.GetTextureOffset("_BaseMap") != Vector2.zero)
				{
					reason = $"{material.name} tiles or offsets its base map.";
					return false;
				}

				size = size == 0 ? texture.width : size;
				if (texture.width != size || texture.height != size)
				{
					reason = "all base maps must be square and the same size.";
					return false;
				}
			}

			// Tiling UVs would sample neighboring cells.
			var uvs = new List<Vector2>();
			mesh.GetUVs(0, uvs);
			foreach (Vector2 uv in uvs)
			{
				if (uv.x < -0.001f || uv.x > 1.001f || uv.y < -0.001f || uv.y > 1.001f)
				{
					reason = $"its UVs leave the 0-1 range ({uv}).";
					return false;
				}
			}

			reason = null;
			return true;
		}

		private static bool SameExceptAtlas(Material a, Material b)
		{
			Shader shader = a.shader;
			if (b.shader != shader)
			{
				return false;
			}

			for (int i = 0; i < shader.GetPropertyCount(); i++)
			{
				string property = shader.GetPropertyName(i);
				if (AtlasProperties.Contains(property))
				{
					continue;
				}

				bool same = shader.GetPropertyType(i) switch
				{
					ShaderPropertyType.Color => a.GetColor(property) == b.GetColor(property),
					ShaderPropertyType.Vector => a.GetVector(property) == b.GetVector(property),
					ShaderPropertyType.Texture => a.GetTexture(property) == b.GetTexture(property),
					_ => Mathf.Approximately(a.GetFloat(property), b.GetFloat(property))
				};

				if (!same)
				{
					return false;
				}
			}

			return true;
		}

		// A vertex can only have one UV, so it can't serve submeshes that land in different cells.
		private static bool CellsAreSeparate(Mesh mesh, int[] cellOf, out string reason)
		{
			var vertexCell = new int[mesh.vertexCount];
			for (int i = 0; i < vertexCell.Length; i++)
			{
				vertexCell[i] = -1;
			}

			for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
			{
				foreach (int index in mesh.GetTriangles(submesh))
				{
					if (vertexCell[index] >= 0 && vertexCell[index] != cellOf[submesh])
					{
						reason = $"vertex {index} is shared by submeshes with different textures.";
						return false;
					}

					vertexCell[index] = cellOf[submesh];
				}
			}

			reason = null;
			return true;
		}

		private static Texture2D SaveAtlas(string path, List<(Texture2D texture, Color color)> cells, int columns,
			int rows, bool keepAlpha)
		{
			int size = cells[0].texture.width;
			int width = columns * size;
			var pixels = new Color32[width * rows * size];
			bool linear = PlayerSettings.colorSpace == ColorSpace.Linear;

			for (int cell = 0; cell < cells.Count; cell++)
			{
				Color32[] source = ReadPixels(cells[cell].texture);
				Color color = cells[cell].color;
				byte[] r = ColorCurve(color.r, linear);
				byte[] g = ColorCurve(color.g, linear);
				byte[] b = ColorCurve(color.b, linear);
				byte[] a = ColorCurve(color.a, false);
				int left = cell % columns * size;
				int bottom = cell / columns * size;

				for (int y = 0; y < size; y++)
				{
					for (int x = 0; x < size; x++)
					{
						Color32 texel = source[y * size + x];
						pixels[(bottom + y) * width + left + x] = new Color32(r[texel.r], g[texel.g], b[texel.b],
							keepAlpha ? a[texel.a] : (byte)255);
					}
				}
			}

			var texture = new Texture2D(width, rows * size, TextureFormat.RGBA32, false);
			texture.SetPixels32(pixels);
			File.WriteAllBytes(path, texture.EncodeToPNG());
			Object.DestroyImmediate(texture);
			AssetDatabase.ImportAsset(path);

			var importer = (TextureImporter)AssetImporter.GetAtPath(path);
			importer.textureType = TextureImporterType.Default;
			importer.sRGBTexture = true;
			importer.mipmapEnabled = true;
			importer.alphaSource = keepAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
			importer.wrapMode = TextureWrapMode.Clamp;
			importer.maxTextureSize = Mathf.Max(width, rows * size);
			importer.textureCompression = TextureImporterCompression.Compressed;
			importer.SaveAndReimport();

			return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		}

		// Imported textures are compressed and unreadable; reimport raw just long enough to copy the pixels.
		private static Color32[] ReadPixels(Texture2D texture)
		{
			string path = AssetDatabase.GetAssetPath(texture);
			var importer = (TextureImporter)AssetImporter.GetAtPath(path);
			bool readable = importer.isReadable;
			TextureImporterCompression compression = importer.textureCompression;

			importer.isReadable = true;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.SaveAndReimport();

			try
			{
				return AssetDatabase.LoadAssetAtPath<Texture2D>(path).GetPixels32();
			}
			finally
			{
				importer.isReadable = readable;
				importer.textureCompression = compression;
				importer.SaveAndReimport();
			}
		}

		// Byte lookup for texel * factor, multiplied in the same space the shader multiplies base map by base color.
		private static byte[] ColorCurve(float factor, bool linear)
		{
			var curve = new byte[256];
			for (int i = 0; i < curve.Length; i++)
			{
				float value = i / 255f;
				value = linear
					? Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(value) * Mathf.GammaToLinearSpace(factor))
					: value * factor;
				curve[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
			}

			return curve;
		}

		// Copies the whole mesh (skin weights, bind poses, blend shapes), moves each submesh's UVs into its cell and
		// joins the submeshes of each group.
		private static Mesh SaveMesh(string path, Mesh source, int[] cellOf, int[] groupOf, int groupCount, int columns,
			int rows)
		{
			Mesh mesh = Object.Instantiate(source);
			mesh.name = source.name + "_Atlas";

			var sourceUVs = new List<Vector2>();
			source.GetUVs(0, sourceUVs);
			var uvs = new List<Vector2>(sourceUVs);
			var scale = new Vector2(1f / columns, 1f / rows);
			var triangles = new List<int>[groupCount];
			for (int i = 0; i < groupCount; i++)
			{
				triangles[i] = new List<int>();
			}

			for (int submesh = 0; submesh < source.subMeshCount; submesh++)
			{
				var offset = new Vector2(cellOf[submesh] % columns * scale.x, cellOf[submesh] / columns * scale.y);
				int[] submeshTriangles = source.GetTriangles(submesh);
				foreach (int index in submeshTriangles)
				{
					uvs[index] = offset + Vector2.Scale(sourceUVs[index], scale);
				}

				triangles[groupOf[submesh]].AddRange(submeshTriangles);
			}

			mesh.SetUVs(0, uvs);
			mesh.subMeshCount = groupCount;
			for (int i = 0; i < groupCount; i++)
			{
				mesh.SetTriangles(triangles[i], i);
			}

			var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
			if (existing == null)
			{
				AssetDatabase.CreateAsset(mesh, path);
			}
			else
			{
				// Overwrite in place so the prefab's reference survives a re-bake.
				EditorUtility.CopySerialized(mesh, existing);
				Object.DestroyImmediate(mesh);
				mesh = existing;
			}

			EditorUtility.SetDirty(mesh);
			AssetDatabase.SaveAssets();
			return mesh;
		}

		private static Material SaveMaterial(string path, Material template, Texture2D atlas)
		{
			var material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material == null)
			{
				material = new Material(template);
				AssetDatabase.CreateAsset(material, path);
			}
			else
			{
				material.CopyPropertiesFromMaterial(template);
			}

			material.SetTexture("_BaseMap", atlas);
			material.SetColor("_BaseColor", Color.white);
			EditorUtility.SetDirty(material);
			AssetDatabase.SaveAssets();
			return material;
		}
	}
}
