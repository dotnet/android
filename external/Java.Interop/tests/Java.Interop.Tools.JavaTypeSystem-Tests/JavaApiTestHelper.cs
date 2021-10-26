using System;
using System.IO;
using Java.Interop.Tools.JavaTypeSystem.Models;

namespace Java.Interop.Tools.JavaTypeSystem.Tests
{
	public class JavaApiTestHelper
	{
		static readonly string TopDir = Path.Combine (Path.GetDirectoryName (typeof (JavaApiTestHelper).Assembly.Location), "..", "..");
		static readonly string ApiPath = Path.Combine (TopDir, "tests", "Java.Interop.Tools.JavaTypeSystem-Tests", "api-24.xml.in");

		public static JavaTypeCollection GetLoadedApi ()
		{
			return JavaXmlApiImporter.Parse (ApiPath);
		}

		public static JavaClassModel CreateClass (JavaPackage javaPackage, string javaNestedName, string javaVisibility = "public", bool javaAbstract = false, bool javaFinal = false, string javaBaseType = "java.lang.Object", string javaBaseTypeGeneric = "java.lang.Object", string javaDeprecated = "not deprecated", bool javaStatic = false, string jniSignature = "", string baseTypeJni = "java/lang/Object")
		{
			if (string.IsNullOrWhiteSpace (jniSignature))
				jniSignature = $"{(!string.IsNullOrWhiteSpace (javaPackage.Name) ? javaPackage.Name + "." : "")}{javaNestedName}".Replace ('.', '/');

			var klass = new JavaClassModel (
				javaPackage: javaPackage,
				javaNestedName: javaNestedName,
				javaVisibility: javaVisibility,
				javaAbstract: javaAbstract,
				javaFinal: javaFinal,
				javaBaseType: javaBaseType,
				javaBaseTypeGeneric: javaBaseTypeGeneric,
				javaDeprecated: javaDeprecated,
				javaStatic: javaStatic,
				jniSignature: jniSignature,
				baseTypeJni: baseTypeJni
			);

			return klass;
		}
	}
}
