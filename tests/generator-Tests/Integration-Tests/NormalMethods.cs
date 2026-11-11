using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class NormalMethods : BaseGeneratorTest
	{

		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "NormalMethods",
					apiDescriptionFile:     "TestInputs/NormalMethods/NormalMethods.xml",
					expectedRelativePath:   "NormalMethods");

			var output = File.ReadAllText (FullPath ("out.xaji/NormalMethods/Xamarin.Test.SomeObject.cs"));
			StringAssert.Contains (output, string.Join (Environment.NewLine, new [] {
				"\t\t\t\tvar __result = __rm;",
				"\t\t\t\tglobal::System.GC.KeepAlive (o);",
				"\t\t\t\tglobal::System.GC.KeepAlive (t);",
				"\t\t\t\treturn __result;",
			}));
			StringAssert.Contains (output, string.Join (Environment.NewLine, new [] {
				"\t\t\t\t_members.InstanceMethods.FinishCreateInstance (__id, this, __args);",
				"\t\t\t\tglobal::System.GC.KeepAlive (c);",
			}));
			StringAssert.Contains (output, string.Join (Environment.NewLine, new [] {
				"\t\t\t\ttry {",
				"\t\t\t\t\tJniArgumentValue* __args = stackalloc JniArgumentValue [3];",
			}));
			StringAssert.Contains (output, string.Join (Environment.NewLine, new [] {
				"\t\t\t\t} finally {",
				"\t\t\t\t\tJNIEnv.DeleteLocalRef (native_astring);",
				"\t\t\t\t\tglobal::System.GC.KeepAlive (anObject);",
			}));
		}
	}
}
