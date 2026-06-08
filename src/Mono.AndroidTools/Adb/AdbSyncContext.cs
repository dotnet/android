// 
// AdbSyncContext.cs
//  
// Authors:
//       Michael Hutchinson <mhutch@xamarin.com>
// 
// Copyright (c) 2011 Xamarin Inc.
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
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using Mono.AndroidTools.Internal;
using System.Linq;

namespace Mono.AndroidTools.Adb
{
	internal class AdbSyncTargetContext
	{
		public AdbSyncTargetContext (AdbSyncDirectory targetDir, string remoteParentDir)
		{
			Debug.Assert (targetDir != null, "Target directory cannot be null");
			Debug.Assert (!string.IsNullOrEmpty (remoteParentDir), "Remote parent directory cannot be null or empty");
			Debug.Assert (remoteParentDir[0] == '/', "Remote parent directory must be rooted");
			
			//ensure tailing / on remote parent
			if (remoteParentDir[remoteParentDir.Length - 1] != '/')
				remoteParentDir += '/';
			
			this.RemoteParentDir = remoteParentDir;
			this.TargetDir = targetDir;
		}

		public string RemoteParentDir { get; private set; }
		public AdbSyncDirectory TargetDir { get; private set; }
		public List<AdbSyncNotification> Operations { get; private set; }
		
		//TODO: in some cases we don't need a full recursive set of AdbFileInfos, would avoid some roundtrips
		public void ComputeRequiredOperations (List<AdbFileInfo> existingFiles, bool removeBeforeCopy)
		{
			var existingFileDict = new Dictionary<string, AdbFileInfo> ();
			foreach (var f in existingFiles) {
				existingFileDict[f.FullPath] = f;
			}
			
			Operations = new List<AdbSyncNotification> ();
			
			//determine needed operations for all the specified files and directories
			foreach (KeyValuePair<string,AdbSyncItem> entry in GetEntriesWithPaths ()) {
				AdbFileInfo existingFile = null;
				existingFileDict.TryGetValue (entry.Key, out existingFile);
				switch (entry.Value.Action) {	
				case AdbSyncAction.CopyIfNewer:
				case AdbSyncAction.Copy: {
					if (existingFile == null) {
						if (entry.Value is AdbSyncDirectory) {
							Operations.Add (new AdbSyncNotification (AdbSyncKind.CreateDirectory, entry.Key, null, 0));
						} else {
							var f = (AdbSyncFile)entry.Value;
							Operations.Add (new AdbSyncNotification (AdbSyncKind.CopyFile, entry.Key,
								f.SourceFile, f.SourceFileSize));
						}
						break;
					}
					
					if ((existingFile.IsFileType (AdbFileMode.S_IFDIR) || existingFile.FileType == 0) && entry.Value is AdbSyncDirectory) {
						Operations.Add (new AdbSyncNotification (AdbSyncKind.SkipCreateDirectory, entry.Key, null, 0));
						existingFileDict.Remove (entry.Key);
					} else if (existingFile.IsFileType (AdbFileMode.S_IFREG) && entry.Value is AdbSyncFile) {
						var file = ((AdbSyncFile)entry.Value);
						var srcTime = file.SourceWriteTimeUtc;
						srcTime = srcTime.AddTicks (-srcTime.Ticks % TimeSpan.TicksPerSecond);
						var destTime = existingFile.GetLastModified ();
						destTime = destTime.AddTicks (-destTime.Ticks % TimeSpan.TicksPerSecond);
						bool skip = entry.Value.Action == AdbSyncAction.CopyIfNewer
							&& !(srcTime > destTime
							    || file.SourceFileSize != existingFile.Size);
						if (!skip && removeBeforeCopy)
							Operations.Add (new AdbSyncNotification (AdbSyncKind.RemoveFile, entry.Key, null, 0));
						Operations.Add (new AdbSyncNotification (skip? AdbSyncKind.SkipCopyFile : AdbSyncKind.CopyFile,
							entry.Key, file.SourceFile, file.SourceFileSize));
						existingFileDict.Remove (entry.Key);
					} else {
						throw new AdbException (string.Format (
							"Could not copy item '{0}' due to unexpected existing item of type '{1}'",
							entry.Key, existingFile.FileType));
					}
					break;
				}
				case AdbSyncAction.Delete: {
					if (existingFile == null)
						break;
					if (existingFile.IsFileType (AdbFileMode.S_IFDIR)) {
						Operations.Add (new AdbSyncNotification (AdbSyncKind.RemoveDirectory, entry.Key, null, 0));
						existingFileDict.Remove (entry.Key);
					} else if (existingFile.IsFileType (AdbFileMode.S_IFREG)) {
						Operations.Add (new AdbSyncNotification (AdbSyncKind.RemoveFile, entry.Key, null, 0));
						existingFileDict.Remove (entry.Key);
					} else {
						throw new AdbException (string.Format (
							"Could not delete blocklisted item '{0}' of unknown type '{1}'",
							entry.Key, existingFile.FileType));
					}
					break;
				}
				case AdbSyncAction.Ignore: {
					if (existingFile == null)
						break;
					var op = existingFile.IsFileType (AdbFileMode.S_IFDIR)?
						AdbSyncKind.PreserveDirectory : AdbSyncKind.PreserveFile;
					Operations.Add (new AdbSyncNotification (op, entry.Key, null, 0));
					existingFileDict.Remove (entry.Key);
					break;
				}
				default:
					throw new Exception ("Unknown AdbSyncAction value:" + entry.Value.Action);
				}
			}
			
			//determine what operations are needed for existing files that aren't specified explicitly
			foreach (var existingFile in existingFileDict.Values) {
				var closest = TargetDir.GetNearestDescendant (existingFile.FullPath.Substring (RemoteParentDir.Length));
				bool isDir = existingFile.IsFileType (AdbFileMode.S_IFDIR);
				var closestAsDir = closest as AdbSyncDirectory;
				
				if (closest.Action == AdbSyncAction.Ignore) {
					var op = isDir? AdbSyncKind.PreserveDirectory : AdbSyncKind.PreserveFile;
					Operations.Add (new AdbSyncNotification (op, existingFile.FullPath, null, 0));
					continue;
				}
				
				if (closest.Action == AdbSyncAction.Delete) {
					AdbSyncKind action = isDir? AdbSyncKind.RemoveDirectory : AdbSyncKind.RemoveFile;
					Operations.Add (new AdbSyncNotification (action, existingFile.FullPath, null, 0));
					continue;
				}
				
				if (closestAsDir != null && closestAsDir.RemoveUnknownEntries) {
					AdbSyncKind action = isDir? AdbSyncKind.RemoveUnknownDirectory : AdbSyncKind.RemoveUnknownFile;
					Operations.Add (new AdbSyncNotification (action, existingFile.FullPath, null, 0));
				}
			}
		}
		
		IEnumerable<KeyValuePair<string,AdbSyncItem>> GetEntriesWithPaths ()
		{
			var dirFullName = RemoteParentDir + TargetDir.Name;
			yield return new KeyValuePair<string, AdbSyncItem> (dirFullName, TargetDir);
			foreach (var c in GetEntriesWithPaths (TargetDir, dirFullName)) {
				yield return c;
			}
		}
		
		static IEnumerable<KeyValuePair<string,AdbSyncItem>> GetEntriesWithPaths (AdbSyncDirectory dir,
			string fullDirName)
		{
			foreach (var entry in dir) {
				string fullName = fullDirName + "/" + entry.Name;
				yield return new KeyValuePair<string, AdbSyncItem> (fullName, entry);
				var childDir = entry as AdbSyncDirectory;
				if (childDir != null)
					foreach (var c in GetEntriesWithPaths (childDir, fullName))
						yield return c;
			}
		}
	}
}