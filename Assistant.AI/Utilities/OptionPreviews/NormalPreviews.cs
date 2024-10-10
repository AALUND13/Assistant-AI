using AssistantAI.Utilities.Extensions;
using AssistantAI.Utilities.Interfaces;

namespace AssistantAI.Utilities.OptionPreviews {
    public class BoolPreview : IOptionPreview<bool> {
        public string GetPreview(bool value) {
            return value ? "Enabled" : "Disabled";
        }

        public (bool, bool) Parse(string value) {
            if(value.ToLower() == "enabled") {
                return (true, true);
            } else if(value.ToLower() == "disabled") {
                return (false, true);
            }

            bool success = bool.TryParse(value, out bool result);
            return (result, success);
        }
    }

    public class IntPreview : IOptionPreview<int> {
        public string GetPreview(int value) {
            return value.ToString().Truncate(20);
        }

        public (int, bool) Parse(string value) {
            bool success = int.TryParse(value, out int result);
            return (result, success);
        }
    }

    public class StringPreview : IOptionPreview<string> {
        public string GetPreview(string value) {
            return value.Truncate(20);
        }

        public (string, bool) Parse(string value) {
            return (value, true);
        }
    }
}
