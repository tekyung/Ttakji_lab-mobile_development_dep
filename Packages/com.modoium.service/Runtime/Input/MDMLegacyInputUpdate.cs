using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modoium.Service {
    internal class MDMLegacyInputUpdate : MonoBehaviour {
        private MDMInputProvider _inputProvider;
        private ModoiumSettings _settings;

        public void Configure(MDMInputProvider inputProvider) {
            _inputProvider = inputProvider;
            _settings = ModoiumSettings.instance;
        }

        private void Update() {
            _inputProvider.UpdateInputFrame(_settings.simulateTouchWithMouse);
        }
    }
}
