using System.Collections;
using System.Collections.Concurrent;

namespace AssistantAI.AiModule.Utilities;

public class EventList<T> : IEnumerable<T> where T : notnull {
    public int MaxItems = 100;

    public readonly List<T> Items = [];

    public event Action<T>? OnItemAdded;
    public event Action<T>? OnItemRemoved;

    public EventList() { }
    public EventList(int maxItems) {
        MaxItems = maxItems;
    }

    public void AddItem(T item) {
        while(Items.Count >= MaxItems) {
            RemoveItem();
        }

        Items.Add(item);
        OnItemAdded?.Invoke(item);
    }

    public bool RemoveItem() {
        if(Items.Count > 0) {
            T? removeItem = Items[0];
            Items.RemoveAt(0);
            OnItemRemoved?.Invoke(removeItem);
            return true;
        }
        return false;
    }

    public bool RemoveItem(T item) {
        T? removeItem = Items.FirstOrDefault(i => i.Equals(item));
        if(removeItem != null) {
            Items.Remove(removeItem);
            OnItemRemoved?.Invoke(removeItem);
            return true;
        }

        return false;
    }

    public IEnumerator<T> GetEnumerator() {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
