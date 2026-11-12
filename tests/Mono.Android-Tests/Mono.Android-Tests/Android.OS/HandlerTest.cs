using System;
using System.Threading;
using Android.OS;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.RuntimeTests {

	[TestClass]
	public class HandlerTest {

		[TestMethod]
		public void RemoveDisposedInstance ()
		{
      using (var t = new HandlerThread ("RemoveDisposedInstance")) {
        t.Start ();
        using (var h = new Handler (t.Looper)) {
    			var e = new ManualResetEvent (false);
          Java.Lang.Runnable r = null;
          r = new Java.Lang.Runnable (() => {
            e.Set ();
            r.Dispose ();
          });
          h.Post (r.Run);
          e.WaitOne ();
        }
        
        t.QuitSafely ();
      }
		}
	}
}
