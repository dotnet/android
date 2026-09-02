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

		[Export ("notAConstructor", SuperArgumentsString = "")]
		public SpecialTypeCollisionPeer (
			[ExportParameter (ExportParameterKind.InputStream)] System.IO.Stream value)
		{
		}
	}
}
