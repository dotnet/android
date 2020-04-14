using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Java.Interop.Tools.TypeNameMappings;
using NUnit.Framework;

namespace Java.Interop.Tools.JavaCallableWrappersTests
{
	[TestFixture]
	public class JavaNativeTypeManagerTests
	{
		PackageNamingPolicy existingValue;

		[SetUp]
		public void SetUp ()
		{
			existingValue = JavaNativeTypeManager.PackageNamingPolicy;
		}

		[TearDown]
		public void TearDown ()
		{
			JavaNativeTypeManager.PackageNamingPolicy = existingValue;
		}

		[Test]
		public void Crc64 ()
		{
			JavaNativeTypeManager.PackageNamingPolicy = PackageNamingPolicy.LowercaseCrc64;
			Assert.AreEqual ("c64r279bf423bcb581100", JavaNativeTypeManager.GetPackageName (typeof (string)));
		}

		[Test]
		public void Lowercase ()
		{
			JavaNativeTypeManager.PackageNamingPolicy = PackageNamingPolicy.Lowercase;
			Assert.AreEqual ("system", JavaNativeTypeManager.GetPackageName (typeof (string)));
		}

		[Test]
		public void LowercaseWithAssemblyName ()
		{
			JavaNativeTypeManager.PackageNamingPolicy = PackageNamingPolicy.LowercaseWithAssemblyName;
			Assert.AreEqual ("assembly_mscorlib.system", JavaNativeTypeManager.GetPackageName (typeof (string)));
		}
	}
}
