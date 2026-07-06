using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace Sense;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Sense : BaseUnityPlugin
{
    private const string PluginGuid = "headclef.Sense";
    private const string PluginName = "Sense";
    private const string PluginVersion = "1.0.3";

    internal static Sense Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;

    // ── Config ──
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<float> Radius = null!;
    internal static ConfigEntry<string> MarkerColor = null!;

    private void Awake()
    {
        Instance = this;
        this.gameObject.transform.parent = null;
        this.gameObject.hideFlags = HideFlags.HideAndDontSave;

        BindConfiguration();

        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }

    // The radar is passive — no input, no patches. We drive it from the plugin's own Update so we
    // never touch game behaviour; we only read enemy positions and add local map markers.
    private void Update()
    {
        EnemyRadar.Tick();
    }

    private void OnDestroy()
    {
        EnemyRadar.ClearAll();
    }

    private void BindConfiguration()
    {
        const string section = "Sense";

        Enabled = Config.Bind(section, "Enabled", true,
            "Master switch. While on, enemies within range are continuously marked on the map.");

        Radius = Config.Bind(section, "Radius", 30f,
            new ConfigDescription(
                "Radius in meters around you within which enemies are marked on the map.",
                new AcceptableValueRange<float>(5f, 100f)));

        MarkerColor = Config.Bind(section, "Marker Color", "FF0000",
            "Hex RGB colour of the enemy map markers (e.g. FF0000 = red, FF8800 = orange).");
    }
}
