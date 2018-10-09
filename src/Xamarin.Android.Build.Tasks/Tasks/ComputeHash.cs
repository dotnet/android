using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks {
	public class ComputeHash : Task {

		List<ITaskItem> output = new List<ITaskItem> ();

		[Required]
		public ITaskItem [] Source { get; set; }

		public bool CopyMetaData { get; set; } = true;

		[Output]
		public ITaskItem [] Output { get => output.ToArray (); }

		public override bool Execute ()
		{
			Log.LogDebugTaskItems ("Source : ", Source);
			using (var sha1 = SHA1.Create ()) {

				foreach (var item in Source) {
					var newItem = new TaskItem(item.ItemSpec, new Dictionary<string, string>() {
						{ "Hash", HashItemSpec (sha1, item.ItemSpec) }
					});
					if (CopyMetaData)
						item.CopyMetadataTo (newItem);
					output.Add (newItem);
				}
				Log.LogDebugTaskItems ("Output : ", Output);
				return !Log.HasLoggedErrors;
			}
		}

		string HashItemSpec (SHA1 sha1, string hashInput)
		{
			var hash = sha1.ComputeHash (Encoding.UTF8.GetBytes (hashInput));
			var hashResult = new StringBuilder (hash.Length * 2);

			foreach (byte b in hash) {
				hashResult.Append (b.ToString ("x2"));
			}
			return hashResult.ToString ();
		}
	}
}
