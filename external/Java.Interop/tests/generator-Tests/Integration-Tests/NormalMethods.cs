using System;
using System.IO;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class NormalMethods : BaseGeneratorTest
	{
		protected override bool TryJavaInterop1 => false;

		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath:     "NormalMethods",
					apiDescriptionFile:     "expected.ji/NormalMethods/NormalMethods.xml",
					expectedRelativePath:   "NormalMethods");

			var output = File.ReadAllText (FullPath ("out.xaji/NormalMethods/Xamarin.Test.SomeObject.cs"));
			Assert.That (output, Does.Contain (string.Join (Environment.NewLine, new [] {
				"\t\t\tvar __result = __rm;",
				"\t\t\tglobal::System.GC.KeepAlive (o);",
				"\t\t\tglobal::System.GC.KeepAlive (t);",
				"\t\t\treturn __result;",
			})));
			Assert.That (output, Does.Contain (string.Join (Environment.NewLine, new [] {
				"\t\t\t_members.InstanceMethods.FinishCreateInstance (__id, this, __args);",
				"\t\t\tglobal::System.GC.KeepAlive (c);",
			})));
			Assert.That (output, Does.Contain (string.Join (Environment.NewLine, new [] {
				"\t\t\ttry {",
				"\t\t\t\tJniArgumentValue* __args = stackalloc JniArgumentValue [3];",
			})));
			Assert.That (output, Does.Contain (string.Join (Environment.NewLine, new [] {
				"\t\t\t} finally {",
				"\t\t\t\tJNIEnv.DeleteLocalRef (native_astring);",
				"\t\t\t\tglobal::System.GC.KeepAlive (anObject);",
			})));
		}
	}
}
