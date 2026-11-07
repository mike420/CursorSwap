using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace CursorSwap;

// The cursors to choose from: the ones built into the mod, then any PNG images in BepInEx/config/CursorSwap.
// The chosen one is loaded, scaled to the Cursor Size setting and set as the mouse cursor, with its click point
// at its tip
internal static class CursorLibrary
{
	internal const string VanillaName = "Vanilla";

	private const string ResourcePrefix = "CursorSwap.Cursors.";

	// How opaque a pixel has to be to count as the cursor's tip
	private const float TipAlpha = 0.5f;

	// Each cursor's name, and where its image comes from, in the order they are listed
	private static readonly Dictionary<string, Func<byte[]>> s_sources = new Dictionary<string, Func<byte[]>>();

	private static Texture2D s_vanilla;

	private static bool s_vanillaLooked;

	private static Texture2D s_texture;

	private static Vector2 s_hotspot;

	// The cursor name and size s_texture was made for
	private static string s_textureKey;

	// True while a cursor from this mod is set, so other mods are kept from changing it
	internal static bool Guarding { get; private set; }

	// True while this mod is setting the cursor, so the guard lets the call through
	internal static bool Setting { get; private set; }

	internal static string[] Names { get; private set; } = { VanillaName };

	internal static string DefaultName => Names.Length > 1 ? Names[1] : VanillaName;

	internal static string CustomFolder => Path.Combine(Paths.ConfigPath, CursorSwapPlugin.PluginName);

	internal static void Discover()
	{
		s_sources.Clear();
		List<string> names = new List<string> { VanillaName };

		Assembly assembly = typeof(CursorLibrary).Assembly;
		foreach (string resource in assembly.GetManifestResourceNames().Where(r => r.StartsWith(ResourcePrefix) && r.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).OrderBy(r => r))
		{
			string name = Path.GetFileNameWithoutExtension(resource.Substring(ResourcePrefix.Length));
			s_sources[name] = () => ReadResource(assembly, resource);
			names.Add(name);
		}

		try
		{
			Directory.CreateDirectory(CustomFolder);
			foreach (string file in Directory.GetFiles(CustomFolder, "*.png").OrderBy(f => f))
			{
				string name = Path.GetFileNameWithoutExtension(file);
				if (name == VanillaName || s_sources.ContainsKey(name))
				{
					name += " (custom)";
				}
				if (s_sources.ContainsKey(name))
				{
					continue;
				}
				s_sources[name] = () => File.ReadAllBytes(file);
				names.Add(name);
			}
		}
		catch (Exception e)
		{
			Jotunn.Logger.LogWarning("Could not read custom cursors from " + CustomFolder + ": " + e.Message);
		}

		Names = names.ToArray();
	}

	internal static void Apply()
	{
		if (MainConfig.cursor == null || !MainConfig.modEnabled.Value)
		{
			return;
		}
		string name = MainConfig.cursor.Value;
		if (name == VanillaName || !s_sources.TryGetValue(name, out Func<byte[]> source))
		{
			SetVanilla();
			return;
		}

		int size = MainConfig.cursorSize.Value;
		string key = name + "|" + size;
		if (s_texture == null || s_textureKey != key)
		{
			Texture2D texture = Load(name, source, size);
			if (texture == null)
			{
				SetVanilla();
				return;
			}
			if (s_texture != null)
			{
				UnityEngine.Object.Destroy(s_texture);
			}
			s_texture = texture;
			s_textureKey = key;
			s_hotspot = FindTip(texture);
		}
		SetCursor(s_texture, s_hotspot);
		Guarding = true;
	}

	// Sets the chosen cursor again, in case another mod changed it some way the guard could not catch
	internal static void Reapply()
	{
		if (Guarding && s_texture != null)
		{
			SetCursor(s_texture, s_hotspot);
		}
	}

	private static void SetCursor(Texture2D texture, Vector2 hotspot)
	{
		Setting = true;
		try
		{
			Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
		}
		finally
		{
			Setting = false;
		}
	}

	private static void SetVanilla()
	{
		if (!s_vanillaLooked)
		{
			s_vanillaLooked = true;
			s_vanilla = Resources.FindObjectsOfTypeAll<Texture2D>().FirstOrDefault(t => t.name == "cursor" && t.isReadable);
		}
		// With no texture the game's default cursor is used. Other mods may change the Vanilla cursor
		Guarding = false;
		SetCursor(s_vanilla, Vector2.zero);
	}

	private static Texture2D Load(string name, Func<byte[]> source, int size)
	{
		Texture2D image = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
		try
		{
			if (!image.LoadImage(source()))
			{
				throw new Exception("not a PNG image");
			}
		}
		catch (Exception e)
		{
			Jotunn.Logger.LogWarning("Could not load the cursor " + name + ": " + e.Message);
			UnityEngine.Object.Destroy(image);
			return null;
		}
		Texture2D scaled = Scale(image, size);
		if (scaled != image)
		{
			UnityEngine.Object.Destroy(image);
		}
		return scaled;
	}

	// Scales the image so its longer side is size pixels, averaging over each new pixel so it stays smooth.
	// Colours are weighted by alpha so the see-through pixels around the edge do not darken it
	private static Texture2D Scale(Texture2D image, int size)
	{
		int width = image.width, height = image.height;
		float scale = (float)size / Mathf.Max(width, height);
		int newWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale));
		int newHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));
		if (newWidth == width && newHeight == height)
		{
			return image;
		}

		Color[] source = image.GetPixels();
		Color[] result = new Color[newWidth * newHeight];
		float stepX = (float)width / newWidth, stepY = (float)height / newHeight;
		for (int y = 0; y < newHeight; y++)
		{
			int y0 = Mathf.FloorToInt(y * stepY), y1 = Mathf.Max(y0 + 1, Mathf.CeilToInt((y + 1) * stepY));
			for (int x = 0; x < newWidth; x++)
			{
				int x0 = Mathf.FloorToInt(x * stepX), x1 = Mathf.Max(x0 + 1, Mathf.CeilToInt((x + 1) * stepX));
				float r = 0f, g = 0f, b = 0f, a = 0f;
				int count = 0;
				for (int sy = y0; sy < y1 && sy < height; sy++)
				{
					for (int sx = x0; sx < x1 && sx < width; sx++)
					{
						Color c = source[sy * width + sx];
						r += c.r * c.a;
						g += c.g * c.a;
						b += c.b * c.a;
						a += c.a;
						count++;
					}
				}
				result[y * newWidth + x] = a > 0f ? new Color(r / a, g / a, b / a, a / count) : Color.clear;
			}
		}

		Texture2D scaled = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, mipChain: false);
		scaled.SetPixels(result);
		scaled.Apply();
		return scaled;
	}

	// The click point: the first solid pixel from the top, which is the fingertip or arrow point of a cursor
	// that points up. Measured from the top left, as the cursor's hotspot is
	private static Vector2 FindTip(Texture2D texture)
	{
		Color[] pixels = texture.GetPixels();
		int width = texture.width, height = texture.height;
		for (int y = height - 1; y >= 0; y--)
		{
			for (int x = 0; x < width; x++)
			{
				if (pixels[y * width + x].a >= TipAlpha)
				{
					return new Vector2(x, height - 1 - y);
				}
			}
		}
		return Vector2.zero;
	}

	private static byte[] ReadResource(Assembly assembly, string resource)
	{
		using Stream stream = assembly.GetManifestResourceStream(resource);
		using MemoryStream memory = new MemoryStream();
		stream.CopyTo(memory);
		return memory.ToArray();
	}
}
