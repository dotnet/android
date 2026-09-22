using System.IO;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class InterfaceCollectionRootingTests : BaseTest
{
	static readonly XNamespace DgmlNamespace = "http://schemas.microsoft.com/vs/2009/dgml";

	static string GraphPath => Path.Combine (
		XABuildPaths.TopDirectory, "tests", "MSBuildDeviceIntegration", "Resources", "InterfaceCollectionApp", "rooting.dgml.xml");

	[Test]
	public void CanonicalWrapperRootingGraph ()
	{
		var path = TestContext.Parameters.Get ("InterfaceCollectionRootingGraph", GraphPath);
		InterfaceCollectionTests.AssertCanonicalWrapperRooting (path);
	}

	[TestCase ("list")]
	[TestCase ("collection")]
	[TestCase ("dictionary")]
	public void DirectFactoryRooting (string wrapper)
	{
		var graph = XDocument.Load (GraphPath);
		UseDirectFactoryRoot (graph, wrapper);
		AssertGraph (graph);
	}

	[TestCase ("dictionary-factory", "conditional factory primary dependency")]
	[TestCase ("peer-metadata", "conditional factory metadata dependency")]
	public void RejectsMissingConditionalInput (string source, string message)
	{
		var graph = XDocument.Load (GraphPath);
		FindLink (graph, source, "dictionary-conditional").Remove ();
		Assert.That (() => AssertGraph (graph), Throws.TypeOf<AssertionException> ().With.Message.Contains (message));
	}

	[TestCase ("dictionary-factory", "conditional factory primary dependency")]
	[TestCase ("peer-metadata", "conditional factory metadata dependency")]
	public void RejectsWrongConditionalInput (string source, string message)
	{
		var graph = XDocument.Load (GraphPath);
		FindLink (graph, source, "dictionary-conditional").SetAttributeValue ("Source", "collection-factory");
		Assert.That (() => AssertGraph (graph), Throws.TypeOf<AssertionException> ().With.Message.Contains (message));
	}

	[TestCase (false)]
	[TestCase (true)]
	public void RejectsMissingAllocation (bool conditional)
	{
		var graph = XDocument.Load (GraphPath);
		if (!conditional) {
			UseDirectFactoryRoot (graph, "dictionary");
		}
		FindLink (graph, conditional ? "dictionary-conditional" : "dictionary-factory", "dictionary-type").Remove ();
		Assert.That (() => AssertGraph (graph), Throws.TypeOf<AssertionException> ().With.Message.Contains ("newobj dependency was not found"));
	}

	[TestCase (false)]
	[TestCase (true)]
	public void RejectsUnexpectedRoot (bool conditional)
	{
		var graph = XDocument.Load (GraphPath);
		if (!conditional) {
			UseDirectFactoryRoot (graph, "dictionary");
		}
		var unexpectedLink = new XElement (FindLink (
			graph, conditional ? "dictionary-conditional" : "dictionary-factory", "dictionary-type"));
		unexpectedLink.SetAttributeValue ("Source", "collection-factory");
		graph.Descendants (DgmlNamespace + "Links").Single ().Add (unexpectedLink);
		Assert.That (() => AssertGraph (graph), Throws.TypeOf<AssertionException> ().With.Message.Contains ("unexpected incoming dependency"));
	}

	[TestCase ("dictionary-factory")]
	[TestCase ("dictionary-conditional")]
	public void RejectsAmbiguousFactoryNodes (string nodeId)
	{
		var graph = XDocument.Load (GraphPath);
		var duplicate = new XElement (FindNode (graph, nodeId));
		duplicate.SetAttributeValue ("Id", "duplicate");
		graph.Descendants (DgmlNamespace + "Nodes").Single ().Add (duplicate);
		Assert.That (() => AssertGraph (graph), Throws.TypeOf<AssertionException> ().With.Message.Contains ("ambiguous node matches"));
	}

	static void UseDirectFactoryRoot (XDocument graph, string wrapper)
	{
		FindLink (graph, $"{wrapper}-conditional", $"{wrapper}-type").SetAttributeValue ("Source", $"{wrapper}-factory");
		FindLink (graph, $"{wrapper}-factory", $"{wrapper}-conditional").Remove ();
		FindLink (graph, "peer-metadata", $"{wrapper}-conditional").Remove ();
		FindNode (graph, $"{wrapper}-conditional").Remove ();
	}

	static XElement FindNode (XDocument graph, string id)
	{
		return graph.Descendants (DgmlNamespace + "Node").Single (node => node.Attribute ("Id")?.Value == id);
	}

	static XElement FindLink (XDocument graph, string source, string target)
	{
		return graph.Descendants (DgmlNamespace + "Link").Single (
			link => link.Attribute ("Source")?.Value == source && link.Attribute ("Target")?.Value == target);
	}

	static void AssertGraph (XDocument graph)
	{
		var path = Path.GetTempFileName ();
		try {
			graph.Save (path);
			InterfaceCollectionTests.AssertCanonicalWrapperRooting (path);
		} finally {
			File.Delete (path);
		}
	}
}
