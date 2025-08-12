using System;

namespace DatabaseManager.SQLite
{
    [AttributeUsage(AttributeTargets.Property)]
    public class AutoIncrementAttribute : Attribute
    {
    }
}