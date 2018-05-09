using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks
{
	sealed class LayoutWidget
	{
		public string Id;
		public string Type;
		public string Name;
		public string PartialClasses;
		public List<LayoutWidgetType> AllTypes;
		public List<LayoutLocationInfo> Locations;
		public List<LayoutTypeFixup> TypeFixups;
		public LayoutWidgetType WidgetType = LayoutWidgetType.Unknown;
	}
}
