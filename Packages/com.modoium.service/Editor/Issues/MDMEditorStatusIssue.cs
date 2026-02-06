using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modoium.Service.Editor {
    internal class MDMRemoteIssueServiceUnavailable : MDMRemoteIssue {
        public static string ID = "00000-service-unavailable";

        public static bool IssueExists(MDMService service) {
            return service.availability != MDMServiceAvailability.Unspecified &&
                   service.availability != MDMServiceAvailability.Available;
        }

        private MDMService _service;

        public MDMRemoteIssueServiceUnavailable(MDMService service) {
            _service = service;
        }

        // implements MDMRemoteIssue
        public override string id => ID;
        public override Level level => Level.Error;
        public override string message => $"Service is unavailable: {_service.availabilityDescription}";
        public override (string label, Action action)[] actions => null;
    }

    internal class MDMRemoteIssueRemoteGameViewNotSelected : MDMRemoteIssue {
        public static string ID = "0010-remote-game-view-not-selected";

        private MDMService _service;

        public MDMRemoteIssueRemoteGameViewNotSelected(MDMService service) {
            _service = service;
        }

        public static bool IssueExists(MDMService service) {
            if (service.remoteViewConnected == false ||
                ModoiumPlugin.isXR) { return false; }

            var currentSizeLabel = service.displayConfigurator.currentSizeLabel;
            return string.IsNullOrEmpty(currentSizeLabel) == false &&
                   currentSizeLabel != MDMDisplayConfigurator.RemoteSizeLabel;
        }

        // implememts MDMRemoteIssue
        public override string id => ID;
        public override Level level => Level.Warning;

    #if UNITY_2022_3_OR_NEWER
        public override string message => "<b>Game View</b> follows the connected device screen size only when the size <b>\"Modoium Remote\"</b> is selected.";
    #else
        public override string message => "Game View follows the connected device screen size only when the size \"Modoium Remote\" is selected.";
    #endif

        public override (string label, Action action)[] actions => new (string, Action)[] {
            ("Fix", () => {
                _service.displayConfigurator.SelectRemoteSize();
            })
        };
    }

    internal class MDMRemoteIssueDeviceNotSupportXR : MDMRemoteIssue {
        public static string ID = "0020-device-not-support-xr";

        public static bool IssueExists(MDMService service) {
            if (service.remoteViewConnected == false) { return false; }

            return (service.remoteViewDesc is MDMStereoVideoDesc) == false && ModoiumPlugin.isXR;
        }

        // implements MDMRemoteIssue
        public override string id => ID;
        public override Level level => Level.Warning;
        public override string message => "The connected device does not support XR.";
        public override (string label, Action action)[] actions => null;
    }

    internal class MDMRemoteIssueIncompleteXRSettings : MDMRemoteIssue {
        public static string ID = "0021-incomplete-xr-settings";

        public static bool IssueExists() {
            if (ModoiumPlugin.isXR == false) { return false; }

#if UNITY_XR_MANAGEMENT && UNITY_EDITOR
            var settings = ModoiumPlugin.xrSettings;
            foreach (var loader in settings.Manager?.activeLoaders) {
                if (loader.name == "ModoiumLoader") {
                    return false;
                }
            }
            return true;
#else
            return false;
#endif
        }

        // implements MDMRemoteIssue
        public override string id => ID;
        public override Level level => Level.Warning;
#if UNITY_2022_3_OR_NEWER
        public override string message => "Select the <b>Modoium</b> XR plug-in provider for the Standalone platform. Note that Modoium Remote uses the XR settings of the editor platform.";
#else
        public override string message => "Select the Modoium XR plug-in provider for the Standalone platform. Note that Modoium Remote uses the XR settings of the editor platform.";
#endif
        public override (string label, Action action)[] actions => new (string, Action)[] {
            ("Learn More..", () => {
                MDMUrlHandler.Open(MDMUrlHandler.ProgrammingGuideURL);
            })
        };
    }

    internal class MDMRemoteIssueDeviceNotSupportMono : MDMRemoteIssue {
        public static string ID = "0022-device-not-support-mono";

        public static bool IssueExists(MDMService service) {
            if (service.remoteViewConnected == false) { return false; }

            return (service.remoteViewDesc is MDMMonoVideoDesc) == false && ModoiumPlugin.isXR == false;
        }

        // implements MDMRemoteIssue
        public override string id => ID;
        public override Level level => Level.Warning;
        public override string message => "The connected device supports XR only.";
        public override (string label, Action action)[] actions => null;
    }

    internal class MDMRemoteIssueGameViewUnfocused : MDMRemoteIssue {
        public static string ID = "0030-game-view-unfocused";

        public static bool IssueExists(MDMService service) {
            if (Application.isPlaying == false || 
                service.remoteViewConnected == false ||
                ModoiumPlugin.isXR) { return false; }

            return Application.isFocused == false;
        }

        // implements MDMRemoteIssue
        public override string id => ID;
        public override Level level => Level.Warning;
        public override string message => "Game window is not focused. The touch input may not work if the current input processing is based on Event System.";
        public override (string label, Action action)[] actions => null;
    }

    internal class MDMRemoteIssueNotRunInBackground : MDMRemoteIssue {
        public static string ID = "1010-not-run-in-background";

        public static bool IssueExists() => Application.runInBackground == false;

        // implements MDMRemoteIssue
        public override string id => ID;
        public override Level level => Level.Info;
    #if UNITY_2022_3_OR_NEWER
        public override string message => "<b>Run In Background</b> in Standalone Player Settings is strongly recommended for better experience.";
    #else
        public override string message => "Run In Background in Standalone Player Settings is strongly recommended for better experience.";
    #endif
        public override (string label, Action action)[] actions => null;
    }

    internal class MDMRemoteIssueInputSystemNotEnabled : MDMRemoteIssue {
        public static string ID = "1020-input-system-not-enabled";

        public static bool IssueExists() {
            if (ModoiumPlugin.isXR) { return false; }

#if UNITY_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return false;
#else
            return true;
#endif
        }

        // implements MDMRemoteIssue
        public override string id => ID;
        public override Level level => Level.Info;
#if UNITY_2022_3_OR_NEWER
        public override string message => "Consider using only <b>Input System</b>, or if you use UnityEngine.Input directly you should use <b>Modoium.Service.Input</b> instead.";
#else
        public override string message => "Consider using only Input System, or if you use UnityEngine.Input directly you should use Modoium.Service.Input instead.";
#endif
        public override (string label, Action action)[] actions => new (string, Action)[] {
            ("Learn More..", () => {
                MDMUrlHandler.Open(MDMUrlHandler.UnityEngineInputGuideURL);
            })
        };
    }
}
