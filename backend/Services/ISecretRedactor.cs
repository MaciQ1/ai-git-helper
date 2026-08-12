namespace CommitGenerator.Services;

public interface ISecretRedactor
{
    string Redact(string value);
}
