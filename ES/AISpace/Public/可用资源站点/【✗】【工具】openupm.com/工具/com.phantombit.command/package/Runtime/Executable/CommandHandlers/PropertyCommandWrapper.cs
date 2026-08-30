using System;
using System.Reflection;
using WellFired.Command.Unity.Runtime.Helpers;
using WellFired.Command.Unity.Runtime.Log;
using WellFired.Command.Unity.Runtime.Wrapper;

namespace WellFired.Command.Unity.Runtime.CommandHandlers
{
	public class PropertyCommandWrapper : CommandWrapper
	{
		#region Fields
	    private readonly PropertyInfo _propertyInfo;
		#endregion
	
		#region Properties
		public override ParameterWrapper[] Parameters
	    {
	        get
	        {
		        var value = _propertyInfo.GetValue(ObjectReference?.Target, null);
		        return new[]
	                   {
	                       new ParameterWrapper()
	                       {IsOptional = true, Name = "value", Type = _propertyInfo.PropertyType, DefaultValue = value}
	                   };
	        }
	    }
		#endregion
	
		#region Methods
	    public PropertyCommandWrapper(string commandName, string description, Type type, object obj, PropertyInfo propertyInfo)
	        : base(commandName, description, type, obj)
	    {
	        _propertyInfo = propertyInfo;
			if(propertyInfo.DeclaringType != null)
				TheMethodOrPropertyName = propertyInfo.DeclaringType.FullName + "." + propertyInfo.Name;
	        else
				TheMethodOrPropertyName = propertyInfo.Name;
	    }
	
	    public override bool Equals(object otherObject)
	    {
		    if(!(otherObject is PropertyCommandWrapper otherCommandHandler))
	            return false;
	
	        if(base.Equals(otherObject) == false)
	            return false;
	
	        return (_propertyInfo == otherCommandHandler._propertyInfo);
	    }
	
	    public override int GetHashCode()
	    {
	        return _propertyInfo.GetHashCode() ^ base.GetHashCode();
	    }
	
	    public override void Invoke(params string[] arguments)
	    {
		    switch (arguments.Length)
		    {
			    case 0:
				    var value = _propertyInfo.GetValue(ObjectReference?.Target, null);

				    DebugLogHistory.Instance.LogMessage(CommandName + " = " + value);
				    break;
			    case 1:
				    object parameterValue;
				    try
				    {
					    parameterValue = Helper.GetArgumentValueFromString(arguments[0], _propertyInfo.PropertyType);
				    }
				    catch(Exception)
				    {
					    var info = $"Command : {CommandName} is invalid, expected : {_propertyInfo.PropertyType}";
					    DebugLogHistory.Instance.LogMessage(info);
					    return;
				    }

				    _propertyInfo.SetValue(ObjectReference?.Target, parameterValue, null);
				    var message = $"{CommandName} has been successfully set to {parameterValue}";
				    DebugLogHistory.Instance.LogMessage(message);
				    break;
			    default:
				    DebugLogHistory.Instance.LogMessage("Properties take either zero or one arguments");
				    break;
		    }
	    }
		#endregion
	}
}