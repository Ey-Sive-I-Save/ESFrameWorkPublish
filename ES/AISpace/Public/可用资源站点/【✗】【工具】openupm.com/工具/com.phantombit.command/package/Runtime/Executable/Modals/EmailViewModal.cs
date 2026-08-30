using System;
using System.Text;
using UnityEngine;

namespace WellFired.Command.Unity.Runtime.Modals
{
	internal class EmailViewModal
	{
		private readonly LogHistory _viewModel;
	
		public EmailViewModal(LogHistory viewModel)
		{
	        _viewModel = viewModel;
	    }

		public string GetAsBody()
		{
			var buffer = new StringBuilder();

			if (_viewModel.IsFiltered)
			{
				buffer.AppendLine("This is a filtered Log");
				if(!string.IsNullOrEmpty(_viewModel.FilterString))
					buffer.AppendLine($"Search string: {_viewModel.FilterString}");
				if (_viewModel.HasFiltersToggled)
				{
					buffer.AppendLine("Active Filters: ");
					buffer.AppendLine($"{_viewModel.ActiveFilterNames()}");
				}
				buffer.AppendLine(string.Empty); buffer.AppendLine(string.Empty);
			}
			
			buffer.Append("Log");
			buffer.AppendLine(string.Empty);

			foreach (var item in _viewModel.LogEntries)
				buffer.AppendLine(item.LogMessage.Trim());

			return buffer.ToString();
		}
	
	    public string GetHtml()
	    {
		    var buffer = new StringBuilder();

		    buffer.AppendLine("<body>");
		    
		    buffer.AppendLine("<br/>");
	
	    	if(_viewModel.IsFiltered)
			    buffer.AppendLine($"<div class='.topNote'>This is a filtered Log<br/>Search string: {_viewModel.FilterString}<br/>)</div>");

		    foreach(var item in _viewModel.LogEntries)
	        {
	            string categoryClass;
	            switch (item.Type)
	            {
		            case LogType.Log:
			            categoryClass = " header-info";
			            break;
		            case LogType.Warning:
			            categoryClass = " header-warning";
			            break;
		            case LogType.Error:
			            categoryClass = " header-error";
			            break;
		            case LogType.Assert:
		            case LogType.Exception:
			            categoryClass = " header-error";
			            break;
		            default:
			            throw new ArgumentOutOfRangeException();
	            }
		        buffer.AppendLine("<div class='row'><div class='header" + categoryClass + "'><div class='time'>" + item.Time +
	                                "</div><div class='header-text'>" + item.LogMessage.Trim() + "</div></div>");
		        buffer.AppendLine("<div class='stacktrace'>" + item.StackTrace.Replace("\n", "<br>") + "</div></div>");
	        }
		    buffer.AppendLine("</body>");

		    return buffer.ToString();
	    }
	}
}