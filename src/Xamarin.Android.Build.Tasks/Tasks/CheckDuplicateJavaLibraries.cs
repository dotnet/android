using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class CheckDuplicateJavaLibraries : AndroidTask
	{
		readonly static string [] ExcludedFiles = new [] {
			"classes.jar",
			"r-classes.jar",
		};

		public override string TaskPrefix => "CDJ";

		public ITaskItem [] JavaSourceFiles { get; set; }
		public ITaskItem[] JavaLibraries { get; set; }
		public ITaskItem[] LibraryProjectJars { get; set; }

		public override bool RunTask ()
		{
			var empty = Array.Empty<ITaskItem> ();
			var jarFiles = (JavaSourceFiles != null) ? JavaSourceFiles.Where (f => f.ItemSpec.EndsWith (".jar")) : null;
			if (jarFiles != null && JavaLibraries != null)
				jarFiles = jarFiles.Concat (JavaLibraries);
			else if (JavaLibraries != null)
				jarFiles = JavaLibraries;
			var jarFilePaths = (LibraryProjectJars ?? empty).Concat (jarFiles ?? empty).Select (j => j.ItemSpec);

			// Remove duplicate identical jars by name, size and content, and reject any jars that conflicts by name (i.e. different content).
			var jars = MonoAndroidHelper.DistinctFilesByContent (jarFilePaths).ToArray ();
			var dups = MonoAndroidHelper.GetDuplicateFileNames (jars, ExcludedFiles);
			if (dups.Any ()) {
				Log.LogCodedError ("XA1014", Properties.Resources.XA1014, String.Join (", ", dups.ToArray ()));
				return false;
			}

			return true;
		}
	}
}

