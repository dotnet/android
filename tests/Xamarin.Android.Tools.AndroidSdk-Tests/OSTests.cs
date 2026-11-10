// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class OSTests
	{
		[Test]
		public void PlatformDetection_MatchesUnameSysname ()
		{
			bool isWindows = Path.DirectorySeparatorChar == '\\';
			Assert.AreEqual (isWindows, OS.IsWindows, "IsWindows");
			if (isWindows) {
				Assert.IsFalse (OS.IsMac, "IsMac");
				Assert.IsFalse (OS.IsLinux, "IsLinux");
				return;
			}

			// Mirrors the uname()-based detection that OS.IsMac used to perform.
			bool isDarwin = GetUnameSysname () == "Darwin";
			Assert.AreEqual (isDarwin, OS.IsMac, "IsMac");
			Assert.AreEqual (!isDarwin, OS.IsLinux, "IsLinux");
		}

		static string? GetUnameSysname ()
		{
			IntPtr buf = Marshal.AllocHGlobal (8192);
			try {
				return uname (buf) == 0 ? Marshal.PtrToStringAnsi (buf) : null;
			} finally {
				Marshal.FreeHGlobal (buf);
			}
		}

		[DllImport ("libc")]
		static extern int uname (IntPtr buf);
	}
}
