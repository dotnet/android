using System;
using System.IO;
using System.Linq;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Tasks
{
	public class ZipArchiveEx : IDisposable
	{

		public static int ZipFlushLimit = 50;

		ZipArchive zip;
		string archive;

		public ZipArchive Archive {
			get { return zip; }
		}

		public ZipArchiveEx (string archive) : this (archive, FileMode.CreateNew)
		{
		}

		public ZipArchiveEx(string archive, FileMode filemode)
		{
			this.archive = archive;
			zip = ZipArchive.Open(archive, filemode);
		}

		public void Flush ()
		{
			if (zip != null) {
				zip.Close ();
				zip.Dispose ();
				zip = null;
			}
			// Requied to ensure that all the FileStream handles are disposed of. 
			GC.Collect ();
			zip = ZipArchive.Open (archive, FileMode.Open);
		}

		string ArchiveNameForFile (string filename, string directoryPathInZip)
		{
			if (string.IsNullOrEmpty (filename)) {
				throw new ArgumentNullException (nameof (filename));
			}
			string pathName;
			if (string.IsNullOrEmpty (directoryPathInZip)) {
				pathName = Path.GetFileName (filename);
			}
			else {
				pathName = Path.Combine (directoryPathInZip, Path.GetFileName (filename));
			}
			return pathName.Replace ("\\", "/");
		}

		void AddFiles (string folder, string folderInArchive)
		{
			int count = 0;
			foreach (string fileName in Directory.GetFiles (folder, "*.*", SearchOption.TopDirectoryOnly)) {
				var fi = new FileInfo (fileName);
				if ((fi.Attributes & FileAttributes.Hidden) != 0)
					continue;
				zip.AddFile (fileName, ArchiveNameForFile (fileName, folderInArchive));
				count++;
				if (count == ZipArchiveEx.ZipFlushLimit) {
					Flush ();
					count = 0;
				}
			}
		}

		public void AddDirectory (string folder, string folderInArchive)
		{
			AddFiles (folder, folderInArchive);
			foreach (string dir in Directory.GetDirectories (folder, "*", SearchOption.AllDirectories)) {
				var di = new DirectoryInfo (dir);
				if ((di.Attributes & FileAttributes.Hidden) != 0)
					continue;
				var internalDir = dir.Replace ("./", string.Empty).Replace (folder, string.Empty);
				string fullDirPath = folderInArchive + internalDir;
				zip.CreateDirectory (fullDirPath);
				AddFiles (dir, fullDirPath);
			}
		}

		public void Close ()
		{
			if (zip != null) {
				zip.Close ();
			}
		}

		public void Dispose ()
		{
			Dispose(true);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose(bool disposing) {
			if (disposing) {
				if (zip != null) {
					zip.Close ();
					zip.Dispose ();
					zip = null;
				}
			}	
		}
	}
}
