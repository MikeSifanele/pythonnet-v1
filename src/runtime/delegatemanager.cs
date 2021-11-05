using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Python.Runtime
{
    /// <summary>
    /// The DelegateManager class manages the creation of true managed
    /// delegate instances that dispatch calls to Python methods.
    /// </summary>
    internal class DelegateManager
    {
        private readonly Dictionary<Type,Type> cache = new Dictionary<Type, Type>();
        private readonly Type basetype = typeof(Dispatcher);
        private readonly Type arrayType = typeof(object[]);
        private readonly Type voidtype = typeof(void);
        private readonly Type typetype = typeof(Type);
        private readonly Type ptrtype = typeof(IntPtr);
        private readonly CodeGenerator codeGenerator = new CodeGenerator();
        private readonly ConstructorInfo arrayCtor;
        private readonly MethodInfo dispatch;

        public DelegateManager()
        {
            arrayCtor = arrayType.GetConstructor(new[] { typeof(int) });
            dispatch = basetype.GetMethod("Dispatch");
        }

        /// <summary>
        /// Given a true delegate instance, return the PyObject handle of the
        /// Python object implementing the delegate (or IntPtr.Zero if the
        /// delegate is not implemented in Python code.
        /// </summary>
        public IntPtr GetPythonHandle(Delegate d)
        {
            if (d?.Target is Dispatcher)
            {
                var disp = (Dispatcher)d.Target;
                return disp.target;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// GetDispatcher is responsible for creating a class that provides
        /// an appropriate managed callback method for a given delegate type.
        /// </summary>
        private Type GetDispatcher(Type dtype)
        {
            // If a dispatcher type for the given delegate type has already
            // been generated, get it from the cache. The cache maps delegate
            // types to generated dispatcher types. A possible optimization
            // for the future would be to generate dispatcher types based on
            // unique signatures rather than delegate types, since multiple
            // delegate types with the same sig could use the same dispatcher.

            if (cache.TryGetValue(dtype, out Type item))
            {
                return item;
            }

            string name = $"__{dtype.FullName}Dispatcher";
            name = name.Replace('.', '_');
            name = name.Replace('+', '_');
            TypeBuilder tb = codeGenerator.DefineType(name, basetype);

            // Generate a constructor for the generated type that calls the
            // appropriate constructor of the Dispatcher base type.
            MethodAttributes ma = MethodAttributes.Public |
                                  MethodAttributes.HideBySig |
                                  MethodAttributes.SpecialName |
                                  MethodAttributes.RTSpecialName;
            var cc = CallingConventions.Standard;
            Type[] args = { ptrtype, typetype };
            ConstructorBuilder cb = tb.DefineConstructor(ma, cc, args);
            ConstructorInfo ci = basetype.GetConstructor(args);
            ILGenerator il = cb.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, ci);
            il.Emit(OpCodes.Ret);

            // Method generation: we generate a method named "Invoke" on the
            // dispatcher type, whose signature matches the delegate type for
            // which it is generated. The method body simply packages the
            // arguments and hands them to the Dispatch() method, which deals
            // with converting the arguments, calling the Python method and
            // converting the result of the call.
            MethodInfo method = dtype.GetMethod("Invoke");
            ParameterInfo[] pi = method.GetParameters();

            var signature = new Type[pi.Length];
            for (var i = 0; i < pi.Length; i++)
            {
                signature[i] = pi[i].ParameterType;
            }

            MethodBuilder mb = tb.DefineMethod("Invoke", MethodAttributes.Public, method.ReturnType, signature);

            il = mb.GetILGenerator();
            // loc_0 = new object[pi.Length]
            il.DeclareLocal(arrayType);
            il.Emit(OpCodes.Ldc_I4, pi.Length);
            il.Emit(OpCodes.Newobj, arrayCtor);
            il.Emit(OpCodes.Stloc_0);

            bool anyByRef = false;

            for (var c = 0; c < signature.Length; c++)
            {
                Type t = signature[c];
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Ldarg_S, (byte)(c + 1));

                if (t.IsByRef)
                {
                    // The argument is a pointer.  We must dereference the pointer to get the value or object it points to.
                    t = t.GetElementType();
                    if (t.IsValueType)
                    {
                        il.Emit(OpCodes.Ldobj, t);
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldind_Ref);
                    }
                    anyByRef = true;
                }

                if (t.IsValueType)
                {
                    il.Emit(OpCodes.Box, t);
                }

                // args[c] = arg
                il.Emit(OpCodes.Stelem_Ref);
            }

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Call, dispatch);

            if (anyByRef)
            {
                // Dispatch() will have modified elements of the args list that correspond to out parameters.
                for (var c = 0; c < signature.Length; c++)
                {
                    Type t = signature[c];
                    if (t.IsByRef)
                    {
                        t = t.GetElementType();
                        // *arg = args[c]
                        il.Emit(OpCodes.Ldarg_S, (byte)(c + 1));
                        il.Emit(OpCodes.Ldloc_0);
                        il.Emit(OpCodes.Ldc_I4, c);
                        il.Emit(OpCodes.Ldelem_Ref);
                        if (t.IsValueType)
                        {
                            il.Emit(OpCodes.Unbox_Any, t);
                            il.Emit(OpCodes.Stobj, t);
                        }
                        else
                        {
                            il.Emit(OpCodes.Stind_Ref);
                        }
                    }
                }
            }

            if (method.ReturnType == voidtype)
            {
                il.Emit(OpCodes.Pop);
            }
            else if (method.ReturnType.IsValueType)
            {
                il.Emit(OpCodes.Unbox_Any, method.ReturnType);
            }

            il.Emit(OpCodes.Ret);

            Type disp = tb.CreateType();
            cache[dtype] = disp;
            return disp;
        }

        /// <summary>
        /// Given a delegate type and a callable Python object, GetDelegate
        /// returns an instance of the delegate type. The delegate instance
        /// returned will dispatch calls to the given Python object.
        /// </summary>
        internal Delegate GetDelegate(Type dtype, IntPtr callable)
        {
            Type dispatcher = GetDispatcher(dtype);
            object[] args = { callable, dtype };
            object o = Activator.CreateInstance(dispatcher, args);
            return Delegate.CreateDelegate(dtype, o, "Invoke");
        }
    }


    /* When a delegate instance is created that has a Python implementation,
       the delegate manager generates a custom subclass of Dispatcher and
       instantiates it, passing the IntPtr of the Python callable.

       The "real" delegate is created using CreateDelegate, passing the
       instance of the generated type and the name of the (generated)
       implementing method (Invoke).

       The true delegate instance holds the only reference to the dispatcher
       instance, which ensures that when the delegate dies, the finalizer
       of the referenced instance will be able to decref the Python
       callable.

       A possible alternate strategy would be to create custom subclasses
       of the required delegate type, storing the IntPtr in it directly.
       This would be slightly cleaner, but I'm not sure if delegates are
       too "special" for this to work. It would be more work, so for now
       the 80/20 rule applies :) */

    public class Dispatcher
    {
        public IntPtr target;
        public Type dtype;
        private bool _disposed = false;
        private bool _finalized = false;

        public Dispatcher(IntPtr target, Type dtype)
        {
            Runtime.XIncref(target);
            this.target = target;
            this.dtype = dtype;
        }

        ~Dispatcher()
        {
            if (_finalized || _disposed)
            {
                return;
            }
            _finalized = true;
            Finalizer.Instance.AddFinalizedObject(ref target);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Runtime.XDecref(target);
            target = IntPtr.Zero;
            dtype = null;
            GC.SuppressFinalize(this);
        }

        public object Dispatch(object[] args)
        {
            IntPtr gs = PythonEngine.AcquireLock();
            object ob;

            try
            {
                ob = TrueDispatch(args);
            }
            finally
            {
                PythonEngine.ReleaseLock(gs);
            }

            return ob;
        }

        private object TrueDispatch(object[] args)
        {
            MethodInfo method = dtype.GetMethod("Invoke");
            ParameterInfo[] pi = method.GetParameters();
            IntPtr pyargs = Runtime.PyTuple_New(pi.Length);
            Type rtype = method.ReturnType;

            for (var i = 0; i < pi.Length; i++)
            {
                // Here we own the reference to the Python value, and we
                // give the ownership to the arg tuple.
                IntPtr arg = Converter.ToPython(args[i], pi[i].ParameterType);
                Runtime.PyTuple_SetItem(pyargs, i, arg);
            }

            IntPtr op = Runtime.PyObject_Call(target, pyargs, IntPtr.Zero);
            Runtime.XDecref(pyargs);

            if (op == IntPtr.Zero)
            {
                throw PythonException.ThrowLastAsClrException();
            }

            try
            {
                int byRefCount = pi.Count(parameterInfo => parameterInfo.ParameterType.IsByRef);
                if (byRefCount > 0)
                {
                    // By symmetry with MethodBinder.Invoke, when there are out
                    // parameters we expect to receive a tuple containing
                    // the result, if any, followed by the out parameters. If there is only
                    // one out parameter and the return type of the method is void,
                    // we instead receive the out parameter as the result from Python.

                    bool isVoid = rtype == typeof(void);
                    int tupleSize = byRefCount + (isVoid ? 0 : 1);
                    if (isVoid && byRefCount == 1)
                    {
                        // The return type is void and there is a single out parameter.
                        for (int i = 0; i < pi.Length; i++)
                        {
                            Type t = pi[i].ParameterType;
                            if (t.IsByRef)
                            {
                                if (!Converter.ToManaged(op, t, out object newArg, true))
                                {
                                    Exceptions.RaiseTypeError($"The Python function did not return {t.GetElementType()} (the out parameter type)");
                                    throw PythonException.ThrowLastAsClrException();
                                }
                                args[i] = newArg;
                                break;
                            }
                        }
                        return null;
                    }
                    else if (Runtime.PyTuple_Check(op) && Runtime.PyTuple_Size(op) == tupleSize)
                    {
                        int index = isVoid ? 0 : 1;
                        for (int i = 0; i < pi.Length; i++)
                        {
                            Type t = pi[i].ParameterType;
                            if (t.IsByRef)
                            {
                                IntPtr item = Runtime.PyTuple_GetItem(op, index++);
                                if (!Converter.ToManaged(item, t, out object newArg, true))
                                {
                                    Exceptions.RaiseTypeError($"The Python function returned a tuple where element {i} was not {t.GetElementType()} (the out parameter type)");
                                    throw PythonException.ThrowLastAsClrException();
                                }
                                args[i] = newArg;
                            }
                        }
                        if (isVoid)
                        {
                            return null;
                        }
                        IntPtr item0 = Runtime.PyTuple_GetItem(op, 0);
                        if (!Converter.ToManaged(item0, rtype, out object result0, true))
                        {
                            Exceptions.RaiseTypeError($"The Python function returned a tuple where element 0 was not {rtype} (the return type)");
                            throw PythonException.ThrowLastAsClrException();
                        }
                        return result0;
                    }
                    else
                    {
                        string tpName = Runtime.PyObject_GetTypeName(op);
                        if (Runtime.PyTuple_Check(op))
                        {
                            tpName += $" of size {Runtime.PyTuple_Size(op)}";
                        }
                        StringBuilder sb = new StringBuilder();
                        if (!isVoid) sb.Append(rtype.FullName);
                        for (int i = 0; i < pi.Length; i++)
                        {
                            Type t = pi[i].ParameterType;
                            if (t.IsByRef)
                            {
                                if (sb.Length > 0) sb.Append(",");
                                sb.Append(t.GetElementType().FullName);
                            }
                        }
                        string returnValueString = isVoid ? "" : "the return value and ";
                        Exceptions.RaiseTypeError($"Expected a tuple ({sb}) of {returnValueString}the values for out and ref parameters, got {tpName}.");
                        throw PythonException.ThrowLastAsClrException();
                    }
                }

                if (rtype == typeof(void))
                {
                    return null;
                }

                object result;
                if (!Converter.ToManaged(op, rtype, out result, true))
                {
                    throw PythonException.ThrowLastAsClrException();
                }

                return result;
            }
            finally
            {
                Runtime.XDecref(op);
            }
        }
    }
}