#nullable enable
namespace Xamarin.Android.Tasks;

	/// Write a list of Item to a file
	///
	public class RtxtWriter {
		public void Write (string file, IList<R> items)
		{
			if (file == null)
				throw new ArgumentNullException (nameof (file));
			if (items == null)
				throw new ArgumentNullException (nameof (items));

			using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) {
				foreach (var item in items) {
					sw.WriteLine (item.ToString ());
				}
				sw.Flush ();
				Files.CopyIfStreamChanged (sw.BaseStream, file);
			}
		}
	}
