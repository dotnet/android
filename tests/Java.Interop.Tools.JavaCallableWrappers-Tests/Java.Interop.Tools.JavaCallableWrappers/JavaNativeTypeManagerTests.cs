using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.Interop.Tools.JavaCallableWrappersTests
{
	[TestClass]
	public class JavaNativeTypeManagerTests
	{
		PackageNamingPolicy existingValue;

		[TestInitialize]
		public void SetUp ()
		{
			existingValue = JavaNativeTypeManager.PackageNamingPolicy;
		}

		[TestCleanup]
		public void TearDown ()
		{
			JavaNativeTypeManager.PackageNamingPolicy = existingValue;
		}

		[TestMethod]
		public void Crc64 ()
		{
			JavaNativeTypeManager.PackageNamingPolicy = PackageNamingPolicy.LowercaseCrc64;
			// System.String moved assemblies in .NET
			Assert.AreEqual ("crc64d04135c992393d83", JavaNativeTypeManager.GetPackageName (typeof (string)));
		}

		[TestMethod]
		public void Lowercase ()
		{
			JavaNativeTypeManager.PackageNamingPolicy = PackageNamingPolicy.Lowercase;
			Assert.AreEqual ("system", JavaNativeTypeManager.GetPackageName (typeof (string)));
		}

		[TestMethod]
		public void LowercaseWithAssemblyName ()
		{
			JavaNativeTypeManager.PackageNamingPolicy = PackageNamingPolicy.LowercaseWithAssemblyName;
			// System.String moved assemblies in .NET
			Assert.AreEqual ("assembly_system_private_corelib.system", JavaNativeTypeManager.GetPackageName (typeof (string)));
		}

		[TestMethod]
		[DataRow (typeof (string), "java/lang/String")]
		[DataRow (typeof (Type),   "java/lang/Object")]
		public void ToJniName (Type type, string expected)
		{
			string actual = JavaNativeTypeManager.ToJniName (type);
			Assert.AreEqual (expected, actual);
		}
	}
}
