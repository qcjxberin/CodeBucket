using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using CodeBucket.Client.V1;
using CodeBucket.Core.Services;
using ReactiveUI;
using Splat;

namespace CodeBucket.Core.ViewModels.Source
{
    public class ChangesetDiffViewModel : BaseViewModel, ILoadableViewModel
    {
		private ChangesetFile _commitFileModel;

        private string _binaryFilePath;
		public string BinaryFilePath
        {
            get { return _binaryFilePath; }
            private set { this.RaiseAndSetIfChanged(ref _binaryFilePath, value); }
        }

        public ReactiveList<ChangesetComment> Comments { get; } = new ReactiveList<ChangesetComment>();

        public IReactiveCommand<Unit> LoadCommand { get; }

        private string _changeType;
        public string ChangeType
        {
            get { return _changeType; }
            private set { this.RaiseAndSetIfChanged(ref _changeType, value); }
        }

        private List<Hunk> _patch;
        public List<Hunk> Patch
        {
            get { return _patch; }
            private set { this.RaiseAndSetIfChanged(ref _patch, value); }
        }

        public ChangesetDiffViewModel(
            string username, string repository, string branch, ChangesetFile model)
            : this(username, repository, branch, model.File)
        {
            _commitFileModel = model;
            ChangeType = model.Type.ToString();
        }

        public ChangesetDiffViewModel(
            string username, string repository, string node, string filename,
            IApplicationService applicationService = null, IDiffService diffService = null)
        {
            applicationService = applicationService ?? Locator.Current.GetService<IApplicationService>();
            diffService = diffService ?? Locator.Current.GetService<IDiffService>();

            var actualFilename = Path.GetFileName(filename);
            if (actualFilename == null)
                actualFilename = filename.Substring(filename.LastIndexOf('/') + 1);

            Title = actualFilename;

            LoadCommand = ReactiveCommand.CreateAsyncTask(async t =>
            {
                Patch = null;
                BinaryFilePath = null;

                var currentFilePath = Path.Combine(Path.GetTempPath(), actualFilename);
                var hasCurrent = _commitFileModel.Type == Client.FileModification.Added || _commitFileModel.Type == Client.FileModification.Modified;
                var hasPast = _commitFileModel.Type == Client.FileModification.Removed || _commitFileModel.Type == Client.FileModification.Modified;
                var isBinary = false;

                if (hasCurrent)
                {
                    using (var stream = new FileStream(currentFilePath, FileMode.Create, FileAccess.Write))
                    {
                        await applicationService.Client.Repositories.GetRawFile(username, repository, node, filename, stream);
                    }

                    using (var stream = new FileStream(currentFilePath, FileMode.Open, FileAccess.Read))
                    {
                        var buffer = new byte[1024];
                        var read = stream.Read(buffer, 0, 1024);
                        isBinary = buffer.Take(read).Any(x => x == 0);
                    }
                }

                if (isBinary)
                {
                    BinaryFilePath = currentFilePath;
                    return;
                }

                var parentFilePath = actualFilename + ".parent";
                var pastFilePath = Path.Combine(Path.GetTempPath(), parentFilePath);

                if (hasPast)
                {
                    var changeset = await applicationService.Client.Commits.GetChangeset(username, repository, node);
                    var parent = changeset.Parents?.FirstOrDefault();
                    if (parent == null)
                      throw new Exception("Diff has no parent. Unable to generate view.");
                    

                    using (var stream = new FileStream(pastFilePath, FileMode.Create, FileAccess.Write))
                    {
                        await applicationService.Client.Repositories.GetRawFile(username, repository, parent, filename, stream);
                    }

                    using (var stream = new FileStream(pastFilePath, FileMode.Open, FileAccess.Read))
                    {
                        var buffer = new byte[1024];
                        var read = stream.Read(buffer, 0, 1024);
                        isBinary = buffer.Take(read).Any(x => x == 0);
                    }
                }

                if (isBinary)
                {
                    BinaryFilePath = currentFilePath;
                    return;
                }

                string newText = null, oldText = null;

                if (hasCurrent)
                    newText = await Task.Run(() => File.ReadAllText(currentFilePath));

                if (hasPast)
                    oldText = await Task.Run(() => File.ReadAllText(pastFilePath));

                Patch = diffService.CreateDiff(oldText, newText, 5).ToList();

            });
        }

		public async Task PostComment(string comment, int? lineFrom, int? lineTo)
		{
//			var c = await Task.Run(() => this.GetApplication().Client.Users[Username].Repositories[Repository].Changesets[Branch].Comments.Create(comment, lineFrom, lineTo, filename: Filename));
//			Comments.Items.Add(c);
		}
    }
}

