using System;
using System.Collections.Generic;

namespace ES
{
    [Serializable]
    public sealed class ESStorySaveMetadata
    {
        public long checkpointRevision;
        public long checkpointUtcTicks;
    }

    [Serializable]
    public sealed class ESStorySaveSection
    {
        public const int CurrentSchemaVersion = 2;
        public int snapshotSchemaVersion = CurrentSchemaVersion;
        public List<ESQuestRecord> questRecords = new List<ESQuestRecord>();
        public ESStorySaveMetadata metadata = new ESStorySaveMetadata();
    }
}
