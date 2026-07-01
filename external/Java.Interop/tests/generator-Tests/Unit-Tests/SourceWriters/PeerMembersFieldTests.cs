using generator.SourceWriters;
using MonoDroid.Generation;
using NUnit.Framework;
using Xamarin.Android.Binder;

namespace generatortests.SourceWriters
{
	[TestFixture]
	public class PeerMembersFieldTests : SourceWritersTestBase
	{
		[Test]
		public void PeerMembersField_Class ()
		{
			var field = new PeerMembersField (new CodeGenerationOptions { CodeGenerationTarget = CodeGenerationTarget.JavaInterop1 }, "B", "MyJavaType", false);

			Assert.AreEqual ("static readonly JniPeerMembers _members = new JniPeerMembers (\"B\", typeof (MyJavaType));", GetOutput (field).Trim ());
		}

		[Test]
		public void WeakImplementorField_Interface ()
		{
			var field = new PeerMembersField (new CodeGenerationOptions { CodeGenerationTarget = CodeGenerationTarget.XAJavaInterop1 }, "B", "IMyJavaType", true);

			Assert.AreEqual ("private static readonly JniPeerMembers _members = new XAPeerMembers (\"B\", typeof (IMyJavaType), isInterface: true);", GetOutput (field).Trim ());
		}
	}
}
