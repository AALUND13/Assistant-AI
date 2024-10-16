using System.Xml.Serialization;

namespace AssistantAI.Resources;

#nullable disable

[XmlRoot("Personalitys")]
public class Personalitys {
    [XmlElement("CurrentPersonality")]
    public string CurrentPersonality { get; set; }

    [XmlElement("Personality")]
    public List<Personality> PersonalityList { get; set; }
}

public class Personality {
    [XmlElement("Name")]
    public string Name { get; set; }

    [XmlElement("MainPrompt")]
    public string MainPrompt { get; set; }

    [XmlElement("ReplyDecisionPrompt")]
    public string ReplyDecisionPrompt { get; set; }
}

#nullable restore