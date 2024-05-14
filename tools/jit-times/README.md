**jit-times** is a tool to process methods.txt file produced by .NET for Android
applications

	Usage: jit-times.exe OPTIONS* <methods-file>

	Processes JIT methods file from XA app with debug.mono.log=timing enabled

	Copyright 2019 Microsoft Corporation

	Options:
	  -h, --help, -?             Show this message and exit
	  -m, --method=TYPE-REGEX    Process only methods whose names match TYPE-REGEX.
	  -s                         Sort by self times. (this is default ordering)
	  -t                         Sort by total times.
	  -u                         Show unsorted results.
	  -v, --verbose              Output information about progress during the run
	                               of the tool

### Getting the `methods.txt` file

The `methods.txt` file can be acquired from .NET for Android application like this:

 1. Set the `debug.mono.log` system property to include `timing`:

        adb shell setprop debug.mono.log timing`

 2. Run the application

 3. Grab `methods.txt`:

        adb shell run-as @PACKAGE_NAME@ cat files/.__override__/methods.txt > methods.txt

### Example usage:

To display JIT times for `System.Reflection.Emit` methods

	mono jit-times.exe -m ^System.Reflection.Emit methods.txt

Results in:

```
Total (ms) |  Self (ms) | Method
      8.35 |       8.35 | System.Reflection.Emit.OpCodes:.cctor ()
      0.57 |       0.57 | System.Reflection.Emit.ILGenerator:Emit (System.Reflection.Emit.OpCode,System.Reflection.Emit.LocalBuilder)
      0.49 |       0.49 | System.Reflection.Emit.ILGenerator:Emit (System.Reflection.Emit.OpCode,System.Reflection.MethodInfo)
      0.39 |       0.39 | System.Reflection.Emit.DynamicMethod:.ctor (string,System.Reflection.MethodAttributes,System.Reflection.CallingConventions,System.Type,System.Type[],System.Type,System.Reflection.Module,bool,bool)
      0.36 |       0.36 | System.Reflection.Emit.ILGenerator:Emit (System.Reflection.Emit.OpCode,System.Reflection.Emit.Label)
      0.36 |       0.36 | System.Reflection.Emit.ILGenerator:DeclareLocal (System.Type,bool)
      0.35 |       0.35 | System.Reflection.Emit.ILGenerator:BeginExceptionBlock ()
      0.34 |       0.34 | System.Reflection.Emit.ILGenerator:Emit (System.Reflection.Emit.OpCode,System.Type)
      0.31 |       0.31 | System.Reflection.Emit.DynamicMethod:CreateDynMethod ()
      0.29 |       0.29 | System.Reflection.Emit.ILGenerator:DefineLabel ()
      0.27 |       0.27 | System.Reflection.Emit.ILGenerator:BeginCatchBlock (System.Type)
      ...
Sum of self time (ms): 16.02
```
