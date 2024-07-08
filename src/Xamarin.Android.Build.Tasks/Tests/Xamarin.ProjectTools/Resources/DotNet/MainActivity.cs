//${USINGS}
namespace ${ROOT_NAMESPACE}
{
	[Android.Runtime.Register ("${JAVA_PACKAGENAME}.MainActivity"), Activity (Label = "${PROJECT_NAME}", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity
	{
		//${FIELDS}
		int count = 1;

		protected override void OnCreate (Bundle? bundle)
		{
			base.OnCreate (bundle);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);

			var button = FindViewById<Button> (Resource.Id.myButton);
			button!.Click += delegate {
				button.Text = string.Format ("{0} clicks!", count++);
			};

			//${AFTER_ONCREATE}
		}
	}
	//${AFTER_MAINACTIVITY}
}
