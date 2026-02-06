using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Modoium.Service.Editor {
    public static class ModoiumGameObjectMenu {
        private const int ProirityStart = 10;

#if MODOIUM_PRIVATE_API

        [MenuItem("GameObject/Modoium/Volume", false, ProirityStart + 1)]
        static void GameObjectVolume(MenuCommand menuCommand) {
            instantiateFromPrefab("Prefabs/Volume.prefab", "Volume", menuCommand);
        }

        private static void instantiateFromPrefab(string prefabPath, string name, MenuCommand menuCommand) {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(makeAssetPath(prefabPath));
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.name = name;

            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            GameObjectUtility.SetParentAndAlign(instance, menuCommand.context as GameObject);

            Undo.RegisterCreatedObjectUndo(instance, "create " + instance.name);

            Selection.activeObject = instance;
        }

        private static string makeAssetPath(string assetPath) => Path.Combine("Packages/com.modoium.service", assetPath);
#endif
    }
}
