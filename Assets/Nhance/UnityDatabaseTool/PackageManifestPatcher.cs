using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Nhance.UnityDatabaseTool
{
    [InitializeOnLoad]
    static class PackageManifestPatcher
    {
        private static readonly string[]   UpmPackages = {
            "com.unity.nuget.newtonsoft-json@3.0.2",
            "com.unity.editorcoroutines@1.0"
        };
    
        private const string EmbeddedPackagePath = "Assets/Nhance/UnityDatabaseTool/Core.unitypackage";
    
        private const string SourceFolder = "Assets/Nhance/UnityDatabaseTool/";
        private const string DestFolder   = "Assets/Plugins/";

        private static ListRequest       listRequest;
        private static AddRequest        addRequest;
        private static HashSet<string>   installed;
        private static int               currentUpmIndex;
        private static bool              hasImportedEmbedded;

        static PackageManifestPatcher()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (listRequest == null)
            {
                listRequest = Client.List(true, false);
                return;
            }
            if (!listRequest.IsCompleted)
                return;
        
            if (installed == null)
            {
                installed = new HashSet<string>(listRequest.Result.Select(p => p.name));
                Debug.Log($"[Patcher] Found {installed.Count} UPM-packages.");
            }
        
            if (currentUpmIndex < UpmPackages.Length)
            {
                if (addRequest == null)
                {
                    var fullId   = UpmPackages[currentUpmIndex];
                    var nameOnly = fullId.Split('@')[0];

                    if (installed.Contains(nameOnly))
                    {
                        Debug.Log($"[Patcher] Skipping {nameOnly} — already installed.");
                        currentUpmIndex++;
                        return;
                    }

                    Debug.Log($"[Patcher] Installing UPM-package: {fullId}");
                    addRequest = Client.Add(fullId);
                }
                else if (addRequest.IsCompleted)
                {
                    if (addRequest.Status == StatusCode.Success)
                    {
                        Debug.Log($"[Patcher] Installed: {UpmPackages[currentUpmIndex]}");
                        installed.Add(UpmPackages[currentUpmIndex].Split('@')[0]);
                    }
                    else
                    {
                        Debug.LogError($"[Patcher] Error while installing {UpmPackages[currentUpmIndex]}: {addRequest.Error.message}");
                    }

                    currentUpmIndex++;
                    addRequest = null;
                }
                return;
            }
        
            if (!hasImportedEmbedded)
            {
                if (File.Exists(EmbeddedPackagePath))
                {
                    Debug.Log($"[Patcher] Installing Core: {EmbeddedPackagePath}");
                    AssetDatabase.ImportPackage(EmbeddedPackagePath, false);
                
                    try
                    {
                        File.Delete(EmbeddedPackagePath);
                        Debug.Log($"[Patcher] Deleted Core package file: {EmbeddedPackagePath}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[Patcher] Failed to delete Core package: {e.Message}");
                    }
                
                    try
                    {
                        Directory.CreateDirectory(DestFolder);
                        var files = new[] { "sqlite3.def", "sqlite3.dll" };
                        foreach (var fileName in files)
                        {
                            var src = Path.Combine(SourceFolder, fileName).Replace("\\", "/");
                            var dst = Path.Combine(DestFolder, fileName).Replace("\\", "/");
                            if (AssetDatabase.MoveAsset(src, dst) == "")
                                Debug.Log($"[Patcher] {fileName} moved to {DestFolder}");
                            else
                                Debug.LogWarning($"[Patcher] Failed to move {fileName} (might be missed).");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[Patcher] Error moving plugin files: {e.Message}");
                    }
                
                    Debug.Log($"[Patcher] Initializing restart...");
                
                    var projectPath = System.IO.Path.GetDirectoryName(Application.dataPath);
                    EditorApplication.OpenProject(projectPath);
                
                }
                else
                {
                    Debug.Log($"[Patcher] Core Package doesn't found.");
                }

                hasImportedEmbedded = true;
            }

            if (!hasImportedEmbedded) return;
            EditorApplication.update -= OnEditorUpdate;
        }
    }
}
