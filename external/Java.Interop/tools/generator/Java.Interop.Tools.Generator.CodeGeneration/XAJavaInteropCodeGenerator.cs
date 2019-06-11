using System;

namespace MonoDroid.Generation {

	class XAJavaInteropCodeGenerator : JavaInteropCodeGenerator {

		protected override string GetPeerMembersType ()
		{
			return "XAPeerMembers";
		}
	}
}

