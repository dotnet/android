using System;
using System.Xml.Linq;

using NUnit.Framework;

namespace WorkloadDependencies.Tests;

[TestFixture]
public class RevisionTests
{
	[Test]
	public void GetLatestRevisionAcceptsSingleComponentRevision ()
	{
		var doc = XDocument.Parse ("""
			<manifest>
			  <jdk revision="17.0.14" obsolete="False" preview="False" />
			  <jdk revision="21" obsolete="False" preview="False" />
			  <jdk revision="22.0" obsolete="False" preview="False" />
			</manifest>
			""");

		var revision = Extensions.GetLatestRevision (
			doc,
			"jdk",
			new Version (17, 0),
			new Version (22, 0));

		Assert.That (revision, Is.EqualTo ("21"));
	}
}
