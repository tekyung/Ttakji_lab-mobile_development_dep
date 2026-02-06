using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;

namespace Modoium.Service.Editor {
    public static class ModoiumEditorMenu {
        [MenuItem("Help/Modoium Remote/User Guide")]
        static void OpenUserGuide() {
            MDMUrlHandler.Open(MDMUrlHandler.UserGuideURL);
        }

#if MODOIUM_PRIVATE_API && MODOIUM_DEV
        [MenuItem("Tools/Modoium Remote/Pack Modoium Remote package...")]
        static async void PackPackage() {
            var packagePath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../modoium-service-unity-package"));
            if (Directory.Exists(packagePath) == false) {
                Debug.LogError("[ERROR] Failed to find modoium-service-unity-package at " + packagePath);
                return;
            }

            var targetFolder = EditorUtility.OpenFolderPanel("Pack Modoium Remote package into tgz...", "", "");
            if (string.IsNullOrEmpty(targetFolder)) { return; }

            var result = Client.Pack(packagePath, targetFolder);
            while (result.Status == StatusCode.InProgress) {
                EditorUtility.DisplayProgressBar("Packing Modoium Remote package...", "", 1f);
                await Task.Yield();
            }
            EditorUtility.ClearProgressBar();

            if (result.Status == StatusCode.Failure) {
                Debug.LogError("[ERROR] Failed to package modoium-service-unity-package: " + result.Error.message);
                return;
            }

            Debug.Log("[DONE] Packaged modoium-service-unity-package into " + result.Result.tarballPath);
        }
#endif
    }
}
