using System;
using System.Collections.Generic;
using System.Linq;
using MonoDroid.Generation;

namespace generator.SourceWriters
{
	/// <summary>
	/// Allocates compact, deterministic CLR names for the generated callback infrastructure of a
	/// single emitted type when the experimental <c>[UnmanagedCallersOnly]</c> callback format is
	/// enabled.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The legacy format names a callback <c>n_{ManagedName}{IDSignature}</c>, where
	/// <c>IDSignature</c> is the escaped JNI signature of the parameter list.  That is unique by
	/// construction, but it puts a full Java signature into the <c>#Strings</c> heap for every one
	/// of the ~30,000 callbacks, twice (once for the callback and once for its
	/// <c>__n_*</c> marshaling target).
	/// </para>
	/// <para>
	/// This allocator instead names the callback <c>n_{ManagedName}</c>, appending <c>_1</c>,
	/// <c>_2</c>, … only when several callbacks in the same emitted type share a managed name.
	/// The method-specific function pointer target becomes an opaque per-type ordinal
	/// <c>m0</c>, <c>m1</c>, … since it is private, referenced only by <c>&amp;m{N}</c> from the
	/// callback in the very same type, and never named in metadata that another assembly reads.
	/// </para>
	/// <para>
	/// Allocation is a pure function of the owning type's own API definition.  It never depends on
	/// the order in which source writers happen to be constructed, because a
	/// <c>[Register]</c> connector emitted by one writer must agree with the callback name emitted
	/// by a different writer — sometimes in a different assembly, for interface invokers and
	/// default interface methods whose connector carries an explicit owner qualifier.
	/// </para>
	/// </remarks>
	public sealed class CallbackNameAllocator
	{
		/// <summary>
		/// Names allocated for one owning type.  Keyed by <see cref="GetMethodKey" />, which is the
		/// legacy unique callback discriminator, so that two <see cref="Method" /> instances
		/// describing the same Java member — clones produced for inherited or interface members —
		/// resolve to the same name.
		/// </summary>
		sealed class TypeAllocation
		{
			public Dictionary<string, string> CallbackNames { get; } = new Dictionary<string, string> (StringComparer.Ordinal);

			public Dictionary<string, string> TargetNames { get; } = new Dictionary<string, string> (StringComparer.Ordinal);

			public HashSet<string> UsedCallbackNames { get; } = new HashSet<string> (StringComparer.Ordinal);
		}

		readonly Dictionary<GenBase, TypeAllocation> allocations = new Dictionary<GenBase, TypeAllocation> ();

		/// <summary>
		/// The unique-within-a-type discriminator for a callback: exactly the suffix the legacy
		/// format appends to <c>n_</c>.
		/// </summary>
		public static string GetMethodKey (Method method) => method.Name + method.IDSignature;

		/// <summary>
		/// The compact <c>n_*</c> callback name for <paramref name="method" /> as declared by
		/// <paramref name="owner" />.
		/// </summary>
		public string GetCallbackName (GenBase owner, Method method)
		{
			var allocation = GetAllocation (owner);
			var key = GetMethodKey (method);

			if (allocation.CallbackNames.TryGetValue (key, out var name))
				return name;

			return AddFallback (allocation, key).callback;
		}

		/// <summary>
		/// The opaque <c>m{N}</c> name of the method-specific function pointer target for
		/// <paramref name="method" /> as declared by <paramref name="owner" />.
		/// </summary>
		public string GetTargetName (GenBase owner, Method method)
		{
			var allocation = GetAllocation (owner);
			var key = GetMethodKey (method);

			if (allocation.TargetNames.TryGetValue (key, out var name))
				return name;

			return AddFallback (allocation, key).target;
		}

		/// <summary>
		/// A member which is not part of the owning type's own API definition — the generator can
		/// emit a callback into a class for an interface member it implements — falls back to the
		/// legacy names.  They are unique by construction and, unlike an allocation counter, do not
		/// depend on the order in which the name happened to be requested.
		/// </summary>
		static (string callback, string target) AddFallback (TypeAllocation allocation, string key)
		{
			var callback = Disambiguate (allocation, "n_" + key);
			allocation.CallbackNames [key] = callback;

			var target = "__" + callback;
			allocation.TargetNames [key] = target;

			return (callback, target);
		}

		TypeAllocation GetAllocation (GenBase owner)
		{
			if (allocations.TryGetValue (owner, out var allocation))
				return allocation;

			allocation = Build (owner);
			allocations.Add (owner, allocation);
			return allocation;
		}

		static TypeAllocation Build (GenBase owner)
		{
			var allocation = new TypeAllocation ();

			// Group by managed name so that the disambiguating index of a member depends only on
			// the other members which share its name, and order within a group by the member's own
			// signature.  Neither depends on the position of unrelated members in the type, which
			// keeps the allocation stable when a type is re-read from a reference API description
			// in another assembly's generator run.
			var groups = GetCallbackCandidates (owner)
				.GroupBy (m => m.Name, StringComparer.Ordinal)
				.OrderBy (g => g.Key, StringComparer.Ordinal);

			var ordinal = 0;

			foreach (var group in groups) {
				var index = 0;

				foreach (var key in group.Select (GetMethodKey).Distinct (StringComparer.Ordinal).OrderBy (k => k, StringComparer.Ordinal)) {
					if (allocation.CallbackNames.ContainsKey (key))
						continue;

					var candidate = index == 0 ? "n_" + group.Key : $"n_{group.Key}_{index}";

					allocation.CallbackNames [key] = Disambiguate (allocation, candidate);
					allocation.TargetNames [key] = "m" + ordinal.ToString (System.Globalization.CultureInfo.InvariantCulture);

					index++;
					ordinal++;
				}
			}

			return allocation;
		}

		static string Disambiguate (TypeAllocation allocation, string candidate)
		{
			var name = candidate;
			var suffix = 0;

			while (!allocation.UsedCallbackNames.Add (name))
				name = candidate + "_" + (++suffix).ToString (System.Globalization.CultureInfo.InvariantCulture);

			return name;
		}

		/// <summary>
		/// Every member of <paramref name="owner" /> which the generator may emit a callback for.
		/// </summary>
		/// <remarks>
		/// The inherited interfaces are included because an emitted type declares their members
		/// too: a class implements the abstract members of the interfaces it implements, and an
		/// interface invoker implements the members of the interfaces its interface derives from.
		/// Including them is what makes an allocation collision-free for the type it is emitted
		/// into.  Note that only the copy in the interface's *own* invoker is ever named by a
		/// <c>[Register]</c> connector, so the duplicate copies in derived invokers are free to be
		/// numbered differently.
		/// </remarks>
		static IEnumerable<Method> GetCallbackCandidates (GenBase owner)
		{
			foreach (var m in GetDeclaredMethods (owner))
				yield return m;

			var inherited = owner switch {
				ClassGen klass => klass.GetAllDerivedInterfaces (),
				InterfaceGen iface => iface.GetAllDerivedInterfaces (),
				_ => null,
			};

			if (inherited is null)
				yield break;

			foreach (var iface in inherited) {
				foreach (var m in GetDeclaredMethods (iface))
					yield return m;
			}
		}

		static IEnumerable<Method> GetDeclaredMethods (GenBase type)
		{
			foreach (var m in type.Methods)
				yield return m;

			foreach (var p in type.Properties) {
				if (p.Getter != null)
					yield return p.Getter;
				if (p.Setter != null)
					yield return p.Setter;
			}
		}
	}
}
