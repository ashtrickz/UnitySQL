using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DatabaseManager.SQLite
{
    class EnumCacheInfo
    {
        public EnumCacheInfo(Type type)
        {
            var typeInfo = type.GetTypeInfo();

            IsEnum = typeInfo.IsEnum;

            if (IsEnum)
            {
                StoreAsText = typeInfo.CustomAttributes.Any(x => x.AttributeType == typeof(StoreAsTextAttribute));

                if (StoreAsText)
                {
                    EnumValues = new Dictionary<int, string>();
#if NET8_0_OR_GREATER
					foreach (object e in Enum.GetValuesAsUnderlyingType (type)) {
						EnumValues[Convert.ToInt32 (e)] = Enum.ToObject(type, e).ToString ();
					}
#else
                    foreach (object e in Enum.GetValues(type))
                    {
                        EnumValues[Convert.ToInt32(e)] = e.ToString();
                    }
#endif
                }
            }
        }

        public bool IsEnum { get; private set; }

        public bool StoreAsText { get; private set; }

        public Dictionary<int, string> EnumValues { get; private set; }
    }
}