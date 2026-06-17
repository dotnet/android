// 
// AndroidDiskInformation.cs
//  
// Author:
//       Carlos Alberto Cortez
//       Michael Hutchinson <mhutch@xamarin.com>
// 
// Copyright (c) 2010-2011 Novell, Inc.
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

using Mono.AndroidTools.Internal;

namespace Mono.AndroidTools
{
	public sealed class AndroidDiskInformation
	{
		long externalSpace = -1;
		long internalSpace = -1;

		const long Kilobyte = 1024;
		const long Megabyte = 1024 * 1024;
		const long Gigabyte = 1024 * 1024 * 1024;

		const string InternalPartition = "/data";
		const string ExternalPartition = "/mnt/sdcard";

		public long InternalSpace { get { return internalSpace; } }
		public long ExternalSpace { get { return externalSpace; } }
		
		AndroidDiskInformation ()
		{
		}
		
		public static AndroidDiskInformation FromDfOutput (string dfOutput)
		{
			var info = new AndroidDiskInformation ();
			if (dfOutput.StartsWith ("Filesystem")) {
				info.ParseNewFormat (dfOutput);
			} else {
				info.ParseOldFormat (dfOutput);
			}
			if (info.internalSpace < 0)
				throw new AdbException ("Could not determine size of internal partition");
			return info;
		}

		void ParseNewFormat (string output)
		{
			string s;
			var reader = new StringReader (output);

			reader.ReadLine (); //  header line

			// /data      496M    54M   442M   4096
			while ((s = reader.ReadLine ()) != null) {
				var parts = s.Split (new char [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length != 5)
					return;

				string partition = parts [0];
				if (partition != InternalPartition && partition != ExternalPartition)
					continue;

				long size;
				if (!ParseToBytes (parts [3], out size))
					return;

				if (partition == InternalPartition)
					internalSpace = size;
				else
					externalSpace = size;
			}
		}

		void ParseOldFormat (string output)
		{
			string s;
			var reader = new StringReader (output);
			while ((s = reader.ReadLine ()) != null) {
				int idx = s.IndexOf (':');
				if (idx < 0)
					return;

				long size = 0;
				string partition = s.Substring (0, idx);
				if (partition != InternalPartition && partition != ExternalPartition)
					continue;

				// /data/: 508416K total, 98548K used, 409868K available (block size 4096)
				var parts = s.Split (new char [] { ',' });
				if (parts.Length != 3 || parts [2].IndexOf ("available") < 0)
					return;

				// the actual available component
				parts = parts [2].Split (new char [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (!ParseToBytes (parts [0].Trim (), out size))
					return;

				if (partition == InternalPartition)
					internalSpace = size;
				else
					externalSpace = size;
			}
		}

		bool ParseToBytes (string s, out long value)
		{
			var unit = s [s.Length - 1];
			var available = s.Substring (0, s.Length - 1);
			if (!long.TryParse (available, out value))
				return false;

			switch (unit) {
				case 'K': value *= Kilobyte;
					  break;
				case 'M': value *= Megabyte;
					  break;
				case 'G': value *= Gigabyte;
					  break;
				default:
					  return false;
			}

			return true;
		}
	}
}