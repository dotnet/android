using System;
using System.Threading;

namespace Android.Runtime {
	internal class DynamicMethodNameCounter {
		static long dynamicMethodNameCounter;

		internal static string GetUniqueName ()
		{
			return Interlocked.Increment (ref dynamicMethodNameCounter).ToString (System.Globalization.CultureInfo.InvariantCulture);
		}
	}
}
