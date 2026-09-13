using System;
using System.Collections.Generic;
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

        public static string SanitizeVariantName(string variantName)
        {
            string source = (variantName ?? string.Empty).Trim();
            if (source.Length == 0) return "Variant";

            var safe = new char[Math.Min(source.Length, 80)];
            int count = 0;
            for (int i = 0; i < source.Length && count < safe.Length; i++)
            {
                char c = source[i];
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-')
                {
                    safe[count++] = c;
                }
                else
                {
                    safe[count++] = '_';
                }
            }

            string result = new string(safe, 0, count).Trim().Trim('.');
            while (result.Contains("__")) result = result.Replace("__", "_");
            if (result.Length == 0 || result == "." || result == "..") return "Variant";
            return result;
        }

        public static string GetVariantPath(string projectRoot, string variantName)
        {
            string directory = GetVariantsDirectory(projectRoot);
            string safeName = SanitizeVariantName(variantName);
            string candidate = Path.GetFullPath(Path.Combine(directory, safeName + ".json"));
            string prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                            Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Workshop variant path escaped the editor-local variants directory.");
            return candidate;
        }

        public static void SaveVariant(string projectRoot, string variantName, VnPresentationWorkshopPreset preset)
        {
            string path = GetVariantPath(projectRoot, variantName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, VnPresentationWorkshopSerialization.Serialize(preset, SanitizeVariantName(variantName)));
        }

        public static VnWorkshopImportResult LoadVariant(string projectRoot, string variantName)
        {
            string path = GetVariantPath(projectRoot, variantName);
            if (!File.Exists(path)) return VnWorkshopImportResult.Failed("Workshop variant does not exist: " + variantName + ".");
            return VnPresentationWorkshopSerialization.Deserialize(File.ReadAllText(path));
        }

        public static bool DeleteVariant(string projectRoot, string variantName)
        {
            string path = GetVariantPath(projectRoot, variantName);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        public static void RenameVariant(string projectRoot, string currentName, string newName)
        {
            string source = GetVariantPath(projectRoot, currentName);
            string destination = GetVariantPath(projectRoot, newName);
            if (!File.Exists(source)) throw new FileNotFoundException("Workshop variant does not exist.", source);
            if (File.Exists(destination)) throw new IOException("A Workshop variant already exists with that name.");
            File.Move(source, destination);
        }

        public static void DuplicateVariant(string projectRoot, string sourceName, string duplicateName)
        {
            string source = GetVariantPath(projectRoot, sourceName);
            string destination = GetVariantPath(projectRoot, duplicateName);
            if (!File.Exists(source)) throw new FileNotFoundException("Workshop variant does not exist.", source);
            if (File.Exists(destination)) throw new IOException("A Workshop variant already exists with that name.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, false);
        }

        public static string[] ListVariants(string projectRoot)
        {
            string directory = GetVariantsDirectory(projectRoot);
            if (!Directory.Exists(directory)) return new string[0];
            string[] files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            var variants = new List<string>(files.Length);
            for (int i = 0; i < files.Length; i++) variants.Add(Path.GetFileNameWithoutExtension(files[i]));
            variants.Sort(StringComparer.OrdinalIgnoreCase);
            return variants.ToArray();
        }
    }
}
