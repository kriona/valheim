using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TweakStats
{
	/// <summary>
	/// Compares objects by reference, since Unity objects compare equal to null once destroyed
	/// </summary>
	public class ReferenceComparer : IEqualityComparer<object>
	{
		public static readonly ReferenceComparer Instance = new ReferenceComparer();

		/// <summary>
		/// Whether two objects are the same object
		/// </summary>
		/// <param name="a">The first object</param>
		/// <param name="b">The second object</param>
		/// <returns>True when they are the same object</returns>
		public new bool Equals(object a, object b)
		{
			return(ReferenceEquals(a, b));
		}

		/// <summary>
		/// A hash code based on the object's identity
		/// </summary>
		/// <param name="obj">The object</param>
		/// <returns>The hash code</returns>
		public int GetHashCode(object obj)
		{
			return(RuntimeHelpers.GetHashCode(obj));
		}
	}
}
