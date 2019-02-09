// 
// AndroidUpdateResDir.cs
//  
// Author:
//       Michael Hutchinson <mhutchinson@novell.com>
// 
// Copyright (c) 2010 Novell, Inc. (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tasks
{
	public class AndroidComputeResPaths : Task
	{
		[Required]
		public ITaskItem[] ResourceFiles { get; set; }
		
		[Required]
		public string IntermediateDir { get; set; }
		
		public string Prefixes { get; set; }

		public bool LowercaseFilenames { get; set; }

		public string ProjectDir { get; set; }
		
		[Output]
		public ITaskItem[] IntermediateFiles { get; set; }

		[Output]
		public ITaskItem [] ResolvedResourceFiles { get; set; }
		
		[Output]
		public string ResourceNameCaseMap { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("  IntermediateDir: {0}", IntermediateDir);
			Log.LogDebugMessage ("  Prefixes: {0}", Prefixes);
			Log.LogDebugMessage ("  ProjectDir: {0}", ProjectDir);
			Log.LogDebugMessage ("  LowercaseFilenames: {0}", LowercaseFilenames);
			Log.LogDebugTaskItems ("  ResourceFiles:", ResourceFiles);

			var intermediateFiles = new List<ITaskItem> ();
			var resolvedFiles = new List<ITaskItem> ();
			
			string[] prefixes = Prefixes != null ? Prefixes.Split (';') : null;
			if (prefixes != null) {
				for (int i = 0; i < prefixes.Length; i++) {
					string p = prefixes [i];
					char c = p [p.Length - 1];
					if (c != '\\' && c != '/')
						prefixes [i] = p + Path.DirectorySeparatorChar;
				}
			}

			var nameCaseMap = new StringWriter ();

			for (int i = 0; i < ResourceFiles.Length; i++) {
				var item = ResourceFiles [i];

				if (Directory.Exists (item.ItemSpec))
					continue;
				//compute the target path
				string rel;
				var logicalName = item.GetMetadata ("LogicalName").Replace ('\\', Path.DirectorySeparatorChar);
				if (item.GetMetadata ("IsWearApplicationResource") == "True") {
					rel = item.ItemSpec.Substring (IntermediateDir.Length);
				} else if (!string.IsNullOrEmpty (logicalName)) {
					rel = logicalName;
				} else {
					rel = item.GetMetadata ("Link").Replace ('\\', Path.DirectorySeparatorChar);
					if (string.IsNullOrEmpty (rel)) {
						rel = item.GetMetadata ("Identity");
						if (!string.IsNullOrEmpty (ProjectDir)) {
							var fullRelPath = Path.GetFullPath (rel).Normalize (NormalizationForm.FormC);
							var fullProjectPath = Path.GetFullPath (ProjectDir).Normalize (NormalizationForm.FormC);
							if (fullRelPath.StartsWith (fullProjectPath)) {
								rel = fullRelPath.Replace (fullProjectPath, string.Empty);
							}
						}
					}
					if (prefixes != null) {
						foreach (var p in prefixes) {
							if (rel.StartsWith (p))
								rel = rel.Substring (p.Length);
						}
					}
				}

				string baseFileName = LowercaseFilenames ? rel.ToLowerInvariant () : rel;
				if (Path.GetExtension (baseFileName) == ".axml")
					baseFileName = Path.ChangeExtension (baseFileName, ".xml");
				if (baseFileName != rel)
					nameCaseMap.WriteLine ("{0}|{1}", rel, baseFileName);
				string dest = Path.GetFullPath (Path.Combine (IntermediateDir, baseFileName));
				var newItem = new TaskItem (dest);
				newItem.SetMetadata ("LogicalName", rel);
				item.CopyMetadataTo (newItem);
				intermediateFiles.Add (newItem);
				resolvedFiles.Add (item);
			}

			IntermediateFiles = intermediateFiles.ToArray ();
			ResolvedResourceFiles = resolvedFiles.ToArray ();
			ResourceNameCaseMap = nameCaseMap.ToString ().Replace (nameCaseMap.NewLine, ";");
			Log.LogDebugTaskItems ("  IntermediateFiles:", IntermediateFiles);
			Log.LogDebugTaskItems ("  ResolvedResourceFiles:", ResolvedResourceFiles);
			Log.LogDebugTaskItems ("  ResourceNameCaseMap:", ResourceNameCaseMap);
			return true;
		}
	}
	
	public class RemoveUnknownFiles : Task
	{
		static bool IsWindows = Path.DirectorySeparatorChar == '\\';

		[Required]
		public ITaskItem[] Files { get; set; }
		
		[Required]
		public string Directory { get; set; }
		
		public bool RemoveDirectories { get; set; }

		[Output]
		public ITaskItem[] RemovedFiles { get; set; }

		[Output]
		public ITaskItem [] RemovedDirectories { get; set; }
		
		public override bool Execute ()
		{
			Log.LogDebugMessage ("RemoveUnknownFiles Task");
			Log.LogDebugTaskItems ("Files", Files);
			Log.LogDebugMessage ($"Directory {Directory}");
			Log.LogDebugMessage ($"RemoveDirectories {RemoveDirectories}");

			var absDir = Path.GetFullPath (Directory);
			
			HashSet<string> knownFiles;
			List<ITaskItem> removedFiles = new List<ITaskItem> ();
			List<ITaskItem> removedDirectories = new List<ITaskItem> ();
			// Do a case insensitive compare on windows, because the file
			// system is case insensitive [Bug #645833]
			if (IsWindows)
				knownFiles = new HashSet<string> (Files.Select (f => f.GetMetadata ("FullPath")), StringComparer.InvariantCultureIgnoreCase);
			else
				knownFiles = new HashSet<string> (Files.Select (f => f.GetMetadata ("FullPath")));

			var files = System.IO.Directory.GetFiles (absDir, "*", SearchOption.AllDirectories);
			foreach (string f in files)
				if (!knownFiles.Contains (f)) {
					Log.LogDebugMessage ("Deleting File {0}", f);
					var item = new TaskItem (f.Replace (absDir, "res" + Path.DirectorySeparatorChar));
					removedFiles.Add (item);
					MonoAndroidHelper.SetWriteable (f);
					File.Delete (f);
				}
			
			if (RemoveDirectories) {
				var knownDirs = new HashSet<string> (knownFiles.Select (d => Path.GetDirectoryName (d)));
				var dirs = System.IO.Directory.GetDirectories (absDir, "*", SearchOption.AllDirectories);

				foreach (string d in dirs.OrderByDescending (s => s.Length))
					if (!knownDirs.Contains (d) && IsDirectoryEmpty (d)) {
						Log.LogDebugMessage ("Deleting Directory {0}", d);
						removedDirectories.Add (new TaskItem(d));
						MonoAndroidHelper.SetDirectoryWriteable (d);
						System.IO.Directory.Delete (d);
					}
			}

			RemovedFiles = removedFiles.ToArray ();
			RemovedDirectories = removedDirectories.ToArray ();
			Log.LogDebugTaskItems ("[Output] RemovedFiles", RemovedFiles);
			Log.LogDebugTaskItems ("[Output] RemovedDirectories", RemovedDirectories);
			return true;
		}

		// We are having issues with trees like this:
		// - /Assets
		//   - /test
		//     - /test2
		//       - myasset.txt
		// /test is not in known directories, so we are trying to delete it,
		// even though we need it because of its subdirectories
		// [Bug #654535]
		private bool IsDirectoryEmpty (string dir)
		{
			if (System.IO.Directory.GetFiles (dir).Length != 0)
				return false;

			if (System.IO.Directory.GetDirectories (dir).Length != 0)
				return false;

			return true;
		}
	}
}

