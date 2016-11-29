using System;
using System.Runtime.InteropServices;

namespace TestNativeLib
{
	public class Binding
	{
		[DllImport("simple")]
		public static extern int SampleFunction ();

		[DllImport("simple2")]
		public static extern int SampleFunction2 ();
	}
}
