using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using Xamarin.Tools.Zip;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class CreateLibraryResourceArchive : AndroidTask
	{
		public override string TaskPrefix => "CLR";

		[Required]
		public string OutputDirectory { get; set; }

		[Required]
		public string OutputJarsDirectory { get; set; }

		[Required]
		public string OutputAnnotationsDirectory { get; set; }

		[Required]
		public ITaskItem[] LibraryProjectPropertiesFiles { get; set; }

		[Required]
		public ITaskItem[] LibraryProjectZipFiles { get; set; }
		
		public CreateLibraryResourceArchive ()
		{
		}

		public override bool RunTask ()
		{
			if (LibraryProjectPropertiesFiles.Length == 0 && LibraryProjectZipFiles.Length == 0)
				return true;
			
			var outDirInfo = new DirectoryInfo (OutputDirectory);
			
			// Copy files into _LibraryProjectImportsDirectoryName (library_project_imports) dir.
			if (!outDirInfo.Exists)
				outDirInfo.Create ();
			
			var projectsResolved = ResolveLibraryProjectReferences (LibraryProjectPropertiesFiles.Select (p => Path.GetFullPath (p.ItemSpec)));
			var imports = projectsResolved.Concat (LibraryProjectZipFiles.Select (p => p.ItemSpec));
			
			foreach (var p in imports) {
				// note that imports could contain file name that neither of build items contains
				// (it may be one of those resolved references in project.properties).
				// Also non-zip files are now specified in full path.
				if (!LibraryProjectZipFiles.Any (l => l.ItemSpec == p)) {
					// project.properties

					var fileInfo = new FileInfo (p);
					if (!fileInfo.Exists)
						throw new InvalidOperationException (String.Format ("Library project properties file '{0}' does not exist.", p));
					var bindir = fileInfo.Directory.FullName;
					
					CopyLibraryContent (bindir, false);
				} else {
					// zip
					string tmpname = Path.Combine (Path.GetTempPath (), "monodroid_import_" + Guid.NewGuid ().ToString ());
					try {
						Directory.CreateDirectory (tmpname);
						var archive = ZipArchive.Open (p, FileMode.Open);
						archive.ExtractAll (tmpname);

						if (!CopyLibraryContent (tmpname, p.EndsWith (".aar", StringComparison.OrdinalIgnoreCase)))
							return false;
					} finally {
						Directory.Delete (tmpname, true);
					}
				}
			}

			var outpath = Path.Combine (outDirInfo.Parent.FullName, "__AndroidLibraryProjects__.zip");

			if (Files.ArchiveZip (outpath, f => {
				using (var zip = new ZipArchiveEx (f)) {
					zip.AddDirectory (OutputDirectory, "library_project_imports");
				}
			})) {
				Log.LogDebugMessage ("Saving contents to " + outpath);
			}
			return true;
		}
			
		bool CopyLibraryContent (string projdir, bool isAar)
		{
			if (Path.GetFullPath (OutputDirectory).StartsWith (Path.GetFullPath (projdir), StringComparison.InvariantCultureIgnoreCase)) {
				Log.LogCodedError ("XA1019", Properties.Resources.XA1019, Path.GetFullPath (projdir));
				return false;
			}
			foreach (var subdir in Directory.GetDirectories (projdir)) {
				var dinfo = new DirectoryInfo (subdir);
				switch (dinfo.Name.ToLowerInvariant ()) {
				case "gen":
				case "src":
					continue;
				}
				CopyDirectory (dinfo, OutputDirectory, true);
				CopyFiles (subdir);
			}
			if (isAar) {
				CopyFiles (projdir);
				// for .aar, copy top-level files (*.jar, AndroidManifest.xml etc.) into bin too.
				var dstdir = Path.Combine (OutputDirectory, "bin");
				foreach (var file in Directory.GetFiles (projdir)) {
					string dstpath = Path.Combine (dstdir, Path.GetFileName (file));
					if (!File.Exists (dstpath))
						MonoAndroidHelper.CopyIfChanged (file, dstpath);
				}
			}
			return true;
		}

		void CopyFiles (string srcdir)
		{
			foreach (var file in Directory.GetFiles (srcdir)) {
				if (file.EndsWith (".jar", StringComparison.OrdinalIgnoreCase)) {
					var dstpath = Path.Combine (OutputJarsDirectory, Path.GetFileName (file));
					if (!File.Exists (dstpath))
						MonoAndroidHelper.CopyIfChanged (file, dstpath);
				} else if (file.EndsWith ("annotations.zip", StringComparison.OrdinalIgnoreCase)) {
					var dstpath = Path.Combine (OutputAnnotationsDirectory, Path.GetFileName (file));
					if (!File.Exists (dstpath))
						MonoAndroidHelper.CopyIfChanged (file, dstpath);
				} else {
					var dstpath = Path.Combine (OutputDirectory, Path.GetFileName (file));
					if (!File.Exists (dstpath))
						MonoAndroidHelper.CopyIfChanged (file, dstpath);
				}
			}
		}
		
		static void CopyDirectory (DirectoryInfo srcSubdirInfo, string dst, bool top)
		{
			string dstsub = Path.Combine (dst, srcSubdirInfo.Name);
			if (!Directory.Exists (dstsub))
				Directory.CreateDirectory (dstsub);
			foreach (var subsub in srcSubdirInfo.GetDirectories ()) {
				// Skip "classes" dir.
				if (top && subsub.Name.ToLowerInvariant () == "classes")
					continue;
				CopyDirectory (subsub, dstsub, false);
			}
			foreach (var file in srcSubdirInfo.GetFiles ())
				MonoAndroidHelper.CopyIfChanged (file.FullName, Path.Combine (dstsub, file.Name));
		}
		
		IEnumerable<string> ResolveLibraryProjectReferences (IEnumerable<string> inputs)
		{
			var l = new List<string> ();
			foreach (var input in inputs)
				foreach (var s in ResolveLibraryProjectReferences (input, new List<string> ()))
						if (!l.Contains (s))
							l.Add (s);
			return l;
		}
		
		const string librefspec = "android.library.reference.";
		
		IEnumerable<string> ResolveLibraryProjectReferences (string singleFile, List<string> recurse)
		{
			if (recurse.Contains (singleFile))
				throw new InvalidOperationException (String.Format ("The library project '{0}' contains invalid recursive references.", singleFile));
			recurse.Add (singleFile);
			var l = new List<string> ();
			l.Add (singleFile);
			var ordered = new SortedList<int,string> ();
			foreach (var line in File.ReadAllLines (singleFile)) {
				var s = line.Trim ();
				if (s.StartsWith ("#"))
					continue;
				if (!s.StartsWith (librefspec))
					continue;
				int eqpos = s.IndexOf ('=');
				if (eqpos < 0)
					throw new InvalidOperationException (String.Format ("Wrong project reference description in '{0}': '=' is missing", singleFile));
				string numspec = s.Substring (librefspec.Length, eqpos - librefspec.Length);
				int num;
				if (!int.TryParse (numspec, out num))
					throw new InvalidOperationException (String.Format ("Wrong project reference description in '{0}': wrong number format for reference priority", singleFile));
				string path = s.Substring (eqpos + 1);
				string refpath = Path.GetFullPath (Path.Combine (Path.GetDirectoryName (singleFile), path, "project.properties"));
				ordered.Add (num, refpath);
			}
			foreach (var refpath in ordered.Values)
				foreach (var sub in ResolveLibraryProjectReferences (refpath, recurse))
					l.Add (sub);
			recurse.Remove (singleFile);
			return l;
		}
	}
}

