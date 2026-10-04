#if UNITY_EDITOR
using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace PetShop.EditorTools
{
    /// <summary>
    /// Lets a local agent's direct (stdio relay) connection reach the Unity MCP server without the
    /// one-time "Pending Connections → Allow" click in Project Settings → AI → Unity MCP Server.
    ///
    ///   Unity -batchmode -nographics -projectPath . -executeMethod PetShop.EditorTools.McpAccess.AllowDirect -quit
    ///
    /// The settings live in the EditorPrefs string "Unity.AI.MCP.ProjectSettings.v2", a JSON dump of
    /// com.unity.ai.assistant's internal MCPSettings. It is patched in place with a regex rather than
    /// round-tripped through JsonUtility, which would drop every field it does not know about
    /// (clientStates, tool overrides, ...).
    ///
    /// EditorPrefs are machine-wide (~/.config/unity3d/prefs on Linux), not per project, so this
    /// loosens approval for EVERY project on the machine. That is why it is opt-in
    /// (Tools/open-editor.sh --approve-mcp) and never runs as part of a normal build.
    /// </summary>
    public static class McpAccess
    {
        private const string PrefKey         = "Unity.AI.MCP.ProjectSettings.v2";
        private const string LogTag          = "[McpAccess]";
        private const int    FailureExitCode = 1;

        // Minimal settings used ONLY when the pref is absent or empty; gateway matches the package default.
        private const string DefaultSettingsJson =
            "{\"bridgeEnabled\":true,\"batchModeEnabled\":true,\"autoApproveInBatchMode\":true," +
            "\"connectionPolicies\":{" +
            "\"gateway\":{\"allowed\":true,\"requiresApproval\":false}," +
            "\"direct\":{\"allowed\":true,\"requiresApproval\":false}}}";

        // The flat body of the "direct" object inside "connectionPolicies" is group 2.
        private static readonly Regex DirectPolicy = new Regex(
            "(\"connectionPolicies\"\\s*:\\s*\\{.*?\"direct\"\\s*:\\s*\\{)([^{}]*)(\\})",
            RegexOptions.Singleline);
        private static readonly Regex RequiresApproval = new Regex("(\"requiresApproval\"\\s*:\\s*)(true|false)");
        private static readonly Regex Allowed          = new Regex("(\"allowed\"\\s*:\\s*)(true|false)");

        public static void AllowDirect()
        {
            try
            {
                string current = EditorPrefs.GetString(PrefKey, string.Empty);
                if (string.IsNullOrWhiteSpace(current))
                {
                    WriteDefault();
                    return;
                }
                string patched = TryPatch(current);
                if (patched == null)
                {
                    RefuseUnpatchable();
                    return;
                }
                EditorPrefs.SetString(PrefKey, patched);
                Debug.Log($"{LogTag} Patched {PrefKey}: direct connections allowed, no approval required; other settings kept.");
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} Could not update {PrefKey}: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(FailureExitCode);
            }
        }

        /// <summary>The patched JSON, or null when the direct policy's keys cannot be found.</summary>
        private static string TryPatch(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            Match match = DirectPolicy.Match(json);
            if (!match.Success) return null;

            Group  bodyGroup = match.Groups[2];
            string body      = bodyGroup.Value;
            if (!RequiresApproval.IsMatch(body) || !Allowed.IsMatch(body)) return null;

            body = RequiresApproval.Replace(body, "${1}false");
            body = Allowed.Replace(body, "${1}true");
            return json.Substring(0, bodyGroup.Index) + body + json.Substring(bodyGroup.Index + bodyGroup.Length);
        }

        private static void WriteDefault()
        {
            EditorPrefs.SetString(PrefKey, DefaultSettingsJson);
            Debug.Log($"{LogTag} Wrote default {PrefKey} (pref was absent or empty): direct connections allowed, no approval required.");
        }

        /// <summary>The pref exists but has an unexpected shape: never overwrite it, fail loudly instead.</summary>
        private static void RefuseUnpatchable()
        {
            Debug.LogError($"{LogTag} {PrefKey} exists but its connectionPolicies.direct \"requiresApproval\"/\"allowed\" " +
                           "keys were not found; settings were left unchanged. Approve manually: " +
                           "Project Settings > AI > Unity MCP Server > Pending Connections > Allow.");
            if (Application.isBatchMode) EditorApplication.Exit(FailureExitCode);
        }
    }
}
#endif
