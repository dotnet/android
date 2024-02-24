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
#if NET
			// System.String moved assemblies in .NET
			Assert.AreEqual ("crc64d04135c992393d83", JavaNativeTypeManager.GetPackageName (typeof (string)));
#else   // !NET
			Assert.AreEqual ("crc64b74743e9328eed0a", JavaNativeTypeManager.GetPackageName (typeof (string)));
#endif  // !NET
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
#if NET
			// System.String moved assemblies in .NET
			Assert.AreEqual ("assembly_system_private_corelib.system", JavaNativeTypeManager.GetPackageName (typeof (string)));
#else   // !NET
			Assert.AreEqual ("assembly_mscorlib.system", JavaNativeTypeManager.GetPackageName (typeof (string)));
#endif  // !NET
		}

		[Test]
		[TestCase (typeof (string), "java/lang/String")]
		[TestCase (typeof (Type),   "java/lang/Object")]
		public void ToJniName (Type type, string expected)
		{
			string actual = JavaNativeTypeManager.ToJniName (type);
			Assert.AreEqual (expected, actual);
		}
	}
}
