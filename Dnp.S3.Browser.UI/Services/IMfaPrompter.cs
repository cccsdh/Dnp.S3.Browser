using System.Threading.Tasks;

namespace Dnp.S3.Browser.UI.Services
{
    public interface IMfaPrompter
    {
        // Prompt the user for an MFA code for the given device ARN, return the code or null if cancelled
        Task<string?> PromptForCodeAsync(string mfaArn);
    }
}
