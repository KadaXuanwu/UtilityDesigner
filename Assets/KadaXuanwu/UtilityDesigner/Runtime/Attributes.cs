using System;

namespace KadaXuanwu.UtilityDesigner.Runtime {
    // Where the editor lists a custom node, under its kind: [CategoryPath("Combat/Melee")].
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class CategoryPathAttribute : Attribute {
        public string SubCategoryPath { get; }

        public CategoryPathAttribute(string subCategoryPath) {
            SubCategoryPath = subCategoryPath;
        }
    }

    // On a node's int field: an index into the SceneReferences list of that type. The editor shows it as a dropdown of
    // the list.
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SceneRefAttribute : Attribute {
        public Type Type { get; }
        public string Label { get; }

        public SceneRefAttribute(Type type, string label) {
            Type = type;
            Label = label;
        }
    }
}
