using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StixGames.NatureCore.Utility.Localization
{
    public static class LocalizationManager
    {
        public const string MissingTooltipMessage = "No tooltip found";

        private static readonly Dictionary<string, string> localizedText = new Dictionary<string, string>();
        private static readonly HashSet<string> loadedFiles = new HashSet<string>();

        private static readonly HashSet<string> missingItems = new HashSet<string>();

        /// <summary>
        /// Additively load localization. If the file was already loaded, do nothing.
        /// </summary>
        /// <param name="filePath"></param>
        public static void LoadLocalizedText(string filePath)
        {
            if (loadedFiles.Contains(filePath))
            {
                return;
            }

            if (File.Exists(filePath))
            {
                string dataAsJson = File.ReadAllText(filePath);
                LocalizationData loadedData = JsonUtility.FromJson<LocalizationData>(dataAsJson);

                foreach (var item in loadedData.Items)
                {
                    localizedText.Add(item.Key, item.Value);
                }

                loadedFiles.Add(filePath);
            }
            else
            {
                Debug.LogError("Cannot find file! " + filePath);
            }
        }

        public static string GetLocalizedValue(string key)
        {
            if (localizedText.ContainsKey(key))
            {
                return localizedText[key];
            }
            else
            {
                missingItems.Add(key);
                return key;
            }
        }

        public static string GetLocalizedValue(string key, string defaultValue)
        {
            if (localizedText.ContainsKey(key))
            {
                return localizedText[key];
            }
            else
            {
                missingItems.Add(key);
                return defaultValue;
            }
        }

        public static GUIContent GetGUIContent(string key)
        {
            var text = GetLocalizedValue(string.Format("{0}.Text", key), key);
            var tooltip = GetLocalizedValue(string.Format("{0}.Tooltip", key), MissingTooltipMessage);
            return new GUIContent(text, tooltip);
        }

        public static GUIContent GetGUIContent(string key, string defaultText, string defaultTooltip)
        {
            var text = GetLocalizedValue(string.Format("{0}.Text", key), defaultText);
            var tooltip = GetLocalizedValue(string.Format("{0}.Tooltip", key), defaultTooltip);
            return new GUIContent(text, tooltip);
        }

        public static GUIContent[] GetGUIContents(string[] grassTypeLabels)
        {
            return grassTypeLabels.Select(x => GetGUIContent(x, x, MissingTooltipMessage)).ToArray();
        }

        public static string[] RetriveMissing()
        {
            var missingItemsArray = missingItems.ToArray();
            missingItems.Clear();
            return missingItemsArray;
        }

        public static void Reset()
        {
            localizedText.Clear();
            loadedFiles.Clear();
            missingItems.Clear();
        }
    }
}