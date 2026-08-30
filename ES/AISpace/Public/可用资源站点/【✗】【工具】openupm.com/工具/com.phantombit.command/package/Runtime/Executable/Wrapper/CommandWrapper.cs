using System;
using System.Text;
using WellFired.Command.Unity.Runtime.Helpers;

namespace WellFired.Command.Unity.Runtime.Wrapper
{
	/// <summary>
	/// Each Command has an instance of something which derives from CommandWrapper, this simply
	/// contains the details for the call. Basically caching it.
	/// It is possible to implement custom commands, simply by deriving from this type
	/// </summary>
	public abstract class CommandWrapper
	{
		protected string TheMethodOrPropertyName;

		#region Properties
	    public string Description { get; }
		public string CommandName { get; }
		public string MethodOrPropertyName => TheMethodOrPropertyName;
		public WeakReference ObjectReference { get; }
		public Type Type { get; }
		private bool IsStatic { get; }
		public abstract ParameterWrapper[] Parameters { get; }
		#endregion

		#region UnImplemented
		public abstract void Invoke(params string[] arguments);
		#endregion
	
		#region Methods
	    protected CommandWrapper(string commandName, string description, Type type, object referenceObject)
	    {
	        CommandName = commandName;
			Description = description;
			Type = type;
			if(referenceObject == null)
				IsStatic = true;
			else
				ObjectReference = new WeakReference(referenceObject);
	    }

		/// <summary>
		/// Your custom Command Wrappers can implement this to check for object equality.
		/// </summary>
		/// <param name="otherObject">The <see cref="CommandWrapper"/> to compare with the current <see cref="CommandWrapper"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="CommandWrapper"/> is equal to the current
		/// <see cref="CommandWrapper"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals(object otherObject)
	    {
		    if(!(otherObject is CommandWrapper otherCommandWrapper))
	            return false;
	
	        if(ObjectReference == null && otherCommandWrapper.ObjectReference == null)
	            return true;
	
	        if(ObjectReference == null && otherCommandWrapper.ObjectReference != null || ObjectReference != null && otherCommandWrapper.ObjectReference == null)
	            return false;

		    // ReSharper disable once PossibleNullReferenceException
		    var other = otherCommandWrapper.ObjectReference.Target;
		    // ReSharper disable once PossibleNullReferenceException
		    return ObjectReference.Target == other;
	    }
	
	    public override int GetHashCode()
	    {
		    var objectTarget = ObjectReference.Target;
	        if(objectTarget == null)
	            throw new InvalidOperationException(
		            $"Command handler target of type {MethodOrPropertyName} was found with a null target object. This is caused when an object is destroyed without unsubscribing from the DevelopmentConsole");

	        return objectTarget.GetHashCode();
		}

		/// <summary>
		/// Determines whether this instance is valid.
		/// </summary>
		/// <returns><c>true</c> if this instance is valid; otherwise, <c>false</c>.</returns>
		public bool IsValid()
		{
			if (IsStatic)
				return true;
			
			return ObjectReference != null && ObjectReference.IsAlive;
		}
		
		/// <summary>
		/// Getsa string containing all the method parameters.
		/// </summary>
		/// <returns>The method parameters as string.</returns>
		public string GetParametersAsString()
		{
			var hasOptionalParameters = false;
			var stringBuilder = new StringBuilder();
			foreach(var parameter in Parameters)
			{
				if(stringBuilder.Length != 0)
					stringBuilder.Append(", ");
				
				var defaultValue = string.Empty;
				
				if(parameter.IsOptional && !hasOptionalParameters)
				{
					hasOptionalParameters = true;
					stringBuilder.Append("[");
				}
				if(parameter.IsOptional)
				{
					if(parameter.Type == typeof(string))
					{
						defaultValue = " = \"\"";
					}
					else
					{
						defaultValue = $" = {parameter.DefaultValue}";
					}
				}
				stringBuilder.Append($"{parameter.Type.Name} {parameter.Name}{defaultValue}");
			}
			
			if(hasOptionalParameters)
				stringBuilder.Append("]");
			
			return stringBuilder.ToString();
		}
		#endregion
	}
}