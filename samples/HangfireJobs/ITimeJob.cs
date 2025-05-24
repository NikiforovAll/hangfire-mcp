namespace HangfireJobs;

public interface ITimeJob
{
    public Task ExecuteAsync();
}
