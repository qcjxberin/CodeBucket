using System;
using UIKit;
using Foundation;
using System.Collections.Generic;
using CodeBucket.Core.ViewModels.Source;
using System.Linq;
using Newtonsoft.Json;
using WebKit;
using CodeBucket.Utilities;
using CodeBucket.Services;
using System.Reactive.Linq;
using System.Reactive;
using CodeBucket.Client.V1;
using ReactiveUI;
using CodeBucket.Views;

namespace CodeBucket.ViewControllers.Source
{
    public class ChangesetDiffViewController : WebViewController<ChangesetDiffViewModel>
    {
        private readonly SubtitleTitleView _titleView = new SubtitleTitleView();
		private bool _domLoaded = false;
		private List<string> _toBeExecuted = new List<string>();

        public ChangesetDiffViewController()
        {
            NavigationItem.TitleView = _titleView;
        }

        public ChangesetDiffViewController(string username, string repository, string branch, ChangesetFile model)
            : this()
        {
            ViewModel = new ChangesetDiffViewModel(username, repository, branch, model);
        }

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();

            this.WhenAnyValue(x => x.ViewModel.Title, x => x.ViewModel.ChangeType)
                .Subscribe(x => _titleView.SetTitles(x.Item1, x.Item2));

            this.WhenAnyValue(x => x.ViewModel.BinaryFilePath)
                .IsNotNull()
                .Subscribe(x => LoadFile(x));

            this.WhenAnyValue(x => x.ViewModel.Patch)
                .Where(x => x != null)
                .Subscribe(x =>
            {
                var hunks = x.Select(y => new Hunk(y.OldStart, y.NewStart, y.Lines));
                var view = new DiffView { Model = new DiffViewModel(hunks) }.GenerateString();
                LoadContent(view);
            });
                

            //            ViewModel.Bind(x => x.IsLoading).Subscribe(x =>
            //			{
            //					if (!x && (ViewModel.File1 != null || ViewModel.File2 != null))
            //					{
            //						var sb = new StringBuilder(2000);
            //						sb.Append("a=\"");
            //						if (ViewModel.File1 != null)
            //							sb.Append(JavaScriptStringEncode(System.IO.File.ReadAllText(ViewModel.File1)));
            //						sb.Append("\";");
            //						sb.Append("b=\"");
            //						if (ViewModel.File2 != null)
            //							sb.Append(JavaScriptStringEncode(System.IO.File.ReadAllText(ViewModel.File2)));
            //						sb.Append("\";");
            //						sb.Append("diff(b,a);");
            //						ExecuteJavascript(sb.ToString());
            //					}
            //					else if (ViewModel.FilePath != null)
            //					{
            //						Web.LoadRequest(new NSUrlRequest(new NSUrl(new Uri("file://" + ViewModel.FilePath).AbsoluteUri)));
            //					}
            //			});

            //ViewModel
            //    .Comments
            //    .ChangedObservable()
            //     .Subscribe(comments =>
            //     {
            //         var slimComments = comments.Where(x => string.Equals(x.Filename, ViewModel.Filename)).Select(x => new
            //         {
            //             Id = x.CommentId,
            //             User = x.Username,
            //             Avatar = x.UserAvatarUrl,
            //             LineTo = x.LineTo,
            //             LineFrom = x.LineFrom,
            //             Content = x.ContentRendered,
            //             Date = x.UtcLastUpdated
            //         }).ToList();

            //         var c = JsonConvert.SerializeObject(slimComments);
            //         ExecuteJavascript("var a = " + c + "; setComments(a);");
            //     });
	    }

        private class JavascriptCommentModel
        {
			public int? LineFrom { get; set; }
			public int? LineTo { get; set; }
        }

        public override bool ShouldStartLoad(WKWebView webView, WKNavigationAction navigationAction)
        {
            var url = navigationAction.Request.Url;
			if(url != null && url.Scheme.Equals("app")) {
                var func = url.Host;

				if (func.Equals("ready"))
				{
					_domLoaded = true;
					foreach (var e in _toBeExecuted)
                        webView.EvaluateJavaScript(e, null);
				}
				else if(func.Equals("comment")) 
				{
                    //var commentModel = JsonConvert.DeserializeObject<JavascriptCommentModel>(UrlDecode(url.Fragment));
					//PromptForComment(commentModel);
                }

				return false;
            }

            return base.ShouldStartLoad(webView, navigationAction);
        }

		private void ExecuteJavascript(string data)
		{
			if (_domLoaded)
                InvokeOnMainThread(() => Web.EvaluateJavaScript(data, null));
			else
				_toBeExecuted.Add(data);
		}

        private void PromptForComment(JavascriptCommentModel model)
        {
			string title = "Line " + (model.LineFrom ?? model.LineTo);

            var sheet = new UIActionSheet(title);
            var addButton = sheet.AddButton("Add Comment");
            var cancelButton = sheet.AddButton("Cancel");
            sheet.CancelButtonIndex = cancelButton;
            sheet.DismissWithClickedButtonIndex(cancelButton, true);
            sheet.Dismissed += (sender, e) => {
                BeginInvokeOnMainThread(() =>
                {
                    if (e.ButtonIndex == addButton)
    					ShowCommentComposer(model.LineFrom, model.LineTo);
                });

                sheet.Dispose();
            };

            sheet.ShowInView(this.View);
        }

		private void ShowCommentComposer(int? lineFrom, int? lineTo)
        {
   //         var composer = new ComposerViewController();
			//composer.NewComment(this, async (text) => {
			//	try
			//	{
			//		await composer.DoWorkAsync("Commenting...", () => ViewModel.PostComment(text, lineFrom, lineTo));
			//		composer.CloseComposer();
			//	}
			//	catch (Exception e)
			//	{
			//		AlertDialogService.ShowAlert("Unable to Comment", e.Message);
			//		composer.EnableSendButton = true;
			//	}
   //         });
        }
    }
}

