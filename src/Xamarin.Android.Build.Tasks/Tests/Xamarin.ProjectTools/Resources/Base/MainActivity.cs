using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
//${USINGS}

namespace ${ROOT_NAMESPACE}
{
	[Register ("${JAVA_PACKAGENAME}.MainActivity"), Activity (Label = "${PROJECT_NAME}", MainLauncher = true, Icon = "@drawable/icon")]
	//${ATTRIBUTES}
	public class MainActivity : Activity
	{
		//${FIELDS}
		int count = 1;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);

			// Get our button from the layout resource,
			// and attach an event to it
			Button button = FindViewById<Button> (Resource.Id.myButton);

			button.Click += delegate {
				button.Text = string.Format ("{0} clicks!", count++);
			};

			//${AFTER_ONCREATE}
		}
	}
	//${AFTER_MAINACTIVITY}
}


