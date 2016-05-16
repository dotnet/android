using System;
using System.IO;

using NUnit.Framework;

namespace System.IOTests {

	[TestFixture]
	public class DriveInfoTest {

		[Test]
		public void TotalFreeSpace_IsNotInt64_MaxValue ()
		{
			foreach (DriveInfo drive in DriveInfo.GetDrives()) {
				if (drive.IsReady) {
					try {
						Console.WriteLine ("# DriveInfo: Name={0}; TotalFreeSpace={1}", drive.Name, drive.TotalFreeSpace);
						Assert.AreNotEqual (drive.TotalFreeSpace, long.MaxValue);
					} catch (UnauthorizedAccessException e) {
						Console.Error.WriteLine ("DriveInfo.TotalFreeSpace IGNORING path '{0}': {1}", drive.Name, e);
					} catch (IOException e) {
						Console.Error.WriteLine ("DriveInfo.TotalFreeSpace IGNORING path '{0}': {1}", drive.Name, e);
					}
				}
			}
		}
	}
}
