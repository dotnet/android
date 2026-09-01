using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xamarin.Android.Tools;

namespace Xamarin.ProjectTools
{
	public static class DexUtils
	{
		/*
		 Example dexdump output:
		 
			Class #12            -
			  Class descriptor  : 'Landroid/runtime/UncaughtExceptionHandler;'
			  Access flags      : 0x0001 (PUBLIC)
			  Superclass        : 'Ljava/lang/Object;'
			  Interfaces        -
				#0              : 'Ljava/lang/Thread$UncaughtExceptionHandler;'
				#1              : 'Lmono/android/IGCUserPeer;'
			  Static fields     -
				#0              : (in Landroid/runtime/UncaughtExceptionHandler;)
				  name          : '__md_methods'
				  type          : 'Ljava/lang/String;'
				  access        : 0x0019 (PUBLIC STATIC FINAL)
			  Instance fields   -
				#0              : (in Landroid/runtime/UncaughtExceptionHandler;)
				  name          : 'refList'
				  type          : 'Ljava/util/ArrayList;'
				  access        : 0x0002 (PRIVATE)
			  Direct methods    -
				#0              : (in Landroid/runtime/UncaughtExceptionHandler;)
				  name          : '<clinit>'
				  type          : '()V'
				  access        : 0x10008 (STATIC CONSTRUCTOR)
				  code          -
				  registers     : 3
				  ins           : 0
				  outs          : 3
				  insns size    : 10 16-bit code units
				  catches       : (none)
				  positions     : 
					0x0002 line=16
				  locals        : 
				#1              : (in Landroid/runtime/UncaughtExceptionHandler;)
				  name          : '<init>'
				  type          : '()V'
				  access        : 0x10001 (PUBLIC CONSTRUCTOR)
				  code          -
				  registers     : 4
				  ins           : 1
				  outs          : 4
				  insns size    : 22 16-bit code units
				  catches       : (none)
				  positions     : 
					0x0000 line=22
					0x0003 line=23
					0x0010 line=24
				  locals        : 
					0x0000 - 0x0016 reg=3 this Landroid/runtime/UncaughtExceptionHandler; 
		 */

		/// <summary>
		/// Runs the dexdump command to see if a class exists in a dex file
		/// </summary>
		/// <param name="className">A Java class name of the form 'Landroid/app/ActivityTracker;'</param>
		public static bool ContainsClass (string className, string dexFile, string androidSdkDirectory)
		{
			return ContainsClass (className, GetDexDump (dexFile, androidSdkDirectory));
		}

		public static bool ContainsClass (string className, IEnumerable<string> dexDump)
		{
			return dexDump.Any (line => line.Contains ("Class descriptor") && line.Contains (className));
		}

		/// <summary>
		/// Runs the dexdump command to see if a class exists in a dex file *and* has a public constructor
		/// </summary>
		/// <param name="className">A Java class name of the form 'Landroid/app/ActivityTracker;'</param>
		/// <param name="method">A Java method name of the form 'foo'</param>
		/// <param name="type">A Java method signature of the form '()V'</param>
		public static bool ContainsClassWithMethod (string className, string method, string type, string dexFile, string androidSdkDirectory)
		{
			return ContainsClassWithMethod (className, method, type, GetDexDump (dexFile, androidSdkDirectory));
		}

		public static bool ContainsClassWithMethod (string className, string method, string type, IEnumerable<string> dexDump)
		{
			bool inClass = false;
			bool hasName = false;
			foreach (var line in dexDump) {
				if (HasDexDumpName (line, "Class descriptor")) {
					inClass = ContainsDexDumpValue (line, "Class descriptor", className);
					hasName = false;
				} else if (inClass && HasDexDumpName (line, "name")) {
					hasName = ContainsDexDumpValue (line, "name", method);
				} else if (hasName && HasDexDumpName (line, "type")) {
					if (ContainsDexDumpValue (line, "type", type)) {
						return true;
					}
					hasName = false;
				}
			}
			return false;
		}

		static bool ContainsDexDumpValue (string line, string name, string value)
		{
			var separator = line.IndexOf (':');
			return separator >= 0 && HasDexDumpName (line, name, separator) &&
				line.Substring (separator + 1).Trim () == $"'{value}'";
		}

		static bool HasDexDumpName (string line, string name)
		{
			return HasDexDumpName (line, name, line.IndexOf (':'));
		}

		static bool HasDexDumpName (string line, string name, int separator)
		{
			return separator >= 0 && line.Substring (0, separator).Trim () == name;
		}

		public static IReadOnlyList<string> GetDexDump (string dexFile, string androidSdkDirectory)
		{
			return DexDump (dexFile, androidSdkDirectory);
		}

		static IReadOnlyList<string> DexDump (string dexFile, string androidSdkDirectory)
		{
			var lines = new List<string> ();
			var linesLock = new object ();
			var androidSdk = new AndroidSdkInfo ((l, m) => {
				Console.WriteLine ($"{l}: {m}");
				if (l == TraceLevel.Error) {
					throw new Exception (m);
				}
			}, androidSdkDirectory, javaSdkPath: AndroidSdkResolver.GetJavaSdkPath ());
			var buildToolsPath = androidSdk.GetBuildToolsPaths ().FirstOrDefault ();
			if (string.IsNullOrEmpty (buildToolsPath)) {
				throw new Exception ($"Unable to find build-tools in `{androidSdkDirectory}`!");
			}

			var psi = new ProcessStartInfo {
				FileName = Path.Combine (buildToolsPath, "dexdump"),
				Arguments = Path.GetFileName (dexFile),
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				WorkingDirectory = Path.GetDirectoryName (dexFile),
			};
			using (var p = new Process { StartInfo = psi }) {
				var errors = new List<string> ();
				p.ErrorDataReceived += (s, e) => {
					if (e.Data != null) {
						errors.Add (e.Data);
					}
				};
				p.OutputDataReceived += (s, e) => {
					if (e.Data != null) {
						lock (linesLock) {
							lines.Add (e.Data);
						}
					}
				};

				p.Start ();
				p.BeginErrorReadLine ();
				p.BeginOutputReadLine ();
				p.WaitForExit ();

				if (p.ExitCode != 0)
					throw new Exception (
						$"'{psi.FileName} {psi.Arguments}' exited with code: {p.ExitCode}" +
						$"{Environment.NewLine}stdout:{Environment.NewLine}{string.Join (Environment.NewLine, lines)}" +
						$"{Environment.NewLine}stderr:{Environment.NewLine}{string.Join (Environment.NewLine, errors)}");
			}
			return lines;
		}
	}
}
