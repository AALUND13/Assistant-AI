using AssistantAI.Utilities.Extensions;
using AssistantAI.Utilities.Interfaces;
using System.Text.RegularExpressions;

namespace AssistantAI.Utilities.OptionPreviews {
    public class ListPreview<T> : IOptionPreview<List<T>> {
        public string GetPreview(List<T> value) {
            int totalLength = 0;
            string resultString = "";
            foreach(T item in value) {
                string itemAsString = PreviewManager.GetPreview<T>()?.GetPreview(item) ?? item?.ToString() ?? "Null";
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
            Type type = typeof(T);

            bool canParse = result.All(v => {
                return type.Name switch {
                    nameof(Int32) => int.TryParse(v, out _),
                    nameof(Int64) => long.TryParse(v, out _),
                    nameof(UInt32) => uint.TryParse(v, out _),
                    nameof(UInt64) => ulong.TryParse(v, out _),
                    nameof(Boolean) => bool.TryParse(v, out _),
                    nameof(String) => true,
                    _ => PreviewManager.GetPreview<T>()?.Parse(v).Item2 ?? false,
                };
            });

            if(canParse) {
                if(PreviewManager.GetPreview<T>()?.Parse(value).Item2 ?? false) {
                    return (result.Select(v => PreviewManager.GetPreview<T>()!.Parse(v).Item1!).ToList(), true);
                }
                return (result.Select(v => (T)Convert.ChangeType(v, typeof(T))).ToList(), true);
            }

            return (null, false);
        }

    }
}
