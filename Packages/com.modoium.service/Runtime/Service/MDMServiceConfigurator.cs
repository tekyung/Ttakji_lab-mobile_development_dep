using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modoium.Service {
    internal class MDMServiceConfigurator {
        private const float DelayToApplyAfterEdit = 1.5f;

        private string _serviceNameInEdit;
        private string _lastAppliedServiceName;
        private float _timeToApply = -1f;

        private string currentServiceName => Application.productName;

        public string serviceName => _lastAppliedServiceName;

        public MDMServiceConfigurator() {
            if (string.IsNullOrEmpty(currentServiceName) == false &&
                string.IsNullOrEmpty(ModoiumPlugin.serviceName)) {
                ModoiumPlugin.serviceName = currentServiceName;
            }
            _lastAppliedServiceName = currentServiceName;
        }

        public void Update() {
            if (Application.isEditor == false ||
                string.IsNullOrEmpty(currentServiceName)) { return; }

            if (currentServiceName == ModoiumPlugin.serviceName) {
                _serviceNameInEdit = string.Empty;
                _timeToApply = -1f;
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (currentServiceName != _serviceNameInEdit) {
                _serviceNameInEdit = currentServiceName;
                _timeToApply = now + DelayToApplyAfterEdit;
            }
            if (now < _timeToApply) { return; }

            applyConfigs();

            ModoiumPlugin.serviceName = currentServiceName;
            _serviceNameInEdit = string.Empty;
            _timeToApply = -1f;
        }

        private void applyConfigs() {
            ModoiumPlugin.ChangeServiceProps(currentServiceName);
            _lastAppliedServiceName = currentServiceName;
        }
    }
}
