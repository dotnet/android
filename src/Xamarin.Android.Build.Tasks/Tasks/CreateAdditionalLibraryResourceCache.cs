using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	public class CreateAdditionalLibraryResourceCache : Task
	{

		[Required]
		public ITaskItem[] AdditionalAndroidResourcePaths { get; set; }

		[Required]
		public ITaskItem[] AdditionalAndroidResourceCachePaths { get; set; }

		[Output]
		public ITaskItem[] CopiedResources { get; set; }


		public override bool Execute ()
		{
			Log.LogDebugMessage ("CreateAdditionalLibraryResourceCache Task");
			Log.LogDebugTaskItems ("  AdditionalAndroidResourcePaths:", AdditionalAndroidResourcePaths);
			Log.LogDebugTaskItems ("  AdditionalAndroidResourceCachePaths: ", AdditionalAndroidResourceCachePaths);
			var copiedResources = new List<ITaskItem> ();

			for (int i = 0; i < AdditionalAndroidResourcePaths.Length; i++) {
				var src = Path.GetFullPath (AdditionalAndroidResourcePaths [i].ItemSpec);
				var dest = Path.GetFullPath (AdditionalAndroidResourceCachePaths [i].ItemSpec);

				foreach (string dirPath in Directory.EnumerateDirectories (src, "*", SearchOption.AllDirectories))
					Directory.CreateDirectory (dirPath.Replace (src, dest));

				//Copy all the files & Replaces any files with the same name
				foreach (string newPath in Directory.EnumerateFiles (src, "*", SearchOption.AllDirectories)) {
					var destPath = newPath.Replace (src, dest);
					var cachedDate = File.GetLastWriteTimeUtc (src);
					MonoAndroidHelper.CopyIfChanged (newPath, destPath);
					copiedResources.Add (new TaskItem (destPath));
				}
			}

			CopiedResources = copiedResources.ToArray ();

			Log.LogDebugTaskItems ("  [Output] CopiedResources:", CopiedResources);
			return !Log.HasLoggedErrors;
		}
	}
}

