using UnityEditor;
using UnityEditor.Toolbars;

namespace SolarHarmony.Config.Editor
{
    public static class GameConfigToolbarButton
    {
        [MainToolbarElement("Solar Harmony/Game Config", defaultDockPosition = MainToolbarDockPosition.Left)]
        private static MainToolbarElement CreateAnalysisWindowsBar()
        {
            var entry = new MainToolbarContent("⚙ Game Config", "Open the game config installer asset");
            return new MainToolbarButton(entry, OpenGameSettings);
        }
        
        private static void OpenGameSettings()
        {
            var guids = AssetDatabase.FindAssets("t:GameConfigInstaller");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var asset = AssetDatabase.LoadAssetAtPath<GameConfigInstaller>(path);
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("No GameConfigInstaller asset found in the project.");
            }
        }
    }
}
