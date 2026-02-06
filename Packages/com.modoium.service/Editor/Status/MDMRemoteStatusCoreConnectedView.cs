using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Modoium.Service.Editor {
    internal class MDMRemoteStatusCoreConnectedView : VisualElement {
        private TextElement _bodyStatus;
        private MDMRemoteStatusVideoBitrate _videoBitrate;
        private TextElement _hostName;
        private TextElement _serviceName;
        private TextElement _verificationCode;

        public MDMRemoteStatusCoreConnectedView(VisualElement parent) {
            this.FillParent().Padding(10);

            Add(createStatus());
            Add(createInfo());
            Add(createDivider());
            Add(createClientGuidelineIntro());
            Add(createClientGuidelineStep1());
            Add(createClientInstallFoldout());
            Add(createClientGuidelineStep2());
            Add(createClientGuidelineStep3());
            Add(createServiceInfo());
            Add(createClientGuidelineStep4());
            Add(createVerificationCode());
            Add(createHelp());

            parent.Add(this);
        }

        public void UpdateView(MDMRemoteStatusWindow.State state, 
                               bool embeddedCoreRunning,
                               float videoBitrate, 
                               string hostname, 
                               string servname, 
                               string verificationCode) {
            style.display = state == MDMRemoteStatusWindow.State.CoreConnected ? 
                DisplayStyle.Flex : DisplayStyle.None;

            updateStatus(embeddedCoreRunning);
            _videoBitrate.UpdateView(videoBitrate, embeddedCoreRunning == false);
            updateServiceInfo(hostname, servname);
            updateVerificationCode(verificationCode);
        }

        private VisualElement createStatus() {
            _bodyStatus = new TextElement { text = Styles.bodyStatusHubConnected };
            _bodyStatus.style.unityFontStyleAndWeight = FontStyle.Bold;
            _bodyStatus.style.display = DisplayStyle.None;

            return _bodyStatus;
        }

        private VisualElement createInfo() {
            var box = new Box().Padding(4);
            box.style.marginTop = 4;

            _videoBitrate = new MDMRemoteStatusVideoBitrate(box);

            return box;
        }

        private VisualElement createDivider() {
            return new Box().Divider();
        }

        private VisualElement createClientGuidelineIntro() {
            var body = new TextElement { text = Styles.bodyClientGuidelineIntro };
            body.style.marginBottom = 4;
            return body;
        }

        private VisualElement createClientGuidelineStep1() {
            return new TextElement { text = Styles.bodyClientGuidelineStep1 };
        }

        private VisualElement createClientInstallFoldout() {
            var foldout = new Foldout { 
                text = Styles.foldoutClientInstall,
                value = false
            };
            foldout.style.marginLeft = 10;

            foldout.Add(createClientInstallAndroid());
            foldout.Add(createClientInstalliOS());
            return foldout;
        }

        private VisualElement createClientGuidelineStep2() {
            return new TextElement { text = Styles.bodyClientGuidelineStep2 };
        }

        private VisualElement createClientGuidelineStep3() {
            return new TextElement { text = Styles.bodyClientGuidelineStep3 };
        }

        private VisualElement createServiceInfo() {
            var container = new Box().Padding(6);
            container.style.marginLeft = 14;

            _hostName = new TextElement { text = string.Empty };
            container.Add(_hostName);

            _serviceName = new TextElement { text = string.Empty };
            _serviceName.style.fontSize = 14;
            _serviceName.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(_serviceName);
            
            return container;
        }

        private VisualElement createClientGuidelineStep4() {
            return new TextElement { text = Styles.bodyClientGuidelineStep4 };
        }

        private VisualElement createVerificationCode() {
            var container = new Box().Horizontal().Padding(6);
            container.style.marginLeft = 14;
            container.style.marginBottom = 8;

            _verificationCode = new TextElement { text = string.Empty };
            _verificationCode.style.fontSize = 14;
            _verificationCode.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(_verificationCode);

            return container;
        }

        private void updateStatus(bool embeddedCoreRunning) {
            _bodyStatus.style.display = embeddedCoreRunning ? DisplayStyle.None : DisplayStyle.Flex;   
        }

        private void updateServiceInfo(string hostname, string servname) {
            _serviceName.text = servname;
            _hostName.text = hostname;
        }

        private void updateVerificationCode(string code) {
            _verificationCode.text = code;
        }

#if UNITY_2022_3_OR_NEWER
        private VisualElement createClientInstallAndroid() {
            return new TextElement { text = string.Format(Styles.bodyClientInstallAndroid, MDMUrlHandler.AndroidAppURL) };
        }

        private VisualElement createClientInstalliOS() {
            return new TextElement { text = string.Format(Styles.bodyClientInstalliOS, MDMUrlHandler.iOSAppURL) };
        }

        private VisualElement createHelp() {
            return new TextElement { text = string.Format(Styles.bodyHelp, MDMUrlHandler.TroubleshootingURL) };
        }
#else
        private VisualElement createClientInstallAndroid() {
            var container = new VisualElement().Horizontal();

            container.Add(new TextElement { text = Styles.bodyClientInstallAndroidPrefix });

            var button = new HyperLinkTextElement { text = $" {Styles.modoium}" };
            button.RegisterCallback<MouseUpEvent>((evt) => MDMUrlHandler.Open(MDMUrlHandler.AndroidAppURL));
            container.Add(button);

            container.Add(new TextElement { text = Styles.bodyClientInstallAndroidSuffix });

            return container;
        }

        private VisualElement createClientInstalliOS() {
            var container = new VisualElement().Horizontal();

            container.Add(new TextElement { text = Styles.bodyClientInstalliOSPrefix });

            var button = new HyperLinkTextElement { text = $" {Styles.modoium}" };
            button.RegisterCallback<MouseUpEvent>((evt) => MDMUrlHandler.Open(MDMUrlHandler.iOSAppURL));
            container.Add(button);

            container.Add(new TextElement { text = Styles.bodyClientInstalliOSSuffix });

            return container;
        }

        private VisualElement createHelp() {
            var container = new VisualElement().Horizontal();

            container.Add(new TextElement { text = Styles.bodyHelpPrefix });

            var button = new HyperLinkTextElement { text = $" {Styles.bodyHelpLink}" };
            button.RegisterCallback<MouseUpEvent>((evt) => MDMUrlHandler.Open(MDMUrlHandler.TroubleshootingURL));
            container.Add(button);

            container.Add(new TextElement { text = Styles.bodyHelpSuffix });

            return container;
        }
#endif

        private class Styles {
            public static string bodyStatusHubConnected = $"Modoium Hub connected";
            public static string bodyClientGuidelineIntro = "How to connect from your mobile device :";
            public static string foldoutClientInstall = "Not installed yet?";
            public static string bodyClientGuidelineStep2 = "2) Make sure your mobile device is connected to the same network as this computer.";
            public static string bodyClientGuidelineStep4 = "4) Enter the verification code below if necessary :";

#if UNITY_2022_3_OR_NEWER
            public static string bodyClientGuidelineStep1 = "1) Run <b>Modoium</b> app.";
            public static string bodyClientInstallAndroid = "\u2022 Android : Get <a href=\"{0}\">Modoium</a> from Google Play Store";
            public static string bodyClientInstalliOS = "\u2022 iOS : Get <a href=\"{0}\">Modoium</a> from App Store";
            public static string bodyClientInstallQuest = "\u2022 Quest : Not availble yet.";
            public static string bodyClientGuidelineStep3 = "3) Select your project from the list on <b>Modoium</b> app :";
            public static string bodyHelp = "If you have any issues connecting, please refer to our <a href=\"{0}\">troubleshooting guide</a>.";
#else
            public static string modoium = "Modoium";
            public static string bodyClientGuidelineStep1 = "1) Run Modoium app.";
            public static string bodyClientInstallAndroidPrefix = "\u2022 Android : Get ";
            public static string bodyClientInstallAndroidSuffix = " from Google Play Store";
            public static string bodyClientInstalliOSPrefix = "\u2022 iOS : Get ";
            public static string bodyClientInstalliOSSuffix = " from App Store";
            public static string bodyClientGuidelineStep3 = "3) Select your project from the list on Modoium app :";
            public static string bodyHelpPrefix = "If you have any issues connecting, please refer to our ";
            public static string bodyHelpLink = "troubleshooting guide";
            public static string bodyHelpSuffix = ".";
#endif
        }
    }    
}
