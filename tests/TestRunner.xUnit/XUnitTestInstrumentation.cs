using System;

using Android.OS;
using Android.Runtime;

namespace Xamarin.Android.UnitTests.XUnit
{
	public abstract class XUnitTestInstrumentation : TestInstrumentation <XUnitTestRunner>
	{
		protected XUnitTestInstrumentation ()
		{
			CommonInit ();
		}

		protected XUnitTestInstrumentation (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
			CommonInit ();
		}

		void CommonInit ()
		{
			LogTag = "xUnit";
		}

		protected override XUnitTestRunner CreateRunner (LogWriter logger, Bundle bundle)
		{
			return new XUnitTestRunner (Context, logger, bundle);
		}
	}
}
