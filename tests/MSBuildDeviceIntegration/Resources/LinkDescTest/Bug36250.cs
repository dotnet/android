using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace LinkTestLib
{
	// https://bugzilla.xamarin.com/show_bug.cgi?id=36250
	public class Bug36250
	{
		// [Test]
		public static string SerializeSearchRequestWithDictionary ()
		{
			var req = new SearchRequest () {
				Query = "query",

				Users = new List<string> () {
					"user_a", "user_b"
				},

				Filters = new List<string> () {
					"filter_a", "filter_b"
				},

				Parameters = new Dictionary<string, string> () {
					{ "param_key_b", "param_value_a" },
					{ "param_key_a", "param_value_b" },
				}
			};

			try {
				using (MemoryStream memoryStream = new MemoryStream ()) {
					var dataContractSerializer = new DataContractSerializer (typeof (SearchRequest));
					dataContractSerializer.WriteObject (memoryStream, req);
					string serializedDataContract = Encoding.UTF8.GetString (memoryStream.ToArray (), 0, (int) memoryStream.Length);
					return $"[PASS] SearchRequest successfully serialized: {serializedDataContract.Substring (0, 14)}";
				}
			} catch (Exception ex) {
				return $"[FAIL] SearchRequest serialization FAILED: {ex}";
			}
		}
	}

	[DataContract]
	public class SearchRequest
	{
		[DataMember]
		public string Query { get; set; }
		[DataMember]
		public List<string> Users { get; set; }
		[DataMember]
		public List<string> Filters { get; set; }
		[DataMember]
		public Dictionary<string, string> Parameters { get; set; }
	}
}
