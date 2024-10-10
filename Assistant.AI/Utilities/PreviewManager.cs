using AssistantAI.Utilities.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace AssistantAI.Utilities {
    public static class PreviewManager {
        public static ConcurrentDictionary<Type, object> Previews { get; } = new ConcurrentDictionary<Type, object>();

        public static void RegisterPreview<T>(IOptionPreview<T> preview) {
            Previews.TryAdd(typeof(T), preview);
        }

        public static IOptionPreview<T>? GetPreview<T>() {
            if(Previews.TryGetValue(typeof(T), out var preview)) {
                return (IOptionPreview<T>)preview;
            }
            return null;
        }
    }
}
