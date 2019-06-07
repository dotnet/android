using System;

namespace Xamarin.Android.Prepare
{
	sealed class TestAssembly
	{
		public string Name               { get; }
		public TestAssemblyType TestType { get; }
		public bool ExcludeDebugSymbols  { get; }

		public TestAssembly (string name, TestAssemblyType type, bool excludeDebugSymbols = false)
		{
			if (String.IsNullOrEmpty (name))
				throw new ArgumentException ("must not be null or empty", nameof (name));

			Name = name;
			TestType = type;
			ExcludeDebugSymbols = excludeDebugSymbols;
		}
	}
}
