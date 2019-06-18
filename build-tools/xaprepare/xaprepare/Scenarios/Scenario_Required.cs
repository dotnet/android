using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	class Scenario_Required : Scenario
	{
		public Scenario_Required () : base ("Required", "Just the basic steps to quickly install required tools and generate build files.", Context.Instance)
		{}
	}
}
