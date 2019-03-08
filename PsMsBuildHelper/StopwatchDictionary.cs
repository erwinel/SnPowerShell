using System;
using Microsoft.Build.Framework;

namespace PsMsBuildHelper
{
    public class StopwatchDictionary : StopwatchItemDictionary<long, BuildStopwatch>
        {
            public BuildStopwatch CurrentBuild { get { return Current; } }
            public ProjectStopwatch CurrentProject
            {
                get
                {
                    BuildStopwatch current = Current;
                    if (current == null)
                        return null;
                    return current.CurrentProject;
                }
            }
            public TargetStopwatch CurrentTarget
            {
                get
                {
                    BuildStopwatch current = Current;
                    if (current == null)
                        return null;
                    return current.CurrentTarget;
                }
            }
            public TaskStopwatch CurrentTask
            {
                get
                {
                    BuildStopwatch current = Current;
                    if (current == null)
                        return null;
                    return current.CurrentTask;
                }
            }
            protected override BuildStopwatch CreateNew(long id) { return new BuildStopwatch(id); }
            public BuildStopwatch StartNewBuild(BuildEventContext context, bool doNotSetCurrent = false) { return StartItem((context == null) ? -1 : context.ProjectContextId, doNotSetCurrent); }
            public ProjectStopwatch StartNewProject(BuildEventContext context, bool doNotSetCurrent = false)
            {
                return StartNewBuild(context, doNotSetCurrent).StartNewProject(context, doNotSetCurrent);
            }
            public TargetStopwatch StartNewTarget(BuildEventContext context, bool doNotSetCurrent = false)
            {
                return StartNewBuild(context, doNotSetCurrent).StartNewTarget(context, doNotSetCurrent);
            }
            public TaskStopwatch StartNewTask(BuildEventContext context, bool doNotSetCurrent = false)
            {
                return StartNewBuild(context, doNotSetCurrent).StartNewTask(context, doNotSetCurrent);
            }
            public TimeSpan StopBuild(BuildEventContext context) { return StopAndRemove((context == null) ? -1 : context.BuildRequestId); }
            public TimeSpan StopProject(BuildEventContext context) { return GetValue((context == null) ? -1 : context.ProjectContextId, i => i.StopAndRemove((context == null) ? -1 : context.ProjectContextId), () => TimeSpan.Zero); }
            public TimeSpan StopTarget(BuildEventContext context) { return GetValue((context == null) ? -1 : context.ProjectContextId, i => i.StopTarget(context), () => TimeSpan.Zero); }
            public TimeSpan StopTask(BuildEventContext context) { return GetValue((context == null) ? -1 : context.ProjectContextId, i => i.StopTask(context), () => TimeSpan.Zero); }
        }
}