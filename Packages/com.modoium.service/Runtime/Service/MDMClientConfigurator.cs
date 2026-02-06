using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Modoium.Service {
    internal class MDMClientConfigurator {
        private MDMService _owner;
        private ModoiumSettings _settings;

        private Config _config;

        public MDMClientConfigurator(MDMService owner) {
            _owner = owner;
            _settings = ModoiumSettings.instance;
            _config = new Config {
                sendNavButtons = _settings.enableAndroidBackButtonInput  
            };
        }

        public void Update() {
            if (_owner.remoteViewConnected == false) { return; }

            var changed = false;

            var sendNavButtons = _settings.enableAndroidBackButtonInput;
            if (sendNavButtons != _config.sendNavButtons) {
                _config.sendNavButtons = sendNavButtons;
                changed = true;
            }

            if (changed) {
                SendConfigClient();
            }
        }

        public void SendConfigClient() {
            ModoiumPlugin.SendConfigClient(JsonConvert.SerializeObject(_config));
        }

        [JsonObject(MemberSerialization.OptIn)]
        private struct Config {
            [JsonProperty("sendNavButtons")]
            public bool sendNavButtons;
        }
    }
}
