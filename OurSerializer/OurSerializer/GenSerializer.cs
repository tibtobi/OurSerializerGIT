using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection.Emit;
using System.Data;
using System.Security.Permissions;
//Debug
using System.Diagnostics;

namespace Extending_WCF
{
    public static class GenSerializer
    {
        private static Dictionary<string, Tuple<Delegate, Delegate>> genSerDict = new Dictionary<string, Tuple<Delegate, Delegate>>();
        public static Dictionary<string, Dictionary<int, object>> objectStore = new Dictionary<string, Dictionary<int, object>>();
        public static Dictionary<string, Dictionary<object, int>> referenceStore = new Dictionary<string, Dictionary<object, int>>();
        public static int refCounter = 0;
        private static IdentityEqualityComparer<object> iec = new IdentityEqualityComparer<object>();
        private static Type genSerType = typeof(GenSerializer);
        private static BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static bool isOnTop = false;
        private delegate void serialize<T>(Stream s, T obj, bool checkType, int referenceId);
        private delegate T deserialize<T>(Stream s, int objectId);

        private static bool DEBUG = true;

        /// <summary>
        /// Creates a new instance of GeneratedSerializer
        /// </summary>
        /// <typeparam name="T">The type it serializes</typeparam>
        /// <param name="name">Name of the type</param>
        /// <returns>GeneratedSerializer</returns>
        private static void CreateSerializer<T>(string name)
        {
            //For debug purposes writing out to a DLL
            var assemblyName = new AssemblyName("Serializer");
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll");
            var typebuilder = moduleBuilder.DefineType("GeneratedSerializer", TypeAttributes.Public);
            //metódusok hozzáadása a típushoz
            var debugSerializeMethodBuilder = typebuilder.DefineMethod("Serialize", MethodAttributes.Public | MethodAttributes.Static, typeof(void), new Type[] { typeof(Stream), typeof(T), typeof(bool), typeof(int) });
            var debugDeserializeMethodBuilder = typebuilder.DefineMethod("Deserialize", MethodAttributes.Public | MethodAttributes.Static, typeof(T), new Type[] { typeof(Stream), typeof(int) });
            
            ILGenerator debugSIL = debugSerializeMethodBuilder.GetILGenerator();
            ILGenerator debugDIL = debugDeserializeMethodBuilder.GetILGenerator();
            Discover<T>(debugSIL, debugDIL);
            debugSIL.Emit(OpCodes.Ret);
            debugDIL.Emit(OpCodes.Ret);
            typebuilder.CreateType();
            assemblyBuilder.Save(assemblyName.Name + ".dll");

            ResetVariables();
            var serializeMethodBuilder = new DynamicMethod(name + "_Serialize", typeof(void), new Type[] { typeof(Stream), typeof(T), typeof(bool), typeof(int) }, true);
            var deserializeMethodBuilder = new DynamicMethod(name + "_Deserialize", typeof(T), new Type[] { typeof(Stream), typeof(int) }, true);
            ILGenerator sil = serializeMethodBuilder.GetILGenerator();
            ILGenerator dil = deserializeMethodBuilder.GetILGenerator();

            Discover<T>(sil, dil);

            sil.Emit(OpCodes.Ret);
            dil.Emit(OpCodes.Ret);
            var serializerDelegate = serializeMethodBuilder.CreateDelegate(typeof(serialize<T>));
            var deserializerDelegate = deserializeMethodBuilder.CreateDelegate(typeof(deserialize<T>));
            Tuple<Delegate, Delegate> serializerTuple = new Tuple<Delegate, Delegate>(serializerDelegate, deserializerDelegate);

            genSerDict.Add(name, serializerTuple);
            objectStore.Add(name, new Dictionary<int, object>());
            referenceStore.Add(name, new Dictionary<object, int>(iec));
            //Console.WriteLine(name + " created.");
        }

        private static void ResetVariables()
        {
            isOnTop = false;
        }

        private static void Discover<T>(ILGenerator sil, ILGenerator dil)
        {
            Type t = typeof(T);
            //begyűjti a függvény információkat a későbbi névszerinti hívásokhoz
            MethodInfo makeGenericMethod = typeof(MethodInfo).GetMethod("MakeGenericMethod");
            MethodInfo getType = typeof(Object).GetMethod("GetType");
            MethodInfo getSerializer = genSerType.GetMethod("getSerializer", BindingFlags.Public | BindingFlags.Static);
            MethodInfo getDeserializer = genSerType.GetMethod("getDeserializer", BindingFlags.Public | BindingFlags.Static);
            MethodInfo serialize = genSerType.GetMethod("_Serialize", BindingFlags.Public | BindingFlags.Static);
            MethodInfo deserialize = genSerType.GetMethod("_Deserialize", BindingFlags.Public | BindingFlags.Static);
            MethodInfo getMethod = typeof(Type).GetMethod("GetMethod", new Type[] { typeof(string), typeof(BindingFlags) });
            MethodInfo createMethod = typeof(Activator).GetMethod("CreateInstance", Type.EmptyTypes);
            MethodInfo invoke = typeof(MethodInfo).GetMethod("Invoke", new Type[] { typeof(object), typeof(object[]) });
            MethodInfo getAssemblyQualifiedName = typeof(Type).GetProperty("AssemblyQualifiedName").GetGetMethod();
            MethodInfo stringEquals = typeof(String).GetMethod("Equals", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(string) }, null);
            createMethod = createMethod.MakeGenericMethod(t);
            //címkéket hoz létre a IL kódhoz
            Label serNullLabel = sil.DefineLabel();
            Label deserNullLabel = dil.DefineLabel();
            Label notNullLabel = sil.DefineLabel();
            Label collectionEntryLabel = sil.DefineLabel();
            Label collectionExitLabel = sil.DefineLabel();
            Label isInRefStoreLabel = sil.DefineLabel();
            Label isNotInObjStoreLabel = dil.DefineLabel();
            //változókat hoz létre az IL kódhoz
            LocalBuilder locSer = sil.DeclareLocal(t);
            LocalBuilder locDe = dil.DeclareLocal(t);
            LocalBuilder referenceId = sil.DeclareLocal(typeof(int));
            LocalBuilder objectId = dil.DeclareLocal(typeof(int));

            //ellenőrzi, hogy az sorosítandó objektum a stack tetején van-e, ha nincs akkor push-olja. Azért az Ldarg_1-et használ, 
            //mert statikus függvény ezért nincs this
            if (!isOnTop)
            {
                isOnTop = true;
                sil.Emit(OpCodes.Ldarg_1);
            }

            //változóba helyezi a sorosítandó objektumot, és jelzi, hogy nincs a stack tetején
            sil.Emit(OpCodes.Stloc, locSer);
            isOnTop = false;

            //debug mód esetén tárolja a sorosított objektum nevét, illetve visszállításnál kiolvassa
            if (DEBUG)
            {
                sil.Emit(OpCodes.Ldstr, t.AssemblyQualifiedName);
                EmitStringSerialze(sil);

                EmitStringDeserialze(dil);
                dil.Emit(OpCodes.Pop);
            }
            
            //primitív T esetén a megfelelő metódusokat helyezi el a szerelvénybe
            if (t.IsPrimitive)
            {
                sil.Emit(OpCodes.Ldloc, locSer);
                EmitSerializePrimitive<T>(sil);
                EmitDeserializePrimitive<T>(dil);
            }
            else
            {
                if (t.IsClass)
                {
                    //Is null?  ha null akkor betölt -1-et
                    sil.Emit(OpCodes.Ldloc, locSer);
                    sil.Emit(OpCodes.Ldnull);
                    sil.Emit(OpCodes.Ceq);
                    sil.Emit(OpCodes.Brfalse, notNullLabel);
                    sil.Emit(OpCodes.Ldc_I4, -1);
                    EmitSerializePrimitive<int>(sil);
                    sil.Emit(OpCodes.Br, serNullLabel);
                    sil.MarkLabel(notNullLabel);

                    //Check if objectId has been already read
                    Label objectIdRead = dil.DefineLabel();
                    dil.Emit(OpCodes.Ldarg_1);          //betölti az objektumot
                    dil.Emit(OpCodes.Stloc, objectId);
                    dil.Emit(OpCodes.Ldarg_1);
                    dil.Emit(OpCodes.Ldc_I4, -1);
                    dil.Emit(OpCodes.Bgt, objectIdRead);
                    //Checking if null and get the reference id
                    dil.Emit(OpCodes.Ldnull);
                    EmitDeserializePrimitive<int>(dil);
                    dil.Emit(OpCodes.Stloc, objectId);
                    //Is null?
                    dil.Emit(OpCodes.Ldloc, objectId);
                    dil.Emit(OpCodes.Ldc_I4, -1);
                    dil.Emit(OpCodes.Ceq);
                    dil.Emit(OpCodes.Brtrue, deserNullLabel);
                    dil.Emit(OpCodes.Pop);
                    dil.MarkLabel(objectIdRead);
                }
                
                //ellenőrzi hogy a collection-e 
                if (typeof(ICollection).IsAssignableFrom(t) && typeof(ICollection<>).MakeGenericType(t.GetGenericArguments()[0]).IsAssignableFrom(t))
                {
                    #region Collections
                    MethodInfo getCurrent = typeof(IEnumerator).GetProperty("Current").GetGetMethod();
                    MethodInfo getEnumerator = typeof(IEnumerable).GetMethod("GetEnumerator");
                    MethodInfo moveNext = typeof(IEnumerator).GetMethod("MoveNext");
                    MethodInfo reset = typeof(IEnumerator).GetMethod("Reset");
                    MethodInfo add = t.GetMethod("Add");
                    MethodInfo serializeTyped = serialize.MakeGenericMethod(t.GetGenericArguments()[0]);
                    //SERIALIZE Collection
                    Label countCollection = sil.DefineLabel();
                    Label countCollectionEnd = sil.DefineLabel();
                    LocalBuilder enumerator = sil.DeclareLocal(typeof(IEnumerator));
                    LocalBuilder serializeCount = sil.DeclareLocal(typeof(int));
                    LocalBuilder checkType = sil.DeclareLocal(typeof(bool));
                    //Serialize an indicator to the collection is not null
                    sil.Emit(OpCodes.Ldc_I4, 1);
                    EmitSerializePrimitive<int>(sil);
                    //Get the item count of the collection
                    sil.Emit(OpCodes.Ldloc, locSer);
                    sil.Emit(OpCodes.Callvirt, getEnumerator);
                    //tárolja a collection iterátorát
                    sil.Emit(OpCodes.Stloc, enumerator);
                    //inicializálja a segédváltozókat
                    sil.Emit(OpCodes.Ldc_I4, -1);
                    sil.Emit(OpCodes.Stloc, serializeCount);
                    sil.Emit(OpCodes.Ldc_I4, 0);
                    sil.Emit(OpCodes.Stloc, checkType);
                    sil.MarkLabel(countCollection);
                    sil.Emit(OpCodes.Ldloc, serializeCount);
                    //Add one to the counter
                    //végig megy a collection-ön, megszámolja az elemeit egy ciklussal
                    sil.Emit(OpCodes.Ldc_I4, 1);
                    sil.Emit(OpCodes.Add);
                    sil.Emit(OpCodes.Stloc, serializeCount);
                    sil.Emit(OpCodes.Ldloc, enumerator);
                    sil.Emit(OpCodes.Callvirt, moveNext);
                    sil.Emit(OpCodes.Brfalse, countCollectionEnd);
                    //Check whether it's heterogenius
                    sil.Emit(OpCodes.Ldloc, checkType);
                    sil.Emit(OpCodes.Brtrue, countCollection);
                    //új típusnál ellenőrzi a típus adatait, és folytatja a számolást
                    sil.Emit(OpCodes.Ldloc, enumerator);
                    sil.Emit(OpCodes.Callvirt, getCurrent);
                    sil.Emit(OpCodes.Callvirt, getType);
                    sil.Emit(OpCodes.Callvirt, getAssemblyQualifiedName);
                    sil.Emit(OpCodes.Ldstr, t.GetGenericArguments()[0].AssemblyQualifiedName);
                    sil.Emit(OpCodes.Call, stringEquals);
                    sil.Emit(OpCodes.Stloc, checkType);
                    sil.Emit(OpCodes.Br, countCollection);
                    sil.MarkLabel(countCollectionEnd);
                    //tárolja az elemszámot
                    sil.Emit(OpCodes.Ldloc, serializeCount);
                    EmitSerializePrimitive<int>(sil);
                    sil.Emit(OpCodes.Ldloc, enumerator);
                    sil.Emit(OpCodes.Callvirt, reset);
                    //Serialize the items one by one
                    sil.MarkLabel(collectionEntryLabel);
                    sil.Emit(OpCodes.Ldloc, enumerator);
                    sil.Emit(OpCodes.Callvirt, moveNext);
                    sil.Emit(OpCodes.Brfalse, collectionExitLabel);
                    sil.Emit(OpCodes.Ldarg_0);
                    sil.Emit(OpCodes.Ldloc, enumerator);
                    sil.Emit(OpCodes.Callvirt, getCurrent);
                    sil.Emit(OpCodes.Unbox_Any, t.GetGenericArguments()[0]);
                    sil.Emit(OpCodes.Ldloc, checkType);
                    sil.Emit(OpCodes.Ldc_I4, -1);
                    sil.Emit(OpCodes.Call, serializeTyped);         //meghívja a _Serialize függvényt az unboxed (megfeleltetett) típussal
                    sil.Emit(OpCodes.Br, collectionEntryLabel);         
                    sil.MarkLabel(collectionExitLabel);

                    //DESERIALIZE Collection
                    Label desCollMember = dil.DefineLabel();
                    LocalBuilder deserializeCount = dil.DeclareLocal(typeof(int));
                    //létrehoz egy referenciát a későbbi objektumnak
                    dil.Emit(OpCodes.Call, createMethod);
                    dil.Emit(OpCodes.Stloc, locDe);
                    //megkapja a hosszát a collection-nek
                    EmitDeserializePrimitive<int>(dil);
                    dil.Emit(OpCodes.Stloc, deserializeCount);
                    dil.MarkLabel(desCollMember);
                    dil.Emit(OpCodes.Ldloc, locDe);
                    MethodInfo deserializeTyped = deserialize.MakeGenericMethod(t.GetGenericArguments()[0]);
                    //egyesével kiolvassa az elemeket és csökkenti a hosszt számontartó változót
                    dil.Emit(OpCodes.Ldarg_0);
                    dil.Emit(OpCodes.Ldc_I4, -1);
                    dil.Emit(OpCodes.Call, deserializeTyped);
                    dil.Emit(OpCodes.Call, add);
                    dil.Emit(OpCodes.Ldloc, deserializeCount);
                    dil.Emit(OpCodes.Ldc_I4, 1);
                    dil.Emit(OpCodes.Sub);
                    dil.Emit(OpCodes.Stloc, deserializeCount);
                    dil.Emit(OpCodes.Ldloc, deserializeCount);
                    dil.Emit(OpCodes.Ldc_I4, 0);
                    dil.Emit(OpCodes.Cgt);
                    dil.Emit(OpCodes.Brtrue, desCollMember);
                }
                #endregion
                else            //ha nem collection
                {
                    LocalBuilder objStore = dil.DeclareLocal(typeof(Dictionary<int, object>));
                    if (t.IsClass)
                    {
                        #region referenceId/objectId
                        //Serialize the referenceId if it's not already serialized
                        Label isRefIdSerialized = sil.DefineLabel();
                        LocalBuilder refStore = sil.DeclareLocal(typeof(Dictionary<object, int>));
                        LocalBuilder refIsIn = sil.DeclareLocal(typeof(bool));
                        //Get the appropriate reference store
                        FieldInfo referenceStoreFI = genSerType.GetField("referenceStore", BindingFlags.Static | BindingFlags.Public);
                        FieldInfo refCounterFI = genSerType.GetField("refCounter", BindingFlags.Static | BindingFlags.Public);
                        MethodInfo tryGetValueRefStore = referenceStore.GetType().GetMethod("TryGetValue");
                        MethodInfo tryGetValue = typeof(Dictionary<object, int>).GetMethod("TryGetValue");
                        MethodInfo add = typeof(Dictionary<object, int>).GetMethod("Add");
                        //stackbe tölti a referencId-t
                        sil.Emit(OpCodes.Ldarg_3);
                        sil.Emit(OpCodes.Stloc, referenceId);
                        sil.Emit(OpCodes.Ldarg_3);
                        sil.Emit(OpCodes.Ldc_I4, -1);
                        //ellenőrzi, hogy a referenceId sorosítva van-e
                        sil.Emit(OpCodes.Bgt, isRefIdSerialized);
                        sil.Emit(OpCodes.Ldsfld, referenceStoreFI);
                        sil.Emit(OpCodes.Ldloc, locSer);
                        //bedobozolja az objektumot
                        sil.Emit(OpCodes.Box, t);
                        //lekérdezi a típust
                        sil.Emit(OpCodes.Callvirt, getType);
                        sil.Emit(OpCodes.Callvirt, getAssemblyQualifiedName);
                        sil.Emit(OpCodes.Ldloca, refStore);
                        sil.Emit(OpCodes.Callvirt, tryGetValueRefStore);
                        //The returning bool should be always true
                        sil.Emit(OpCodes.Pop);
                        //Get the refStoreId
                        sil.Emit(OpCodes.Ldloc, refStore);
                        sil.Emit(OpCodes.Ldloc, locSer);
                        sil.Emit(OpCodes.Ldloca, referenceId);
                        sil.Emit(OpCodes.Callvirt, tryGetValue);
                        sil.Emit(OpCodes.Stloc, refIsIn);
                        sil.Emit(OpCodes.Ldloc, referenceId);
                        sil.Emit(OpCodes.Ldloc, refIsIn);
                        sil.Emit(OpCodes.Brtrue, isInRefStoreLabel);
                        sil.Emit(OpCodes.Pop);
                        sil.Emit(OpCodes.Ldsfld, refCounterFI);
                        sil.Emit(OpCodes.Ldsfld, refCounterFI);
                        sil.Emit(OpCodes.Ldc_I4, 1);
                        sil.Emit(OpCodes.Add);
                        sil.Emit(OpCodes.Stsfld, refCounterFI);
                        //Write the reference id
                        sil.MarkLabel(isInRefStoreLabel);
                        sil.Emit(OpCodes.Stloc, referenceId);
                        sil.Emit(OpCodes.Ldloc, referenceId);
                        EmitSerializePrimitive<int>(sil);
                        sil.Emit(OpCodes.Ldloc, refIsIn);
                        sil.Emit(OpCodes.Brtrue, serNullLabel);
                        //Put in the refStore
                        sil.Emit(OpCodes.Ldloc, refStore);
                        sil.Emit(OpCodes.Ldloc, locSer);
                        sil.Emit(OpCodes.Ldloc, referenceId);
                        sil.Emit(OpCodes.Callvirt, add);
                        sil.MarkLabel(isRefIdSerialized);
                        
                        LocalBuilder tempObj = dil.DeclareLocal(typeof(object));
                        LocalBuilder objIsIn = dil.DeclareLocal(typeof(bool));
                        //Get the apropriate objectStore
                        FieldInfo objectStoreFI = genSerType.GetField("objectStore", BindingFlags.Static | BindingFlags.Public);
                        MethodInfo tryGetValueObjStore = objectStore.GetType().GetMethod("TryGetValue");
                        MethodInfo tryGetObject = typeof(Dictionary<int, object>).GetMethod("TryGetValue");
                        dil.Emit(OpCodes.Ldsfld, objectStoreFI);
                        dil.Emit(OpCodes.Ldstr, t.AssemblyQualifiedName);
                        dil.Emit(OpCodes.Ldloca, objStore);
                        dil.Emit(OpCodes.Callvirt, tryGetValueObjStore);
                        //The returning bool should be always true
                        dil.Emit(OpCodes.Pop);
                        //Search the objectStore
                        dil.Emit(OpCodes.Ldloc, objStore);
                        dil.Emit(OpCodes.Ldloc, objectId);
                        dil.Emit(OpCodes.Ldloca, tempObj);
                        dil.Emit(OpCodes.Callvirt, tryGetObject);
                        dil.Emit(OpCodes.Brfalse, isNotInObjStoreLabel);
                        dil.Emit(OpCodes.Ldloc, tempObj);
                        dil.Emit(OpCodes.Castclass, t);
                        dil.Emit(OpCodes.Br, deserNullLabel);
                        dil.MarkLabel(isNotInObjStoreLabel);
                        #endregion
                    }
                    if (t == typeof(string))
                    {
                        sil.Emit(OpCodes.Ldloc, locSer);
                        EmitStringSerialze(sil);
                        EmitStringDeserialze(dil);
                        dil.Emit(OpCodes.Stloc, locDe);
                    }
                    else
                    {
                        //ez nem fut le sosem
                        
                        if (false && t.IsClass)
                        {
                            #region descendant
                            Label serNotDescendant = sil.DefineLabel();
                            Label serNoCheckType = sil.DefineLabel();
                            LocalBuilder serDescendant = sil.DeclareLocal(typeof(bool));
                            LocalBuilder serField = sil.DeclareLocal(typeof(object));
                            LocalBuilder serTypeArray = sil.DeclareLocal(typeof(Type[]));
                            LocalBuilder serObjectArray = sil.DeclareLocal(typeof(object[]));
                            MethodInfo serializeString = serialize.MakeGenericMethod(typeof(string));
                            //If checkType is false, it doesn't check type
                            sil.Emit(OpCodes.Ldc_I4, 1);
                            sil.Emit(OpCodes.Stloc, serDescendant);
                            sil.Emit(OpCodes.Ldarg_2);
                            sil.Emit(OpCodes.Brfalse, serNoCheckType);
                            //Check whether it's a descendant or not
                            sil.Emit(OpCodes.Ldloc, locSer);
                            sil.Emit(OpCodes.Box, t);
                            sil.Emit(OpCodes.Callvirt, getType);
                            sil.Emit(OpCodes.Callvirt, getAssemblyQualifiedName);
                            sil.Emit(OpCodes.Ldstr, t.AssemblyQualifiedName);
                            sil.Emit(OpCodes.Call, stringEquals);
                            sil.Emit(OpCodes.Stloc, serDescendant);
                            sil.MarkLabel(serNoCheckType);
                            sil.Emit(OpCodes.Ldloc, serDescendant);
                            EmitSerializePrimitive<bool>(sil);
                            sil.Emit(OpCodes.Ldloc, serDescendant);
                            //If it's not, serialize
                            sil.Emit(OpCodes.Brtrue, serNotDescendant);
                            //If descendant, call the appropiate serializer
                            sil.Emit(OpCodes.Ldarg_0);
                            sil.Emit(OpCodes.Ldstr, t.AssemblyQualifiedName);
                            sil.Emit(OpCodes.Ldc_I4, 1);
                            sil.Emit(OpCodes.Ldc_I4, -1);
                            sil.Emit(OpCodes.Call, serializeString);

                            //Store the stream, the object and checkType in an object array
                            sil.Emit(OpCodes.Ldloc, locSer);
                            sil.Emit(OpCodes.Box, t);
                            sil.Emit(OpCodes.Stloc, serField);
                            sil.Emit(OpCodes.Ldc_I4, 4);
                            sil.Emit(OpCodes.Newarr, typeof(object));
                            sil.Emit(OpCodes.Stloc, serObjectArray);
                            sil.Emit(OpCodes.Ldloc, serObjectArray);
                            sil.Emit(OpCodes.Ldc_I4, 0);
                            sil.Emit(OpCodes.Ldarg_0);
                            sil.Emit(OpCodes.Stelem_Ref);
                            sil.Emit(OpCodes.Ldloc, serObjectArray);
                            sil.Emit(OpCodes.Ldc_I4, 1);
                            sil.Emit(OpCodes.Ldloc, serField);
                            sil.Emit(OpCodes.Stelem_Ref);
                            sil.Emit(OpCodes.Ldloc, serObjectArray);
                            sil.Emit(OpCodes.Ldc_I4, 2);
                            sil.Emit(OpCodes.Ldc_I4, 0);
                            sil.Emit(OpCodes.Box, typeof(bool));
                            sil.Emit(OpCodes.Stelem_Ref);
                            sil.Emit(OpCodes.Ldloc, serObjectArray);
                            sil.Emit(OpCodes.Ldc_I4, 3);
                            sil.Emit(OpCodes.Ldloc, referenceId);
                            sil.Emit(OpCodes.Box, typeof(int));
                            sil.Emit(OpCodes.Stelem_Ref);
                            //Make the typed serialize method from the generic
                            sil.Emit(OpCodes.Call, getSerializer);
                            sil.Emit(OpCodes.Ldc_I4, 1);
                            sil.Emit(OpCodes.Newarr, typeof(Type));
                            sil.Emit(OpCodes.Stloc, serTypeArray);
                            sil.Emit(OpCodes.Ldloc, serTypeArray);
                            sil.Emit(OpCodes.Ldc_I4, 0);
                            sil.Emit(OpCodes.Ldloc, serField);
                            sil.Emit(OpCodes.Callvirt, getType);
                            sil.Emit(OpCodes.Stelem_Ref);
                            sil.Emit(OpCodes.Ldloc, serTypeArray);
                            sil.Emit(OpCodes.Callvirt, makeGenericMethod);
                            sil.Emit(OpCodes.Ldnull);
                            sil.Emit(OpCodes.Ldloc, serObjectArray);
                            sil.Emit(OpCodes.Callvirt, invoke);
                            //It's void, so Invoke returns a null object
                            sil.Emit(OpCodes.Pop);
                            sil.Emit(OpCodes.Br, serNullLabel);
                            sil.MarkLabel(serNotDescendant);

                            //get whether it's heterogeneius and if it is, then call the appropriate deserializer
                            Label deserNotDescendant = dil.DefineLabel();
                            LocalBuilder deserDescendant = dil.DeclareLocal(typeof(bool));
                            LocalBuilder originalType = dil.DeclareLocal(typeof(Type));
                            LocalBuilder deserTypeArray = dil.DeclareLocal(typeof(Type[]));
                            LocalBuilder deserObjectArray = dil.DeclareLocal(typeof(object[]));
                            MethodInfo getTypeByName = typeof(Type).GetMethod("GetType", new Type[] { typeof(string) });
                            MethodInfo deserializeString = deserialize.MakeGenericMethod(typeof(string));
                            EmitDeserializePrimitive<bool>(dil);
                            dil.Emit(OpCodes.Stloc, deserDescendant);
                            dil.Emit(OpCodes.Ldloc, deserDescendant);
                            dil.Emit(OpCodes.Brtrue, deserNotDescendant);
                            //If descendant get the original type, and call the appropiate deserializer
                            dil.Emit(OpCodes.Ldarg_0);
                            dil.Emit(OpCodes.Ldc_I4, -1);
                            dil.Emit(OpCodes.Call, deserializeString);
                            dil.Emit(OpCodes.Call, getTypeByName);
                            dil.Emit(OpCodes.Stloc, originalType);
                            //Put the input stream, and the objectId into an object array for invoke
                            dil.Emit(OpCodes.Ldc_I4, 2);
                            dil.Emit(OpCodes.Newarr, typeof(object));
                            dil.Emit(OpCodes.Stloc, deserObjectArray);
                            dil.Emit(OpCodes.Ldloc, deserObjectArray);
                            dil.Emit(OpCodes.Ldc_I4, 0);
                            dil.Emit(OpCodes.Ldarg_0);
                            dil.Emit(OpCodes.Stelem_Ref);
                            dil.Emit(OpCodes.Ldloc, deserObjectArray);
                            dil.Emit(OpCodes.Ldc_I4, 1);
                            dil.Emit(OpCodes.Ldloc, objectId);
                            dil.Emit(OpCodes.Box, typeof(int));
                            dil.Emit(OpCodes.Stelem_Ref);
                            //Make the typed deserialize method from the generic
                            dil.Emit(OpCodes.Call, getDeserializer);
                            dil.Emit(OpCodes.Ldc_I4, 1);
                            dil.Emit(OpCodes.Newarr, typeof(Type));
                            dil.Emit(OpCodes.Stloc, deserTypeArray);
                            dil.Emit(OpCodes.Ldloc, deserTypeArray);
                            dil.Emit(OpCodes.Ldc_I4, 0);
                            dil.Emit(OpCodes.Ldloc, originalType);
                            dil.Emit(OpCodes.Stelem_Ref);
                            dil.Emit(OpCodes.Ldloc, deserTypeArray);
                            dil.Emit(OpCodes.Callvirt, makeGenericMethod);
                            dil.Emit(OpCodes.Ldnull);
                            dil.Emit(OpCodes.Ldloc, deserObjectArray);
                            dil.Emit(OpCodes.Callvirt, invoke);
                            dil.Emit(OpCodes.Castclass, t);
                            dil.Emit(OpCodes.Br, deserNullLabel);
                            dil.MarkLabel(deserNotDescendant);
                        #endregion
                        }
                        
                        MethodInfo discMethod = genSerType.GetMethod("Discover", BindingFlags.NonPublic | BindingFlags.Static);
                        var fields = t.GetFields(bf).OrderBy(x => x.Name);
                        dil.Emit(OpCodes.Call, createMethod);
                        dil.Emit(OpCodes.Stloc, locDe);

                        foreach (var item in fields)
                        {

                            if (!isOnTop)
                            {
                                isOnTop = true;
                                if (t.IsClass)
                                {
                                    sil.Emit(OpCodes.Ldloc, locSer);
                                    dil.Emit(OpCodes.Ldloc, locDe);
                                }
                                else
                                {
                                    sil.Emit(OpCodes.Ldloca, locSer);
                                    dil.Emit(OpCodes.Ldloca, locDe);
                                }
                            }
                            var so = t.GetField(item.Name, bf);
                            if (item.FieldType.IsClass)
                            {
                                MethodInfo serializeTyped = serialize.MakeGenericMethod(item.FieldType);
                                LocalBuilder serObj = sil.DeclareLocal(item.FieldType);
                                isOnTop = false;
                                sil.Emit(OpCodes.Ldfld, so);
                                sil.Emit(OpCodes.Stloc, serObj);
                                sil.Emit(OpCodes.Ldarg_0);
                                sil.Emit(OpCodes.Ldloc, serObj);
                                sil.Emit(OpCodes.Ldc_I4, 1);
                                sil.Emit(OpCodes.Ldc_I4, -1);
                                sil.Emit(OpCodes.Call, serializeTyped);

                                MethodInfo deserializeTyped = deserialize.MakeGenericMethod(item.FieldType);
                                dil.Emit(OpCodes.Ldarg_0);
                                dil.Emit(OpCodes.Ldc_I4, -1);
                                dil.Emit(OpCodes.Call, deserializeTyped);
                                dil.Emit(OpCodes.Stfld, so);
                            }
                            else
                            {
                                MethodInfo discover = discMethod.MakeGenericMethod(so.FieldType);
                                sil.Emit(OpCodes.Ldfld, so);
                                discover.Invoke(null, new object[] { sil, dil });
                                dil.Emit(OpCodes.Stfld, so);
                            }

                        }
                    }
                    if (t.IsClass)
                    {
                        MethodInfo addObject = typeof(Dictionary<int, object>).GetMethod("Add");
                        dil.Emit(OpCodes.Ldloc, objStore);
                        dil.Emit(OpCodes.Ldloc, objectId);
                        dil.Emit(OpCodes.Ldloc, locDe);
                        dil.Emit(OpCodes.Callvirt, addObject);
                    }
                }
                dil.Emit(OpCodes.Ldloc, locDe);
                dil.MarkLabel(deserNullLabel);
                sil.MarkLabel(serNullLabel);
            }
        }

        private static void EmitDeserializePrimitive<T>(ILGenerator dil)
        {
            Type t = typeof(T);
            int size = 0;
            MethodInfo readFromStream = typeof(Stream).GetMethod("Read", new Type[] { typeof(byte[]), typeof(int), typeof(int) });
            MethodInfo readByteFromStream = typeof(Stream).GetMethod("ReadByte", new Type[] { });
            LocalBuilder buffer = dil.DeclareLocal(typeof(byte[]));
            TypeCode tc = Type.GetTypeCode(typeof(T));
            MethodInfo getObject = null;
            OpCode conv = OpCodes.Conv_I8;
            bool isFloat = false;
            switch (tc)
            {
                case TypeCode.Boolean:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                    conv = OpCodes.Conv_I4;
                    break;
                case TypeCode.Int64:
                    break;
                case TypeCode.Byte:
                case TypeCode.Char:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                    conv = OpCodes.Conv_U4;
                    break;
                case TypeCode.UInt64:
                    conv = OpCodes.Conv_U8;
                    break;
                case TypeCode.Double:
                    size = sizeof(double);
                    getObject = typeof(BitConverter).GetMethod("ToDouble", new Type[] { typeof(byte[]), typeof(int) });
                    isFloat = true;
                    break;
                case TypeCode.Single:
                    size = sizeof(float);
                    getObject = typeof(BitConverter).GetMethod("ToSingle", new Type[] { typeof(byte[]), typeof(int) });
                    isFloat = true;
                    break;
                case TypeCode.Empty:
                default:
                    throw new NotImplementedException();
            }
            if (!isFloat)
            {
                LocalBuilder num = dil.DeclareLocal(typeof(ulong));
                LocalBuilder shift = dil.DeclareLocal(typeof(int));
                LocalBuilder readByte = dil.DeclareLocal(typeof(byte));
                Label endOfWhile = dil.DefineLabel();
                Label beginOfWhile = dil.DefineLabel();
                Label notNegative = dil.DefineLabel();
                dil.Emit(OpCodes.Ldc_I4, 0);
                dil.Emit(OpCodes.Stloc, shift);
                dil.Emit(OpCodes.Ldc_I8, 0L);
                dil.Emit(OpCodes.Stloc, num);
                dil.Emit(OpCodes.Ldarg_0);
                dil.Emit(OpCodes.Callvirt, readByteFromStream);
                dil.Emit(OpCodes.Stloc, readByte);
                dil.MarkLabel(beginOfWhile);
                dil.Emit(OpCodes.Ldloc, readByte);
                dil.Emit(OpCodes.Ldc_I4, 0x80);
                dil.Emit(OpCodes.And);
                dil.Emit(OpCodes.Ldc_I4, 0);
                dil.Emit(OpCodes.Beq, endOfWhile);
                dil.Emit(OpCodes.Ldloc, num);
                dil.Emit(OpCodes.Ldc_I4, 0x7F);
                dil.Emit(OpCodes.Ldloc, readByte);
                dil.Emit(OpCodes.And);
                dil.Emit(OpCodes.Conv_I8);
                dil.Emit(OpCodes.Ldloc, shift);
                dil.Emit(OpCodes.Shl);
                dil.Emit(OpCodes.Or);
                dil.Emit(OpCodes.Stloc, num);
                dil.Emit(OpCodes.Ldloc, shift);
                dil.Emit(OpCodes.Ldc_I4, 7);
                dil.Emit(OpCodes.Add);
                dil.Emit(OpCodes.Stloc, shift);
                dil.Emit(OpCodes.Ldarg_0);
                dil.Emit(OpCodes.Callvirt, readByteFromStream);
                dil.Emit(OpCodes.Stloc, readByte);
                dil.Emit(OpCodes.Br, beginOfWhile);
                dil.MarkLabel(endOfWhile);
                dil.Emit(OpCodes.Ldloc, num);
                dil.Emit(OpCodes.Ldc_I4, 0x3F);
                dil.Emit(OpCodes.Ldloc, readByte);
                dil.Emit(OpCodes.And);
                dil.Emit(OpCodes.Conv_I8);
                dil.Emit(OpCodes.Ldloc, shift);
                dil.Emit(OpCodes.Shl);
                dil.Emit(OpCodes.Or);
                dil.Emit(OpCodes.Ldloc, readByte);
                dil.Emit(OpCodes.Ldc_I4, 0x40);
                dil.Emit(OpCodes.And);
                dil.Emit(OpCodes.Ldc_I4, 0);
                dil.Emit(OpCodes.Beq, notNegative);
                dil.Emit(OpCodes.Neg);
                dil.MarkLabel(notNegative);
                dil.Emit(conv);
            }
            else
            {
                dil.Emit(OpCodes.Ldc_I4, size);
                dil.Emit(OpCodes.Newarr, typeof(byte));
                dil.Emit(OpCodes.Stloc, buffer);
                dil.Emit(OpCodes.Ldarg_0);
                dil.Emit(OpCodes.Ldloc, buffer);
                dil.Emit(OpCodes.Ldc_I4, 0);
                dil.Emit(OpCodes.Ldc_I4, size);
                dil.Emit(OpCodes.Callvirt, readFromStream);
                dil.Emit(OpCodes.Pop);
                dil.Emit(OpCodes.Ldloc, buffer);
                dil.Emit(OpCodes.Ldc_I4, 0);
                dil.Emit(OpCodes.Call, getObject);
            }
        }

        private static void EmitSerializePrimitive<T>(ILGenerator sil)
        {
            TypeCode tc = Type.GetTypeCode(typeof(T));
            MethodInfo toByteArray = null;
            MethodInfo writeOnStream = typeof(Stream).GetMethod("Write", new Type[] { typeof(byte[]), typeof(int), typeof(int) });
            MethodInfo writeByteOnStream = typeof(Stream).GetMethod("WriteByte", new Type[] { typeof(byte) });
            LocalBuilder bytes = sil.DeclareLocal(typeof(byte[]));
            int size = 0;
            bool isFloat = false;
            switch (tc)
            {
                case TypeCode.Boolean:
                case TypeCode.Byte:
                case TypeCode.Char:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    LocalBuilder num = sil.DeclareLocal(typeof(ulong));
                    LocalBuilder neg = sil.DeclareLocal(typeof(bool));
                    Label lessThan0x80 = sil.DefineLabel();
                    Label endOfWhile = sil.DefineLabel();
                    Label notNegative = sil.DefineLabel();
                    sil.Emit(OpCodes.Conv_I8);
                    sil.Emit(OpCodes.Stloc, num);
                    sil.Emit(OpCodes.Ldloc, num);
                    sil.Emit(OpCodes.Ldc_I8, 0L);
                    sil.Emit(OpCodes.Clt);
                    sil.Emit(OpCodes.Stloc, neg);
                    sil.Emit(OpCodes.Ldloc, neg);
                    sil.Emit(OpCodes.Brfalse, lessThan0x80);
                    sil.Emit(OpCodes.Ldloc, num);
                    sil.Emit(OpCodes.Neg);
                    sil.Emit(OpCodes.Stloc, num);
                    sil.MarkLabel(lessThan0x80);
                    sil.Emit(OpCodes.Ldloc, num);
                    sil.Emit(OpCodes.Ldc_I8, 0x40L);
                    sil.Emit(OpCodes.Clt_Un);
                    sil.Emit(OpCodes.Brtrue, endOfWhile);
                    sil.Emit(OpCodes.Ldarg_0);
                    sil.Emit(OpCodes.Ldloc, num);
                    sil.Emit(OpCodes.Ldc_I8, 0x80L);
                    sil.Emit(OpCodes.Or);
                    sil.Emit(OpCodes.Conv_U1);
                    sil.Emit(OpCodes.Callvirt, writeByteOnStream);
                    sil.Emit(OpCodes.Ldloc, num);
                    sil.Emit(OpCodes.Ldc_I4, 7);
                    sil.Emit(OpCodes.Shr_Un);
                    sil.Emit(OpCodes.Stloc, num);
                    sil.Emit(OpCodes.Br, lessThan0x80);
                    sil.MarkLabel(endOfWhile);
                    sil.Emit(OpCodes.Ldarg_0);
                    sil.Emit(OpCodes.Ldloc, num);
                    sil.Emit(OpCodes.Ldloc, neg);
                    sil.Emit(OpCodes.Brfalse, notNegative);
                    sil.Emit(OpCodes.Ldc_I8, 0x40L);
                    sil.Emit(OpCodes.Or);
                    sil.MarkLabel(notNegative);
                    sil.Emit(OpCodes.Conv_U1);
                    sil.Emit(OpCodes.Callvirt, writeByteOnStream);
                    break;
                case TypeCode.Double:
                    toByteArray = typeof(BitConverter).GetMethod("GetBytes", new Type[] { typeof(double) });
                    size = sizeof(double);
                    isFloat = true;
                    break;
                case TypeCode.Single:
                    toByteArray = typeof(BitConverter).GetMethod("GetBytes", new Type[] { typeof(float) });
                    size = sizeof(float);
                    isFloat = true;
                    break;
                case TypeCode.Empty:
                default:
                    throw new NotImplementedException();
            }
            if (isFloat)
            {
                sil.Emit(OpCodes.Call, toByteArray);
                sil.Emit(OpCodes.Stloc, bytes);
                sil.Emit(OpCodes.Ldarg_0);
                sil.Emit(OpCodes.Ldloc, bytes);
                sil.Emit(OpCodes.Ldc_I4, 0);
                sil.Emit(OpCodes.Ldc_I4, size);
                sil.Emit(OpCodes.Callvirt, writeOnStream);
            }
        }

        public static void EmitStringDeserialze(ILGenerator dil)
        {
            MethodInfo getObject = typeof(BitConverter).GetMethod("ToInt32", new Type[] { typeof(byte[]), typeof(int) });
            MethodInfo readFromStream = typeof(Stream).GetMethod("Read", new Type[] { typeof(byte[]), typeof(int), typeof(int) });
            LocalBuilder length = dil.DeclareLocal(typeof(int));
            LocalBuilder lengthBuffer = dil.DeclareLocal(typeof(byte[]));
            LocalBuilder buffer = dil.DeclareLocal(typeof(byte[]));
            //Read the length of the string's byte array
            EmitDeserializePrimitive<int>(dil);
            dil.Emit(OpCodes.Stloc, length);
            //Read the string
            getObject = typeof(UTF8EncodingProxy).GetMethod("GetString", new Type[] { typeof(byte[]), typeof(int) });
            dil.Emit(OpCodes.Ldloc, length);
            dil.Emit(OpCodes.Newarr, typeof(byte));
            dil.Emit(OpCodes.Stloc, buffer);
            dil.Emit(OpCodes.Ldarg_0);
            dil.Emit(OpCodes.Ldloc, buffer);
            dil.Emit(OpCodes.Ldc_I4, 0);
            dil.Emit(OpCodes.Ldloc, length);
            dil.Emit(OpCodes.Callvirt, readFromStream);
            dil.Emit(OpCodes.Pop);
            dil.Emit(OpCodes.Ldloc, buffer);
            dil.Emit(OpCodes.Ldc_I4, 0);
            dil.Emit(OpCodes.Call, getObject);
        }

        public static void EmitStringSerialze(ILGenerator sil)
        {
            MethodInfo writeOnStream = typeof(Stream).GetMethod("Write", new Type[] { typeof(byte[]), typeof(int), typeof(int) });
            MethodInfo toByteArray = typeof(UTF8EncodingProxy).GetMethod("GetBytes", new Type[] { typeof(string) });
            MethodInfo getLength = typeof(Array).GetMethod("GetLength", new Type[] { typeof(int) });
            LocalBuilder bytes = sil.DeclareLocal(typeof(byte[]));
            LocalBuilder lengthBytes = sil.DeclareLocal(typeof(byte[]));
            LocalBuilder length = sil.DeclareLocal(typeof(int));
            //Make a byte array from the string
            sil.Emit(OpCodes.Call, toByteArray);
            sil.Emit(OpCodes.Stloc, bytes);
            //Get it's length
            sil.Emit(OpCodes.Ldloc, bytes);
            sil.Emit(OpCodes.Ldc_I4, 0);
            sil.Emit(OpCodes.Callvirt, getLength);
            sil.Emit(OpCodes.Stloc, length);
            sil.Emit(OpCodes.Ldloc, length);
            //Write length of array
            EmitSerializePrimitive<int>(sil);
            //Write the serialized string
            sil.Emit(OpCodes.Ldarg_0);
            sil.Emit(OpCodes.Ldloc, bytes);
            sil.Emit(OpCodes.Ldc_I4, 0);
            sil.Emit(OpCodes.Ldloc, length);
            sil.Emit(OpCodes.Callvirt, writeOnStream);
        }


        public static void NullSerialize(Stream s)
        {
            byte[] bytes = BitConverter.GetBytes(-1);
            s.Write(bytes, 0, bytes.Length);
        }

        //létrehozza egy tuple-be a sorosító és visszaállító függvényeket
        private static Tuple<Delegate, Delegate> getSerializerTuple<T>()
        {
            string typename = typeof(T).AssemblyQualifiedName;                 //T típusát megállapítja
            Tuple<Delegate, Delegate> serializer;
            if (!genSerDict.TryGetValue(typename, out serializer))             //genSerDict.TryGetValue() akkor lesz igaz, ha tartalmazza a dictionary a paraméterként adott értéket az adott kulccsal
            {
                CreateSerializer<T>(typename);                                 //nincs, ez a típus még tárolva, létrehozza a sorosító szerelvényt
                if (!genSerDict.TryGetValue(typename, out serializer))         //ha ezután sem tárolja az értéket, akkor kivételt dob.
                {
                    throw new NotImplementedException();
                }
            }
            return serializer;                                                  //visszatér a sorosító és visszaállító függvényeket
        }

        //A függvény létrehozatja a sorosító metódust, majd meghívja azt
        public static void _Serialize<T>(Stream s, T o, bool checkType, int referenceId)
        {

            Tuple<Delegate, Delegate> serializer = getSerializerTuple<T>();     //getSerializerTuple adja a vissza a sorosító és visszaállító függvényeket
            serialize<T> serializeMethod = (serialize<T>)serializer.Item1;      //a sorosító függvényt tárolja a statikus változoként létrehozott delegate-be
            serializeMethod(s, o, checkType, referenceId);                      //meghívja a sorosító függvényt a delegate-ből
        }

        //"belépési pont", ezzel lehet példányosítani az osztályt
        public static void Serialize<T>(Stream s, T o)
        {
            foreach (var item in referenceStore) 
            {
                item.Value.Clear();
            }
            refCounter = 0;
            _Serialize<T>(s, o, true, -1);      //felkészül a példányosításra
        }

        public static T _Deserialize<T>(Stream s, int objectId)
        {
            Tuple<Delegate, Delegate> serializer = getSerializerTuple<T>();
            deserialize<T> deserializeMethod = (deserialize<T>)serializer.Item2;
            return deserializeMethod(s, objectId);
        }

        public static T Deserialize<T>(Stream s)
        {
            foreach (var item in objectStore)
            {
                item.Value.Clear();
            }
            return _Deserialize<T>(s, -1);
        }

        public static MethodInfo getSerializer()
        {
            //Console.WriteLine("S");
            return genSerType.GetMethod("_Serialize");
        }

        public static MethodInfo getDeserializer()
        {
            Console.WriteLine("D");
            return genSerType.GetMethod("_Deserialize");
        }
    }
}
