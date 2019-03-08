using System;

namespace Xamarin.Android.Prepare
{
	[AttributeUsage (AttributeTargets.Class)]
	class ScenarioAttribute : Attribute
	{
		public bool IsDefault { get; }

		public ScenarioAttribute (bool isDefault)
		{
			IsDefault = isDefault;
		}
	}
}
