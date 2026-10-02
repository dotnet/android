using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.BuildTools.Tests
{
	static class ProcessTestChild
	{
		const int LineCount = 5000;
		static readonly string Padding = new string ('x', 128);

		static int Main (string [] args)
		{
			if (args [0] == "invoke-ci") {
				var assembly = Assembly.LoadFrom (args [1]);
				var program = assembly.GetType ("Program") ?? throw new InvalidOperationException ("Missing CI program.");
				var method = program.GetMethods (BindingFlags.Static | BindingFlags.NonPublic)
					.Single (m => m.Name.Contains ($"g__{args [2]}|", StringComparison.Ordinal));
				var child = StartInfo (args.Skip (4).ToArray ());
				try {
					object result = args [2] == "Run"
						? method.Invoke (null, new object [] { child.FileName, child.ArgumentList.ToArray () })
						: method.Invoke (null, new object [] { child, TimeSpan.FromMilliseconds (int.Parse (args [3])) });
					var capture = ((int Code, string Stdout, string Stderr)) result;
					Console.Write (JsonSerializer.Serialize (new { capture.Code, capture.Stdout, capture.Stderr }));
					return 0;
				} catch (TargetInvocationException ex) {
					Console.Error.WriteLine (ex.InnerException ?? ex);
					return 70;
				}
			}

			if (args [0] == "hang" || args [0] == "hold-pipes" || args [0] == "closed-pipes") {
				if (args.Length > 1) {
					File.WriteAllText (args [1], Environment.ProcessId.ToString ());
				}
				Console.WriteLine ($"pid:{Environment.ProcessId}");
				if (args [0] == "closed-pipes") {
					if (OperatingSystem.IsWindows ()) {
						if (!CloseHandle (GetStdHandle (-11)) || !CloseHandle (GetStdHandle (-12))) {
							throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
						}
					} else if (Close (1) != 0 || Close (2) != 0) {
						throw new System.ComponentModel.Win32Exception (Marshal.GetLastWin32Error ());
					}
				}
				Thread.Sleep (60000);
				return 0;
			}
			if (args [0] == "orphan") {
				var info = StartInfo ("hold-pipes");
				info.RedirectStandardOutput = false;
				info.RedirectStandardError = false;
				using var holder = Process.Start (info) ?? throw new InvalidOperationException ("Could not start pipe holder.");
				File.WriteAllText (args [1], holder.Id.ToString ());
				return 0;
			}
			if (args [0] == "stream") {
				Console.WriteLine ($"pid:{Environment.ProcessId}");
				while (true) {
					Console.Error.WriteLine (Padding);
					Console.WriteLine (Padding);
					Thread.Sleep (10);
				}
			}
			if (args [0] == "small") {
				Console.WriteLine ("stdout");
				Console.Error.WriteLine ("stderr");
				return 0;
			}
			if (args [0] == "delayed") {
				Thread.Sleep (200);
				Console.WriteLine ("stdout");
				return 0;
			}
			if (args [0] == "exact") {
				Console.Out.Write (" leading\r\n\ntrailing\u00e9\0");
				Console.Error.Write (" error\t\r\nlast");
				return 0;
			}
			if (args [0] == "version" || args [0] == "version-failure") {
				Console.WriteLine ("tool 8.2.1");
				WriteLines (Console.Out, "ignored", LineCount);
				return args [0] == "version-failure" ? 7 : 0;
			}

			var tool = Path.GetFileNameWithoutExtension (Assembly.GetExecutingAssembly ().Location);
			if (tool == "Microsoft.DotNet.ApiCompat") {
				var scenario = File.ReadAllText (args [0]);
				switch (scenario) {
				case "silent-failure":
					return 7;
				case "reported-failure":
					Console.WriteLine ("Total issues: 0");
					return 7;
				case "crash":
					File.AppendAllText (args [0] + ".attempts", "attempt\n");
					Console.Error.WriteLine ("Native Crash Reporting");
					return 1;
				case "breakages":
					Console.WriteLine ("Total issues: 1");
					WriteBoth ("out-issue", "err-issue", LineCount);
					return 1;
				default:
					Console.WriteLine ("Total issues: 0");
					WriteBoth (" ", " ", LineCount, padding: "");
					return 0;
				}
			}
			if (tool == "Microsoft.DotNet.GenAPI") {
				var scenario = File.ReadAllText (args [0]);
				switch (scenario) {
				case "members-contract":
					WriteBoth ("out-member", "err-member", LineCount);
					break;
				case "members-implementation":
					WriteBoth ("out-member", "err-member", LineCount - 1);
					break;
				case "syntax-contract":
				case "syntax-implementation":
					Console.WriteLine ("namespace Tests\n{\n[TypeAttribute]\npublic partial class Example\n{");
					if (scenario == "syntax-contract") {
						Console.WriteLine ("[MethodAttribute]\npublic void Missing ();");
					}
					Console.WriteLine ("}\n}");
					break;
				case "malformed":
					Console.WriteLine ("}");
					break;
				case "failure":
					Console.Error.WriteLine ("GenAPI failed");
					return 7;
				}
				return 0;
			}

			WriteBoth ("out", "err", LineCount);
			Console.Out.Write ("stdout tail");
			Console.Error.Write ("stderr tail");
			return args.Length > 1 ? int.Parse (args [1]) : 0;
		}

		static ProcessStartInfo StartInfo (params string [] args)
		{
			var info = new ProcessStartInfo (Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet") {
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			info.ArgumentList.Add (Assembly.GetExecutingAssembly ().Location);
			foreach (var arg in args) {
				info.ArgumentList.Add (arg);
			}
			return info;
		}

		static void WriteBoth (string stdout, string stderr, int count, string padding = null)
		{
			var error = Task.Run (() => WriteLines (Console.Error, stderr, count, padding));
			WriteLines (Console.Out, stdout, count, padding);
			error.GetAwaiter ().GetResult ();
		}

		static void WriteLines (TextWriter writer, string prefix, int count, string padding = null)
		{
			for (int i = 0; i < count; i++) {
				writer.WriteLine (padding == "" ? prefix : $"{prefix}-{i:D5}:{padding ?? Padding}");
			}
		}

		[DllImport ("kernel32.dll")]
		static extern IntPtr GetStdHandle (int handle);

		[DllImport ("kernel32.dll", SetLastError = true)]
		static extern bool CloseHandle (IntPtr handle);

		[DllImport ("libc", EntryPoint = "close", SetLastError = true)]
		static extern int Close (int descriptor);
	}
}
