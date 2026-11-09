using System.Reflection;
using AndroidMSTestProof.TestLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

namespace AndroidMSTestProof;

[TestClass]
public class ProofTests : LifecycleBase
{
	[TestMethod]
	[TestCategory ("proof")]
	public void AndroidBinding ()
	{
		AssertInitialized ();
		Assert.IsTrue (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.N);
		Assert.AreEqual ("Mono.Android", typeof (Java.Lang.String).Assembly.GetName ().Name);
	}

	[TestMethod]
	[TestCategory ("proof")]
	public async Task AsyncTask ()
	{
		AssertInitialized ();
		await Task.Delay (1);
		AssertInitialized ();
		using var value = new Java.Lang.String ("async JNI");
		Assert.AreEqual ("async JNI", value.ToString ());
	}

	[TestMethod]
	[TestCategory ("proof")]
	public async ValueTask AsyncValueTask ()
	{
		AssertInitialized ();
		await Task.Delay (1);
		AssertInitialized ();
	}

#pragma warning disable MSTEST0042, MSTEST0087 // Identical arguments and display names deliberately test row identity.
	[TestMethod]
	[TestCategory ("proof")]
	[DataRow (0, DisplayName = "same data display")]
	[DataRow (0, DisplayName = "same data display")]
	[DataRow (1, DisplayName = "same data display")]
	public void DataRows (int row)
	{
		AssertInitialized ();
		Assert.IsTrue (row is 0 or 1);
		Context.WriteLine ($"[D ROW] DataRows value={row}");
	}
#pragma warning restore MSTEST0042, MSTEST0087

	public static IEnumerable<object []> DynamicCases {
		get {
			yield return [0];
			yield return [0];
			yield return [1];
		}
	}

	public static string DynamicDisplay (MethodInfo method, object [] data) => "same dynamic display";

	[TestMethod]
	[TestCategory ("proof")]
	[DynamicData (nameof (DynamicCases), DynamicDataDisplayName = nameof (DynamicDisplay))]
	public async Task DynamicRows (int row)
	{
		AssertInitialized ();
		await Task.Delay (1);
		Assert.IsTrue (row is 0 or 1);
		Context.WriteLine ($"[D ROW] DynamicRows value={row}");
	}

	[TestMethod]
	[TestCategory ("proof")]
	public void Attachment ()
	{
		AssertInitialized ();
		var directory = Android.App.Application.Context.GetExternalFilesDir (null)?.AbsolutePath
			?? throw new InvalidOperationException ("No Android external results directory.");
		var path = Path.Combine (directory, "TestResults", "d-proof-attachment.txt");
		File.WriteAllText (path, "D attachment\nline two");
		Context.AddResultFile (path);
		Context.WriteLine ("[D ATTACHMENT] " + path);
	}

	[TestMethod]
	[TestCategory ("proof")]
	[Ignore ("intentional D ignored test; no initialization or cleanup expected")]
	public void Ignored ()
	{
		Assert.Fail ("an ignored test must never execute");
	}

	[TestMethod]
	[TestCategory ("diagnostic")]
	public void GeneratedFailure ()
	{
		AssertInitialized ();
#pragma warning disable MSTEST0025 // Preserve an actual comparison failure and its generated invocation stack.
		Assert.AreEqual ("expected D value", "actual D value", "intentional diagnostic failure\nsecond line");
#pragma warning restore MSTEST0025
	}
}
