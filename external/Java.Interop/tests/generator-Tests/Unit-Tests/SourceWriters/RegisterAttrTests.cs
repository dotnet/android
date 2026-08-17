using System.IO;
using System.Reflection;
using generator.SourceWriters;
using NUnit.Framework;
using Xamarin.SourceWriter;

namespace generatortests.SourceWriters
{
	[TestFixture]
	public class RegisterAttrTests : SourceWritersTestBase
	{
		static string GetAttributeOutput (RegisterAttr attribute)
		{
			var writer = new StringWriter ();
			attribute.WriteAttribute (new CodeWriter (writer));
			return writer.ToString ();
		}

		[Test]
		public void RegisterAttribute_ManagedCreated ()
		{
			var attribute = new RegisterAttr ("my/ManagedPeer") {
				IsManagedCreated = true,
				UseGlobal = true,
			};

			Assert.AreEqual (
				"[global::Android.Runtime.Register (\"my/ManagedPeer\", IsManagedCreated=true)]",
				GetAttributeOutput (attribute).Trim ());
		}

		[Test]
		public void JniTypeSignatureAttribute_ManagedCreated ()
		{
			var attribute = new RegisterAttr ("my/ManagedPeer", connector: "My.ManagedPeerInvoker") {
				IsManagedCreated = true,
				MemberType = MemberTypes.TypeInfo,
			};

			Assert.AreEqual (
				"[global::Java.Interop.JniTypeSignature (\"my/ManagedPeer\", GenerateJavaPeer=true, IsManagedCreated=true, InvokerType=typeof (My.ManagedPeerInvoker))]",
				GetAttributeOutput (attribute).Trim ());
		}
	}
}
