using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Runtime.Serialization;

namespace DynamicSerializer.Core
{


    public class SmartBinaryReader : BinaryReader
    {
        public SmartBinaryReader( Stream s ) : base( s )
        {

        }
        public override int ReadInt32()
        {
            var currentByte = (uint)base.ReadByte();
            byte read = 1;
            uint result = currentByte & 0x7FU;
            int shift = 7;
            while( (currentByte & 0x80) != 0 )
            {
                currentByte = (uint)base.ReadByte();
                read++;
                result |= (currentByte & 0x7FU) << shift;
                shift += 7;
                if( read > 5 )
                {
                    throw new InvalidOperationException( "Invalid integer value in the input stream." );
                }
            }
            return (int)((-(result & 1)) ^ ((result >> 1) & 0x7FFFFFFFU));
        }
        public override long ReadInt64()
        {
            var value = (uint)base.ReadByte();
            byte read = 1;
            ulong result = value & 0x7FUL;
            int shift = 7;
            while( (value & 0x80) != 0 )
            {
                value = (uint)base.ReadByte();
                read++;
                result |= (value & 0x7FUL) << shift;
                shift += 7;
                if( read > 10 )
                {
                    throw new InvalidOperationException( "Invalid integer long in the input stream." );
                }
            }
            var tmp = unchecked((long)result);
            return (-(tmp & 0x1L)) ^ ((tmp >> 1) & 0x7FFFFFFFFFFFFFFFL);
        }
        public DateTime ReadDateTime()
        {
            return new DateTime( this.ReadInt64() );
        }
    }


    public class SmartBinaryWriter : BinaryWriter
    {
        public SmartBinaryWriter() : base()
        {

        }
        public SmartBinaryWriter( Stream s ) : base( s )
        {

        }

        public override void Write( int value )
        {
            var zigZagEncoded = unchecked((uint)((value << 1) ^ (value >> 31)));
            while( (zigZagEncoded & ~0x7F) != 0 )
            {
                base.Write( (byte)((zigZagEncoded | 0x80) & 0xFF) );
                zigZagEncoded >>= 7;
            }
            base.Write( (byte)zigZagEncoded );
        }

        public override void Write( long value )
        {
            var zigZagEncoded = unchecked((ulong)((value << 1) ^ (value >> 63)));
            while( (zigZagEncoded & ~0x7FUL) != 0 )
            {
                base.Write( (byte)((zigZagEncoded | 0x80) & 0xFF) );
                zigZagEncoded >>= 7;
            }
            base.Write( (byte)zigZagEncoded );
        }

        public void Write( DateTime value )
        {
            this.Write( value.Ticks );
        }
    }

    public class DynamicSerializerEngine
    {
        public static List<string> types = new List<string>();
        public static Dictionary<Type, MethodBuilder> knowntypes = new Dictionary<Type, MethodBuilder>();
        public static Dictionary<Type, MethodBuilder> knowntypes_deserializers = new Dictionary<Type, MethodBuilder>();
        public static Dictionary<Type, TypeBuilder> knowntypes_deserializers_typebuilders = new Dictionary<Type, TypeBuilder>();

        public static Action<object, SmartBinaryWriter> nextserializer;
        public static Action<object, SmartBinaryWriter> firstserializer = null;
        public static Func<object, SmartBinaryReader> firstdeserializer = null;
        private static AppDomain ad = AppDomain.CurrentDomain;
        private static AssemblyName an = new AssemblyName( "SerializerAssembly" );
        private static AssemblyName dan = new AssemblyName( "DeSerializerAssembly" );
        private static AssemblyBuilder builder = ad.DefineDynamicAssembly( an, AssemblyBuilderAccess.RunAndSave );
        private static AssemblyBuilder debuilder = ad.DefineDynamicAssembly( dan, AssemblyBuilderAccess.RunAndSave );
        private static ModuleBuilder mb = builder.DefineDynamicModule( "DynModule", "SerializerAssembly.dll" );
        private static ModuleBuilder deserializermb = debuilder.DefineDynamicModule( "DynModule", "DeSerializerAssembly.dll" );
        private static int iterate = -1;

        private static TypeBuilder deserializerdispatchertype = deserializermb.DefineType( "DeSerializerDispatcher", TypeAttributes.Public | TypeAttributes.Class );


        #region TypeInfo for used types

        public static Type stringtype = typeof( string );
        public static Type inttype = typeof( int );
        public static Type longtype = typeof( long );
        public static Type shorttype = typeof( short );
        public static Type booltype = typeof( bool );
        public static Type doubletype = typeof( double );
        public static Type bytetype = typeof( byte );
        public static Type chartype = typeof( char );
        public static Type decimaltype = typeof( decimal );
        public static Type sbytetype = typeof( sbyte );
        public static Type floattype = typeof( float );
        public static Type uinttype = typeof( uint );
        public static Type ulongtype = typeof( ulong );
        public static Type ushorttype = typeof( ushort );
        public static Type datetimetype = typeof( DateTime );


        private static List<Type> atomictypes = new List<Type>
                                                    {
                                                        stringtype,
                                                        inttype,
                                                        longtype,
                                                        shorttype,
                                                        booltype,
                                                        doubletype,
                                                        bytetype,
                                                        chartype,
                                                        decimaltype,
                                                        sbytetype,
                                                        floattype,
                                                        uinttype,
                                                        ulongtype,
                                                        ushorttype,
                                                        datetimetype
                                                    };

        private static List<string> typefieldnames = new List<string>
                                                         {
                                                             "stringtype",
                                                             "inttype",
                                                             "longtype",
                                                             "shorttype",
                                                             "booltype",
                                                             "doubletype",
                                                             "bytetype",
                                                             "chartype",
                                                             "decimaltype",
                                                             "sbytetype",
                                                             "floattype",
                                                             "uinttype",
                                                             "ulongtype",
                                                             "ushorttype",
                                                             "datetimetype"
                                                         };

        private static Type objecttype = typeof( object );
        private static Type typetype = typeof( Type );
        private static Type actiontype = typeof( Action<object, SmartBinaryWriter> );
        private static Type dynamicserializertype = typeof( DynamicSerializerEngine );
        private static Type binarywritertype = typeof( SmartBinaryWriter );
        private static Type genericcollectiontype = typeof( ICollection<> );
        private static Type genericenumtype = typeof( IEnumerable<> );
        private static Type genericenumeratortype = typeof( IEnumerator<> );
        private static Type keyvaluetype = typeof( KeyValuePair<,> );
        private static Type voidtype = typeof( void );
        private static Type binaryreadertype = typeof( SmartBinaryReader );
        private static Type dynamicSerializerEngineType = typeof( DynamicSerializerEngine );
        private static Type objectListType = typeof( List<object> );
        private static Type intObjectDictType = typeof( Dictionary<int, object> );

        #endregion

        #region MethodInfo for called methods

        private static MethodInfo gettype = objecttype.GetMethod( "GetType" );
        private static MethodInfo addmethod = types.GetType().GetMethod( "Add", new[] { stringtype } );
        private static MethodInfo asmqualifiednamegetter = typetype.GetProperty( "AssemblyQualifiedName" ).GetGetMethod();
        private static MethodInfo typeserializermethod = dynamicserializertype.GetMethod( "CreateTypeSerializer" );
        private static MethodInfo dispatchermethod = dynamicserializertype.GetMethod( "CreateSerializerDispatcher" );

        private static MethodInfo runtimetypehandlemethod = typetype.GetMethod( "GetTypeFromHandle",
                                                                               new[] { typeof( RuntimeTypeHandle ) } );


        private static MethodInfo movenextmethod = typeof( IEnumerator ).GetMethod( "MoveNext",
                                                                                  BindingFlags.Public |
                                                                                  BindingFlags.Instance );

        private static MethodInfo stringwriter = binarywritertype.GetMethod( "Write", new[] { stringtype } );
        private static MethodInfo intwriter = binarywritertype.GetMethod( "Write", new[] { inttype } );
        private static MethodInfo longwriter = binarywritertype.GetMethod( "Write", new[] { longtype } );
        private static MethodInfo shortwriter = binarywritertype.GetMethod( "Write", new[] { shorttype } );
        private static MethodInfo boolwriter = binarywritertype.GetMethod( "Write", new[] { booltype } );
        private static MethodInfo doublewriter = binarywritertype.GetMethod( "Write", new[] { doubletype } );
        private static MethodInfo bytewriter = binarywritertype.GetMethod( "Write", new[] { bytetype } );
        private static MethodInfo charwriter = binarywritertype.GetMethod( "Write", new[] { chartype } );
        private static MethodInfo decimalwriter = binarywritertype.GetMethod( "Write", new[] { decimaltype } );
        private static MethodInfo sbytewriter = binarywritertype.GetMethod( "Write", new[] { sbytetype } );
        private static MethodInfo floatwriter = binarywritertype.GetMethod( "Write", new[] { floattype } );
        private static MethodInfo uintwriter = binarywritertype.GetMethod( "Write", new[] { uinttype } );
        private static MethodInfo ulongwriter = binarywritertype.GetMethod( "Write", new[] { ulongtype } );
        private static MethodInfo ushortwriter = binarywritertype.GetMethod( "Write", new[] { ushorttype } );
        private static MethodInfo dateTimeWriter = binaryreadertype.GetMethod( "Write", new[] { datetimetype } );

        private static MethodInfo intreader = binaryreadertype.GetMethod( "ReadInt32" );
        private static MethodInfo stringreader = binaryreadertype.GetMethod( "ReadString" );
        private static MethodInfo longreader = binaryreadertype.GetMethod( "ReadInt64" );
        private static MethodInfo shortreader = binaryreadertype.GetMethod( "ReadInt16" );
        private static MethodInfo boolreader = binaryreadertype.GetMethod( "ReadBoolean" );
        private static MethodInfo doublereader = binaryreadertype.GetMethod( "ReadDouble" );
        private static MethodInfo bytereader = binaryreadertype.GetMethod( "ReadByte" );
        private static MethodInfo charreader = binaryreadertype.GetMethod( "ReadChar" );
        private static MethodInfo decimalreader = binaryreadertype.GetMethod( "ReadDecimal" );
        private static MethodInfo sbytereader = binaryreadertype.GetMethod( "ReadSByte" );
        private static MethodInfo floatreader = binaryreadertype.GetMethod( "ReadSingle" );
        private static MethodInfo uintreader = binaryreadertype.GetMethod( "ReadUInt32" );
        private static MethodInfo ulongreader = binaryreadertype.GetMethod( "ReadUInt64" );
        private static MethodInfo ushortreader = binaryreadertype.GetMethod( "ReadUInt16" );
        private static MethodInfo dateTimeReader = binaryreadertype.GetMethod( "ReadDateTime" );

        #endregion

        private static List<MethodInfo> atomictypewriters = new List<MethodInfo>
                                                                {
                                                                    stringwriter,
                                                                    intwriter,
                                                                    longwriter,
                                                                    shortwriter,
                                                                    boolwriter,
                                                                    doublewriter,
                                                                    bytewriter,
                                                                    charwriter,
                                                                    decimalwriter,
                                                                    sbytewriter,
                                                                    floatwriter,
                                                                    uintwriter,
                                                                    ulongwriter,
                                                                    ushortwriter,
                                                                    longwriter
                                                                };

        private static List<MethodInfo> atomictypereaders = new List<MethodInfo>
                                                                {
                                                                    stringreader,
                                                                    intreader,
                                                                    longreader,
                                                                    shortreader,
                                                                    boolreader,
                                                                    doublereader,
                                                                    bytereader,
                                                                    charreader,
                                                                    decimalreader,
                                                                    sbytereader,
                                                                    floatreader,
                                                                    uintreader,
                                                                    ulongreader,
                                                                    ushortreader,
                                                                    longreader
                                                                };

        private static Dictionary<Type, MethodInfo> writerdict = new Dictionary<Type, MethodInfo>()
                                                                     {
                                                                         {stringtype, stringwriter},
                                                                         {inttype, intwriter},
                                                                         {longtype, longwriter},
                                                                         {shorttype, shortwriter},
                                                                         {booltype, boolwriter},
                                                                         {doubletype, doublewriter},
                                                                         {bytetype, bytewriter},
                                                                         {chartype, charwriter},
                                                                         {decimaltype, decimalwriter},
                                                                         {sbytetype, sbytewriter},
                                                                         {floattype, floatwriter},
                                                                         {uinttype, uintwriter},
                                                                         {ulongtype, ulongwriter},
                                                                         {ushorttype, ushortwriter},
                                                                         {datetimetype, ulongwriter}
                                                                     };

        private static Dictionary<Type, MethodInfo> readerdict = new Dictionary<Type, MethodInfo>()
                                                                     {
                                                                         {stringtype, stringreader},
                                                                         {inttype, intreader},
                                                                         {longtype, longreader},
                                                                         {shorttype, shortreader},
                                                                         {booltype, boolreader},
                                                                         {doubletype, doublereader},
                                                                         {bytetype, bytereader},
                                                                         {chartype, charreader},
                                                                         {decimaltype, decimalreader},
                                                                         {sbytetype, sbytereader},
                                                                         {floattype, floatreader},
                                                                         {uinttype, uintreader},
                                                                         {ulongtype, ulongreader},
                                                                         {ushorttype, ushortreader},
                                                                         {datetimetype, ulongreader}
                                                                     };

        #region FieldInfo for used fields

        private static FieldInfo typesfield = typeof( DynamicSerializerEngine ).GetField( "types",
                                                                                  BindingFlags.Static |
                                                                                  BindingFlags.Public );

        #endregion        

        private static FieldInfo nextserializerfield = dynamicserializertype.GetField( "nextserializer",
                                                                                      BindingFlags.Static |
                                                                                      BindingFlags.Public );

        private static MethodInfo invokemethod = actiontype.GetMethod( "Invoke", new[] { objecttype, binarywritertype } );

        public static List<object> references;

        private static FieldInfo referencesFieldInfo = dynamicSerializerEngineType.GetField( nameof( references ),
            BindingFlags.Static | BindingFlags.Public );

        private static MethodInfo listIndexOf = objectListType.GetMethod( "IndexOf", new[] { objecttype } );
        private static MethodInfo listAdd = objectListType.GetMethod( "Add", new[] { objecttype } );
        private static MethodInfo listContains = objectListType.GetMethod( "Contains", new[] { objecttype } );
        private static MethodInfo listIndexer = objectListType.GetProperty( "Item", objecttype ).GetGetMethod();

        private static MethodInfo getTypeFromString = typetype.GetMethod( "GetType", new[] { stringtype } );

        private static MethodInfo formatterServiceGetUninitialized =
            typeof( FormatterServices ).GetMethod( "GetUninitializedObject", new[] { typetype } );

        public static Dictionary<int, object> referenceDict;
        private static FieldInfo referenceDictFieldInfo = dynamicSerializerEngineType.GetField( nameof( referenceDict ), BindingFlags.Static | BindingFlags.Public );

        private static MethodInfo dictIndexer = intObjectDictType.GetProperty( "Item" ).GetGetMethod();
        private static MethodInfo dictAdd = intObjectDictType.GetMethod( "Add" );
        private static MethodInfo dictContains = intObjectDictType.GetMethod( "ContainsKey" );

        public static int referenceCounter;
        private static FieldInfo referenceCounterFieldInfo = dynamicSerializerEngineType.GetField( nameof( referenceCounter ), BindingFlags.Static | BindingFlags.Public );


        public static ObjectIDGenerator oidGenerator;
        private static FieldInfo oidGeneratorFieldInfo = dynamicSerializerEngineType.GetField( nameof( oidGenerator ),
           BindingFlags.Static | BindingFlags.Public );

        private static MethodInfo castLongToInt32 = typeof( Convert ).GetMethod( nameof( Convert.ToInt32 ), new[] { longtype } );

        private static Type objectIdGeneratorType = typeof( ObjectIDGenerator );
        private static MethodInfo oidGenGetId = objectIdGeneratorType.GetMethod( "GetId" );
        private static MethodInfo oidGenHasId = objectIdGeneratorType.GetMethod( "HasId" );









        #region Serialization Methods        


        public static void CreateTypeSerializer( Type t )
        {
            if( knowntypes.ContainsKey( t ) )
            {
                return;
            }
            string myname = t.Name;
            if( t.IsGenericDictionary() )
            {
                var temp = t.GetGenericArguments();
                myname += temp[0].Name + "_" + temp[1].Name;
            }
            else if( t.IsGenericCollection() )
            {
                if( !t.IsArray )
                {
                    myname += t.GetGenericArguments()[0].Name;
                }
            }
            TypeBuilder typeserializer = mb.DefineType( "Serialize" + myname,
                                                       TypeAttributes.Public | TypeAttributes.Class );
            MethodBuilder typeserializermethod = typeserializer.DefineMethod( "SerializeType" + myname,
                                                                             MethodAttributes.Public |
                                                                             MethodAttributes.Static, voidtype,
                                                                             new[] { t, binarywritertype } );
            knowntypes.Add( t, typeserializermethod );
            ILGenerator gen = typeserializermethod.GetILGenerator();

            gen.Emit( OpCodes.Ldarg_1 );
            gen.Emit( OpCodes.Ldc_I4, types.TypeIndex( t.AssemblyQualifiedName ) + 15 );
            gen.Emit( OpCodes.Callvirt, intwriter );

            if( t.IsGenericDictionary() )
            {
                Type[] genargs = t.GetGenericArguments();
                Type keytype = genargs[0];
                Type valuetype = genargs[1];
                Type componenttype = keyvaluetype.MakeGenericType( genargs );
                Type collectiontype = genericcollectiontype.MakeGenericType( componenttype );

                gen.Emit( OpCodes.Ldarg_1 );
                gen.Emit( OpCodes.Ldarg_0 );
                gen.Emit( OpCodes.Castclass, collectiontype );
                gen.Emit( OpCodes.Callvirt, collectiontype.GetProperty( "Count" ).GetGetMethod() );
                gen.Emit( OpCodes.Callvirt, intwriter );

                Label beginwhile = gen.DefineLabel();
                Label endwhile = gen.DefineLabel();

                gen.Emit( OpCodes.Ldarg_0 );
                gen.Emit( OpCodes.Castclass, collectiontype );
                gen.Emit( OpCodes.Callvirt,
                         genericenumtype.MakeGenericType( componenttype ).GetMethod( "GetEnumerator",
                                                                                  BindingFlags.Public |
                                                                                  BindingFlags.Instance ) );
                Type enumeratortype = genericenumeratortype.MakeGenericType( componenttype );
                LocalBuilder enumerator = gen.DeclareLocal( enumeratortype );
                gen.Emit( OpCodes.Stloc, enumerator );
                gen.Emit( OpCodes.Br, endwhile );
                gen.MarkLabel( beginwhile );
                LocalBuilder temp = gen.DeclareLocal( componenttype );

                MethodInfo currentgetter = enumeratortype.GetProperty( "Current", BindingFlags.Public | BindingFlags.Instance ).GetGetMethod();
                gen.Emit( OpCodes.Ldloc, enumerator );
                gen.Emit( OpCodes.Callvirt, currentgetter );
                gen.Emit( OpCodes.Stloc, temp );
                if( keytype.IsAtomic() )
                {

                    MethodInfo keygetter = componenttype.GetProperty( "Key" ).GetGetMethod();

                    if( keytype == datetimetype )
                    {
                        gen.Emit( OpCodes.Ldarg_1 );
                        gen.Emit( OpCodes.Ldloca, temp );
                        gen.Emit( OpCodes.Call, componenttype.GetProperty( "Key" ).GetGetMethod() );

                        gen.Emit( OpCodes.Call, datetimetype.GetProperty( "Ticks" ).GetGetMethod() );
                        gen.Emit( OpCodes.Callvirt, longwriter );
                    }
                    else if( keytype.IsEnum )
                    {
                        gen.Emit( OpCodes.Ldarg_1 );
                        gen.Emit( OpCodes.Ldloca, temp );
                        gen.Emit( OpCodes.Call, componenttype.GetProperty( "Key" ).GetGetMethod() );

                        gen.Emit( OpCodes.Callvirt, intwriter );
                    }
                    else
                    {
                        gen.Emit( OpCodes.Ldarg_1 );
                        gen.Emit( OpCodes.Ldloca, temp );
                        gen.Emit( OpCodes.Call, componenttype.GetProperty( "Key" ).GetGetMethod() );
                        gen.Emit( OpCodes.Callvirt, writerdict[keytype] );
                    }
                }
                else
                {

                    gen.Emit( OpCodes.Ldloca, temp );
                    gen.Emit( OpCodes.Callvirt, componenttype.GetProperty( "Key" ).GetGetMethod() );

                    LocalBuilder current = gen.DeclareLocal( keytype );
                    gen.Emit( OpCodes.Stloc, current );

                    gen.Emit( OpCodes.Ldsfld, nextserializerfield );
                    gen.Emit( OpCodes.Ldloc, current );
                    gen.Emit( OpCodes.Ldarg_1 );
                    gen.Emit( OpCodes.Callvirt, invokemethod );
                }

                if( valuetype.IsAtomic() )
                {

                    MethodInfo valuegetter = componenttype.GetProperty( "Value" ).GetGetMethod();

                    if( valuetype == stringtype )
                    {
                        gen.Emit( OpCodes.Ldarg_1 );

                        gen.Emit( OpCodes.Ldloca, temp );
                        gen.Emit( OpCodes.Call, valuegetter );

                        Label ifnotnull = gen.DefineLabel();
                        Label endif = gen.DefineLabel();
                        gen.Emit( OpCodes.Ldnull );
                        gen.Emit( OpCodes.Ceq );
                        gen.Emit( OpCodes.Brfalse, ifnotnull );
                        gen.Emit( OpCodes.Ldarg_1 );
                        gen.Emit( OpCodes.Ldc_I4, -1 );
                        gen.Emit( OpCodes.Callvirt, intwriter );
                        gen.Emit( OpCodes.Br, endif );
                        gen.MarkLabel( ifnotnull );
                        gen.Emit( OpCodes.Ldarg_1 );
                        gen.Emit( OpCodes.Ldc_I4, 0 );
                        gen.Emit( OpCodes.Callvirt, intwriter );
                        gen.Emit( OpCodes.Ldarg_1 );

                        gen.Emit( OpCodes.Ldloca, temp );
                        gen.Emit( OpCodes.Call, valuegetter );
                        gen.Emit( OpCodes.Callvirt, stringwriter );
                        gen.MarkLabel( endif );

                    }
                    else if( valuetype == datetimetype )
                    {
                        gen.Emit( OpCodes.Ldarg_1 );

                        gen.Emit( OpCodes.Ldloca, temp );
                        gen.Emit( OpCodes.Call, valuegetter );

                        gen.Emit( OpCodes.Call, datetimetype.GetProperty( "Ticks" ).GetGetMethod() );
                        gen.Emit( OpCodes.Callvirt, longwriter );
                    }
                    else if( valuetype.IsEnum )
                    {
                        gen.Emit( OpCodes.Ldarg_1 );

                        gen.Emit( OpCodes.Ldloca, temp );
                        gen.Emit( OpCodes.Call, valuegetter );

                        gen.Emit( OpCodes.Callvirt, intwriter );
                    }
                    else
                    {
                        gen.Emit( OpCodes.Ldarg_1 );

                        gen.Emit( OpCodes.Ldloca, temp );
                        gen.Emit( OpCodes.Call, valuegetter );

                        gen.Emit( OpCodes.Callvirt, writerdict[valuetype] );
                    }
                }
                else
                {

                    gen.Emit( OpCodes.Ldloca, temp );
                    gen.Emit( OpCodes.Callvirt, componenttype.GetProperty( "Value" ).GetGetMethod() );

                    LocalBuilder current = gen.DeclareLocal( valuetype );
                    gen.Emit( OpCodes.Stloc, current );

                    gen.Emit( OpCodes.Ldsfld, nextserializerfield );
                    gen.Emit( OpCodes.Ldloc, current );
                    gen.Emit( OpCodes.Ldarg_1 );
                    gen.Emit( OpCodes.Callvirt, invokemethod );
                }

                gen.MarkLabel( endwhile );
                gen.Emit( OpCodes.Ldloc, enumerator );
                gen.Emit( OpCodes.Callvirt, movenextmethod );
                gen.Emit( OpCodes.Brtrue, beginwhile );


            }
            else if( t.IsGenericCollection() )
            {
                Type componenttype;
                if( t.IsArray )
                {
                    componenttype = t.GetElementType();
                }
                else
                {
                    componenttype = t.GetGenericArguments()[0];
                }
                Type collectiontype = genericcollectiontype.MakeGenericType( componenttype );
                gen.Emit( OpCodes.Ldarg_1 );
                gen.Emit( OpCodes.Ldarg_0 );
                gen.Emit( OpCodes.Callvirt, collectiontype.GetProperty( "Count" ).GetGetMethod() );
                gen.Emit( OpCodes.Callvirt, intwriter );

                Label beginwhile = gen.DefineLabel();
                Label endwhile = gen.DefineLabel();
                gen.Emit( OpCodes.Ldarg_0 );
                gen.Emit( OpCodes.Callvirt,
                         genericenumtype.MakeGenericType( componenttype ).GetMethod( "GetEnumerator",
                                                                                  BindingFlags.Public |
                                                                                  BindingFlags.Instance ) );
                Type enumeratortype = genericenumeratortype.MakeGenericType( componenttype );
                LocalBuilder enumerator = gen.DeclareLocal( enumeratortype );
                gen.Emit( OpCodes.Stloc, enumerator );
                gen.Emit( OpCodes.Br, endwhile );
                gen.MarkLabel( beginwhile );
                if( componenttype.IsAtomic() )
                {
                    MethodInfo currentgetter = enumeratortype.GetProperty( "Current", BindingFlags.Public | BindingFlags.Instance ).
                                     GetGetMethod();
                    if( componenttype == stringtype )
                    {
                        gen.Emit( OpCodes.Ldloc, enumerator );
                        gen.Emit( OpCodes.Callvirt, currentgetter );

                        Label ifnotnull = gen.DefineLabel();
                        Label endif = gen.DefineLabel();
                        gen.Emit( OpCodes.Ldnull );
                        gen.Emit( OpCodes.Ceq );
                        gen.Emit( OpCodes.Brfalse, ifnotnull );
                        gen.Emit( OpCodes.Ldarg_1 );
                        gen.Emit( OpCodes.Ldc_I4, -1 );
                        gen.Emit( OpCodes.Callvirt, intwriter );
                        gen.Emit( OpCodes.Br, endif );
                        gen.MarkLabel( ifnotnull );
                        gen.Emit( OpCodes.Ldarg_1 );
                        gen.Emit( OpCodes.Ldc_I4, 0 );
                        gen.Emit( OpCodes.Callvirt, intwriter );
                        gen.Emit( OpCodes.Ldarg_1 );
                        gen.Emit( OpCodes.Ldloc, enumerator );
                        gen.Emit( OpCodes.Callvirt, currentgetter );
                        gen.Emit( OpCodes.Callvirt, stringwriter );
                        gen.MarkLabel( endif );
                    }
                    if( componenttype.IsEnum )
                    {
                        gen.Emit( OpCodes.Ldarg_1 );
                        gen.Emit( OpCodes.Ldloc, enumerator );
                        gen.Emit( OpCodes.Callvirt, currentgetter );

                        gen.Emit( OpCodes.Callvirt, intwriter );
                    }
                    else
                    {
                        gen.Emit( OpCodes.Ldarg_1 );
                        gen.Emit( OpCodes.Ldloc, enumerator );
                        gen.Emit( OpCodes.Callvirt, currentgetter );

                        gen.Emit( OpCodes.Callvirt, writerdict[componenttype] );
                    }
                }
                else
                {
                    gen.Emit( OpCodes.Ldloc, enumerator );
                    gen.Emit( OpCodes.Callvirt,
                             enumeratortype.GetProperty( "Current", BindingFlags.Public | BindingFlags.Instance ).
                                 GetGetMethod() );
                    LocalBuilder current = gen.DeclareLocal( componenttype );
                    gen.Emit( OpCodes.Stloc, current );
                    gen.Emit( OpCodes.Ldsfld, nextserializerfield );
                    gen.Emit( OpCodes.Ldloc, current );
                    gen.Emit( OpCodes.Ldarg_1 );
                    gen.Emit( OpCodes.Callvirt, invokemethod );

                }
                gen.MarkLabel( endwhile );
                gen.Emit( OpCodes.Ldloc, enumerator );
                gen.Emit( OpCodes.Callvirt, movenextmethod );
                gen.Emit( OpCodes.Brtrue, beginwhile );
            }
            else if( t.IsNonGenericCollection() )
            {

            }
            else
            {
                PropertyInfo[] pi = t.GetProperties();
                Array.Sort( pi, ( a, b ) => a.Name.CompareTo( b.Name ) );
                int propcount = pi.Length;
                LocalBuilder toserialize = gen.DeclareLocal( t );
                gen.Emit( OpCodes.Ldarg_0 );
                gen.Emit( OpCodes.Stloc, toserialize );
                for( int i = 0; i < propcount; i++ )
                {
                    if( (pi[i]).IsPropertyWritable() )
                    {
                        MethodInfo getter = pi[i].GetGetMethod( true );
                        if( (pi[i].PropertyType).IsAtomic() )
                        {

                            if( pi[i].PropertyType == stringtype )
                            {
                                gen.Emit( OpCodes.Ldloc, toserialize );
                                gen.Emit( OpCodes.Callvirt, getter );

                                Label ifnotnull = gen.DefineLabel();
                                Label endif = gen.DefineLabel();
                                gen.Emit( OpCodes.Ldnull );
                                gen.Emit( OpCodes.Ceq );
                                gen.Emit( OpCodes.Brfalse, ifnotnull );
                                gen.Emit( OpCodes.Ldarg_1 );
                                gen.Emit( OpCodes.Ldc_I4, -1 );
                                gen.Emit( OpCodes.Callvirt, intwriter );
                                gen.Emit( OpCodes.Br, endif );
                                gen.MarkLabel( ifnotnull );
                                gen.Emit( OpCodes.Ldarg_1 );
                                gen.Emit( OpCodes.Ldc_I4, 0 );
                                gen.Emit( OpCodes.Callvirt, intwriter );
                                gen.Emit( OpCodes.Ldarg_1 );
                                gen.Emit( OpCodes.Ldloc, toserialize );
                                gen.Emit( OpCodes.Callvirt, getter );
                                gen.Emit( OpCodes.Callvirt, stringwriter );
                                gen.MarkLabel( endif );
                            }
                            else
                            {
                                gen.Emit( OpCodes.Ldarg_1 );
                                gen.Emit( OpCodes.Ldloc, toserialize );
                                gen.Emit( OpCodes.Callvirt, getter );
                                gen.Emit( OpCodes.Callvirt, writerdict[pi[i].PropertyType] );
                            }
                        }
                        else
                        {
                            gen.Emit( OpCodes.Ldsfld, nextserializerfield );
                            gen.Emit( OpCodes.Ldloc, toserialize );
                            gen.Emit( OpCodes.Callvirt, getter );
                            gen.Emit( OpCodes.Ldarg_1 );
                            gen.Emit( OpCodes.Callvirt, invokemethod );
                        }
                    }
                }

                FieldInfo[] fi = t.GetFields();
                Array.Sort( fi, ( a, b ) => a.Name.CompareTo( b.Name ) );
                int fieldcount = fi.Length;
                LocalBuilder toserializef = gen.DeclareLocal( t );
                gen.Emit( OpCodes.Ldarg_0 );
                gen.Emit( OpCodes.Stloc, toserializef );
                for( int i = 0; i < fieldcount; i++ )
                {
                    if( (fi[i]).IsFieldWritable() )
                    {
                        if( (fi[i].FieldType).IsAtomic() )
                        {
                            gen.Emit( OpCodes.Ldarg_1 );
                            gen.Emit( OpCodes.Ldloc, toserializef );
                            gen.Emit( OpCodes.Ldfld, fi[i] );
                            if( fi[i].FieldType == stringtype )
                            {
                                Label ifnotnull = gen.DefineLabel();
                                Label endif = gen.DefineLabel();
                                gen.Emit( OpCodes.Ldnull );
                                gen.Emit( OpCodes.Ceq );
                                gen.Emit( OpCodes.Brfalse, ifnotnull );
                                gen.Emit( OpCodes.Ldc_I4, -1 );
                                gen.Emit( OpCodes.Callvirt, intwriter );
                                gen.Emit( OpCodes.Br, endif );
                                gen.MarkLabel( ifnotnull );
                                gen.Emit( OpCodes.Ldc_I4, 0 );
                                gen.Emit( OpCodes.Callvirt, intwriter );
                                gen.Emit( OpCodes.Ldarg_1 );
                                gen.Emit( OpCodes.Ldloc, toserializef );
                                gen.Emit( OpCodes.Ldfld, fi[i] );
                                gen.Emit( OpCodes.Callvirt, stringwriter );
                                gen.MarkLabel( endif );
                            }
                            else
                            {
                                gen.Emit( OpCodes.Callvirt, writerdict[fi[i].FieldType] );
                            }
                        }
                        else
                        {
                            gen.Emit( OpCodes.Ldsfld, nextserializerfield );
                            gen.Emit( OpCodes.Ldloc, toserialize );
                            gen.Emit( OpCodes.Ldfld, fi[i] );
                            gen.Emit( OpCodes.Ldarg_1 );
                            gen.Emit( OpCodes.Callvirt, invokemethod );
                        }
                    }
                }
            }
            gen.Emit( OpCodes.Ret );
            typeserializer.CreateType();
        }
        public static void CreateSerializerDispatcher()
        {
            iterate++;
            TypeBuilder serializer = mb.DefineType( "Serializer_" + iterate.ToString(),
                                                   TypeAttributes.Public | TypeAttributes.Class );
            MethodBuilder objectserializer = serializer.DefineMethod( "SerializeObject_" + iterate.ToString(),
                                                                     MethodAttributes.Public | MethodAttributes.Static,
                                                                     typeof( void ),
                                                                     new[] { typeof( object ), typeof( SmartBinaryWriter ) } );

            ILGenerator gen = objectserializer.GetILGenerator();


            //if null then write -1
            Label ifnotnull = gen.DefineLabel();
            gen.Emit( OpCodes.Ldarg_0 );
            gen.Emit( OpCodes.Ldnull );
            gen.Emit( OpCodes.Ceq );
            gen.Emit( OpCodes.Brfalse, ifnotnull );
            gen.Emit( OpCodes.Ldarg_1 );
            gen.Emit( OpCodes.Ldc_I4, -1 );
            gen.Emit( OpCodes.Callvirt, intwriter );
            gen.Emit( OpCodes.Ret );
            gen.MarkLabel( ifnotnull );

            LocalBuilder isFirst = gen.DeclareLocal( typeof( bool ) );
            if( iterate > 0 )
            {
                //write by reference
                Label notcontains = gen.DefineLabel();
                gen.Emit( OpCodes.Ldsfld, oidGeneratorFieldInfo );
                gen.Emit( OpCodes.Ldarg_0 ); //paraméter a stackre
                gen.Emit( OpCodes.Ldloca, isFirst );
                gen.Emit( OpCodes.Callvirt, oidGenHasId );
                gen.Emit( OpCodes.Pop );
                gen.Emit( OpCodes.Ldloc, isFirst );
                gen.Emit( OpCodes.Brtrue, notcontains ); //ha még nincs hozzá id akkor kisorosítjuk

                //kiírjuk az indexet
                gen.Emit( OpCodes.Ldarg_1 );
                gen.Emit( OpCodes.Ldc_I4, -2 );
                gen.Emit( OpCodes.Callvirt, intwriter );
                gen.Emit( OpCodes.Ldarg_1 ); //writer a stackre

                gen.Emit( OpCodes.Ldsfld, oidGeneratorFieldInfo );
                gen.Emit( OpCodes.Ldarg_0 ); //paraméter a stackre
                gen.Emit( OpCodes.Ldloca, isFirst );
                gen.Emit( OpCodes.Callvirt, oidGenGetId );
                gen.Emit( OpCodes.Conv_I4 );
                gen.Emit( OpCodes.Callvirt, intwriter ); //kiírás
                gen.Emit( OpCodes.Ret );

                //ha nincs benne akkor majd a metódus legvégén hozzáadjuk
                gen.MarkLabel( notcontains );
            }

            LocalBuilder type = gen.DeclareLocal( typetype );

            Label endif = gen.DefineLabel();


            gen.Emit( OpCodes.Ldarg_0 );
            gen.Emit( OpCodes.Callvirt, gettype );
            gen.Emit( OpCodes.Stloc, type );

            if( iterate == 0 )
            {
                CreateAtomicSerializer( gen, type );
                knowntypes = new Dictionary<Type, MethodBuilder>();
            }

            foreach( KeyValuePair<Type, MethodBuilder> pair in knowntypes )
            {
                Label next = gen.DefineLabel();

                gen.Emit( OpCodes.Ldloc, type );
                gen.Emit( OpCodes.Ldtoken, pair.Key );
                gen.Emit( OpCodes.Call, runtimetypehandlemethod );
                gen.Emit( OpCodes.Ceq );
                gen.Emit( OpCodes.Brfalse, next );
                //hozzáadjuk a referenciák listájához, mivel következőnek biztos ez lesz kiírva
                gen.Emit( OpCodes.Ldsfld, oidGeneratorFieldInfo );
                gen.Emit( OpCodes.Ldarg_0 ); //paraméter a stackre
                gen.Emit( OpCodes.Ldloca, isFirst );
                gen.Emit( OpCodes.Callvirt, oidGenGetId ); //generáltatunk neki egy id-t az objectidgeneratorral így legközelebb a HasId == true lesz
                gen.Emit( OpCodes.Pop ); //visszatérési érték nem kell
                gen.Emit( OpCodes.Ldsfld, referenceCounterFieldInfo );
                gen.Emit( OpCodes.Ldc_I4_1 );
                gen.Emit( OpCodes.Add );
                gen.Emit( OpCodes.Stsfld, referenceCounterFieldInfo );
                //kiírjuk a típust
                gen.Emit( OpCodes.Ldarg_0 );
                if( pair.Key.IsValueType )
                {
                    gen.Emit( OpCodes.Unbox_Any, pair.Key );
                }
                else
                {
                    gen.Emit( OpCodes.Castclass, pair.Key );
                }
                gen.Emit( OpCodes.Ldarg_1 );
                gen.Emit( OpCodes.Call, pair.Value );
                gen.Emit( OpCodes.Ret );
                gen.MarkLabel( next );
            }

            gen.Emit( OpCodes.Ldsfld, typesfield );
            gen.Emit( OpCodes.Ldloc, type );
            gen.Emit( OpCodes.Callvirt, asmqualifiednamegetter );
            gen.Emit( OpCodes.Callvirt, addmethod );
            gen.Emit( OpCodes.Ldloc, type );
            gen.Emit( OpCodes.Call, typeserializermethod );
            gen.Emit( OpCodes.Call, dispatchermethod );

            gen.EmitInvokeNextSerializer();

            gen.Emit( OpCodes.Ret );
            gen.MarkLabel( endif );
            nextserializer =
                (Action<object, SmartBinaryWriter>)
                Delegate.CreateDelegate( actiontype,
                                        serializer.CreateType().GetMethod( "SerializeObject_" + iterate.ToString() ) );
            if( firstserializer == null )
            {
                firstserializer = (Action<object, SmartBinaryWriter>)nextserializer.Clone();
            }
        }
        private static void CreateAtomicSerializer( ILGenerator gen, LocalBuilder type )
        {
            for( int i = 0; i < 15; i++ )
            {
                Label next = gen.DefineLabel();
                gen.Emit( OpCodes.Ldloc, type );
                gen.Emit( OpCodes.Ldsfld,
                         dynamicserializertype.GetField( typefieldnames[i], BindingFlags.Public | BindingFlags.Static ) );
                gen.Emit( OpCodes.Ceq );
                gen.Emit( OpCodes.Brfalse, next );
                gen.Emit( OpCodes.Ldarg_1 );
                gen.Emit( OpCodes.Ldc_I4, i );
                gen.Emit( OpCodes.Callvirt, intwriter );
                gen.Emit( OpCodes.Ldarg_1 );
                gen.Emit( OpCodes.Ldarg_0 );
                if( i == 0 )
                {
                    gen.Emit( OpCodes.Castclass, atomictypes[i] );
                }
                else
                {
                    gen.Emit( OpCodes.Unbox_Any, atomictypes[i] );
                }

                gen.Emit( OpCodes.Callvirt, atomictypewriters[i] );
                gen.Emit( OpCodes.Ret );
                gen.MarkLabel( next );
            }
        }

        /// <summary>
        /// Starting point of the serialization process
        /// </summary>
        /// <param name="o">Root of the object graph</param>
        /// <param name="ms2">Target stream</param>
        public static void Serialize( object o, MemoryStream ms2 )
        {
            oidGenerator = new ObjectIDGenerator();
            referenceCounter = 0;

            CreateSerializerDispatcher();
            MemoryStream ms = new MemoryStream();
            firstserializer( o, new SmartBinaryWriter( ms ) );

            SmartBinaryWriter typewriter = new SmartBinaryWriter( ms2 );
            //referenciák száma
            typewriter.Write( referenceCounter );
            //a kimeneti streamre kiírjuk az egyedi típusok számát, a nevüket, majd mögé másoljuk a sorosított adatot
            typewriter.Write( types.Count );
            for( int i = 0; i < types.Count; i++ )
            {
                typewriter.Write( types[i] );
            }
            byte[] b1 = ms.GetBuffer();
            for( int i = 0; i < ms.Length; i++ )
            {
                ms2.WriteByte( b1[i] );
            }
            //SaveAssembly();

        }
        #endregion

        private static List<string> typestodeserialize = new List<string>();

        #region Deserialization Methods

        public static object Deserialize( MemoryStream ms )
        {
            oidGenerator = new ObjectIDGenerator();
            SmartBinaryReader br = new SmartBinaryReader( ms );
            //performance okokból a ref listát mérettel inicializáljuk
            int referencecount = br.ReadInt32();
            //references = new List<object>(referencecount);
            referenceDict = new Dictionary<int, object>( referencecount );
            referenceCounter = 0;
            int typecount = br.ReadInt32();
            List<Type> newtypes = new List<Type>();
            for( int i = 0; i < typecount; i++ )
            {
                string type = br.ReadString();
                if( typestodeserialize.Contains( type ) )
                {
                    continue;
                }
                typestodeserialize.Add( type );
                Type t = Type.GetType( type );
                newtypes.Add( t );
                string myname = t.Name;
                if( t.IsGenericDictionary() )
                {
                    var temp = t.GetGenericArguments();
                    myname += temp[0].Name + "_" + temp[1].Name;
                }
                else if( t.IsGenericCollection() )
                {
                    if( !t.IsArray )
                    {
                        myname += t.GetGenericArguments()[0].Name;
                    }
                }
                TypeBuilder typedeserializer = mb.DefineType( "Deserialize" + myname,
                                                             TypeAttributes.Public | TypeAttributes.Class );
                MethodBuilder typedeserializermethod = typedeserializer.DefineMethod( "DeserializeType" + myname,
                                                                                     MethodAttributes.Public |
                                                                                     MethodAttributes.Static, t,
                                                                                     new[] { typeof( SmartBinaryReader ) } );
                knowntypes_deserializers_typebuilders.Add( t, typedeserializer );
                knowntypes_deserializers.Add( t, typedeserializermethod );
            }
            foreach( var t2 in newtypes )
            {
                CreateTypeDeserializer( t2 );
            }
            int roottype = br.ReadInt32();
            var c = knowntypes_deserializers_typebuilders[Type.GetType( typestodeserialize[roottype - 15] )].GetMethods( BindingFlags.Public | BindingFlags.Static )[0].Invoke( null, new[] { br } );
            return c;
        }

        public static void SaveAssembly()
        {
            builder.Save( "SerializerAssembly.dll" );
        }



        public static void CreateTypeDeserializer( Type t )
        {

            ILGenerator gen = knowntypes_deserializers[t].GetILGenerator();
            if( t.IsGenericDictionary() )
            {
                #region IsGenericDictionary
                LocalBuilder retval = gen.DeclareLocal( t );
                LocalBuilder count = gen.DeclareLocal( inttype );

                gen.Emit( OpCodes.Ldarg_0 );
                gen.Emit( OpCodes.Callvirt, intreader );
                gen.Emit( OpCodes.Stloc, count );
                gen.Emit( OpCodes.Ldloc, count );
                gen.Emit( OpCodes.Newobj, t.GetConstructor( new[] { inttype } ) );
                gen.Emit( OpCodes.Stloc, retval );

                EmitAddToReferences( gen, retval );

                LocalBuilder loopvar = gen.DeclareLocal( inttype );
                gen.Emit( OpCodes.Ldc_I4_0 );
                gen.Emit( OpCodes.Stloc, loopvar );
                Label startloop = gen.DefineLabel();
                Label endloop = gen.DefineLabel();
                Type genarg0 = t.GetGenericArguments()[0];
                Type genarg1 = t.GetGenericArguments()[1];

                gen.MarkLabel( startloop );
                gen.Emit( OpCodes.Ldloc, count );
                gen.Emit( OpCodes.Ldloc, loopvar );
                gen.Emit( OpCodes.Ble, endloop );

                gen.Emit( OpCodes.Ldloc, retval );

                if( genarg0.IsAtomic() )
                {

                    if( genarg0.IsEnum )
                    {
                        gen.Emit( OpCodes.Ldarg_0 );
                        gen.Emit( OpCodes.Callvirt, intreader );
                    }
                    else
                    {
                        gen.Emit( OpCodes.Ldarg_0 );
                        gen.Emit( OpCodes.Callvirt, readerdict[genarg0] );
                    }
                }
                else
                {
                    LocalBuilder typeindex = gen.DeclareLocal( inttype );
                    gen.Emit( OpCodes.Ldarg_0 );
                    gen.Emit( OpCodes.Callvirt, readerdict[inttype] );
                    gen.Emit( OpCodes.Stloc, typeindex );
                    Label endif = gen.DefineLabel();
                    foreach( KeyValuePair<Type, MethodBuilder> pair in knowntypes_deserializers )
                    {
                        Label next = gen.DefineLabel();
                        gen.Emit( OpCodes.Ldloc, typeindex );
                        gen.Emit( OpCodes.Ldc_I4, typestodeserialize.TypeIndex( pair.Key.AssemblyQualifiedName ) + 15 );
                        gen.Emit( OpCodes.Ceq );
                        gen.Emit( OpCodes.Brfalse, next );
                        gen.Emit( OpCodes.Ldarg_0 );
                        gen.Emit( OpCodes.Call, pair.Value );
                        gen.Emit( OpCodes.Castclass, pair.Key );
                        gen.Emit( OpCodes.Br, endif );
                        gen.MarkLabel( next );
                    }
                    gen.MarkLabel( endif );
                }

                if( genarg1.IsAtomic() )
                {
                    if( genarg1 == stringtype )
                    {
                        Label nullabel = gen.DefineLabel();
                        Label notnullabel = gen.DefineLabel();
                        gen.Emit( OpCodes.Ldarg_0 );
                        gen.Emit( OpCodes.Callvirt, intreader );
                        gen.Emit( OpCodes.Ldc_I4, -1 );
                        gen.Emit( OpCodes.Ceq );
                        gen.Emit( OpCodes.Brfalse, notnullabel );
                        gen.Emit( OpCodes.Ldnull );
                        gen.Emit( OpCodes.Br, nullabel );
                        gen.MarkLabel( notnullabel );
                        gen.Emit( OpCodes.Ldarg_0 );
                        gen.Emit( OpCodes.Callvirt, stringreader );
                        gen.MarkLabel( nullabel );
                    }
                    if( genarg1.IsEnum )
                    {
                        gen.Emit( OpCodes.Ldarg_0 );
                        gen.Emit( OpCodes.Callvirt, intreader );
                    }
                    else
                    {
                        gen.Emit( OpCodes.Ldarg_0 );
                        gen.Emit( OpCodes.Callvirt, readerdict[genarg1] );
                    }
                }
                else
                {
                    LocalBuilder typeindex = gen.DeclareLocal( inttype );
                    gen.Emit( OpCodes.Ldarg_0 );
                    gen.Emit( OpCodes.Callvirt, readerdict[inttype] );
                    gen.Emit( OpCodes.Stloc, typeindex );
                    Label endif = gen.DefineLabel();
                    foreach( KeyValuePair<Type, MethodBuilder> pair in knowntypes_deserializers )
                    {
                        Label next = gen.DefineLabel();
                        gen.Emit( OpCodes.Ldloc, typeindex );
                        gen.Emit( OpCodes.Ldc_I4, typestodeserialize.TypeIndex( pair.Key.AssemblyQualifiedName ) + 15 );
                        gen.Emit( OpCodes.Ceq );
                        gen.Emit( OpCodes.Brfalse, next );
                        gen.Emit( OpCodes.Ldarg_0 );
                        gen.Emit( OpCodes.Call, pair.Value );
                        gen.Emit( OpCodes.Castclass, pair.Key );
                        gen.Emit( OpCodes.Br, endif );
                        gen.MarkLabel( next );
                    }
                    gen.MarkLabel( endif );
                }
                gen.Emit( OpCodes.Callvirt, t.GetMethod( "Add" ) );
                gen.Emit( OpCodes.Ldloc, loopvar );
                gen.Emit( OpCodes.Ldc_I4_1 );
                gen.Emit( OpCodes.Add );
                gen.Emit( OpCodes.Stloc, loopvar );
                gen.Emit( OpCodes.Br, startloop );
                gen.MarkLabel( endloop );
                gen.Emit( OpCodes.Ldloc, retval );
                gen.Emit( OpCodes.Ret );
                #endregion
            }
            else if( t.IsGenericCollection() )
            {
                #region IsGenericCollection
                if( t.IsArray )
                {
                    LocalBuilder retval = gen.DeclareLocal( t );
                    LocalBuilder count = gen.DeclareLocal( inttype );
                    Label startloop = gen.DefineLabel();
                    Label endloop = gen.DefineLabel();
                    LocalBuilder loopvar = gen.DeclareLocal( inttype );
                    Type elementtype = t.GetElementType();
                    gen.Emit( OpCodes.Ldarg_0 );
                    gen.Emit( OpCodes.Callvirt, readerdict[inttype] );
                    gen.Emit( OpCodes.Stloc, count );
                    gen.Emit( OpCodes.Ldloc, count );
                    gen.Emit( OpCodes.Newarr, elementtype );
                    gen.Emit( OpCodes.Stloc, retval );

                    EmitAddToReferences( gen, retval );

                    gen.MarkLabel( startloop );
                    gen.Emit( OpCodes.Ldloc, count );
                    gen.Emit( OpCodes.Ldloc, loopvar );
                    gen.Emit( OpCodes.Ble, endloop );

                    if( elementtype.IsAtomic() )
                    {
                        if( elementtype == stringtype )
                        {
                            Label nullabel = gen.DefineLabel();
                            Label notnullabel = gen.DefineLabel();
                            gen.Emit( OpCodes.Ldarg_0 );
                            gen.Emit( OpCodes.Callvirt, intreader );
                            gen.Emit( OpCodes.Ldc_I4, -1 );
                            gen.Emit( OpCodes.Ceq );
                            gen.Emit( OpCodes.Brfalse, notnullabel );
                            gen.Emit( OpCodes.Ldnull );
                            gen.Emit( OpCodes.Br, nullabel );
                            gen.MarkLabel( notnullabel );
                            gen.Emit( OpCodes.Ldarg_0 );
                            gen.Emit( OpCodes.Callvirt, stringreader );
                            gen.MarkLabel( nullabel );
                        }
                        else if( elementtype.IsEnum )
                        {
                            gen.Emit( OpCodes.Ldarg_0 );
                            gen.Emit( OpCodes.Callvirt, readerdict[inttype] );
                        }
                        else
                        {
                            gen.Emit( OpCodes.Ldarg_0 );
                            gen.Emit( OpCodes.Callvirt, readerdict[elementtype] );
                        }
                    }
                    else
                    {
                        gen.Emit( OpCodes.Ldarg_0 );
                        gen.Emit( OpCodes.Callvirt, readerdict[inttype] );
                        LocalBuilder typeindex = gen.DeclareLocal( inttype );
                        gen.Emit( OpCodes.Stloc, typeindex );

                        Label endif = gen.DefineLabel();

                        gen.Emit( OpCodes.Ldloc, typeindex );
                        EmitLoadByReference( gen, retval, ( g ) =>
                          {
                              gen.Emit( OpCodes.Ldloc, retval );
                              gen.Emit( OpCodes.Ldloc, loopvar );
                          },
                        ( g ) =>
                        {
                            g.Emit( OpCodes.Castclass, elementtype );
                            g.Emit( OpCodes.Stelem, elementtype );
                            gen.Emit( OpCodes.Br, endif );
                        } );

                        foreach( KeyValuePair<Type, MethodBuilder> pair in knowntypes_deserializers )
                        {
                            Label next = gen.DefineLabel();
                            gen.Emit( OpCodes.Ldloc, typeindex );
                            gen.Emit( OpCodes.Ldc_I4, typestodeserialize.TypeIndex( pair.Key.AssemblyQualifiedName ) + 15 );
                            gen.Emit( OpCodes.Ceq );
                            gen.Emit( OpCodes.Brfalse, next );
                            gen.Emit( OpCodes.Ldloc, retval );
                            gen.Emit( OpCodes.Ldloc, loopvar );
                            gen.Emit( OpCodes.Ldarg_0 );
                            gen.Emit( OpCodes.Call, pair.Value );
                            gen.Emit( OpCodes.Castclass, pair.Key );
                            gen.Emit( OpCodes.Stelem, elementtype );
                            gen.Emit( OpCodes.Br, endif );
                            gen.MarkLabel( next );
                        }
                        gen.MarkLabel( endif );
                    }
                    gen.Emit( OpCodes.Ldloc, loopvar );
                    gen.Emit( OpCodes.Ldc_I4_1 );
                    gen.Emit( OpCodes.Add );
                    gen.Emit( OpCodes.Stloc, loopvar );
                    gen.Emit( OpCodes.Br, startloop );
                    gen.MarkLabel( endloop );

                    gen.Emit( OpCodes.Ldloc, retval );
                    gen.Emit( OpCodes.Ret );
                }
                else
                {
                    LocalBuilder retval = gen.DeclareLocal( t );
                    LocalBuilder count = gen.DeclareLocal( inttype );
                    gen.Emit( OpCodes.Ldarg_0 );
                    gen.Emit( OpCodes.Callvirt, readerdict[inttype] );
                    gen.Emit( OpCodes.Stloc, count );
                    gen.Emit( OpCodes.Ldloc, count );
                    gen.Emit( OpCodes.Newobj, t.GetConstructor( new[] { inttype } ) );
                    gen.Emit( OpCodes.Stloc, retval );

                    EmitAddToReferences( gen, retval );

                    LocalBuilder loopvar = gen.DeclareLocal( inttype );
                    gen.Emit( OpCodes.Ldc_I4_0 );
                    gen.Emit( OpCodes.Stloc, loopvar );
                    Label startloop = gen.DefineLabel();
                    Label endloop = gen.DefineLabel();
                    Type genarg = t.GetGenericArguments()[0];
                    //if (!flag)
                    //{
                    //    gen.Emit(OpCodes.Ldc_I4_2);
                    //    gen.Emit(OpCodes.Ldloc, count);
                    //    gen.Emit(OpCodes.Mul);
                    //    gen.Emit(OpCodes.Stloc, count);
                    //    //flag = true;
                    //}        
                    gen.MarkLabel( startloop );
                    gen.Emit( OpCodes.Ldloc, count );
                    gen.Emit( OpCodes.Ldloc, loopvar );
                    gen.Emit( OpCodes.Ble, endloop );
                    if( genarg.IsAtomic() )
                    {
                        gen.Emit( OpCodes.Ldloc, retval );
                        if( genarg == stringtype )
                        {
                            Label nullabel = gen.DefineLabel();
                            Label notnullabel = gen.DefineLabel();
                            gen.Emit( OpCodes.Ldarg_0 );
                            gen.Emit( OpCodes.Callvirt, intreader );
                            gen.Emit( OpCodes.Ldc_I4, -1 );
                            gen.Emit( OpCodes.Ceq );
                            gen.Emit( OpCodes.Brfalse, notnullabel );
                            gen.Emit( OpCodes.Ldnull );
                            gen.Emit( OpCodes.Br, nullabel );
                            gen.MarkLabel( notnullabel );
                            gen.Emit( OpCodes.Ldarg_0 );
                            gen.Emit( OpCodes.Callvirt, stringreader );
                            gen.MarkLabel( nullabel );
                        }
                        else if( genarg.IsEnum )
                        {
                            gen.Emit( OpCodes.Ldarg_0 );
                            gen.Emit( OpCodes.Callvirt, readerdict[inttype] );
                        }
                        else
                        {
                            gen.Emit( OpCodes.Ldarg_0 );
                            gen.Emit( OpCodes.Callvirt, readerdict[genarg] );
                        }
                        gen.Emit( OpCodes.Callvirt, t.GetMethod( "Add" ) );
                    }
                    else
                    {
                        LocalBuilder typeindex = gen.DeclareLocal( inttype );
                        gen.Emit( OpCodes.Ldarg_0 );
                        gen.Emit( OpCodes.Callvirt, readerdict[inttype] );
                        gen.Emit( OpCodes.Stloc, typeindex );

                        Label endif = gen.DefineLabel();

                        gen.Emit( OpCodes.Ldloc, typeindex );
                        EmitLoadByReference( gen, retval, ( g ) => gen.Emit( OpCodes.Ldloc, retval ),
                        ( g ) =>
                        {
                            gen.Emit( OpCodes.Callvirt, t.GetMethod( "Add" ) );
                            gen.Emit( OpCodes.Br, endif );
                        } );

                        foreach( KeyValuePair<Type, MethodBuilder> pair in knowntypes_deserializers )
                        {
                            //gen.EmitWriteLine(typeindex);
                            //flag = true;
                            Label next = gen.DefineLabel();
                            gen.Emit( OpCodes.Ldloc, typeindex );
                            gen.Emit( OpCodes.Ldc_I4, typestodeserialize.TypeIndex( pair.Key.AssemblyQualifiedName ) + 15 );
                            gen.Emit( OpCodes.Ceq );
                            gen.Emit( OpCodes.Brfalse, next );
                            gen.Emit( OpCodes.Ldloc, retval );
                            gen.Emit( OpCodes.Ldarg_0 );
                            gen.Emit( OpCodes.Call, pair.Value );
                            gen.Emit( OpCodes.Castclass, pair.Key );
                            gen.Emit( OpCodes.Callvirt, t.GetMethod( "Add" ) );
                            gen.Emit( OpCodes.Br, endif );
                            gen.MarkLabel( next );
                        }
                        gen.MarkLabel( endif );
                    }
                    gen.Emit( OpCodes.Ldloc, loopvar );
                    gen.Emit( OpCodes.Ldc_I4_1 );
                    gen.Emit( OpCodes.Add );
                    gen.Emit( OpCodes.Stloc, loopvar );
                    gen.Emit( OpCodes.Br, startloop );
                    gen.MarkLabel( endloop );
                    gen.Emit( OpCodes.Ldloc, retval );
                    gen.Emit( OpCodes.Ret );
                }
                #endregion
            }
            else
            {
                LocalBuilder retval = gen.DeclareLocal( t );

                if( t.GetConstructor( Type.EmptyTypes ) != null )
                {
                    gen.Emit( OpCodes.Newobj, t.GetConstructor( Type.EmptyTypes ) );
                    gen.Emit( OpCodes.Stloc, retval );
                }
                else
                {
                    EmitGetUninitializedType( gen, t, retval );
                }

                EmitAddToReferences( gen, retval );

                PropertyInfo[] pi = t.GetProperties();
                Array.Sort( pi, ( a, b ) => a.Name.CompareTo( b.Name ) );
                int propcount = pi.Length;
                for( int i = 0; i < propcount; i++ )
                {
                    if( pi[i].IsPropertyWritable() )
                    {
                        MethodInfo setter = pi[i].GetSetMethod();
                        if( pi[i].PropertyType.IsAtomic() )
                        {
                            gen.Emit( OpCodes.Ldloc, retval );
                            if( pi[i].PropertyType == stringtype )
                            {
                                Label nullabel = gen.DefineLabel();
                                Label notnullabel = gen.DefineLabel();
                                gen.Emit( OpCodes.Ldarg_0 );
                                gen.Emit( OpCodes.Callvirt, intreader );
                                gen.Emit( OpCodes.Ldc_I4, -1 );
                                gen.Emit( OpCodes.Ceq );
                                gen.Emit( OpCodes.Brfalse, notnullabel );
                                gen.Emit( OpCodes.Ldnull );
                                gen.Emit( OpCodes.Br, nullabel );
                                gen.MarkLabel( notnullabel );
                                gen.Emit( OpCodes.Ldarg_0 );
                                gen.Emit( OpCodes.Callvirt, stringreader );
                                gen.MarkLabel( nullabel );
                            }
                            else if( pi[i].PropertyType.IsEnum )
                            {
                                gen.Emit( OpCodes.Ldarg_0 );
                                gen.Emit( OpCodes.Callvirt, readerdict[inttype] );
                            }
                            else
                            {
                                gen.Emit( OpCodes.Ldarg_0 );
                                gen.Emit( OpCodes.Callvirt, readerdict[pi[i].PropertyType] );
                            }
                            gen.Emit( OpCodes.Callvirt, setter );
                        }
                        else
                        {
                            gen.Emit( OpCodes.Ldarg_0 );
                            gen.Emit( OpCodes.Callvirt, readerdict[inttype] );
                            LocalBuilder typeindex = gen.DeclareLocal( inttype );
                            gen.Emit( OpCodes.Stloc, typeindex );

                            gen.Emit( OpCodes.Ldloc, typeindex );
                            EmitLoadByReference( gen, retval, ( g ) => g.Emit( OpCodes.Ldloc, retval ), ( g ) =>
                                  {
                                      if( pi[i].PropertyType.IsValueType )
                                      {
                                          g.Emit( OpCodes.Unbox_Any, pi[i].PropertyType );
                                      }
                                      else
                                      {
                                          g.Emit( OpCodes.Castclass, pi[i].PropertyType );
                                      }
                                      g.Emit( OpCodes.Callvirt, setter );
                                  } );

                            Label endif = gen.DefineLabel();
                            foreach( KeyValuePair<Type, MethodBuilder> pair in knowntypes_deserializers )
                            {
                                Label next = gen.DefineLabel();
                                gen.Emit( OpCodes.Ldloc, typeindex );
                                gen.Emit( OpCodes.Ldc_I4, typestodeserialize.TypeIndex( pair.Key.AssemblyQualifiedName ) + 15 );
                                gen.Emit( OpCodes.Ceq );
                                gen.Emit( OpCodes.Brfalse, next );
                                gen.Emit( OpCodes.Ldloc, retval );
                                gen.Emit( OpCodes.Ldarg_0 );
                                gen.Emit( OpCodes.Call, pair.Value );
                                if( pi[i].PropertyType.IsValueType )
                                {
                                    gen.Emit( OpCodes.Unbox_Any, pi[i].PropertyType );
                                }
                                else
                                {
                                    gen.Emit( OpCodes.Castclass, pi[i].PropertyType );
                                }
                                gen.Emit( OpCodes.Callvirt, setter );
                                gen.Emit( OpCodes.Br, endif );
                                gen.MarkLabel( next );
                            }
                            gen.MarkLabel( endif );

                        }
                    }
                }
                FieldInfo[] fi = t.GetFields();
                Array.Sort( pi, ( a, b ) => a.Name.CompareTo( b.Name ) );
                int fieldcount = fi.Length;
                for( int i = 0; i < fieldcount; i++ )
                {
                    if( fi[i].IsFieldWritable() )
                    {
                        gen.Emit( OpCodes.Ldloc, retval );
                        if( fi[i].FieldType.IsAtomic() )
                        {

                            if( fi[i].FieldType == stringtype )
                            {
                                Label nullabel = gen.DefineLabel();
                                Label notnullabel = gen.DefineLabel();
                                gen.Emit( OpCodes.Ldarg_0 );
                                gen.Emit( OpCodes.Callvirt, intreader );
                                gen.Emit( OpCodes.Ldc_I4, -1 );
                                gen.Emit( OpCodes.Ceq );
                                gen.Emit( OpCodes.Brfalse, notnullabel );
                                gen.Emit( OpCodes.Ldnull );
                                gen.Emit( OpCodes.Br, nullabel );
                                gen.MarkLabel( notnullabel );
                                gen.Emit( OpCodes.Ldarg_0 );
                                gen.Emit( OpCodes.Callvirt, stringreader );
                                gen.MarkLabel( nullabel );
                            }
                            else if( fi[i].FieldType.IsEnum )
                            {
                                gen.Emit( OpCodes.Ldarg_0 );
                                gen.Emit( OpCodes.Callvirt, readerdict[inttype] );
                            }
                            else
                            {
                                gen.Emit( OpCodes.Ldarg_0 );
                                gen.Emit( OpCodes.Callvirt, readerdict[fi[i].FieldType] );
                            }
                            gen.Emit( OpCodes.Stfld, fi[i] );
                        }
                        else
                        {

                            gen.Emit( OpCodes.Ldarg_0 );
                            gen.Emit( OpCodes.Callvirt, readerdict[inttype] );
                            LocalBuilder typeindex = gen.DeclareLocal( inttype );
                            gen.Emit( OpCodes.Stloc, typeindex );

                            gen.Emit( OpCodes.Ldloc, typeindex );
                            EmitLoadByReference( gen, retval, ( g ) => g.Emit( OpCodes.Ldloc, retval ), ( g ) =>
                                    {
                                        if( fi[i].FieldType.IsValueType )
                                        {
                                            g.Emit( OpCodes.Unbox_Any, fi[i].FieldType );
                                        }
                                        else
                                        {
                                            g.Emit( OpCodes.Castclass, fi[i].FieldType );
                                        }
                                        g.Emit( OpCodes.Stfld, fi[i] );
                                    } );

                            Label endif = gen.DefineLabel();
                            foreach( KeyValuePair<Type, MethodBuilder> pair in knowntypes_deserializers )
                            {
                                Label next = gen.DefineLabel();
                                gen.Emit( OpCodes.Ldloc, typeindex );
                                gen.Emit( OpCodes.Ldc_I4, typestodeserialize.TypeIndex( pair.Key.AssemblyQualifiedName ) + 15 );
                                gen.Emit( OpCodes.Ceq );
                                gen.Emit( OpCodes.Brfalse, next );
                                gen.Emit( OpCodes.Ldloc, retval );
                                gen.Emit( OpCodes.Ldarg_0 );
                                gen.Emit( OpCodes.Call, pair.Value );
                                if( fi[i].FieldType.IsValueType )
                                {
                                    gen.Emit( OpCodes.Unbox_Any, fi[i].FieldType );
                                }
                                else
                                {
                                    gen.Emit( OpCodes.Castclass, fi[i].FieldType );
                                }
                                gen.Emit( OpCodes.Stfld, fi[i] );
                                gen.Emit( OpCodes.Br, endif );
                                gen.MarkLabel( next );
                            }
                            gen.MarkLabel( endif );
                        }
                    }
                }

                gen.Emit( OpCodes.Ldloc, retval );
                gen.Emit( OpCodes.Ret );
            }
            knowntypes_deserializers_typebuilders[t].CreateType();
        }

        /// <summary>
        /// Visszaad egy inicializálatlan példányt az adott típusból. Nem futtat ctor-t.
        /// </summary>
        /// <param name="gen"></param>
        /// <param name="t"></param>
        /// <param name="retval"></param>
        private static void EmitGetUninitializedType( ILGenerator gen, Type t, LocalBuilder retval )
        {
            //gen.Emit(OpCodes.Ldstr, t.AssemblyQualifiedName);
            //gen.Emit(OpCodes.Call, getTypeFromString);

            gen.Emit( OpCodes.Ldtoken, t );
            gen.Emit( OpCodes.Call, runtimetypehandlemethod );
            gen.Emit( OpCodes.Call, formatterServiceGetUninitialized );
            gen.Emit( OpCodes.Castclass, t );
            gen.Emit( OpCodes.Stloc, retval );
        }

        private static void EmitAddToReferences( ILGenerator gen, LocalBuilder retval )
        {
            Label contains = gen.DefineLabel();

            //hozzáadjuk a referencia dictionary-hez
            gen.Emit( OpCodes.Ldsfld, referenceDictFieldInfo ); //dict a stackre mert később kelleni fog

            //kérünk egy id-t a példánynak
            //ez meg fog felelni mert az objectid generator ugyanabban a sorrendben (1-től) osztja ki az id-ket
            //mint a sorosítási oldalon
            //LocalBuilder isFirst = gen.DeclareLocal(typeof(bool));
            //gen.Emit(OpCodes.Ldsfld, oidGeneratorFieldInfo);
            //gen.Emit(OpCodes.Ldloc, retval);
            //gen.Emit(OpCodes.Ldloca, isFirst);
            //gen.Emit(OpCodes.Callvirt, oidGenGetId);

            //mivel az objectid generator úgyis 1-től sorrendben osztja ki a számokat így a deserializálási oldalon
            //elég csak a beolvasott elemek számát számon tartani
            gen.Emit( OpCodes.Ldsfld, referenceCounterFieldInfo );
            gen.Emit( OpCodes.Ldc_I4_1 );
            gen.Emit( OpCodes.Add );
            gen.Emit( OpCodes.Stsfld, referenceCounterFieldInfo );
            gen.Emit( OpCodes.Ldsfld, referenceCounterFieldInfo );

            gen.Emit( OpCodes.Ldloc, retval );
            gen.Emit( OpCodes.Callvirt, dictAdd );

            gen.MarkLabel( contains );
        }

        #endregion

        /// <summary>
        /// Legyen a stacken a beolvasott osztály-sorszám a hívás előtt
        /// </summary>
        /// <param name="gen"></param>
        public static void EmitLoadByReference( ILGenerator gen, LocalBuilder retval, Action<ILGenerator> emitPrepare, Action<ILGenerator> emitSaveToVariable )
        {
            //ha -2-t olvastunk be egy referencia típusú változó sorszámának akkor az referencia szerinti sorosítás
            Label ei = gen.DefineLabel();
            gen.Emit( OpCodes.Ldc_I4, -2 );
            gen.Emit( OpCodes.Ceq );
            gen.Emit( OpCodes.Brfalse, ei );

            //sorszám kiolvasása
            gen.Emit( OpCodes.Ldarg_0 );
            gen.Emit( OpCodes.Callvirt, readerdict[inttype] );
            LocalBuilder sernum = gen.DeclareLocal( inttype );
            gen.Emit( OpCodes.Stloc, sernum );

            //Elemek betöltése amiknek a stacken kell lenni a beolvasott példány "alatt".
            emitPrepare( gen );


            gen.Emit( OpCodes.Ldsfld, referenceDictFieldInfo );
            gen.Emit( OpCodes.Ldloc, sernum );
            gen.Emit( OpCodes.Callvirt, dictIndexer );
            emitSaveToVariable( gen );
            gen.MarkLabel( ei );
        }
    }

}
