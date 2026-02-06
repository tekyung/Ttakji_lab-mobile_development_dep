using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modoium.Service.Editor {
    internal abstract class MDMRemoteIssue {
        public enum Level {
            Info,
            Warning,
            Error
        }

        public abstract string id { get; }
        public abstract Level level { get; }
        public abstract string message { get; }
        public abstract (string label, Action action)[] actions { get; }
    }
}
