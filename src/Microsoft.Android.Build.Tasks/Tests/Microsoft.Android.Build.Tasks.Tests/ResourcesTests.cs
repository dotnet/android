using System.Globalization;
using System.Resources;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class ResourcesTests
{
	// The tasks in this assembly report XA#### diagnostics through resources linked from
	// Xamarin.Android.Build.Tasks, and [Export] diagnostics through Java.Interop.Localization.
	// Both must keep resolving their satellite assemblies after the move to this assembly.
	// Assert that a translation exists and differs from English rather than asserting on the
	// translated text itself, which the localization pipeline regenerates regularly.
	[TestCase ("de")]
	[TestCase ("ja")]
	public void TaskDiagnosticsAreLocalized (string culture)
	{
		AssertLocalized (Xamarin.Android.Tasks.Properties.Resources.ResourceManager, "XA4250", culture);
		AssertLocalized (Java.Interop.Localization.Resources.ResourceManager, "JavaCallableWrappers_XA4205", culture);
	}

	static void AssertLocalized (ResourceManager resourceManager, string name, string culture)
	{
		var neutral = resourceManager.GetString (name, CultureInfo.InvariantCulture);
		Assert.IsNotNull (neutral, $"'{name}' should exist in the neutral resources.");

		var translated = resourceManager.GetString (name, CultureInfo.GetCultureInfo (culture));
		Assert.IsNotNull (translated, $"'{name}' should exist in the '{culture}' resources.");
		Assert.AreNotEqual (neutral, translated,
			$"'{name}' fell back to English, so the '{culture}' satellite assembly was not found.");
	}
}
