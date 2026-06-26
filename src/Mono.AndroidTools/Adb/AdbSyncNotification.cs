// 
// AdbSyncNotification.cs
//  
// Author:
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

namespace Mono.AndroidTools.Adb
{
	/// <summary>
	/// Provides information about a file that has been synced.
	/// </summary>
	public class AdbSyncNotification
	{
		public AdbSyncNotification (AdbSyncKind kind, string remotePath, string localPath, long size)
		{
			this.Kind = kind;
			this.RemotePath = remotePath;
			this.LocalPath = localPath;
			this.Size = size;
		}
		
		public AdbSyncKind Kind { get; private set; }
		public string RemotePath { get; private set; }
		public string LocalPath { get; private set; }
		public long Size { get; private set; }
		
		public string GetMessage ()
		{
			switch (Kind) {
			case AdbSyncKind.CopyFile:
				return string.Format ("Copied file '{0}' to '{1}'", LocalPath, RemotePath);
			case AdbSyncKind.SkipCopyFile:
				return string.Format ("Skipped copying file '{0}' to '{1}' because the destination is newer than the source",
					LocalPath, RemotePath);
			case AdbSyncKind.CreateDirectory:
				return string.Format ("Created directory '{0}'", RemotePath);
			case AdbSyncKind.SkipCreateDirectory:
				return string.Format ("Skipped creating directory '{0}' because it already exists", RemotePath);
			case AdbSyncKind.RemoveUnknownFile:
				return string.Format ("Removed unknown file '{0}'", RemotePath);
			case AdbSyncKind.RemoveUnknownDirectory:
				return string.Format ("Removed unknown directory '{0}'", RemotePath);
			case AdbSyncKind.RemoveFile:
				return string.Format ("Removed blocklisted file '{0}'", RemotePath);
			case AdbSyncKind.RemoveDirectory:
				return string.Format ("Removed blocklisted directory '{0}'", RemotePath);
			case AdbSyncKind.PreserveFile:
				return string.Format ("Preserved allowlisted file '{0}'", RemotePath);
			case AdbSyncKind.PreserveDirectory:
				return string.Format ("Preserved allowlisted directory '{0}'", RemotePath);
			}
			throw new InvalidOperationException ("Unknown kind '" + Kind.ToString () + "'");
		}
		
		internal bool IsRemove {
			get {
				return (Kind & (AdbSyncKind.RemoveDirectory | AdbSyncKind.RemoveFile
					| AdbSyncKind.RemoveUnknownDirectory | AdbSyncKind.RemoveUnknownFile)) != 0;
			}
		}
		
		internal bool IsCopyFile {
			get {
				return (Kind & (AdbSyncKind.CopyFile | AdbSyncKind.SkipCopyFile)) != 0;
			}
		}
		
		internal bool IsCreateDirectory {
			get {
				return (Kind & (AdbSyncKind.CreateDirectory | AdbSyncKind.SkipCreateDirectory)) != 0;
			}
		}
		
		internal bool Redundant { get; set; }
	}
	
	public enum AdbSyncKind
	{
		CopyFile = 1 << 0,
		SkipCopyFile = 1 << 1,
		CreateDirectory = 1 << 2,
		SkipCreateDirectory = 1 << 3,
		RemoveUnknownFile = 1 << 5,
		RemoveUnknownDirectory = 1 << 6,
		RemoveFile = 1 << 7,
		RemoveDirectory = 1 << 8,
		PreserveFile = 1 << 9,
		PreserveDirectory = 1 << 10,
	}
}