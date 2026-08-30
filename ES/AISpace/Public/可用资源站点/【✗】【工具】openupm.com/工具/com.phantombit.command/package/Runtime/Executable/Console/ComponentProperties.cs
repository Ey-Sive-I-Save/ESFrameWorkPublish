using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace WellFired.Command.Unity.Runtime.Console
{
	class ComponentProperties
	{
		private List<PropertyInfo> _properties = new List<PropertyInfo>();
		private List<FieldInfo> _fields = new List<FieldInfo>();
	
		public List<PropertyInfo> Properties
		{
			get => _properties;
			set { _properties = value; }
		}
		
		public List<FieldInfo> Fields
		{
			get => _fields;
			set { _fields = value; }
		}
	
		public string ComponentName
		{
			get;
			set;
		}
	
		public Component Component
		{
			get;
			set;
		}
	}
}