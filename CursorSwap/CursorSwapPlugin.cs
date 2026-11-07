using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;

namespace CursorSwap;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.HardDependency)]
[NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
public class CursorSwapPlugin : BaseUnityPlugin
{
	public const string PluginName = "CursorSwap";
	public const string PluginVersion = "1.0.0";
	public const string PluginAuthor = "MagicMike";
	internal const string PluginGUID = "MagicMike.CursorSwap";
	internal const string PluginCopy = "Copyright © MagicMike 2026";
	public const string PluginLink = "https://discord.gg/mnr36G6J4x";

	public static CursorSwapPlugin plugininstance;

	internal static ConfigFile ConfigFile;
	internal static ConfigFileWatcher ConfigFileWatcher;

	// How often, in seconds, the cursor is set again in case another mod changed it
	private const float ReapplyInterval = 1f;

	private Harmony harmony;

	private float reapplyTimer;

	public void Awake()
	{
		plugininstance = this;
		ConfigFile = Config;
		ConfigFileWatcher = new ConfigFileWatcher(Config, 1000L);
		Config.SaveOnConfigSet = false;

		// The cursor list has to be known before the Cursor setting is bound, since it is the setting's list of choices
		CursorLibrary.Discover();
		MainConfig.InitConfigs(Config);

		MagicMike.ModSettings.ModSettings.Init(this);

		UpdatePlugin();

		if (MainConfig.modEnabled.Value)
		{
			harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGUID);

			ConfigFileWatcher.OnConfigFileReloaded += delegate
			{
				UpdatePlugin(saveConfig: false);
			};
			SynchronizationManager.OnConfigurationWindowClosed += delegate
			{
				UpdatePlugin();
			};
			Config.SettingChanged += delegate
			{
				CursorLibrary.Apply();
			};
		}
	}

	private void Update()
	{
		reapplyTimer += UnityEngine.Time.unscaledDeltaTime;
		if (reapplyTimer >= ReapplyInterval)
		{
			reapplyTimer = 0f;
			CursorLibrary.Reapply();
		}
	}

	private void OnDestroy()
	{
		harmony?.UnpatchSelf();
	}

	private void UpdatePlugin(bool saveConfig = true)
	{
		if (saveConfig)
		{
			base.Config.Save();
		}
		if (MainConfig.modEnabled.Value)
		{
			CursorLibrary.Apply();
		}
	}
}
