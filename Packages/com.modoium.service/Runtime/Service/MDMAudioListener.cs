using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modoium.Service {
    [RequireComponent(typeof(AudioListener))]
    public class MDMAudioListener : MonoBehaviour {
        private static MDMAudioListener _instance;

        private void Awake() {
            if (_instance != null) {
                Destroy(_instance);
            }
            _instance = this;

            hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInInspector;
        }

        private void OnAudioFilterRead(float[] data, int channels) {
            ModoiumPlugin.ProcessMasterAudioOutput(data, data.Length / channels, channels, AudioSettings.dspTime);
        }
    }
}
