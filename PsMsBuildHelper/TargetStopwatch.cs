using System;
using Microsoft.Build.Framework;

namespace PsMsBuildHelper
{
    public class TargetStopwatch : KeyedStopwatchItem<int, int, TaskStopwatch>
        {
            public TaskStopwatch CurrentTask { get { return Current; } }
            public TargetStopwatch(int id) : base(id) { }
            protected override TaskStopwatch CreateNew(int id) { return new TaskStopwatch(id); }
            public TaskStopwatch StartNewTask(BuildEventContext context, bool doNotSetCurrent = false) { return StartItem(context.TaskId, doNotSetCurrent); }
            public TimeSpan StopTask(BuildEventContext context) { return StopAndRemove(context.TaskId); }

            public override int GetHashCode() { return ID; }
        }
}