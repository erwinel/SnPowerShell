using System;

namespace PsMsBuildHelper
{
    public abstract class KeyedStopwatchItem<TCurrentKey, TChildKey, TItem> : StopwatchItemDictionary<TChildKey, TItem>, IStopwatchItem<TCurrentKey>, IEquatable<KeyedStopwatchItem<TCurrentKey, TChildKey, TItem>>
        where TChildKey : struct, IComparable, IFormattable, IConvertible, IComparable<TChildKey>, IEquatable<TChildKey>
        where TCurrentKey : struct, IComparable, IFormattable, IConvertible, IComparable<TCurrentKey>, IEquatable<TCurrentKey>
        where TItem : class, IStopwatchItem<TChildKey>
    {
        public TCurrentKey ID { get; private set; }
        public KeyedStopwatchItem(TCurrentKey id) { ID = id; }
        public bool Equals(KeyedStopwatchItem<TCurrentKey, TChildKey, TItem> other) { return (other != null && ReferenceEquals(this, other)); }
        public override bool Equals(object obj) { return (obj != null && obj is KeyedStopwatchItem<TCurrentKey, TChildKey, TItem> && ReferenceEquals(this, obj)); }
        public abstract override int GetHashCode();
        public override string ToString() { return ID.ToString(); }
    }
}