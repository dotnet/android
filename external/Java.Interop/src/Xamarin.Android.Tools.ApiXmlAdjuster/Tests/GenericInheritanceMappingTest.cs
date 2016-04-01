using System;
using System.Linq;
using NUnit.Framework;

namespace Xamarin.Android.Tools.ApiXmlAdjuster.Tests
{
	[TestFixture]
	public class GenericInheritanceMappingTest
	{
		JavaApi api;
		
		[TestFixtureSetUp]
		public void SetupFixture ()
		{
			api = JavaApiTestHelper.GetLoadedApi ();
			api.Resolve ();
			api.CreateGenericInheritanceMapping ();
		}
		
		[Test]
		public void GenericInheritanceMappings ()
		{
			var obj = api.FindNonGenericType ("java.lang.Object") as JavaClass;
			Assert.IsNotNull (obj.GenericInheritanceMapping, "java.lang.Object mapping not found");
			Assert.AreEqual (0, obj.GenericInheritanceMapping.Count, "ContentObservable mapping not found");
			
			var kls = api.FindNonGenericType ("android.database.ContentObservable") as JavaClass;
			var map = kls.GenericInheritanceMapping;
			Assert.IsNotNull (map, "ContentObservable mapping not found");
			Assert.AreEqual (1, map.Count, "ContentObservable mapping count unexpected");
			
			Assert.IsNotNull (map.Keys.First ().ReferencedTypeParameter, "key is not GenericTypeParameter");
			Assert.IsNotNull ("T", map.Keys.First ().ReferencedTypeParameter.Name, "key GenericTypeParameter has unexpected name");
			Assert.IsNotNull (map.Values.First ().ReferencedType, "value is not to JavaType");
			Assert.IsNotNull ("android.database.ContentObserver", map.Values.First ().ReferencedType.FullName, "value JavaType has unexpected name");
			
			var dummyType = new JavaClass (new JavaPackage (api) { Name = string.Empty }) { Name = "Dummy" };
			var tps = new JavaTypeParameters (dummyType);
			JavaTypeReference mapped;
			Assert.IsTrue (map.TryGetValue (new JavaTypeReference (new JavaTypeParameter (tps) { Name = "T" }, null), out mapped),
				"Mapped type for generic parameter 'T' not found, or dictionary lookup failed.");
			Assert.AreEqual ("android.database.ContentObserver", mapped.ReferencedType.FullName, "unexpected resolved type");
		}
		
		[Test]
		public void GenericDerivation ()
		{
			var dic = api.FindNonGenericType ("java.util.Dictionary") as JavaClass;
			Assert.IsNotNull (dic, "Dictionary not found");
			Assert.AreEqual (0, dic.GenericInheritanceMapping.Count, "Dictionary should have no mapping.");
			
			var hashtable = api.FindNonGenericType ("java.util.Hashtable") as JavaClass;
			Assert.IsNotNull (hashtable, "Hashtable not found");
			Assert.AreEqual (0, hashtable.GenericInheritanceMapping.Count, "Hashtable should have no mapping.");
			
			var dummyType = new JavaClass (new JavaPackage (api) { Name = string.Empty }) { Name = "Dummy" };
			var tps = new JavaTypeParameters (dummyType);
			var props = api.FindNonGenericType ("java.util.Properties") as JavaClass;
			Assert.IsNotNull (props, "Properties not found");
			Assert.AreEqual (2, props.GenericInheritanceMapping.Count, "Properties should have no mapping.");
			var k = new JavaTypeReference (new JavaTypeParameter (tps) { Name = "K" }, null);
			var v = new JavaTypeReference (new JavaTypeParameter (tps) { Name = "V" }, null);
			Assert.IsNotNull (props.GenericInheritanceMapping [k], "Properties: mapping for K not found.");
			Assert.AreEqual ("java.lang.Object", props.GenericInheritanceMapping [k].ReferencedType.FullName, "Properties: mapping for K is not to java.lang.Object.");
			Assert.AreEqual ("java.lang.Object", props.GenericInheritanceMapping [v].ReferencedType.FullName, "Properties: mapping for K is not to java.lang.Object.");
		}
		
		[Test]
		public void NonGenericDerivation ()
		{
			var viewGroup = api.FindNonGenericType ("android.view.ViewGroup") as JavaClass;
			Assert.IsNotNull (viewGroup, "ViewGroup not found");
			Assert.AreEqual (0, viewGroup.GenericInheritanceMapping.Count, "ViewGroup should have no mapping.");
			
			var adapterView = api.FindNonGenericType ("android.widget.AdapterView") as JavaClass;
			Assert.IsNotNull (adapterView, "AdapterView not found");
			Assert.AreEqual (0, adapterView.GenericInheritanceMapping.Count, "AdapterView should have no mapping.");
			
			var absListView = api.FindNonGenericType ("android.widget.AbsListView") as JavaClass;
			Assert.IsNotNull (absListView, "AbsListView not found");
			Assert.AreEqual (1, absListView.GenericInheritanceMapping.Count, "AbsListView should have 1 mapping.");
			
			var listView = api.FindNonGenericType ("android.widget.ListView") as JavaClass;
			Assert.IsNotNull (listView, "ListView not found");
			Assert.AreEqual (0, listView.GenericInheritanceMapping.Count, "ListView should have no mapping.");
		}
	}
}

