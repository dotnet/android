#nullable enable
using System.IO;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class SampleRepositoryTests : BaseTest
{
	[Test]
	public void CreatesMissingRepository ()
	{
		var directory = Path.Combine (Root, "temp", TestName);
		var archivePath = Path.Combine (directory, "samples.zip");
		Directory.CreateDirectory (directory);

		try {
			const string source = "Console.WriteLine (\"Hello\");";
			var repository = new global::SampleRepository (archivePath);
			string id;
			try {
				id = repository.RegisterSample (source, new global::SampleDesc {
					Language = "C#",
					FullTypeName = "Example.Widget",
					DocumentationFilePath = "Example.Widget.xml",
				});
			} finally {
				repository.Close (removeOldEntries: false);
			}

			FileAssert.Exists (archivePath);

			var reopened = global::SampleRepository.LoadFrom (archivePath);
			try {
				Assert.AreEqual (source, reopened.GetSampleFromID (id, out var description));
				Assert.AreEqual ("C#", description.Language);
				Assert.AreEqual ("Example.Widget", description.FullTypeName);
				Assert.AreEqual ("Example.Widget.xml", description.DocumentationFilePath);
			} finally {
				reopened.Close (removeOldEntries: false);
			}
		} finally {
			Directory.Delete (directory, recursive: true);
		}
	}
}
