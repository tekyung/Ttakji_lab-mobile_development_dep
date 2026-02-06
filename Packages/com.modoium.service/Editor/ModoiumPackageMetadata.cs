#if XR_MGMT_320

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.XR.Management.Metadata;

namespace Modoium.Service.Editor {
    internal class ModoiumMetadata : IXRPackage {
        private static IXRPackageMetadata _metadata = new ModoiumPackageMetadata();

        public IXRPackageMetadata metadata => _metadata;

        public bool PopulateNewSettingsInstance(ScriptableObject obj) {
            var settings = obj as ModoiumXRSettings;

            return settings != null;
        }

        private class ModoiumPackageMetadata : IXRPackageMetadata {
            public string packageName => "Modoium";
            public string packageId => "com.modoium.service";
            public string settingsType => "Modoium.Service.ModoiumXRSettings";
            
            private static readonly List<IXRLoaderMetadata> _loaderMetadata = new List<IXRLoaderMetadata>() { new ModoiumLoaderMetadata() };
            public List<IXRLoaderMetadata> loaderMetadata => _loaderMetadata;
        }

        private class ModoiumLoaderMetadata : IXRLoaderMetadata {
            public string loaderName => "Modoium";
            public string loaderType => "Modoium.Service.ModoiumLoader";

            private static readonly List<BuildTargetGroup> _supportedBuildTargets = new List<BuildTargetGroup>() { BuildTargetGroup.Standalone };
            public List<BuildTargetGroup> supportedBuildTargets => _supportedBuildTargets;
        }
    }
}

#endif
