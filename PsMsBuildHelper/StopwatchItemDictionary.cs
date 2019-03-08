using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Diagnostics;

namespace PsMsBuildHelper
{
    public abstract class StopwatchItemDictionary<TKey, TItem> : KeyedCollection<TKey, TItem>, IStopwatchItem
        where TKey : struct, IComparable, IFormattable, IConvertible, IComparable<TKey>, IEquatable<TKey>
        where TItem : class, IStopwatchItem<TKey>
    {
        private List<TItem> _currencyList = new List<TItem>();
        public TItem Current
        {
            get
            {
                Monitor.Enter(_currencyList);
                try
                {
                    if (_currencyList.Count == 0)
                        return null;
                    return _currencyList[0];
                }
                finally { Monitor.Exit(_currencyList); }
            }
        }
        public TimeSpan Elapsed { get { return _stopwatch.Elapsed; } }
        public bool IsRunning { get { return _stopwatch.IsRunning; } }
        private Stopwatch _stopwatch = Stopwatch.StartNew();
        public Stopwatch GetStopwatch() { return _stopwatch; }
        public StopwatchItemDictionary() { }
        public TimeSpan Stop()
        {
            Monitor.Enter(_currencyList);
            try
            {
                if (_stopwatch.IsRunning)
                    _stopwatch.Stop();
                foreach (TItem item in base.Items)
                    item.Stop();
                return _stopwatch.Elapsed;
            }
            finally { Monitor.Exit(_currencyList); }
        }
        public void Start()
        {
            Monitor.Enter(_currencyList);
            try
            {
                if (!_stopwatch.IsRunning)
                    _stopwatch.Start();
                foreach (TItem item in base.Items)
                    item.Start();
            }
            finally { Monitor.Exit(_currencyList); }
        }
        protected override TKey GetKeyForItem(TItem item)
        {
            if (item == null)
                throw new ArgumentNullException("item");
            return item.ID;
        }
        protected override void ClearItems()
        {
            Monitor.Enter(_currencyList);
            try
            {
                foreach (TItem item in base.Items)
                    item.Stop();
                base.ClearItems();
                _currencyList.Clear();
            }
            finally { Monitor.Exit(_currencyList); }
        }
        protected override void InsertItem(int index, TItem item)
        {
            if (item == null)
                throw new ArgumentNullException("item");
            Monitor.Enter(_currencyList);
            try
            {
                if (Contains(item.ID))
                    throw new ArgumentOutOfRangeException("Item key already exists");
                base.InsertItem(index, item);
                item.Start();
                if (!_currencyList.Contains(item))
                    _currencyList.Add(item);
            }
            finally { Monitor.Exit(_currencyList); }
        }
        protected override void RemoveItem(int index)
        {
            Monitor.Enter(_currencyList);
            try
            {
                TItem item = base.Items[index];
                item.Stop();
                base.RemoveItem(index);
                _currencyList.Remove(item);
            }
            finally { Monitor.Exit(_currencyList); }
        }
        protected override void SetItem(int index, TItem item)
        {
            if (item == null)
                throw new ArgumentNullException("item");
            Monitor.Enter(_currencyList);
            try
            {
                TItem oldItem = base.Items[index];
                if (Contains(item.ID) && !oldItem.ID.Equals(item.ID))
                    throw new ArgumentOutOfRangeException("Item key already exists");
                base.SetItem(index, item);
                if (ReferenceEquals(item, oldItem))
                    return;
                oldItem.Stop();
                item.Start();
                if (!_currencyList.Contains(item))
                    _currencyList.Add(item);
                _currencyList.Remove(oldItem);
            }
            finally { Monitor.Exit(_currencyList); }
        }
        public TimeSpan StopAndRemove(TKey key)
        {
            Monitor.Enter(_currencyList);
            try
            {
                if (!Contains(key))
                    return TimeSpan.Zero;
                TItem item = this[key];
                TimeSpan result = item.Stop();
                Remove(key);
                return result;
            }
            finally { Monitor.Exit(_currencyList); }
        }
        protected abstract TItem CreateNew(TKey id);
        public TItem StartItem(TKey key, bool doNotSetCurrent = false)
        {
            TItem item;
            Monitor.Enter(_currencyList);
            try
            {
                if (Contains(key))
                {
                    item = this[key];
                    item.Start();
                    if (doNotSetCurrent)
                        return item;
                    _currencyList.Remove(item);
                    _currencyList.Insert(0, item);
                }
                else
                {
                    item = CreateNew(key);
                    if (!doNotSetCurrent)
                        _currencyList.Insert(0, item);
                    item.Start();
                    this.Add(item);
                }
            }
            finally { Monitor.Exit(_currencyList); }
            return item;
        }
        public TResult GetValue<TResult>(TKey key, Func<TItem, TResult> getValue, Func<TResult> defaultValue)
        {
            Monitor.Enter(_currencyList);
            try
            {
                if (Contains(key))
                    return getValue(this[key]);
            }
            finally { Monitor.Exit(_currencyList); }
            return defaultValue();
        }
    }
}