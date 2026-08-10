using System.Collections.Generic;

namespace ES
{
    public sealed class ESDeveloperTraceProvider : IESDeveloperTraceProvider
    {
        private const int MaxBufferedEvents = 512;

        private readonly List<ESDeveloperEventEnvelope> events =
            new List<ESDeveloperEventEnvelope>(MaxBufferedEvents);

        public bool IsEnabled => true;

        public IReadOnlyList<ESDeveloperEventEnvelope> Events => events;

        public int Count => events.Count;

        public ESDeveloperEventEnvelope LastEvent =>
            events.Count == 0
                ? default
                : events[events.Count - 1];

        public void Emit(in ESDeveloperEventEnvelope envelope)
        {
            if (events.Count >= MaxBufferedEvents)
            {
                events.RemoveAt(0);
            }

            events.Add(envelope);
        }

        public void Clear()
        {
            events.Clear();
        }
    }
}
