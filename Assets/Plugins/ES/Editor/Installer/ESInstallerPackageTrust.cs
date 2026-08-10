using System;
using System.Collections.Generic;

namespace ES.EditorInternal.Installer
{
    /// <summary>Serializable contract shared by the existing installer verifier and its publisher.</summary>
    [Serializable]
    internal sealed class UnityPackageTrustManifest
    {
        public int schemaVersion = 1;
        public string keyId = string.Empty;
        public string packageId = string.Empty;
        public string packageVersion = string.Empty;
        public string source = string.Empty;
        public List<UnityPackageTrustArtifact> artifacts = new List<UnityPackageTrustArtifact>();
        public string signature = string.Empty;
    }

    [Serializable]
    internal sealed class UnityPackageTrustArtifact
    {
        public string relativePath = string.Empty;
        public long size;
        public string sha256 = string.Empty;
    }
}
