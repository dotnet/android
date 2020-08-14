using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class RegisterAttr : AttributeWriter
	{
		public string Name { get; set; }
		public string Signature { get; set; }
		public string Connector { get; set; }
		public bool DoNotGenerateAcw { get; set; }
		public string AdditionalProperties { get; set; }
		public bool UseGlobal { get; set; }	// TODO: Temporary for matching existing unit tests
		public bool UseShortForm { get; set; }  // TODO: Temporary for matching existing unit tests
		public bool AcwLast { get; set; }       // TODO: Temporary for matching existing unit tests

		public RegisterAttr (string name, string signature = null, string connector = null, bool noAcw = false, string additionalProperties = null)
		{
			Name = name;
			Signature = signature;
			Connector = connector;
			DoNotGenerateAcw = noAcw;
			AdditionalProperties = additionalProperties;
		}

		public override void WriteAttribute (CodeWriter writer)
		{
			var sb = new StringBuilder ();

			if (UseGlobal)
				sb.Append ($"[global::Android.Runtime.Register (\"{Name}\"");
			else
				sb.Append ($"[Register (\"{Name}\"");

			if ((Signature.HasValue () || Connector.HasValue ()) && !UseShortForm)
				sb.Append ($", \"{Signature}\", \"{Connector}\"");

			if (DoNotGenerateAcw && !AcwLast)
				sb.Append (", DoNotGenerateAcw=true");

			if (AdditionalProperties.HasValue ())
				sb.Append (AdditionalProperties);

			if (DoNotGenerateAcw && AcwLast)
				sb.Append (", DoNotGenerateAcw=true");

			sb.Append (")]");

			writer.WriteLine (sb.ToString ());
		}
	}
}
