using System.Runtime.Versioning;
using Android.Runtime;
using Java.Interop;

[assembly: SupportedOSPlatform ("android24.0")]

namespace System.IO
{
	public class Stream
	{
	}
}

namespace System.Xml
{
	public class XmlReader
	{
	}
}

namespace Java.Lang
{
	public interface ICharSequence
	{
	}
}

namespace System.Collections
{
	public interface IList
	{
	}

	public interface IDictionary
	{
	}

	public interface ICollection
	{
	}
}

namespace UserApp.JavaSourceParity
{
	[Register ("com/example/collision/SignatureCollision")]
	public class SignatureCollision : Java.Lang.Object
	{
	}

	public enum EnumCollision
	{
		None,
	}
}

namespace SpecialTypeCollision
{
	[Register ("com/example/collision/SpecialTypeCollisionPeer")]
	public class SpecialTypeCollisionPeer : Java.Lang.Object
	{
		[return: ExportParameter (ExportParameterKind.OutputStream)]
		[Export ("invalidStream")]
		public System.IO.Stream InvalidStream (
			[ExportParameter (ExportParameterKind.InputStream)] System.IO.Stream value)
			=> value;

		[return: ExportParameter (ExportParameterKind.XmlPullParser)]
		[ExportField ("INVALID_XML_FIELD")]
		public System.Xml.XmlReader InvalidXmlField () => new ();

		[Export ("invalidCharSequence")]
		public Java.Lang.ICharSequence InvalidCharSequence (Java.Lang.ICharSequence value) => value;

		[Export ("invalidList")]
		public System.Collections.IList InvalidList (System.Collections.IList value) => value;

		[Export ("invalidDictionary")]
		public System.Collections.IDictionary InvalidDictionary (System.Collections.IDictionary value) => value;

		[Export ("invalidCollection")]
		public System.Collections.ICollection InvalidCollection (System.Collections.ICollection value) => value;

		[Export ("notAConstructor", SuperArgumentsString = "")]
		public SpecialTypeCollisionPeer (
			[ExportParameter (ExportParameterKind.InputStream)] System.IO.Stream value)
		{
		}
	}
}
