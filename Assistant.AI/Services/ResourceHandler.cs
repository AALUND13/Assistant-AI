using System.Xml.Serialization;

namespace AssistantAI.Services;

public class ResourceHandler<T> {
    public T Resource { get; private set; }

    public void LoadResource(string path) {
        XmlSerializer serializer = new XmlSerializer(typeof(T));
        using FileStream fileStream = new FileStream(path, FileMode.Open);
        var resource = (T?)serializer.Deserialize(fileStream);

        if(resource == null)
            throw new Exception("Failed to load resource.");

        Resource = resource;
    }
}
