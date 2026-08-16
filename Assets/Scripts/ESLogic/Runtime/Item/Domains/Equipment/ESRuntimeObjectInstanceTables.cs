using System;

namespace ES
{
    public readonly struct ESBuffInstanceRecord
    {
        public readonly ESActiveBuffRuntime instance;

        public ESBuffInstanceRecord(ESActiveBuffRuntime instance)
        {
            this.instance = instance;
        }
    }

    public sealed class ESBuffInstanceTable
        : ESInstanceTable<ESBuffInstanceRecord, ulong, int, int>
    {
        private ulong nextRuntimeId = 1;

        public ESBuffInstanceTable(int capacity) : base(capacity)
        {
        }

        public bool TryAddInstance(
            ESActiveBuffRuntime instance,
            int definitionRuntimeKey,
            int ownerId,
            out ESInstanceHandle handle)
        {
            handle = default;
            if (instance == null || definitionRuntimeKey <= 0 || ownerId == 0)
                return false;
            if (!TryAllocateRuntimeId(out ulong runtimeId))
                return false;
            return TryAdd(
                new ESBuffInstanceRecord(instance),
                runtimeId,
                definitionRuntimeKey,
                ownerId,
                out handle);
        }

        public bool TryGetInstance(ESInstanceHandle handle, out ESActiveBuffRuntime instance)
        {
            if (TryGet(handle, out ESBuffInstanceRecord record) && record.instance != null)
            {
                instance = record.instance;
                return true;
            }
            instance = null;
            return false;
        }

        private bool TryAllocateRuntimeId(out ulong runtimeId)
        {
            runtimeId = nextRuntimeId++;
            return runtimeId != 0;
        }
    }

    public readonly struct ESShotInstanceRecord
    {
        public readonly Item instance;

        public ESShotInstanceRecord(Item instance)
        {
            this.instance = instance;
        }
    }

    public sealed class ESShotInstanceTable
        : ESInstanceTable<ESShotInstanceRecord, ulong, int, int>
    {
        private ulong nextRuntimeId = 1;

        public ESShotInstanceTable(int capacity) : base(capacity)
        {
        }

        public bool TryAddInstance(
            Item instance,
            int definitionRuntimeKey,
            int ownerId,
            out ESInstanceHandle handle)
        {
            handle = default;
            if (instance == null || definitionRuntimeKey <= 0 || ownerId == 0)
                return false;
            ulong runtimeId = nextRuntimeId++;
            if (runtimeId == 0)
                return false;
            return TryAdd(
                new ESShotInstanceRecord(instance),
                runtimeId,
                definitionRuntimeKey,
                ownerId,
                out handle);
        }

        public bool TryGetInstance(ESInstanceHandle handle, out Item instance)
        {
            if (TryGet(handle, out ESShotInstanceRecord record) && record.instance != null)
            {
                instance = record.instance;
                return true;
            }
            instance = null;
            return false;
        }
    }
}
