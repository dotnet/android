using System;
using System.IO;
using System.Diagnostics;

namespace Xamarin.ProjectTools {
	public class SevenZipHelper : IDisposable {
		public string ArchivePath { get; private set; }

		public SevenZipHelper (string archivePath)
		{
			ArchivePath = archivePath;
		}

		public bool ExtractAll (string destinationDir)
		{
			using (var p = new Process ()) {
				p.StartInfo.FileName = Path.Combine ("7z");
				p.StartInfo.Arguments = $"x {ArchivePath} -o{destinationDir}";
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;
				p.ErrorDataReceived += (sender, e) => {
					if (e.Data != null) {
						Console.WriteLine (e.Data);
					}
				};
				p.ErrorDataReceived += (sender, e) => {
					if (e.Data != null) {
						Console.WriteLine (e.Data);
					}
				};

				p.Start ();
				p.BeginOutputReadLine ();
				p.BeginErrorReadLine ();
				bool completed = p.WaitForExit ((int) new TimeSpan (0, 15, 0).TotalMilliseconds);
				return completed && p.ExitCode == 0;
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
