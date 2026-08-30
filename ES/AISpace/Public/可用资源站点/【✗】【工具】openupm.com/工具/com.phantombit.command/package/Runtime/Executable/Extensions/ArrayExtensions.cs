using System;

namespace WellFired.Command.Unity.Runtime.Extensions
{
	public static class ArrayExtensions
	{
		/// <summary>
		/// This method gets a sub section of another array.
		/// </summary>
		/// <returns>The array.</returns>
	    public static T[] SubArray<T>(this T[] data, int index, int length)
	    {
	        var result = new T[length];
	        Array.Copy(data, index, result, 0, length);
	        return result;
	    }

		/// <summary>
		/// Populates an array with the specified value.
		/// </summary>
		/// <param name="data">The array we will populate</param>
		/// <param name="value">The value to populate this array with</param>
		public static void Populate<T>(this T[] data, T value)
		{
			for (var i = 0; i < data.Length; ++i)
			{
				data[i] = value;
			}
		}
	}
}