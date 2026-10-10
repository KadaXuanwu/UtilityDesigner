using System;

namespace KadaXuanwu.UtilityDesigner.Scripts
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class CategoryPathAttribute : Attribute
    {
        public string SubCategoryPath { get; }

        public CategoryPathAttribute(string subCategoryPath)
        {
            SubCategoryPath = subCategoryPath;
        }
    }
}