using AssistantAI.Utilities.Extensions;
using AssistantAI.Utilities.Interfaces;
using System.Text.RegularExpressions;

namespace AssistantAI.Utilities.OptionPreviews {
    public class ListPreview<T> : IOptionPreview<List<T>> {
        public string GetPreview(List<T> value) {
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
            return $"[{resultString.TrimEnd(", ".ToCharArray())}]";
            

        }

        public (List<T>?, bool) Parse(string value) {
            List<string> result = value.Split(',').Select(v => v.Trim()).ToList();

            bool canParse = result.All(v => {
                Type type = typeof(T);
                switch(type.Name) {
                    case nameof(Int32):
                        return int.TryParse(v, out _);
                    case nameof(Int64):
                        return long.TryParse(v, out _);
                    case nameof(UInt32):
                        return uint.TryParse(v, out _);
                    case nameof(UInt64):
                        return ulong.TryParse(v, out _);
                    case nameof(Boolean):
                        return bool.TryParse(v, out _);
                    case nameof(String):
                        return true;
                    default:
                        return false;
                }
            });

            if(canParse) {
                return (result.Select(v => (T)Convert.ChangeType(v, typeof(T))).ToList(), true);
            }

            return (null, false);
        }
    }
}
