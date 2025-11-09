using Polly;
using Polly.Retry;
using System;
using System.Net.Sockets;

public static class RetryPolicies
{
    public static AsyncRetryPolicy DefaultPolicy() =>
        Policy.Handle<Exception>(ex => ex is SocketException || ex is System.IO.IOException)
              .WaitAndRetryAsync(new[]
              {
                  TimeSpan.FromSeconds(2),
                  TimeSpan.FromSeconds(5),
                  TimeSpan.FromSeconds(10)
              }, (ex, ts, count, ctx) => {
                  // logging hook can go here
              });
}
