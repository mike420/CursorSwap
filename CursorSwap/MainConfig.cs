using BepInEx.Configuration;
using Jotunn.Extensions;

namespace CursorSwap;

internal static class MainConfig
{
	public static ConfigEntry<bool> modEnabled;

	// Cursor
	public static ConfigEntry<string> cursor;
	public static ConfigEntry<int> cursorSize;

	public static void InitConfigs(ConfigFile config)
	{
		modEnabled = config.BindConfigInOrder("General Settings", "Enable Plugin", defaultValue: true, "Enable plugin.\n(Restart required. It is read before connecting to a server, so it is a local setting).\n", synced: false, sectionOrder: true, settingOrder: true);

		cursor = config.BindConfigInOrder("Cursor", "Cursor", defaultValue: CursorLibrary.DefaultName, "The mouse cursor to use. Vanilla is the game's own cursor.\nTo add your own, put PNG images in BepInEx/config/" + CursorSwapPlugin.PluginName + " and restart the game: each one is added to this list by its file name.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueList<string>(CursorLibrary.Names));
		cursorSize = config.BindConfigInOrder("Cursor", "Cursor Size", defaultValue: 48, "How big the cursor is, in pixels. Does not change the Vanilla cursor.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<int>(16, 128));
	}
}
