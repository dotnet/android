using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class PrepareAbiItemsTests : BaseTest
	{
		[TestCase ("typemap", "typemaps")]
		[TestCase ("environment", "environment")]
		[TestCase ("compressed", "compressed_assemblies")]
		[TestCase ("jniremap", "jni_remap")]
		[TestCase ("marshal_methods", "marshal_methods")]
		[TestCase ("runtime_linking", "pinvoke_preserve")]
		[TestCase ("jni_init", "jni_init_funcs")]
		public void ValidMode_ProducesCorrectBaseName (string mode, string expectedBase)
		{
			var task = new PrepareAbiItems {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				BuildTargetAbis = new [] { "arm64-v8a" },
				NativeSourcesDir = "/native/src",
				Mode = mode,
				Debug = false,
			};
			Assert.IsTrue (task.Execute ());
			Assert.IsNotNull (task.AssemblySources);
			Assert.AreEqual (1, task.AssemblySources.Length);
			Assert.AreEqual ($"/native/src/{expectedBase}.arm64-v8a.ll", task.AssemblySources [0].ItemSpec);
			Assert.AreEqual ("arm64-v8a", task.AssemblySources [0].GetMetadata ("abi"));
		}

		[TestCase ("TypeMap", "typemaps")]
		[TestCase ("TYPEMAP", "typemaps")]
		[TestCase ("Environment", "environment")]
		[TestCase ("COMPRESSED", "compressed_assemblies")]
		[TestCase ("JniRemap", "jni_remap")]
		[TestCase ("Marshal_Methods", "marshal_methods")]
		[TestCase ("Runtime_Linking", "pinvoke_preserve")]
		[TestCase ("JNI_INIT", "jni_init_funcs")]
		public void ValidMode_CaseInsensitive (string mode, string expectedBase)
		{
			var task = new PrepareAbiItems {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				BuildTargetAbis = new [] { "x86_64" },
				NativeSourcesDir = "/native/src",
				Mode = mode,
				Debug = false,
			};
			Assert.IsTrue (task.Execute ());
			Assert.IsNotNull (task.AssemblySources);
			Assert.AreEqual (1, task.AssemblySources.Length);
			Assert.AreEqual ($"/native/src/{expectedBase}.x86_64.ll", task.AssemblySources [0].ItemSpec);
		}

		[Test]
		public void InvalidMode_LogsErrorAndReturnsFalse ()
		{
			var errors = new List<BuildErrorEventArgs> ();
			var task = new PrepareAbiItems {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				BuildTargetAbis = new [] { "arm64-v8a" },
				NativeSourcesDir = "/native/src",
				Mode = "invalid_mode",
				Debug = false,
			};
			Assert.IsFalse (task.Execute ());
			Assert.AreEqual (1, errors.Count);
		}

		[Test]
		public void MultipleAbis_CreatesItemForEachAbi ()
		{
			var abis = new [] { "arm64-v8a", "armeabi-v7a", "x86_64", "x86" };
			var task = new PrepareAbiItems {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				BuildTargetAbis = abis,
				NativeSourcesDir = "/native/src",
				Mode = "typemap",
				Debug = false,
			};
			Assert.IsTrue (task.Execute ());
			Assert.IsNotNull (task.AssemblySources);
			Assert.AreEqual (abis.Length, task.AssemblySources.Length);
			for (int i = 0; i < abis.Length; i++) {
				Assert.AreEqual ($"/native/src/typemaps.{abis [i]}.ll", task.AssemblySources [i].ItemSpec);
				Assert.AreEqual (abis [i], task.AssemblySources [i].GetMetadata ("abi"));
			}
		}

		[Test]
		public void SingleAbi_CreatesSingleItem ()
		{
			var task = new PrepareAbiItems {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				BuildTargetAbis = new [] { "x86" },
				NativeSourcesDir = "/output",
				Mode = "environment",
				Debug = true,
			};
			Assert.IsTrue (task.Execute ());
			Assert.IsNotNull (task.AssemblySources);
			Assert.AreEqual (1, task.AssemblySources.Length);
			Assert.AreEqual ("/output/environment.x86.ll", task.AssemblySources [0].ItemSpec);
			Assert.AreEqual ("x86", task.AssemblySources [0].GetMetadata ("abi"));
		}
	}
}
