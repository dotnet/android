//
// Copyright 2011-2012 Xamarin Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace Xamarin.Android.NUnitLite {

	[Activity (Label = "Options", WindowSoftInputMode = SoftInput.AdjustPan,
		ConfigurationChanges = ConfigChanges.KeyboardHidden | ConfigChanges.Orientation)]
	internal class OptionsActivity : Activity {
		CheckBox remote;
		TextView host_name;
		TextView host_port;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			SetContentView (Resource.Layout.options);

			Options options = AndroidRunner.Runner.Options;
			remote = FindViewById<CheckBox> (Resource.Id.OptionRemoteServer);
			remote.Checked = options.EnableNetwork;
			host_name = FindViewById<EditText> (Resource.Id.OptionHostName);
			host_name.Text = options.HostName;
			host_port = FindViewById<EditText> (Resource.Id.OptionPort);
			host_port.Text = options.HostPort.ToString ();

			base.OnCreate (bundle);
		}

		int GetPort ()
		{
			int port;
			ushort p;
			if (UInt16.TryParse (host_port.Text, out p))
				port = p;
			else
				port = -1;
			return port;
		}

		protected override void OnPause ()
		{
			Options options = AndroidRunner.Runner.Options;
			options.EnableNetwork = remote.Checked;
			options.HostName = host_name.Text;
			options.HostPort = GetPort ();
			options.Save (this);
			base.OnPause ();
		}
	}
}