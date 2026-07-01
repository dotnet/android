using System;
using System.Linq;
using Java.Interop.Tools.JavaTypeSystem.Models;
using NUnit.Framework;

namespace Java.Interop.Tools.JavaTypeSystem.Tests
{
	[TestFixture]
	public class GenericInheritanceMappingTests
	{
		JavaTypeCollection api;

		[OneTimeSetUp]
		public void SetupFixture ()
		{
			api = JavaApiTestHelper.GetLoadedApi ();
			api.ResolveCollection ();
		}

		[Test]
		public void GenericInheritanceMappings ()
		{
			var obj = api.FindType ("java.lang.Object") as JavaClassModel;
			Assert.IsNotNull (obj.GenericInheritanceMapping, "java.lang.Object mapping not found");
			Assert.AreEqual (0, obj.GenericInheritanceMapping.Count, "ContentObservable mapping not found");

			var kls = api.FindType ("android.database.ContentObservable") as JavaClassModel;
			var map = kls.GenericInheritanceMapping;
			Assert.IsNotNull (map, "ContentObservable mapping not found");
			Assert.AreEqual (1, map.Count, "ContentObservable mapping count unexpected");

			Assert.IsNotNull (map.Keys.First ().ReferencedTypeParameter, "key is not GenericTypeParameter");
			Assert.IsNotNull ("T", map.Keys.First ().ReferencedTypeParameter.Name, "key GenericTypeParameter has unexpected name");
			Assert.IsNotNull (map.Values.First ().ReferencedType, "value is not to JavaType");
			Assert.IsNotNull ("android.database.ContentObserver", map.Values.First ().ReferencedType.FullName, "value JavaType has unexpected name");

			var pkg = new JavaPackage ("com.example", "com/example", null);
			var dummyType = JavaApiTestHelper.CreateClass (pkg, "Dummy");
			var tps = new JavaTypeParameters (dummyType);
			var gt = new JavaTypeParameter ("T", tps);

			Assert.IsTrue (map.TryGetValue (new JavaTypeReference (gt, null), out var mapped),
				"Mapped type for generic parameter 'T' not found, or dictionary lookup failed.");

			Assert.AreEqual ("android.database.ContentObserver", mapped.ReferencedType.FullName, "unexpected resolved type");
		}

		[Test]
		public void GenericDerivation ()
		{
			var dic = api.FindType ("java.util.Dictionary") as JavaClassModel;
			Assert.IsNotNull (dic, "Dictionary not found");
			Assert.AreEqual (0, dic.GenericInheritanceMapping.Count, "Dictionary should have no mapping.");

			var hashtable = api.FindType ("java.util.Hashtable") as JavaClassModel;
			Assert.IsNotNull (hashtable, "Hashtable not found");
			Assert.AreEqual (0, hashtable.GenericInheritanceMapping.Count, "Hashtable should have no mapping.");

			var pkg = new JavaPackage ("com.example", "com/example", null);
			var dummyType = JavaApiTestHelper.CreateClass (pkg, "Dummy");
			var tps = new JavaTypeParameters (dummyType);

			var props = api.FindType ("java.util.Properties") as JavaClassModel;
			Assert.IsNotNull (props, "Properties not found");
			Assert.AreEqual (2, props.GenericInheritanceMapping.Count, "Properties should have no mapping.");

			var k = new JavaTypeReference (new JavaTypeParameter ("K", tps), null);
			var v = new JavaTypeReference (new JavaTypeParameter ("V", tps), null);

			Assert.IsNotNull (props.GenericInheritanceMapping [k], "Properties: mapping for K not found.");
			Assert.AreEqual ("java.lang.Object", props.GenericInheritanceMapping [k].ReferencedType.FullName, "Properties: mapping for K is not to java.lang.Object.");
			Assert.AreEqual ("java.lang.Object", props.GenericInheritanceMapping [v].ReferencedType.FullName, "Properties: mapping for K is not to java.lang.Object.");
		}

		[Test]
		public void NonGenericDerivation ()
		{
			var viewGroup = api.FindType ("android.view.ViewGroup") as JavaClassModel;
			Assert.IsNotNull (viewGroup, "ViewGroup not found");
			Assert.AreEqual (0, viewGroup.GenericInheritanceMapping.Count, "ViewGroup should have no mapping.");
			
			var adapterView = api.FindType ("android.widget.AdapterView") as JavaClassModel;
			Assert.IsNotNull (adapterView, "AdapterView not found");
			Assert.AreEqual (0, adapterView.GenericInheritanceMapping.Count, "AdapterView should have no mapping.");
			
			var absListView = api.FindType ("android.widget.AbsListView") as JavaClassModel;
			Assert.IsNotNull (absListView, "AbsListView not found");
			Assert.AreEqual (1, absListView.GenericInheritanceMapping.Count, "AbsListView should have 1 mapping.");
			
			var listView = api.FindType ("android.widget.ListView") as JavaClassModel;
			Assert.IsNotNull (listView, "ListView not found");
			Assert.AreEqual (0, listView.GenericInheritanceMapping.Count, "ListView should have no mapping.");
		}
	}
}
