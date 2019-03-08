using System;
using Microsoft.Build.Framework;

namespace PsMsBuildHelper
{
    public class ProjectStopwatch : KeyedStopwatchItem<int, int, TargetStopwatch>
        {
            public TargetStopwatch CurrentTarget { get { return Current; } }
            public TaskStopwatch CurrentTask
            {
                get
                {
                    TargetStopwatch current = Current;
                    if (current == null)
                        return null;
                    return current.CurrentTask;
                }
            }
            public ProjectStopwatch(int id) : base(id) { }
            protected override TargetStopwatch CreateNew(int id) { return new TargetStopwatch(id); }
            public TargetStopwatch StartNewTarget(BuildEventContext context, bool doNotSetCurrent = false) { return StartItem(context.TargetId, doNotSetCurrent); }
            public TaskStopwatch StartNewTask(BuildEventContext context, bool doNotSetCurrent = false)
            {
                return StartNewTarget(context, doNotSetCurrent).StartItem(context.TaskId, doNotSetCurrent);
            }
            public TimeSpan StopTarget(BuildEventContext context) { return StopAndRemove(context.TargetId); }
            public TimeSpan StopTask(BuildEventContext context) { return GetValue(context.TargetId, i => i.StopAndRemove(context.TaskId), () => TimeSpan.Zero); }

            public override int GetHashCode() { return ID; }
        }
}