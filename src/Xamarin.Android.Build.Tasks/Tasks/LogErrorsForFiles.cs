using System;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	public class LogErrorsForFiles : Task
	{
		[Required]
		public ITaskItem[] Files { get; set; }

		[Required]
		public string Code { get; set; }

		[Required]
		public string Text { get; set; }

		public string SubCategory { get; set; }

		public string HelpKeyword { get; set; }

		public override bool Execute ()
		{
			foreach (var item in Files) {
				Log.LogError (SubCategory, Code, HelpKeyword, item.ItemSpec
					, 0, 0, 0, 0, Text);
			}
			return !Log.HasLoggedErrors;
		}
	}
}

