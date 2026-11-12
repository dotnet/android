using System.Linq;
using generator.SourceWriters;
using MonoDroid.Generation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests.SourceWriters
{
	[TestClassAttribute]
	public class MethodExplicitInterfaceImplementationTests : SourceWritersTestBase
	{
		[TestMethodAttribute]
		public void MethodExplicitInterfaceImplementation ()
		{
			var opt = new CodeGenerationOptions ();
			var iface = SupportTypeBuilder.CreateInterface ("MyNamespace.IMyObject", opt);
			var method = iface.Methods.First (m => m.Name == "GetCountForKey");

			var wrapper = new MethodExplicitInterfaceImplementation (iface, method, opt);
			var expected =
@"int MyNamespace.IMyObject.GetCountForKey (string key)
{
	return GetCountForKey (key)
}";

			Assert.AreEqual (expected, GetOutput (wrapper).Trim ());
		}
	}
}
