using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Surf.EditorTools
{
	/// <summary>
	/// Merges a prefab's flat-colored toon submeshes into one submesh with a single material, so it draws once per
	/// pass instead of once per material. Each submesh's base and shadow colors go into one texel of a palette
	/// texture, and its UVs point at that texel, so the result looks the same. Only works when the materials differ
	/// in nothing but those colors.
	/// </summary>
	public static class PaletteMeshBaker
	{
		private const string ToonShader = "AnimeSurfers/ToonCharacter";
		private const string UseShadowMapKeyword = "_SHADOW_MAP";

		// Per-material colors that go into the palettes; every other property must match.
		private static readonly HashSet<string> PaletteProperties = new HashSet<string>
		{
			"_BaseColor", "_ShadowColor", "_EmissionColor"
		};

		[MenuItem("Tools/Surf/Bake Palette Mesh (Selected Prefabs)")]
		public static void BakeSelected()
		{
			foreach (GameObject prefab in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
			{
				Bake(AssetDatabase.GetAssetPath(prefab));
			}
		}

		[MenuItem("Tools/Surf/Bake Palette Mesh (Selected Prefabs)", true)]
		private static bool CanBakeSelected()
		{
			return Selection.GetFiltered<GameObject>(SelectionMode.Assets).Length > 0;
		}

		public static void Bake(string prefabPath)
		{
			GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

			try
			{
				var renderer = root.GetComponentInChildren<MeshRenderer>();
				var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
				Material[] materials = renderer != null ? renderer.sharedMaterials : null;

				if (filter == null || filter.sharedMesh == null || materials.Length < 2)
				{
					Debug.LogWarning($"{prefabPath}: needs a MeshRenderer with several materials.");
					return;
				}

				if (!CanMerge(materials, out string reason))
				{
					Debug.LogWarning($"{prefabPath}: can't merge materials, {reason}");
					return;
				}

				// Next to the imported models: <models>/Prefabs/X.prefab -> <models>/Textures, <models>/Materials.
				string folder = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
				string models = Path.GetFileName(folder) == "Prefabs"
					? Path.GetDirectoryName(folder).Replace('\\', '/')
					: folder;
				string name = Path.GetFileNameWithoutExtension(prefabPath);

				Texture2D basePalette = SavePalette($"{Subfolder(models, "Textures")}/{name}_BasePalette.png", materials,
					"_BaseColor");
				Texture2D shadowPalette = SavePalette($"{Subfolder(models, "Textures")}/{name}_ShadowPalette.png",
					materials, "_ShadowColor");
				Mesh mesh = SaveMesh($"{models}/{name}_Palette.asset", filter.sharedMesh, materials.Length);
				Material material = SaveMaterial($"{Subfolder(models, "Materials")}/{name}_Palette.mat", materials[0],
					basePalette, shadowPalette);

				filter.sharedMesh = mesh;
				renderer.sharedMaterials = new[] { material };
				PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
				Debug.Log($"{prefabPath}: merged {materials.Length} materials into {material.name}.");
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(root);
			}
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

		private static bool CanMerge(Material[] materials, out string reason)
		{
			Material first = materials[0];
			Shader shader = first.shader;

			foreach (Material material in materials)
			{
				if (material == null || material.shader != shader || shader.name != ToonShader)
				{
					reason = $"all materials must use {ToonShader}.";
					return false;
				}

				if (material.IsKeywordEnabled("_EMISSION"))
				{
					reason = $"{material.name} uses emission.";
					return false;
				}

				for (int i = 0; i < shader.GetPropertyCount(); i++)
				{
					string property = shader.GetPropertyName(i);
					if (PaletteProperties.Contains(property))
					{
						continue;
					}

					bool same = shader.GetPropertyType(i) switch
					{
						ShaderPropertyType.Color => material.GetColor(property) == first.GetColor(property),
						ShaderPropertyType.Vector => material.GetVector(property) == first.GetVector(property),
						ShaderPropertyType.Texture => material.GetTexture(property) == null,
						_ => Mathf.Approximately(material.GetFloat(property), first.GetFloat(property))
					};

					if (!same)
					{
						reason = $"{material.name} differs in {property}.";
						return false;
					}
				}
			}

			reason = null;
			return true;
		}

		private static Texture2D SavePalette(string path, Material[] materials, string property)
		{
			var texture = new Texture2D(materials.Length, 1, TextureFormat.RGBA32, false);
			for (int i = 0; i < materials.Length; i++)
			{
				Color color = materials[i].GetColor(property);
				color.a = 1f;
				texture.SetPixel(i, 0, color);
			}

			File.WriteAllBytes(path, texture.EncodeToPNG());
			Object.DestroyImmediate(texture);
			AssetDatabase.ImportAsset(path);

			var importer = (TextureImporter)AssetImporter.GetAtPath(path);
			importer.textureType = TextureImporterType.Default;
			importer.sRGBTexture = true;
			importer.mipmapEnabled = false;
			importer.filterMode = FilterMode.Point;
			importer.wrapMode = TextureWrapMode.Clamp;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.npotScale = TextureImporterNPOTScale.None;
			importer.SaveAndReimport();

			return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		}

		// Submeshes may share vertices, so every submesh gets its own copy with its palette texel's UV.
		private static Mesh SaveMesh(string path, Mesh source, int paletteSize)
		{
			Vector3[] sourcePositions = source.vertices;
			Vector3[] sourceNormals = source.normals;
			var positions = new List<Vector3>();
			var normals = new List<Vector3>();
			var uvs = new List<Vector2>();
			var triangles = new List<int>();

			for (int submesh = 0; submesh < source.subMeshCount; submesh++)
			{
				var remap = new Dictionary<int, int>();
				var uv = new Vector2((submesh + 0.5f) / paletteSize, 0.5f);

				foreach (int index in source.GetTriangles(submesh))
				{
					if (!remap.TryGetValue(index, out int mapped))
					{
						mapped = positions.Count;
						remap[index] = mapped;
						positions.Add(sourcePositions[index]);
						normals.Add(sourceNormals[index]);
						uvs.Add(uv);
					}

					triangles.Add(mapped);
				}
			}

			var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
			if (mesh == null)
			{
				mesh = new Mesh();
				AssetDatabase.CreateAsset(mesh, path);
			}

			mesh.Clear();
			mesh.name = source.name + "_Palette";
			mesh.SetVertices(positions);
			mesh.SetNormals(normals);
			mesh.SetUVs(0, uvs);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			mesh.UploadMeshData(true);
			EditorUtility.SetDirty(mesh);
			AssetDatabase.SaveAssets();
			return mesh;
		}

		private static Material SaveMaterial(string path, Material template, Texture2D basePalette,
			Texture2D shadowPalette)
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

			material.SetColor("_BaseColor", Color.white);
			material.SetColor("_ShadowColor", Color.white);
			material.SetTexture("_BaseMap", basePalette);
			material.SetTextureScale("_BaseMap", Vector2.one);
			material.SetTextureOffset("_BaseMap", Vector2.zero);
			material.SetTexture("_ShadowMap", shadowPalette);
			material.SetFloat("_UseShadowMap", 1f);
			material.EnableKeyword(UseShadowMapKeyword);
			EditorUtility.SetDirty(material);
			AssetDatabase.SaveAssets();
			return material;
		}
	}
}
