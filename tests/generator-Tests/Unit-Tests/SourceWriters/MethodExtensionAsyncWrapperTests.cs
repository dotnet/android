using System.Linq;
using generator.SourceWriters;
using MonoDroid.Generation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests.SourceWriters
{
	[TestClassAttribute]
	public class MethodExtensionAsyncWrapperTests : SourceWritersTestBase
	{
		[TestMethodAttribute]
		public void MethodExtensionAsyncWrapper ()
		{
			var opt = new CodeGenerationOptions ();
			var klass = SupportTypeBuilder.CreateClass ("MyNamespace.MyObject", opt);
			var method = klass.Methods.First (m => m.Name == "GetCountForKey");

			var wrapper = new MethodExtensionAsyncWrapper (method, opt, "OtherObject");
			var expected =
@"public static global::System.Threading.Tasks.Task<int> GetCountForKeyAsync (this OtherObject self, string key)
{
	return global::System.Threading.Tasks.Task.Run (() => self.GetCountForKey (key));
}";

			Assert.AreEqual (expected, GetOutput (wrapper).Trim ());
		}

		[TestMethodAttribute]
		public void MethodExtensionAsyncWrapper_VoidReturnType ()
		{
			var opt = new CodeGenerationOptions ();
			var klass = SupportTypeBuilder.CreateClass ("MyNamespace.MyObject", opt);
			var method = klass.Methods.First (m => m.Name == "StaticMethod");

			var wrapper = new MethodExtensionAsyncWrapper (method, opt, "OtherObject");
			var expected =
@"public static global::System.Threading.Tasks.Task StaticMethodAsync (this OtherObject self)
{
	return global::System.Threading.Tasks.Task.Run (() => self.StaticMethod ());
}";

			Assert.AreEqual (expected, GetOutput (wrapper).Trim ());
		}
	}
}
