using AssistantAI.Utilities.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AssistantAI.Utilities.OptionPreviews {
    internal class ChannelMentionPreview : IOptionPreview<ChannelMention> {
        public string GetPreview(ChannelMention value) {
            return $"<#{value.ChannelId}>";
        }

        public (ChannelMention?, bool) Parse(string value) {
            Regex regex = new Regex(@"<#(\d+)>", RegexOptions.Compiled);
            bool canParse = ulong.TryParse(regex.Match(value).Groups[1].Value, out ulong result);
            return (new ChannelMention { ChannelId = result }, canParse);
        }
    }
}
