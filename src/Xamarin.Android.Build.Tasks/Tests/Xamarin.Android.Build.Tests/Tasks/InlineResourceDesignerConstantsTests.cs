using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests {

	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class InlineResourceDesignerConstantsTests {

		const string DesignerResourceFullName = "_Microsoft.Android.Resource.Designer.Resource";

		// Builds:
		//   _Microsoft.Android.Resource.Designer.Resource
		//     Drawable  { static int   get_tile ()   }
		//     Styleable { static int[] get_MyView () }
		//   Consumer    { static int[] Consume () { _ = Resource.Drawable.tile; return Resource.Styleable.MyView; } }
		static MethodBody CreateConsumerBody (out ModuleDefinition module)
		{
			module = ModuleDefinition.CreateModule ("Test", ModuleKind.Dll);
			var intType = module.TypeSystem.Int32;
			var intArray = new ArrayType (intType);
			var objectType = module.TypeSystem.Object;

			var resource = new TypeDefinition ("_Microsoft.Android.Resource.Designer", "Resource",
				TypeAttributes.Public | TypeAttributes.Class, objectType);
			module.Types.Add (resource);

			var drawable = new TypeDefinition ("", "Drawable", TypeAttributes.NestedPublic | TypeAttributes.Class, objectType);
			resource.NestedTypes.Add (drawable);
			var getTile = new MethodDefinition ("get_tile", MethodAttributes.Public | MethodAttributes.Static, intType);
			var gilt = getTile.Body.GetILProcessor ();
			gilt.Emit (OpCodes.Ldc_I4_0);
			gilt.Emit (OpCodes.Ret);
			drawable.Methods.Add (getTile);

			var styleable = new TypeDefinition ("", "Styleable", TypeAttributes.NestedPublic | TypeAttributes.Class, objectType);
			resource.NestedTypes.Add (styleable);
			var getMyView = new MethodDefinition ("get_MyView", MethodAttributes.Public | MethodAttributes.Static, intArray);
			var gilm = getMyView.Body.GetILProcessor ();
			gilm.Emit (OpCodes.Ldnull);
			gilm.Emit (OpCodes.Ret);
			styleable.Methods.Add (getMyView);

			var consumer = new TypeDefinition ("Test", "Consumer", TypeAttributes.Public | TypeAttributes.Class, objectType);
			module.Types.Add (consumer);
			var consume = new MethodDefinition ("Consume", MethodAttributes.Public | MethodAttributes.Static, intArray);
			var cil = consume.Body.GetILProcessor ();
			cil.Emit (OpCodes.Call, getTile);
			cil.Emit (OpCodes.Pop);
			cil.Emit (OpCodes.Call, getMyView);
			cil.Emit (OpCodes.Ret);
			consumer.Methods.Add (consume);

			return consume.Body;
		}

		[Test]
		public void InlinesScalarAndArrayGetters ()
		{
			var body = CreateConsumerBody (out var module);
			using (module) {
				var scalar = new Dictionary<string, int> { ["Drawable::tile"] = 0x7f010000 };
				var arrays = new Dictionary<string, int []> { ["Styleable::MyView"] = new [] { 10, 20, 30 } };

				bool changed = InlineResourceDesignerConstants.RewriteMethodBody (body, DesignerResourceFullName, scalar, arrays);

				Assert.IsTrue (changed, "The body should have been rewritten.");
				Assert.IsFalse (body.Instructions.Any (i => i.OpCode == OpCodes.Call),
					"No calls to the designer getters should remain.");
				Assert.IsTrue (body.Instructions.Any (i => i.OpCode == OpCodes.Ldc_I4 && (int) i.Operand == 0x7f010000),
					"The scalar id should be inlined as a literal.");
				Assert.IsTrue (body.Instructions.Any (i => i.OpCode == OpCodes.Newarr),
					"The styleable array should be reconstructed inline.");
				// The three array values should be present as literals.
				foreach (var v in new [] { 10, 20, 30 }) {
					Assert.IsTrue (body.Instructions.Any (i => i.OpCode == OpCodes.Ldc_I4 && (int) i.Operand == v),
						$"Array value {v} should be inlined.");
				}
			}
		}

		[Test]
		public void LeavesUnknownGettersUntouched ()
		{
			var body = CreateConsumerBody (out var module);
			using (module) {
				// Empty maps: nothing matches, so nothing is rewritten.
				bool changed = InlineResourceDesignerConstants.RewriteMethodBody (body, DesignerResourceFullName,
					new Dictionary<string, int> (), new Dictionary<string, int []> ());

				Assert.IsFalse (changed, "Nothing should be rewritten when no ids match.");
				Assert.AreEqual (2, body.Instructions.Count (i => i.OpCode == OpCodes.Call),
					"Both designer getter calls should remain.");
			}
		}
	}
}
