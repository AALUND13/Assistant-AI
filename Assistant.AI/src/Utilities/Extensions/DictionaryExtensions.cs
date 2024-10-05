namespace AssistantAI.Utilities.Extension;

public static class DictionaryExtensions {
    public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default!) where TKey : notnull {
        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public static TValue? SetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue?> dictionary, TKey key, TValue? value) where TKey : notnull {
        if(!dictionary.TryAdd(key, value)) {
            dictionary[key] = value;
        }

        return value;
    }
}
