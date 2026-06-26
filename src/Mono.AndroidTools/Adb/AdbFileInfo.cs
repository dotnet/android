// 
// AdbFileInfo.cs
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
using System.Text;

namespace Mono.AndroidTools.Adb
{
	/// <summary>
	/// Information about a file on a remote device
	/// </summary>
	public sealed class AdbFileInfo
	{
		string fullPath;
		internal uint mode;
		internal uint time;
		internal uint size;
		
		internal AdbFileInfo (string fullPath)
		{
			this.fullPath = fullPath;
		}
		
		internal AdbFileInfo (string fullPath, uint mode, uint size, uint time)
		{
			this.fullPath = fullPath;
			this.mode = mode;
			this.time = time;
			this.size = size;
		}
		
		/// <summary>
		/// The filename.
		/// </summary>
		public string Name {
			get {
				var idx = fullPath.LastIndexOf ('/');
				if (idx >= 0)
					return fullPath.Substring (idx + 1);
				return fullPath;
			}
		}
		
		/// <summary>
		/// The full path.
		/// </summary>
		public string FullPath {
			get { return fullPath; }
		}
		
		/// <summary>
		/// The file mode.
		/// </summary>
		public AdbFileMode Mode {
			get { return (AdbFileMode) mode; }
		}
		
		/// <summary>
		/// The time that the file was last modified, as a UNIX filetime.
		/// </summary>
		public uint FileTime {
			get { return time; }
		}
		
		/// <summary>
		/// The file size, in bytes.
		/// </summary>
		public uint Size { get { return size; } }
		
		/// <summary>
		/// The time that the file was last modified.
		/// </summary>
		public DateTime GetLastModified ()
		{
			return DateTimeFromUnixFileTime (FileTime);
		}
		
		/// <summary>
		/// The permissions bits of the file mode.
		/// </summary>
		public AdbFileMode Permissions {
			get {
				return Mode & AdbFileMode.ALLPERMS;
			}
		}
		
		/// <summary>
		/// Determines whether the file has the specified permissions.
		/// </summary>
		public bool HasPermissions (AdbFileMode permissions)
		{
			return (Permissions & permissions) == permissions;
		}
		
		/// <summary>
		/// The file type bits of the file mode.
		/// </summary>
		public AdbFileMode FileType {
			get {
				return Mode & AdbFileMode.S_IFMT;
			}
		}
		
		/// <summary>
		/// Determines whether the file is of the specified type.
		/// </summary>
		public bool IsFileType (AdbFileMode type)
		{
			return FileType == type;
		}
		
		/// <summary>
		/// Converts a DateTime to a UNIX filetime.
		/// </summary>
		public static uint UnixFileTimeFromDateTime (DateTime datetime)
		{
			//HACK: work around XBC #5697. DateTime.ToFileTimeUtc() does not convert to UTC, so convert beforehand
			if (datetime.Kind != DateTimeKind.Utc)
				datetime = datetime.ToUniversalTime ();

			long windowsFiletime = datetime.ToFileTimeUtc ();
			return (uint) ((windowsFiletime - 116444736000000000) / 10000000);
		}
		
		/// <summary>
		/// Converts a UNIX filetime to a DateTime.
		/// </summary>
		public static DateTime DateTimeFromUnixFileTime (uint filetime)
		{
			long windowsFiletime = ((long)filetime) * 10000000 + 116444736000000000;
			return DateTime.FromFileTimeUtc (windowsFiletime);
		}
		
		/// <summary>
		/// Gets the file's mode as a symbolic string.
		/// </summary>
		public string GetSymbolicMode ()
		{
			var sb = new StringBuilder ();
			var mode = (AdbFileMode) this.mode;
			switch (mode & AdbFileMode.S_IFMT) {
			case AdbFileMode.S_IFSOCK:
				sb.Append ('s');
				break;
			case AdbFileMode.S_IFLNK:
				sb.Append ('l');
				break;
			case AdbFileMode.S_IFREG:
				sb.Append ('-');
				break;
			case AdbFileMode.S_IFBLK:
				sb.Append ('b');
				break;
			case AdbFileMode.S_IFDIR:
				sb.Append ('d');
				break;
			case AdbFileMode.S_IFCHR:
				sb.Append ('c');
				break;
			case AdbFileMode.S_IFIFO:
				sb.Append ('p');
				break;
			}
			sb.Append (mode.HasFlag (AdbFileMode.S_IRUSR)? 'r' : '-');
			sb.Append (mode.HasFlag (AdbFileMode.S_IWUSR)? 'w' : '-');
			sb.Append (mode.HasFlag (AdbFileMode.S_IXUSR)?
				(mode.HasFlag (AdbFileMode.S_ISUID)? 's' : 'x') : (mode.HasFlag (AdbFileMode.S_ISUID)? 'S' : '-'));
			sb.Append (mode.HasFlag (AdbFileMode.S_IRGRP)? 'r' : '-');
			sb.Append (mode.HasFlag (AdbFileMode.S_IWGRP)? 'w' : '-');
			sb.Append (mode.HasFlag (AdbFileMode.S_IXGRP)?
				(mode.HasFlag (AdbFileMode.S_ISGID)? 's' : 'x') : (mode.HasFlag (AdbFileMode.S_ISGID)? 'S' : '-'));
			sb.Append (mode.HasFlag (AdbFileMode.S_IROTH)? 'r' : '-');
			sb.Append (mode.HasFlag (AdbFileMode.S_IWOTH)? 'w' : '-');
			sb.Append (mode.HasFlag (AdbFileMode.S_IXOTH)?
				(mode.HasFlag (AdbFileMode.S_ISVTX)? 't' : 'x') : (mode.HasFlag (AdbFileMode.S_ISVTX)? 'T' : '-'));
			
			return sb.ToString ();
		}

		public override string ToString ()
		{
			return GetSymbolicMode ();
		}
	}
}