using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoDroid.Generation
{
    abstract class CodeGenerator
    {

        protected CodeGenerator()
        {
        }

        internal abstract void WriteClassHandle(ClassGen type, TextWriter writer, string indent, CodeGenerationOptions opt, bool requireNew);

        internal abstract void WriteClassHandle(InterfaceGen type, TextWriter writer, string indent, CodeGenerationOptions opt, string declaringType);

        internal abstract void WriteClassInvokerHandle(ClassGen type, TextWriter writer, string indent, CodeGenerationOptions opt, string declaringType);
        internal abstract void WriteInterfaceInvokerHandle(InterfaceGen type, TextWriter writer, string indent, CodeGenerationOptions opt, string declaringType);

        internal abstract void WriteConstructorIdField(Ctor ctor, TextWriter writer, string indent, CodeGenerationOptions opt);
        internal abstract void WriteConstructorBody(Ctor ctor, TextWriter writer, string indent, CodeGenerationOptions opt, StringCollection call_cleanup);

        internal abstract void WriteMethodIdField(Method method, TextWriter writer, string indent, CodeGenerationOptions opt);
        internal abstract void WriteMethodBody(Method method, TextWriter writer, string indent, CodeGenerationOptions opt);

        internal abstract void WriteFieldIdField(Field field, TextWriter writer, string indent, CodeGenerationOptions opt);
        internal abstract void WriteFieldGetBody(Field field, TextWriter writer, string indent, CodeGenerationOptions opt, GenBase type);
        internal abstract void WriteFieldSetBody(Field field, TextWriter writer, string indent, CodeGenerationOptions opt, GenBase type);

        internal virtual void WriteField(Field field, TextWriter writer, string indent, CodeGenerationOptions opt, GenBase type)
        {
            if (field.IsEnumified)
                writer.WriteLine("[global::Android.Runtime.GeneratedEnum]");
            if (field.NeedsProperty)
            {
                string fieldType = field.Symbol.IsArray ? "IList<" + field.Symbol.ElementType + ">" : opt.GetOutputName(field.Symbol.FullName);
                WriteFieldIdField(field, writer, indent, opt);
                writer.WriteLine();
                writer.WriteLine("{0}// Metadata.xml XPath field reference: path=\"{1}/field[@name='{2}']\"", indent, type.MetadataXPathReference, field.JavaName);
                writer.WriteLine("{0}[Register (\"{1}\"{2})]", indent, field.JavaName, field.AdditionalAttributeString());
                writer.WriteLine("{0}{1} {2}{3} {4} {{", indent, field.Visibility, field.IsStatic ? "static " : String.Empty, fieldType, field.Name);
                writer.WriteLine("{0}\tget {{", indent);
                WriteFieldGetBody(field, writer, indent + "\t\t", opt, type);
                writer.WriteLine("{0}\t}}", indent);

                if (!field.IsConst)
                {
                    writer.WriteLine("{0}\tset {{", indent);
                    WriteFieldSetBody(field, writer, indent + "\t\t", opt, type);
                    writer.WriteLine("{0}\t}}", indent);
                }
                writer.WriteLine("{0}}}", indent);
            }
            else
            {
                writer.WriteLine("{0}// Metadata.xml XPath field reference: path=\"{1}/field[@name='{2}']\"", indent, type.MetadataXPathReference, field.JavaName);
                writer.WriteLine("{0}[Register (\"{1}\"{2})]", indent, field.JavaName, field.AdditionalAttributeString());
                if (field.IsDeprecated)
                    writer.WriteLine("{0}[Obsolete (\"{1}\")]", indent, field.DeprecatedComment);
                if (field.Annotation != null)
                    writer.WriteLine("{0}{1}", indent, field.Annotation);

                // the Value complication is due to constant enum from negative integer value (C# compiler requires explicit parenthesis).
                writer.WriteLine("{0}{1} const {2} {3} = ({2}) {4};", indent, field.Visibility, opt.GetOutputName(field.Symbol.FullName), field.Name, field.Value.Contains('-') && field.Symbol.FullName.Contains('.') ? '(' + field.Value + ')' : field.Value);
            }
        }

        #region "if you're changing this part, also change method in https://github.com/xamarin/xamarin-android/blob/master/src/Mono.Android.Export/CallbackCode.cs"
        public virtual void WriteMethodCallback(Method method, TextWriter writer, string indent, CodeGenerationOptions opt, GenBase type, string property_name, bool as_formatted = false)
        {
            string delegate_type = method.GetDelegateType();
            writer.WriteLine("{0}static Delegate {1};", indent, method.EscapedCallbackName);
            writer.WriteLine("#pragma warning disable 0169");
            if (method.Deprecated != null)
                writer.WriteLine($"{indent}[Obsolete]");
            writer.WriteLine("{0}static Delegate {1} ()", indent, method.ConnectorName);
            writer.WriteLine("{0}{{", indent);
            writer.WriteLine("{0}\tif ({1} == null)", indent, method.EscapedCallbackName);
            writer.WriteLine("{0}\t\t{1} = JNINativeWrapper.CreateDelegate (({2}) n_{3});", indent, method.EscapedCallbackName, delegate_type, method.Name + method.IDSignature);
            writer.WriteLine("{0}\treturn {1};", indent, method.EscapedCallbackName);
            writer.WriteLine("{0}}}", indent);
            writer.WriteLine();
            if (method.Deprecated != null)
                writer.WriteLine($"{indent}[Obsolete]");
            writer.WriteLine("{0}static {1} n_{2} (IntPtr jnienv, IntPtr native__this{3})", indent, method.RetVal.NativeType, method.Name + method.IDSignature, method.Parameters.GetCallbackSignature(opt));
            writer.WriteLine("{0}{{", indent);
            writer.WriteLine("{0}\t{1} __this = global::Java.Lang.Object.GetObject<{1}> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);", indent, opt.GetOutputName(type.FullName));
            foreach (string s in method.Parameters.GetCallbackPrep(opt))
                writer.WriteLine("{0}\t{1}", indent, s);
            if (String.IsNullOrEmpty(property_name))
            {
                string call = "__this." + method.Name + (as_formatted ? "Formatted" : String.Empty) + " (" + method.Parameters.GetCall(opt) + ")";
                if (method.IsVoid)
                    writer.WriteLine("{0}\t{1};", indent, call);
                else
                    writer.WriteLine("{0}\t{1} {2};", indent, method.Parameters.HasCleanup ? method.RetVal.NativeType + " __ret =" : "return", method.RetVal.ToNative(opt, call));
            }
            else
            {
                if (method.IsVoid)
                    writer.WriteLine("{0}\t__this.{1} = {2};", indent, property_name, method.Parameters.GetCall(opt));
                else
                    writer.WriteLine("{0}\t{1} {2};", indent, method.Parameters.HasCleanup ? method.RetVal.NativeType + " __ret =" : "return", method.RetVal.ToNative(opt, "__this." + property_name));
            }
            foreach (string cleanup in method.Parameters.GetCallbackCleanup(opt))
                writer.WriteLine("{0}\t{1}", indent, cleanup);
            if (!method.IsVoid && method.Parameters.HasCleanup)
                writer.WriteLine("{0}\treturn __ret;", indent);
            writer.WriteLine("{0}}}", indent);
            writer.WriteLine("#pragma warning restore 0169");
            writer.WriteLine();
        }
        #endregion

        public void WriteMethodCustomAttributes(Method method, TextWriter writer, string indent)
        {
            if (method.GenericArguments != null && method.GenericArguments.Any())
                writer.WriteLine("{0}{1}", indent, method.GenericArguments.ToGeneratedAttributeString());
            if (method.CustomAttributes != null)
                writer.WriteLine("{0}{1}", indent, method.CustomAttributes);
            if (method.Annotation != null)
                writer.WriteLine("{0}{1}", indent, method.Annotation);
        }

        public void WriteMethodExplicitInterfaceImplementation(Method method, TextWriter writer, string indent, CodeGenerationOptions opt, GenBase iface)
        {
            //writer.WriteLine ("// explicitly implemented method from " + iface.FullName);
            WriteMethodCustomAttributes(method, writer, indent);
            writer.WriteLine("{0}{1} {2}.{3} ({4})", indent, opt.GetOutputName(method.RetVal.FullName), opt.GetOutputName(iface.FullName), method.Name, GenBase.GetSignature(method, opt));
            writer.WriteLine("{0}{{", indent);
            writer.WriteLine("{0}\treturn {1} ({2});", indent, method.Name, method.Parameters.GetCall(opt));
            writer.WriteLine("{0}}}", indent);
            writer.WriteLine();
        }

        public void WriteMethodExplicitInterfaceInvoker(Method method, TextWriter writer, string indent, CodeGenerationOptions opt, GenBase iface)
        {
            //writer.WriteLine ("\t\t// explicitly implemented invoker method from " + iface.FullName);
            WriteMethodIdField(method, writer, indent, opt);
            writer.WriteLine("{0}unsafe {1} {2}.{3} ({4})",
                    indent, opt.GetOutputName(method.RetVal.FullName), opt.GetOutputName(iface.FullName), method.Name, GenBase.GetSignature(method, opt));
            writer.WriteLine("{0}{{", indent);
            WriteMethodBody(method, writer, indent + "\t", opt);
            writer.WriteLine("{0}}}", indent);
            writer.WriteLine();
        }

        public void WriteMethodAbstractDeclaration(Method method, TextWriter writer, string indent, CodeGenerationOptions opt, InterfaceGen gen, GenBase impl)
        {
            if (method.RetVal.IsGeneric && gen != null)
            {
                WriteMethodCustomAttributes(method, writer, indent);
                writer.WriteLine("{0}{1} {2}.{3} ({4})", indent, opt.GetOutputName(method.RetVal.FullName), opt.GetOutputName(gen.FullName), method.Name, GenBase.GetSignature(method, opt));
                writer.WriteLine("{0}{{", indent);
                writer.WriteLine("{0}\tthrow new NotImplementedException ();", indent);
                writer.WriteLine("{0}}}", indent);
                writer.WriteLine();
            }
            else
            {
                bool gen_as_formatted = method.IsReturnCharSequence;
                string name = method.AdjustedName;
                WriteMethodCallback(method, writer, indent, opt, impl, null, gen_as_formatted);
                if (method.DeclaringType.IsGeneratable)
                    writer.WriteLine("{0}// Metadata.xml XPath method reference: path=\"{1}\"", indent, method.GetMetadataXPathReference(method.DeclaringType));
                writer.WriteLine("{0}[Register (\"{1}\", \"{2}\", \"{3}\"{4})]", indent, method.JavaName, method.JniSignature, method.ConnectorName, method.AdditionalAttributeString());
                WriteMethodCustomAttributes(method, writer, indent);
                writer.WriteLine("{0}{1}{2} abstract {3} {4} ({5});",
                        indent,
                        impl.RequiresNew(method.Name) ? "new " : "",
                        method.Visibility,
                        opt.GetOutputName(method.RetVal.FullName),
                        name,
                        GenBase.GetSignature(method, opt));
                writer.WriteLine();

                if (gen_as_formatted || method.Parameters.HasCharSequence)
                    WriteMethodStringOverload(method, writer, indent, opt);
            }

            WriteMethodAsyncWrapper(method, writer, indent, opt);
        }

        public void WriteMethodDeclaration(Method method, TextWriter writer, string indent, CodeGenerationOptions opt, GenBase type, string adapter)
        {
            if (method.DeclaringType.IsGeneratable)
                writer.WriteLine("{0}// Metadata.xml XPath method reference: path=\"{1}\"", indent, method.GetMetadataXPathReference(method.DeclaringType));
            if (method.Deprecated != null)
                writer.WriteLine("[Obsolete (@\"{0}\")]", method.Deprecated.Replace("\"", "\"\""));
            if (method.IsReturnEnumified)
                writer.WriteLine("{0}[return:global::Android.Runtime.GeneratedEnum]", indent);
            if (method.IsInterfaceDefaultMethod)
                writer.WriteLine("{0}[global::Java.Interop.JavaInterfaceDefaultMethod]", indent);
            writer.WriteLine("{0}[Register (\"{1}\", \"{2}\", \"{3}:{4}\"{5})]", indent, method.JavaName, method.JniSignature, method.ConnectorName, method.GetAdapterName(opt, adapter), method.AdditionalAttributeString());
            WriteMethodCustomAttributes(method, writer, indent);
            writer.WriteLine("{0}{1} {2} ({3});", indent, opt.GetOutputName(method.RetVal.FullName), method.AdjustedName, GenBase.GetSignature(method, opt));
            writer.WriteLine();
        }

        public void WriteMethodEventDelegate(Method method, TextWriter writer, string indent, CodeGenerationOptions opt)
        {
            writer.WriteLine("{0}public delegate {1} {2}EventHandler ({3});", indent, opt.GetOutputName(method.RetVal.FullName), method.Name, GenBase.GetSignature(method, opt));
            writer.WriteLine();
        }

        // This is supposed to generate instantiated generic method output, but I don't think it is done yet.
        public void WriteMethodExplicitIface(Method method, TextWriter writer, string indent, CodeGenerationOptions opt, GenericSymbol gen)
        {
            writer.WriteLine("{0}// This method is explicitly implemented as a member of an instantiated {1}", indent, gen.FullName);
            WriteMethodCustomAttributes(method, writer, indent);
            writer.WriteLine("{0}{1} {2}.{3} ({4})", indent, opt.GetOutputName(method.RetVal.FullName), opt.GetOutputName(gen.Gen.FullName), method.Name, GenBase.GetSignature(method, opt));
            writer.WriteLine("{0}{{", indent);
            Dictionary<string, string> mappings = new Dictionary<string, string>();
            for (int i = 0; i < gen.TypeParams.Length; i++)
                mappings[gen.Gen.TypeParameters[i].Name] = gen.TypeParams[i].FullName;
            WriteMethodGenericBody(method, writer, indent + "\t", opt, null, String.Empty, mappings);
            writer.WriteLine("{0}}}", indent);
            writer.WriteLine();
        }

        void WriteMethodGenericBody(Method method, TextWriter writer, string indent, CodeGenerationOptions opt, string property_name, string container_prefix, Dictionary<string, string> mappings)
        {
            if (String.IsNullOrEmpty(property_name))
            {
                string call = container_prefix + method.Name + " (" + method.Parameters.GetGenericCall(opt, mappings) + ")";
                writer.WriteLine("{0}{1}{2};", indent, method.IsVoid ? String.Empty : "return ", method.RetVal.GetGenericReturn(opt, call, mappings));
            }
            else
            {
                if (method.IsVoid) // setter
                    writer.WriteLine("{0}{1} = {2};", indent, container_prefix + property_name, method.Parameters.GetGenericCall(opt, mappings));
                else // getter
                    writer.WriteLine("{0}return {1};", indent, method.RetVal.GetGenericReturn(opt, container_prefix + property_name, mappings));
            }
        }

        public void WriteMethodIdField(Method method, TextWriter writer, string indent, CodeGenerationOptions opt, bool invoker = false)
        {
            if (invoker)
            {
                writer.WriteLine("{0}IntPtr {1};", indent, method.EscapedIdName);
                return;
            }
            WriteMethodIdField(method, writer, indent, opt);
        }

        public void WriteMethodInvoker(Method method, TextWriter writer, string indent, CodeGenerationOptions opt, GenBase type)
        {
            WriteMethodCallback(method, writer, indent, opt, type, null, method.IsReturnCharSequence);
            WriteMethodIdField(method, writer, indent, opt, invoker: true);
            writer.WriteLine("{0}public unsafe {1}{2} {3} ({4})",
                          indent, method.IsStatic ? "static " : string.Empty, opt.GetOutputName(method.RetVal.FullName), method.AdjustedName, GenBase.GetSignature(method, opt));
            writer.WriteLine("{0}{{", indent);
            WriteMethodInvokerBody(method, writer, indent + "\t", opt);
            writer.WriteLine("{0}}}", indent);
            writer.WriteLine();
        }

        public void WriteMethodInvokerBody(Method method, TextWriter writer, string indent, CodeGenerationOptions opt)
        {
            writer.WriteLine("{0}if ({1} == IntPtr.Zero)", indent, method.EscapedIdName);
            writer.WriteLine("{0}\t{1} = JNIEnv.GetMethodID (class_ref, \"{2}\", \"{3}\");", indent, method.EscapedIdName, method.JavaName, method.JniSignature);
            foreach (string prep in method.Parameters.GetCallPrep(opt))
                writer.WriteLine("{0}{1}", indent, prep);
            method.Parameters.WriteCallArgs(writer, indent, opt, invoker: true);
            string env_method = "Call" + method.RetVal.CallMethodPrefix + "Method";
            string call = "JNIEnv." + env_method + " (" +
                opt.ContextType.GetObjectHandleProperty("this") + ", " + method.EscapedIdName + method.Parameters.GetCallArgs(opt, invoker: true) + ")";
            if (method.IsVoid)
                writer.WriteLine("{0}{1};", indent, call);
            else
                writer.WriteLine("{0}{1}{2};", indent, method.Parameters.HasCleanup ? opt.GetOutputName(method.RetVal.FullName) + " __ret = " : "return ", method.RetVal.FromNative(opt, call, true));

            foreach (string cleanup in method.Parameters.GetCallCleanup(opt))
                writer.WriteLine("{0}{1}", indent, cleanup);

            if (!method.IsVoid && method.Parameters.HasCleanup)
                writer.WriteLine("{0}return __ret;", indent);
        }

        void WriteMethodStringOverloadBody(Method method, TextWriter writer, string indent, CodeGenerationOptions opt, bool haveSelf)
        {
            var call = new System.Text.StringBuilder();
            foreach (Parameter p in method.Parameters)
            {
                string pname = p.Name;
                if (p.Type == "Java.Lang.ICharSequence")
                {
                    pname = p.GetName("jls_");
                    writer.WriteLine("{0}global::Java.Lang.String {1} = {2} == null ? null : new global::Java.Lang.String ({2});", indent, pname, p.Name);
                }
                else if (p.Type == "Java.Lang.ICharSequence[]" || p.Type == "params Java.Lang.ICharSequence[]")
                {
                    pname = p.GetName("jlca_");
                    writer.WriteLine("{0}global::Java.Lang.ICharSequence[] {1} = CharSequence.ArrayFromStringArray({2});", indent, pname, p.Name);
                }
                if (call.Length > 0)
                    call.Append(", ");
                call.Append(pname);
            }
            writer.WriteLine("{0}{1}{2}{3} ({4});", indent, method.RetVal.IsVoid ? String.Empty : opt.GetOutputName(method.RetVal.FullName) + " __result = ", haveSelf ? "self." : "", method.AdjustedName, call.ToString());
            switch (method.RetVal.FullName)
            {
                case "void":
                    break;
                case "Java.Lang.ICharSequence[]":
                    writer.WriteLine("{0}var __rsval = CharSequence.ArrayToStringArray (__result);", indent);
                    break;
                case "Java.Lang.ICharSequence":
                    writer.WriteLine("{0}var __rsval = __result?.ToString ();", indent);
                    break;
                default:
                    writer.WriteLine("{0}var __rsval = __result;", indent);
                    break;
            }
            foreach (Parameter p in method.Parameters)
            {
                if (p.Type == "Java.Lang.ICharSequence")
                    writer.WriteLine("{0}{1}?.Dispose ();", indent, p.GetName("jls_"));
                else if (p.Type == "Java.Lang.ICharSequence[]")
                    writer.WriteLine("{0}if ({1} != null) foreach (global::Java.Lang.String s in {1}) s?.Dispose ();", indent, p.GetName("jlca_"));
            }
            if (!method.RetVal.IsVoid)
            {
                writer.WriteLine($"{indent}return __rsval;");
            }
        }

        void WriteMethodStringOverload(Method method, TextWriter writer, string indent, CodeGenerationOptions opt)
        {
            string static_arg = method.IsStatic ? " static" : String.Empty;
            string ret = opt.GetOutputName(method.RetVal.FullName.Replace("Java.Lang.ICharSequence", "string"));
            if (method.Deprecated != null)
                writer.WriteLine("{0}[Obsolete (@\"{1}\")]", indent, method.Deprecated.Replace("\"", "\"\"").Trim());
            writer.WriteLine("{0}{1}{2} {3} {4} ({5})", indent, method.Visibility, static_arg, ret, method.Name, GenBase.GetSignature(method, opt).Replace("Java.Lang.ICharSequence", "string").Replace("global::string", "string"));
            writer.WriteLine("{0}{{", indent);
            WriteMethodStringOverloadBody(method, writer, indent + "\t", opt, false);
            writer.WriteLine("{0}}}", indent);
            writer.WriteLine();
        }

        public void WriteMethodExtensionOverload(Method method, TextWriter writer, string indent, CodeGenerationOptions opt, string selfType)
        {
            if (!method.CanHaveStringOverload)
                return;

            string ret = opt.GetOutputName(method.RetVal.FullName.Replace("Java.Lang.ICharSequence", "string"));
            writer.WriteLine();
            writer.WriteLine("{0}public static {1} {2} (this {3} self, {4})",
                    indent, ret, method.Name, selfType,
                GenBase.GetSignature(method, opt).Replace("Java.Lang.ICharSequence", "string").Replace("global::string", "string"));
            writer.WriteLine("{0}{{", indent);
            WriteMethodStringOverloadBody(method, writer, indent + "\t", opt, true);
            writer.WriteLine("{0}}}", indent);
        }

        public void WriteMethodAsyncWrapper(Method method, TextWriter writer, string indent, CodeGenerationOptions opt)
        {
            if (!method.Asyncify)
                return;

            string static_arg = method.IsStatic ? " static" : String.Empty;
            string ret;

            if (method.IsVoid)
                ret = "global::System.Threading.Tasks.Task";
            else
                ret = "global::System.Threading.Tasks.Task<" + opt.GetOutputName(method.RetVal.FullName) + ">";

            writer.WriteLine("{0}{1}{2} {3} {4}Async ({5})", indent, method.Visibility, static_arg, ret, method.AdjustedName, GenBase.GetSignature(method, opt));
            writer.WriteLine("{0}{{", indent);
            writer.WriteLine("{0}\treturn global::System.Threading.Tasks.Task.Run (() => {1} ({2}));", indent, method.AdjustedName, method.Parameters.GetCall(opt));
            writer.WriteLine("{0}}}", indent);
            writer.WriteLine();
        }

        public void WriteMethodExtensionAsyncWrapper(Method method, TextWriter writer, string indent, CodeGenerationOptions opt, string selfType)
        {
            if (!method.Asyncify)
                return;

            string ret;

            if (method.IsVoid)
                ret = "global::System.Threading.Tasks.Task";
            else
                ret = "global::System.Threading.Tasks.Task<" + opt.GetOutputName(method.RetVal.FullName) + ">";

            writer.WriteLine("{0}public static {1} {2}Async (this {3} self{4}{5})", indent, ret, method.AdjustedName, selfType, method.Parameters.Count > 0 ? ", " : string.Empty, GenBase.GetSignature(method, opt));
            writer.WriteLine("{0}{{", indent);
            writer.WriteLine("{0}\treturn global::System.Threading.Tasks.Task.Run (() => self.{1} ({2}));", indent, method.AdjustedName, method.Parameters.GetCall(opt));
            writer.WriteLine("{0}}}", indent);
            writer.WriteLine();
        }

        public void WriteMethod(Method method, TextWriter writer, string indent, CodeGenerationOptions opt, GenBase type, bool generate_callbacks)
        {
            if (!method.IsValid)
                return;

            bool gen_as_formatted = method.IsReturnCharSequence;
            if (generate_callbacks && method.IsVirtual)
                WriteMethodCallback(method, writer, indent, opt, type, null, gen_as_formatted);

            string name_and_jnisig = method.JavaName + method.JniSignature.Replace("java/lang/CharSequence", "java/lang/String");
            bool gen_string_overload = !method.IsOverride && method.Parameters.HasCharSequence && !type.ContainsMethod(name_and_jnisig);

            string static_arg = method.IsStatic ? " static" : String.Empty;
            string virt_ov = method.IsOverride ? " override" : method.IsVirtual ? " virtual" : String.Empty;
            if ((string.IsNullOrEmpty(virt_ov) || virt_ov == " virtual") && type.RequiresNew(method.AdjustedName))
            {
                virt_ov = " new" + virt_ov;
            }
            string seal = method.IsOverride && method.IsFinal ? " sealed" : null;
            string ret = opt.GetOutputName(method.RetVal.FullName);
            WriteMethodIdField(method, writer, indent, opt);
            if (method.DeclaringType.IsGeneratable)
                writer.WriteLine("{0}// Metadata.xml XPath method reference: path=\"{1}\"", indent, method.GetMetadataXPathReference(method.DeclaringType));
            if (method.Deprecated != null)
                writer.WriteLine("{0}[Obsolete (@\"{1}\")]", indent, method.Deprecated.Replace("\"", "\"\""));
            if (method.IsReturnEnumified)
                writer.WriteLine("{0}[return:global::Android.Runtime.GeneratedEnum]", indent);
            writer.WriteLine("{0}[Register (\"{1}\", \"{2}\", \"{3}\"{4})]",
                indent, method.JavaName, method.JniSignature, method.IsVirtual ? method.ConnectorName : String.Empty, method.AdditionalAttributeString());
            WriteMethodCustomAttributes(method, writer, indent);
            writer.WriteLine("{0}{1}{2}{3}{4} unsafe {5} {6} ({7})", indent, method.Visibility, static_arg, virt_ov, seal, ret, method.AdjustedName, GenBase.GetSignature(method, opt));
            writer.WriteLine("{0}{{", indent);
            WriteMethodBody(method, writer, indent + "\t", opt);
            writer.WriteLine("{0}}}", indent);
            writer.WriteLine();

            //NOTE: Invokers are the only place false is passed for generate_callbacks, they do not need string overloads
            if (generate_callbacks && (gen_string_overload || gen_as_formatted))
                WriteMethodStringOverload(method, writer, indent, opt);

            WriteMethodAsyncWrapper(method, writer, indent, opt);
        }
    }
}
