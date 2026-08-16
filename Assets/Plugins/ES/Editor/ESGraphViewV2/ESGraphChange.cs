using System;
using System.Collections.Generic;

namespace ES.EditorInternal
{
    [Flags]
    internal enum ESGraphChangeKind : byte
    {
        None = 0,
        Layout = 1 << 0,
        Content = 1 << 1,
        Structure = 1 << 2,
        Schema = 1 << 3,
        External = 1 << 4
    }

    /// <summary>
    /// Describes one committed Graph edit. It is Editor-only projection metadata and never becomes
    /// a second serialized Graph state. Layout changes do not invalidate baked content.
    /// </summary>
    internal readonly struct ESGraphChange
    {
        public ESGraphChangeKind Kind { get; }
        public string NodeId { get; }
        public string EdgeId { get; }
        public IReadOnlyCollection<string> NodeIds { get; }
        public IReadOnlyCollection<string> EdgeIds { get; }

        public bool AffectsBake => (Kind & (ESGraphChangeKind.Content
            | ESGraphChangeKind.Structure
            | ESGraphChangeKind.Schema
            | ESGraphChangeKind.External)) != 0;

        public bool RequiresFullProjection => (Kind & (ESGraphChangeKind.Schema
            | ESGraphChangeKind.External)) != 0;

        public ESGraphChange(ESGraphChangeKind kind, string nodeId = null, string edgeId = null,
            IReadOnlyCollection<string> nodeIds = null,
            IReadOnlyCollection<string> edgeIds = null)
        {
            Kind = kind;
            NodeId = nodeId ?? string.Empty;
            EdgeId = edgeId ?? string.Empty;
            NodeIds = nodeIds;
            EdgeIds = edgeIds;
        }

        public static ESGraphChange ExternalChange => new ESGraphChange(ESGraphChangeKind.External);
    }
}
