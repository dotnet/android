// 
// AdbFileMode.cs
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

//modified from Mono.Unix.Native, MIT/X11

namespace Mono.AndroidTools.Adb
{
	/// <summary>
	/// File mode flags for files on remote Android devices.
	/// </summary>
	[Flags]
	public enum AdbFileMode : uint
	{
		/// <summary>Set user ID on execution.</summary>
		S_ISUID     = 0x0800,
		/// <summary>Set group ID on execution.</summary>
		S_ISGID     = 0x0400,
		/// <summary>Save swapped text after use (sticky).</summary>
		S_ISVTX     = 0x0200,
		/// <summary>Read by owner.</summary>
		S_IRUSR     = 0x0100,
		/// <summary>Write by owner.</summary>
		S_IWUSR     = 0x0080,
		/// <summary>Execute by owner.</summary>
		S_IXUSR     = 0x0040,
		/// <summary>Read by group.</summary>
		S_IRGRP     = 0x0020,
		/// <summary>Write by group.</summary>
		S_IWGRP     = 0x0010,
		/// <summary>Execute by group.</summary>
		S_IXGRP     = 0x0008,
		/// <summary>Read by other.</summary>
		S_IROTH     = 0x0004,
		/// <summary>Write by other.</summary>
		S_IWOTH     = 0x0002,
		/// <summary>Execute by other.</summary>
		S_IXOTH     = 0x0001,
		
		/// <summary>Mask for group access permissions.</summary>
		S_IRWXG     = (S_IRGRP | S_IWGRP | S_IXGRP),
		/// <summary>Mask for user access permissions.</summary>
		S_IRWXU     = (S_IRUSR | S_IWUSR | S_IXUSR),
		/// <summary>Mask for other access permissions.</summary>
		S_IRWXO     = (S_IROTH | S_IWOTH | S_IXOTH),
		/// <summary>Mask for access permissions (0777).</summary>
		ACCESSPERMS = (S_IRWXU | S_IRWXG | S_IRWXO),
		/// <summary>Mask for all permissions (7777).</summary>
		ALLPERMS    = (S_ISUID | S_ISGID | S_ISVTX | S_IRWXU | S_IRWXG | S_IRWXO),
		/// <summary>Mask for all read/write permissions (0666).</summary>
		DEFFILEMODE = (S_IRUSR | S_IWUSR | S_IRGRP | S_IWGRP | S_IROTH | S_IWOTH),

		// Device types
		/// <summary>Mask for file type</summary>
		S_IFMT      = 0xF000,
		/// <summary>Directory</summary>
		S_IFDIR     = 0x4000,
		/// <summary>Character device</summary>
		S_IFCHR     = 0x2000,
		/// <summary>Block device</summary>
		S_IFBLK     = 0x6000,
		/// <summary>Regular file</summary>
		S_IFREG     = 0x8000,
		/// <summary>FIFO</summary>
		S_IFIFO     = 0x1000,
		/// <summary>Symbolic link</summary>
		S_IFLNK     = 0xA000,
		/// <summary>Socket</summary>
		S_IFSOCK    = 0xC000,
	}
}

