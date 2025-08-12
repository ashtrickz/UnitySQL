using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DatabaseManager.SQLite
{
    public class TableMapping
    {
#if NET8_0_OR_GREATER
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
#endif
        public Type MappedType { get; private set; }

        public string TableName { get; private set; }

        public bool WithoutRowId { get; private set; }

        public Column[] Columns { get; private set; }

        public Column PK { get; private set; }

        public string GetByPrimaryKeySql { get; private set; }

        public CreateFlags CreateFlags { get; private set; }

        internal MapMethod Method { get; private set; } = MapMethod.ByName;

        readonly Column _autoPk;
        readonly Column[] _insertColumns;
        readonly Column[] _insertOrReplaceColumns;

        public TableMapping(
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            Type type,
            CreateFlags createFlags = CreateFlags.None)
        {
            MappedType = type;
            CreateFlags = createFlags;

#if ENABLE_IL2CPP
			var typeInfo = type.GetTypeInfo ();
			var tableAttr = typeInfo.GetCustomAttribute<TableAttribute> ();
#elif NET8_0_OR_GREATER
			var tableAttr = type.GetCustomAttributes<TableAttribute> ().FirstOrDefault ();
#else
            var typeInfo = type.GetTypeInfo();
            var tableAttr =
                typeInfo.CustomAttributes
                    .Where(x => x.AttributeType == typeof(TableAttribute))
                    .Select(x => (TableAttribute) Orm.InflateAttribute(x))
                    .FirstOrDefault();
#endif

            TableName = (tableAttr != null && !string.IsNullOrEmpty(tableAttr.Name)) ? tableAttr.Name : MappedType.Name;
            WithoutRowId = tableAttr != null ? tableAttr.WithoutRowId : false;

            var members = GetPublicMembers(type);
            var cols = new List<Column>(members.Count);
            foreach (var m in members)
            {
                var ignore = m.IsDefined(typeof(IgnoreAttribute), true);
                if (!ignore)
                    cols.Add(new Column(m, createFlags));
            }

            Columns = cols.ToArray();
            foreach (var c in Columns)
            {
                if (c.IsAutoInc && c.IsPK)
                {
                    _autoPk = c;
                }

                if (c.IsPK)
                {
                    PK = c;
                }
            }

            HasAutoIncPK = _autoPk != null;

            if (PK != null)
            {
                GetByPrimaryKeySql = string.Format("select * from \"{0}\" where \"{1}\" = ?", TableName, PK.Name);
            }
            else
            {
                // People should not be calling Get/Find without a PK
                GetByPrimaryKeySql = string.Format("select * from \"{0}\" limit 1", TableName);
            }

            _insertColumns = Columns.Where(c => !c.IsAutoInc).ToArray();
            _insertOrReplaceColumns = Columns.ToArray();
        }

        private IReadOnlyCollection<MemberInfo> GetPublicMembers(
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
#endif
            Type type)
        {
            if (type.Name.StartsWith("ValueTuple`"))
                return GetFieldsFromValueTuple(type);

            var members = new List<MemberInfo>();
            var memberNames = new HashSet<string>();
            var newMembers = new List<MemberInfo>();
            do
            {
                var ti = type.GetTypeInfo();
                newMembers.Clear();

                newMembers.AddRange(
                    from p in ti.DeclaredProperties
                    where !memberNames.Contains(p.Name) &&
                          p.CanRead && p.CanWrite &&
                          p.GetMethod != null && p.SetMethod != null &&
                          p.GetMethod.IsPublic && p.SetMethod.IsPublic &&
                          !p.GetMethod.IsStatic && !p.SetMethod.IsStatic
                    select p);

                members.AddRange(newMembers);
                foreach (var m in newMembers)
                    memberNames.Add(m.Name);

                type = ti.BaseType;
            } while (type != typeof(object));

            return members;
        }

        private IReadOnlyCollection<MemberInfo> GetFieldsFromValueTuple(
#if NET8_0_OR_GREATER
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicFields)]
#endif
            Type type)
        {
            Method = MapMethod.ByPosition;
            var fields = type.GetFields();

            // https://docs.microsoft.com/en-us/dotnet/api/system.valuetuple-8.rest
            if (fields.Length >= 8)
                throw new NotSupportedException(
                    "ValueTuple with more than 7 members not supported due to nesting; see https://docs.microsoft.com/en-us/dotnet/api/system.valuetuple-8.rest");

            return fields;
        }

        public bool HasAutoIncPK { get; private set; }

        public void SetAutoIncPK(object obj, long id)
        {
            if (_autoPk != null)
            {
                _autoPk.SetValue(obj, Convert.ChangeType(id, _autoPk.ColumnType, null));
            }
        }

        public Column[] InsertColumns
        {
            get { return _insertColumns; }
        }

        public Column[] InsertOrReplaceColumns
        {
            get { return _insertOrReplaceColumns; }
        }

        public Column FindColumnWithPropertyName(string propertyName)
        {
            var exact = Columns.FirstOrDefault(c => c.PropertyName == propertyName);
            return exact;
        }

        public Column FindColumn(string columnName)
        {
            if (Method != MapMethod.ByName)
                throw new InvalidOperationException(
                    $"This {nameof(TableMapping)} is not mapped by name, but {Method}.");

            var exact = Columns.FirstOrDefault(c => c.Name.ToLower() == columnName.ToLower());
            return exact;
        }

        public class Column
        {
            MemberInfo _member;

            public string Name { get; private set; }

            public PropertyInfo PropertyInfo => _member as PropertyInfo;

            public string PropertyName
            {
                get { return _member.Name; }
            }

            public Type ColumnType { get; private set; }

            public string Collation { get; private set; }

            public bool IsAutoInc { get; private set; }
            public bool IsAutoGuid { get; private set; }

            public bool IsPK { get; private set; }

            public IEnumerable<IndexedAttribute> Indices { get; set; }

            public bool IsNullable { get; private set; }

            public int? MaxStringLength { get; private set; }

            public bool StoreAsText { get; private set; }

            public Column(MemberInfo member, CreateFlags createFlags = CreateFlags.None)
            {
                _member = member;
                var memberType = GetMemberType(member);

                var colAttr = member.CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(ColumnAttribute));
#if ENABLE_IL2CPP
				var ca = member.GetCustomAttribute(typeof(ColumnAttribute)) as ColumnAttribute;
				Name = ca == null ? member.Name : ca.Name;
#else
                Name = (colAttr != null && colAttr.ConstructorArguments.Count > 0)
                    ? colAttr.ConstructorArguments[0].Value?.ToString()
                    : member.Name;
#endif
                //If this type is Nullable<T> then Nullable.GetUnderlyingType returns the T, otherwise it returns null, so get the actual type instead
                ColumnType = Nullable.GetUnderlyingType(memberType) ?? memberType;
                Collation = Orm.Collation(member);

                IsPK = Orm.IsPK(member) ||
                       (((createFlags & CreateFlags.ImplicitPK) == CreateFlags.ImplicitPK) &&
                        string.Compare(member.Name, Orm.ImplicitPkName, StringComparison.OrdinalIgnoreCase) == 0);

                var isAuto = Orm.IsAutoInc(member) ||
                             (IsPK && ((createFlags & CreateFlags.AutoIncPK) == CreateFlags.AutoIncPK));
                IsAutoGuid = isAuto && ColumnType == typeof(Guid);
                IsAutoInc = isAuto && !IsAutoGuid;

                Indices = Orm.GetIndices(member);
                if (!Indices.Any()
                    && !IsPK
                    && ((createFlags & CreateFlags.ImplicitIndex) == CreateFlags.ImplicitIndex)
                    && Name.EndsWith(Orm.ImplicitIndexSuffix, StringComparison.OrdinalIgnoreCase)
                )
                {
                    Indices = new IndexedAttribute[] {new IndexedAttribute()};
                }

                IsNullable = !(IsPK || Orm.IsMarkedNotNull(member));
                MaxStringLength = Orm.MaxStringLength(member);

                StoreAsText = memberType.GetTypeInfo().CustomAttributes
                    .Any(x => x.AttributeType == typeof(StoreAsTextAttribute));
            }

            public Column(PropertyInfo member, CreateFlags createFlags = CreateFlags.None)
                : this((MemberInfo) member, createFlags)
            {
            }

            public void SetValue(object obj, object val)
            {
                if (_member is PropertyInfo propy)
                {
                    if (val != null && ColumnType.GetTypeInfo().IsEnum)
                        propy.SetValue(obj, Enum.ToObject(ColumnType, val));
                    else
                        propy.SetValue(obj, val);
                }
                else if (_member is FieldInfo field)
                {
                    if (val != null && ColumnType.GetTypeInfo().IsEnum)
                        field.SetValue(obj, Enum.ToObject(ColumnType, val));
                    else
                        field.SetValue(obj, val);
                }
                else
                    throw new InvalidProgramException("unreachable condition");
            }

            public object GetValue(object obj)
            {
                if (_member is PropertyInfo propy)
                    return propy.GetValue(obj);
                else if (_member is FieldInfo field)
                    return field.GetValue(obj);
                else
                    throw new InvalidProgramException("unreachable condition");
            }

            private static Type GetMemberType(MemberInfo m)
            {
                switch (m.MemberType)
                {
                    case MemberTypes.Property: return ((PropertyInfo) m).PropertyType;
                    case MemberTypes.Field: return ((FieldInfo) m).FieldType;
                    default:
                        throw new InvalidProgramException(
                            $"{nameof(TableMapping)} supports properties or fields only.");
                }
            }
        }

        internal enum MapMethod
        {
            ByName,
            ByPosition
        }
    }
}