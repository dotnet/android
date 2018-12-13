using System;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using System.Linq;
using System.IO;

namespace Xamarin.Android.Tasks
{
	public class DetermineJavaLibrariesToCompile : Task
	{
		[Required]
		public ITaskItem[] MonoPlatformJarPaths { get; set; }

		public bool EnableInstantRun { get; set; }

		public ITaskItem[] JavaSourceFiles { get; set; }

		public ITaskItem[] JavaLibraries { get; set; }

		public ITaskItem[] ExternalJavaLibraries { get; set; }

		public ITaskItem[] DoNotPackageJavaLibraries { get; set; }

		public ITaskItem[] LibraryProjectJars { get; set; }

		public ITaskItem[] AdditionalJavaLibraryReferences { get; set; }

		[Output]
		public ITaskItem[] JavaLibrariesToCompile { get; set; }

		[Output]
		public ITaskItem[] ReferenceJavaLibraries { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("DetermineJavaLibrariesToCompile");
			Log.LogDebugMessage ("  EnableInstantRun: {0}", EnableInstantRun);
			Log.LogDebugMessage ("  MonoPlatformJarPaths: {0}", MonoPlatformJarPaths);
			Log.LogDebugTaskItems ("  JavaSourceFiles:", JavaSourceFiles);
			Log.LogDebugTaskItems ("  JavaLibraries:", JavaLibraries);
			Log.LogDebugTaskItems ("  ExternalJavaLibraries:", ExternalJavaLibraries);
			Log.LogDebugTaskItems ("  LibraryProjectJars:", LibraryProjectJars);
			Log.LogDebugTaskItems ("  AdditionalJavaLibraryReferences:", AdditionalJavaLibraryReferences);
			Log.LogDebugTaskItems ("  DoNotPackageJavaLibraries:", DoNotPackageJavaLibraries);

			var jars = new List<ITaskItem> ();
			if (!EnableInstantRun)
				jars.AddRange (MonoPlatformJarPaths);
			if (JavaSourceFiles != null)
				foreach (var jar in JavaSourceFiles.Where (p => Path.GetExtension (p.ItemSpec) == ".jar"))
					jars.Add (jar);
			if (JavaLibraries != null)
				foreach (var jarfile in JavaLibraries)
					jars.Add (jarfile);
			if (LibraryProjectJars != null)
				foreach (var jar in LibraryProjectJars)
					if (!MonoAndroidHelper.IsEmbeddedReferenceJar (jar.ItemSpec))
						jars.Add (jar);
			if (AdditionalJavaLibraryReferences != null)
				foreach (var jar in AdditionalJavaLibraryReferences.Distinct (TaskItemComparer.DefaultComparer))
					jars.Add (jar);

			var distinct  = MonoAndroidHelper.DistinctFilesByContent (jars);
			jars          = jars.Where (j => distinct.Contains (j)).ToList ();

			JavaLibrariesToCompile = jars.Where (j => !IsExcluded (j.ItemSpec)).ToArray ();
			ReferenceJavaLibraries = (ExternalJavaLibraries ?? Enumerable.Empty<ITaskItem> ())
				.Concat (jars.Except (JavaLibrariesToCompile)).ToArray ();

			Log.LogDebugTaskItems ("  JavaLibrariesToCompile:", JavaLibrariesToCompile);
			Log.LogDebugTaskItems ("  ReferenceJavaLibraries:", ReferenceJavaLibraries);

			return true;
		}

		bool IsExcluded (string jar)
		{
			if (DoNotPackageJavaLibraries == null)
				return false;
			return DoNotPackageJavaLibraries.Any (x => Path.GetFileName (jar).EndsWith (x.ItemSpec, StringComparison.OrdinalIgnoreCase));
		}
	}

	class TaskItemComparer : IEqualityComparer<ITaskItem> {
		public static readonly TaskItemComparer     DefaultComparer     = new TaskItemComparer ();

		public bool Equals (ITaskItem a, ITaskItem b)
		{
			return string.Compare (a.ItemSpec, b.ItemSpec, StringComparison.OrdinalIgnoreCase) == 0;
		}

		public int GetHashCode (ITaskItem value)
		{
			return value.ItemSpec.GetHashCode ();
		}
	}
}

