using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	/// Write a list of Item to a file
	///
	public class RtxtWriter {
		public void Write (string file, IList<R> items)
		{
			using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) {
				foreach (var item in items) {
					sw.WriteLine (item.ToString ());
				}
				sw.Flush ();
				Files.CopyIfStreamChanged (sw.BaseStream, file);
			}
		}
	}
}
