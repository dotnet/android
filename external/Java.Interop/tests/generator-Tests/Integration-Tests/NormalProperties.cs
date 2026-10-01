using System;
using System.IO;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class NormalProperties : BaseGeneratorTest
	{
		[Test]
		public void GeneratedOK ()
		{
			RunAllTargets (
					outputRelativePath:     "NormalProperties",
					apiDescriptionFile:     "expected.ji/NormalProperties/NormalProperties.xml",
					expectedRelativePath:   "NormalProperties");

			var output = File.ReadAllText (FullPath ("out.xaji/NormalProperties/Xamarin.Test.SomeObject.cs"));
			Assert.That (output, Does.Contain (string.Join (Environment.NewLine, new [] {
				"\t\t\t\t_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);",
				"\t\t\t\tglobal::System.GC.KeepAlive (value);",
			})));
		}
	}
}
