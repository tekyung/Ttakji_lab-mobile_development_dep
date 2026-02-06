using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Modoium.Service.Editor {
    internal class MDMRemoteStatusNoRunningCoreView : VisualElement {
        public MDMRemoteStatusNoRunningCoreView(VisualElement parent) {
            this.FillParent().Padding(10);

            Add(createBody());

            parent.Add(this);
        }

        public void UpdateView(MDMRemoteStatusWindow.State state) {
            style.display = state == MDMRemoteStatusWindow.State.NoRunningCore ?
                DisplayStyle.Flex : DisplayStyle.None;
        }

        private VisualElement createBody() {
            return new TextElement { text = Styles.bodyText };
        }

        private class Styles {
            public static string bodyText = "Searching for a Modoium host...";
        }
    }
}
