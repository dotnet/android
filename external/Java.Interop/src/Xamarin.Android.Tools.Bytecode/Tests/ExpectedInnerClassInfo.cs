using System;

using Xamarin.Android.Tools.Bytecode;

using NAssert   = NUnit.Framework.Assert;

namespace Xamarin.Android.Tools.BytecodeTests {

	class ExpectedInnerClassInfo {

		public  string              InnerClassName;
		public  string              OuterClassName;
		public  string              InnerName;
		public  ClassAccessFlags    AccessFlags;

		public void Assert (InnerClassInfo info)
		{
			NAssert.AreEqual (InnerClassName,   info.InnerClass.Name.Value);
			NAssert.AreEqual (OuterClassName,   info.OuterClass.Name.Value);
			NAssert.AreEqual (InnerName,        info.InnerName);
			NAssert.AreEqual (AccessFlags,      info.InnerClassAccessFlags);
		}
	}
}

