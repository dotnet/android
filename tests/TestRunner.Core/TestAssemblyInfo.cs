using System;
using System.Reflection;

namespace Xamarin.Android.UnitTests
{
	public class TestAssemblyInfo
	{
		public Assembly Assembly { get; }
		public string FullPath { get; }

		public TestAssemblyInfo (Assembly assembly, string fullPath)
		{
			Assembly = assembly ?? throw new ArgumentNullException (nameof (assembly));
			FullPath = fullPath ?? String.Empty;
		}
	}
}
