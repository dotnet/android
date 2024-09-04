using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	static class ResourceData
	{
		static Lazy<byte[]> javaSourceJarTestJar        = new Lazy<byte[]>(() => GetResourceData ("javasourcejartest.jar"));
		static Lazy<byte[]> javaSourceJarTestSourcesJar = new Lazy<byte[]>(() => GetResourceData ("javasourcejartest-sources.jar"));
		static Lazy<byte[]> javaSourceJarTestJavadocJar = new Lazy<byte[]>(() => GetResourceData ("javasourcejartest-javadoc.jar"));
		static Lazy<byte []> library1Aar = new Lazy<byte []> (() => GetResourceData ("library1.aar"));
		static Lazy<byte []> library2Aar = new Lazy<byte []> (() => GetResourceData ("library2.aar"));
		static Lazy<byte []> apacheHttpClient_cs = new Lazy<byte []> (() => GetResourceData ("ApacheHttpClient.cs"));
		static Lazy<byte []> javadocCopyright = new Lazy<byte []> (() => GetResourceData ("javadoc-copyright.xml"));
		static Lazy<byte []> javaSourceTestExtension = new Lazy<byte []> (() => GetResourceData ("JavaSourceTestExtension.java"));
		static Lazy<byte []> javaSourceTestInterface = new Lazy<byte []> (() => GetResourceData ("JavaSourceTestInterface.java"));
		static Lazy<byte []> remapActivityJava = new Lazy<byte []> (() => GetResourceData ("RemapActivity.java"));
		static Lazy<byte []> remapActivityXml = new Lazy<byte []> (() => GetResourceData ("RemapActivity.xml"));
		static Lazy<byte []> idmStaticMethodsInterface = new Lazy<byte []> (() => GetResourceData ("StaticMethodsInterface.java"));

		static Lazy<byte []> rtxt = new Lazy<byte []> (() => GetResourceData ("R.txt"));

		public  static  byte[]  JavaSourceJarTestJar            => javaSourceJarTestJar.Value;
		public  static  byte[]  JavaSourceJarTestSourcesJar     => javaSourceJarTestSourcesJar.Value;
		public  static  byte[]  JavaSourceJarTestJavadocJar     => javaSourceJarTestJavadocJar.Value;
		public  static  byte [] Library1Aar => library1Aar.Value;
		public  static  byte [] Library2Aar => library2Aar.Value;
		public  static  byte [] ApacheHttpClient_cs => apacheHttpClient_cs.Value;
		public  static  byte [] JavadocCopyright => javadocCopyright.Value;

		public  static  string JavaSourceTestExtension => Encoding.ASCII.GetString (javaSourceTestExtension.Value);
		public  static  string JavaSourceTestInterface => Encoding.ASCII.GetString (javaSourceTestInterface.Value);
		public  static  string RemapActivityJava => Encoding.UTF8.GetString (remapActivityJava.Value);
		public  static  string RemapActivityXml => Encoding.UTF8.GetString (remapActivityXml.Value);
		public  static  string IdmStaticMethodsInterface => Encoding.UTF8.GetString (idmStaticMethodsInterface.Value);

		public  static  string RTxt => Encoding.UTF8.GetString (rtxt.Value);

		static byte[] GetResourceData (string name)
		{
			using var s = typeof (ResourceData).Assembly.GetManifestResourceStream (name);
			if (s == null) {
				throw new NotSupportedException ($"Could not find resource `{name}` in assembly `{typeof (ResourceData).Assembly}`!");
			}
			using var m = new MemoryStream (checked ((int) s.Length));
			s.CopyTo (m);
			return m.ToArray ();
		}

		public static byte [] GetKeystore (string keyname = "test.keystore")
		{
			var assembly = typeof (XamarinAndroidCommonProject).Assembly;
			using (var stream = assembly.GetManifestResourceStream ($"Xamarin.ProjectTools.Resources.Base.{keyname}")) {
				var data = new byte [stream.Length];
				_ = stream.Read (data, 0, (int) stream.Length);
				return data;
			}
		}
	}
}
