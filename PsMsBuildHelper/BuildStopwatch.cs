using System;
using Microsoft.Build.Framework;

namespace PsMsBuildHelper
{
    public class BuildStopwatch : KeyedStopwatchItem<long, int, ProjectStopwatch>
        {
            public ProjectStopwatch CurrentProject { get { return Current; } }
            public TargetStopwatch CurrentTarget
            {
                get
                {
                    ProjectStopwatch current = Current;
                    if (current == null)
                        return null;
                    return current.CurrentTarget;
                }
            }
            public TaskStopwatch CurrentTask
            {
                get
                {
                    ProjectStopwatch current = Current;
                    if (current == null)
                        return null;
                    return current.CurrentTask;
                }
            }
            public BuildStopwatch(long id) : base(id) { }
            protected override ProjectStopwatch CreateNew(int id) { return new ProjectStopwatch(id); }
            public ProjectStopwatch StartNewProject(BuildEventContext context, bool doNotSetCurrent = false) { return StartItem(context.ProjectContextId, doNotSetCurrent); }
            public TargetStopwatch StartNewTarget(BuildEventContext context, bool doNotSetCurrent = false)
            {
                return StartNewProject(context, doNotSetCurrent).StartNewTarget(context, doNotSetCurrent);
            }
            public TaskStopwatch StartNewTask(BuildEventContext context, bool doNotSetCurrent = false)
            {
                return StartNewProject(context, doNotSetCurrent).StartNewTask(context, doNotSetCurrent);
            }
            public TimeSpan StopProject(BuildEventContext context) { return StopAndRemove(context.ProjectContextId); }
            public TimeSpan StopTarget(BuildEventContext context) { return GetValue(context.ProjectContextId, i => i.StopAndRemove(context.TargetId), () => TimeSpan.Zero); }
            public TimeSpan StopTask(BuildEventContext context) { return GetValue(context.ProjectContextId, i => i.StopTask(context), () => TimeSpan.Zero); }

            public override int GetHashCode() { return ID.GetHashCode(); }
        }
}