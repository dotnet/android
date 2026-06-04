// 
// Win32Interop.cs
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
using System.Runtime.InteropServices;
using System.Threading;

namespace Mono.AndroidTools.Util
{
	//Signatures from pinvoke.net with modifications and additions
	static class Win32Interop
	{		
		[DllImport ("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true, EntryPoint="CreateProcessW")]
		public static extern bool CreateProcess (
			string lpApplicationName,
			string lpCommandLine,
			ref SecurityAttributes lpProcessAttributes,
			ref SecurityAttributes lpThreadAttributes,
			bool bInheritHandles,
			CreateProcessFlags dwCreationFlags,
			IntPtr lpEnvironment,
			string lpCurrentDirectory,
			[In] ref StartupInfo lpStartupInfo,
			out ProcessInfo lpProcessInformation);
		
		[DllImport ("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true, EntryPoint="CreateProcessW")]
		public static extern bool CreateProcess (
			string lpApplicationName,
			string lpCommandLine,
			IntPtr lpProcessAttributes,
			IntPtr lpThreadAttributes,
			bool bInheritHandles,
			CreateProcessFlags dwCreationFlags,
			IntPtr lpEnvironment,
			string lpCurrentDirectory,
			[In] ref StartupInfo lpStartupInfo,
			out ProcessInfo lpProcessInformation);
		
		[DllImport ("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true, EntryPoint="TerminateProcess")]
		public static extern bool TerminateProcess (IntPtr hProcess, uint exitCode);
		
		[StructLayout (LayoutKind.Sequential)]
		public struct ProcessInfo
		{
			public IntPtr hProcess;
			public IntPtr hThread;
			public Int32 ProcessId;
			public Int32 ThreadId;
		}

		[StructLayout (LayoutKind.Sequential)]
		public struct SecurityAttributes
		{
			public int length;
			public IntPtr lpSecurityDescriptor;
			public bool bInheritHandle;
		}

		[StructLayout (LayoutKind.Sequential, CharSet=CharSet.Unicode)]
		public struct StartupInfo
		{
			public uint cb;
			public string lpReserved;
			public string lpDesktop;
			public string lpTitle;
			public uint dwX;
			public uint dwY;
			public uint dwXSize;
			public uint dwYSize;
			public uint dwXCountChars;
			public uint dwYCountChars;
			public uint dwFillAttribute;
			public StartF dwFlags;
			public short wShowWindow;
#pragma warning disable 0169
			short cbReserved2;
			IntPtr lpReserved2;
#pragma warning restore 0169
			public IntPtr hStdInput;
			public IntPtr hStdOutput;
			public IntPtr hStdError;
		}
		
		[Flags]
		public enum StartF : uint
		{
			None = 0,
			ForceOnFeedback  = 0x00000040,
			ForceOffFeedback = 0x00000080,
			PreventPinning   = 0x00002000,
			RunFullscreen    = 0x00000020,
			TitleIsAppID     = 0x00001000,
			TitleIsLinkName  = 0x00000800,
			UseCountChars    = 0x00000008,
			UseFillAttribute = 0x00000010,
			UseHotKey        = 0x00000200,
			UsePosition      = 0x00000004,
			UseShowWindow    = 0x00000001,
			UseSize          = 0x00000002,
			UseStdHandles    = 0x00000100,
		}
		
		[DllImport ("kernel32.dll", SetLastError=true)]
		public static extern Int32 WaitForSingleObject (IntPtr Handle, Int32 Wait);
		
		[DllImport ("kernel32.dll", SetLastError=true)]
		public static extern bool GetExitCodeProcess (IntPtr Handle, out int lpExitCode);
		
		[DllImport ("kernel32.dll", SetLastError=true)]
		public static extern bool SetHandleInformation (IntPtr hObject, HandleFlags dwMask, HandleFlags dwFlags);
		
		[Flags]
		public enum HandleFlags
		{
			None = 0,
			Inherit = 1,
			ProtectFromClose = 2
		}
		
		public static void ThrowWin32Error ()
		{
			throw new Exception (string.Format ("Win32 error: {0}", Marshal.GetLastWin32Error ()));
		}
		
		[DllImport ("kernel32.dll", SetLastError = true)]
		public static extern IntPtr GetStdHandle (int nStdHandle);
		
		const int STD_INPUT_HANDLE = -10;
		const int STD_OUTPUT_HANDLE = -11;
		const int STD_ERROR_HANDLE = -12;
		
		public static IntPtr StdInputHandle {
			get {
				IntPtr handle = GetStdHandle (STD_INPUT_HANDLE);
				if (handle == IntPtr.Zero)
					ThrowWin32Error ();
				return handle;
			}
		}
		
		public static IntPtr StdOutputHandle {
			get {
				IntPtr handle = GetStdHandle (STD_OUTPUT_HANDLE);
				if (handle == IntPtr.Zero)
					ThrowWin32Error ();
				return handle;
			}
		}
		
		public static IntPtr StdErrorHandle {
			get {
				IntPtr handle = GetStdHandle (STD_ERROR_HANDLE);
				if (handle == IntPtr.Zero)
					ThrowWin32Error ();
				return handle;
			}
		}
		
		[Flags]
		public enum CreateProcessFlags : uint
		{
			None = 0,
			CreateBreakawayFromJob = 0x01000000,
			CreateDefaultErrorMode = 0x04000000,
			CreateNewConsole       = 0x00000010,
			CreateNewProcessGroup  = 0x00000200,
			CreateNoWindow         = 0x08000000,
			CreateProtectedProcess = 0x00040000,
			CreatePreserveCodeAuthzLevel = 0x02000000,
			CreateSeparateWowVdm   = 0x00000800,
			CreateSharedWowVvm     = 0x00001000,
			CreateSuspended        = 0x00000004,
			CreateUnicodeEnvironment = 0x00000400,
			DebugOnlyThisProcess   = 0x00000002,
			DebugProcess           = 0x00000001,
			DetachedProcess        = 0x00000008,
			ExtendedStartupInfoPresent = 0x00080000,
			InheritParentAffinity  = 0x00010000,
			
			AboveNormalPriorityClass = 0x00008000,
			BelowNormalPriorityClass = 0x00004000,
			HighPriorityClass        = 0x00000080,
			IdlePriorityClass        = 0x00000040,
			NormalPriorityClass      = 0x00000020,
			RealtimePriorityClass    = 0x00000100,
		}
		
		public class SafeProcessHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
		{
			public SafeProcessHandle (IntPtr handle, bool ownsHandle) : base (ownsHandle)
			{
				base.handle = handle;
			}
			
			protected override bool ReleaseHandle ()
			{
				return CloseHandle (handle);
			}
			
			[DllImport("kernel32.dll", SetLastError=true)]
			[return: MarshalAs (UnmanagedType.Bool)]
			static extern bool CloseHandle (IntPtr hObject);
			
			static void ThrowWin32Error ()
			{
				throw new Exception (string.Format ("Win32 error: {0}", Marshal.GetLastWin32Error ()));
			}
		}
		
		public class ProcessWaitHandle : WaitHandle
		{
			public ProcessWaitHandle (SafeProcessHandle processHandle)
			{
				SafeWaitHandle = new Microsoft.Win32.SafeHandles.SafeWaitHandle (processHandle.DangerousGetHandle (), false);
			}
		}
	}
}