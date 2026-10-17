namespace Yusay.AdminCli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new AdminCliApp();
        return await app.RunAsync(args);
    }
}
