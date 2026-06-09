//
// AdbShellCommand.cs
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
//

using System;
using System.Collections.Generic;
using Mono.AndroidTools;
using Mono.AndroidTools.Util;

namespace Mono.AndroidTools.Internal
{
	static class AdbShellCommand
	{
		public static string Am (string command, string action, string[] categories = null, IDictionary<string, string> extras = null, string name = null, string component = null, bool wait = false)
		{
			var pb = new ProcessArgumentBuilder ();
			pb.Add ("am", command);
			if (wait)
				pb.Add ("-W");
			pb.Add ("-a", action);
			if (categories != null)
				foreach (string category in categories)
					if (!string.IsNullOrEmpty (category))
						pb.Add ("-c", category);
			if (extras != null)
				foreach (KeyValuePair<string, string> e in extras) {
					pb.Add (
							string.IsNullOrEmpty (e.Value) ? "--esn" : "-e");
					pb.AddQuoted (e.Key);
					if (!string.IsNullOrEmpty (e.Value))
						pb.AddQuoted (e.Value);
				}
			if (!String.IsNullOrEmpty (name))
				pb.Add ("-n", name);
			if (!string.IsNullOrEmpty (component))
				pb.Add (component);
			return pb.ToString ();
		}

		public static string AmForceStop (string package)
		{
			var pb = new ProcessArgumentBuilder ();
			pb.Add ("am", "force-stop");
			pb.Add (package);
			return pb.ToString ();
		}

		public static string JoinArguments (params string[] arguments)
		{
			var pb = new ProcessArgumentBuilder ();

			foreach (var a in arguments)
				pb.AddQuoted (a);

			return pb.ToString ();
		}

		internal static AdbInstallFlags ToInstallFlags (bool reinstall, bool external)
		{
			var flags = AdbInstallFlags.None;
			if (reinstall)
				flags |= AdbInstallFlags.Reinstall;
			if (external)
				flags |= AdbInstallFlags.External;
			return flags;
		}

		public static string PmInstall (string remoteApkFile, bool reinstall, bool external)
		{
			return PmInstall (remoteApkFile, ToInstallFlags (reinstall, external));
		}

		public static string PmInstall (string remoteApkFile, AdbInstallFlags flags)
		{
			return new PmInstallCommand () {
				RemoteApkFile = remoteApkFile,
				Flags = flags,
			}.ToString ();
		}

		public static string PmUninstall (string package, bool preserveData = false)
		{
			return new PmUninstallCommand ()
			{
				PackageName = package,
				PreserveData = preserveData,
			}.ToString ();
		}

		public static string Rm (string remoteFile, bool recursive=false, bool ignoreError=false)
		{
			var pb = new ProcessArgumentBuilder ();
			pb.Add ("rm");
			if (recursive)
				pb.Add ("-r");
			if (ignoreError)
				pb.Add ("-f");
			pb.AddQuoted (remoteFile);
			return pb.ToString ();
		}

		public static string Rm (string[] remoteFiles, bool recursive=false, bool ignoreError=false)
		{
			var pb = new ProcessArgumentBuilder ();
			pb.Add ("rm");
			if (recursive)
				pb.Add ("-r");
			if (ignoreError)
				pb.Add ("-f");
			pb.AddQuoted (remoteFiles);
			return pb.ToString ();
		}

		public static string Setprop (string property, string value)
		{
			if (property == null)
				throw new ArgumentNullException ("property");
			if (property.Length == 0)
				throw new ArgumentException ("property name cannot be zero length", "property");
			if (value == null)
				throw new ArgumentNullException ("value");

			var pb = new ProcessArgumentBuilder ();
			pb.Add ("setprop");
			pb.AddQuoted (property, value);
			return pb.ToString ();
		}
	}
}
