using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modoium.Service.Editor {
    internal static class MDMUrlHandler {
        public static string AndroidAppURL = "https://play.google.com/store/apps/details?id=com.modoium.client.app";
        public static string iOSAppURL = "https://apps.apple.com/app/id6587558465";
        public static string UserGuideURL = "https://modoium.com/docs/introduction/";
        public static string ProgrammingGuideURL = "https://modoium.com/docs/programming-guide/";
        public static string UnityEngineInputGuideURL = "https://modoium.com/docs/programming-guide/#unityengine-input";
        public static string TroubleshootingURL = "https://modoium.com/docs/troubleshooting/";

        public static void Open(string url) {
            if (string.IsNullOrEmpty(url)) { return; }

            System.Diagnostics.Process.Start(url);
        }
    }
}
