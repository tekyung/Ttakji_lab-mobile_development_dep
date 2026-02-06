using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Modoium.Service.Editor {
    [InitializeOnLoad]
    internal class MDMEditorService : MDMService.IApplication {
        private static MDMEditorService _instance;
        private static bool _started;
        private static bool _playRequested;

        public static MDMService service => _instance?._service;
        public static MDMEditorStatusMonitor statusMonitor => _instance?._statusMonitor;

        static MDMEditorService() {
            EditorApplication.update += EditorUpdate;
            EditorApplication.playModeStateChanged += EditorPlayModeStateChanged;
            EditorApplication.quitting += EditorOnDestroy;

            _instance = new MDMEditorService();
        }

        static void EditorUpdate() {
            if (_started == false) {
                _instance.Start();
                _started = true;
            }

            _instance.Update();

            if (_playRequested) {
                _instance.play();
                _playRequested = false;
            }
        }

        static void EditorPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.EnteredPlayMode) {
                _playRequested = true;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode) {
                _instance.stop();
            }
        }

        static void EditorOnDestroy() {
            _instance.OnDestroy();
        }

        private MDMService _service;
        private MDMEditorStatusMonitor _statusMonitor;

        private MDMEditorService() {
            _service = new MDMService(this);
            _statusMonitor = new MDMEditorStatusMonitor(_service);
        }

        private async void Start() {
            if (await _service.Startup((message) => {
                EditorUtility.DisplayDialog("Failed to init Modoium Remote.", message, "OK");
            })) {
                MDMRemoteStatusWindow.OpenWindow();
            }
        }

        private void Update() {
            _service.Update();
            _statusMonitor.Update();
        }

        private void OnDestroy() {
            _service.Shutdown();
        }

        private void play() {
            _service.Play();
        }

        private void stop() {
            _service.Stop();
        }

        // implements MDMService.IApplication
        bool MDMService.IApplication.useEmbeddedCore => true;
        string MDMService.IApplication.verificationCodeForEmbeddedCore => null;

        bool MDMService.IApplication.isMobilePlatform => 
                EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ||
                EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;

        ScreenOrientation MDMService.IApplication.orientation => PlayerSettings.defaultInterfaceOrientation switch {
            UIOrientation.LandscapeLeft => ScreenOrientation.LandscapeLeft,
            UIOrientation.LandscapeRight => ScreenOrientation.LandscapeRight,
            UIOrientation.Portrait => ScreenOrientation.Portrait,
            UIOrientation.PortraitUpsideDown => ScreenOrientation.PortraitUpsideDown,
            _ => ScreenOrientation.AutoRotation
        };

        bool MDMService.IApplication.isPlaying => Application.isPlaying;
    }
}
