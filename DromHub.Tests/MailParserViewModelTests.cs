using System.Threading.Tasks;
using DromHub.ViewModels;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DromHub.Tests;

public class MailParserViewModelTests
{
    [Fact]
    public async Task ConnectAndDownloadCommand_UsesLatestPassword()
    {
        var viewModel = new TestMailParserViewModel
        {
            EmailAddress = "user@example.com",
            RememberCredentials = false
        };
        viewModel.UpdatePassword("s3cret");

        await viewModel.ConnectAndDownloadCommand.ExecuteAsync(null);

        Assert.Equal("user@example.com", viewModel.CapturedEmail);
        Assert.Equal("s3cret", viewModel.CapturedPassword);
    }

    private sealed class TestMailParserViewModel : MailParserViewModel
    {
        public string? CapturedPassword { get; private set; }
        public string? CapturedEmail { get; private set; }

        public TestMailParserViewModel()
            : base(NullLogger<MailParserViewModel>.Instance)
        {
        }

        protected override Task ConnectAsync(ImapClient client, string server, int port, SecureSocketOptions options)
        {
            // Skip network interaction during tests.
            return Task.CompletedTask;
        }

        protected override Task AuthenticateAsync(ImapClient client, string email, string password)
        {
            CapturedEmail = email;
            CapturedPassword = password;
            return Task.CompletedTask;
        }

        protected override Task ProcessInboxAsync(ImapClient client)
        {
            // The test only verifies credential flow, so skip mailbox work.
            return Task.CompletedTask;
        }

        protected override Task DisconnectAsync(ImapClient client)
        {
            return Task.CompletedTask;
        }
    }
}
