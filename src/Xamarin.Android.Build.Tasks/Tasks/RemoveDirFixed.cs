//
// RemoveDir.cs: Removes a directory.
//
// Author:
//   Marek Sieradzki (marek.sieradzki@gmail.com)
// 
// (C) 2005 Marek Sieradzki
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class RemoveDirFixed : AndroidTask
	{
		public override string TaskPrefix => "RDF";

		public override bool RunTask ()
		{
			var temporaryRemovedDirectories = new List<ITaskItem> (Directories.Length);

			foreach (var directory in Directories) {
				var fullPath = directory.GetMetadata ("FullPath");
				if (!Directory.Exists (fullPath)) {
					Log.LogDebugMessage ($"Directory did not exist: {fullPath}");
					continue;
				}
				try {
					// try to do a normal "fast" delete of the directory.
					Directory.Delete (fullPath, true);
					temporaryRemovedDirectories.Add (directory);
				} catch (UnauthorizedAccessException ex) {
					// if that fails we probably have readonly files (or locked files)
					// so try to make them writable and try again.
					try {
						MonoAndroidHelper.SetDirectoryWriteable (fullPath);
						Directory.Delete (fullPath, true);
						temporaryRemovedDirectories.Add (directory);
					} catch (Exception inner) {
						Log.LogUnhandledException (TaskPrefix, ex);
						Log.LogUnhandledException (TaskPrefix, inner);
					}
				} catch (DirectoryNotFoundException ex) {
					// This could be a file inside the directory over MAX_PATH.
					// We can attempt using the \\?\ syntax.
					if (OS.IsWindows) {
						try {
							fullPath = Files.ToLongPath (fullPath);
							Log.LogDebugMessage ("Trying long path: " + fullPath);
							Directory.Delete (fullPath, true);
							temporaryRemovedDirectories.Add (directory);
						} catch (Exception inner) {
							Log.LogUnhandledException (TaskPrefix, ex);
							Log.LogUnhandledException (TaskPrefix, inner);
						}
					} else {
						Log.LogUnhandledException (TaskPrefix, ex);
					}
				} catch (Exception ex) {
					Log.LogUnhandledException (TaskPrefix, ex);
				}
			}

			RemovedDirectories = temporaryRemovedDirectories.ToArray ();
			Log.LogDebugTaskItems ("  RemovedDirectories: ", RemovedDirectories);

			return !Log.HasLoggedErrors;
		}

		[Required]
		public ITaskItem [] Directories { get; set; }

		[Output]
		public ITaskItem [] RemovedDirectories { get; set; }
	}
}
