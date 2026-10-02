using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	public abstract class HostProcessFixture
	{
		protected const int OutputLineCount = 2048;
		protected static readonly string Padding = new string ('x', 128);
		protected string directory = "";
		protected string previousJdk;
		string previousPath;
		object previousVersion;
		protected readonly List<string> processIds = new List<string> ();

		[SetUp]
		public void SetUp ()
		{
			directory = Path.Combine (Path.GetTempPath (), $"host-process-{Guid.NewGuid ():N}");
			Directory.CreateDirectory (Path.Combine (directory, "jdk", "bin"));
			previousJdk = Environment.GetEnvironmentVariable ("TEST_ANDROID_JDK_PATH");
			previousPath = Environment.GetEnvironmentVariable ("PATH");
			previousVersion = JavaVersionField.GetValue (null);
			Environment.SetEnvironmentVariable ("TEST_ANDROID_JDK_PATH", Path.Combine (directory, "jdk"));
			Environment.SetEnvironmentVariable ("PATH", directory + Path.PathSeparator + previousPath);
			JavaVersionField.SetValue (null, null);
		}

		[TearDown]
		public void TearDown ()
		{
			try {
				foreach (var file in processIds) {
					if (!File.Exists (file))
						continue;
					int id = int.Parse (File.ReadAllText (file).Trim (), CultureInfo.InvariantCulture);
					try {
						using var process = Process.GetProcessById (id);
						if (!process.HasExited) {
							process.Kill (entireProcessTree: true);
							Assert.IsTrue (process.WaitForExit (5000), $"Fixture process {id} did not exit.");
						}
					} catch (ArgumentException) {
						// The fixture process has already exited.
					}
				}
			} finally {
				processIds.Clear ();
				Environment.SetEnvironmentVariable ("TEST_ANDROID_JDK_PATH", previousJdk);
				Environment.SetEnvironmentVariable ("PATH", previousPath);
				JavaVersionField.SetValue (null, previousVersion);
				FileSystemUtils.DeleteDirectoryWithRetry (directory);
			}
		}

		static FieldInfo JavaVersionField =>
			typeof (AndroidSdkResolver).GetField ("JavaSdkVersionString", BindingFlags.NonPublic | BindingFlags.Static) ??
			throw new MissingFieldException (nameof (AndroidSdkResolver), "JavaSdkVersionString");

		protected string CreateScript (string name, string contents)
		{
			if (OperatingSystem.IsWindows ())
				throw new PlatformNotSupportedException ("Shell fixtures require Unix.");
			var path = Path.Combine (directory, name);
			var parent = Path.GetDirectoryName (path);
			if (parent == null)
				throw new InvalidOperationException ($"No parent directory for '{path}'.");
			Directory.CreateDirectory (parent);
			var pidFile = Path.Combine (directory, $"{processIds.Count}.pid");
			processIds.Add (pidFile);
			using (var writer = new StreamWriter (path)) {
				writer.WriteLine ("#!/bin/sh");
				writer.WriteLine ($"echo $$ > {Quote (pidFile)}");
				writer.WriteLine (contents);
			}
			File.SetUnixFileMode (path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
			return path;
		}

		protected static string Quote (string value) => "'" + value.Replace ("'", "'\"'\"'") + "'";

		protected static string LargeOutput => $$"""
			emit () {
				i=0
				while [ "$i" -lt {{OutputLineCount}} ]; do
					printf '%s-%04d:%s\n' "$1" "$i" '{{Padding}}'
					i=$((i + 1))
				done
			}
			emit stdout &
			emit stderr >&2 &
			wait
			printf stdout-tail
			printf stderr-tail >&2
			""";

		protected static ProcessStartInfo StartInfo (string script) => new ProcessStartInfo (script) {
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};

		protected static T Invoke<T> (Type type, string name, object instance, Type [] parameterTypes, params object [] arguments)
		{
			if (InvokeRaw (type, name, instance, parameterTypes, arguments) is T result)
				return result;
			throw new InvalidOperationException ($"Unexpected result from {type.Name}.{name}.");
		}

		protected static object InvokeRaw (Type type, string name, object instance, Type [] parameterTypes, params object [] arguments)
		{
			var method = type.GetMethod (name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance, null, parameterTypes, null);
			if (method == null)
				throw new MissingMethodException (type.FullName, name);
			try {
				return method.Invoke (instance, arguments);
			} catch (TargetInvocationException ex) {
				if (ex.InnerException is Exception inner)
					ExceptionDispatchInfo.Capture (inner).Throw ();
				throw;
			}
		}

		protected static T WithDeadline<T> (Func<T> action)
		{
			var task = Task.Run (action);
			var completed = Task.WhenAny (task, Task.Delay (TimeSpan.FromSeconds (15))).GetAwaiter ().GetResult ();
			Assert.AreSame (task, completed, "Process helper exceeded its test deadline.");
			return task.GetAwaiter ().GetResult ();
		}

		protected static string [] Lines (string output) => output.Split (new [] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

		protected static void AssertOutput (string output)
		{
			var lines = Lines (output);
			foreach (var stream in new [] { "stdout", "stderr" }) {
				var expected = Enumerable.Range (0, OutputLineCount).Select (i => $"{stream}-{i:0000}:{Padding}").Append ($"{stream}-tail");
				CollectionAssert.AreEqual (expected, lines.Where (line => line.StartsWith (stream + "-", StringComparison.Ordinal)), stream);
			}
		}

		protected static void AssertProcessExited (string pidFile)
		{
			int id = int.Parse (File.ReadAllText (pidFile).Trim (), CultureInfo.InvariantCulture);
			Assert.IsTrue (SpinWait.SpinUntil (() => {
				try {
					using var process = Process.GetProcessById (id);
					return process.HasExited;
				} catch (ArgumentException) {
					return true;
				}
			}, TimeSpan.FromSeconds (10)), $"Fixture process {id} is still alive.");
		}
	}
}
