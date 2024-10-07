using System.Collections;
using System.Collections.Concurrent;

namespace AssistantAI.AiModule.Utilities;

public class EventQueue<T> : IEnumerable<T> where T : notnull {
    public int MaxItems;

    public readonly ConcurrentQueue<T> Items = [];

    public event Action<T>? OnItemAdded;
    public event Action<T>? OnItemRemoved;

    public EventQueue(int maxItems) {
        MaxItems = maxItems;
    }

    public void AddItem(T item) {
        while(Items.Count >= MaxItems) {
            Items.TryDequeue(out T? removeItem);
            if(removeItem != null) {
                OnItemRemoved?.Invoke(removeItem);
            }
        }

        Items.Enqueue(item);
        OnItemAdded?.Invoke(item);
    }

    public void RemoveItem() {
        Items.TryDequeue(out T? removeItem);
        if(removeItem != null) {
            OnItemRemoved?.Invoke(removeItem);
        }
    }

    public IEnumerator<T> GetEnumerator() {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
