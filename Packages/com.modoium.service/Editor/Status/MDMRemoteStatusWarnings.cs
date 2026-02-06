using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Modoium.Service.Editor {
    internal class MDMRemoteStatusWarnings : VisualElement {
        private MDMEditorStatusMonitor _statusMonitor;
        private VisualElement _noContent;
        private List<MDMMessageBox> _messages;

        public MDMRemoteStatusWarnings(VisualElement parent, MDMEditorStatusMonitor statusMonitor) {
            _messages = new List<MDMMessageBox>();
            _statusMonitor = statusMonitor;

            this.FillParent().Padding(10);
            style.paddingTop = 0;

            Add(createDivider());
            Add(createTitle());
            Add(createNoContent());

            updateMessages();

            parent.Add(this);
        }

        public void UpdateView(MDMRemoteStatusWindow.State state) {
            if (state != MDMRemoteStatusWindow.State.Unavailable &&
                state != MDMRemoteStatusWindow.State.CoreConnected &&
                state != MDMRemoteStatusWindow.State.ClientConnected) {
                style.display = DisplayStyle.None;
                return;
            }
            style.display = DisplayStyle.Flex;

            updateNoContent();
            updateMessages();
        }

        private VisualElement createDivider() {
            var divider = new Box().Divider();
            divider.style.marginTop = 0;

            return divider;
        }

        private VisualElement createTitle() {
            var title = new TextElement { text = Styles.title };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;

            return title;
        }

        private VisualElement createNoContent() {
            var box = new Box().Padding(10);
            box.style.alignContent = Align.Center;

            box.Add(new TextElement { text = Styles.bodyNoContent });
            _noContent = box;

            return box;
        }

        private void updateNoContent() {
            _noContent.style.display = _statusMonitor.hasIssues == false ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void updateMessages() {
            if (_statusMonitor.issuesUpdated == false) { return; }

            foreach (var message in _messages) {
                Remove(message);
            }
            _messages.Clear();

            if (_statusMonitor.hasIssues) {
                _messages.AddRange(_statusMonitor.issues.Values.Select(issue => new MDMMessageBox(this, issue)));
            }
        }

        private class Styles {
            public static string title = "Warnings & Info";
            public static string bodyNoContent = "No issues";
        }
    }
}
