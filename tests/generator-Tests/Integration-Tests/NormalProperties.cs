using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class NormalProperties : BaseGeneratorTest
	{
		[TestMethodAttribute]
		public void GeneratedOK ()
		{
			RunTarget (
					outputRelativePath:     "NormalProperties",
					apiDescriptionFile:     "TestInputs/NormalProperties/NormalProperties.xml",
					expectedRelativePath:   "NormalProperties");

			var output = File.ReadAllText (FullPath ("out.xaji/NormalProperties/Xamarin.Test.SomeObject.cs"));
			StringAssert.Contains (output, string.Join (Environment.NewLine, new [] {
				"\t\t\t\t\t_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);",
				"\t\t\t\t\tglobal::System.GC.KeepAlive (value);",
			}));
		}
	}
}
