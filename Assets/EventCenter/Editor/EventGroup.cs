using System.Collections.Generic;

namespace FlyRabbit.EventCenter
{
    public class EventGroup
    {
        public string Signature = null;
        public bool HasError = false;
        public List<EventUsageInfo> Adds = new List<EventUsageInfo>();
        public List<EventUsageInfo> Removes = new List<EventUsageInfo>();
        public List<EventUsageInfo> Triggers = new List<EventUsageInfo>();
    }
}