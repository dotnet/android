// 
// AdbSyncDirectory.cs
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

namespace Mono.AndroidTools.Adb
{
	public sealed class AdbSyncDirectory : AdbSyncItem, IEnumerable<AdbSyncItem>
	{
		Dictionary<string,AdbSyncItem> entries;
		
		public bool RemoveUnknownEntries { get; set; }
		
		public AdbSyncDirectory (string name, AdbSyncAction action = AdbSyncAction.CopyIfNewer,
			bool removeUnknown = false, params AdbSyncItem[] entries)
			: this (name, action, removeUnknown, (IEnumerable<AdbSyncItem>)entries)
		{
			this.RemoveUnknownEntries = removeUnknown;
		}
		
		public AdbSyncDirectory (string name, AdbSyncAction action = AdbSyncAction.CopyIfNewer,
			bool removeUnknown=false, IEnumerable<AdbSyncItem> entries=null)
			: base (name, action)
		{
			if (entries != null)
				AddRange (entries);
		}
		
		public void AddRange (IEnumerable<AdbSyncItem> entries)
		{
			if (this.entries == null)
				this.entries = new Dictionary<string,AdbSyncItem> ();
			foreach (var entry in entries)
				this.entries.Add (entry.Name, entry);
		}
		
		public void Add (AdbSyncItem entry)
		{
			if (entries == null)
				entries = new Dictionary<string,AdbSyncItem> ();
			this.entries.Add (entry.Name, entry);
		}
		
		public IEnumerable<AdbSyncItem> Entries {
			get {
				if (entries == null)
					return new AdbSyncItem[0];
				return entries.Values;
			}
		}
		
		public IEnumerator<AdbSyncItem> GetEnumerator ()
		{
			if (entries == null)
				return ((IEnumerable<AdbSyncItem>)new AdbSyncItem[0]).GetEnumerator ();
			return entries.Values.GetEnumerator ();
		}
		
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}
		
		public AdbSyncItem GetNearestDescendant (string subpath)
		{
			AdbSyncDirectory dir = this;
			string[] components = subpath.Split ('/');
			for (int i = 0; i < components.Length; i++) {
				if (entries == null)
					return dir;
				AdbSyncItem child;
				if (!entries.TryGetValue (components[i], out child))
					return dir;
				var childDir = child as AdbSyncDirectory;
				if (childDir == null)
					return child;
				dir = childDir;
			}
			return dir;
		}
		
		public static AdbSyncDirectory FromLocalDirectory (string directory,
			string remoteName = null,
			AdbSyncAction fileAction = AdbSyncAction.CopyIfNewer,
			bool removeUnknown = false,
			Func<FileInfo,bool> filter = null)
		{
			if (remoteName == null) {
				return new AdbSyncDirectory (Path.GetFileName (directory), fileAction, removeUnknown,
					AdbSyncItem.FromLocalDirectoryContents (directory, fileAction, removeUnknown, filter));
			}
			
			//remoteName may be a subpath, if so, create all the components
			var components = remoteName.Split (new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			var root = new AdbSyncDirectory (components[0], fileAction, removeUnknown);
			var leaf = root;
			for (int i = 1; i < components.Length; i++) {
				var newLeaf = new AdbSyncDirectory (components[i], fileAction, removeUnknown);
				leaf.Add (newLeaf);
				leaf = newLeaf;
			}
			leaf.AddRange (AdbSyncItem.FromLocalDirectoryContents (directory, fileAction, removeUnknown, filter));
			return root;
		}

		public override string ToString ()
		{
			return $"Name:{Name} Action:{Action} RemoveUnknownEntries:{RemoveUnknownEntries}";
		}
	}
}