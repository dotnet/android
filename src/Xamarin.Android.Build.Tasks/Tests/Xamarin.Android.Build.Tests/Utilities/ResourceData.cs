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

		public  static  byte[]  JavaSourceJarTestJar            => javaSourceJarTestJar.Value;
		public  static  byte[]  JavaSourceJarTestSourcesJar     => javaSourceJarTestSourcesJar.Value;
		public  static  byte[]  JavaSourceJarTestJavadocJar     => javaSourceJarTestJavadocJar.Value;

		static byte[] GetResourceData (string name)
		{
			using (var s = typeof (InlineData).Assembly.GetManifestResourceStream (name))
			using (var m = new MemoryStream (checked ((int) s.Length))) {
				s.CopyTo (m);
				return m.ToArray ();
			}
		}

		public static byte [] GetKeystore (string keyname = "test.keystore")
		{
			var assembly = typeof (XamarinAndroidCommonProject).Assembly;
			using (var stream = assembly.GetManifestResourceStream ($"Xamarin.ProjectTools.Resources.Base.{keyname}")) {
				var data = new byte [stream.Length];
				stream.Read (data, 0, (int) stream.Length);
				return data;
			}
		}
	}
}
