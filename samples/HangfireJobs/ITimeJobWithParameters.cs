namespace HangfireJobs;

public interface ITimeJobWithParameters
{
    public Task ExecuteAsync(Payload payload);
    public Task ExecuteAsync(string userName);
}
