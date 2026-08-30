using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace WellFired.Command.Unity.Runtime.UI.Windows
{
	public class Filter
	{
		public class FilterState
		{
			private bool _state;

			public string Name  { get; }
			
			[PublicAPI]
			public bool CustomFilter { get; }

			public bool State 
			{ 
				get => PlayerPrefs.GetInt(Name, 1) == 1;
				set 
				{ 
					_state = value;  
					PlayerPrefs.SetInt(Name, _state == false ? 0 : 1);
				} 
			}

			public FilterState(string name, bool custom)
			{
				Name = name;
				CustomFilter = custom;
			}
		}

		public IEnumerable<FilterState> Custom { get; private set; }
		public IEnumerable<FilterState> Global { get; }

		public int ActiveFilterCount => Active(Custom) + Active(Global);

		private static int Active(IEnumerable<FilterState> custom) => custom.Count(filter => filter.State);

		public Filter()
		{
			Global = new[]
			{
				new FilterState("Info", false), 
				new FilterState("Warnings", false), 
				new FilterState("Errors", false), 
				new FilterState("Exceptions", false)
			};

			Custom = new[] { new FilterState("No Filter", true) };
		}

		public void AddCustomFilters(IEnumerable<FilterState> filterStates)
		{
			var customList = Custom.ToList();
			customList.AddRange(filterStates);
			Custom = customList;
		}
	}
}