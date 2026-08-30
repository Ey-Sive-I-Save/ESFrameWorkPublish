using System;
using UnityEngine;
using WellFired.Command.Unity.Runtime.CommandHandlers;
using WellFired.Command.Unity.Runtime.Console;
using Debug = WellFired.Command.Log.Debug;

namespace WellFired.Command.Platform.Prefabs
{
	public class DotCommandLoader : MonoBehaviour
	{
		[Header(".Command customisation")]
		[SerializeField] private bool _autoOpenOnException = true;
		[SerializeField] private bool _autoOpenOnError = true;
		[SerializeField] private bool _showOpenDotCommandButton = true;
		[SerializeField] private string _showDotCommandButtonMessage = "Show .Command (~)";
		[SerializeField] private DisplayCorner _displayCorner = DisplayCorner.TopLeft;
	
		[Header("Built in commands")]
		[SerializeField] private bool _emailLogSupportEnabled = true;
		[SerializeField] private bool _inspectCommandEnabled = true;
		[SerializeField] private bool _deviceIdCommandEnabled = true;
		[SerializeField] private bool _clearConsoleCommandEnabled = true;
		[SerializeField] private bool _autoScrollEnabled = true;
		
		private enum AdditionalFilters
		{
			// ReSharper disable once InconsistentNaming
			dotCommand
		}
	
		private void Start()
		{
			DevelopmentConsole.Load(
				_clearConsoleCommandEnabled,
				_deviceIdCommandEnabled,
				_inspectCommandEnabled,
				_emailLogSupportEnabled,
				_autoScrollEnabled,
				typeof(AdditionalFilters)
				);
			
			Debug.Log(AdditionalFilters.dotCommand, "Loaded .Command, now Applying custom settings");
			
			if(_autoOpenOnError || _autoOpenOnException)
				DevelopmentConsole.Instance.EnableAutoOpen(_autoOpenOnException, _autoOpenOnError);
			else
				DevelopmentConsole.Instance.DisableAutoOpen();

			DevelopmentConsole.Instance.DrawShowDotCommandButton = _showOpenDotCommandButton;
			DevelopmentConsole.Instance.ShowDotCommandButtonMessage = _showDotCommandButtonMessage;
			DevelopmentConsole.Instance.DisplayCorner = _displayCorner;
			
			Destroy(gameObject);
		}
	}
}
