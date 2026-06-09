// 
// AdbSyncEntry.cs
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

namespace Mono.AndroidTools.Adb
{
	public class AdbSyncItem
	{
		internal AdbSyncItem (string name, AdbSyncAction action)
		{
			Debug.Assert (!string.IsNullOrEmpty (name), "Entry name cannot be null or empty");
			Debug.Assert (name.IndexOf ('/') < 0, "Entry name cannot contain remote path components");
			
			this.Action = action;
			this.Name = name;
		}
		
		public string Name { get; private set; }
		public AdbSyncAction Action { get; private set; }
		
		public static IEnumerable<AdbSyncItem> FromLocalDirectoryContents (string localDirectoryPath,
			AdbSyncAction fileAction = AdbSyncAction.CopyIfNewer,
			bool removeUnknown=false,
			Func<FileInfo,bool> filter=null)
		{
			var entries = Directory.EnumerateFileSystemEntries (localDirectoryPath, "*", SearchOption.TopDirectoryOnly);
			foreach (var e in entries) {
				var fsi = new FileInfo (e);
				if (filter != null && !filter(fsi))
					continue;
				if (fsi.Attributes.HasFlag (FileAttributes.Directory)) {
					yield return AdbSyncDirectory.FromLocalDirectory (fsi.FullName, null, fileAction,
						removeUnknown, filter);
				} else {
					yield return new AdbSyncFile (fsi, action: fileAction);
				}
			}
		}
		
		public static AdbSyncItem Ignore (string name)
		{
			return new AdbSyncItem (name, AdbSyncAction.Ignore);
		}
		
		public static AdbSyncItem Delete (string name)
		{
			return new AdbSyncItem (name, AdbSyncAction.Delete);
		}
	}
	
	public enum AdbSyncAction
	{
		Copy,
		CopyIfNewer,
		Delete,
		Ignore,
	}
}