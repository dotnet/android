// 
// AdbStartWindowsServerProcess.cs
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
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using Mono.AndroidTools.Util;

namespace Mono.AndroidTools.Internal
{
	// adb start-server has issues on Windows. When it starts the server, it forks a process, which
	// stays alive indefinitely. This new process carries off all the handles from the adb
	// commandline process and hence keeps them alive as long as it lives. The hardcoded behaviour
	// of .NET is to pass all handles to child processes. That means that the adb commandline 
	// process and hence the server process gets all MonoDevelop's handles and keeps them alive
	// indefinitely. However, the new process is on a new process tree from the one MD started, 
	// so we can't (easily) kill it when MD exits (nor do we necessarily want to do so).
	//
	// And if we do redirect adb's stdout and stderr, blocking reads on them get stuck in native code
	// and cannot be interrupted even when the process exits, because the child (server) process keeps
	// them open and doesn't write to them frequently.
	//
	// We have two workarounds for this, and both involve using P/Invoke to call CreateProcess directly.
	// a) Start the process with bInheritHandles set to false. That means we can't redirect 
	//    stdout/stderr, can only report the exit code.
	// b) Redirect the process's stdout and stderr to a file that we can read when reporting errors. 
	//    Mark this MD's stdout/stderr/stdin as noninheritable, so the child cannot hold them
	//    open. This is hacky, as other child processes may have a legitimate reason to use these 
	//    handles. There could also be problems if MD has other inheritable handles.
	//
	// For now, (a) is the default behavior, and (b) can be enabled by defining ADB_SERVER_OUTPUT.
	//
	// Possibly a better (but more complex solution) would be to implement a wrapper process for
	// adb start-server that redirects stdout/stderr to a log file, and start that process without 
	// inheriting handles.
	//
	// More info:
	// http://www.davidmoore.info/2011/06/21/when-system-diagnostics-process-creates-a-process-it-inherits-inheritable-handles-from-the-parent-process/
	// http://social.msdn.microsoft.com/Forums/en-US/netfxbcl/thread/94ba760c-7080-4614-8a56-15582c48f900/
	//
	class AdbStartWindowsServerProcess : IAdbStartServerProcess
	{
		Win32Interop.ProcessWaitHandle waitHandle;
		Win32Interop.SafeProcessHandle hProcess;
		EventHandler exited;
		bool completed;
		int exitCode;

#if ADB_SERVER_OUTPUT
		string logFile;
		
		public AdbStartWindowsServerProcess (string adbExe, string logFile, EventHandler exited)
		{
			this.logFile = logFile;
#endif
		
		public AdbStartWindowsServerProcess (string adbExe, EventHandler exited)
		{
			if (exited != null)
				this.Exited += exited;
			
			//the commandline arg has to include argv[0]
			var pb = new ProcessArgumentBuilder ();
			pb.AddQuoted (adbExe);
			pb.Add ("start-server");
			var commandline = pb.ToString ();
			
			var startupInfo = new Win32Interop.StartupInfo ();
			startupInfo.cb = (uint) Marshal.SizeOf (startupInfo);
			
			Win32Interop.ProcessInfo processInformation;
#if ADB_SERVER_OUTPUT
			//HACK: prevent the new process from inheriting our stdhandles
			Win32Interop.SetHandleInformation (Win32Interop.StdInputHandle, Win32Interop.HandleFlags.Inherit, Win32Interop.HandleFlags.None);
			Win32Interop.SetHandleInformation (Win32Interop.StdOutputHandle, Win32Interop.HandleFlags.Inherit, Win32Interop.HandleFlags.None);
			Win32Interop.SetHandleInformation (Win32Interop.StdErrorHandle, Win32Interop.HandleFlags.Inherit, Win32Interop.HandleFlags.None);
			
			var logStream = File.Open (logFile, FileMode.Create, FileAccess.Write, FileShare.Inheritable | FileShare.Read);
            
			startupInfo.dwFlags = Win32Interop.StartF.UseStdHandles;
			startupInfo.hStdOutput = logStream.SafeFileHandle.DangerousGetHandle ();
			startupInfo.hStdError = startupInfo.hStdOutput;
			startupInfo.hStdInput = IntPtr.Zero;
			
			bool success;
			success = Win32Interop.CreateProcess (null, commandline, IntPtr.Zero, IntPtr.Zero, true,
				Win32Interop.CreateProcessFlags.CreateNoWindow, IntPtr.Zero, null, ref startupInfo, out processInformation);
			logStream.Close ();
#else
			bool success;
			success = Win32Interop.CreateProcess (null, commandline, IntPtr.Zero, IntPtr.Zero, false,
				Win32Interop.CreateProcessFlags.CreateNoWindow, IntPtr.Zero, null, ref startupInfo, out processInformation);
#endif
			
			if (!success)
				Win32Interop.ThrowWin32Error ();
			
			hProcess = new Win32Interop.SafeProcessHandle (processInformation.hProcess, true);
			var hThread = new Win32Interop.SafeProcessHandle (processInformation.hThread, true);
			hThread.Close ();
			
			waitHandle = new Win32Interop.ProcessWaitHandle (hProcess);
			ThreadPool.RegisterWaitForSingleObject (waitHandle, ProcessExited, null, 10 * 1000, true);
		}
		
		void ProcessExited (object state, bool timedOut)
		{
			try {
				if (timedOut) {
					AndroidLogger.LogError ("Timeout checking adb start-server status");
					//TODO: kill process
					exitCode = int.MaxValue;
				} else {
					if (!Win32Interop.GetExitCodeProcess (hProcess.DangerousGetHandle (), out exitCode)) {
						int err = Marshal.GetLastWin32Error ();
						AndroidLogger.LogError (
							string.Format ("Error checking adb start-server status: win32 code {0}", err));
						exitCode = int.MaxValue;
					}
				}
				EventHandler evt;
				lock (this) {
					completed = true;
					evt = this.exited;
				}
				if (evt != null)
					evt (this, EventArgs.Empty);
			} catch (Exception ex) {
				AndroidLogger.LogError ("Error checking adb start-server status", ex);
			}
			Dispose ();
		}
		
		public event EventHandler Exited {
			add {
				bool invoke = false;
				lock (this) {
					if (completed)
						invoke = true;
					exited += value;
				}
				if (invoke)
					value (this, EventArgs.Empty);
			}
			remove {
				lock (this) {
					exited -= value;
				}
			}
		}
		
		public bool Success {
			get {
				return exitCode == 0;
			}
		}
		
		public bool Completed {
			get {
				return completed;
			}
		}
		
		public string GetOutput ()
		{
#if ADB_SERVER_OUTPUT
			return File.ReadAllText (logFile);
#else
			return string.Format ("Error code {0}", exitCode);
#endif
		}
		
		public void Dispose ()
		{
			var hp = hProcess;
			if (hp != null) {
				hp.Dispose ();
				hProcess = null;
			}
			var wh = waitHandle;
			if (wh != null) {
				wh.Dispose ();
				wh = null;
			}
		}
			
		public void Cancel ()
		{
			if (completed)
				return;
			var hp = hProcess;
			if (hp == null)
				return;
			if (!Win32Interop.TerminateProcess (hp.DangerousGetHandle (), UInt32.MaxValue)) {
				int err = Marshal.GetLastWin32Error ();
				throw new System.ComponentModel.Win32Exception (err);
			}
		}
	}
}