using System;
using System.Diagnostics;

namespace PsMsBuildHelper
{
    public class TaskStopwatch : IStopwatchItem<int>, IEquatable<TaskStopwatch>
        {
            public int ID { get; private set; }
            public TimeSpan Elapsed { get { return _stopwatch.Elapsed; } }
            public bool IsRunning { get { return _stopwatch.IsRunning; } }
            private Stopwatch _stopwatch = Stopwatch.StartNew();
            public TimeSpan Stop()
            {
                if (_stopwatch.IsRunning)
                    _stopwatch.Stop();
                return _stopwatch.Elapsed;
            }
            public void Start()
            {
                if (!_stopwatch.IsRunning)
                    _stopwatch.Start();
            }
            public bool Equals(TaskStopwatch other) { return (other != null && ReferenceEquals(this, other)); }
            public override bool Equals(object obj) { return (obj != null && obj is TaskStopwatch && ReferenceEquals(this, obj)); }
            public override int GetHashCode() { return ID; }
            public override string ToString() { return ID.ToString(); }
            public TaskStopwatch(int id) { ID = id;}
        }
}