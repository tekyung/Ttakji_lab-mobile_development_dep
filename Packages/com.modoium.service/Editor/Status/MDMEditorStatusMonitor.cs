using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modoium.Service.Editor {
    internal class MDMEditorStatusMonitor {
        private const float IntervalToCheckIssues = 0.25f;

        private MDMService _service;
        private float _remainingToCheckIssues;
        private bool _issuesUpdated;

        public Dictionary<string, MDMRemoteIssue> issues { get; private set; }
        public bool hasIssues => issues != null && issues.Count > 0;
        
        public bool issuesUpdated { 
            get {
                var updated = _issuesUpdated;
                _issuesUpdated = false;

                return updated;
            }
        }

        public MDMEditorStatusMonitor(MDMService service) {
            _service = service;
        }

        public void Update() {
            _remainingToCheckIssues -= Time.unscaledDeltaTime;
            if (_remainingToCheckIssues > 0) { return; }

            _remainingToCheckIssues = IntervalToCheckIssues;
            _issuesUpdated |= checkIssues();
        }

        private bool checkIssues() {
            var updated = false;

            if (issues == null) {
                issues = new Dictionary<string, MDMRemoteIssue>();
                updated = true;
            }

            checkIfIssueExists(MDMRemoteIssueServiceUnavailable.ID, 
                               () => MDMRemoteIssueServiceUnavailable.IssueExists(_service),
                               () => new MDMRemoteIssueServiceUnavailable(_service),
                               ref updated);
            checkIfIssueExists(MDMRemoteIssueDeviceNotSupportXR.ID, 
                               () => MDMRemoteIssueDeviceNotSupportXR.IssueExists(_service),
                               () => new MDMRemoteIssueDeviceNotSupportXR(),
                               ref updated);
            checkIfIssueExists(MDMRemoteIssueDeviceNotSupportMono.ID, 
                               () => MDMRemoteIssueDeviceNotSupportMono.IssueExists(_service),
                               () => new MDMRemoteIssueDeviceNotSupportMono(),
                               ref updated);
            checkIfIssueExists(MDMRemoteIssueRemoteGameViewNotSelected.ID, 
                               () => MDMRemoteIssueRemoteGameViewNotSelected.IssueExists(_service),
                               () => new MDMRemoteIssueRemoteGameViewNotSelected(_service),
                               ref updated);
            checkIfIssueExists(MDMRemoteIssueGameViewUnfocused.ID,
                               () => MDMRemoteIssueGameViewUnfocused.IssueExists(_service),
                               () => new MDMRemoteIssueGameViewUnfocused(),
                               ref updated);
            checkIfIssueExists(MDMRemoteIssueNotRunInBackground.ID,
                               () => MDMRemoteIssueNotRunInBackground.IssueExists(),
                               () => new MDMRemoteIssueNotRunInBackground(),
                               ref updated);
            checkIfIssueExists(MDMRemoteIssueInputSystemNotEnabled.ID,
                               () => MDMRemoteIssueInputSystemNotEnabled.IssueExists(),
                               () => new MDMRemoteIssueInputSystemNotEnabled(),
                               ref updated);
            checkIfIssueExists(MDMRemoteIssueIncompleteXRSettings.ID,
                               () => MDMRemoteIssueIncompleteXRSettings.IssueExists(),
                               () => new MDMRemoteIssueIncompleteXRSettings(),
                               ref updated);

            return updated;
        }

        private void checkIfIssueExists(string id, Func<bool> issueExists, Func<MDMRemoteIssue> create, ref bool updated) {
            var exists = issueExists();
            if (issues.ContainsKey(id) == exists) { return; }

            if (exists) {
                issues.Add(id, create());
            }
            else {
                issues.Remove(id);
            }

            updated = true;
        }
    }
}
