using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DatabaseManager.SQLite
{
    public static class Orm
    {
        public const int DefaultMaxStringLength = 140;
        public const string ImplicitPkName = "Id";
        public const string ImplicitIndexSuffix = "Id";

        public static Type GetType(object obj)
        {
            if (obj == null)
                return typeof(object);
            var rt = obj as IReflectableType;
            if (rt != null)
                return rt.GetTypeInfo().AsType();
            return obj.GetType();
        }

        public static string SqlDecl(TableMapping.Column p, bool storeDateTimeAsTicks, bool storeTimeSpanAsTicks)
        {
            string decl = "\"" + p.Name + "\" " + SqlType(p, storeDateTimeAsTicks, storeTimeSpanAsTicks) + " ";

            if (p.IsPK)
            {
                decl += "primary key ";
            }

            if (p.IsAutoInc)
            {
                decl += "autoincrement ";
            }

            if (!p.IsNullable)
            {
                decl += "not null ";
            }

            if (!string.IsNullOrEmpty(p.Collation))
            {
                decl += "collate " + p.Collation + " ";
            }

            return decl;
        }

        public static string SqlType(TableMapping.Column p, bool storeDateTimeAsTicks, bool storeTimeSpanAsTicks)
        {
            var clrType = p.ColumnType;
            if (clrType == typeof(Boolean) || clrType == typeof(Byte) || clrType == typeof(UInt16) ||
                clrType == typeof(SByte) || clrType == typeof(Int16) || clrType == typeof(Int32) ||
                clrType == typeof(UInt32) || clrType == typeof(Int64) || clrType == typeof(UInt64))
            {
                return "integer";
            }
            else if (clrType == typeof(Single) || clrType == typeof(Double) || clrType == typeof(Decimal))
            {
                return "float";
            }
            else if (clrType == typeof(String) || clrType == typeof(StringBuilder) || clrType == typeof(Uri) ||
                     clrType == typeof(UriBuilder))
            {
                int? len = p.MaxStringLength;

                if (len.HasValue)
                    return "varchar(" + len.Value + ")";

                return "varchar";
            }
            else if (clrType == typeof(TimeSpan))
            {
                return storeTimeSpanAsTicks ? "bigint" : "time";
            }
            else if (clrType == typeof(DateTime))
            {
                return storeDateTimeAsTicks ? "bigint" : "datetime";
            }
            else if (clrType == typeof(DateTimeOffset))
            {
                return "bigint";
            }
            else if (clrType.GetTypeInfo().IsEnum)
            {
                if (p.StoreAsText)
                    return "varchar";
                else
                    return "integer";
            }
            else if (clrType == typeof(byte[]))
            {
                return "blob";
            }
            else if (clrType == typeof(Guid))
            {
                return "varchar(36)";
            }
            else
            {
                throw new NotSupportedException("Don't know about " + clrType);
            }
        }

        public static bool IsPK(MemberInfo p)
        {
            return p.CustomAttributes.Any(x => x.AttributeType == typeof(PrimaryKeyAttribute));
        }

        public static string Collation(MemberInfo p)
        {
#if ENABLE_IL2CPP
			return (p.GetCustomAttribute<CollationAttribute> ()?.Value) ?? "";
#else
            return
                (p.CustomAttributes
                    .Where(x => typeof(CollationAttribute) == x.AttributeType)
                    .Select(x =>
                    {
                        var args = x.ConstructorArguments;
                        return args.Count > 0 ? ((args[0].Value as string) ?? "") : "";
                    })
                    .FirstOrDefault()) ?? "";
#endif
        }

        public static bool IsAutoInc(MemberInfo p)
        {
            return p.CustomAttributes.Any(x => x.AttributeType == typeof(AutoIncrementAttribute));
        }

        public static FieldInfo GetField(
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            TypeInfo t,
            string name)
        {
            var f = t.GetDeclaredField(name);
            if (f != null)
                return f;
            return GetField(t.BaseType.GetTypeInfo(), name);
        }

        public static PropertyInfo GetProperty(
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            TypeInfo t,
            string name)
        {
            var f = t.GetDeclaredProperty(name);
            if (f != null)
                return f;
            return GetProperty(t.BaseType.GetTypeInfo(), name);
        }

#if !NET8_0_OR_GREATER
        public static object InflateAttribute(CustomAttributeData x)
        {
            var atype = x.AttributeType;
            var typeInfo = atype.GetTypeInfo();
#if ENABLE_IL2CPP
			var r = Activator.CreateInstance (x.AttributeType);
#else
            var args = x.ConstructorArguments.Select(a => a.Value).ToArray();
            var r = Activator.CreateInstance(x.AttributeType, args);
            foreach (var arg in x.NamedArguments)
            {
                if (arg.IsField)
                {
                    GetField(typeInfo, arg.MemberName).SetValue(r, arg.TypedValue.Value);
                }
                else
                {
                    GetProperty(typeInfo, arg.MemberName).SetValue(r, arg.TypedValue.Value);
                }
            }
#endif
            return r;
        }
#endif

        public static IEnumerable<IndexedAttribute> GetIndices(MemberInfo p)
        {
#if ENABLE_IL2CPP || NET8_0_OR_GREATER
			return p.GetCustomAttributes<IndexedAttribute> ();
#else
            var indexedInfo = typeof(IndexedAttribute).GetTypeInfo();
            return
                p.CustomAttributes
                    .Where(x => indexedInfo.IsAssignableFrom(x.AttributeType.GetTypeInfo()))
                    .Select(x => (IndexedAttribute) InflateAttribute(x));
#endif
        }

        public static int? MaxStringLength(MemberInfo p)
        {
#if ENABLE_IL2CPP
			return p.GetCustomAttribute<MaxLengthAttribute> ()?.Value;
#elif NET8_0_OR_GREATER
			return p.GetCustomAttributes<MaxLengthAttribute> ().FirstOrDefault ()?.Value;
#else
            var attr = p.CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(MaxLengthAttribute));
            if (attr != null)
            {
                var attrv = (MaxLengthAttribute) InflateAttribute(attr);
                return attrv.Value;
            }

            return null;
#endif
        }

        public static int? MaxStringLength(PropertyInfo p) => MaxStringLength((MemberInfo) p);

        public static bool IsMarkedNotNull(MemberInfo p)
        {
            return p.CustomAttributes.Any(x => x.AttributeType == typeof(NotNullAttribute));
        }
    }
}