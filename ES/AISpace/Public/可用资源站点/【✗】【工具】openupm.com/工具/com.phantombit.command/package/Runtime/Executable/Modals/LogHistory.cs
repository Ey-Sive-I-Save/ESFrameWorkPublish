using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using UnityEngine;
using WellFired.Command.Unity.Runtime.Log;
using WellFired.Command.Unity.Runtime.UI.Windows;

namespace WellFired.Command.Unity.Runtime.Modals
{
	public class LogHistory
	{
	    private readonly List<LogEntry> _filteredLogItems = new List<LogEntry>();
	    private string _filterString = "";
		private readonly Filter _filter;
		private readonly DebugLogHistory _debugLogHistory;

		[PublicAPI]
	    public string FilterString
	    {
	        get => _filterString;
		    set
	        {
	            if (_filterString == value)
	                return;
	
	            _filterString = value;
	            FilterLogItems();
	        }
	    }
	
	    public IList<LogEntry> LogEntries { get; private set; }

		public bool IsFiltered => _filterString != "" || HasFiltersToggled;

		public bool HasFiltersToggled => _filter.Custom.Any(customFilter => !customFilter.State) || _filter.Global.Any(customFilter => !customFilter.State);

		public LogHistory(DebugLogHistory debugLogHistory, Filter filter)
	    {
			_debugLogHistory = debugLogHistory;
			_debugLogHistory.LogEntryAdded += AddLogEntryToFilteredListIfNecessary;
			_debugLogHistory.LogEntryRemoved += RemoveLogEntryFromFilteredList;
			_debugLogHistory.LogHistoryCleared += FilterLogItems;
			LogEntries = _debugLogHistory.LogItems;
			_filter = filter;
		}
		
		public void FilterLogItems()
		{
			_filteredLogItems.Clear();
			
			if (!IsFiltered)
			{
				LogEntries = _debugLogHistory.LogItems;
				return;
			}

			var items = _debugLogHistory.LogItems;
			
			var nonEnabled = _filter.Custom.Any(o => o.Name == "No Filter" && o.State);
			if (!nonEnabled)
				items = _debugLogHistory.LogItems.Where(o => !string.IsNullOrEmpty(o.FilterName)).ToList();

			foreach (var item in items)
			{
				if(PassesAllFilters(item))
					_filteredLogItems.Add(item);
			}
			
			LogEntries = _filteredLogItems.AsReadOnly();
		}

		private bool PassesAllFilters(LogEntry item)
		{
			if (!PassesSearch(item))
				return false;

			if (!PassesGlobalFilters(item, _filter.Global.Where(f => f.State)))
				return false;

			if (!PassesCustomFilters(item, _filter.Custom.Where(f => f.State)))
				return false;

			return true;
		}

		private void AddLogEntryToFilteredListIfNecessary(LogEntry logEntry)
	    {
			if (IsFiltered && PassesAllFilters(logEntry))
				_filteredLogItems.Add(logEntry);
	    }
	
		private void RemoveLogEntryFromFilteredList(LogEntry logEntry)
		{
			_filteredLogItems.Remove(logEntry);
		}

		private bool PassesSearch(LogEntry logEntry)
		{
			return string.IsNullOrEmpty(_filterString) || logEntry.LogMessage.Contains(_filterString);
		}
		
		private static bool PassesGlobalFilters(LogEntry logEntry, IEnumerable<Filter.FilterState> enabledFilters)
		{
			var isInfo = logEntry.Type == LogType.Log;
			var isWarning = logEntry.Type == LogType.Warning;
			var isError = logEntry.Type == LogType.Error;
			var isException = logEntry.Type == LogType.Exception;

			var filterStates = enabledFilters as Filter.FilterState[] ?? enabledFilters.ToArray();
			var isInfoEnabled = filterStates.Any(o => o.Name == "Info");
			var isWarningEnabled = filterStates.Any(o => o.Name == "Warnings");
			var isErrorEnabled = filterStates.Any(o => o.Name == "Errors");
			var isExceptionEnabled = filterStates.Any(o => o.Name == "Exceptions");

			if (isInfo && isInfoEnabled)
				return true;
		    
			if (isWarning && isWarningEnabled)
				return true;
		    
			if (isError && isErrorEnabled)
				return true;
		    
			return isException && isExceptionEnabled;
		}
	
	    private static bool PassesCustomFilters(LogEntry logEntry, IEnumerable<Filter.FilterState> enabledFilters)
	    {
		    var isNotCustomFilterEntry = string.IsNullOrEmpty(logEntry.FilterName);
		    if(isNotCustomFilterEntry)
			    return true;
		    
		    return enabledFilters.Any(o => logEntry.FilterName.Equals(o.Name));
	    }

		public string ActiveFilterNames()
		{
			var builder = new StringBuilder();

			var active = _filter.Global.Where(f => f.State).ToList();
			active.AddRange(_filter.Custom.Where(f => f.State));

			foreach (var item in active)
				builder.AppendLine(!string.IsNullOrEmpty(item.Name) ? $"    {item.Name}" : "    Unknown");
			
			return builder.ToString();
		}
	}
}