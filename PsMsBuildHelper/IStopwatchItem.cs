using System;

namespace PsMsBuildHelper
{
        public interface IStopwatchItem
        {
            TimeSpan Elapsed { get; }
            bool IsRunning { get; }
            TimeSpan Stop();
            void Start();
        }
        public interface IStopwatchItem<T> : IStopwatchItem
            where T : struct, IComparable, IFormattable, IConvertible, IComparable<T>, IEquatable<T>
        {
            T ID { get; }
        }
}