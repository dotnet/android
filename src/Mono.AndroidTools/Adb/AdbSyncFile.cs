// 
// AdbSyncFile.cs
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
using System.IO;
using System.Diagnostics;

namespace Mono.AndroidTools.Adb
{
	public sealed class AdbSyncFile : AdbSyncItem
	{
		public AdbSyncFile (string sourceFile, string remoteName = null,
			AdbSyncAction action = AdbSyncAction.CopyIfNewer)
			: this (new FileInfo (sourceFile), remoteName, action)
		{
		}
		
		public AdbSyncFile (FileInfo sourceFile, string remoteName = null,
			AdbSyncAction action = AdbSyncAction.CopyIfNewer)
			: this (sourceFile.FullName, remoteName ?? sourceFile.Name, sourceFile.LastWriteTimeUtc, sourceFile.Length, action)
		{
		}
		
		public AdbSyncFile (string sourceFile, string remoteName, DateTime sourceWriteTimeUtc, long size,
			AdbSyncAction action)
			: base (remoteName, action)
		{
			Debug.Assert (!string.IsNullOrEmpty (sourceFile), "Source file path cannot be null or empty");
			Debug.Assert (Path.IsPathRooted (sourceFile), "Source file path must be rooted");
			
			this.SourceFile = sourceFile;
			this.SourceWriteTimeUtc = sourceWriteTimeUtc;
			this.SourceFileSize = size;
		}
		
		public string SourceFile { get; private set; }
		public long SourceFileSize { get; private set; }
		public DateTime SourceWriteTimeUtc { get; private set; }
		
		public static AdbSyncFile Copy (string sourceFile, string remoteName = null)
		{
			return new AdbSyncFile (sourceFile, remoteName, AdbSyncAction.Copy);
		}
		
		public static AdbSyncFile CopyIfNewer (string sourceFile, string remoteName = null)
		{
			return new AdbSyncFile (sourceFile, remoteName, AdbSyncAction.CopyIfNewer);
		}
	}
}