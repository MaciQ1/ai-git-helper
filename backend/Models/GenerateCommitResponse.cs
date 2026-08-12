namespace CommitGenerator.Models;

public sealed record GenerateCommitResponse(
    string CommitMessage,
    string PullRequestDescription);
