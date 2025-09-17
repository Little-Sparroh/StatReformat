using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

// Namespace matching the game (adjust if needed, e.g., Pigeon).
using Pigeon;

namespace StatReformatMod
{
    [BepInPlugin("com.yourname.statreformat", "StatReformat", "1.0.0")]
    [MycoMod(null, ModFlags.IsClientSide)]
    public class StatReformatPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> EnableStatReformat;
        internal static ConfigEntry<string> StatFormat;
        internal static ConfigEntry<bool> DebugStatLogging;
        internal static ConfigEntry<bool> EnableSafetyPatch;
        internal static new ManualLogSource Logger;

        private Harmony harmony;

        private void Awake()
        {
            Logger = base.Logger;

            EnableStatReformat = Config.Bind("General", "EnableStatReformat", true, "If true, forces all stats to 'Key: Value' format.");
            StatFormat = Config.Bind("General", "StatFormat", "{0}: {1}", "Format string for stats (e.g., '{0}: {1}' for 'Key: Value').");
            DebugStatLogging = Config.Bind("Debug", "EnableStatLogging", true, "Logs raw and reformatted stats for debugging.");
            EnableSafetyPatch = Config.Bind("Debug", "EnableSafetyPatch", false, "Enables ScoutLaserRifle safety (logs/skips nulls; disable if crashing).");

            var harmony = new Harmony("com.yourname.statreformat");
            harmony.PatchAll();

            Logger.LogInfo($"{harmony.Id} loaded!");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }

        public static void ReformatStats(Upgrade __instance, ref string text)
        {
            if (!EnableStatReformat.Value || string.IsNullOrEmpty(text))
                return;

            if (DebugStatLogging.Value)
                Logger.LogDebug($"ReformatStats raw for {__instance.APIName}: {text}");

            try
            {
                string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                    {
                        sb.AppendLine();
                        continue;
                    }

                    string tagStripped = Regex.Replace(trimmed, @"<[^>]*>", "");
                    if (Regex.IsMatch(tagStripped, @"^[-+]?\d"))
                    {
                        var match = Regex.Match(tagStripped, @"^([-+]?\d+(?:\.\d+)?[%s]?)\s*(.+)$");
                        if (match.Success)
                        {
                            string value = match.Groups[1].Value;
                            string key = match.Groups[2].Value.Trim();
                            if (!string.IsNullOrEmpty(key))
                            {
                                string formatted = $"{key}: <b>{value}</b>";
                                sb.AppendLine(formatted);
                                continue;
                            }
                        }
                    }
                    sb.AppendLine(line);
                }

                text = sb.ToString().TrimEnd();

                if (DebugStatLogging.Value)
                    Logger.LogDebug($"ReformatStats after for {__instance.APIName}: {text}");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ReformatStats: Failed for {__instance.APIName}: {ex.Message}");
            }
        }

        public static void ReformatUIText(TextMeshProUGUI textComponent, string fieldName = "text")
        {
            if (!EnableStatReformat.Value || textComponent == null)
                return;

            string currentText = textComponent.text;
            if (string.IsNullOrEmpty(currentText))
                return;

            if (DebugStatLogging.Value)
                Logger.LogDebug($"ReformatUIText raw {fieldName}: {currentText}");

            try
            {
                string[] lines = currentText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                    {
                        sb.AppendLine();
                        continue;
                    }

                    string tagStripped = Regex.Replace(trimmed, @"<[^>]*>", "");
                    if (Regex.IsMatch(tagStripped, @"^[-+]?\d"))
                    {
                        var match = Regex.Match(tagStripped, @"^([-+]?\d+(?:\.\d+)?[%s]?)\s*(.+)$");
                        if (match.Success)
                        {
                            string value = match.Groups[1].Value;
                            string key = match.Groups[2].Value.Trim();
                            if (!string.IsNullOrEmpty(key))
                            {
                                string formatted = $"{key}: <b>{value}</b>";
                                sb.AppendLine(formatted);
                                continue;
                            }
                        }
                    }
                    sb.AppendLine(line);
                }

                string reformattedText = sb.ToString().TrimEnd();
                textComponent.text = reformattedText;
                textComponent.ForceMeshUpdate();

                if (DebugStatLogging.Value)
                    Logger.LogDebug($"ReformatUIText after {fieldName}: {reformattedText}");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ReformatUIText: Failed for {fieldName}: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Upgrade), nameof(Upgrade.GetStatList))]
    public static class StatListReformatPatch
    {
        static void Postfix(Upgrade __instance, int seed, ref string __result)
        {
            StatReformatPlugin.Logger.LogInfo($"GetStatList called for {__instance.APIName}, raw result length: {__result?.Length ?? 0}");
            StatReformatPlugin.ReformatStats(__instance, ref __result);
        }
    }

    [HarmonyPatch(typeof(Upgrade), nameof(Upgrade.ModifyDisplayedProperties))]
    public static class ModifyPropertiesReformatPatch
    {
        static void Postfix(Upgrade __instance, ref string properties, UpgradeInstance instance)
        {
            StatReformatPlugin.Logger.LogInfo($"ModifyDisplayedProperties called, raw properties length: {properties?.Length ?? 0}");
            // No ReformatStats here to avoid double-processing.
        }
    }

    [HarmonyPatch(typeof(HoverInfoDisplay), "Activate")]
    public static class HoverInfoDisplayReformatPatch
    {
        static void Postfix(HoverInfoDisplay __instance, HoverInfo info, bool resetPosition)
        {
            if (!StatReformatPlugin.EnableStatReformat.Value || info == null)
                return;

            StatReformatPlugin.Logger.LogInfo("Activate postfix fired!");
            try
            {
                FieldInfo textField = AccessTools.Field(typeof(HoverInfoDisplay), "text");
                if (textField != null)
                {
                    TextMeshProUGUI textComponent = (TextMeshProUGUI)textField.GetValue(__instance);
                    StatReformatPlugin.ReformatUIText(textComponent, "main text");
                }

                FieldInfo statsField = AccessTools.Field(typeof(HoverInfoDisplay), "statsText");
                if (statsField != null)
                {
                    TextMeshProUGUI statsComponent = (TextMeshProUGUI)statsField.GetValue(__instance);
                    StatReformatPlugin.ReformatUIText(statsComponent, "statsText");
                }
            }
            catch (Exception ex)
            {
                StatReformatPlugin.Logger.LogWarning($"HoverInfoDisplayReformatPatch: Failed: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(HoverInfoDisplay), "Refresh")]
    public static class HoverInfoDisplayRefreshReformatPatch
    {
        static void Postfix(HoverInfoDisplay __instance)
        {
            StatReformatPlugin.Logger.LogInfo("Refresh postfix fired!");
            FieldInfo selectedField = AccessTools.Field(typeof(HoverInfoDisplay), "selectedInfo");
            if (selectedField == null)
                return;
            var selected = selectedField.GetValue(__instance) as HoverInfo;
            if (!StatReformatPlugin.EnableStatReformat.Value || selected == null)
                return;

            try
            {
                FieldInfo textField = AccessTools.Field(typeof(HoverInfoDisplay), "text");
                if (textField != null)
                {
                    TextMeshProUGUI textComponent = (TextMeshProUGUI)textField.GetValue(__instance);
                    StatReformatPlugin.ReformatUIText(textComponent, "main text (refresh)");
                }

                FieldInfo statsField = AccessTools.Field(typeof(HoverInfoDisplay), "statsText");
                if (statsField != null)
                {
                    TextMeshProUGUI statsComponent = (TextMeshProUGUI)statsField.GetValue(__instance);
                    StatReformatPlugin.ReformatUIText(statsComponent, "statsText (refresh)");
                }
            }
            catch (Exception ex)
            {
                StatReformatPlugin.Logger.LogWarning($"HoverInfoDisplayRefreshReformatPatch: Failed: {ex.Message}");
            }
        }
    }
}