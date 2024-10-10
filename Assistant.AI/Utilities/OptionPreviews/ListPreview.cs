using AssistantAI.Utilities.Extensions;
using AssistantAI.Utilities.Interfaces;
using System.Text.RegularExpressions;

namespace AssistantAI.Utilities.OptionPreviews {
    public class ListPreview<T> : IOptionPreview<IEnumerable<T>> {
        public string GetPreview(IEnumerable<T> value) {
            int totalLength = 0;
            string resultString = "";
            foreach(T item in value) {
                string itemAsString = item.ToString();
                if(totalLength + itemAsString.Length > 100) {
                    resultString += "...";
                    break;
                }
                resultString += itemAsString + ", ";
                totalLength += itemAsString.Length + 2;
            }
            return $"[{resultString}]";
            

        }

        public (IEnumerable<T>?, bool) Parse(string value) {
            IEnumerable<string> result = value.Split(',').Select(v => v.Trim());

            bool canParse = result.All(v => {
                Type type = typeof(T);
                switch(type.Name) {
                    case "Int32":
                        return int.TryParse(v, out _);
                    case "Int64":
                        return long.TryParse(v, out _);
                    case "UInt32":
                        return uint.TryParse(v, out _);
                    case "UInt64":
                        return ulong.TryParse(v, out _);
                    case "Boolean":
                        return bool.TryParse(v, out _);
                    case "String":
                        return true;
                    default:
                        return false;
                }
            });

            if(canParse) {
                return (result.Select(v => (T)Convert.ChangeType(v, typeof(T))), true);
            }

            return (null, false);
        }
    }
}
