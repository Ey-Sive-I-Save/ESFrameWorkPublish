using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace ES.Tests
{
    public sealed class ESDialogContractTests
    {
        [Test]
        public async Task ExplicitHost_RoutesToMatchingPresenter()
        {
            var presenter = new TestPresenter(ESDialogHost.Runtime);
            using (ESDialog.RegisterPresenter(presenter))
            {
                ESDialogResult result = await ESDialog.ShowAsync(new ESDialogRequest
                {
                    DialogId = "tests.dialog.explicit-host",
                    Host = ESDialogHost.Runtime,
                    Title = "Test",
                });

                Assert.That(result.Accepted, Is.True);
                Assert.That(result.Host, Is.EqualTo(ESDialogHost.Runtime));
                Assert.That(presenter.ShowCount, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task Auto_WithTwoPresenters_ReturnsAmbiguousHost()
        {
            ESDialogPresenterLease optionalEditorLease = TryRegisterEditorPresenter();
            try
            {
                using (ESDialog.RegisterPresenter(new TestPresenter(ESDialogHost.Runtime)))
                {
                    ESDialogResult result = await ESDialog.ShowAsync(new ESDialogRequest
                    {
                        DialogId = "tests.dialog.ambiguous",
                        Title = "Test",
                    });

                    Assert.That(result.Completion, Is.EqualTo(ESDialogCompletion.AmbiguousHost));
                }
            }
            finally
            {
                optionalEditorLease?.Dispose();
            }
        }

        [Test]
        public void DuplicateHostRegistration_IsRejected()
        {
            using (ESDialog.RegisterPresenter(new TestPresenter(ESDialogHost.Runtime)))
            {
                Assert.Throws<InvalidOperationException>(() =>
                    ESDialog.RegisterPresenter(new TestPresenter(ESDialogHost.Runtime)));
            }
        }

        [Test]
        public void LeaseRelease_StopsOnlyItsPresenter()
        {
            var first = new TestPresenter(ESDialogHost.Runtime);
            ESDialogPresenterLease firstLease = ESDialog.RegisterPresenter(first);
            long firstGeneration = firstLease.Generation;
            firstLease.Dispose();

            var second = new TestPresenter(ESDialogHost.Runtime);
            using (ESDialogPresenterLease secondLease = ESDialog.RegisterPresenter(second))
            {
                firstLease.Dispose();
                Assert.That(secondLease.Generation, Is.GreaterThan(firstGeneration));
                Assert.That(secondLease.IsActive, Is.True);
                Assert.That(first.StopCount, Is.EqualTo(1));
                Assert.That(second.StopCount, Is.EqualTo(0));
            }
            Assert.That(second.StopCount, Is.EqualTo(1));
        }

        [Test]
        public async Task SubmittedRequest_IsDeepSnapshot()
        {
            var presenter = new TestPresenter(ESDialogHost.Runtime);
            using (ESDialog.RegisterPresenter(presenter))
            {
                var request = new ESDialogRequest
                {
                    DialogId = "tests.dialog.snapshot",
                    Host = ESDialogHost.Runtime,
                    Title = "Before",
                };
                ESDialogField field = request.AddChoice(
                    "mode",
                    "Mode",
                    new[] { new ESDialogOption("before", "Before") },
                    "before");

                Task<ESDialogResult> task = ESDialog.ShowAsync(request);
                request.Title = "After";
                field.Options.Add(new ESDialogOption("after", "After"));
                await task;

                Assert.That(presenter.LastRequest.Title, Is.EqualTo("Before"));
                Assert.That(presenter.LastRequest.Fields[0].Options.Count, Is.EqualTo(1));
            }
        }

        private sealed class TestPresenter : IESDialogPresenter
        {
            internal int ShowCount;
            internal int StopCount;
            internal ESDialogRequest LastRequest;

            internal TestPresenter(ESDialogHost host)
            {
                Host = host;
            }

            public ESDialogHost Host { get; }
            public ESDialogCapabilities Capabilities => ESDialogCapabilities.AllCommon;

            public Task<ESDialogResult> ShowAsync(
                ESDialogRequest request,
                CancellationToken cancellationToken)
            {
                ShowCount++;
                LastRequest = request;
                return Task.FromResult(new ESDialogResult(
                    ESDialogCompletion.Accepted,
                    Host));
            }

            public void Stop(ESDialogPresenterStopReason reason)
            {
                StopCount++;
            }
        }

        private static ESDialogPresenterLease TryRegisterEditorPresenter()
        {
            try
            {
                return ESDialog.RegisterPresenter(new TestPresenter(ESDialogHost.Editor));
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
