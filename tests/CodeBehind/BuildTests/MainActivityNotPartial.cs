﻿using Android.App;
using Android.Widget;
using Android.OS;

namespace Xamarin.Android.Tests.CodeBehindBuildTests
{
	[Activity (Label = "MainActivityNotPartial")]
	public class MainActivityNotPartial : Activity
	{
		int count = 1;

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			// Set our view from the "main" layout resource
			Binding.Main items = SetContentView <Binding.Main> ();

			// Compile time: check if `myButton` is of correct type
			Button button = items.myButton;
			button.Click += delegate { button.Text = $"{count++} clicks!"; };
		}
	}
}

