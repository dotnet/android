using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

namespace UnnamedProject
{
	[Register("unnamedproject.unnamedproject.MainActivity"), Activity(Label = "UnnamedProject", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity
	{
		int count = 1;

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			// Get our button from the layout resource,
			// and attach an event to it
			Button button = FindViewById<Button>(Resource.Id.myButton);

			button.Click += delegate
			{
				button.Text = string.Format("{0} clicks!", count++);
			};

			string TAG = "XALINKERTESTS";

			// [Test] TryCreateInstanceOfSomeClass
			try
			{
				var asm = typeof(Library1.SomeClass).Assembly;
				// TODO: ILLink removing .ctor: https://github.com/mono/linker/issues/1633
				Android.Util.Log.Info(TAG, $"[PASS] Able to use 'typeof({typeof(Library1.SomeClass)})'.");

			}
			catch (Exception ex)
			{
				Android.Util.Log.Info(TAG, $"[FAIL] Unable to create instance of 'SomeClass'.\n{ex}");
			}

			// [Test] TryCreateInstanceOfXmlPreservedLinkerClass
			try
			{
				var asm = typeof(Library1.SomeClass).Assembly;
				var o = Activator.CreateInstance(asm.GetType("Library1.LinkerClass"));
				Android.Util.Log.Info(TAG, $"[PASS] Able to create instance of '{o.GetType().Name}'.");
			}
			catch (Exception ex)
			{
				Android.Util.Log.Info(TAG, $"[FAIL] Unable to create instance of 'LinkerClass'.\n{ex}");
			}

			// [Test] TryAccessXmlPreservedMethodOfLinkerClass
			try
			{
				var asm = typeof(Library1.SomeClass).Assembly;
				var t = asm.GetType("Library1.LinkerClass");
				var m = t.GetMethod("WasThisMethodPreserved");
				Android.Util.Log.Info(TAG, $"[PASS] Able to locate method '{m.Name}'.");
			}
			catch (Exception ex)
			{
				Android.Util.Log.Info(TAG, $"[FAIL] Unable to access 'WasThisMethodPreserved ()' method of 'LinkerClass'.\n{ex}");
			}

			// [Test] TryAccessAttributePreservedMethodOfLinkerClass
			try
			{
				var asm = typeof(Library1.SomeClass).Assembly;
				var t = asm.GetType("Library1.LinkerClass");
				var m = t.GetMethod("PreserveAttribMethod");
				Android.Util.Log.Info(TAG, $"[PASS] Able to locate method '{m.Name}'.");
			}
			catch (Exception ex)
			{
				Android.Util.Log.Info(TAG, $"[FAIL] Unable to access 'PreserveAttribMethod ()' method of 'LinkerClass'.\n{ex}");
			}

			// [Test] TryAccessXmlPreservedFieldOfLinkerClass
			try
			{
				var asm = typeof(Library1.SomeClass).Assembly;
				var t = asm.GetType("Library1.LinkerClass");
				var m = t.GetProperty("IsPreserved");
				Android.Util.Log.Info(TAG, $"[PASS] Able to locate field '{m.Name}'.");
			}
			catch (Exception ex)
			{
				Android.Util.Log.Info(TAG, $"[FAIL] Unable to access 'IsPreserved' field of 'LinkerClass'.\n{ex}");
			}

			// [Test] TryCreateInstanceOfNonXmlPreservedClass
			try
			{
				var asm = typeof(Library1.SomeClass).Assembly;
				var o = Activator.CreateInstance(asm.GetType("Library1.NonPreserved"));
				Android.Util.Log.Info(TAG, $"[LINKALLFAIL] Able to create instance of '{o.GetType().Name}' which should have been linked away.");
			}
			catch (Exception ex)
			{
				Android.Util.Log.Info(TAG, $"[LINKALLPASS] Unable to create instance of 'NonPreserved' as expected.\n{ex}");
			}

			// [Test] Post
			Android.Util.Log.Info(TAG, HttpClientTest.Post ());

			// [Test] MethodsArePreserved
			Android.Util.Log.Info (TAG, PreserveTest.MethodsArePreserved ());

			// [Test] TextChanged
			Android.Util.Log.Info (TAG, MaterialTextChanged.TextChanged (this));

			var cldt = new CustomLinkerDescriptionTests();
			Android.Util.Log.Info(TAG, cldt.TryAccessNonXmlPreservedMethodOfLinkerModeFullClass());
			Android.Util.Log.Info(TAG, LinkTestLib.Bug21578.MulticastOption_ShouldNotBeStripped());
			Android.Util.Log.Info(TAG, LinkTestLib.Bug21578.MulticastOption_ShouldNotBeStripped2());
			Android.Util.Log.Info(TAG, LinkTestLib.Bug35195.AttemptCreateTable());
			Android.Util.Log.Info(TAG, "All regression tests completed.");
		}
	}

	public class CustomLinkerDescriptionTests
	{
		Type t = typeof(Library1.LinkModeFullClass);

		// [Test]
		public string TryAccessNonXmlPreservedMethodOfLinkerModeFullClass()
		{
			try
			{
				System.Reflection.MethodInfo m = t.GetMethod("ThisMethodShouldNotBePreserved");
				if (m == null)
					return $"[LINKALLPASS] Was unable to locate 'ThisMethodShouldNotBePreserved ()' method of 'LinkModeFullClass' as expected.";
				else
					return $"[LINKALLFAIL] Able to locate method that should have been linked: '{m.Name}'.";
			}
			catch (Exception ex)
			{
				return $"[LINKALLFAIL] Unexpected exception thrown attempting to locate 'ThisMethodShouldNotBePreserved' method of 'LinkModeFullClass'.\n{ex}";
			}
		}

	}
}
