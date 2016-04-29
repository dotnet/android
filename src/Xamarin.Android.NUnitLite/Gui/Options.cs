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
using Android.OS;

namespace Xamarin.Android.NUnitLite {

	internal class Options {

		public Options ()
		{
		}

		// Options are not as useful as under iOS since re-installing the
		// application deletes the file containing them.
		internal Options (Activity activity)
		{
			ISharedPreferences prefs = activity.GetSharedPreferences ("options", FileCreationMode.Private);
			EnableNetwork = prefs.GetBoolean ("remote", false);
			HostName = prefs.GetString ("hostName", "0.0.0.0");
			HostPort = prefs.GetInt ("hostPort", -1);
		}

		public bool EnableNetwork { get; set; }

		public string HostName { get; set; }

		public int HostPort { get; set; }

		public bool ShowUseNetworkLogger {
			get { return (EnableNetwork && !String.IsNullOrWhiteSpace (HostName) && (HostPort > 0)); }
		}

		public void LoadFromBundle (Bundle bundle)
		{
			EnableNetwork = bundle.GetBoolean ("remote", false);
			HostName      = bundle.GetString ("hostName") ?? "0.0.0.0";
			HostPort      = bundle.GetInt ("hostPort", -1);
		}

		public void Save (Activity activity)
		{
			ISharedPreferences prefs = activity.GetSharedPreferences ("options", FileCreationMode.Private);
			var edit = prefs.Edit ();
			edit.PutBoolean ("remote", EnableNetwork);
			edit.PutString ("hostName", HostName);
			edit.PutInt ("hostPort", HostPort);
			edit.Commit ();
		}
	}
}