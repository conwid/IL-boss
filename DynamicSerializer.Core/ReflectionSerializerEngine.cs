using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DynamicSerializer.Core
{    
  
    public class ReflectionSerializerEngine
    {
        static ReflectionSerializerEngine()
        {
            propertycache = new Dictionary<string, PropertyInfo[]>();
            fieldcache = new Dictionary<string, FieldInfo[]>();
        }

        private static Dictionary<string, PropertyInfo[]> propertycache;
        private static Dictionary<string, FieldInfo[]> fieldcache;
        private static List<string> serializetypes;
        private static List<string> deserializetypes;
       
        
        private static bool IsAtomic(Type type)
        {            
            return ((type.IsPrimitive) || (type.IsEnum) || (type == typeof(string)) || (type==typeof(DateTime)));
        }
        private static bool IsGenericCollection(Type type)
        {
            return type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>));
        }
        private static bool IsGenericDictionary(Type type)
        {
            return ((type.IsGenericType) && (type.GetGenericTypeDefinition() == typeof(Dictionary<,>)));
        }
        private static bool IsNonGenericCollection(Type type)
        {
            Type t = type.GetInterface("ICollection");
            return (t!= null && !t.IsGenericType);
        }
        private static bool IsPropertyWritable(PropertyInfo p)
        {
            return ((p.CanWrite) && (p.GetSetMethod(true) != null));
        }
        private static bool IsFieldWritable(FieldInfo f)
        {
            return (!f.IsLiteral)&&(!f.IsInitOnly);
        }
        private static bool HasDefaultCtor(Type typeinfo)
        {
            return (typeinfo.GetConstructor(Type.EmptyTypes) != null);
        }

        public static void Serialize(Stream s, object graph)
        {
            serializetypes = new List<string>();
            FillFrequentTypes(serializetypes);
            GetTypes(serializetypes, graph);
            BinaryWriter bw = new BinaryWriter(s);
            bw.Write(serializetypes.Count-15);
            for (int i=15; i<serializetypes.Count; i++)
            {
                bw.Write(serializetypes[i]);
                
            }            
            innerSerialize(bw,graph);            
        }
        public static object DeSerialize(Stream s)
        {
            BinaryReader br = new BinaryReader(s);
            int typecount = br.ReadInt32();
            deserializetypes = new List<string>();
            FillFrequentTypes(deserializetypes);
            for (int i=0; i<typecount; i++)
            {
                deserializetypes.Add(br.ReadString());
            }
            return innerDeserialize(br);
        }

        private static void innerSerialize(BinaryWriter bw, object graph)
        {
            Type t = graph.GetType();            
            //Kiírjuk a típusindexet
            bw.Write(serializetypes.IndexOf(t.AssemblyQualifiedName));            
            //Ha atomi a típus...
            if (IsAtomic(t))
            {
                int typeindex = serializetypes.IndexOf(t.AssemblyQualifiedName);
                //...akkor közvetlenül sorosítunk a BinaryWriter megfelelő metódusával.
                switch (typeindex)
                {
                    case 0:
                        bw.Write((string)graph);
                        break;
                    case 1:
                        bw.Write((int) graph);
                        break;
                    #region...
                    case 2:
                        bw.Write((long)graph);
                        break;
                    case 3:
                        bw.Write((short)graph);
                        break;
                    case 4:
                        bw.Write((bool)graph);
                        break;
                    case 5:
                        bw.Write((double)graph);
                        break;
                    case 6:
                        bw.Write((byte)graph);
                        break;
                    case 7:
                        bw.Write((char)graph);
                        break;
                    case 8:
                        bw.Write((decimal)graph);
                        break;
                    case 9:
                        bw.Write((sbyte)graph);
                        break;
                    case 10:
                        bw.Write((float)graph);
                        break;
                    case 11:
                        bw.Write((uint)graph);
                        break;
                    case 12:
                        bw.Write((ushort)graph);
                        break;
                    case 13:
                        bw.Write((ulong)graph);
                        break;
       #endregion 
                    case 14:
                        bw.Write(((DateTime)graph).Ticks);
                        break;
                    default:
                        bw.Write((int)graph);
                        break;
                }                                                               
            }
            //Ha a típus generikus szótár...
            if (IsGenericDictionary(t))
            {
                //...akkor először meghatározzuk milyen típusú elemek alkotják design-time, hogy végig tudjunk lépkedni rajta
                Type generickeyvalue = typeof(KeyValuePair<,>).MakeGenericType(t.GetGenericArguments());
                //...kikeressük a metódust, ami megmondja hány elemünk van...
                MethodInfo counter =
                   (from result in
                        (from method in typeof(Enumerable).GetMethods() where method.Name == "Count" select method)
                    where result.GetParameters().Count() == 1
                    select result).Single().MakeGenericMethod(generickeyvalue);
                int dcount = (int)counter.Invoke(null, new[] { graph });
                bw.Write(dcount);
                //...kikeressük a metódust, amivel tudunk indexelni...
                MethodInfo dindexer =
                    (from method in (typeof(Enumerable).GetMethods()) where method.Name == "ElementAt" select method).
                        Single().MakeGenericMethod(generickeyvalue);
                   //...majd egyesével végiglépkedünk a szótáron...
                   for (int i = 0; i < dcount; i++)
                    {
                        //... és sorosítjuk először a kulcsot, majd az értéket.
                        object keyvaluepair = dindexer.Invoke(null, new[] { graph, i });
                        object key = keyvaluepair.GetType().GetProperty("Key").GetValue(keyvaluepair, null);
                        object value = keyvaluepair.GetType().GetProperty("Value").GetValue(keyvaluepair, null);
                        innerSerialize(bw, key);
                        innerSerialize(bw, value);
                    }         
                return;
            }                                         
            if (IsGenericCollection(t))
            {
                //Az egyéb generikus gyűjtemények esetén ugyanígy játunk el.
                #region...
                Type genarg =
                   t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>)).Single().GetGenericArguments()[0];
                MethodInfo counter =
                   (from result in
                        (from method in typeof(Enumerable).GetMethods() where method.Name == "Count" select method)
                    where result.GetParameters().Count() == 1
                    select result).Single().MakeGenericMethod(genarg);                
                int count = (int)counter.Invoke(null, new[] { graph });
                bw.Write(count);                
                
                MethodInfo indexer =
                    (from method in (typeof(Enumerable).GetMethods()) where method.Name == "ElementAt" select method).
                        Single().MakeGenericMethod(genarg);
                
                for (int i = 0; i < count; i++)
                {
                    innerSerialize(bw, indexer.Invoke(null, new[] { graph, i }));
                }
                return;
#endregion
            }
            if (IsNonGenericCollection(t))
            {
                
                return;
            }
            //Egyébként pedif valamilyen komplex típusról van szó...
            PropertyInfo[] properties;
          
                //...lekérjük a tulajdonságokat (és cachelünk, hogy valamivel gyorsabb legyen a módszer...
                if (!propertycache.ContainsKey(t.AssemblyQualifiedName))
                {
                    properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    //...rendezzük a tulajdonságokat név szerint...
                    Array.Sort(properties, (a, b) => a.Name.CompareTo(b.Name));
                    propertycache.Add(t.AssemblyQualifiedName, properties);
                }
                else
                {
                    properties = propertycache[t.AssemblyQualifiedName];
                }
            
            FieldInfo[] fields;
            
            //...hasonlóan járunk el mezők esetében is...    
            if (!fieldcache.ContainsKey(t.AssemblyQualifiedName))
                {
                    fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
                    Array.Sort(fields, (a, b) => a.Name.CompareTo(b.Name));
                    fieldcache.Add(t.AssemblyQualifiedName, fields);
                }
                else
                {
                    fields = fieldcache[t.AssemblyQualifiedName];
                }
            
            int propnum = properties.Length;
            int fieldnum = fields.Length;
            //...majd sorosítjuk az összes sorosítható tulajdonságot...
            for (int i = 0; i < propnum; i++)
            {
                if (IsPropertyWritable(properties[i]))
                    innerSerialize(bw, properties[i].GetValue(graph, null));
            }
            //... és mezőt.
            for (int i = 0; i < fieldnum; i++)
            {
                if (IsFieldWritable(fields[i]))
                    innerSerialize(bw, fields[i].GetValue(graph));
            }
        }
        private static object innerDeserialize(BinaryReader br)
        {            
            int typeindex = br.ReadInt32();            
            Type type = Type.GetType(deserializetypes[typeindex]);                        
            if (IsAtomic(type))
            {
                int index = deserializetypes.IndexOf(type.AssemblyQualifiedName);                
                switch (index)
                {
                    case 0:
                        return br.ReadString();                        
                    case 1:
                        return br.ReadInt32();
                    case 2:
                        return br.ReadInt64();
                    case 3:
                        return br.ReadInt16();
                    case 4:
                        return br.ReadBoolean();
                    case 5:
                        return br.ReadDouble();
                    case 6:
                        return br.ReadByte();
                    case 7:
                        return br.ReadChar();
                    case 8:
                        return br.ReadDecimal();
                    case 9:
                        return br.ReadSByte();
                    case 10:
                        return br.ReadSingle();
                    case 11:
                        return br.ReadUInt32();
                    case 12:
                        return br.ReadUInt16();
                    case 13:
                        return br.ReadUInt64();
                    case 14:
                        return new DateTime(br.ReadInt64());
                    default:
                        return br.ReadInt32();                        
                }                      
            }
            if (IsGenericDictionary(type))
            {
                int count = br.ReadInt32();
                object dict = Activator.CreateInstance(type);
                MethodInfo addmethod = type.GetMethod("Add");
                for (int i = 0; i < count; i++)
                {
                    addmethod.Invoke(dict, new[] { innerDeserialize(br),innerDeserialize(br) });
                }
                return dict;
            }
            if (IsGenericCollection(type))
            {
                int count = br.ReadInt32();                
                if (type.IsArray)
                {
                   Type genarg= type.GetInterfaces().Where(
                        i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof (ICollection<>)).Single();
                    Type listtype = typeof (List<>).MakeGenericType(genarg.GetGenericArguments());
                    object list = Activator.CreateInstance(listtype);
                    MethodInfo addmethod = listtype.GetMethod("Add");
                    for (int i = 0; i < count; i++)
                    {
                        addmethod.Invoke(list, new[] { innerDeserialize(br) });
                    }
                    MethodInfo tolistmethod = listtype.GetMethod("ToArray");
                    return tolistmethod.Invoke(list, null);       
                }
                else
                {
                   object list = Activator.CreateInstance(type);
                   MethodInfo addmethod = type.GetMethod("Add");
                   for (int i = 0; i < count; i++)
                   {
                       addmethod.Invoke(list, new[] { innerDeserialize(br) });
                   }
                   return list;       
                }                         
            }
            if (IsNonGenericCollection(type))
            {                
                return null;
            }
            object complex = Activator.CreateInstance(type);
            PropertyInfo[] properties;
            
                if (!propertycache.ContainsKey(type.AssemblyQualifiedName))
                {
                    properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    Array.Sort(properties, (a, b) => a.Name.CompareTo(b.Name));
                    propertycache.Add(type.AssemblyQualifiedName, properties);
                }
                else
                {
                    properties = propertycache[type.AssemblyQualifiedName];
                }
            
            FieldInfo[] fields;
            
                if (!fieldcache.ContainsKey(type.AssemblyQualifiedName))
                {
                    fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                    Array.Sort(fields, (a, b) => a.Name.CompareTo(b.Name));
                    fieldcache.Add(type.AssemblyQualifiedName, fields);
                }
                else
                {
                    fields = fieldcache[type.AssemblyQualifiedName];
                }
            
            int propnum = properties.Length;
            int fieldnum = fields.Length;
            for (int i = 0; i < propnum; i++)
            {
                if (IsPropertyWritable(properties[i]))
                {
                    properties[i].SetValue(complex, innerDeserialize(br), null); 
                }                    
            }
            for (int i = 0; i < fieldnum; i++)
            {
                if (IsFieldWritable(fields[i]))
                {
                    fields[i].SetValue(complex, innerDeserialize(br));
                }                    
            }
            return complex;
        }                
        private static void FillFrequentTypes(List<string> l)
        {
            l.Add(typeof(string).AssemblyQualifiedName);
            l.Add(typeof(int).AssemblyQualifiedName);
            l.Add(typeof(long).AssemblyQualifiedName);
            l.Add(typeof(short).AssemblyQualifiedName);
            l.Add(typeof(bool).AssemblyQualifiedName);
            l.Add(typeof(double).AssemblyQualifiedName);
            l.Add(typeof(byte).AssemblyQualifiedName);
            l.Add(typeof(char).AssemblyQualifiedName);
            l.Add(typeof(decimal).AssemblyQualifiedName);
            l.Add(typeof(sbyte).AssemblyQualifiedName);
            l.Add(typeof(float).AssemblyQualifiedName);
            l.Add(typeof(uint).AssemblyQualifiedName);
            l.Add(typeof(ulong).AssemblyQualifiedName);
            l.Add(typeof(ushort).AssemblyQualifiedName);
            l.Add(typeof(DateTime).AssemblyQualifiedName);
            TextWriter tw = new StreamWriter("types.txt");
            int i = 0;
            foreach (string s in l)
            {
                tw.WriteLine("{0}: {1}", i, s);
                i++;
            }
            tw.Close();
        }
        private static void GetTypes(List<string> types, object graph)
        {
            Type t = graph.GetType();            
            if (!types.Contains(t.AssemblyQualifiedName))
            {
                types.Add(t.AssemblyQualifiedName);   
            }            
            if (IsAtomic(t)) return;
            if (IsGenericDictionary(t))
            {                
                Type generickeyvalue = typeof (KeyValuePair<,>).MakeGenericType(t.GetGenericArguments());
                MethodInfo dcounter =
                   (from result in
                        (from method in typeof(Enumerable).GetMethods() where method.Name == "Count" select method)
                    where result.GetParameters().Count() == 1
                    select result).Single().MakeGenericMethod(generickeyvalue);
                int dcount = (int)dcounter.Invoke(null, new[] { graph });
                
                MethodInfo dindexer =
                    (from method in (typeof (Enumerable).GetMethods()) where method.Name == "ElementAt" select method).
                        Single().MakeGenericMethod(generickeyvalue);
                for (int i = 0; i < dcount; i++)
                {
                    object keyvaluepair = dindexer.Invoke(null, new[] {graph, i});
                    object key = keyvaluepair.GetType().GetProperty("Key").GetValue(keyvaluepair, null);
                    object value = keyvaluepair.GetType().GetProperty("Value").GetValue(keyvaluepair, null);
                    GetTypes(types,key );
                    GetTypes(types, value);                    
                }         
                ///*
                //IDictionary objectvalue = (IDictionary)graph;                    
                //foreach (var key in objectvalue.Keys)
                //{
                //    GetTypes(types, key);
                //    GetTypes(types, objectvalue[key]);
                //}
                // */
               return;

            } 
            if (IsGenericCollection(t))
            {                
                Type genarg =
                   t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof (ICollection<>)).Single().GetGenericArguments()[0];
                MethodInfo counter =
                    (from result in
                         (from method in typeof (Enumerable).GetMethods() where method.Name == "Count" select method)
                     where result.GetParameters().Count() == 1
                     select result).Single().MakeGenericMethod(genarg);
                int count =(int) counter.Invoke(null, new [] {graph});

                MethodInfo indexer =
                    (from method in (typeof(Enumerable).GetMethods()) where method.Name == "ElementAt" select method).
                        Single().MakeGenericMethod(genarg);                
                for (int i = 0; i < count; i++)
                {
                    GetTypes(types, indexer.Invoke(null, new[] {graph,i}));                    
                }               
                return;
            }
            if (IsNonGenericCollection(t))
            {              
                return;
            }
            PropertyInfo[] properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);                
            int propnum = properties.Length;
            int fieldnum = fields.Length;
            for (int i=0; i<propnum; i++)
            {                
                if (IsPropertyWritable(properties[i]))
                    GetTypes(types, properties[i].GetValue(graph,null));
            }
            for (int i=0; i<fieldnum; i++)
            {
                if (IsFieldWritable(fields[i]))
                    GetTypes(types, fields[i].GetValue(graph));
            }
        }
    }
}
