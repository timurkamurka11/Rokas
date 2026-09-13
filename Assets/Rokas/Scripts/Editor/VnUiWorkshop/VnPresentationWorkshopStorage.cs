using System;
using System.IO;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static class VnPresentationWorkshopStorage
    {
        private static readonly string[] VariantPathParts =
        {
            "Library", "ROKAS", "VnUiWorkshop", "variants"
        };

        public static string GetVariantsDirectory(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot)) throw new ArgumentException("Project root is required.", "projectRoot");
            string path = projectRoot;
            for (int i = 0; i < VariantPathParts.Length; i++)
            {
                path = Path.Combine(path, VariantPathParts[i]);
            }
            return Path.GetFullPath(path);
        }
    }
}
