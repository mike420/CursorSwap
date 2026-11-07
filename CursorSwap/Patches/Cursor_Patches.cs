using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CursorSwap.Patches;

// While a cursor from this mod is set, other mods (and the game) are kept from changing it.
// The Vanilla cursor is left alone, so other cursor mods still work when it is chosen
[HarmonyPatch]
internal static class Cursor_SetCursor_Patch
{
	// The methods already logged as blocked, so each is only logged once
	private static readonly HashSet<string> s_logged = new HashSet<string>();

	private static IEnumerable<MethodBase> TargetMethods()
	{
		yield return AccessTools.Method(typeof(Cursor), nameof(Cursor.SetCursor), new[] { typeof(Texture2D), typeof(Vector2), typeof(CursorMode) });
		yield return AccessTools.Method(typeof(Cursor), nameof(Cursor.SetCursor), new[] { typeof(Texture2D), typeof(CursorMode) });
	}

	[HarmonyPriority(Priority.First)]
	private static bool Prefix()
	{
		if (CursorLibrary.Setting || !CursorLibrary.Guarding)
		{
			return true;
		}
		LogBlocked();
		return false;
	}

	private static void LogBlocked()
	{
		string caller = "unknown";
		foreach (StackFrame frame in new StackTrace().GetFrames() ?? new StackFrame[0])
		{
			MethodBase method = frame.GetMethod();
			if (method?.DeclaringType == null || method.DeclaringType == typeof(Cursor_SetCursor_Patch) || method.DeclaringType == typeof(Cursor))
			{
				continue;
			}
			caller = method.DeclaringType.FullName + "." + method.Name + " (" + method.DeclaringType.Assembly.GetName().Name + ")";
			break;
		}
		if (s_logged.Add(caller))
		{
			Jotunn.Logger.LogInfo("Kept " + caller + " from changing the cursor");
		}
	}
}
