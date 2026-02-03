
using System;
using System.Collections.Generic;
using System.Reflection;
using Android.App;
using Android.OS;
using Android.Runtime;
using Xamarin.Android.UnitTests;
using Xamarin.Android.UnitTests.NUnit;

namespace Xamarin.Android.RuntimeTests
{
    [Instrumentation(Name = "xamarin.android.runtimetests.NUnitInstrumentation")]
    // [Register("xamarin/android/runtimetests/NUnitInstrumentation", DoNotGenerateAcw = true)]
    public class NUnitInstrumentation : NUnitTestInstrumentation
    {
        [Register ("onCreate", "(Landroid/os/Bundle;)V", "GetOnCreate_Landroid_os_Bundle_Handler")]
        public override void OnCreate (Bundle? arguments)
        {
            base.OnCreate (arguments);
        }
        const string DefaultLogTag = "NUnit";

        string logTag = DefaultLogTag;

        protected override string LogTag
        {
            get { return logTag; }
            set { logTag = value ?? DefaultLogTag; }
        }

        protected NUnitInstrumentation(IntPtr handle, JniHandleOwnership transfer)
            : base(handle, transfer)
        {
        }

        protected override IList<TestAssemblyInfo> GetTestAssemblies()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            #if !NATIVEAOT // TODO: Java.Interop-Tests not passing yet
            Assembly ji  = typeof (Java.InteropTests.JavaInterop_Tests_Reference).Assembly;
            #endif


            return new List<TestAssemblyInfo>()
            {
                new TestAssemblyInfo (asm, asm.Location ?? String.Empty),
                #if !NATIVEAOT
                new TestAssemblyInfo (ji, ji.Location ?? String.Empty),
                #endif
            };
        }
    }
}