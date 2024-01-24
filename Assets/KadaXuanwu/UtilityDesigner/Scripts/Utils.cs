using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KadaXuanwu.UtilityDesigner.Scripts.Evaluation;

namespace KadaXuanwu.UtilityDesigner.Scripts
{
    public static class Utils
    {
        internal static string VerifyItemName<T>(string prefix, string newDesignation, IEnumerable<T> collection,
            Func<T, string> getDesignation, string oldName = "")
        {
            var enumerable = collection as T[] ?? collection.ToArray();
            newDesignation = newDesignation.Replace(" ", "").Equals("")
                ? $"{prefix}{enumerable.Length}"
                : newDesignation;

            int indexIncrease = 0;
            while (enumerable.Any(item => getDesignation(item).Equals(newDesignation)) &&
                   newDesignation != oldName && indexIncrease < 10000)
            {
                indexIncrease++;
                newDesignation = $"{prefix}{enumerable.Length + indexIncrease}";
            }

            return newDesignation;
        }
        
        internal static string CamelCaseToReadable(string camelCase)
        {
            if (string.IsNullOrEmpty(camelCase))
                return string.Empty;

            var result = new StringBuilder();
            result.Append(char.ToUpper(camelCase[0]));

            for (int i = 1; i < camelCase.Length; i++)
            {
                char currentChar = camelCase[i];
                if (char.IsUpper(currentChar))
                    result.Append(' ');
                
                result.Append(currentChar);
            }

            return result.ToString();
        }
        
        internal static string AddSpacesBeforeUppercase(string input)
        {
            return string.Concat(input.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
        }

        internal static Consideration FindConsideration(List<ConsiderationSet> considerationSets, string designation, KadaXuanwu.UtilityDesigner.Scripts.UtilityDesigner utilityDesigner = null)
        {
            string[] parts = designation.Split(" | ");
            if (parts.Length != 2)
                return null;

            ConsiderationSet selectedConsiderationSet = considerationSets.FirstOrDefault(cs => cs.name == parts[0]);
            if (selectedConsiderationSet == null)
                return null;

            if (selectedConsiderationSet.local && utilityDesigner != null)
            {
                return utilityDesigner.localConsiderationSets[selectedConsiderationSet].considerations.FirstOrDefault(
                    consideration => consideration.designation == parts[1]);
            }

            return selectedConsiderationSet.considerations.FirstOrDefault(
                consideration => consideration.designation == parts[1]);
        }
    }
}
