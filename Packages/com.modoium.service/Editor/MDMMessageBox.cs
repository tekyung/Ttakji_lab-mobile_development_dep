using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace Modoium.Service.Editor {
    internal class MDMMessageBox : Box {
        public enum Icon {
            Warning
        }

        public MDMMessageBox(VisualElement parent, MDMRemoteIssue issue) {
            this.Padding(6);
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.FlexStart;
            style.marginTop = style.marginBottom = 4;
            style.minHeight = 44;

            Add(createIcon(issue));
            Add(createBody(issue));

            parent.Add(this);
        }

        private VisualElement createIcon(MDMRemoteIssue issue) {
            var icon = issue.level switch {
                MDMRemoteIssue.Level.Info => "console.infoicon.sml",
                MDMRemoteIssue.Level.Warning => "console.warnicon.sml",
                MDMRemoteIssue.Level.Error => "console.erroricon.sml",
                _ => null
            };

            var element = new VisualElement();
            element.style.width = element.style.height = 16;
            element.style.marginRight = 6;
            element.style.backgroundImage = string.IsNullOrEmpty(icon) == false ? 
                EditorGUIUtility.IconContent(icon).image as Texture2D : null;

            return element;
        }

        private VisualElement createBody(MDMRemoteIssue issue) {
            var container = new VisualElement().FillParent();
            container.style.justifyContent = Justify.Center;

            var message = new TextElement { text = issue.message };
            message.style.flexWrap = Wrap.Wrap;
            container.Add(message);
            
            var actions = issue.actions;
            if (actions != null && actions.Length > 0) {
                var actionContainer = new VisualElement();
                actionContainer.style.flexDirection = FlexDirection.Row;
                actionContainer.style.flexWrap = Wrap.Wrap;
                actionContainer.style.marginTop = 6;
                actionContainer.style.justifyContent = Justify.FlexStart;

                foreach (var (label, action) in actions) {
                    var button = new Button { text = label };
                    button.style.minWidth = 60;
                    button.clicked += action;
                    actionContainer.Add(button);
                }

                container.Add(actionContainer);
            }

            return container;
        }
    }
}
