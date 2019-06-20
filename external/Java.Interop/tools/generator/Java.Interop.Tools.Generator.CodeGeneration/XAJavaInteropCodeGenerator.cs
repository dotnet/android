using System;
using System.IO;

namespace MonoDroid.Generation
{
	class XAJavaInteropCodeGenerator : JavaInteropCodeGenerator
	{
		public XAJavaInteropCodeGenerator (TextWriter writer, CodeGenerationOptions options) : base (writer, options)
		{
		}

		protected override string GetPeerMembersType ()
		{
			return "XAPeerMembers";
		}
	}
}

