using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

namespace AndroidMSTestProof.TestLibrary;

public abstract class LifecycleBase
{
	int initialized;
	int cleaned;

	public TestContext? TestContext { get; set; }

	protected TestContext Context => TestContext ?? throw new InvalidOperationException ("MSTest did not assign the inherited context.");

	[TestInitialize]
	public void Initialize ()
	{
		Assert.AreEqual (1, ++initialized);
		Assert.AreEqual (0, cleaned);
		using var value = new Java.Lang.String ("initialize");
		Assert.AreEqual ("initialize", value.ToString ());
		Context.WriteLine ($"[D INITIALIZE] {GetType ().FullName} {Context.TestName} calls={initialized}");
	}

	[TestCleanup]
	public void Cleanup ()
	{
		Assert.AreEqual (1, initialized);
		Assert.AreEqual (1, ++cleaned);
		using var value = new Java.Lang.String ("cleanup");
		Assert.AreEqual ("cleanup", value.ToString ());
		Context.WriteLine ($"[D CLEANUP] {GetType ().FullName} {Context.TestName} calls={cleaned} outcome={Context.CurrentTestOutcome}");
	}

	protected void AssertInitialized ()
	{
		Assert.AreEqual (1, initialized);
		Assert.AreEqual (0, cleaned);
	}
}

[TestClass]
public class LibraryTests : LifecycleBase
{
	[TestMethod]
	[TestCategory ("proof")]
	[TestCategory ("library")]
	public void ReferencedLibrary ()
	{
		AssertInitialized ();
		Assert.AreEqual (typeof (LibraryTests).Assembly, GetType ().Assembly);
		Context.WriteLine ("[D ASSEMBLY] " + typeof (LibraryTests).Assembly.GetName ().Name);
	}

	[TestMethod]
	[TestCategory ("proof")]
	[TestCategory ("library")]
	public async Task AsyncLibrary ()
	{
		AssertInitialized ();
		await Task.Delay (1);
		AssertInitialized ();
	}

	[TestMethod]
	[TestCategory ("proof")]
	[TestCategory ("library")]
	public void InconclusiveLibrary ()
	{
		AssertInitialized ();
		Assert.Inconclusive ("intentional D inconclusive; inherited cleanup must execute");
	}

	[TestMethod]
	[TestCategory ("diagnostic")]
	public void GeneratedLibraryFailure ()
	{
		AssertInitialized ();
		Assert.Fail ("intentional referenced-library generated invocation failure");
	}
}
