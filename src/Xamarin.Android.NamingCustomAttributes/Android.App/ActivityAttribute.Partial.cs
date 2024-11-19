using System;
using Android.Views;
using Java.Interop;

namespace Android.App;

public sealed partial class ActivityAttribute
{
	string IJniNameProviderAttribute.Name => Name ?? "";

	[Obsolete ("There is no //activity/@android:layoutDirection attribute. This was a mistake. " +
	    "Perhaps you wanted ConfigurationChanges=ConfigChanges.LayoutDirection?")]
	public LayoutDirection LayoutDirection { get; set; }

	public bool MainLauncher { get; set; }
}
