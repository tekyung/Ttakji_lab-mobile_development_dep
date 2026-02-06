using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Modoium.Service.Editor {
    internal class MDMRemoteStatusClientConnectedView : VisualElement {
        private TextElement _bodyDevice;
        private MDMRemoteStatusVideoBitrate _videoBitrate;

        public MDMRemoteStatusClientConnectedView(VisualElement parent) {
            this.FillParent().Padding(10);

            Add(createStatus());
            Add(createInfo());
            Add(createDivider());
            Add(createGuide());
            Add(createHelp());

            parent.Add(this);
        }

        public void UpdateView(MDMRemoteStatusWindow.State state, bool embeddedCoreRunning, float videoBitrate, string deviceName) {
            style.display = state == MDMRemoteStatusWindow.State.ClientConnected ?
                DisplayStyle.Flex : DisplayStyle.None;

            updateInfo(embeddedCoreRunning, videoBitrate, deviceName);
        }

        private VisualElement createStatus() {
            var body = new TextElement { text = Styles.bodyStatus };
            body.style.color = Color.green;
            body.style.unityFontStyleAndWeight = FontStyle.Bold;
            return body;
        }

        private VisualElement createInfo() {
            var box = new Box().Padding(4);
            box.style.marginTop = 4;

            _bodyDevice = new TextElement { text = string.Format(Styles.bodyDevice, string.Empty) };
            box.Add(_bodyDevice);

            _videoBitrate = new MDMRemoteStatusVideoBitrate(box);
            _videoBitrate.style.marginTop = 4;

            return box;
        }

        private VisualElement createDivider() {
            return new Box().Divider();
        }

        private VisualElement createGuide() {
            var body = new TextElement { text = Styles.bodyGuide };
            body.style.marginBottom = 8;

            return body;
        }

        private void updateInfo(bool embeddedCoreRunning, float videoBitrate, string deviceName) {
            _bodyDevice.text = string.Format(Styles.bodyDevice, deviceName);
            _videoBitrate.UpdateView(videoBitrate, embeddedCoreRunning == false);
        }

#if UNITY_2022_3_OR_NEWER
        private VisualElement createHelp() {
            return new TextElement { text = string.Format(Styles.bodyHelp, MDMUrlHandler.ProgrammingGuideURL) };
        }
#else
        private VisualElement createHelp() {
            var container = new VisualElement().Horizontal();

            container.Add(new TextElement { text = Styles.bodyHelpPrefix1 });
            container.Add(new TextElement { text = Styles.bodyHelpPrefix2 });

            var button = new HyperLinkTextElement { text = $" {Styles.bodyHelpLink}" };
            button.RegisterCallback<MouseUpEvent>((evt) => MDMUrlHandler.Open(MDMUrlHandler.ImportantNotesURL));
            container.Add(button);

            container.Add(new TextElement { text = Styles.bodyHelpSuffix });

            return container;
        }
#endif

        private class Styles {
            public static string bodyStatus = $"Device connected";
            public static string bodyDevice = "Device : {0}";
            public static string bodyGuide = "Play the editor to view the game on your client device!";

#if UNITY_2022_3_OR_NEWER
            public static string bodyHelp = "If you encounter any issues, particularly with inputs, please refer to our <a href=\"{0}\">user guide</a>.";
#else
            public static string bodyHelpPrefix1 = "If you encounter any issues, particularly with inputs, ";
            public static string bodyHelpPrefix2 = "please refer to our ";
            public static string bodyHelpLink = "user guide";
            public static string bodyHelpSuffix = ".";
#endif
        }
    }
}
