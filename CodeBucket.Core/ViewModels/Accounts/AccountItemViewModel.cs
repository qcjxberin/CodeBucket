﻿using CodeBucket.Core.Data;
using ReactiveUI;

namespace CodeBucket.Core.ViewModels.Accounts
{
    public class AccountItemViewModel : ReactiveObject, ICanGoToViewModel
    {
        private bool _selected;
        public bool Selected
        {
            get { return _selected; }
            internal set { this.RaiseAndSetIfChanged(ref _selected, value); }
        }

        public string Username { get; }

        public string AvatarUrl { get; }

        public string Domain { get; }

        public IReactiveCommand<object> DeleteCommand { get; } = ReactiveCommand.Create();

        public IReactiveCommand<object> GoToCommand { get; } = ReactiveCommand.Create();

        internal AccountItemViewModel(BitbucketAccount account)
        {
            Username = account.Username;
            AvatarUrl = account.AvatarUrl;
            Domain = "https://bitbucket.org";
        }
    }
}

