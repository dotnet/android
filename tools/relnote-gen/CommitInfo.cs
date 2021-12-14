using System.Collections.Generic;

namespace Xamarin.Android.Tools.ReleaseNotes {

	record CommitInfo (string CommitHash) {
		public  string?         Summary;
		public  string?         PR;
		public  IList<string>   CommitMessage   = new List<string>();
		public  IList<string>   Fixes           = new List<string>();
		public  IList<string>   ReleaseNotes    = new List<string>();
	}
}
