using HarmonyLib;

namespace CursorSwap.Patches;

// Jewelcrafting sets its own cursor when the scene loads, so ours is set again after it
[HarmonyPatch(typeof(ZNetScene), "Awake")]
internal static class ZNetScene_Awake_Patch
{
	[HarmonyAfter("org.bepinex.plugins.jewelcrafting")]
	private static void Postfix()
	{
		CursorLibrary.Apply();
	}
}
