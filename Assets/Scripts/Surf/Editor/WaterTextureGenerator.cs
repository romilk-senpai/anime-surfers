using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Surf.EditorTools
{
	/// <summary>
	/// Generates the tileable textures used by the water shader and the spray particles.
	/// </summary>
	public static class WaterTextureGenerator
	{
		private const string Folder = "Assets/Textures/Surf";

		[MenuItem("Tools/Surf/Generate Water Textures")]
		public static void GenerateAll()
		{
			if (!AssetDatabase.IsValidFolder("Assets/Textures"))
			{
				AssetDatabase.CreateFolder("Assets", "Textures");
			}

			if (!AssetDatabase.IsValidFolder(Folder))
			{
				AssetDatabase.CreateFolder("Assets/Textures", "Surf");
			}

			GenerateNormalMap(Folder + "/WaterNormal.png", 512, 4, 5, 3f);
			GenerateFoam(Folder + "/WaterFoam.png", 512, 12);
			GenerateSprayStreak(Folder + "/SprayStreak.png", 64);
			GenerateMist(Folder + "/SprayMist.png", 128);
		}

		// Inverted billow noise gives sharp little crests between rounded troughs, like wind chop.
		private static void GenerateNormalMap(string path, int size, int basePeriod, int octaves, float strength)
		{
			var heights = new float[size * size];

			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float h = 0f;
					float amplitude = 1f;
					int period = basePeriod;

					for (int o = 0; o < octaves; o++)
					{
						float n = PeriodicPerlin(x * period / (float)size, y * period / (float)size, period, o * 31);
						float ridge = 1f - Mathf.Abs(n);
						h += ridge * ridge * amplitude;
						amplitude *= 0.5f;
						period *= 2;
					}

					heights[y * size + x] = h;
				}
			}

			var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float dx = heights[y * size + (x + 1) % size] - heights[y * size + (x - 1 + size) % size];
					float dy = heights[((y + 1) % size) * size + x] - heights[((y - 1 + size) % size) * size + x];
					Vector3 n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
					texture.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
				}
			}

			Save(texture, path, importer =>
			{
				importer.textureType = TextureImporterType.NormalMap;
				importer.wrapMode = TextureWrapMode.Repeat;
			});
		}

		// Cellular (Worley) edges make bubbly foam lines; a low-frequency noise breaks them into patches.
		private static void GenerateFoam(string path, int size, int cells)
		{
			var points = new Vector2[cells * cells];
			var random = new System.Random(7);
			for (int i = 0; i < points.Length; i++)
			{
				points[i] = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
			}

			var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float fx = x * cells / (float)size;
					float fy = y * cells / (float)size;
					Worley(fx, fy, cells, points, out float f1, out float f2);
					Worley(fx * 2f, fy * 2f, cells, points, out float g1, out float g2);

					float edges = Mathf.Clamp01(1f - (f2 - f1) * 3.5f);
					float fineEdges = Mathf.Clamp01(1f - (g2 - g1) * 3f);
					float patches = PeriodicPerlin(x * 4f / size, y * 4f / size, 4, 99) * 0.5f + 0.5f;
					float foam = Mathf.Clamp01(edges * 0.7f + fineEdges * 0.45f) * Mathf.Lerp(0.55f, 1f, patches);
					texture.SetPixel(x, y, new Color(foam, foam, foam, 1f));
				}
			}

			Save(texture, path, importer =>
			{
				importer.sRGBTexture = false;
				importer.wrapMode = TextureWrapMode.Repeat;
			});
		}

		// Soft elongated droplet, drawn along velocity by stretched-billboard particles.
		private static void GenerateSprayStreak(string path, int size)
		{
			var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float u = (x + 0.5f) / size * 2f - 1f;
					float v = (y + 0.5f) / size * 2f - 1f;
					float d = Mathf.Sqrt(u * u * 2.5f + v * v);
					float alpha = Mathf.Clamp01(1f - d);
					alpha = alpha * alpha * (3f - 2f * alpha);
					texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
				}
			}

			Save(texture, path, importer =>
			{
				importer.alphaIsTransparency = true;
				importer.wrapMode = TextureWrapMode.Clamp;
			});
		}

		// Cloudy puff for the mist that hangs behind the spray.
		private static void GenerateMist(string path, int size)
		{
			var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float u = (x + 0.5f) / size * 2f - 1f;
					float v = (y + 0.5f) / size * 2f - 1f;
					float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
					float noise = 0f;
					float amplitude = 0.5f;
					int period = 4;
					for (int o = 0; o < 4; o++)
					{
						noise += (PeriodicPerlin(x * period / (float)size, y * period / (float)size, period, 50 + o) * 0.5f + 0.5f) * amplitude;
						amplitude *= 0.5f;
						period *= 2;
					}

					float alpha = Mathf.Clamp01(falloff * falloff * (0.4f + noise * 1.2f));
					texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
				}
			}

			Save(texture, path, importer =>
			{
				importer.alphaIsTransparency = true;
				importer.wrapMode = TextureWrapMode.Clamp;
			});
		}

		private static void Save(Texture2D texture, string path, System.Action<TextureImporter> configure)
		{
			texture.Apply();
			File.WriteAllBytes(path, texture.EncodeToPNG());
			Object.DestroyImmediate(texture);

			AssetDatabase.ImportAsset(path);
			var importer = (TextureImporter)AssetImporter.GetAtPath(path);
			configure(importer);
			importer.mipmapEnabled = true;
			importer.SaveAndReimport();
		}

		private static void Worley(float x, float y, int cells, Vector2[] points, out float f1, out float f2)
		{
			int cx = Mathf.FloorToInt(x);
			int cy = Mathf.FloorToInt(y);
			f1 = float.MaxValue;
			f2 = float.MaxValue;

			for (int oy = -1; oy <= 1; oy++)
			{
				for (int ox = -1; ox <= 1; ox++)
				{
					int nx = cx + ox;
					int ny = cy + oy;
					int wx = ((nx % cells) + cells) % cells;
					int wy = ((ny % cells) + cells) % cells;
					Vector2 p = points[wy * cells + wx] + new Vector2(nx, ny);
					float d = Vector2.Distance(p, new Vector2(x, y));

					if (d < f1)
					{
						f2 = f1;
						f1 = d;
					}
					else if (d < f2)
					{
						f2 = d;
					}
				}
			}
		}

		// Perlin gradient noise that tiles every `period` units.
		private static float PeriodicPerlin(float x, float y, int period, int seed)
		{
			int x0 = Mathf.FloorToInt(x);
			int y0 = Mathf.FloorToInt(y);
			float fx = x - x0;
			float fy = y - y0;
			float u = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
			float v = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);

			float n00 = Gradient(x0, y0, period, seed, fx, fy);
			float n10 = Gradient(x0 + 1, y0, period, seed, fx - 1f, fy);
			float n01 = Gradient(x0, y0 + 1, period, seed, fx, fy - 1f);
			float n11 = Gradient(x0 + 1, y0 + 1, period, seed, fx - 1f, fy - 1f);

			return Mathf.Lerp(Mathf.Lerp(n00, n10, u), Mathf.Lerp(n01, n11, u), v) * 1.4f;
		}

		private static float Gradient(int ix, int iy, int period, int seed, float dx, float dy)
		{
			ix = ((ix % period) + period) % period;
			iy = ((iy % period) + period) % period;
			uint h = (uint)(ix * 374761393 + iy * 668265263 + seed * 1442695041);
			h = (h ^ (h >> 13)) * 1274126177u;
			h ^= h >> 16;
			float angle = (h & 0xffff) / 65535f * Mathf.PI * 2f;
			return Mathf.Cos(angle) * dx + Mathf.Sin(angle) * dy;
		}
	}
}
