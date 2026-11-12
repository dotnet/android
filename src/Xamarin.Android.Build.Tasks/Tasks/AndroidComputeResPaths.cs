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

#nullable enable

using System;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	[MSBuildMultiThreadableTask]
	public class AndroidComputeResPaths : MultithreadedAndroidTask
	{
		public override string TaskPrefix => "CRP";

		[Required]
		public ITaskItem[] ResourceFiles { get; set; } = [];

		[Required]
		public string IntermediateDir { get; set; } = "";

		public string? AssetPackIntermediateDir { get; set; }

		public string? Prefixes { get; set; }

		[Required]
		public string PrefixProperty { get; set; } = "";

		public bool LowercaseFilenames { get; set; }

		public string? ProjectDir { get; set; }

		public string? AndroidLibraryFlatFilesDirectory { get; set; }

		[Output]
		public ITaskItem[]? IntermediateFiles { get; set; }

		[Output]
		public ITaskItem []? ResolvedResourceFiles { get; set; }

		[Output]
		public string? FilesHash { get; set; }

		// Generated logical name as identity, original logical name as OriginalName metadata.
		[Output]
		public ITaskItem [] ResourceCaseMap { get; set; } = [];

		public override bool RunTask ()
		{
			var intermediateFiles = new List<ITaskItem> (ResourceFiles.Length);
			var resolvedFiles = new List<ITaskItem> (ResourceFiles.Length);

			string[]? prefixes = Prefixes != null ? Prefixes.Split (';') : null;
			if (prefixes != null) {
				for (int i = 0; i < prefixes.Length; i++) {
					string p = prefixes [i];
					if (Path.IsPathRooted (p)) {
						Log.LogCodedError ("XA1041", message: Properties.Resources.XA1041, PrefixProperty, p);
						continue;
					}
					char c = p [p.Length - 1];
					if (c != '\\' && c != '/')
						prefixes [i] = p + Path.DirectorySeparatorChar;
				}
			}

			var nameCaseMap = new Dictionary<string, string> (ResourceFiles.Length, StringComparer.Ordinal);
			var sb = new StringBuilder ();

			for (int i = 0; i < ResourceFiles.Length; i++) {
				var item = ResourceFiles [i];

				var source = TaskEnvironment.GetAbsolutePath (item.ItemSpec);
				if (Directory.Exists (source))
					continue;
				//compute the target path
				string rel;
				var assetPack = item.GetMetadata ("AssetPack");
				var logicalName = item.GetMetadata ("LogicalName").Replace ('\\', Path.DirectorySeparatorChar);
				if (item.GetMetadata ("IsWearApplicationResource") == "True") {
					rel = item.ItemSpec.Substring (IntermediateDir.Length);
				} else if (!logicalName.IsNullOrEmpty ()) {
					rel = logicalName;
				} else {
					rel = item.GetMetadata ("Link").Replace ('\\', Path.DirectorySeparatorChar);
					if (rel.IsNullOrEmpty ()) {
						rel = item.GetMetadata ("Identity");
						if (!ProjectDir.IsNullOrEmpty ()) {
							var fullRelPath = TaskEnvironment.GetAbsolutePath (rel).GetCanonicalForm ().Value.Normalize (NormalizationForm.FormC);
							var fullProjectPath = TaskEnvironment.GetAbsolutePath (ProjectDir).GetCanonicalForm ().Value.Normalize (NormalizationForm.FormC);
							if (fullRelPath.StartsWith (fullProjectPath, StringComparison.OrdinalIgnoreCase)) {
								rel = fullRelPath.Replace (fullProjectPath, string.Empty);
							}
						}
					}
				}

				if (Path.IsPathRooted (rel)) {
					var root = Path.GetPathRoot (rel) ?? throw new ArgumentException ("A rooted resource path must have a root.", nameof (rel));
					rel = rel.Substring (root.Length);
				}

				if (prefixes != null) {
					foreach (var p in prefixes) {
						if (rel.StartsWith (p, StringComparison.OrdinalIgnoreCase))
							rel = rel.Substring (p.Length);
					}
				}

				string baseFileName = LowercaseFilenames ? rel.ToLowerInvariant () : rel;
				if (Aapt2.IsInvalidFilename (baseFileName)) {
					Log.LogDebugMessage ($"Invalid filename, ignoring: {baseFileName}");
					continue;
				}
				if (Path.GetExtension (baseFileName) == ".axml")
					baseFileName = Path.ChangeExtension (baseFileName, ".xml");
				if (baseFileName != rel) {
					nameCaseMap [baseFileName] = rel;
				}
				string dest = TaskEnvironment.GetAbsolutePath (Path.Combine (IntermediateDir, baseFileName)).GetCanonicalForm ();
				string intermediateDirFullPath = TaskEnvironment.GetAbsolutePath (IntermediateDir).GetCanonicalForm ();
				if (!assetPack.IsNullOrEmpty () &&
						!string.Equals (assetPack, "base", StringComparison.OrdinalIgnoreCase) &&
						!AssetPackIntermediateDir.IsNullOrEmpty ()) {
					dest = TaskEnvironment.GetAbsolutePath (Path.Combine (AssetPackIntermediateDir, assetPack, "assets", baseFileName)).GetCanonicalForm ();
					intermediateDirFullPath = TaskEnvironment.GetAbsolutePath (AssetPackIntermediateDir).GetCanonicalForm ();
				}
				
				// if the path ends up "outside" of our target intermediate directory, just use the filename
				if (!dest.StartsWith (intermediateDirFullPath, StringComparison.OrdinalIgnoreCase)) {
					dest = TaskEnvironment.GetAbsolutePath (Path.Combine (IntermediateDir, Path.GetFileName (baseFileName))).GetCanonicalForm ();
				}
				if (!File.Exists (source)) {
					Log.LogCodedError ("XA2001", file: item.ItemSpec, lineNumber: 0, message: Properties.Resources.XA2001, item.ItemSpec);
					continue;
				}
				var newItem = new TaskItem (dest);
				newItem.SetMetadata ("LogicalName", rel);
				newItem.SetMetadata ("_FlatFile", Monodroid.AndroidResource.CalculateAapt2FlatArchiveFileName (dest));
				newItem.SetMetadata ("_ArchiveDirectory", AndroidLibraryFlatFilesDirectory);
				item.CopyMetadataTo (newItem);
				intermediateFiles.Add (newItem);
				resolvedFiles.Add (item);
				// write both files so we handle changes in destination also
				sb.AppendLine ($"{item.ItemSpec};{newItem.ItemSpec}");
			}

			IntermediateFiles = intermediateFiles.ToArray ();
			ResolvedResourceFiles = resolvedFiles.ToArray ();
			FilesHash = Files.HashString (sb.ToString ());
			// Escape logical keys without normalizing them as physical paths.
			ITaskItem2 escapedName = new TaskItem ("");
			ResourceCaseMap = nameCaseMap.Select (entry => {
				escapedName.SetMetadataValueLiteral ("Name", entry.Key);
				var item = new TaskItem (escapedName.GetMetadataValueEscaped ("Name"), treatAsFilePath: false);
				((ITaskItem2) item).SetMetadataValueLiteral ("OriginalName", entry.Value);
				return (ITaskItem) item;
			}).ToArray ();
			return !Log.HasLoggedErrors;
		}
	}
}
