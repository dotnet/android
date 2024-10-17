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
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class RemoveDirFixed : AndroidTask
	{
		public override string TaskPrefix => "RDF";

		const int DEFAULT_DIRECTORY_DELETE_RETRY_DELAY_MS = 1000;
		const int DEFAULT_REMOVEDIRFIXED_RETRIES = 10;
		const int ERROR_ACCESS_DENIED = -2147024891;
		const int ERROR_SHARING_VIOLATION = -2147024864;

		public override bool RunTask ()
		{
			var temporaryRemovedDirectories = new List<ITaskItem> (Directories.Length);

			foreach (var directory in Directories) {
				var fullPath = directory.GetMetadata ("FullPath");
				if (!Directory.Exists (fullPath)) {
					Log.LogDebugMessage ($"Directory did not exist: {fullPath}");
					continue;
				}
				int retryCount = 0;
				try {
					while (retryCount <= DEFAULT_REMOVEDIRFIXED_RETRIES) {
						try {
							// try to do a normal "fast" delete of the directory.
							// only do the set writable on the second attempt
							if (retryCount == 1)
								Files.SetDirectoryWriteable (fullPath);
							Directory.Delete (fullPath, true);
							temporaryRemovedDirectories.Add (directory);
							break;
						} catch (Exception e) {
							switch (e) {
								case DirectoryNotFoundException:
									if (OS.IsWindows) {
										fullPath = Files.ToLongPath (fullPath);
										Log.LogDebugMessage ("Trying long path: " + fullPath);
										break;
									}
									throw;
								case UnauthorizedAccessException:
								case IOException:
									int code = Marshal.GetHRForException(e);
									if ((code != ERROR_ACCESS_DENIED && code != ERROR_SHARING_VIOLATION) || retryCount == DEFAULT_REMOVEDIRFIXED_RETRIES) {
										throw;
									};
									break;
								default:
									throw;
							}
							Thread.Sleep(DEFAULT_DIRECTORY_DELETE_RETRY_DELAY_MS);
							retryCount++;
						}
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
