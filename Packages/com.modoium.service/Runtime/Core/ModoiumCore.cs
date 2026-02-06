using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Modoium.Service {
    internal class ModoiumCore {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private const string LibName = "modoiumCore";
#else
        private const string LibName = "modoium-core";
#endif

        [DllImport(LibName)] private static extern int ModoiumPort();
        [DllImport(LibName)] private static extern int ModoiumStartUpAsEmbedded(string hostname, string verificationCode, string platformName, string platformVersion);
        [DllImport(LibName)] private static extern void ModoiumShutDown();

        public int port => ModoiumPort();
        public bool running => port > 0;

        public void Startup(string verificationCode) {
            if (running) { return; }

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            var platformName = "macOS";
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            var platformName = "Windows";
#else
            var platformName = "";
#endif

            var p = ModoiumStartUpAsEmbedded("Modoium Remote", verificationCode, platformName, SystemInfo.operatingSystem);
            if (p > 0) {
                ModoiumPlugin.videoBitrate = 24_000_000;
            }
            else {
                Debug.LogError($"[modoium] failed to start core: {p}");
            }
        }

        internal void Shutdown() {
            if (running == false) { return; }

            ModoiumShutDown();
        }
    }
}
