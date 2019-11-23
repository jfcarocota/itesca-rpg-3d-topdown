using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StixGames.NatureCore.Utility
{
    /// <summary>
    /// This class is used to find a folder, even if that folder was moved.
    /// </summary>
    //[CreateAssetMenu(menuName = "File Anchor")]
    public class FileAnchor : ScriptableObject
    {
        public static Dictionary<string, string> PathCache = new Dictionary<string, string>();

        public static string GetFilePath(string anchorName, string filePath)
        {
            var anchorPath = Path.GetDirectoryName(GetPath(anchorName));

            return Path.Combine(anchorPath, filePath);
        }

        public static string GetPath(string name)
        {
            //Check the cache, if the object has never been searched for
            //or the file doesn't exist any more, search the asset database.
            if (PathCache.ContainsKey(name))
            {
                var path = PathCache[name];

                if (File.Exists(path))
                {
                    return path;
                }
            }

            var assets = AssetDatabase.FindAssets(name);

            if (assets.Length == 0)
            {
                throw new ArgumentException(string.Format("The file anchor {0} could not be found.", name));
            }

            if (assets.Length > 1)
            {
                throw new ArgumentException(string.Format("The file anchor {0} has been found multiple times.", name));
            }

            var newPath = AssetDatabase.GUIDToAssetPath(assets.Single());
            PathCache[name] = newPath;

            return newPath;
        }
    }
}
