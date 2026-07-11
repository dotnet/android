using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace JniReferenceLeakTests;

[TestClass]
public sealed class WidgetReferenceTests
{
	[TestMethod]
	public void InflateCustomViewDoesNotLeakGlobalReferences ()
	{
		var inflater = Application.Context.GetSystemService (Context.LayoutInflaterService) as LayoutInflater;
		if (inflater is null) {
			throw new AssertFailedException ("Could not obtain LayoutInflater.");
		}

		ReferenceTestHelpers.AssertNoGlobalReferenceLeak (() => {
			using var view = inflater.Inflate (Resource.Layout.leak_test_widget, null);
		});
	}
}

[Register ("net/dot/jni/referenceleaktests/LeakTestButton")]
public class LeakTestButton : Button
{
	public LeakTestButton (Context context)
		: base (context)
	{
	}

	public LeakTestButton (Context context, IAttributeSet attributes)
		: base (context, attributes)
	{
	}
}
