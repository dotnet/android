using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = CSharpCodeFixVerifier <CustomApplicationAnalyzer, CustomApplicationCodeFixProvider>;

[TestClass]
public class DNAA0001Tests
{
	[TestMethod]
	[DataRow ("IntPtr", "JniHandleOwnership")]
	[DataRow ("nint", "Android.Runtime.JniHandleOwnership")]
	[DataRow ("global::System.IntPtr", "global::Android.Runtime.JniHandleOwnership")]
	[DataRow ("System.IntPtr", "AR.JniHandleOwnership")]
	public async Task DNAA0001DoesNotShowForExistingCode (string handle, string type)
	{
		var test = $@"
using System;
using Android.App;
using Android.Runtime;
using AR = Android.Runtime;
namespace ConsoleApplication1
{{
	public class Foo : Application
	{{
		public Foo({handle} javaReference, {type} transfer) : base(javaReference, transfer)
		{{
		}}
	}}
}}
namespace Android.Runtime {{
	public enum JniHandleOwnership {{
		None,
	}};
}}
namespace Android.App {{
	using Android.Runtime;
	public class Application {{
		public Application () {{}}
		protected Application (IntPtr handle, JniHandleOwnership transfer) {{
		}}
	}}
}}
";
		await VerifyCS.VerifyAnalyzerAsync (test);
	}
	[TestMethod]
	[DataRow ("IntPtr", "JniHandleOwnership")]
	[DataRow ("nint", "Android.Runtime.JniHandleOwnership")]
	[DataRow ("global::System.IntPtr", "global::Android.Runtime.JniHandleOwnership")]
	[DataRow ("System.IntPtr", "AR.JniHandleOwnership")]
	public async Task DNAA0001DoesNotShowForPrimaryConstructor (string handle, string type)
	{
		var test = $@"
using System;
using Android.App;
using Android.Runtime;
using AR = Android.Runtime;
namespace ConsoleApplication1
{{
	public class Foo({handle} javaReference, {type} transfer) : Application(javaReference, transfer)
	{{
	}}
}}
namespace Android.Runtime {{
	public enum JniHandleOwnership {{
		None,
	}};
}}
namespace Android.App {{
	using Android.Runtime;
	public class Application {{
		public Application () {{}}
		protected Application (IntPtr handle, JniHandleOwnership transfer) {{
		}}
	}}
}}
";
		await VerifyCS.VerifyAnalyzerAsync (test);
	}

	[TestMethod]
	public async Task DNAA0001DoesNotShowForOriginalIssueExample ()
	{
		var test = @"
using System;
using Android.App;
using Android.Runtime;

namespace ConsoleApplication1
{
	public class MainApplication(IntPtr handle, JniHandleOwnership ownership) : Application(handle, ownership)
	{
	}
}
namespace Android.Runtime {
	public enum JniHandleOwnership {
		None,
	};
}
namespace Android.App {
	using Android.Runtime;
	public class Application {
		public Application () {}
		protected Application (IntPtr handle, JniHandleOwnership transfer)
		{
		}
	}
}
";
		await VerifyCS.VerifyAnalyzerAsync (test);
	}

	[TestMethod]
	public async Task DNAA0001IsShownForInvalidPrimaryConstructor ()
	{
		var brokenCode = @"
using System;
using Android.App;
using Android.Runtime;

namespace ConsoleApplication1
{
	public class Foo(string name) : Application()
	{   
	}
}
namespace Android.Runtime {
	public enum JniHandleOwnership {
		None,
	};
}
namespace Android.App {
	using Android.Runtime;
	public class Application {
		public Application () {}
		protected Application (IntPtr handle, JniHandleOwnership transfer)
		{
		}
	}
}
";
		var expected = VerifyCS.Diagnostic ().WithSpan (8, 15, 8, 18).WithArguments ("Foo");
		await VerifyCS.VerifyAnalyzerAsync (brokenCode, expected);
	}

	[TestMethod]
	public async Task DNAA0001IsShownForEmptyPrimaryConstructor ()
	{
		var brokenCode = @"
using System;
using Android.App;
using Android.Runtime;

namespace ConsoleApplication1
{
	public class Foo() : Application()
	{   
	}
}
namespace Android.Runtime {
	public enum JniHandleOwnership {
		None,
	};
}
namespace Android.App {
	using Android.Runtime;
	public class Application {
		public Application () {}
		protected Application (IntPtr handle, JniHandleOwnership transfer)
		{
		}
	}
}
";
		var expected = VerifyCS.Diagnostic ().WithSpan (8, 15, 8, 18).WithArguments ("Foo");
		await VerifyCS.VerifyAnalyzerAsync (brokenCode, expected);
	}

	[TestMethod]
	public async Task DNAA0001IsShownWhenUsingFullyQualifiedType ()
	{
		var brokenCode = @"
using System;
using System.Diagnostics;
using Android.App;

namespace ConsoleApplication1
{
	public class Foo : Application
	{   
	}
}
namespace Android.Runtime {
	public enum JniHandleOwnership {
		None,
	};
}
namespace Android.App {
	public class Application {
		public Application () {}
		protected Application (IntPtr handle, Android.Runtime.JniHandleOwnership transfer)
		{
		}
	}
}
";
		var expected = VerifyCS.Diagnostic ().WithSpan (8, 15, 8, 18).WithArguments ("Foo");
		await VerifyCS.VerifyAnalyzerAsync (brokenCode, expected);
	}

	[TestMethod]
	public async Task DNAA0001IsShownWhen ()
	{
		var brokenCode = @"
using System;
using System.Diagnostics;
using Android.App;

namespace ConsoleApplication1
{
	public class Foo : Application
	{   
	}
}
namespace Android.Runtime {
	public enum JniHandleOwnership {
		None,
	};
}
namespace Android.App {
	using Android.Runtime;
	public class Application {
		public Application () {}
		protected Application (IntPtr handle, JniHandleOwnership transfer)
		{
		}
	}
}
";
		var expected = VerifyCS.Diagnostic ().WithSpan (8, 15, 8, 18).WithArguments ("Foo");
		await VerifyCS.VerifyAnalyzerAsync (brokenCode, expected);
	}

	[TestMethod]
	public async Task DNAA0001IsFixed()
	{
		var brokenCode = @"
using System;
using Android.App;
namespace ConsoleApplication1
{
	public class Foo : Application
	{
	}
}
namespace Android.Runtime {
	public enum JniHandleOwnership {
		None,
	};
}
namespace Android.App {
	using Android.Runtime;
	public class Application {
		public Application () {}
		protected Application (IntPtr handle, JniHandleOwnership transfer)
		{
		}
	}
}
";

		// DO NOT Change the format of the code below or the test will fail.
		// The generator does not respect existing code formatting.
		var expectedFixedCode = @"
using System;
using Android.App;
namespace ConsoleApplication1
{
	public class Foo : Application
	{
        public Foo(IntPtr javaReference, Android.Runtime.JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }
    }
}
namespace Android.Runtime {
	public enum JniHandleOwnership {
		None,
	};
}
namespace Android.App {
	using Android.Runtime;
	public class Application {
		public Application () {}
		protected Application (IntPtr handle, JniHandleOwnership transfer)
		{
		}
	}
}
";

		var expected = VerifyCS.Diagnostic ("DNAA0001").WithSpan (6, 15, 6, 18).WithArguments ("Foo");
		await VerifyCS.VerifyCodeFixAsync (brokenCode, expected, expectedFixedCode);
	}
}
