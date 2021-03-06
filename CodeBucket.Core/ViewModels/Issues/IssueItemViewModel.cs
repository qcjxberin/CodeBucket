﻿using CodeBucket.Client.V1;
using Humanizer;
using ReactiveUI;

namespace CodeBucket.Core.ViewModels.Issues
{
    public class IssueItemViewModel : ReactiveObject, ICanGoToViewModel
    {
        public IReactiveCommand<object> GoToCommand { get; } = ReactiveCommand.Create();

        public string Title { get; }

        public string Status { get; }

        public string Assigned { get; }

        public string Priority { get; }

        public string LastUpdated { get; }

        public string Kind { get; }

        public string Id { get; }

        public IssueItemViewModel(Issue issue)
        {
            Id = issue.LocalId.ToString();
            Title = issue.Title;
            Status = issue.Status;
            Assigned = issue.Responsible?.Username ?? "unassigned";
            Priority = issue.Priority;
            LastUpdated = issue.UtcLastUpdated.Humanize();
            Kind = issue.Metadata?.Kind ?? "unknown";

            if (Kind == "enhancement")
                Kind = "enhance";
        }
    }
}

