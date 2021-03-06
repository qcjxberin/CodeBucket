using CodeBucket.Core.ViewModels.Repositories;
using System.Threading.Tasks;
using CodeBucket.Core.ViewModels.Source;
using System;
using CodeBucket.Core.ViewModels.Users;
using CodeBucket.Core.Services;
using System.Reactive.Linq;
using System.Reactive;
using System.Linq;
using ReactiveUI;
using Splat;
using CodeBucket.Core.Utils;
using CodeBucket.Core.ViewModels.Comments;
using Humanizer;
using CodeBucket.Client;

namespace CodeBucket.Core.ViewModels.Commits
{
    public class CommitViewModel : BaseViewModel, ILoadableViewModel
    {
        private readonly ReactiveList<CommitComment> _comments = new ReactiveList<CommitComment>();
        private readonly IApplicationService _applicationService;

        public string Username { get; }

        public string Repository { get; }

        public string Node { get; }

        public bool ShowRepository { get; }

        public IReactiveCommand<Unit> LoadCommand { get; }

        public IReactiveCommand<Unit> ToggleApproveButton { get; }

        public IReactiveCommand<object> GoToUserCommand { get; } = ReactiveCommand.Create();

        public IReactiveCommand<object> GoToRepositoryCommand { get; } = ReactiveCommand.Create();

        public IReactiveCommand<object> GoToAddedFiles { get; }

        public IReactiveCommand<object> GoToRemovedFiles { get; }

        public IReactiveCommand<object> GoToModifiedFiles { get; }

        public IReactiveCommand<object> GoToAllFiles { get; } = ReactiveCommand.Create();

        public IReactiveCommand<object> ShowMenuCommand { get; }

        public IReactiveCommand<object> AddCommentCommand { get; } = ReactiveCommand.Create();

        public IReadOnlyReactiveList<CommitFileItemViewModel> CommitFiles { get; }

        private readonly ObservableAsPropertyHelper<UserItemViewModel[]> _approvals;
        public UserItemViewModel[] Approvals => _approvals.Value;

        private Client.V1.Changeset _changeset;
        public Client.V1.Changeset Changeset
        {
            get { return _changeset; }
            private set { this.RaiseAndSetIfChanged(ref _changeset, value); }
        }

        private Commit _commit;
        public Commit Commit
		{
			get { return _commit; }
            private set { this.RaiseAndSetIfChanged(ref _commit, value); }
		}

        public IReadOnlyReactiveList<CommentItemViewModel> Comments { get; }

        private readonly ObservableAsPropertyHelper<int> _diffAdditions;
        public int DiffAdditions => _diffAdditions.Value;

        private readonly ObservableAsPropertyHelper<int> _diffDeletions;
        public int DiffDeletions => _diffDeletions.Value;

        private readonly ObservableAsPropertyHelper<int> _diffModifications;
        public int DiffModifications => _diffModifications.Value;

        private bool _approved;
        public bool Approved
        {
            get { return _approved; }
            private set { this.RaiseAndSetIfChanged(ref _approved, value); }
        }

        public NewCommentViewModel NewCommentViewModel { get; }

        public CommitViewModel(
            string username, string repository, Commit commit, bool showRepository = false,
            IApplicationService applicationService = null, IActionMenuService actionMenuService = null,
            IAlertDialogService alertDialogService = null)
            : this(username, repository, commit.Hash, showRepository, applicationService, 
                   actionMenuService, alertDialogService)
        {
            Commit = commit;
        }

        public CommitViewModel(
            string username, string repository, string node, bool showRepository = false,
            IApplicationService applicationService = null, IActionMenuService actionMenuService = null,
            IAlertDialogService alertDialogService = null)
        {
            _applicationService = applicationService = applicationService ?? Locator.Current.GetService<IApplicationService>();
            actionMenuService = actionMenuService ?? Locator.Current.GetService<IActionMenuService>();
            alertDialogService = alertDialogService ?? Locator.Current.GetService<IAlertDialogService>();

            Username = username;
            Repository = repository;
            Node = node;
            ShowRepository = showRepository;

            var shortNode = node.Substring(0, node.Length > 7 ? 7 : node.Length);
            Title = $"Commit {shortNode}";

            Comments = _comments.CreateDerivedCollection(comment =>
            {
                var name = comment.User.DisplayName ?? comment.User.Username;
                var avatar = new Avatar(comment.User.Links?.Avatar?.Href);
                return new CommentItemViewModel(name, avatar, comment.CreatedOn.Humanize(), comment.Content.Raw);
            });

            GoToUserCommand
                .OfType<string>()
                .Select(x => new UserViewModel(x))
                .Subscribe(NavigateTo);

            GoToRepositoryCommand
                .Select(_ => new RepositoryViewModel(username, repository))
                .Subscribe(NavigateTo);

            GoToAddedFiles = ReactiveCommand.Create(
                this.WhenAnyValue(x => x.DiffAdditions).Select(x => x > 0));

            GoToRemovedFiles = ReactiveCommand.Create(
                this.WhenAnyValue(x => x.DiffDeletions).Select(x => x > 0));

            GoToModifiedFiles = ReactiveCommand.Create(
                this.WhenAnyValue(x => x.DiffModifications).Select(x => x > 0));

            var canShowMenu = this.WhenAnyValue(x => x.Commit).Select(x => x != null);

            ShowMenuCommand = ReactiveCommand.Create(canShowMenu);
            ShowMenuCommand.Subscribe(sender =>
            {
                var uri = new Uri($"https://bitbucket.org/{username}/{repository}/commits/{node}");
                var menu = actionMenuService.Create();
                menu.AddButton("Add Comment", AddCommentCommand);
                menu.AddButton("Copy SHA", () => actionMenuService.SendToPasteBoard(node));
                menu.AddButton("Share", () => actionMenuService.ShareUrl(sender, uri));
                menu.AddButton("Show In Bitbucket", () => NavigateTo(new WebBrowserViewModel(uri.AbsoluteUri)));
                menu.Show(sender);
            });

            ToggleApproveButton = ReactiveCommand.CreateAsyncTask(async _ => 
            {
                if (Approved)
                    await applicationService.Client.Commits.Unapprove(username, repository, node);
                else
                    await applicationService.Client.Commits.Approve(username, repository, node);

                var shouldBe = !Approved;
                var commit = await applicationService.Client.Commits.Get(username, repository, node);
                var currentUsername = applicationService.Account.Username;
                var me = commit.Participants.FirstOrDefault(
                    y => string.Equals(currentUsername, y?.User?.Username, StringComparison.OrdinalIgnoreCase));
                if (me != null)
                    me.Approved = shouldBe;
                Commit = commit;
            });

            ToggleApproveButton
                .ThrownExceptions
                .Subscribe(x => alertDialogService.Alert("Error", "Unable to approve commit: " + x.Message).ToBackground());

            var changesetFiles = 
                this.WhenAnyValue(x => x.Changeset)
                .IsNotNull()
                .Select(x => x.Files ?? Enumerable.Empty<Client.V1.ChangesetFile>());

            changesetFiles
                .Select(x => x.Count(y => y.Type == FileModification.Added))
                .ToProperty(this, x => x.DiffAdditions, out _diffAdditions);

            changesetFiles
                .Select(x => x.Count(y => y.Type == FileModification.Removed))
                .ToProperty(this, x => x.DiffDeletions, out _diffDeletions);

            changesetFiles
                .Select(x => x.Count(y => y.Type == FileModification.Modified))
                .ToProperty(this, x => x.DiffModifications, out _diffModifications);

            var commitFiles = new ReactiveList<Client.V1.ChangesetFile>();
            CommitFiles = commitFiles.CreateDerivedCollection(x =>
            {
                var vm = new CommitFileItemViewModel(username, repository, node, Changeset.Parents.FirstOrDefault(), x);
                vm.GoToCommand
                  .Select(_ => new ChangesetDiffViewModel(username, repository, node, x))
                  .Subscribe(NavigateTo);
                return vm;
            });

            this.WhenAnyValue(x => x.Commit.Participants)
                .Select(participants =>
                {
                    return (participants ?? Enumerable.Empty<CommitParticipant>())
                        .Where(x => x.Approved)
                        .Select(x =>
                        {
                            var avatar = new Avatar(x.User?.Links?.Avatar?.Href);
                            var vm = new UserItemViewModel(x.User?.Username, x.User?.DisplayName, avatar);
                            vm.GoToCommand
                              .Select(_ => new UserViewModel(x.User))
                              .Subscribe(NavigateTo);
                            return vm;
                        });
                })
                .Select(x => x.ToArray())
                .ToProperty(this, x => x.Approvals, out _approvals, new UserItemViewModel[0]);

            this.WhenAnyValue(x => x.Changeset)
                .Subscribe(x => commitFiles.Reset(x?.Files ?? Enumerable.Empty<Client.V1.ChangesetFile>()));

            this.WhenAnyValue(x => x.Commit)
                .Subscribe(x => 
                {
                    var currentUsername = applicationService.Account.Username;
                    Approved = x?.Participants
                        ?.FirstOrDefault(y => string.Equals(currentUsername, y?.User?.Username, StringComparison.OrdinalIgnoreCase))
                        ?.Approved ?? false;
                });

            LoadCommand = ReactiveCommand.CreateAsyncTask(_ => 
            {
                var commit = applicationService.Client.Commits.Get(username, repository, node)
                    .OnSuccess(x => Commit = x);
                var changeset = applicationService.Client.Commits.GetChangeset(username, repository, node)
                    .OnSuccess(x => Changeset = x);
                applicationService.Client.AllItems(x => x.Commits.GetComments(username, repository, node))
                    .ToBackground(_comments.Reset);
                return Task.WhenAll(commit, changeset);
            });
        }

        public async Task AddComment(string text)
        {
            var model = new NewChangesetComment { Content = text };
            var comment = await _applicationService.Client.Commits.CreateComment(Username, Repository, Node, model);
            _comments.Add(new CommitComment
            {
                CreatedOn = comment.UtcCreatedOn,
                Content = new CommitCommentContent
                {
                    Raw = comment.Content,
                    Html = comment.ContentRendered
                },
                User = new User
                {
                    DisplayName = comment.DisplayName,
                    Username = comment.Username,
                    Links = new User.UserLinks
                    {
                        Avatar = new Link(comment.UserAvatarUrl)
                    }
                }
            });
        }
    }
}

