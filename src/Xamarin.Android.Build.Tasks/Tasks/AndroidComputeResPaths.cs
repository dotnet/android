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
	public class AndroidComputeResPaths : AndroidTask
	{
		public override string TaskPrefix => "CRP";

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

		public override bool RunTask ()
		{
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
				}

				if (Path.IsPathRooted (rel)) {
					var root = Path.GetPathRoot (rel);
					rel = rel.Substring (root.Length);
				}

				if (prefixes != null) {
					foreach (var p in prefixes) {
						if (rel.StartsWith (p))
							rel = rel.Substring (p.Length);
					}
				}

				string baseFileName = LowercaseFilenames ? rel.ToLowerInvariant () : rel;
				if (Path.GetExtension (baseFileName) == ".axml")
					baseFileName = Path.ChangeExtension (baseFileName, ".xml");
				if (baseFileName != rel)
					nameCaseMap.WriteLine ("{0}|{1}", rel, baseFileName);
				string dest = Path.GetFullPath (Path.Combine (IntermediateDir, baseFileName));
				string intermediateDirFullPath = Path.GetFullPath (IntermediateDir);
				// if the path ends up "outside" of our target intermediate directory, just use the filename
				if (String.Compare (intermediateDirFullPath, 0, dest, 0, intermediateDirFullPath.Length, StringComparison.OrdinalIgnoreCase) != 0) {
					dest = Path.GetFullPath (Path.Combine (IntermediateDir, Path.GetFileName (baseFileName)));
				}
				if (!File.Exists (item.ItemSpec)) {
					Log.LogCodedError ("XA2001", file: item.ItemSpec, lineNumber: 0, message: Properties.Resources.XA2001, item.ItemSpec);
					continue;
				}
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
			return !Log.HasLoggedErrors;
		}
	}
}

