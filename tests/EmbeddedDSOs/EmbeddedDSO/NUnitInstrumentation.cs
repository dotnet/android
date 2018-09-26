using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using Android.App;
using Android.OS;
using Android.Runtime;

using Xamarin.Android.UnitTests;
using Xamarin.Android.UnitTests.NUnit;

namespace EmbeddedDSO
{
	[Instrumentation (Name = "xamarin.android.embeddeddso_test.NUnitInstrumentation")]
	public class NUnitInstrumentation : NUnitTestInstrumentation
	{
		const string DefaultLogTag = "EmbeddedDSO";

		string logTag = DefaultLogTag;

		protected override string LogTag { 
			get { return logTag; } 
			set { logTag = value ?? DefaultLogTag; }
		}

		protected NUnitInstrumentation (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
			CommonInit ();
		}

		public NUnitInstrumentation ()
		{
			CommonInit ();
		}

		void CommonInit ()
		{
			EmbeddedDSOApp.DataDir = Context?.DataDir?.AbsolutePath;
			EmbeddedDSOApp.ApiLevel = (int)Build.VERSION.SdkInt;
		}

		protected override IList<TestAssemblyInfo> GetTestAssemblies()
		{
			IList<TestAssemblyInfo> ret = base.GetTestAssemblies();

			if (ret == null)
				ret = new List<TestAssemblyInfo> ();

			Assembly asm = GetType ().Assembly;
			ret.Add (new TestAssemblyInfo (asm, asm.Location ?? String.Empty));

			return ret;
		}
	}
}
