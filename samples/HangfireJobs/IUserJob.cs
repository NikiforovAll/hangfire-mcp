namespace HangfireJobs;

public interface IUserJob
{
    public Task ExecuteAsync(Payload payload);
    public Task ExecuteAsync(string userName);
}
