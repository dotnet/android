using generator.SourceWriters;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests.SourceWriters
{
	[TestFixture]
	public class PeerMembersFieldTests : SourceWritersTestBase
	{
		[Test]
		public void PeerMembersField_XamarinAndroidClass ()
		{
			var field = new PeerMembersField (new CodeGenerationOptions (), "B", "MyJavaType", false);

			Assert.AreEqual ("static readonly JniPeerMembers _members = new JniPeerMembers (\"B\", typeof (MyJavaType));", GetOutput (field).Trim ());
		}

		[Test]
		public void PeerMembersField_XAInterface ()
		{
			var field = new PeerMembersField (new CodeGenerationOptions (), "B", "IMyJavaType", true);

			Assert.AreEqual ("private static readonly JniPeerMembers _members = new JniPeerMembers (\"B\", typeof (IMyJavaType), isInterface: true);", GetOutput (field).Trim ());
		}
	}
}
