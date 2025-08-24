namespace ReceptRegister.Tests;

public static class TestPathHelpers
{
    public const string FrontendTempPrefix = "rr_frontendtests_";
    public const string ApiTempPrefix = "rr_apitests_";

    public static string NewFrontendTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), FrontendTempPrefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    public static string NewApiTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), ApiTempPrefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
