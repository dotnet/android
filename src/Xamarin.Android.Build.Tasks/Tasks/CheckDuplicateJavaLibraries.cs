using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class CheckDuplicateJavaLibraries : Task
	{
		public ITaskItem[] JavaSourceFiles { get; set; }
		public ITaskItem[] JavaLibraries { get; set; }		
		public ITaskItem[] LibraryProjectJars { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugTaskItems ("  JavaSourceFiles:", JavaSourceFiles);
			Log.LogDebugTaskItems ("  JavaLibraries:", JavaLibraries);
			Log.LogDebugTaskItems ("  LibraryProjectJars:", LibraryProjectJars);

			var jarFiles = (JavaSourceFiles != null) ? JavaSourceFiles.Where (f => f.ItemSpec.EndsWith (".jar")) : null;
			if (jarFiles != null && JavaLibraries != null)
				jarFiles = jarFiles.Concat (JavaLibraries);
			else if (JavaLibraries != null)
				jarFiles = JavaLibraries;
			var jarFilePaths = (LibraryProjectJars ?? new ITaskItem [0]).Concat (jarFiles ?? new ITaskItem [0]).Select (j => j.ItemSpec);

			// Remove duplicate identical jars by name, size and content, and reject any jars that conflicts by name (i.e. different content).
			var jars = MonoAndroidHelper.DistinctFilesByContent (jarFilePaths).ToArray ();
			var dups = MonoAndroidHelper.GetDuplicateFileNames (jars, new string [] {"classes.jar"});
			if (dups.Any ()) {
				Log.LogError ("You have Jar libraries, {0}, that have the identical name with inconsistent file contents. Please make sure to remove any conflicting libraries in EmbeddedJar, InputJar and AndroidJavaLibrary.", String.Join (", ", dups.ToArray ()));
				return false;
			}
			
			return true;
		}
	}
}

