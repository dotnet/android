using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Xamarin.ProjectTools {
	public class SevenZipHelper : IDisposable {
		public string ArchivePath { get; private set; }

		public SevenZipHelper (string archivePath)
		{
			ArchivePath = archivePath;
		}

		public bool ExtractAll (string destinationDir)
		{
			return ExtractAll (destinationDir, 15 * 60 * 1000);
		}

		bool ExtractAll (string destinationDir, int timeoutMilliseconds)
		{
			var outputLock = new object ();
			bool acceptingOutput = true;
			using (var p = new Process ()) {
				var errorDone = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
				var outputDone = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
				p.StartInfo.FileName = Path.Combine ("7z");
				p.StartInfo.ArgumentList.Add ("x");
				p.StartInfo.ArgumentList.Add (ArchivePath);
				p.StartInfo.ArgumentList.Add ($"-o{destinationDir}");
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;
				p.ErrorDataReceived += (sender, e) => {
					if (e.Data == null)
						errorDone.TrySetResult (true);
					else
						WriteOutput (e.Data);
				};
				p.OutputDataReceived += (sender, e) => {
					if (e.Data == null)
						outputDone.TrySetResult (true);
					else
						WriteOutput (e.Data);
				};

				void WriteOutput (string line)
				{
					lock (outputLock) {
						if (acceptingOutput)
							Console.WriteLine (line);
					}
				}

				try {
					p.Start ();
					p.BeginOutputReadLine ();
					p.BeginErrorReadLine ();
					bool completed = p.WaitForExit (timeoutMilliseconds);
					if (!completed) {
						Console.Error.WriteLine ($"7z timed out after {timeoutMilliseconds}ms.");
						if (!p.HasExited) {
							try {
								p.Kill (entireProcessTree: true);
							} catch (InvalidOperationException) when (p.HasExited) {
								// The process exited before the kill request.
							}
							if (!p.WaitForExit (30000))
								Console.Error.WriteLine ("7z did not exit after termination.");
						}
					}
					if (!Builder.WaitForRedirectedOutput (outputDone.Task, errorDone.Task))
						Console.Error.WriteLine ("7z exited or timed out with redirected output still open.");
					return completed && p.ExitCode == 0;
				} finally {
					lock (outputLock) {
						acceptingOutput = false;
					}
				}
			}
		}

		public void Dispose ()
		{
		}

		public static SevenZipHelper Open (string path, FileMode fileMode)
		{
			return new SevenZipHelper (path);
		}
	}
}
