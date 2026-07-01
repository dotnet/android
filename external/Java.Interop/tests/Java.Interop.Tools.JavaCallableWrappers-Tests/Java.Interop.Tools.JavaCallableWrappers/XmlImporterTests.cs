using System.IO;
using System.Xml.Linq;
using Java.Interop.Tools.JavaCallableWrappers.Adapters;
using Java.Interop.Tools.TypeNameMappings;
using NUnit.Framework;

namespace Java.Interop.Tools.JavaCallableWrappersTests
{
	[TestFixture]
	public class XmlImporterTests
	{
		PackageNamingPolicy existingValue;

		[SetUp]
		public void SetUp ()
		{
			existingValue = JavaNativeTypeManager.PackageNamingPolicy;
			JavaNativeTypeManager.PackageNamingPolicy = PackageNamingPolicy.LowercaseCrc64;
		}

		[TearDown]
		public void TearDown ()
		{
			JavaNativeTypeManager.PackageNamingPolicy = existingValue;
		}

		[Test]
		public void ImportType_EmptyPackage_GeneratesCrc64Package ()
		{
			// Simulate the case where [Register("CustomView")] is used without a package prefix
			// The XML has an empty package attribute, and we expect XmlImporter to generate a crc64 package
			var xml = XElement.Parse (@"
				<type name=""CustomView"" 
				      package="""" 
				      partial_assembly_qualified_name=""MyApp.Views.CustomView, MyApp.Android"" 
				      extends_type=""android.view.View"">
					<constructors />
					<methods />
				</type>");

			var type = XmlImporter.ImportType (xml);

			// The generated package should be "crc64" + crc64hash of "MyApp.Views:MyApp.Android"
			Assert.AreEqual ("crc64b8e52a012da2d805", type.Package);
			Assert.AreEqual ("CustomView", type.Name);
		}

		[Test]
		public void ImportType_EmptyPackage_NestedType_GeneratesCrc64Package ()
		{
			// Simulate the case with a nested type
			var xml = XElement.Parse (@"
				<type name=""InnerView"" 
				      package="""" 
				      partial_assembly_qualified_name=""MyApp.Views.OuterView+InnerView, MyApp.Android"" 
				      extends_type=""android.view.View"">
					<constructors />
					<methods />
				</type>");

			var type = XmlImporter.ImportType (xml);

			// The generated package should be based on "MyApp.Views:MyApp.Android" (ignoring nested type part)
			Assert.AreEqual ("crc64b8e52a012da2d805", type.Package);
			Assert.AreEqual ("InnerView", type.Name);
		}

		[Test]
		public void ImportType_MissingPackageAttribute_GeneratesCrc64Package ()
		{
			// Simulate the case where the package attribute is completely missing
			var xml = XElement.Parse (@"
				<type name=""CustomView"" 
				      partial_assembly_qualified_name=""MyApp.Views.CustomView, MyApp.Android"" 
				      extends_type=""android.view.View"">
					<constructors />
					<methods />
				</type>");

			var type = XmlImporter.ImportType (xml);

			// The generated package should be "crc64" + crc64hash of "MyApp.Views:MyApp.Android"
			Assert.AreEqual ("crc64b8e52a012da2d805", type.Package);
		}

		[Test]
		public void ImportType_WithPackage_UsesProvidedPackage ()
		{
			// Ensure that when a package is provided, it is used directly
			var xml = XElement.Parse (@"
				<type name=""CustomView"" 
				      package=""my.custom.package"" 
				      partial_assembly_qualified_name=""MyApp.Views.CustomView, MyApp.Android"" 
				      extends_type=""android.view.View"">
					<constructors />
					<methods />
				</type>");

			var type = XmlImporter.ImportType (xml);

			Assert.AreEqual ("my.custom.package", type.Package);
		}

		[Test]
		public void ImportType_NoNamespace_ReturnsEmptyPackage ()
		{
			// When there's no namespace in the type name, we can't generate a package
			var xml = XElement.Parse (@"
				<type name=""CustomView"" 
				      package="""" 
				      partial_assembly_qualified_name=""CustomView, MyApp.Android"" 
				      extends_type=""android.view.View"">
					<constructors />
					<methods />
				</type>");

			var type = XmlImporter.ImportType (xml);

			// Without a namespace, we can't generate a crc64 package, so it should remain empty
			Assert.AreEqual ("", type.Package);
		}

		[Test]
		public void ImportType_XamarinAndroidToolsTests_MatchesExpectedCrc64 ()
		{
			// This test verifies the crc64 generation matches what JavaNativeTypeManager would generate
			// Based on the test data in JavaCallableWrapperGeneratorTests, namespace "Xamarin.Android.ToolsTests"
			// with assembly "Java.Interop.Tools.JavaCallableWrappers-Tests" should produce crc64197ae30a36756915
			var xml = XElement.Parse (@"
				<type name=""ExportsMembers"" 
				      package="""" 
				      partial_assembly_qualified_name=""Xamarin.Android.ToolsTests.ExportsMembers, Java.Interop.Tools.JavaCallableWrappers-Tests"" 
				      extends_type=""java.lang.Object"">
					<constructors />
					<methods />
				</type>");

			var type = XmlImporter.ImportType (xml);

			// This should match the expected crc64 from JavaCallableWrapperGeneratorTests
			Assert.AreEqual ("crc64197ae30a36756915", type.Package);
		}
	}
}
